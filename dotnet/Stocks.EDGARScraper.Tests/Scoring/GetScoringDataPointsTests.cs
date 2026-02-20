using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Stocks.DataModels;
using Stocks.DataModels.Enums;
using Stocks.DataModels.Scoring;
using Stocks.Persistence.Database;
using Stocks.Persistence.Database.DTO.Taxonomies;
using Stocks.Shared;

namespace Stocks.EDGARScraper.Tests.Scoring;

public class GetScoringDataPointsTests {
    private readonly DbmInMemoryService _dbm = new();
    private readonly CancellationToken _ct = CancellationToken.None;

    private const ulong CompanyId = 1;
    private const ulong CompanyCik = 320193;

    private async Task SeedCompanyAndTaxonomy() {
        await _dbm.BulkInsertCompanies([new Company(CompanyId, CompanyCik, "EDGAR")], _ct);

        // Create a taxonomy type and concepts
        await _dbm.EnsureTaxonomyType("us-gaap", 2024, _ct);
        await _dbm.BulkInsertTaxonomyConcepts([
            new ConceptDetailsDTO(100, 1, 1, 0, false, "StockholdersEquity", "Stockholders Equity", ""),
            new ConceptDetailsDTO(101, 1, 1, 0, false, "RetainedEarningsAccumulatedDeficit", "Retained Earnings", ""),
            new ConceptDetailsDTO(102, 1, 2, 0, false, "NetIncomeLoss", "Net Income", ""),
            new ConceptDetailsDTO(103, 1, 1, 0, false, "Goodwill", "Goodwill", ""),
            new ConceptDetailsDTO(104, 1, 2, 0, false, "PaymentsOfDividends", "Dividends Paid", ""),
        ], _ct);
    }

    private DataPoint MakeDataPoint(ulong dpId, ulong submissionId, long conceptId, decimal value,
        DateOnly startDate, DateOnly endDate) {
        return new DataPoint(
            dpId, CompanyId, "fact", "ref",
            new DatePair(startDate, endDate),
            value,
            new DataPointUnit(1, "USD"),
            endDate,
            submissionId,
            conceptId);
    }

    [Fact]
    public async Task GetScoringDataPoints_ReturnsConceptsFromTenKFilings() {
        await SeedCompanyAndTaxonomy();

        var reportDate = new DateOnly(2024, 9, 28);
        await _dbm.BulkInsertSubmissions([
            new Submission(10, CompanyId, "ref-1", FilingType.TenK, FilingCategory.Annual, reportDate, null)
        ], _ct);

        await _dbm.BulkInsertDataPoints([
            MakeDataPoint(1000, 10, 100, 500_000_000m, reportDate, reportDate),
            MakeDataPoint(1001, 10, 101, 100_000_000m, reportDate, reportDate),
        ], _ct);

        Result<IReadOnlyCollection<ScoringConceptValue>> result = await _dbm.GetScoringDataPoints(
            CompanyId, ["StockholdersEquity", "RetainedEarningsAccumulatedDeficit"], _ct);

        Assert.True(result.IsSuccess);
        IReadOnlyCollection<ScoringConceptValue> values = result.Value!;
        Assert.Equal(2, values.Count);

        var list = new List<ScoringConceptValue>(values);
        Assert.Contains(list, v => v.ConceptName == "StockholdersEquity" && v.Value == 500_000_000m);
        Assert.Contains(list, v => v.ConceptName == "RetainedEarningsAccumulatedDeficit" && v.Value == 100_000_000m);
    }

    [Fact]
    public async Task GetScoringDataPoints_IncludesTenQWhenMoreRecent() {
        await SeedCompanyAndTaxonomy();

        var tenKDate = new DateOnly(2024, 9, 28);
        var tenQDate = new DateOnly(2025, 3, 29);
        await _dbm.BulkInsertSubmissions([
            new Submission(10, CompanyId, "ref-1", FilingType.TenK, FilingCategory.Annual, tenKDate, null),
            new Submission(11, CompanyId, "ref-2", FilingType.TenQ, FilingCategory.Quarterly, tenQDate, null),
        ], _ct);

        await _dbm.BulkInsertDataPoints([
            MakeDataPoint(1000, 10, 100, 500_000_000m, tenKDate, tenKDate),
            MakeDataPoint(1001, 11, 100, 480_000_000m, tenQDate, tenQDate),
        ], _ct);

        Result<IReadOnlyCollection<ScoringConceptValue>> result = await _dbm.GetScoringDataPoints(
            CompanyId, ["StockholdersEquity"], _ct);

        Assert.True(result.IsSuccess);
        var list = new List<ScoringConceptValue>(result.Value!);
        // Both the 10-K and 10-Q data should be included
        Assert.Equal(2, list.Count);
        Assert.Contains(list, v => v.ReportDate == tenQDate && v.Value == 480_000_000m && v.FilingTypeId == (int)FilingType.TenQ);
        Assert.Contains(list, v => v.ReportDate == tenKDate && v.Value == 500_000_000m && v.FilingTypeId == (int)FilingType.TenK);
    }

    [Fact]
    public async Task GetScoringDataPoints_TenQOlderThanTenKNotIncluded() {
        await SeedCompanyAndTaxonomy();

        var tenKDate = new DateOnly(2024, 9, 28);
        var tenQDate = new DateOnly(2024, 6, 29);
        await _dbm.BulkInsertSubmissions([
            new Submission(10, CompanyId, "ref-1", FilingType.TenK, FilingCategory.Annual, tenKDate, null),
            new Submission(11, CompanyId, "ref-2", FilingType.TenQ, FilingCategory.Quarterly, tenQDate, null),
        ], _ct);

        await _dbm.BulkInsertDataPoints([
            MakeDataPoint(1000, 10, 100, 500_000_000m, tenKDate, tenKDate),
            MakeDataPoint(1001, 11, 100, 480_000_000m, tenQDate, tenQDate),
        ], _ct);

        Result<IReadOnlyCollection<ScoringConceptValue>> result = await _dbm.GetScoringDataPoints(
            CompanyId, ["StockholdersEquity"], _ct);

        Assert.True(result.IsSuccess);
        var list = new List<ScoringConceptValue>(result.Value!);
        // The 10-Q is older than the 10-K, so it doesn't qualify as the latest date
        // Only the 10-K date is eligible (5 most recent 10-K dates UNION 1 most recent any-type date = 10-K date)
        Assert.Single(list);
        Assert.Equal(500_000_000m, list[0].Value);
        Assert.Equal(tenKDate, list[0].ReportDate);
    }

    [Fact]
    public async Task GetScoringDataPoints_LimitsToFiveMostRecentYears() {
        await SeedCompanyAndTaxonomy();

        // Insert 7 years of 10-K filings
        var submissions = new List<Submission>();
        var dataPoints = new List<DataPoint>();
        for (int year = 2018; year <= 2024; year++) {
            ulong subId = (ulong)(100 + year);
            var reportDate = new DateOnly(year, 9, 28);
            submissions.Add(new Submission(subId, CompanyId, $"ref-{year}", FilingType.TenK,
                FilingCategory.Annual, reportDate, null));
            dataPoints.Add(MakeDataPoint((ulong)(2000 + year), subId, 100, year * 1_000_000m,
                reportDate, reportDate));
        }

        await _dbm.BulkInsertSubmissions(submissions, _ct);
        await _dbm.BulkInsertDataPoints(dataPoints, _ct);

        Result<IReadOnlyCollection<ScoringConceptValue>> result = await _dbm.GetScoringDataPoints(
            CompanyId, ["StockholdersEquity"], _ct);

        Assert.True(result.IsSuccess);
        var list = new List<ScoringConceptValue>(result.Value!);
        Assert.Equal(5, list.Count);

        // Verify the 5 most recent years (2020-2024)
        foreach (ScoringConceptValue v in list) {
            Assert.True(v.ReportDate.Year >= 2020, $"Expected year >= 2020, got {v.ReportDate.Year}");
        }
    }

    [Fact]
    public async Task GetScoringDataPoints_FiltersToRequestedConcepts() {
        await SeedCompanyAndTaxonomy();

        var reportDate = new DateOnly(2024, 9, 28);
        await _dbm.BulkInsertSubmissions([
            new Submission(10, CompanyId, "ref-1", FilingType.TenK, FilingCategory.Annual, reportDate, null)
        ], _ct);

        // Insert data points for multiple concepts
        await _dbm.BulkInsertDataPoints([
            MakeDataPoint(1000, 10, 100, 500_000_000m, reportDate, reportDate),
            MakeDataPoint(1001, 10, 101, 100_000_000m, reportDate, reportDate),
            MakeDataPoint(1002, 10, 102, 50_000_000m, reportDate, reportDate),
            MakeDataPoint(1003, 10, 103, 20_000_000m, reportDate, reportDate),
        ], _ct);

        // Only request 2 of the 4 concepts
        Result<IReadOnlyCollection<ScoringConceptValue>> result = await _dbm.GetScoringDataPoints(
            CompanyId, ["StockholdersEquity", "Goodwill"], _ct);

        Assert.True(result.IsSuccess);
        var list = new List<ScoringConceptValue>(result.Value!);
        Assert.Equal(2, list.Count);
        Assert.Contains(list, v => v.ConceptName == "StockholdersEquity");
        Assert.Contains(list, v => v.ConceptName == "Goodwill");
    }

    [Fact]
    public async Task GetScoringDataPoints_ReturnsEmptyForCompanyWithNoData() {
        await SeedCompanyAndTaxonomy();

        ulong unknownCompanyId = 999;
        Result<IReadOnlyCollection<ScoringConceptValue>> result = await _dbm.GetScoringDataPoints(
            unknownCompanyId, ["StockholdersEquity"], _ct);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }
}
