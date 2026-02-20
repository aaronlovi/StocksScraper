using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stocks.DataModels;
using Stocks.DataModels.Enums;
using Stocks.Persistence.Database;
using Stocks.Persistence.Database.DTO.Taxonomies;
using Stocks.Shared;
using Stocks.Shared.Models;

namespace EDGARScraper.Services;

internal sealed class InlineXbrlSharesImporter {
    private const string SharesConceptName = "EntityCommonStockSharesOutstanding";
    private const string SharesFactNameLower = "entitycommonstocksharesoutstanding";
    private const string SharesUnitName = "shares";
    private const int UpsertBatchSize = 100;

    private static readonly string[] SharesConcepts = [
        "CommonStockSharesOutstanding",
        "WeightedAverageNumberOfSharesOutstandingBasic",
        SharesConceptName
    ];

    private readonly IDbmService _dbm;
    private readonly ILogger _logger;

    internal InlineXbrlSharesImporter(IDbmService dbm, ILogger logger) {
        _dbm = dbm;
        _logger = logger;
    }

    internal async Task<Result> ImportAsync(
        string submissionsZipPath,
        RateLimitedHttpClient httpClient,
        CancellationToken ct) {

        // Step 1: Find companies with price data but no recent shares data
        var recentCutoff = new DateTime(2020, 1, 1);
        _logger.LogInformation("InlineXbrlSharesImporter - Finding companies without recent shares data (cutoff: {Cutoff})...", recentCutoff);
        Result<IReadOnlyCollection<Company>> companiesResult =
            await _dbm.GetCompaniesWithoutSharesData(SharesConcepts, recentCutoff, ct);
        if (companiesResult.IsFailure)
            return Result.Failure(companiesResult);

        IReadOnlyCollection<Company> targetCompanies = companiesResult.Value!;
        _logger.LogInformation("InlineXbrlSharesImporter - Found {Count} companies without recent shares data", targetCompanies.Count);

        if (targetCompanies.Count == 0)
            return Result.Success;

        // Step 2: Build CIK set and CIK-to-CompanyId mapping
        var targetCiks = new HashSet<ulong>();
        var companyIdsByCiks = new Dictionary<ulong, ulong>();
        var companiesById = new Dictionary<ulong, Company>();
        foreach (Company company in targetCompanies) {
            targetCiks.Add(company.Cik);
            companyIdsByCiks[company.Cik] = company.CompanyId;
            companiesById[company.CompanyId] = company;
        }

        // Step 3: Resolve primary document filenames from submissions.zip
        _logger.LogInformation("InlineXbrlSharesImporter - Reading submissions.zip for primary document filenames...");
        var resolver = new PrimaryDocumentResolver(_logger);
        Dictionary<string, string> primaryDocsByFilingRef =
            resolver.Resolve(submissionsZipPath, targetCiks, companyIdsByCiks);
        _logger.LogInformation("InlineXbrlSharesImporter - Found {Count} primary document mappings", primaryDocsByFilingRef.Count);

        // Step 4: Resolve taxonomy concept ID for EntityCommonStockSharesOutstanding
        long taxonomyConceptId = await ResolveTaxonomyConceptIdAsync(ct);
        if (taxonomyConceptId == 0) {
            _logger.LogError("InlineXbrlSharesImporter - Could not resolve taxonomy concept ID for {Concept}", SharesConceptName);
            return Result.Failure(ErrorCodes.NotFound, $"Taxonomy concept not found: {SharesConceptName}");
        }

        // Step 5: Resolve shares unit ID
        Result<DataPointUnit> unitResult = await ResolveSharesUnitAsync(ct);
        if (unitResult.IsFailure)
            return Result.Failure(unitResult);
        DataPointUnit sharesUnit = unitResult.Value!;

        // Step 6: Process each target company
        var parser = new InlineXbrlParser();
        int totalCompanies = 0;
        int companiesWithData = 0;
        int totalDataPoints = 0;
        int totalErrors = 0;
        var dataPointsBatch = new List<DataPoint>();

        foreach (Company company in targetCompanies) {
            ++totalCompanies;
            if (totalCompanies % 50 == 0)
                _logger.LogInformation(
                    "InlineXbrlSharesImporter - Progress: {Done}/{Total} companies, {DataPoints} data points, {Errors} errors",
                    totalCompanies, targetCompanies.Count, totalDataPoints, totalErrors);

            int companyDataPoints = await ProcessCompanyAsync(
                company, primaryDocsByFilingRef, httpClient, parser,
                sharesUnit, taxonomyConceptId, dataPointsBatch, ct);

            if (companyDataPoints > 0)
                ++companiesWithData;
            totalDataPoints += companyDataPoints;

            // Flush batch if needed
            if (dataPointsBatch.Count >= UpsertBatchSize) {
                Result upsertResult = await _dbm.UpsertDataPoints(dataPointsBatch, ct);
                if (upsertResult.IsFailure) {
                    _logger.LogWarning("InlineXbrlSharesImporter - Upsert batch failed: {Error}", upsertResult.ErrorMessage);
                    ++totalErrors;
                }
                dataPointsBatch.Clear();
            }
        }

        // Flush remaining batch
        if (dataPointsBatch.Count > 0) {
            Result upsertResult = await _dbm.UpsertDataPoints(dataPointsBatch, ct);
            if (upsertResult.IsFailure) {
                _logger.LogWarning("InlineXbrlSharesImporter - Final upsert batch failed: {Error}", upsertResult.ErrorMessage);
                ++totalErrors;
            }
        }

        _logger.LogInformation(
            "InlineXbrlSharesImporter - Done. {CompaniesWithData}/{TotalCompanies} companies yielded data, "
            + "{DataPoints} data points inserted, {Errors} errors",
            companiesWithData, totalCompanies, totalDataPoints, totalErrors);

        return Result.Success;
    }

    private async Task<int> ProcessCompanyAsync(
        Company company,
        Dictionary<string, string> primaryDocsByFilingRef,
        RateLimitedHttpClient httpClient,
        InlineXbrlParser parser,
        DataPointUnit sharesUnit,
        long taxonomyConceptId,
        List<DataPoint> dataPointsBatch,
        CancellationToken ct) {

        // Get company submissions
        Result<IReadOnlyCollection<Submission>> subsResult =
            await _dbm.GetSubmissionsByCompanyId(company.CompanyId, ct);
        if (subsResult.IsFailure)
            return 0;

        // Filter to annual report submissions, most recent 5
        var annualSubs = new List<Submission>();
        foreach (Submission sub in subsResult.Value!) {
            if (sub.FilingType is FilingType.TenK or FilingType.TenK_A
                or FilingType.TenKT or FilingType.TenKT_A
                or FilingType.TwentyF or FilingType.TwentyF_A
                or FilingType.FortyF or FilingType.FortyF_A)
                annualSubs.Add(sub);
        }

        // Sort by report date descending and take most recent 5
        annualSubs.Sort((a, b) => b.ReportDate.CompareTo(a.ReportDate));
        int limit = Math.Min(annualSubs.Count, 5);
        int dataPointCount = 0;

        for (int i = 0; i < limit; i++) {
            Submission sub = annualSubs[i];

            // Look up primary document filename
            if (!primaryDocsByFilingRef.TryGetValue(sub.FilingReference, out string? primaryDoc))
                continue;

            // Build the filing URL
            string accessionNoDashes = sub.FilingReference.Replace("-", "");
            string url = $"https://www.sec.gov/Archives/edgar/data/{company.Cik}/{accessionNoDashes}/{primaryDoc}";

            // Download the filing HTML
            Result<string> htmlResult = await httpClient.FetchStringAsync(url, ct);
            if (htmlResult.IsFailure)
                continue;

            // Parse inline XBRL for shares
            IReadOnlyCollection<AggregatedSharesFact> sharesFacts =
                await parser.ParseSharesFromHtmlAsync(htmlResult.Value!);

            foreach (AggregatedSharesFact fact in sharesFacts) {
                ulong dpId = await _dbm.GetNextId64(ct);
                var datePair = new DatePair(fact.Date, fact.Date); // instant fact
                var dataPoint = new DataPoint(
                    dpId,
                    company.CompanyId,
                    SharesFactNameLower,
                    sub.FilingReference,
                    datePair,
                    fact.TotalShares,
                    sharesUnit,
                    sub.ReportDate,
                    sub.SubmissionId,
                    taxonomyConceptId);
                dataPointsBatch.Add(dataPoint);
                ++dataPointCount;
            }
        }

        return dataPointCount;
    }

    private async Task<long> ResolveTaxonomyConceptIdAsync(CancellationToken ct) {
        // Try recent taxonomy years first (dei concepts)
        for (int year = 2025; year >= 2020; year--) {
            Result<TaxonomyTypeInfo> typeResult =
                await _dbm.GetTaxonomyTypeByNameVersion("dei", year, ct);
            if (typeResult.IsFailure)
                continue;

            Result<IReadOnlyCollection<ConceptDetailsDTO>> conceptsResult =
                await _dbm.GetTaxonomyConceptsByTaxonomyType(typeResult.Value!.TaxonomyTypeId, ct);
            if (conceptsResult.IsFailure)
                continue;

            foreach (ConceptDetailsDTO concept in conceptsResult.Value!) {
                if (string.Equals(concept.Name, SharesConceptName, StringComparison.OrdinalIgnoreCase))
                    return concept.ConceptId;
            }
        }

        return 0;
    }

    private async Task<Result<DataPointUnit>> ResolveSharesUnitAsync(CancellationToken ct) {
        Result<IReadOnlyCollection<DataPointUnit>> unitsResult = await _dbm.GetDataPointUnits(ct);
        if (unitsResult.IsFailure)
            return Result<DataPointUnit>.Failure(unitsResult);

        foreach (DataPointUnit unit in unitsResult.Value!) {
            if (string.Equals(unit.UnitNameNormalized, SharesUnitName, StringComparison.OrdinalIgnoreCase))
                return Result<DataPointUnit>.Success(unit);
        }

        return Result<DataPointUnit>.Failure(ErrorCodes.NotFound, "Shares unit not found in data_point_units");
    }
}
