using System;
using System.Collections.Generic;
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
    private readonly CancellationToken _ct;

    public StatementPrinter(
        IDbmService dbmService,
        string cik,
        string concept,
        DateOnly date,
        int maxDepth,
        string format,
        bool listStatements,
        TextWriter? stdout = null,
        TextWriter? stderr = null,
        CancellationToken ct = default) {
        _dbmService = dbmService;
        _cik = cik;
        _concept = concept;
        _date = date;
        _maxDepth = maxDepth;
        _format = format;
        _listStatements = listStatements;
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

        // 2. Load taxonomy concepts (US-GAAP 2025 assumed for now)
        const int UsGaap2025TaxonomyTypeId = 1; // TODO: Make configurable if needed
        Result<IReadOnlyCollection<ConceptDetailsDTO>> conceptsResult = await _dbmService.GetTaxonomyConceptsByTaxonomyType(UsGaap2025TaxonomyTypeId, _ct);
        if (conceptsResult.IsFailure || conceptsResult.Value is null) {
            await _stderr.WriteLineAsync($"ERROR: Could not load taxonomy concepts for US-GAAP 2025.");
            return 2;
        }
        IReadOnlyCollection<ConceptDetailsDTO> concepts = conceptsResult.Value;

        // 3. Load presentation hierarchy for the taxonomy (only if not listing statements)
        ConceptDetailsDTO? rootConcept = null;
        Dictionary<long, List<PresentationDetailsDTO>>? parentToChildren = null;
        if (!_listStatements) {
            Result<IReadOnlyCollection<PresentationDetailsDTO>> presentationsResult = await _dbmService.GetTaxonomyPresentationsByTaxonomyType(UsGaap2025TaxonomyTypeId, _ct);
            if (presentationsResult.IsFailure || presentationsResult.Value is null) {
                await _stderr.WriteLineAsync($"ERROR: Could not load taxonomy presentation hierarchy for US-GAAP 2025.");
                return 2;
            }
            IReadOnlyCollection<PresentationDetailsDTO> presentations = presentationsResult.Value;
            parentToChildren = BuildParentToChildrenMap(presentations);

            // Find root concept by name (case-insensitive) or ID
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
        }

        if (_listStatements) {
            // 4. Filter for abstract concepts (top-level statements)
            var abstractConcepts = new List<ConceptDetailsDTO>();
            foreach (ConceptDetailsDTO c in concepts) {
                if (c.IsAbstract) {
                    abstractConcepts.Add(c);
                }
            }
            abstractConcepts.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            // Print header always
            await _stdout.WriteLineAsync("ConceptName,Label,Documentation");
            if (abstractConcepts.Count == 0) {
                // No error, just header
                return 0;
            }
            // 5. Output as CSV (for now)
            foreach (ConceptDetailsDTO c in abstractConcepts) {
                string doc = c.Documentation != null ? c.Documentation.Replace('\n', ' ').Replace('\r', ' ') : string.Empty;
                await _stdout.WriteLineAsync($"{c.Name},\"{c.Label}\",\"{doc}\"");
            }
            return 0;
        }

        // 4. Find submission for the specified date
        Result<IReadOnlyCollection<Submission>> submissionsResult = await _dbmService.GetSubmissions(_ct);
        if (submissionsResult.IsFailure || submissionsResult.Value is null) {
            await _stderr.WriteLineAsync($"ERROR: Could not load submissions for company.");
            return 2;
        }
        Submission? selectedSubmission = null;
        foreach (Submission sub in submissionsResult.Value) {
            if (sub.CompanyId != company.CompanyId
                || sub.ReportDate > _date
                || (selectedSubmission != null && sub.ReportDate <= selectedSubmission.ReportDate)) {
                continue;
            }
            selectedSubmission = sub;
        }
        if (selectedSubmission is null) {
            await _stderr.WriteLineAsync($"ERROR: No submission found for CIK '{_cik}' on or before {_date:yyyy-MM-dd}.");
            return 2;
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
        await FormatOutput(hierarchy, conceptMap, dataPointMap);
        return 0;
    }

    private async Task FormatOutput(List<HierarchyNode> hierarchy, Dictionary<long, ConceptDetailsDTO> conceptMap, Dictionary<long, DataPoint> dataPointMap) {
        if (_format.Equals("csv", StringComparison.OrdinalIgnoreCase)) {
            await _stdout.WriteLineAsync("ConceptName,Label,Value,Depth,ParentConceptName");
            foreach (HierarchyNode node in hierarchy) {
                string value = string.Empty;
                if (dataPointMap.TryGetValue(node.ConceptId, out DataPoint? dp))
                    value = dp.Value.ToString();
                else
                    await _stderr.WriteLineAsync($"WARNING: No data point found for concept '{node.Name}' (ConceptId: {node.ConceptId}) in submission.");
                string parentName = node.ParentConceptId.HasValue && conceptMap.TryGetValue(node.ParentConceptId.Value, out ConceptDetailsDTO? parentConcept) ? parentConcept.Name : string.Empty;
                await _stdout.WriteLineAsync($"{node.Name},\"{node.Label}\",{value},{node.Depth},{parentName}");
            }
        } else if (_format.Equals("html", StringComparison.OrdinalIgnoreCase)) {
            await _stdout.WriteLineAsync("<ul>");
            await WriteHtmlTree(hierarchy, conceptMap, dataPointMap, null, 0);
            await _stdout.WriteLineAsync("</ul>");
        } else if (_format.Equals("json", StringComparison.OrdinalIgnoreCase)) {
            await _stdout.WriteLineAsync(System.Text.Json.JsonSerializer.Serialize(BuildJsonTree(hierarchy, conceptMap, dataPointMap, null)));
        } else {
            await _stderr.WriteLineAsync($"ERROR: Unknown format '{_format}'.");
        }
    }

    private async Task WriteHtmlTree(List<HierarchyNode> nodes, Dictionary<long, ConceptDetailsDTO> conceptMap, Dictionary<long, DataPoint> dataPointMap, long? parentId, int depth) {
        foreach (HierarchyNode node in nodes) {
            if (node.ParentConceptId != parentId)
                continue;
            string value = dataPointMap.TryGetValue(node.ConceptId, out DataPoint? dp) ? dp.Value.ToString() : string.Empty;
            await _stdout.WriteLineAsync($"<li>{node.Name}: {value}");
            bool hasChildren = nodes.Exists(n => n.ParentConceptId == node.ConceptId);
            if (hasChildren) {
                await _stdout.WriteLineAsync("<ul>");
                await WriteHtmlTree(nodes, conceptMap, dataPointMap, node.ConceptId, depth + 1);
                await _stdout.WriteLineAsync("</ul>");
            }
            await _stdout.WriteLineAsync("</li>");
        }
    }

    private object? BuildJsonTree(List<HierarchyNode> nodes, Dictionary<long, ConceptDetailsDTO> conceptMap, Dictionary<long, DataPoint> dataPointMap, long? parentId) {
        var result = new List<object>();
        foreach (HierarchyNode node in nodes) {
            if (node.ParentConceptId != parentId)
                continue;
            var obj = new Dictionary<string, object?> {
                ["ConceptName"] = node.Name,
                ["Label"] = node.Label,
                ["Value"] = dataPointMap.TryGetValue(node.ConceptId, out DataPoint? dp) ? dp.Value : null
            };
            object? children = BuildJsonTree(nodes, conceptMap, dataPointMap, node.ConceptId);
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
            foreach (PresentationDetailsDTO child in children) {
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

    /// <summary>
    /// Handles output formatting for each supported format (CSV, HTML, JSON).
    /// </summary>
    private void FormatOutput(/* params */) {
        // TODO: Implement output formatting
    }

    /// <summary>
    /// Ensures all required parameters are present and valid.
    /// </summary>
    public static bool ValidateParameters(/* params */) {
        // TODO: Implement parameter validation
        return true;
    }

    /// <summary>
    /// Builds a map from ParentConceptId to a list of child PresentationDetailsDTOs for efficient hierarchy traversal.
    /// </summary>
    private static Dictionary<long, List<PresentationDetailsDTO>> BuildParentToChildrenMap(IEnumerable<PresentationDetailsDTO> presentations) {
        var parentToChildren = new Dictionary<long, List<PresentationDetailsDTO>>();
        foreach (PresentationDetailsDTO pres in presentations) {
            if (!parentToChildren.TryGetValue(pres.ParentConceptId, out List<PresentationDetailsDTO>? children)) {
                children = [];
                parentToChildren[pres.ParentConceptId] = children;
            }
            children.Add(pres);
        }
        return parentToChildren;
    }
}
