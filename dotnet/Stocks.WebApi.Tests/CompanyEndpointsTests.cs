using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Stocks.DataModels;
using Stocks.Persistence.Database;

namespace Stocks.WebApi.Tests;

public class CompanyEndpointsTests : IClassFixture<WebApplicationFactory<Program>> {
    private readonly HttpClient _client;
    private readonly DbmInMemoryService _dbm = new();

    public CompanyEndpointsTests(WebApplicationFactory<Program> factory) {
        DbmInMemoryService dbm = _dbm;
        WebApplicationFactory<Program> customFactory = factory.WithWebHostBuilder(builder =>
            _ = builder.ConfigureServices(services =>
                _ = services.AddSingleton<IDbmService>(dbm)));
        _client = customFactory.CreateClient();
    }

    private async Task SeedCompany() {
        _ = await _dbm.BulkInsertCompanies([new Company(1, 320193, "EDGAR")], CancellationToken.None);
        _ = await _dbm.BulkInsertCompanyNames([new CompanyName(100, 1, "Apple Inc")], CancellationToken.None);
        _ = await _dbm.BulkInsertCompanyTickers([new CompanyTicker(1, "AAPL", "NASDAQ")], CancellationToken.None);
        _ = await _dbm.BulkInsertPrices(new List<PriceRow> {
            new PriceRow(1, 320193, "AAPL", "NASDAQ", "AAPL.US",
                new DateOnly(2025, 6, 13), 190.0m, 192.0m, 189.0m, 191.50m, 50000000),
            new PriceRow(2, 320193, "AAPL", "NASDAQ", "AAPL.US",
                new DateOnly(2025, 6, 12), 188.0m, 191.0m, 187.0m, 190.0m, 45000000),
        }, CancellationToken.None);
    }

    [Fact]
    public async Task GetCompanyByCik_Found_Returns200WithCompanyData() {
        await SeedCompany();

        HttpResponseMessage response = await _client.GetAsync("/api/companies/320193");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("320193", body);
        Assert.Contains("AAPL", body);
        Assert.Contains("Apple Inc", body);
        Assert.Contains("191.5", body);
        Assert.Contains("2025-06-13", body);
    }

    [Fact]
    public async Task GetCompanyByCik_NotFound_Returns404() {
        HttpResponseMessage response = await _client.GetAsync("/api/companies/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
