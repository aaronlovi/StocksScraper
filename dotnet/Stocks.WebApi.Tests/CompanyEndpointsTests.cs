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
    }

    [Fact]
    public async Task GetCompanyByCik_Found_Returns200WithCompanyData() {
        await SeedCompany();

        HttpResponseMessage response = await _client.GetAsync("/api/companies/320193");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("320193", body);
        Assert.Contains("AAPL", body);
    }

    [Fact]
    public async Task GetCompanyByCik_NotFound_Returns404() {
        HttpResponseMessage response = await _client.GetAsync("/api/companies/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
