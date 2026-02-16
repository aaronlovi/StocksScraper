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

namespace Stocks.WebApi.Tests;

public class SubmissionEndpointsTests : IClassFixture<WebApplicationFactory<Program>> {
    private readonly HttpClient _client;
    private readonly DbmInMemoryService _dbm = new();

    public SubmissionEndpointsTests(WebApplicationFactory<Program> factory) {
        DbmInMemoryService dbm = _dbm;
        WebApplicationFactory<Program> customFactory = factory.WithWebHostBuilder(builder =>
            _ = builder.ConfigureServices(services =>
                _ = services.AddSingleton<IDbmService>(dbm)));
        _client = customFactory.CreateClient();
    }

    [Fact]
    public async Task GetSubmissions_ReturnsAllSubmissionsForCompany() {
        _ = await _dbm.BulkInsertCompanies([new Company(1, 320193, "EDGAR")], CancellationToken.None);
        _ = await _dbm.BulkInsertSubmissions([
            new Submission(10, 1, "ref-1", FilingType.TenK, FilingCategory.Annual,
                new DateOnly(2024, 9, 28), null),
            new Submission(11, 1, "ref-2", FilingType.TenQ, FilingCategory.Quarterly,
                new DateOnly(2024, 6, 29), null)
        ], CancellationToken.None);

        HttpResponseMessage response = await _client.GetAsync("/api/companies/320193/submissions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("TenK", body);
        Assert.Contains("TenQ", body);
    }

    [Fact]
    public async Task GetSubmissions_UnknownCik_Returns404() {
        HttpResponseMessage response = await _client.GetAsync("/api/companies/999999/submissions");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
