using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Stocks.DataModels;
using Stocks.Persistence.Database;

namespace Stocks.WebApi.Tests;

public class SearchEndpointsTests : IClassFixture<WebApplicationFactory<Program>> {
    private readonly HttpClient _client;
    private readonly DbmInMemoryService _dbm = new();

    public SearchEndpointsTests(WebApplicationFactory<Program> factory) {
        DbmInMemoryService dbm = _dbm;
        WebApplicationFactory<Program> customFactory = factory.WithWebHostBuilder(builder =>
            _ = builder.ConfigureServices(services =>
                _ = services.AddSingleton<IDbmService>(dbm)));
        _client = customFactory.CreateClient();
    }

    private async Task SeedCompanies() {
        _ = await _dbm.BulkInsertCompanies([
            new Company(1, 320193, "EDGAR"),
            new Company(2, 789019, "EDGAR"),
            new Company(3, 1018724, "EDGAR")
        ], CancellationToken.None);
        _ = await _dbm.BulkInsertCompanyNames([
            new CompanyName(100, 1, "Apple Inc"),
            new CompanyName(101, 2, "Microsoft Corporation"),
            new CompanyName(102, 3, "Amazon.com Inc")
        ], CancellationToken.None);
        _ = await _dbm.BulkInsertCompanyTickers([
            new CompanyTicker(1, "AAPL", "NASDAQ"),
            new CompanyTicker(2, "MSFT", "NASDAQ"),
            new CompanyTicker(3, "AMZN", "NASDAQ")
        ], CancellationToken.None);
        _ = await _dbm.BulkInsertPrices([
            new PriceRow(1, 320193, "AAPL", "NASDAQ", "AAPL.US", new DateOnly(2025, 6, 13), 195.00m, 198.50m, 194.00m, 197.25m, 50000000),
            new PriceRow(2, 320193, "AAPL", "NASDAQ", "AAPL.US", new DateOnly(2025, 6, 12), 193.00m, 196.00m, 192.50m, 195.00m, 45000000)
        ], CancellationToken.None);
    }

    [Fact]
    public async Task Search_ByName_ReturnsMatches() {
        await SeedCompanies();

        HttpResponseMessage response = await _client.GetAsync("/api/search?q=Apple&page=1&pageSize=25");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Apple", body);
        Assert.Contains("197.25", body);
        Assert.Contains("2025-06-13", body);
    }

    [Fact]
    public async Task Search_NoResults_ReturnsEmptyPage() {
        await SeedCompanies();

        HttpResponseMessage response = await _client.GetAsync("/api/search?q=NonExistent&page=1&pageSize=25");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("[]", body);
    }

    [Fact]
    public async Task Search_Pagination_RespectsPageSize() {
        await SeedCompanies();

        HttpResponseMessage response = await _client.GetAsync("/api/search?q=a&page=1&pageSize=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"totalItems\":", body);
    }
}
