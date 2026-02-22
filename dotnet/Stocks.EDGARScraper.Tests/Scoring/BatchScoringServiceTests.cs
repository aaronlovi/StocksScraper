using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Stocks.DataModels;
using Stocks.DataModels.Enums;
using Stocks.DataModels.Scoring;
using Stocks.Persistence.Database;
using Stocks.Persistence.Database.DTO.Taxonomies;
using Stocks.Persistence.Services;
using Stocks.Shared;

namespace Stocks.EDGARScraper.Tests.Scoring;

public class BatchScoringServiceTests {
    private readonly DbmInMemoryService _dbm = new();
    private readonly CancellationToken _ct = CancellationToken.None;
    private static readonly DataPointUnit UsdUnit = new(1, "USD");
    private static readonly DataPointUnit SharesUnit = new(2, "shares");

    private async Task SeedTaxonomyConcepts() {
        await _dbm.BulkInsertTaxonomyConcepts([
            // Balance sheet
            new ConceptDetailsDTO(1, 1, 2, 2, false, "Assets", "Assets", ""),
            new ConceptDetailsDTO(2, 1, 2, 1, false, "Liabilities", "Liabilities", ""),
            new ConceptDetailsDTO(3, 1, 2, 1, false, "StockholdersEquity", "Equity", ""),
            new ConceptDetailsDTO(4, 1, 2, 2, false, "Goodwill", "Goodwill", ""),
            new ConceptDetailsDTO(5, 1, 2, 2, false, "IntangibleAssetsNetExcludingGoodwill", "Intangibles", ""),
            new ConceptDetailsDTO(6, 1, 2, 1, false, "LongTermDebt", "Debt", ""),
            new ConceptDetailsDTO(7, 1, 2, 2, false, "RetainedEarningsAccumulatedDeficit", "RE", ""),
            new ConceptDetailsDTO(8, 1, 2, 2, false, "CommonStockSharesOutstanding", "Shares", ""),
            // Income statement
            new ConceptDetailsDTO(9, 1, 1, 1, false, "NetIncomeLoss", "Net Income", ""),
            // Cash flow
            new ConceptDetailsDTO(10, 1, 1, 2, false, "CashCashEquivalentsRestrictedCashAndRestrictedCashEquivalentsPeriodIncreaseDecreaseIncludingExchangeRateEffect", "Cash Change", ""),
            new ConceptDetailsDTO(11, 1, 1, 2, false, "PaymentsToAcquirePropertyPlantAndEquipment", "CapEx", ""),
            new ConceptDetailsDTO(12, 1, 1, 2, false, "DepreciationDepletionAndAmortization", "DDA", ""),
        ], _ct);
    }

    private async Task SeedCompanyWithScoringData(ulong companyId, ulong cik, string name,
        string ticker, string exchange, int numYears) {
        await _dbm.BulkInsertCompanies([new Company(companyId, cik, "EDGAR")], _ct);
        await _dbm.BulkInsertCompanyNames([new CompanyName(companyId * 100, companyId, name)], _ct);
        await _dbm.BulkInsertCompanyTickers([new CompanyTicker(companyId, ticker, exchange)], _ct);

        var submissions = new List<Submission>();
        var dataPoints = new List<DataPoint>();
        ulong subId = companyId * 1000;
        ulong dpId = companyId * 10000;

        for (int i = 0; i < numYears; i++) {
            int year = 2024 - i;
            var reportDate = new DateOnly(year, 12, 31);
            submissions.Add(new Submission(subId, companyId, $"ref-{companyId}-{year}",
                FilingType.TenK, FilingCategory.Annual, reportDate, null));

            var endDate = new DateOnly(year, 12, 31);
            var startDate = new DateOnly(year, 1, 1);
            var dp = new DatePair(startDate, endDate);
            var filed = new DateOnly(year + 1, 2, 1);

            // Balance sheet
            dataPoints.Add(new DataPoint(dpId++, companyId, "Assets", "ref", dp, 1_000_000_000m, UsdUnit, filed, subId, 1));
            dataPoints.Add(new DataPoint(dpId++, companyId, "Liabilities", "ref", dp, 400_000_000m, UsdUnit, filed, subId, 2));
            dataPoints.Add(new DataPoint(dpId++, companyId, "StockholdersEquity", "ref", dp, 600_000_000m, UsdUnit, filed, subId, 3));
            dataPoints.Add(new DataPoint(dpId++, companyId, "Goodwill", "ref", dp, 50_000_000m, UsdUnit, filed, subId, 4));
            dataPoints.Add(new DataPoint(dpId++, companyId, "IntangibleAssetsNetExcludingGoodwill", "ref", dp, 30_000_000m, UsdUnit, filed, subId, 5));
            dataPoints.Add(new DataPoint(dpId++, companyId, "LongTermDebt", "ref", dp, 200_000_000m, UsdUnit, filed, subId, 6));
            dataPoints.Add(new DataPoint(dpId++, companyId, "RetainedEarningsAccumulatedDeficit", "ref", dp, 300_000_000m + (i * 50_000_000m), UsdUnit, filed, subId, 7));
            dataPoints.Add(new DataPoint(dpId++, companyId, "CommonStockSharesOutstanding", "ref", dp, 100_000_000m, SharesUnit, filed, subId, 8));

            // Income statement
            dataPoints.Add(new DataPoint(dpId++, companyId, "NetIncomeLoss", "ref", dp, 80_000_000m, UsdUnit, filed, subId, 9));

            // Cash flow
            dataPoints.Add(new DataPoint(dpId++, companyId, "CashCashEquivalentsRestrictedCashAndRestrictedCashEquivalentsPeriodIncreaseDecreaseIncludingExchangeRateEffect",
                "ref", dp, 120_000_000m, UsdUnit, filed, subId, 10));
            dataPoints.Add(new DataPoint(dpId++, companyId, "PaymentsToAcquirePropertyPlantAndEquipment", "ref", dp, 30_000_000m, UsdUnit, filed, subId, 11));
            dataPoints.Add(new DataPoint(dpId++, companyId, "DepreciationDepletionAndAmortization", "ref", dp, 40_000_000m, UsdUnit, filed, subId, 12));

            subId++;
        }

        await _dbm.BulkInsertSubmissions(submissions, _ct);
        await _dbm.BulkInsertDataPoints(dataPoints, _ct);
    }

    private async Task SeedPrices(string ticker, decimal close) {
        await _dbm.BulkInsertPrices([
            new PriceRow(1, 0, ticker, "NYSE", ticker.ToLowerInvariant() + ".us",
                new DateOnly(2024, 12, 19), close - 2m, close + 1m, close - 3m, close, 10_000_000),
        ], _ct);
    }

    [Fact]
    public async Task ComputeAllScores_ReturnsCorrectNumberOfResults() {
        await SeedTaxonomyConcepts();
        await SeedCompanyWithScoringData(1, 320193, "Apple Inc", "AAPL", "NASDAQ", 5);
        await SeedCompanyWithScoringData(2, 789019, "Microsoft Corporation", "MSFT", "NASDAQ", 3);
        await SeedPrices("AAPL", 250m);
        await SeedPrices("MSFT", 430m);

        var service = new ScoringService(_dbm);
        Result<IReadOnlyCollection<CompanyScoreSummary>> result = await service.ComputeAllScores(_ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count);
    }

    [Fact]
    public async Task ComputeAllScores_MatchesIndividualScoring() {
        await SeedTaxonomyConcepts();
        await SeedCompanyWithScoringData(1, 320193, "Apple Inc", "AAPL", "NASDAQ", 5);
        await SeedPrices("AAPL", 250m);

        var service = new ScoringService(_dbm);

        // Compute batch
        Result<IReadOnlyCollection<CompanyScoreSummary>> batchResult = await service.ComputeAllScores(_ct);
        Assert.True(batchResult.IsSuccess);
        CompanyScoreSummary batchScore = Assert.Single(batchResult.Value!);

        // Compute individual
        Result<ScoringResult> individualResult = await service.ComputeScore(1, _ct);
        Assert.True(individualResult.IsSuccess);

        // Verify scores match
        Assert.Equal(individualResult.Value!.OverallScore, batchScore.OverallScore);
        Assert.Equal(individualResult.Value.ComputableChecks, batchScore.ComputableChecks);
        Assert.Equal(individualResult.Value.YearsOfData, batchScore.YearsOfData);
    }

    [Fact]
    public async Task ComputeAllScores_CompanyWithNoPriceStillGetsScore() {
        await SeedTaxonomyConcepts();
        await SeedCompanyWithScoringData(1, 320193, "Apple Inc", "AAPL", "NASDAQ", 4);
        // No prices seeded

        var service = new ScoringService(_dbm);
        Result<IReadOnlyCollection<CompanyScoreSummary>> result = await service.ComputeAllScores(_ct);

        Assert.True(result.IsSuccess);
        CompanyScoreSummary score = Assert.Single(result.Value!);
        Assert.Null(score.PricePerShare);
        Assert.Null(score.MarketCap);
        Assert.True(score.OverallScore >= 0);
    }

    [Fact]
    public async Task ComputeAllScores_CompanyWithNoTenKProducesNoScore() {
        await SeedTaxonomyConcepts();

        // Company with only 10-Q filings
        await _dbm.BulkInsertCompanies([new Company(5, 999999, "EDGAR")], _ct);
        await _dbm.BulkInsertCompanyNames([new CompanyName(500, 5, "NoTenK Corp")], _ct);
        await _dbm.BulkInsertSubmissions([
            new Submission(5000, 5, "ref-5-q", FilingType.TenQ, FilingCategory.Quarterly,
                new DateOnly(2024, 3, 31), null),
        ], _ct);
        await _dbm.BulkInsertDataPoints([
            new DataPoint(50000, 5, "Assets", "ref", new DatePair(new DateOnly(2024, 1, 1), new DateOnly(2024, 3, 31)),
                100_000m, UsdUnit, new DateOnly(2024, 5, 1), 5000, 1),
        ], _ct);

        var service = new ScoringService(_dbm);
        Result<IReadOnlyCollection<CompanyScoreSummary>> result = await service.ComputeAllScores(_ct);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public async Task ComputeAllScores_PopulatesCompanyMetadata() {
        await SeedTaxonomyConcepts();
        await SeedCompanyWithScoringData(1, 320193, "Apple Inc", "AAPL", "NASDAQ", 3);
        await SeedPrices("AAPL", 250m);

        var service = new ScoringService(_dbm);
        Result<IReadOnlyCollection<CompanyScoreSummary>> result = await service.ComputeAllScores(_ct);

        Assert.True(result.IsSuccess);
        CompanyScoreSummary score = Assert.Single(result.Value!);
        Assert.Equal(1UL, score.CompanyId);
        Assert.Equal("320193", score.Cik);
        Assert.Equal("Apple Inc", score.CompanyName);
        Assert.Equal("AAPL", score.Ticker);
        Assert.Equal("NASDAQ", score.Exchange);
        Assert.Equal(250m, score.PricePerShare);
    }

    [Fact]
    public async Task TruncateAndBulkInsert_RoundTrip() {
        var now = DateTime.UtcNow;
        var scores = new List<CompanyScoreSummary> {
            new CompanyScoreSummary(1, "320193", "Apple Inc", "AAPL", "NASDAQ",
                10, 13, 5, 500_000_000m, 3_000_000_000_000m, 0.3m, 5.0m, 0.4m,
                200_000_000m, 100_000_000m, 90_000_000m, 12.5m, 11.0m, 8.5m, 7.2m,
                250m, new DateOnly(2024, 12, 19), 100_000_000, null, null, null, null, now),
        };

        Result insertResult = await _dbm.BulkInsertCompanyScores(scores, _ct);
        Assert.True(insertResult.IsSuccess);

        IReadOnlyCollection<CompanyScoreSummary> stored = _dbm.GetInMemoryData().GetCompanyScores();
        Assert.Single(stored);

        Result truncateResult = await _dbm.TruncateCompanyScores(_ct);
        Assert.True(truncateResult.IsSuccess);

        IReadOnlyCollection<CompanyScoreSummary> afterTruncate = _dbm.GetInMemoryData().GetCompanyScores();
        Assert.Empty(afterTruncate);
    }

    [Fact]
    public async Task ComputeAllScores_UsesQuarterlyBalanceSheet_WhenMoreRecent() {
        await SeedTaxonomyConcepts();

        // Seed company with 2 years of 10-K data
        await SeedCompanyWithScoringData(1, 320193, "Apple Inc", "AAPL", "NASDAQ", 2);
        await SeedPrices("AAPL", 250m);

        // Add a more recent 10-Q with updated balance sheet values
        var tenQDate = new DateOnly(2025, 3, 31);
        await _dbm.BulkInsertSubmissions([
            new Submission(9999, 1, "ref-1-q1", FilingType.TenQ, FilingCategory.Quarterly, tenQDate, null)
        ], _ct);

        var dp = new DatePair(new DateOnly(2025, 1, 1), tenQDate);
        var filed = new DateOnly(2025, 5, 1);
        await _dbm.BulkInsertDataPoints([
            new DataPoint(99001, 1, "Assets", "ref", dp, 1_200_000_000m, UsdUnit, filed, 9999, 1),
            new DataPoint(99002, 1, "Liabilities", "ref", dp, 500_000_000m, UsdUnit, filed, 9999, 2),
            new DataPoint(99003, 1, "StockholdersEquity", "ref", dp, 700_000_000m, UsdUnit, filed, 9999, 3),
            new DataPoint(99004, 1, "Goodwill", "ref", dp, 60_000_000m, UsdUnit, filed, 9999, 4),
            new DataPoint(99005, 1, "IntangibleAssetsNetExcludingGoodwill", "ref", dp, 35_000_000m, UsdUnit, filed, 9999, 5),
            new DataPoint(99006, 1, "LongTermDebt", "ref", dp, 180_000_000m, UsdUnit, filed, 9999, 6),
            new DataPoint(99007, 1, "RetainedEarningsAccumulatedDeficit", "ref", dp, 280_000_000m, UsdUnit, filed, 9999, 7),
            new DataPoint(99008, 1, "CommonStockSharesOutstanding", "ref", dp, 105_000_000m, SharesUnit, filed, 9999, 8),
        ], _ct);

        var service = new ScoringService(_dbm);
        Result<IReadOnlyCollection<CompanyScoreSummary>> result = await service.ComputeAllScores(_ct);

        Assert.True(result.IsSuccess);
        CompanyScoreSummary score = Assert.Single(result.Value!);

        // yearsOfData = 2 (only 10-K years count)
        Assert.Equal(2, score.YearsOfData);

        // Shares from quarterly snapshot
        Assert.Equal(105_000_000, score.SharesOutstanding);

        // BookValue from quarterly: Equity(700M) - Goodwill(60M) - Intangibles(35M) = 605M
        Assert.Equal(605_000_000m, score.BookValue);
    }

    [Fact]
    public async Task ComputeAllScores_TenKOneTenQ_UsesQuarterlyForBalanceSheet() {
        await SeedTaxonomyConcepts();

        // 1 year of 10-K data + more recent 10-Q
        await SeedCompanyWithScoringData(1, 320193, "Apple Inc", "AAPL", "NASDAQ", 1);
        await SeedPrices("AAPL", 250m);

        var tenQDate = new DateOnly(2025, 6, 30);
        await _dbm.BulkInsertSubmissions([
            new Submission(8888, 1, "ref-1-q2", FilingType.TenQ, FilingCategory.Quarterly, tenQDate, null)
        ], _ct);

        var dp = new DatePair(new DateOnly(2025, 4, 1), tenQDate);
        var filed = new DateOnly(2025, 8, 1);
        await _dbm.BulkInsertDataPoints([
            new DataPoint(88001, 1, "StockholdersEquity", "ref", dp, 750_000_000m, UsdUnit, filed, 8888, 3),
            new DataPoint(88002, 1, "CommonStockSharesOutstanding", "ref", dp, 110_000_000m, SharesUnit, filed, 8888, 8),
        ], _ct);

        var service = new ScoringService(_dbm);

        // Compute batch
        Result<IReadOnlyCollection<CompanyScoreSummary>> batchResult = await service.ComputeAllScores(_ct);
        Assert.True(batchResult.IsSuccess);
        CompanyScoreSummary batchScore = Assert.Single(batchResult.Value!);

        // Compute individual
        Result<ScoringResult> individualResult = await service.ComputeScore(1, _ct);
        Assert.True(individualResult.IsSuccess);

        // Both should match and use the quarterly balance sheet
        Assert.Equal(individualResult.Value!.OverallScore, batchScore.OverallScore);
        Assert.Equal(1, batchScore.YearsOfData);
    }
}
