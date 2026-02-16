using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Stocks.DataModels;
using Stocks.EDGARScraper.Models.Statements;
using Stocks.Persistence.Database;
using Stocks.Persistence.Database.DTO.Taxonomies;
using Stocks.Shared;
using Stocks.Shared.Models;

namespace Stocks.EDGARScraper.Services.Statements;

/// <summary>
/// Responsible for rendering a financial statement or taxonomy concept hierarchy for a company.
/// </summary>
public class StatementPrinter {
    private readonly IDbmService _dbmService;
    private readonly TextWriter _stdout;
    private readonly TextWriter _stderr;

    // Parameters
    private readonly string _cik;
    private readonly string _concept;
    private readonly DateOnly _date;
    private readonly int _maxDepth;
    private readonly string _format;
    private readonly bool _listStatements;
    private readonly string? _roleName;
    private readonly int _taxonomyTypeId;
    private readonly CancellationToken _ct;
    private int _htmlRowIndex;

    public StatementPrinter(
        IDbmService dbmService,
        string cik,
        string concept,
        DateOnly date,
        int maxDepth,
        string format,
        string? roleName,
        bool listStatements,
        int taxonomyTypeId,
        TextWriter? stdout = null,
        TextWriter? stderr = null,
        CancellationToken ct = default) {
        _dbmService = dbmService;
        _cik = cik;
        _concept = concept;
        _date = date;
        _maxDepth = maxDepth;
        _format = format;
        _roleName = roleName;
        _listStatements = listStatements;
        _taxonomyTypeId = taxonomyTypeId;
        _stdout = stdout ?? Console.Out;
        _stderr = stderr ?? Console.Error;
        _ct = ct;
    }

    /// <summary>
    /// Main entry point for rendering the statement or listing available statements.
    /// </summary>
    public async Task<int> PrintStatement() {
        // Only support EdgarDataSource for now
        const string DataSource = "EDGAR";
        if (!ulong.TryParse(_cik, out ulong cikNum)) {
            await _stderr.WriteLineAsync($"ERROR: Invalid CIK '{_cik}'.");
            return 2;
        }
        // TODO: Refactor to query for a single company by CIK in the database for better performance with large datasets.
        Result<IReadOnlyCollection<Company>> companiesResult = await _dbmService.GetAllCompaniesByDataSource(DataSource, _ct);
        if (companiesResult.IsFailure || companiesResult.Value is null) {
            await _stderr.WriteLineAsync($"ERROR: Could not load companies from data source '{DataSource}'.");
            return 2;
        }
        Company? company = null;
        foreach (Company c in companiesResult.Value) {
            if (c.Cik == cikNum) {
                company = c;
                break;
            }
        }
        if (company is null) {
            await _stderr.WriteLineAsync($"ERROR: Company with CIK '{_cik}' not found.");
            return 2;
        }

        // 2. Load taxonomy concepts
        Result<IReadOnlyCollection<ConceptDetailsDTO>> conceptsResult = await _dbmService.GetTaxonomyConceptsByTaxonomyType(_taxonomyTypeId, _ct);
        if (conceptsResult.IsFailure || conceptsResult.Value is null) {
            await _stderr.WriteLineAsync($"ERROR: Could not load taxonomy concepts for taxonomy type {_taxonomyTypeId}.");
            return 2;
        }
        IReadOnlyCollection<ConceptDetailsDTO> concepts = conceptsResult.Value;

        // 3. Load presentation hierarchy for role-scoped traversal
        Result<IReadOnlyCollection<PresentationDetailsDTO>> presentationsResult =
            await _dbmService.GetTaxonomyPresentationsByTaxonomyType(_taxonomyTypeId, _ct);
        if (presentationsResult.IsFailure || presentationsResult.Value is null) {
            await _stderr.WriteLineAsync($"ERROR: Could not load taxonomy presentation hierarchy for taxonomy type {_taxonomyTypeId}.");
            return 2;
        }
        IReadOnlyCollection<PresentationDetailsDTO> presentations = presentationsResult.Value;

        if (_listStatements) {
            var roleRoots = new Dictionary<string, long>();
            foreach (PresentationDetailsDTO p in presentations) {
                if (p.Depth != 1)
                    continue;
                if (string.IsNullOrWhiteSpace(p.RoleName))
                    continue;
                if (!roleRoots.ContainsKey(p.RoleName))
                    roleRoots[p.RoleName] = p.ConceptId;
            }

            await _stdout.WriteLineAsync("RoleName,RootConceptName,RootLabel");
            if (roleRoots.Count == 0)
                return 0;
            foreach ((string roleName, long rootConceptId) in roleRoots) {
                ConceptDetailsDTO? root = null;
                foreach (ConceptDetailsDTO c in concepts) {
                    if (c.ConceptId == rootConceptId) {
                        root = c;
                        break;
                    }
                }
                if (root is null) {
                    await _stdout.WriteLineAsync($"{roleName},,");
                    continue;
                }
                await _stdout.WriteLineAsync($"{roleName},{root.Name},\"{root.Label}\"");
            }
            return 0;
        }

        ConceptDetailsDTO? rootConcept = null;
        foreach (ConceptDetailsDTO c in concepts) {
            if (c.Name.EqualsOrdinalIgnoreCase(_concept)) {
                rootConcept = c;
                break;
            }
            if (long.TryParse(_concept, out long conceptId) && c.ConceptId == conceptId) {
                rootConcept = c;
                break;
            }
        }
        if (rootConcept is null) {
            await _stderr.WriteLineAsync($"ERROR: Concept '{_concept}' not found in taxonomy.");
            return 2;
        }

        string? roleNameToUse = _roleName;
        if (string.IsNullOrWhiteSpace(roleNameToUse)) {
            var matchingRoles = new List<string>();
            foreach (PresentationDetailsDTO p in presentations) {
                if (p.ConceptId != rootConcept.ConceptId)
                    continue;
                if (string.IsNullOrWhiteSpace(p.RoleName))
                    continue;
                bool alreadyAdded = false;
                foreach (string role in matchingRoles) {
                    if (role.EqualsOrdinalIgnoreCase(p.RoleName)) {
                        alreadyAdded = true;
                        break;
                    }
                }
                if (!alreadyAdded)
                    matchingRoles.Add(p.RoleName);
            }
            if (matchingRoles.Count == 1) {
                roleNameToUse = matchingRoles[0];
            } else if (matchingRoles.Count == 0) {
                await _stderr.WriteLineAsync($"ERROR: No presentation role found for concept '{rootConcept.Name}'.");
                return 2;
            } else {
                await _stderr.WriteLineAsync($"ERROR: Multiple presentation roles found for concept '{rootConcept.Name}'. Please specify --role.");
                return 2;
            }
        }

        Dictionary<long, List<PresentationDetailsDTO>> parentToChildren =
            BuildParentToChildrenMap(presentations, roleNameToUse!);

        // 4. Find submission for the specified date
        Result<IReadOnlyCollection<Submission>> submissionsResult = await _dbmService.GetSubmissions(_ct);
        if (submissionsResult.IsFailure || submissionsResult.Value is null) {
            await _stderr.WriteLineAsync($"ERROR: Could not load submissions for company.");
            return 2;
        }
        Submission? selectedSubmission = null;
        foreach (Submission sub in submissionsResult.Value) {
            if (sub.CompanyId != company.CompanyId)
                continue;
            if (sub.ReportDate > _date)
                continue;
            if (selectedSubmission == null || sub.ReportDate > selectedSubmission.ReportDate)
                selectedSubmission = sub;
        }
        // If no submission found, and there are submissions for this company, but all are after the requested date, return error
        bool hasAnySubmissionForCompany = false;
        foreach (Submission sub in submissionsResult.Value) {
            if (sub.CompanyId == company.CompanyId) {
                hasAnySubmissionForCompany = true;
                break;
            }
        }
        if (selectedSubmission is null) {
            if (hasAnySubmissionForCompany) {
                await _stderr.WriteLineAsync($"ERROR: No submission found for CIK '{_cik}' on or before {_date:yyyy-MM-dd}.");
                return 2;
            } else {
                await _stderr.WriteLineAsync($"ERROR: No submissions exist for CIK '{_cik}'.");
                return 2;
            }
        }

        // 5. Load data points for the company and submission
        Result<IReadOnlyCollection<DataPoint>> dataPointsResult = await _dbmService.GetDataPointsForSubmission(company.CompanyId, selectedSubmission.SubmissionId, _ct);
        if (dataPointsResult.IsFailure || dataPointsResult.Value is null) {
            await _stderr.WriteLineAsync($"ERROR: Could not load data points for company/submission.");
            return 2;
        }
        var dataPointMap = new Dictionary<long, DataPoint>();
        foreach (DataPoint dp in dataPointsResult.Value) {
            dataPointMap[dp.TaxonomyConceptId] = dp;
        }

        // 6. Traverse the taxonomy tree and collect hierarchy nodes
        Dictionary<long, ConceptDetailsDTO> conceptMap = [];
        foreach (ConceptDetailsDTO c in concepts)
            conceptMap[c.ConceptId] = c;
        var ctx = new TraverseContext(
            rootConcept!.ConceptId,
            parentToChildren!,
            conceptMap,
            0,
            _maxDepth,
            null,
            []
        );
        Result<List<HierarchyNode>> hierarchyResult = await TraverseConceptTree(ctx);
        if (hierarchyResult.IsFailure || hierarchyResult.Value is null) {
            await _stderr.WriteLineAsync($"ERROR: {hierarchyResult.ErrorMessage}");
            return 2;
        }
        List<HierarchyNode> hierarchy = hierarchyResult.Value;

        // 7. Output in the requested format
        var childrenMap = new Dictionary<long, List<HierarchyNode>>();
        var rootNodes = new List<HierarchyNode>();
        BuildChildrenMap(hierarchy, childrenMap, rootNodes);
        var includedConceptIds = new HashSet<long>();
        foreach (HierarchyNode rootNode in rootNodes)
            _ = HasValueOrChildValue(rootNode.ConceptId, childrenMap, dataPointMap, includedConceptIds);
        if (includedConceptIds.Count == 0) {
            foreach (HierarchyNode rootNode in rootNodes) {
                if (!dataPointMap.ContainsKey(rootNode.ConceptId)) {
                    await _stderr.WriteLineAsync($"WARNING: No data point found for concept '{rootNode.Name}' (ConceptId: {rootNode.ConceptId}) in submission.");
                }
            }
        }
        await FormatOutput(hierarchy, conceptMap, dataPointMap, childrenMap, rootNodes, includedConceptIds);
        return 0;
    }

    private async Task FormatOutput(
        List<HierarchyNode> hierarchy,
        Dictionary<long, ConceptDetailsDTO> conceptMap,
        Dictionary<long, DataPoint> dataPointMap,
        Dictionary<long, List<HierarchyNode>> childrenMap,
        List<HierarchyNode> rootNodes,
        HashSet<long> includedConceptIds) {
        if (_format.Equals("csv", StringComparison.OrdinalIgnoreCase)) {
            await _stdout.WriteLineAsync("ConceptName,Label,Value,Depth,ParentConceptName");
            foreach (HierarchyNode node in hierarchy) {
                if (!includedConceptIds.Contains(node.ConceptId))
                    continue;
                string value = string.Empty;
                if (dataPointMap.TryGetValue(node.ConceptId, out DataPoint? dp))
                    value = dp.Value.ToString();
                else
                    await _stderr.WriteLineAsync($"WARNING: No data point found for concept '{node.Name}' (ConceptId: {node.ConceptId}) in submission.");
                string parentName = node.ParentConceptId.HasValue && conceptMap.TryGetValue(node.ParentConceptId.Value, out ConceptDetailsDTO? parentConcept) ? parentConcept.Name : string.Empty;
                await _stdout.WriteLineAsync($"{node.Name},\"{node.Label}\",{value},{node.Depth},{parentName}");
            }
        } else if (_format.Equals("html", StringComparison.OrdinalIgnoreCase)) {
            await _stdout.WriteLineAsync("<div class=\"statement-root\" style=\"display:flex; flex-direction:column; gap:6px;\">");
            await _stdout.WriteLineAsync("<style>");
            await _stdout.WriteLineAsync(".breadcrumb { font-size: 11px; color: #5a6b85; }");
            await _stdout.WriteLineAsync("#toggle-breadcrumbs:not(:checked) ~ .statement-rows .breadcrumb { display: none; }");
            await _stdout.WriteLineAsync("</style>");
            await _stdout.WriteLineAsync("<input type=\"checkbox\" id=\"toggle-breadcrumbs\" style=\"margin:0 6px 0 0;\" />");
            await _stdout.WriteLineAsync("<label for=\"toggle-breadcrumbs\" style=\"font-size:12px;\">Show breadcrumbs</label>");
            await _stdout.WriteLineAsync("<div class=\"statement-rows\" style=\"display:flex; flex-direction:column; gap:2px;\">");
            _htmlRowIndex = 0;
            await WriteHtmlTree(childrenMap, rootNodes, includedConceptIds, conceptMap, dataPointMap, null, string.Empty);
            await _stdout.WriteLineAsync("</div>");
            await _stdout.WriteLineAsync("</div>");
        } else if (_format.Equals("json", StringComparison.OrdinalIgnoreCase)) {
            await _stdout.WriteLineAsync(System.Text.Json.JsonSerializer.Serialize(BuildJsonTree(childrenMap, rootNodes, includedConceptIds, dataPointMap, null)));
        } else {
            await _stderr.WriteLineAsync($"ERROR: Unknown format '{_format}'.");
        }
    }

    private async Task WriteHtmlTree(
        Dictionary<long, List<HierarchyNode>> childrenMap,
        List<HierarchyNode> rootNodes,
        HashSet<long> includedConceptIds,
        Dictionary<long, ConceptDetailsDTO> conceptMap,
        Dictionary<long, DataPoint> dataPointMap,
        long? parentId,
        string parentBreadcrumb) {
        List<HierarchyNode> nodesToRender = rootNodes;
        if (parentId.HasValue) {
            if (!childrenMap.TryGetValue(parentId.Value, out List<HierarchyNode>? children))
                return;
            nodesToRender = children;
        }
        var seenConceptIds = new HashSet<long>();
        foreach (HierarchyNode node in nodesToRender) {
            if (!seenConceptIds.Add(node.ConceptId))
                continue;
            if (!includedConceptIds.Contains(node.ConceptId))
                continue;
            bool isAbstract = conceptMap.TryGetValue(node.ConceptId, out ConceptDetailsDTO? concept) && concept.IsAbstract;
            string backgroundColor = GetRowBackgroundColor(_htmlRowIndex, isAbstract);
            string breadcrumb = string.IsNullOrEmpty(parentBreadcrumb)
                ? node.Name
                : $"{parentBreadcrumb} > {node.Name}";
            string value = dataPointMap.TryGetValue(node.ConceptId, out DataPoint? dp)
                ? FormatValueWithUnit(dp)
                : string.Empty;
            string formattedValue = string.IsNullOrEmpty(value)
                ? string.Empty
                : $"<span style=\"text-align:right; min-width:220px; font-variant-numeric: tabular-nums;\">{value}</span>";
            int indent = node.Depth * 20;
            await _stdout.WriteLineAsync($"<div style=\"display:flex; justify-content:space-between; padding:2px 4px; padding-left:{indent}px; background-color:{backgroundColor};\">");
            await _stdout.WriteLineAsync("<span style=\"display:flex; flex-direction:column; gap:2px;\">");
            await _stdout.WriteLineAsync($"<span class=\"breadcrumb\">{breadcrumb}</span>");
            await _stdout.WriteLineAsync($"<span>{node.Name}</span>");
            await _stdout.WriteLineAsync("</span>");
            await _stdout.WriteLineAsync($"{formattedValue}");
            await _stdout.WriteLineAsync("</div>");
            _htmlRowIndex++;
            if (childrenMap.TryGetValue(node.ConceptId, out List<HierarchyNode>? children)) {
                bool hasIncludedChildren = false;
                foreach (HierarchyNode child in children) {
                    if (includedConceptIds.Contains(child.ConceptId)) {
                        hasIncludedChildren = true;
                        break;
                    }
                }
                if (hasIncludedChildren) {
                    await WriteHtmlTree(childrenMap, rootNodes, includedConceptIds, conceptMap, dataPointMap, node.ConceptId, breadcrumb);
                }
            }
        }
    }

    private static string FormatNumber(decimal value) =>
        value.ToString("#,##0.################", CultureInfo.InvariantCulture);

    private static string FormatValueWithUnit(DataPoint dataPoint) {
        string formattedValue = FormatNumber(dataPoint.Value);
        string unit = dataPoint.Units.UnitName;
        if (string.IsNullOrWhiteSpace(unit))
            return formattedValue;
        return $"{formattedValue} {unit}";
    }

    private static string GetRowBackgroundColor(int rowIndex, bool isAbstract) {
        bool isEven = rowIndex % 2 == 0;
        if (isAbstract)
            return isEven ? "#dbe7ff" : "#cfe0ff";
        return isEven ? "#ffffff" : "#f4f9ff";
    }

    private object? BuildJsonTree(
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
                ["ConceptName"] = node.Name,
                ["Label"] = node.Label,
                ["Value"] = dataPointMap.TryGetValue(node.ConceptId, out DataPoint? dp) ? dp.Value : null
            };
            object? children = BuildJsonTree(childrenMap, rootNodes, includedConceptIds, dataPointMap, node.ConceptId);
            if (children is List<object> list && list.Count > 0)
                obj["Children"] = children;
            result.Add(obj);
        }
        if (parentId == null && result.Count == 1)
            return result[0];
        return result;
    }

    /// <summary>
    /// Recursively walks the taxonomy tree from the selected concept.
    /// </summary>
    private async Task<Result<List<HierarchyNode>>> TraverseConceptTree(TraverseContext ctx) {
        var result = new List<HierarchyNode>();
        string? error = await Traverse(ctx, result);
        if (error is not null)
            return Result<List<HierarchyNode>>.Failure(ErrorCodes.GenericError, error);
        return Result<List<HierarchyNode>>.Success(result);
    }

    private async Task<string?> Traverse(TraverseContext ctx, List<HierarchyNode> result) {
        if (ctx.Depth > ctx.MaxDepth)
            return null;
        if (!ctx.ConceptMap.TryGetValue(ctx.ConceptId, out ConceptDetailsDTO? concept))
            return $"ConceptId {ctx.ConceptId} not found in concept map.";
        if (!ctx.Visited.Add(ctx.ConceptId)) {
            await _stderr.WriteLineAsync($"WARNING: Cycle detected at conceptId {ctx.ConceptId}, skipping to prevent infinite recursion.");
            return null;
        }
        result.Add(new HierarchyNode {
            ConceptId = concept.ConceptId,
            Name = concept.Name ?? string.Empty,
            Label = concept.Label ?? string.Empty,
            Depth = ctx.Depth,
            ParentConceptId = ctx.ParentConceptId,
        });
        if (ctx.ParentToChildren.TryGetValue(ctx.ConceptId, out List<PresentationDetailsDTO>? children)) {
            var seenChildConceptIds = new HashSet<long>();
            foreach (PresentationDetailsDTO child in children) {
                if (!seenChildConceptIds.Add(child.ConceptId))
                    continue;
                TraverseContext childCtx = ctx with {
                    ConceptId = child.ConceptId,
                    Depth = ctx.Depth + 1,
                    ParentConceptId = ctx.ConceptId
                };
                string? err = await Traverse(childCtx, result);
                if (err is not null)
                    return err;
            }
        }
        _ = ctx.Visited.Remove(ctx.ConceptId);
        return null;
    }

    private static void BuildChildrenMap(
        List<HierarchyNode> nodes,
        Dictionary<long, List<HierarchyNode>> childrenMap,
        List<HierarchyNode> rootNodes) {
        foreach (HierarchyNode node in nodes) {
            if (node.ParentConceptId.HasValue) {
                long parentId = node.ParentConceptId.Value;
                if (!childrenMap.TryGetValue(parentId, out List<HierarchyNode>? children)) {
                    children = [];
                    childrenMap[parentId] = children;
                }
                children.Add(node);
            } else {
                rootNodes.Add(node);
            }
        }
    }

    private static bool HasValueOrChildValue(
        long conceptId,
        Dictionary<long, List<HierarchyNode>> childrenMap,
        Dictionary<long, DataPoint> dataPointMap,
        HashSet<long> includedConceptIds) {
        bool hasValue = dataPointMap.ContainsKey(conceptId);
        bool hasChildValue = false;
        if (childrenMap.TryGetValue(conceptId, out List<HierarchyNode>? children)) {
            foreach (HierarchyNode child in children) {
                if (HasValueOrChildValue(child.ConceptId, childrenMap, dataPointMap, includedConceptIds)) {
                    hasChildValue = true;
                }
            }
        }
        if (hasValue || hasChildValue)
            _ = includedConceptIds.Add(conceptId);
        return hasValue || hasChildValue;
    }

    /// <summary>
    /// Handles output formatting for each supported format (CSV, HTML, JSON).
    /// </summary>
    private void FormatOutput(/* params */) {
        // TODO: Implement output formatting
    }

    /// <summary>
    /// Ensures all required parameters are present and valid.
    /// </summary>
    public static bool ValidateParameters(/* params */) =>
        // TODO: Implement parameter validation
        true;

    /// <summary>
    /// Builds a map from ParentConceptId to a list of child PresentationDetailsDTOs for efficient hierarchy traversal.
    /// </summary>
    private static Dictionary<long, List<PresentationDetailsDTO>> BuildParentToChildrenMap(
        IEnumerable<PresentationDetailsDTO> presentations,
        string roleName) {
        var parentToChildren = new Dictionary<long, List<PresentationDetailsDTO>>();
        foreach (PresentationDetailsDTO pres in presentations) {
            if (!string.Equals(pres.RoleName, roleName, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!parentToChildren.TryGetValue(pres.ParentConceptId, out List<PresentationDetailsDTO>? children)) {
                children = [];
                parentToChildren[pres.ParentConceptId] = children;
            }
            children.Add(pres);
        }
        return parentToChildren;
    }
}
