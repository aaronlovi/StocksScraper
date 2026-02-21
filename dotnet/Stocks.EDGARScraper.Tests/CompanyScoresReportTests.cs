using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Stocks.DataModels;
using Stocks.DataModels.Scoring;
using Stocks.Persistence.Database;
using Stocks.Shared;

namespace Stocks.EDGARScraper.Tests;

public class CompanyScoresReportTests {
    private readonly DbmInMemoryService _dbm = new();
    private readonly CancellationToken _ct = CancellationToken.None;

    private static readonly DateTime Now = DateTime.UtcNow;

    private async Task SeedScores() {
        var scores = new List<CompanyScoreSummary> {
            new(1, "320193", "Apple Inc", "AAPL", "NASDAQ", 11, 13, 5,
                500_000_000m, 3_000_000_000_000m, 0.3m, 5.0m, 0.4m,
                200_000_000m, 100_000_000m, 90_000_000m, 8.5m, 7.2m,
                250m, new DateOnly(2024, 12, 19), 100_000_000, null, null, null, Now),
            new(2, "789019", "Microsoft Corporation", "MSFT", "NASDAQ", 9, 13, 5,
                400_000_000m, 2_500_000_000_000m, 0.4m, 6.0m, 0.5m,
                180_000_000m, 90_000_000m, 85_000_000m, 7.0m, 6.5m,
                430m, new DateOnly(2024, 12, 19), 80_000_000, null, null, null, Now),
            new(3, "1018724", "Amazon.com Inc", "AMZN", "NASDAQ", 7, 13, 5,
                300_000_000m, 1_800_000_000_000m, 0.6m, 4.0m, 0.7m,
                150_000_000m, 80_000_000m, 75_000_000m, 6.0m, 5.5m,
                190m, new DateOnly(2024, 12, 19), 90_000_000, null, null, null, Now),
            new(4, "1326801", "Meta Platforms Inc", "META", "NASDAQ", 10, 13, 5,
                350_000_000m, 1_200_000_000_000m, 0.2m, 3.5m, 0.3m,
                160_000_000m, 95_000_000m, 88_000_000m, 9.0m, 8.0m,
                550m, new DateOnly(2024, 12, 19), 50_000_000, null, null, null, Now),
            new(5, "34088", "Exxon Mobil Corporation", "XOM", "NYSE", 12, 13, 5,
                600_000_000m, 500_000_000_000m, 0.1m, 2.0m, 0.2m,
                250_000_000m, 120_000_000m, 110_000_000m, 12.0m, 11.0m,
                110m, new DateOnly(2024, 12, 19), 200_000_000, null, null, null, Now),
        };
        await _dbm.BulkInsertCompanyScores(scores, _ct);
    }

    [Fact]
    public async Task GetCompanyScores_ReturnsPagedResults() {
        await SeedScores();

        Result<PagedResults<CompanyScoreSummary>> result =
            await _dbm.GetCompanyScores(new PaginationRequest(1, 3),
                ScoresSortBy.OverallScore, SortDirection.Descending, null, _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value!.Items.Count);
        Assert.Equal(5u, result.Value.Pagination.TotalItems);
        Assert.Equal(2u, result.Value.Pagination.TotalPages);
    }

    [Fact]
    public async Task GetCompanyScores_Page2_ReturnsRemainingItems() {
        await SeedScores();

        Result<PagedResults<CompanyScoreSummary>> result =
            await _dbm.GetCompanyScores(new PaginationRequest(2, 3),
                ScoresSortBy.OverallScore, SortDirection.Descending, null, _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Items.Count);
    }

    [Fact]
    public async Task GetCompanyScores_SortByScoreDescending_TopScoresFirst() {
        await SeedScores();

        Result<PagedResults<CompanyScoreSummary>> result =
            await _dbm.GetCompanyScores(new PaginationRequest(1, 5),
                ScoresSortBy.OverallScore, SortDirection.Descending, null, _ct);

        Assert.True(result.IsSuccess);
        var items = new List<CompanyScoreSummary>(result.Value!.Items);
        Assert.Equal(12, items[0].OverallScore); // XOM
        Assert.Equal(11, items[1].OverallScore); // AAPL
    }

    [Fact]
    public async Task GetCompanyScores_SortByScoreAscending_BottomScoresFirst() {
        await SeedScores();

        Result<PagedResults<CompanyScoreSummary>> result =
            await _dbm.GetCompanyScores(new PaginationRequest(1, 5),
                ScoresSortBy.OverallScore, SortDirection.Ascending, null, _ct);

        Assert.True(result.IsSuccess);
        var items = new List<CompanyScoreSummary>(result.Value!.Items);
        Assert.Equal(7, items[0].OverallScore); // AMZN
        Assert.Equal(9, items[1].OverallScore); // MSFT
    }

    [Fact]
    public async Task GetCompanyScores_SortByEstimatedReturnCF() {
        await SeedScores();

        Result<PagedResults<CompanyScoreSummary>> result =
            await _dbm.GetCompanyScores(new PaginationRequest(1, 5),
                ScoresSortBy.EstimatedReturnCF, SortDirection.Descending, null, _ct);

        Assert.True(result.IsSuccess);
        var items = new List<CompanyScoreSummary>(result.Value!.Items);
        Assert.Equal(12.0m, items[0].EstimatedReturnCF); // XOM highest CF return
    }

    [Fact]
    public async Task GetCompanyScores_FilterByMinScore() {
        await SeedScores();

        var filter = new ScoresFilter(10, null, null);
        Result<PagedResults<CompanyScoreSummary>> result =
            await _dbm.GetCompanyScores(new PaginationRequest(1, 10),
                ScoresSortBy.OverallScore, SortDirection.Descending, filter, _ct);

        Assert.True(result.IsSuccess);
        // Scores 10+: XOM(12), AAPL(11), META(10)
        Assert.Equal(3, result.Value!.Items.Count);
        foreach (CompanyScoreSummary s in result.Value.Items)
            Assert.True(s.OverallScore >= 10);
    }

    [Fact]
    public async Task GetCompanyScores_FilterByExchange() {
        await SeedScores();

        var filter = new ScoresFilter(null, null, "NYSE");
        Result<PagedResults<CompanyScoreSummary>> result =
            await _dbm.GetCompanyScores(new PaginationRequest(1, 10),
                ScoresSortBy.OverallScore, SortDirection.Descending, filter, _ct);

        Assert.True(result.IsSuccess);
        CompanyScoreSummary single = Assert.Single(result.Value!.Items);
        Assert.Equal("XOM", single.Ticker);
    }

    [Fact]
    public async Task GetCompanyScores_CombinedFilterAndSort() {
        await SeedScores();

        var filter = new ScoresFilter(9, null, "NASDAQ");
        Result<PagedResults<CompanyScoreSummary>> result =
            await _dbm.GetCompanyScores(new PaginationRequest(1, 10),
                ScoresSortBy.OverallScore, SortDirection.Ascending, filter, _ct);

        Assert.True(result.IsSuccess);
        // NASDAQ with score >= 9: MSFT(9), META(10), AAPL(11)
        var items = new List<CompanyScoreSummary>(result.Value!.Items);
        Assert.Equal(3, items.Count);
        Assert.Equal(9, items[0].OverallScore);  // MSFT ascending
        Assert.Equal(10, items[1].OverallScore); // META
        Assert.Equal(11, items[2].OverallScore); // AAPL
    }

    [Fact]
    public async Task GetCompanyScores_FilterMatchesNothing_ReturnsEmpty() {
        await SeedScores();

        var filter = new ScoresFilter(13, null, null);
        Result<PagedResults<CompanyScoreSummary>> result =
            await _dbm.GetCompanyScores(new PaginationRequest(1, 10),
                ScoresSortBy.OverallScore, SortDirection.Descending, filter, _ct);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.Items);
        Assert.Equal(0u, result.Value.Pagination.TotalItems);
    }

    [Fact]
    public async Task GetCompanyScores_EmptyTable_ReturnsEmpty() {
        Result<PagedResults<CompanyScoreSummary>> result =
            await _dbm.GetCompanyScores(new PaginationRequest(1, 10),
                ScoresSortBy.OverallScore, SortDirection.Descending, null, _ct);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.Items);
    }
}
