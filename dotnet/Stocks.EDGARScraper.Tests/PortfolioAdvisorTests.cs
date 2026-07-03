using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Stocks.DataModels.Scoring;
using Stocks.Persistence.Database;
using Stocks.Persistence.Services;
using Stocks.Shared;

namespace Stocks.EDGARScraper.Tests;

public class PortfolioAdvisorTests {
    private readonly DbmInMemoryService _dbm = new();
    private readonly CancellationToken _ct = CancellationToken.None;

    private static readonly DateOnly Baseline = new(2026, 6, 26);

    private static CompanyScoreSummary MakeScore(
        ulong companyId, string cik, string? name, string? ticker,
        int score, int computable, decimal? price,
        decimal? bookValue = null, decimal? estimatedReturnCf = null) =>
        new(companyId, cik, name, ticker, "NYSE", score, computable, 5,
            bookValue, null, null, null, null, null, null, null, null, null,
            estimatedReturnCf, null,
            price, Baseline, null, null, null, null, DateTime.UtcNow);

    private async Task SeedScenario() {
        // Current live scores
        _ = await _dbm.BulkInsertCompanyScores([
            MakeScore(1, "111", "Hold Co", "HLD", 15, 15, 100m, 500_000_000m),
            // Dropped from 15 to 14: est return rose above 40% with unchanged fundamentals
            MakeScore(2, "222", "Sell Co", "SEL", 14, 15, 26.76m, 618_000_000m, 40.1m),
            // A fresh 15/15 not held by the user
            MakeScore(3, "333", "Buy Co", "BUY", 15, 15, 55m, 300_000_000m, 20m),
        ], _ct);

        // Baseline snapshot
        _ = await _dbm.BulkInsertGrahamScoreSnapshots(Baseline, [
            MakeScore(1, "111", "Hold Co", "HLD", 15, 15, 99m, 500_000_000m),
            MakeScore(2, "222", "Sell Co", "SEL", 15, 15, 29.14m, 618_000_000m, 36.8m),
            MakeScore(3, "333", "Buy Co", "BUY", 14, 15, 60m, 300_000_000m, 4.5m),
        ], _ct);
    }

    [Fact]
    public async Task GetRecommendations_ClassifiesHoldSellBuyUnknown() {
        await SeedScenario();
        var service = new PortfolioAdvisorService(_dbm);

        Result<PortfolioAdvisorReport> result =
            await service.GetRecommendations(["HLD", "sel", "NOPE"], _ct);
        Assert.True(result.IsSuccess);

        PortfolioAdvisorReport report = result.Value!;
        Assert.Equal(Baseline, report.BaselineSnapshotDate);

        PortfolioRecommendation hold = Assert.Single(report.Holds);
        Assert.Equal("HLD", hold.Ticker);

        PortfolioRecommendation sell = Assert.Single(report.Sells);
        Assert.Equal("SEL", sell.Ticker); // lowercase input normalized

        PortfolioRecommendation buy = Assert.Single(report.Buys);
        Assert.Equal("BUY", buy.Ticker);

        PortfolioRecommendation unknown = Assert.Single(report.Unknowns);
        Assert.Equal("NOPE", unknown.Ticker);
    }

    [Fact]
    public async Task GetRecommendations_SellReasonNamesTheFlippedCheckAndPriceTrigger() {
        await SeedScenario();
        var service = new PortfolioAdvisorService(_dbm);

        Result<PortfolioAdvisorReport> result = await service.GetRecommendations(["SEL"], _ct);
        Assert.True(result.IsSuccess);

        PortfolioRecommendation sell = Assert.Single(result.Value!.Sells);
        // Fundamentals identical to baseline -> price-driven
        Assert.Equal("price", sell.Trigger);
        // Est. Return (CF) crossed the < 40% ceiling: 36.8 -> 40.1
        Assert.Contains(sell.Reasons, r => r.Contains("Est. Return (CF) Not Too Big") && r.Contains("36.8") && r.Contains("40.1"));
        Assert.Contains(sell.Reasons, r => r.Contains("Dropped from 15/15"));
    }

    [Fact]
    public async Task GetRecommendations_BuyReasonShowsNewQualificationAndTrigger() {
        await SeedScenario();
        var service = new PortfolioAdvisorService(_dbm);

        Result<PortfolioAdvisorReport> result = await service.GetRecommendations(["HLD"], _ct);
        Assert.True(result.IsSuccess);

        PortfolioRecommendation buy = Assert.Single(result.Value!.Buys);
        Assert.Equal("BUY", buy.Ticker);
        // Est return moved from 4.5 (fail: not > 5%) to 20 (pass) with a price change AND
        // fundamentals identical -> price trigger; reason names the flipped check
        Assert.Contains(buy.Reasons, r => r.Contains("Newly qualified"));
        Assert.Contains(buy.Reasons, r => r.Contains("Est. Return (CF) Big Enough"));
        Assert.Equal("price", buy.Trigger);
    }

    [Fact]
    public async Task GetRecommendations_HeldPerfectScorerIsNotAlsoABuy() {
        await SeedScenario();
        var service = new PortfolioAdvisorService(_dbm);

        Result<PortfolioAdvisorReport> result = await service.GetRecommendations(["HLD", "BUY"], _ct);
        Assert.True(result.IsSuccess);

        Assert.Empty(result.Value!.Buys);
        Assert.Equal(2, result.Value!.Holds.Count);
    }

    [Fact]
    public async Task GetRecommendations_WorksWithoutBaselineSnapshot() {
        _ = await _dbm.BulkInsertCompanyScores([
            MakeScore(1, "111", "Hold Co", "HLD", 15, 15, 100m, 500_000_000m),
            MakeScore(2, "222", "Weak Co", "WEA", 12, 15, 10m, 100_000_000m, 3m),
        ], _ct);

        var service = new PortfolioAdvisorService(_dbm);
        Result<PortfolioAdvisorReport> result = await service.GetRecommendations(["WEA"], _ct);
        Assert.True(result.IsSuccess);

        Assert.Null(result.Value!.BaselineSnapshotDate);
        PortfolioRecommendation sell = Assert.Single(result.Value!.Sells);
        Assert.Null(sell.Trigger);
        // Without a baseline, reasons fall back to the currently failing checks
        Assert.Contains(sell.Reasons, r => r.Contains("FAILS"));
    }
}
