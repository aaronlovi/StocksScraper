using System.Collections.Generic;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Stocks.DataModels;
using Stocks.Persistence.Database;
using Stocks.Persistence.Services;
using Stocks.Shared;
using Stocks.WebApi.Middleware;

namespace Stocks.WebApi.Endpoints;

public static class StatementEndpoints {
    public static void MapStatementEndpoints(this IEndpointRouteBuilder app) {
        _ = app.MapGet("/api/companies/{cik}/submissions/{submissionId}/statements",
            async (string cik, ulong submissionId, IDbmService dbm, StatementDataService sds, CancellationToken ct) => {
                Result<Company> companyResult = await dbm.GetCompanyByCik(cik, ct);
                if (companyResult.IsFailure)
                    return companyResult.ToHttpResult();

                // Derive taxonomy year from submission report date
                Result<IReadOnlyCollection<Submission>> subsResult =
                    await dbm.GetSubmissionsByCompanyId(companyResult.Value!.CompanyId, ct);
                if (subsResult.IsFailure)
                    return subsResult.ToHttpResult();

                Submission? matchedSub = null;
                foreach (Submission s in subsResult.Value!) {
                    if (s.SubmissionId == submissionId) {
                        matchedSub = s;
                        break;
                    }
                }
                if (matchedSub is null)
                    return Results.NotFound(new { error = $"Submission {submissionId} not found for company." });

                int taxonomyYear = matchedSub.ReportDate.Year;
                Result<TaxonomyTypeInfo> taxResult =
                    await dbm.GetTaxonomyTypeByNameVersion("us-gaap", taxonomyYear, ct);
                if (taxResult.IsFailure)
                    return Results.NotFound(new { error = $"No taxonomy found for year {taxonomyYear}." });

                Result<IReadOnlyCollection<StatementListItem>> listResult =
                    await sds.ListStatementsForSubmission(
                        companyResult.Value!.CompanyId, submissionId,
                        taxResult.Value!.TaxonomyTypeId, ct);
                if (listResult.IsFailure)
                    return listResult.ToHttpResult();

                return Results.Ok(listResult.Value);
            });

        _ = app.MapGet("/api/companies/{cik}/submissions/{submissionId}/statements/{concept}",
            async (string cik, ulong submissionId, string concept,
                   int? maxDepth, int? taxonomyYear, string? roleName,
                   IDbmService dbm, StatementDataService sds, CancellationToken ct) => {
                Result<Company> companyResult = await dbm.GetCompanyByCik(cik, ct);
                if (companyResult.IsFailure)
                    return companyResult.ToHttpResult();

                Company company = companyResult.Value!;

                // Find the submission to get report date for taxonomy year
                Result<IReadOnlyCollection<Submission>> subsResult =
                    await dbm.GetSubmissionsByCompanyId(company.CompanyId, ct);
                if (subsResult.IsFailure)
                    return subsResult.ToHttpResult();

                Submission? matchedSub = null;
                foreach (Submission s in subsResult.Value!) {
                    if (s.SubmissionId == submissionId) {
                        matchedSub = s;
                        break;
                    }
                }
                if (matchedSub is null)
                    return Results.NotFound(new { error = $"Submission {submissionId} not found for company." });

                int resolvedTaxYear = taxonomyYear ?? matchedSub.ReportDate.Year;
                Result<TaxonomyTypeInfo> taxResult =
                    await dbm.GetTaxonomyTypeByNameVersion("us-gaap", resolvedTaxYear, ct);
                if (taxResult.IsFailure)
                    return Results.NotFound(new { error = $"No taxonomy found for year {resolvedTaxYear}." });

                int taxonomyTypeId = taxResult.Value!.TaxonomyTypeId;
                int depth = maxDepth ?? 10;

                Result<StatementData> dataResult = await sds.GetStatementData(
                    company.CompanyId, submissionId, concept, taxonomyTypeId, depth, roleName, ct);
                if (dataResult.IsFailure) {
                    if (dataResult.ErrorMessage is not null && dataResult.ErrorMessage.Contains("Multiple presentation roles")) {
                        // Extract available roles and return them so the client can prompt
                        Result<IReadOnlyCollection<StatementListItem>> rolesResult =
                            await sds.ListStatements(taxonomyTypeId, ct);
                        var availableRoles = new List<object>();
                        if (rolesResult.IsSuccess) {
                            foreach (StatementListItem item in rolesResult.Value!) {
                                if (item.RootConceptName.EqualsOrdinalIgnoreCase(concept))
                                    availableRoles.Add(new { roleName = item.RoleName, rootLabel = item.RootLabel });
                            }
                        }
                        return Results.Json(new { error = "Multiple roles available. Specify roleName.", roles = availableRoles },
                            statusCode: 300);
                    }
                    return dataResult.ToHttpResult();
                }

                StatementData data = dataResult.Value!;
                object? jsonTree = BuildJsonTree(data.ChildrenMap, data.RootNodes,
                    data.IncludedConceptIds, data.DataPointMap, null);

                return Results.Ok(jsonTree);
            });
    }

    private static object? BuildJsonTree(
        Dictionary<long, List<HierarchyNode>> childrenMap,
        List<HierarchyNode> rootNodes,
        HashSet<long> includedConceptIds,
        Dictionary<long, DataPoint> dataPointMap,
        long? parentId) {
        var result = new List<object>();
        List<HierarchyNode> nodesToRender = rootNodes;
        if (parentId.HasValue) {
            if (!childrenMap.TryGetValue(parentId.Value, out List<HierarchyNode>? children))
                return result;
            nodesToRender = children;
        }
        foreach (HierarchyNode node in nodesToRender) {
            if (!includedConceptIds.Contains(node.ConceptId))
                continue;
            var obj = new Dictionary<string, object?> {
                ["conceptName"] = node.Name,
                ["label"] = node.Label,
                ["value"] = dataPointMap.TryGetValue(node.ConceptId, out DataPoint? dp) ? dp.Value : null
            };
            object? nodeChildren = BuildJsonTree(childrenMap, rootNodes, includedConceptIds, dataPointMap, node.ConceptId);
            if (nodeChildren is List<object> list && list.Count > 0)
                obj["children"] = nodeChildren;
            result.Add(obj);
        }
        if (parentId == null && result.Count == 1)
            return result[0];
        return result;
    }
}
