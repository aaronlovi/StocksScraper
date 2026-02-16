using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Stocks.DataModels;
using Stocks.Persistence.Database;
using Stocks.Shared;

namespace Stocks.EDGARScraper.Tests;

public class CompanySearchTests {
    private readonly DbmInMemoryService _dbm = new();
    private readonly CancellationToken _ct = CancellationToken.None;

    private async Task SeedCompanies() {
        _ = await _dbm.BulkInsertCompanies([
            new Company(1, 320193, "EDGAR"),
            new Company(2, 789019, "EDGAR"),
            new Company(3, 1018724, "EDGAR")
        ], _ct);

        _ = await _dbm.BulkInsertCompanyNames([
            new CompanyName(100, 1, "Apple Inc"),
            new CompanyName(101, 2, "Microsoft Corporation"),
            new CompanyName(102, 3, "Amazon.com Inc")
        ], _ct);

        _ = await _dbm.BulkInsertCompanyTickers([
            new CompanyTicker(1, "AAPL", "NASDAQ"),
            new CompanyTicker(2, "MSFT", "NASDAQ"),
            new CompanyTicker(3, "AMZN", "NASDAQ")
        ], _ct);
    }

    [Fact]
    public async Task SearchCompanies_ByName_ReturnsMatchingCompanies() {
        await SeedCompanies();

        Result<PagedResults<CompanySearchResult>> result =
            await _dbm.SearchCompanies("Apple", new PaginationRequest(1, 25), _ct);
        Assert.True(result.IsSuccess);

        CompanySearchResult match = Assert.Single(result.Value!.Items);
        Assert.Equal("Apple Inc", match.CompanyName);
        Assert.Equal("320193", match.Cik);
    }

    [Fact]
    public async Task SearchCompanies_ByTicker_ReturnsMatchingCompanies() {
        await SeedCompanies();

        Result<PagedResults<CompanySearchResult>> result =
            await _dbm.SearchCompanies("MSFT", new PaginationRequest(1, 25), _ct);
        Assert.True(result.IsSuccess);

        CompanySearchResult match = Assert.Single(result.Value!.Items);
        Assert.Equal("Microsoft Corporation", match.CompanyName);
        Assert.Equal("MSFT", match.Ticker);
    }

    [Fact]
    public async Task SearchCompanies_ByCik_ReturnsExactMatch() {
        await SeedCompanies();

        Result<PagedResults<CompanySearchResult>> result =
            await _dbm.SearchCompanies("1018724", new PaginationRequest(1, 25), _ct);
        Assert.True(result.IsSuccess);

        CompanySearchResult match = Assert.Single(result.Value!.Items);
        Assert.Equal("Amazon.com Inc", match.CompanyName);
    }

    [Fact]
    public async Task SearchCompanies_NoMatch_ReturnsEmptyPage() {
        await SeedCompanies();

        Result<PagedResults<CompanySearchResult>> result =
            await _dbm.SearchCompanies("NonExistentCompany", new PaginationRequest(1, 25), _ct);
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.Items);
        Assert.Equal(0U, result.Value.Pagination.TotalItems);
    }

    [Fact]
    public async Task SearchCompanies_Pagination_RespectsPageSize() {
        _ = await _dbm.BulkInsertCompanies([
            new Company(1, 100, "EDGAR"),
            new Company(2, 200, "EDGAR"),
            new Company(3, 300, "EDGAR"),
            new Company(4, 400, "EDGAR"),
            new Company(5, 500, "EDGAR")
        ], _ct);

        _ = await _dbm.BulkInsertCompanyNames([
            new CompanyName(10, 1, "Test Corp Alpha"),
            new CompanyName(11, 2, "Test Corp Beta"),
            new CompanyName(12, 3, "Test Corp Gamma"),
            new CompanyName(13, 4, "Test Corp Delta"),
            new CompanyName(14, 5, "Test Corp Epsilon")
        ], _ct);

        Result<PagedResults<CompanySearchResult>> result =
            await _dbm.SearchCompanies("Test Corp", new PaginationRequest(1, 3), _ct);
        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value!.Items.Count);
        Assert.Equal(5U, result.Value.Pagination.TotalItems);
        Assert.Equal(2U, result.Value.Pagination.TotalPages);
    }
}
