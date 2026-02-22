using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Stocks.DataModels;
using Stocks.DataModels.Scoring;
using Stocks.Persistence.Database;
using Stocks.Shared;

namespace Stocks.EDGARScraper.Tests.Scoring;

public class Return1yEnrichmentTests {
    private readonly DbmInMemoryService _dbm = new();
    private readonly CancellationToken _ct = CancellationToken.None;

    private static PriceRow MakePrice(ulong priceId, ulong cik, string ticker, DateOnly date, decimal close) {
        return new PriceRow(priceId, cik, ticker, "NYSE", ticker.ToLowerInvariant() + ".us",
            date, close - 1m, close + 1m, close - 2m, close, 1_000_000);
    }

    [Fact]
    public async Task GetAllPricesNearDate_ReturnsClosestPricePerTicker() {
        await _dbm.BulkInsertPrices([
            MakePrice(1, 320193, "AAPL", new DateOnly(2024, 1, 10), 180m),
            MakePrice(2, 320193, "AAPL", new DateOnly(2024, 1, 14), 182m),
            MakePrice(3, 320193, "AAPL", new DateOnly(2024, 1, 20), 185m),
            MakePrice(4, 789019, "MSFT", new DateOnly(2024, 1, 12), 400m),
            MakePrice(5, 789019, "MSFT", new DateOnly(2024, 1, 18), 410m),
        ], _ct);

        Result<IReadOnlyCollection<LatestPrice>> result =
            await _dbm.GetAllPricesNearDate(new DateOnly(2024, 1, 15), _ct);

        Assert.True(result.IsSuccess);
        var prices = new Dictionary<string, LatestPrice>(StringComparer.OrdinalIgnoreCase);
        foreach (LatestPrice lp in result.Value!)
            prices[lp.Ticker] = lp;

        Assert.Equal(2, prices.Count);
        Assert.Equal(182m, prices["AAPL"].Close);
        Assert.Equal(new DateOnly(2024, 1, 14), prices["AAPL"].PriceDate);
        Assert.Equal(400m, prices["MSFT"].Close);
        Assert.Equal(new DateOnly(2024, 1, 12), prices["MSFT"].PriceDate);
    }

    [Fact]
    public async Task GetAllPricesNearDate_ExcludesFuturePrices() {
        await _dbm.BulkInsertPrices([
            MakePrice(1, 320193, "AAPL", new DateOnly(2025, 6, 1), 200m),
        ], _ct);

        Result<IReadOnlyCollection<LatestPrice>> result =
            await _dbm.GetAllPricesNearDate(new DateOnly(2024, 1, 15), _ct);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public void CompanyScoreSummary_Return1y_RoundTrip() {
        var now = DateTime.UtcNow;
        var score = new CompanyScoreSummary(
            1, "320193", "Apple Inc", "AAPL", "NASDAQ",
            10, 13, 5, 500m, 3000m, 0.3m, 5.0m, 0.4m,
            200m, 100m, 90m, 12.5m, 11.0m, 8.5m, 7.2m,
            250m, new DateOnly(2024, 12, 19), 100_000_000, null, null, null, null, now);

        // Enrich with Return1y using `with` expression
        var enriched = score with { Return1y = 25.5m };

        Assert.Equal(25.5m, enriched.Return1y);
        Assert.Equal(score.CompanyId, enriched.CompanyId);
        Assert.Equal(score.OverallScore, enriched.OverallScore);
    }

    [Fact]
    public void CompanyMoatScoreSummary_Return1y_RoundTrip() {
        var now = DateTime.UtcNow;
        var score = new CompanyMoatScoreSummary(
            1, "320193", "Apple Inc", "AAPL", "NASDAQ",
            10, 13, 8, 43.0m, 30.0m, 25.0m, 22.0m, 8.0m, 7.5m,
            15.0m, 20.0m, 0.6m, 230m, new DateOnly(2025, 6, 1), 15_000_000_000, null, now);

        var enriched = score with { Return1y = -12.3m };

        Assert.Equal(-12.3m, enriched.Return1y);
        Assert.Equal(score.CompanyId, enriched.CompanyId);
        Assert.Equal(score.OverallScore, enriched.OverallScore);
    }

    [Fact]
    public async Task CompanyScoreSummary_WithReturn1y_StoredAndRetrieved() {
        var now = DateTime.UtcNow;
        var scores = new List<CompanyScoreSummary> {
            new CompanyScoreSummary(1, "320193", "Apple Inc", "AAPL", "NASDAQ",
                10, 13, 5, 500m, 3000m, 0.3m, 5.0m, 0.4m,
                200m, 100m, 90m, 12.5m, 11.0m, 8.5m, 7.2m,
                250m, new DateOnly(2024, 12, 19), 100_000_000, null, null, null, 42.5m, now),
            new CompanyScoreSummary(2, "789019", "Microsoft", "MSFT", "NASDAQ",
                9, 13, 5, 400m, 2500m, 0.4m, 6.0m, 0.5m,
                180m, 90m, 85m, 12.0m, 11.0m, 7.0m, 6.5m,
                430m, new DateOnly(2024, 12, 19), 80_000_000, null, null, null, null, now),
        };
        await _dbm.BulkInsertCompanyScores(scores, _ct);

        var pagination = new PaginationRequest(1, 50);
        Result<PagedResults<CompanyScoreSummary>> result =
            await _dbm.GetCompanyScores(pagination, ScoresSortBy.Return1y, SortDirection.Descending, null, _ct);

        Assert.True(result.IsSuccess);
        var items = new List<CompanyScoreSummary>(result.Value!.Items);
        Assert.Equal(2, items.Count);
        // Sorted by Return1y DESC: Apple (42.5) first, then Microsoft (null)
        Assert.Equal(42.5m, items[0].Return1y);
        Assert.Equal("AAPL", items[0].Ticker);
        Assert.Null(items[1].Return1y);
    }

    [Fact]
    public async Task CompanyMoatScoreSummary_WithReturn1y_StoredAndRetrieved() {
        var now = DateTime.UtcNow;
        var scores = new List<CompanyMoatScoreSummary> {
            new CompanyMoatScoreSummary(1, "320193", "Apple", "AAPL", "NASDAQ",
                10, 13, 8, 43.0m, 30.0m, 25.0m, 22.0m, 8.0m, 7.5m,
                15.0m, 20.0m, 0.6m, 230m, new DateOnly(2025, 6, 1), 15_000_000_000, -5.0m, now),
            new CompanyMoatScoreSummary(2, "789019", "Microsoft", "MSFT", "NASDAQ",
                9, 13, 8, 40.0m, 28.0m, 20.0m, 18.0m, 7.0m, 6.5m,
                12.0m, 18.0m, 0.5m, 430m, new DateOnly(2025, 6, 1), 10_000_000_000, 15.0m, now),
        };
        await _dbm.BulkInsertCompanyMoatScores(scores, _ct);

        var pagination = new PaginationRequest(1, 50);
        Result<PagedResults<CompanyMoatScoreSummary>> result =
            await _dbm.GetCompanyMoatScores(pagination, MoatScoresSortBy.Return1y, SortDirection.Descending, null, _ct);

        Assert.True(result.IsSuccess);
        var items = new List<CompanyMoatScoreSummary>(result.Value!.Items);
        Assert.Equal(2, items.Count);
        // Sorted by Return1y DESC: Microsoft (15.0) first, then Apple (-5.0)
        Assert.Equal(15.0m, items[0].Return1y);
        Assert.Equal("MSFT", items[0].Ticker);
        Assert.Equal(-5.0m, items[1].Return1y);
    }
}
