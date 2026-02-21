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

public class BatchMoatScoringServiceTests {
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
            // Moat-specific concepts
            new ConceptDetailsDTO(13, 1, 1, 1, false, "Revenues", "Revenues", ""),
            new ConceptDetailsDTO(14, 1, 1, 1, false, "CostOfGoodsAndServicesSold", "COGS", ""),
            new ConceptDetailsDTO(15, 1, 1, 1, false, "GrossProfit", "Gross Profit", ""),
            new ConceptDetailsDTO(16, 1, 1, 1, false, "OperatingIncomeLoss", "Operating Income", ""),
            new ConceptDetailsDTO(17, 1, 1, 1, false, "InterestExpense", "Interest Expense", ""),
            new ConceptDetailsDTO(18, 1, 1, 1, false, "PaymentsOfDividends", "Dividends", ""),
            new ConceptDetailsDTO(19, 1, 1, 2, false, "PaymentsForRepurchaseOfCommonStock", "Buybacks", ""),
            new ConceptDetailsDTO(20, 1, 2, 1, false, "LiabilitiesAndStockholdersEquity", "Total L+E", ""),
        ], _ct);
    }

    private async Task SeedCompanyWithMoatData(ulong companyId, ulong cik, string name,
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
            dataPoints.Add(new DataPoint(dpId++, companyId, "LongTermDebt", "ref", dp, 200_000_000m, UsdUnit, filed, subId, 6));
            dataPoints.Add(new DataPoint(dpId++, companyId, "RetainedEarningsAccumulatedDeficit", "ref", dp, 300_000_000m + (i * 50_000_000m), UsdUnit, filed, subId, 7));
            dataPoints.Add(new DataPoint(dpId++, companyId, "CommonStockSharesOutstanding", "ref", dp, 100_000_000m, SharesUnit, filed, subId, 8));
            dataPoints.Add(new DataPoint(dpId++, companyId, "LiabilitiesAndStockholdersEquity", "ref", dp, 1_000_000_000m, UsdUnit, filed, subId, 20));

            // Income statement
            dataPoints.Add(new DataPoint(dpId++, companyId, "NetIncomeLoss", "ref", dp, 150_000_000m, UsdUnit, filed, subId, 9));
            dataPoints.Add(new DataPoint(dpId++, companyId, "Revenues", "ref", dp, 500_000_000m + (i * 20_000_000m), UsdUnit, filed, subId, 13));
            dataPoints.Add(new DataPoint(dpId++, companyId, "GrossProfit", "ref", dp, 250_000_000m, UsdUnit, filed, subId, 15));
            dataPoints.Add(new DataPoint(dpId++, companyId, "OperatingIncomeLoss", "ref", dp, 180_000_000m, UsdUnit, filed, subId, 16));
            dataPoints.Add(new DataPoint(dpId++, companyId, "InterestExpense", "ref", dp, 20_000_000m, UsdUnit, filed, subId, 17));

            // Cash flow
            dataPoints.Add(new DataPoint(dpId++, companyId, "CashCashEquivalentsRestrictedCashAndRestrictedCashEquivalentsPeriodIncreaseDecreaseIncludingExchangeRateEffect",
                "ref", dp, 120_000_000m, UsdUnit, filed, subId, 10));
            dataPoints.Add(new DataPoint(dpId++, companyId, "PaymentsToAcquirePropertyPlantAndEquipment", "ref", dp, 30_000_000m, UsdUnit, filed, subId, 11));
            dataPoints.Add(new DataPoint(dpId++, companyId, "DepreciationDepletionAndAmortization", "ref", dp, 40_000_000m, UsdUnit, filed, subId, 12));
            dataPoints.Add(new DataPoint(dpId++, companyId, "PaymentsOfDividends", "ref", dp, 25_000_000m, UsdUnit, filed, subId, 18));
            dataPoints.Add(new DataPoint(dpId++, companyId, "PaymentsForRepurchaseOfCommonStock", "ref", dp, 50_000_000m, UsdUnit, filed, subId, 19));

            subId++;
        }

        await _dbm.BulkInsertSubmissions(submissions, _ct);
        await _dbm.BulkInsertDataPoints(dataPoints, _ct);
    }

    private async Task SeedPrices(string ticker, string exchange, decimal close) {
        await _dbm.BulkInsertPrices([
            new PriceRow(1, 0, ticker, exchange, ticker.ToLowerInvariant() + ".us",
                new DateOnly(2024, 12, 19), close - 2m, close + 1m, close - 3m, close, 10_000_000),
        ], _ct);
    }

    [Fact]
    public async Task ComputeAllMoatScores_SingleCompany_ReturnsScore() {
        await SeedTaxonomyConcepts();
        await SeedCompanyWithMoatData(1, 320193, "Apple Inc", "AAPL", "NASDAQ", 8);
        await SeedPrices("AAPL", "NASDAQ", 250m);

        var service = new MoatScoringService(_dbm);
        Result<IReadOnlyCollection<CompanyMoatScoreSummary>> result = await service.ComputeAllMoatScores(_ct);

        Assert.True(result.IsSuccess);
        CompanyMoatScoreSummary score = Assert.Single(result.Value!);
        Assert.Equal(1UL, score.CompanyId);
        Assert.Equal("320193", score.Cik);
        Assert.Equal("Apple Inc", score.CompanyName);
        Assert.Equal("AAPL", score.Ticker);
        Assert.Equal("NASDAQ", score.Exchange);
        Assert.True(score.OverallScore > 0, "Expected at least some passing checks");
        Assert.Equal(8, score.YearsOfData);
        Assert.Equal(250m, score.PricePerShare);
    }

    [Fact]
    public async Task ComputeAllMoatScores_CompanyWithInsufficientData_ReturnsLowScore() {
        await SeedTaxonomyConcepts();
        await SeedCompanyWithMoatData(1, 320193, "NewCo", "NEW", "NYSE", 1);
        await SeedPrices("NEW", "NYSE", 50m);

        var service = new MoatScoringService(_dbm);
        Result<IReadOnlyCollection<CompanyMoatScoreSummary>> result = await service.ComputeAllMoatScores(_ct);

        Assert.True(result.IsSuccess);
        CompanyMoatScoreSummary score = Assert.Single(result.Value!);
        Assert.Equal(1, score.YearsOfData);
        // History check (>= 7 years) should fail with only 1 year
        Assert.True(score.OverallScore < score.ComputableChecks,
            "Expected some failing checks with insufficient data");
    }

    [Fact]
    public async Task ComputeAllMoatScores_MultipleCompanies_ReturnsAll() {
        await SeedTaxonomyConcepts();
        await SeedCompanyWithMoatData(1, 320193, "Apple Inc", "AAPL", "NASDAQ", 8);
        await SeedCompanyWithMoatData(2, 789019, "Microsoft", "MSFT", "NASDAQ", 5);
        await SeedCompanyWithMoatData(3, 66740, "3M Company", "MMM", "NYSE", 3);
        await SeedPrices("AAPL", "NASDAQ", 250m);
        await SeedPrices("MSFT", "NASDAQ", 430m);
        await SeedPrices("MMM", "NYSE", 120m);

        var service = new MoatScoringService(_dbm);
        Result<IReadOnlyCollection<CompanyMoatScoreSummary>> result = await service.ComputeAllMoatScores(_ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value!.Count);
    }
}
