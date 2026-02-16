using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Stocks.DataModels;
using Stocks.Persistence.Database;
using Stocks.Persistence.Database.DTO.Taxonomies;
using Stocks.Persistence.Services;
using Stocks.Shared;

namespace Stocks.EDGARScraper.Services.Statements;

/// <summary>
/// Responsible for rendering a financial statement or taxonomy concept hierarchy for a company.
/// </summary>
public class StatementPrinter {
    private readonly IDbmService _dbmService;
    private readonly StatementDataService _statementDataService;
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
        _statementDataService = new StatementDataService(dbmService);
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
        // Validate CIK
        const string DataSource = "EDGAR";
        if (!ulong.TryParse(_cik, out ulong cikNum)) {
            await _stderr.WriteLineAsync($"ERROR: Invalid CIK '{_cik}'.");
            return 2;
        }

        // Look up company
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

        // Handle --list-statements via StatementDataService
        if (_listStatements) {
            Result<IReadOnlyCollection<StatementListItem>> listResult =
                await _statementDataService.ListStatements(_taxonomyTypeId, _ct);
            if (listResult.IsFailure) {
                await _stderr.WriteLineAsync($"ERROR: {listResult.ErrorMessage}");
                return 2;
            }
            await _stdout.WriteLineAsync("RoleName,RootConceptName,RootLabel");
            foreach (StatementListItem item in listResult.Value!) {
                if (string.IsNullOrEmpty(item.RootConceptName))
                    await _stdout.WriteLineAsync($"{item.RoleName},,");
                else
                    await _stdout.WriteLineAsync($"{item.RoleName},{item.RootConceptName},\"{item.RootLabel}\"");
            }
            return 0;
        }

        // Validate concept exists before looking for submissions
        Result<IReadOnlyCollection<ConceptDetailsDTO>> earlyConceptsResult =
            await _dbmService.GetTaxonomyConceptsByTaxonomyType(_taxonomyTypeId, _ct);
        if (earlyConceptsResult.IsFailure || earlyConceptsResult.Value is null) {
            await _stderr.WriteLineAsync($"ERROR: Could not load taxonomy concepts for taxonomy type {_taxonomyTypeId}.");
            return 2;
        }
        Result<IReadOnlyCollection<PresentationDetailsDTO>> earlyPresentationsResult =
            await _dbmService.GetTaxonomyPresentationsByTaxonomyType(_taxonomyTypeId, _ct);
        if (earlyPresentationsResult.IsFailure || earlyPresentationsResult.Value is null) {
            await _stderr.WriteLineAsync($"ERROR: Could not load taxonomy presentation hierarchy for taxonomy type {_taxonomyTypeId}.");
            return 2;
        }
        ConceptDetailsDTO? earlyRootConcept = null;
        foreach (ConceptDetailsDTO c in earlyConceptsResult.Value) {
            if (c.Name.EqualsOrdinalIgnoreCase(_concept)) {
                earlyRootConcept = c;
                break;
            }
            if (long.TryParse(_concept, out long conceptId) && c.ConceptId == conceptId) {
                earlyRootConcept = c;
                break;
            }
        }
        if (earlyRootConcept is null) {
            await _stderr.WriteLineAsync($"ERROR: Concept '{_concept}' not found in taxonomy.");
            return 2;
        }

        // Resolve role name early
        if (string.IsNullOrWhiteSpace(_roleName)) {
            var matchingRoles = new List<string>();
            foreach (PresentationDetailsDTO p in earlyPresentationsResult.Value) {
                if (p.ConceptId != earlyRootConcept.ConceptId)
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
            if (matchingRoles.Count == 0) {
                await _stderr.WriteLineAsync($"ERROR: No presentation role found for concept '{earlyRootConcept.Name}'.");
                return 2;
            }
            if (matchingRoles.Count > 1) {
                await _stderr.WriteLineAsync($"ERROR: Multiple presentation roles found for concept '{earlyRootConcept.Name}'. Please specify --role.");
                return 2;
            }
        }

        // Find submission for the specified date
        Result<IReadOnlyCollection<Submission>> submissionsResult = await _dbmService.GetSubmissions(_ct);
        if (submissionsResult.IsFailure || submissionsResult.Value is null) {
            await _stderr.WriteLineAsync("ERROR: Could not load submissions for company.");
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
        bool hasAnySubmissionForCompany = false;
        foreach (Submission sub in submissionsResult.Value) {
            if (sub.CompanyId == company.CompanyId) {
                hasAnySubmissionForCompany = true;
                break;
            }
        }
        if (selectedSubmission is null) {
            if (hasAnySubmissionForCompany)
                await _stderr.WriteLineAsync($"ERROR: No submission found for CIK '{_cik}' on or before {_date:yyyy-MM-dd}.");
            else
                await _stderr.WriteLineAsync($"ERROR: No submissions exist for CIK '{_cik}'.");
            return 2;
        }

        // Get statement data via StatementDataService
        Result<StatementData> dataResult = await _statementDataService.GetStatementData(
            company.CompanyId,
            selectedSubmission.SubmissionId,
            _concept,
            _taxonomyTypeId,
            _maxDepth,
            _roleName,
            _ct,
            _stderr);
        if (dataResult.IsFailure || dataResult.Value is null) {
            await _stderr.WriteLineAsync($"ERROR: {dataResult.ErrorMessage}");
            return 2;
        }

        StatementData data = dataResult.Value;

        // Build concept map for formatting
        Result<IReadOnlyCollection<ConceptDetailsDTO>> conceptsResult =
            await _dbmService.GetTaxonomyConceptsByTaxonomyType(_taxonomyTypeId, _ct);
        Dictionary<long, ConceptDetailsDTO> conceptMap = new();
        if (conceptsResult.IsSuccess && conceptsResult.Value is not null) {
            foreach (ConceptDetailsDTO c in conceptsResult.Value)
                conceptMap[c.ConceptId] = c;
        }

        if (data.IncludedConceptIds.Count == 0) {
            foreach (HierarchyNode rootNode in data.RootNodes) {
                if (!data.DataPointMap.ContainsKey(rootNode.ConceptId))
                    await _stderr.WriteLineAsync($"WARNING: No data point found for concept '{rootNode.Name}' (ConceptId: {rootNode.ConceptId}) in submission.");
            }
        }

        await FormatOutput(data.Hierarchy, conceptMap, data.DataPointMap,
            data.ChildrenMap, data.RootNodes, data.IncludedConceptIds);
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
            if (childrenMap.TryGetValue(node.ConceptId, out List<HierarchyNode>? nodeChildren)) {
                bool hasIncludedChildren = false;
                foreach (HierarchyNode child in nodeChildren) {
                    if (includedConceptIds.Contains(child.ConceptId)) {
                        hasIncludedChildren = true;
                        break;
                    }
                }
                if (hasIncludedChildren)
                    await WriteHtmlTree(childrenMap, rootNodes, includedConceptIds, conceptMap, dataPointMap, node.ConceptId, breadcrumb);
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
            object? nodeChildren = BuildJsonTree(childrenMap, rootNodes, includedConceptIds, dataPointMap, node.ConceptId);
            if (nodeChildren is List<object> list && list.Count > 0)
                obj["Children"] = nodeChildren;
            result.Add(obj);
        }
        if (parentId == null && result.Count == 1)
            return result[0];
        return result;
    }

    /// <summary>
    /// Ensures all required parameters are present and valid.
    /// </summary>
    public static bool ValidateParameters(/* params */) =>
        // TODO: Implement parameter validation
        true;
}
