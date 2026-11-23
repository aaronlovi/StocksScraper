using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Stocks.DataModels;
using Stocks.DataModels.Scoring;
using Stocks.Persistence.Database;

namespace Stocks.WebApi.Tests;

public class ReportFinancialsEndpointsTests : IClassFixture<WebApplicationFactory<Program>> {
    private readonly HttpClient _client;

    public ReportFinancialsEndpointsTests(WebApplicationFactory<Program> factory) {
        var dbm = new DbmInMemoryService();
        Seed(dbm).GetAwaiter().GetResult();
        WebApplicationFactory<Program> customFactory = factory.WithWebHostBuilder(builder =>
            _ = builder.ConfigureServices(services => _ = services.AddSingleton<IDbmService>(dbm)));
        _client = customFactory.CreateClient();
    }

    private static async Task Seed(DbmInMemoryService dbm) {
        CancellationToken ct = CancellationToken.None;
        DateTime now = DateTime.UtcNow;
        _ = await dbm.BulkInsertCompanyScores([
            new CompanyScoreSummary(1, "320193", "Apple", "AAPL", "NASDAQ", 12, 15, 5,
                null, null, null, null, null, null, null, null, null, null, null, null,
                null, null, null, null, null, null, now),
            new CompanyScoreSummary(2, "70858", "Bank of America", "BAC", "NYSE", 13, 15, 5,
                null, null, null, null, null, null, null, null, null, null, null, null,
                null, null, null, null, null, null, now),
        ], ct);
        _ = await dbm.BulkInsertCompanyMoatScores([
            new CompanyMoatScoreSummary(1, "320193", "Apple", "AAPL", "NASDAQ", 11, 13, 5,
                null, null, null, null, null, null, null, null, null, null, null, null, now),
            new CompanyMoatScoreSummary(2, "70858", "Bank of America", "BAC", "NYSE", 9, 13, 5,
                null, null, null, null, null, null, null, null, null, null, null, null, now),
        ], ct);
        _ = await dbm.UpdateCompanySic([
            new CompanySic(1, 3571, "Electronic Computers"),
            new CompanySic(2, 6021, "National Commercial Banks"),
        ], ct);
    }

    private async Task<List<JsonElement>> GetItems(string url) {
        HttpResponseMessage response = await _client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var items = new List<JsonElement>();
        foreach (JsonElement item in doc.RootElement.GetProperty("items").EnumerateArray())
            items.Add(item.Clone());
        return items;
    }

    [Theory]
    [InlineData("/api/reports/scores")]
    [InlineData("/api/reports/moat-scores")]
    public async Task Reports_RowsCarrySicFields(string path) {
        List<JsonElement> items = await GetItems(path);

        Assert.Equal(2, items.Count);
        foreach (JsonElement item in items) {
            bool isBank = item.GetProperty("ticker").GetString() == "BAC";
            Assert.Equal(isBank, item.GetProperty("isFinancial").GetBoolean());
            Assert.Equal(isBank ? 6021 : 3571, item.GetProperty("sicCode").GetInt32());
            Assert.False(string.IsNullOrEmpty(item.GetProperty("sicDescription").GetString()));
        }
    }

    [Theory]
    [InlineData("/api/reports/scores?financials=only", "BAC")]
    [InlineData("/api/reports/scores?financials=exclude", "AAPL")]
    [InlineData("/api/reports/moat-scores?financials=ONLY", "BAC")]
    [InlineData("/api/reports/moat-scores?financials=exclude", "AAPL")]
    public async Task Reports_FinancialsFilter(string url, string expectedTicker) {
        List<JsonElement> items = await GetItems(url);

        Assert.Single(items);
        Assert.Equal(expectedTicker, items[0].GetProperty("ticker").GetString());
    }

    [Fact]
    public async Task Reports_UnknownFinancialsValueMeansAll() {
        List<JsonElement> items = await GetItems("/api/reports/scores?financials=bogus");

        Assert.Equal(2, items.Count);
    }
}
