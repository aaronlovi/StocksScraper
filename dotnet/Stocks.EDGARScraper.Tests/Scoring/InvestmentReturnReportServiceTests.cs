using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Stocks.DataModels;
using Stocks.DataModels.Scoring;
using Stocks.Persistence.Database;
using Stocks.Persistence.Services;
using Stocks.Shared;

namespace Stocks.EDGARScraper.Tests.Scoring;

public class InvestmentReturnReportServiceTests {
    private readonly DbmInMemoryService _dbm = new();
    private readonly InvestmentReturnReportService _service;
    private readonly CancellationToken _ct = CancellationToken.None;

    private static readonly DateTime Now = DateTime.UtcNow;
    private static readonly DateOnly StartDate = new(2024, 1, 2);
    private static readonly DateOnly EndDate = new(2025, 1, 2);

    public InvestmentReturnReportServiceTests() {
        _service = new InvestmentReturnReportService(_dbm);
    }

    private async Task SeedGrahamScores(List<CompanyScoreSummary> scores) {
        await _dbm.BulkInsertCompanyScores(scores, _ct);
    }

    private async Task SeedMoatScores(List<CompanyMoatScoreSummary> scores) {
        await _dbm.BulkInsertCompanyMoatScores(scores, _ct);
    }

    private async Task SeedPrices(List<PriceRow> prices) {
        await _dbm.BulkInsertPrices(prices, _ct);
    }

    private static CompanyScoreSummary MakeGrahamScore(ulong companyId, string cik, string ticker, string exchange, int score) {
        return new CompanyScoreSummary(
            companyId, cik, ticker + " Inc", ticker, exchange,
            score, 13, 5,
            100m, 1000m, 0.5m, 2.0m, 0.3m,
            50m, 25m, 20m, 10m, 9m, 8m, 7m,
            100m, new DateOnly(2024, 12, 19), 1_000_000, null, null, null, Now);
    }

    private static CompanyMoatScoreSummary MakeMoatScore(ulong companyId, string cik, string ticker, string exchange, int score) {
        return new CompanyMoatScoreSummary(
            companyId, cik, ticker + " Inc", ticker, exchange,
            score, 10, 5,
            0.4m, 0.2m, 10m, 9m, 8m, 5m, 0.1m, 20m, 0.5m,
            100m, new DateOnly(2024, 12, 19), 1_000_000, Now);
    }

    private static PriceRow MakePrice(string ticker, DateOnly date, decimal close) {
        return new PriceRow(0, 0, ticker, "NASDAQ", ticker + ".US", date, close, close, close, close, 100_000);
    }

    // Positive return computation

    [Fact]
    public async Task GetGrahamReturns_PositiveReturn_ComputesCorrectly() {
        await SeedGrahamScores(new List<CompanyScoreSummary> {
            MakeGrahamScore(1, "100", "AAPL", "NASDAQ", 10)
        });
        await SeedPrices(new List<PriceRow> {
            MakePrice("AAPL", StartDate, 100m),
            MakePrice("AAPL", EndDate, 150m)
        });

        Result<PagedResults<CompanyScoreReturnSummary>> result =
            await _service.GetGrahamReturns(StartDate, new PaginationRequest(1, 50), ReturnsReportSortBy.OverallScore, SortDirection.Descending, null, _ct);

        Assert.True(result.IsSuccess);
        CompanyScoreReturnSummary row = Assert.Single(result.Value!.Items);
        Assert.Equal(50.00m, row.TotalReturnPct);
        Assert.Equal(1500.00m, row.CurrentValueOf1000);
        Assert.NotNull(row.AnnualizedReturnPct);
        Assert.Equal(StartDate, row.StartDate);
        Assert.Equal(EndDate, row.EndDate);
        Assert.Equal(100m, row.StartPrice);
        Assert.Equal(150m, row.EndPrice);
    }

    // Negative return computation

    [Fact]
    public async Task GetGrahamReturns_NegativeReturn_ComputesCorrectly() {
        await SeedGrahamScores(new List<CompanyScoreSummary> {
            MakeGrahamScore(1, "100", "AAPL", "NASDAQ", 10)
        });
        await SeedPrices(new List<PriceRow> {
            MakePrice("AAPL", StartDate, 200m),
            MakePrice("AAPL", EndDate, 150m)
        });

        Result<PagedResults<CompanyScoreReturnSummary>> result =
            await _service.GetGrahamReturns(StartDate, new PaginationRequest(1, 50), ReturnsReportSortBy.OverallScore, SortDirection.Descending, null, _ct);

        Assert.True(result.IsSuccess);
        var items = new List<CompanyScoreReturnSummary>(result.Value!.Items);
        CompanyScoreReturnSummary row = items[0];
        Assert.Equal(-25.00m, row.TotalReturnPct);
        Assert.Equal(750.00m, row.CurrentValueOf1000);
        Assert.NotNull(row.AnnualizedReturnPct);
    }

    // Missing start price

    [Fact]
    public async Task GetGrahamReturns_MissingStartPrice_ReturnsNullReturnFields() {
        await SeedGrahamScores(new List<CompanyScoreSummary> {
            MakeGrahamScore(1, "100", "AAPL", "NASDAQ", 10)
        });
        // Only seed end price, no start price on or before StartDate
        await SeedPrices(new List<PriceRow> {
            MakePrice("AAPL", EndDate, 150m)
        });

        Result<PagedResults<CompanyScoreReturnSummary>> result =
            await _service.GetGrahamReturns(StartDate, new PaginationRequest(1, 50), ReturnsReportSortBy.OverallScore, SortDirection.Descending, null, _ct);

        Assert.True(result.IsSuccess);
        var items = new List<CompanyScoreReturnSummary>(result.Value!.Items);
        CompanyScoreReturnSummary row = items[0];
        Assert.Null(row.TotalReturnPct);
        Assert.Null(row.AnnualizedReturnPct);
        Assert.Null(row.CurrentValueOf1000);
    }

    // No prices at all for ticker

    [Fact]
    public async Task GetGrahamReturns_NoPricesForTicker_ReturnsNullReturnFields() {
        await SeedGrahamScores(new List<CompanyScoreSummary> {
            MakeGrahamScore(1, "100", "AAPL", "NASDAQ", 10)
        });
        // No AAPL prices seeded at all — only a different ticker
        await SeedPrices(new List<PriceRow> {
            MakePrice("OTHER", StartDate, 100m),
            MakePrice("OTHER", EndDate, 200m)
        });

        Result<PagedResults<CompanyScoreReturnSummary>> result =
            await _service.GetGrahamReturns(StartDate, new PaginationRequest(1, 50), ReturnsReportSortBy.OverallScore, SortDirection.Descending, null, _ct);

        Assert.True(result.IsSuccess);
        var items = new List<CompanyScoreReturnSummary>(result.Value!.Items);
        CompanyScoreReturnSummary row = items[0];
        Assert.Null(row.TotalReturnPct);
        Assert.Null(row.AnnualizedReturnPct);
        Assert.Null(row.CurrentValueOf1000);
    }

    // Zero start price

    [Fact]
    public async Task GetGrahamReturns_ZeroStartPrice_ReturnsNullReturnFields() {
        await SeedGrahamScores(new List<CompanyScoreSummary> {
            MakeGrahamScore(1, "100", "AAPL", "NASDAQ", 10)
        });
        await SeedPrices(new List<PriceRow> {
            MakePrice("AAPL", StartDate, 0m),
            MakePrice("AAPL", EndDate, 150m)
        });

        Result<PagedResults<CompanyScoreReturnSummary>> result =
            await _service.GetGrahamReturns(StartDate, new PaginationRequest(1, 50), ReturnsReportSortBy.OverallScore, SortDirection.Descending, null, _ct);

        Assert.True(result.IsSuccess);
        var items = new List<CompanyScoreReturnSummary>(result.Value!.Items);
        CompanyScoreReturnSummary row = items[0];
        Assert.Null(row.TotalReturnPct);
        Assert.Null(row.AnnualizedReturnPct);
        Assert.Null(row.CurrentValueOf1000);
    }

    // Same-day prices

    [Fact]
    public async Task GetGrahamReturns_SameDay_ZeroReturnNoAnnualized() {
        await SeedGrahamScores(new List<CompanyScoreSummary> {
            MakeGrahamScore(1, "100", "AAPL", "NASDAQ", 10)
        });
        // Only one price on start date — latest price will also resolve to this same row
        await SeedPrices(new List<PriceRow> {
            MakePrice("AAPL", StartDate, 100m)
        });

        Result<PagedResults<CompanyScoreReturnSummary>> result =
            await _service.GetGrahamReturns(StartDate, new PaginationRequest(1, 50), ReturnsReportSortBy.OverallScore, SortDirection.Descending, null, _ct);

        Assert.True(result.IsSuccess);
        var items = new List<CompanyScoreReturnSummary>(result.Value!.Items);
        CompanyScoreReturnSummary row = items[0];
        Assert.Equal(0.00m, row.TotalReturnPct);
        Assert.Null(row.AnnualizedReturnPct);
        Assert.Equal(1000.00m, row.CurrentValueOf1000);
    }

    // Sorting by TotalReturnPct descending, nulls last

    [Fact]
    public async Task GetGrahamReturns_SortByTotalReturnPctDescending_NullsLast() {
        await SeedGrahamScores(new List<CompanyScoreSummary> {
            MakeGrahamScore(1, "100", "AAPL", "NASDAQ", 10),
            MakeGrahamScore(2, "200", "MSFT", "NASDAQ", 8),
            MakeGrahamScore(3, "300", "GOOG", "NASDAQ", 12)
        });
        await SeedPrices(new List<PriceRow> {
            MakePrice("AAPL", StartDate, 100m),
            MakePrice("AAPL", EndDate, 120m),   // 20%
            MakePrice("MSFT", StartDate, 100m),
            MakePrice("MSFT", EndDate, 160m),   // 60%
            // GOOG has no prices → null returns
        });

        Result<PagedResults<CompanyScoreReturnSummary>> result =
            await _service.GetGrahamReturns(StartDate, new PaginationRequest(1, 50), ReturnsReportSortBy.TotalReturnPct, SortDirection.Descending, null, _ct);

        Assert.True(result.IsSuccess);
        var items = new List<CompanyScoreReturnSummary>(result.Value!.Items);
        Assert.Equal(3, items.Count);
        Assert.Equal("MSFT", items[0].Ticker);  // 60%
        Assert.Equal("AAPL", items[1].Ticker);  // 20%
        Assert.Equal("GOOG", items[2].Ticker);  // null (last)
    }

    // Sorting by OverallScore

    [Fact]
    public async Task GetGrahamReturns_SortByOverallScoreDescending() {
        await SeedGrahamScores(new List<CompanyScoreSummary> {
            MakeGrahamScore(1, "100", "AAPL", "NASDAQ", 10),
            MakeGrahamScore(2, "200", "MSFT", "NASDAQ", 8),
            MakeGrahamScore(3, "300", "GOOG", "NASDAQ", 12)
        });
        await SeedPrices(new List<PriceRow> {
            MakePrice("AAPL", StartDate, 100m),
            MakePrice("AAPL", EndDate, 120m),
            MakePrice("MSFT", StartDate, 100m),
            MakePrice("MSFT", EndDate, 160m),
            MakePrice("GOOG", StartDate, 100m),
            MakePrice("GOOG", EndDate, 110m),
        });

        Result<PagedResults<CompanyScoreReturnSummary>> result =
            await _service.GetGrahamReturns(StartDate, new PaginationRequest(1, 50), ReturnsReportSortBy.OverallScore, SortDirection.Descending, null, _ct);

        Assert.True(result.IsSuccess);
        var items = new List<CompanyScoreReturnSummary>(result.Value!.Items);
        Assert.Equal(3, items.Count);
        Assert.Equal("GOOG", items[0].Ticker);  // 12
        Assert.Equal("AAPL", items[1].Ticker);  // 10
        Assert.Equal("MSFT", items[2].Ticker);  // 8
    }

    // Pagination

    [Fact]
    public async Task GetGrahamReturns_Pagination_Page1() {
        await SeedGrahamScores(new List<CompanyScoreSummary> {
            MakeGrahamScore(1, "100", "AAPL", "NASDAQ", 10),
            MakeGrahamScore(2, "200", "MSFT", "NASDAQ", 8),
            MakeGrahamScore(3, "300", "GOOG", "NASDAQ", 12)
        });

        Result<PagedResults<CompanyScoreReturnSummary>> result =
            await _service.GetGrahamReturns(StartDate, new PaginationRequest(1, 2), ReturnsReportSortBy.OverallScore, SortDirection.Descending, null, _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Items.Count);
        Assert.Equal(3u, result.Value.Pagination.TotalItems);
        Assert.Equal(2u, result.Value.Pagination.TotalPages);
    }

    [Fact]
    public async Task GetGrahamReturns_Pagination_Page2() {
        await SeedGrahamScores(new List<CompanyScoreSummary> {
            MakeGrahamScore(1, "100", "AAPL", "NASDAQ", 10),
            MakeGrahamScore(2, "200", "MSFT", "NASDAQ", 8),
            MakeGrahamScore(3, "300", "GOOG", "NASDAQ", 12)
        });

        Result<PagedResults<CompanyScoreReturnSummary>> result =
            await _service.GetGrahamReturns(StartDate, new PaginationRequest(2, 2), ReturnsReportSortBy.OverallScore, SortDirection.Descending, null, _ct);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!.Items);
    }

    // Filter by minScore

    [Fact]
    public async Task GetGrahamReturns_FilterByMinScore() {
        await SeedGrahamScores(new List<CompanyScoreSummary> {
            MakeGrahamScore(1, "100", "AAPL", "NASDAQ", 10),
            MakeGrahamScore(2, "200", "MSFT", "NASDAQ", 8),
            MakeGrahamScore(3, "300", "GOOG", "NASDAQ", 12)
        });

        var filter = new ReturnsReportFilter(10, null, null);
        Result<PagedResults<CompanyScoreReturnSummary>> result =
            await _service.GetGrahamReturns(StartDate, new PaginationRequest(1, 50), ReturnsReportSortBy.OverallScore, SortDirection.Descending, filter, _ct);

        Assert.True(result.IsSuccess);
        var items = new List<CompanyScoreReturnSummary>(result.Value!.Items);
        Assert.Equal(2, items.Count);
        // All items should have score >= 10
        Assert.True(items[0].OverallScore >= 10);
        Assert.True(items[1].OverallScore >= 10);
    }

    // Filter by exchange

    [Fact]
    public async Task GetGrahamReturns_FilterByExchange() {
        await SeedGrahamScores(new List<CompanyScoreSummary> {
            MakeGrahamScore(1, "100", "AAPL", "NASDAQ", 10),
            MakeGrahamScore(2, "200", "XOM", "NYSE", 8),
            MakeGrahamScore(3, "300", "GOOG", "NASDAQ", 12)
        });

        var filter = new ReturnsReportFilter(null, null, "NYSE");
        Result<PagedResults<CompanyScoreReturnSummary>> result =
            await _service.GetGrahamReturns(StartDate, new PaginationRequest(1, 50), ReturnsReportSortBy.OverallScore, SortDirection.Descending, filter, _ct);

        Assert.True(result.IsSuccess);
        CompanyScoreReturnSummary single = Assert.Single(result.Value!.Items);
        Assert.Equal("XOM", single.Ticker);
    }

    // Buffett returns (moat scores)

    [Fact]
    public async Task GetBuffettReturns_PositiveReturn_ComputesCorrectly() {
        await SeedMoatScores(new List<CompanyMoatScoreSummary> {
            MakeMoatScore(1, "100", "AAPL", "NASDAQ", 8)
        });
        await SeedPrices(new List<PriceRow> {
            MakePrice("AAPL", StartDate, 100m),
            MakePrice("AAPL", EndDate, 200m)
        });

        Result<PagedResults<CompanyScoreReturnSummary>> result =
            await _service.GetBuffettReturns(StartDate, new PaginationRequest(1, 50), ReturnsReportSortBy.OverallScore, SortDirection.Descending, null, _ct);

        Assert.True(result.IsSuccess);
        var items = new List<CompanyScoreReturnSummary>(result.Value!.Items);
        Assert.Single(items);
        CompanyScoreReturnSummary row = items[0];
        Assert.Equal(100.00m, row.TotalReturnPct);
        Assert.Equal(2000.00m, row.CurrentValueOf1000);
        Assert.NotNull(row.AnnualizedReturnPct);
    }

    // Buffett filter by minScore

    [Fact]
    public async Task GetBuffettReturns_FilterByMinScore() {
        await SeedMoatScores(new List<CompanyMoatScoreSummary> {
            MakeMoatScore(1, "100", "AAPL", "NASDAQ", 8),
            MakeMoatScore(2, "200", "MSFT", "NASDAQ", 5),
            MakeMoatScore(3, "300", "GOOG", "NASDAQ", 9)
        });

        var filter = new ReturnsReportFilter(8, null, null);
        Result<PagedResults<CompanyScoreReturnSummary>> result =
            await _service.GetBuffettReturns(StartDate, new PaginationRequest(1, 50), ReturnsReportSortBy.OverallScore, SortDirection.Descending, filter, _ct);

        Assert.True(result.IsSuccess);
        var items = new List<CompanyScoreReturnSummary>(result.Value!.Items);
        Assert.Equal(2, items.Count);
        Assert.True(items[0].OverallScore >= 8);
        Assert.True(items[1].OverallScore >= 8);
    }

    // Annualized return computation accuracy

    [Fact]
    public async Task GetGrahamReturns_AnnualizedReturn_ComputedAccurately() {
        await SeedGrahamScores(new List<CompanyScoreSummary> {
            MakeGrahamScore(1, "100", "AAPL", "NASDAQ", 10)
        });
        // 366 days (2024 is leap year), 50% total return → annualized ≈ 50%
        var start = new DateOnly(2024, 1, 1);
        var end = new DateOnly(2025, 1, 1);
        await SeedPrices(new List<PriceRow> {
            MakePrice("AAPL", start, 100m),
            MakePrice("AAPL", end, 150m)
        });

        Result<PagedResults<CompanyScoreReturnSummary>> result =
            await _service.GetGrahamReturns(start, new PaginationRequest(1, 50), ReturnsReportSortBy.OverallScore, SortDirection.Descending, null, _ct);

        Assert.True(result.IsSuccess);
        var items = new List<CompanyScoreReturnSummary>(result.Value!.Items);
        CompanyScoreReturnSummary row = items[0];
        Assert.Equal(50.00m, row.TotalReturnPct);
        // Annualized should be close to 50% for ~1 year holding period
        Assert.NotNull(row.AnnualizedReturnPct);
        Assert.InRange(row.AnnualizedReturnPct!.Value, 49m, 51m);
    }
}
