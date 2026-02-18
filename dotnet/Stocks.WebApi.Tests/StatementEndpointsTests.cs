using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Stocks.DataModels;
using Stocks.DataModels.Enums;
using Stocks.Persistence.Database;
using Stocks.Persistence.Services;

namespace Stocks.WebApi.Tests;

public class StatementEndpointsTests : IClassFixture<WebApplicationFactory<Program>> {
    private readonly HttpClient _client;
    private readonly DbmInMemoryService _dbm = new();

    public StatementEndpointsTests(WebApplicationFactory<Program> factory) {
        DbmInMemoryService dbm = _dbm;
        WebApplicationFactory<Program> customFactory = factory.WithWebHostBuilder(builder =>
            _ = builder.ConfigureServices(services => {
                _ = services.AddSingleton<IDbmService>(dbm);
                _ = services.AddSingleton<StatementDataService>();
            }));
        _client = customFactory.CreateClient();
    }

    private async Task SeedData() {
        _ = await _dbm.BulkInsertCompanies([new Company(1, 320193, "EDGAR")], CancellationToken.None);
        _ = await _dbm.BulkInsertSubmissions([
            new Submission(10, 1, "ref-1", FilingType.TenK, FilingCategory.Annual,
                new DateOnly(2024, 9, 28), null)
        ], CancellationToken.None);

        // Create taxonomy type matching the submission's report date year
        _ = await _dbm.EnsureTaxonomyType("us-gaap", 2024, CancellationToken.None);
    }

    [Fact]
    public async Task ListStatements_ReturnsAvailableRoles() {
        await SeedData();

        HttpResponseMessage response = await _client.GetAsync("/api/companies/320193/submissions/10/statements");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        // Should return a JSON array (possibly empty since no taxonomy concepts are loaded)
        Assert.StartsWith("[", body);
    }

    [Fact]
    public async Task GetStatement_InvalidConcept_Returns404() {
        await SeedData();

        HttpResponseMessage response = await _client.GetAsync(
            "/api/companies/320193/submissions/10/statements/NonExistentConcept");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetStatement_UnknownCik_Returns404() {
        HttpResponseMessage response = await _client.GetAsync(
            "/api/companies/999999/submissions/10/statements/SomeConcept");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
