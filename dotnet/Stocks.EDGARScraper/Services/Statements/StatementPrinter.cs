using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Stocks.DataModels;
using Stocks.Persistence.Database;
using Stocks.Persistence.Database.DTO.Taxonomies;
using Stocks.Shared;

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
        // 1. Load company by CIK
        if (!ulong.TryParse(_cik, out ulong cikNum)) {
            await _stderr.WriteLineAsync($"ERROR: Invalid CIK '{_cik}'.");
            return 2;
        }
        // TODO: Refactor to query for a single company by CIK in the database for better performance with large datasets.
        Result<IReadOnlyCollection<Company>> companiesResult = await _dbmService.GetAllCompaniesByDataSource(DataSource, _ct);
        if (companiesResult.IsFailure || companiesResult.Value == null) {
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
        if (conceptsResult.IsFailure || conceptsResult.Value == null) {
            await _stderr.WriteLineAsync($"ERROR: Could not load taxonomy concepts for US-GAAP 2025.");
            return 2;
        }
        IReadOnlyCollection<ConceptDetailsDTO> concepts = conceptsResult.Value;

        // 3. Load presentation hierarchy for the taxonomy (only if not listing statements)
        if (!_listStatements) {
            Result<IReadOnlyCollection<PresentationDetailsDTO>> presentationsResult = await _dbmService.GetTaxonomyPresentationsByTaxonomyType(UsGaap2025TaxonomyTypeId, _ct);
            if (presentationsResult.IsFailure || presentationsResult.Value == null) {
                await _stderr.WriteLineAsync($"ERROR: Could not load taxonomy presentation hierarchy for US-GAAP 2025.");
                return 2;
            }
            IReadOnlyCollection<PresentationDetailsDTO> presentations = presentationsResult.Value;
            // Build parent-to-children map for traversal
            Dictionary<long, List<PresentationDetailsDTO>> parentToChildren = BuildParentToChildrenMap(presentations);
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

        // TODO: Implement hierarchy traversal and output
        await _stderr.WriteLineAsync("ERROR: Only --list-statements is implemented in this prototype.");
        return 2;
    }

    /// <summary>
    /// Recursively walks the taxonomy tree from the selected concept.
    /// </summary>
    private void TraverseConceptTree(/* params */) {
        // TODO: Implement recursive traversal
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
    private static Dictionary<long, List<PresentationDetailsDTO>> BuildParentToChildrenMap(IEnumerable<PresentationDetailsDTO> presentations)
    {
        var parentToChildren = new Dictionary<long, List<PresentationDetailsDTO>>();
        foreach (var pres in presentations)
        {
            if (!parentToChildren.TryGetValue(pres.ParentConceptId, out var children))
            {
                children = new List<PresentationDetailsDTO>();
                parentToChildren[pres.ParentConceptId] = children;
            }
            children.Add(pres);
        }
        return parentToChildren;
    }
}
