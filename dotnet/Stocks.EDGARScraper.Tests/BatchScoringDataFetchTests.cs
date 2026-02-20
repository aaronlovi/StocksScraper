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

namespace Stocks.EDGARScraper.Tests;

public class BatchScoringDataFetchTests {
    private readonly DbmInMemoryService _dbm = new();
    private readonly CancellationToken _ct = CancellationToken.None;

    private static readonly string[] TestConceptNames = ["Revenues", "NetIncomeLoss", "Assets"];
    private static readonly DataPointUnit UsdUnit = new(1, "USD");

    private async Task SeedTaxonomyConcepts() {
        await _dbm.BulkInsertTaxonomyConcepts([
            new ConceptDetailsDTO(1, 1, 1, 2, false, "Revenues", "Revenues", "Total revenues"),
            new ConceptDetailsDTO(2, 1, 1, 1, false, "NetIncomeLoss", "Net Income", "Net income or loss"),
            new ConceptDetailsDTO(3, 1, 1, 2, false, "Assets", "Assets", "Total assets"),
            new ConceptDetailsDTO(4, 1, 1, 1, false, "Liabilities", "Liabilities", "Total liabilities")
        ], _ct);
    }

    private async Task SeedCompaniesAndSubmissions() {
        await _dbm.BulkInsertCompanies([
            new Company(1, 320193, "EDGAR"),
            new Company(2, 789019, "EDGAR"),
            new Company(3, 999999, "EDGAR")
        ], _ct);

        // Company 1: 6 10-K filings (should only use 5 most recent)
        await _dbm.BulkInsertSubmissions([
            new Submission(100, 1, "0001-100", FilingType.TenK, FilingCategory.Annual, new DateOnly(2019, 9, 28), null),
            new Submission(101, 1, "0001-101", FilingType.TenK, FilingCategory.Annual, new DateOnly(2020, 9, 26), null),
            new Submission(102, 1, "0001-102", FilingType.TenK, FilingCategory.Annual, new DateOnly(2021, 9, 25), null),
            new Submission(103, 1, "0001-103", FilingType.TenK, FilingCategory.Annual, new DateOnly(2022, 9, 24), null),
            new Submission(104, 1, "0001-104", FilingType.TenK, FilingCategory.Annual, new DateOnly(2023, 9, 30), null),
            new Submission(105, 1, "0001-105", FilingType.TenK, FilingCategory.Annual, new DateOnly(2024, 9, 28), null),
        ], _ct);

        // Company 1: 10-Q filing (should be excluded)
        await _dbm.BulkInsertSubmissions([
            new Submission(110, 1, "0001-110", FilingType.TenQ, FilingCategory.Annual, new DateOnly(2024, 3, 30), null),
        ], _ct);

        // Company 2: 2 10-K filings
        await _dbm.BulkInsertSubmissions([
            new Submission(200, 2, "0002-200", FilingType.TenK, FilingCategory.Annual, new DateOnly(2023, 6, 30), null),
            new Submission(201, 2, "0002-201", FilingType.TenK, FilingCategory.Annual, new DateOnly(2024, 6, 30), null),
        ], _ct);

        // Company 3: no 10-K filings (only 10-Q)
        await _dbm.BulkInsertSubmissions([
            new Submission(300, 3, "0003-300", FilingType.TenQ, FilingCategory.Annual, new DateOnly(2024, 3, 31), null),
        ], _ct);
    }

    private async Task SeedDataPoints() {
        var dataPoints = new List<DataPoint>();
        ulong dpId = 1000;

        // Company 1: data points for 6 years, across all 3 test concepts
        ulong[] company1Submissions = [100, 101, 102, 103, 104, 105];
        foreach (ulong subId in company1Submissions) {
            dataPoints.Add(new DataPoint(dpId++, 1, "Revenues", "ref", new DatePair(new DateOnly(2019, 1, 1), new DateOnly(2024, 12, 31)), 100_000m, UsdUnit, new DateOnly(2024, 11, 1), subId, 1));
            dataPoints.Add(new DataPoint(dpId++, 1, "NetIncomeLoss", "ref", new DatePair(new DateOnly(2019, 1, 1), new DateOnly(2024, 12, 31)), 20_000m, UsdUnit, new DateOnly(2024, 11, 1), subId, 2));
            dataPoints.Add(new DataPoint(dpId++, 1, "Assets", "ref", new DatePair(new DateOnly(2019, 1, 1), new DateOnly(2024, 12, 31)), 500_000m, UsdUnit, new DateOnly(2024, 11, 1), subId, 3));
        }

        // Company 1: data point for 10-Q (should be excluded)
        dataPoints.Add(new DataPoint(dpId++, 1, "Revenues", "ref", new DatePair(new DateOnly(2024, 1, 1), new DateOnly(2024, 3, 30)), 25_000m, UsdUnit, new DateOnly(2024, 5, 1), 110, 1));

        // Company 1: data point for concept NOT in TestConceptNames (should be excluded by filter)
        dataPoints.Add(new DataPoint(dpId++, 1, "Liabilities", "ref", new DatePair(new DateOnly(2019, 1, 1), new DateOnly(2024, 12, 31)), 300_000m, UsdUnit, new DateOnly(2024, 11, 1), 105, 4));

        // Company 2: data points for 2 years
        ulong[] company2Submissions = [200, 201];
        foreach (ulong subId in company2Submissions) {
            dataPoints.Add(new DataPoint(dpId++, 2, "Revenues", "ref", new DatePair(new DateOnly(2023, 1, 1), new DateOnly(2024, 6, 30)), 200_000m, UsdUnit, new DateOnly(2024, 8, 1), subId, 1));
            dataPoints.Add(new DataPoint(dpId++, 2, "NetIncomeLoss", "ref", new DatePair(new DateOnly(2023, 1, 1), new DateOnly(2024, 6, 30)), 50_000m, UsdUnit, new DateOnly(2024, 8, 1), subId, 2));
        }

        // Company 3: data point for 10-Q only (should produce no results for scoring)
        dataPoints.Add(new DataPoint(dpId++, 3, "Revenues", "ref", new DatePair(new DateOnly(2024, 1, 1), new DateOnly(2024, 3, 31)), 10_000m, UsdUnit, new DateOnly(2024, 5, 1), 300, 1));

        await _dbm.BulkInsertDataPoints(dataPoints, _ct);
    }

    private async Task SeedAll() {
        await SeedTaxonomyConcepts();
        await SeedCompaniesAndSubmissions();
        await SeedDataPoints();
    }

    // --- GetAllScoringDataPoints tests ---

    [Fact]
    public async Task GetAllScoringDataPoints_ReturnsOnlyTenKData() {
        await SeedAll();

        Result<IReadOnlyCollection<BatchScoringConceptValue>> result =
            await _dbm.GetAllScoringDataPoints(TestConceptNames, _ct);
        Assert.True(result.IsSuccess);

        // Company 3 has only 10-Q, should have no results
        bool hasCompany3 = false;
        foreach (BatchScoringConceptValue v in result.Value!) {
            if (v.CompanyId == 3)
                hasCompany3 = true;
        }
        Assert.False(hasCompany3, "Company 3 (only 10-Q) should have no scoring data");
    }

    [Fact]
    public async Task GetAllScoringDataPoints_LimitsToFiveMostRecentYears() {
        await SeedAll();

        Result<IReadOnlyCollection<BatchScoringConceptValue>> result =
            await _dbm.GetAllScoringDataPoints(TestConceptNames, _ct);
        Assert.True(result.IsSuccess);

        // Company 1 has 6 10-K years, should only return data for the 5 most recent
        var company1Dates = new HashSet<DateOnly>();
        foreach (BatchScoringConceptValue v in result.Value!) {
            if (v.CompanyId == 1)
                company1Dates.Add(v.ReportDate);
        }

        Assert.True(company1Dates.Count <= 5, $"Expected at most 5 distinct dates for company 1, got {company1Dates.Count}");
        // The oldest date (2019-09-28) should be excluded
        Assert.DoesNotContain(new DateOnly(2019, 9, 28), company1Dates);
    }

    [Fact]
    public async Task GetAllScoringDataPoints_FiltersConceptNames() {
        await SeedAll();

        Result<IReadOnlyCollection<BatchScoringConceptValue>> result =
            await _dbm.GetAllScoringDataPoints(TestConceptNames, _ct);
        Assert.True(result.IsSuccess);

        // "Liabilities" is not in TestConceptNames, should not appear
        foreach (BatchScoringConceptValue v in result.Value!) {
            Assert.NotEqual("Liabilities", v.ConceptName);
        }
    }

    [Fact]
    public async Task GetAllScoringDataPoints_ReturnsMultipleCompanies() {
        await SeedAll();

        Result<IReadOnlyCollection<BatchScoringConceptValue>> result =
            await _dbm.GetAllScoringDataPoints(TestConceptNames, _ct);
        Assert.True(result.IsSuccess);

        var companyIds = new HashSet<ulong>();
        foreach (BatchScoringConceptValue v in result.Value!)
            companyIds.Add(v.CompanyId);

        Assert.Contains(1UL, companyIds);
        Assert.Contains(2UL, companyIds);
    }

    [Fact]
    public async Task GetAllScoringDataPoints_EmptyWhenNoData() {
        // No seeding at all
        Result<IReadOnlyCollection<BatchScoringConceptValue>> result =
            await _dbm.GetAllScoringDataPoints(TestConceptNames, _ct);
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public async Task GetAllScoringDataPoints_DeduplicatesByEndDate() {
        await SeedTaxonomyConcepts();

        await _dbm.BulkInsertCompanies([new Company(10, 111111, "EDGAR")], _ct);
        await _dbm.BulkInsertSubmissions([
            new Submission(500, 10, "0010-500", FilingType.TenK, FilingCategory.Annual, new DateOnly(2024, 12, 31), null)
        ], _ct);

        // Two data points for the same concept in the same submission, different end dates
        await _dbm.BulkInsertDataPoints([
            new DataPoint(9001, 10, "Revenues", "ref-a", new DatePair(new DateOnly(2024, 1, 1), new DateOnly(2024, 6, 30)), 50_000m, UsdUnit, new DateOnly(2025, 1, 1), 500, 1),
            new DataPoint(9002, 10, "Revenues", "ref-b", new DatePair(new DateOnly(2024, 1, 1), new DateOnly(2024, 12, 31)), 100_000m, UsdUnit, new DateOnly(2025, 1, 1), 500, 1),
        ], _ct);

        Result<IReadOnlyCollection<BatchScoringConceptValue>> result =
            await _dbm.GetAllScoringDataPoints(new[] { "Revenues" }, _ct);
        Assert.True(result.IsSuccess);

        // Should return only one row (the one with later end_date = 2024-12-31, value = 100_000)
        BatchScoringConceptValue single = Assert.Single(result.Value!);
        Assert.Equal(100_000m, single.Value);
    }

    // --- GetAllLatestPrices tests ---

    [Fact]
    public async Task GetAllLatestPrices_ReturnsLatestPerTicker() {
        await _dbm.BulkInsertPrices([
            new PriceRow(1, 320193, "AAPL", "NASDAQ", "aapl.us", new DateOnly(2024, 12, 18), 248m, 252m, 247m, 250m, 50_000_000),
            new PriceRow(2, 320193, "AAPL", "NASDAQ", "aapl.us", new DateOnly(2024, 12, 19), 250m, 255m, 249m, 254m, 45_000_000),
            new PriceRow(3, 789019, "MSFT", "NASDAQ", "msft.us", new DateOnly(2024, 12, 17), 420m, 425m, 418m, 422m, 30_000_000),
            new PriceRow(4, 789019, "MSFT", "NASDAQ", "msft.us", new DateOnly(2024, 12, 19), 425m, 430m, 424m, 428m, 35_000_000),
        ], _ct);

        Result<IReadOnlyCollection<LatestPrice>> result = await _dbm.GetAllLatestPrices(_ct);
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count);

        var priceByTicker = new Dictionary<string, LatestPrice>(StringComparer.OrdinalIgnoreCase);
        foreach (LatestPrice lp in result.Value!)
            priceByTicker[lp.Ticker] = lp;

        Assert.Equal(254m, priceByTicker["AAPL"].Close);
        Assert.Equal(new DateOnly(2024, 12, 19), priceByTicker["AAPL"].PriceDate);
        Assert.Equal(428m, priceByTicker["MSFT"].Close);
        Assert.Equal(new DateOnly(2024, 12, 19), priceByTicker["MSFT"].PriceDate);
    }

    [Fact]
    public async Task GetAllLatestPrices_EmptyWhenNoPrices() {
        Result<IReadOnlyCollection<LatestPrice>> result = await _dbm.GetAllLatestPrices(_ct);
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public async Task GetAllLatestPrices_SinglePricePerTicker() {
        await _dbm.BulkInsertPrices([
            new PriceRow(10, 320193, "AAPL", "NASDAQ", "aapl.us", new DateOnly(2024, 12, 19), 250m, 255m, 249m, 254m, 45_000_000),
        ], _ct);

        Result<IReadOnlyCollection<LatestPrice>> result = await _dbm.GetAllLatestPrices(_ct);
        Assert.True(result.IsSuccess);

        LatestPrice single = Assert.Single(result.Value!);
        Assert.Equal("AAPL", single.Ticker);
        Assert.Equal(254m, single.Close);
    }
}
