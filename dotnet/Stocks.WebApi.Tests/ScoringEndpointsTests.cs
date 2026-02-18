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
using Stocks.DataModels.Enums;
using Stocks.Persistence.Database;
using Stocks.Persistence.Database.DTO.Taxonomies;
using Stocks.Persistence.Services;

namespace Stocks.WebApi.Tests;

public class ScoringEndpointsTests : IClassFixture<WebApplicationFactory<Program>> {
    private readonly HttpClient _client;
    private readonly DbmInMemoryService _dbm = new();
    private readonly CancellationToken _ct = CancellationToken.None;

    private const ulong CompanyId = 1;
    private const ulong CompanyCik = 320193;

    public ScoringEndpointsTests(WebApplicationFactory<Program> factory) {
        DbmInMemoryService dbm = _dbm;
        WebApplicationFactory<Program> customFactory = factory.WithWebHostBuilder(builder =>
            _ = builder.ConfigureServices(services => {
                _ = services.AddSingleton<IDbmService>(dbm);
                _ = services.AddSingleton<ScoringService>();
            }));
        _client = customFactory.CreateClient();
    }

    private async Task SeedScoringData() {
        await _dbm.BulkInsertCompanies([new Company(CompanyId, CompanyCik, "EDGAR")], _ct);

        await _dbm.BulkInsertCompanyTickers(
            [new CompanyTicker(CompanyId, "AAPL", "NASDAQ")], _ct);

        await _dbm.BulkInsertPrices([
            new PriceRow(1, CompanyCik, "AAPL", "NASDAQ", "AAPL.US",
                new DateOnly(2024, 12, 20), 170m, 175m, 169m, 174m, 50_000_000)
        ], _ct);

        await _dbm.EnsureTaxonomyType("us-gaap", 2024, _ct);
        await _dbm.BulkInsertTaxonomyConcepts([
            new ConceptDetailsDTO(100, 1, 1, 0, false, "StockholdersEquity", "Stockholders Equity", ""),
            new ConceptDetailsDTO(101, 1, 1, 0, false, "RetainedEarningsAccumulatedDeficit", "Retained Earnings", ""),
            new ConceptDetailsDTO(102, 1, 2, 0, false, "NetIncomeLoss", "Net Income", ""),
            new ConceptDetailsDTO(103, 1, 1, 0, false, "LongTermDebt", "Long Term Debt", ""),
            new ConceptDetailsDTO(104, 1, 1, 0, false, "Goodwill", "Goodwill", ""),
            new ConceptDetailsDTO(105, 1, 1, 0, false, "IntangibleAssetsNetExcludingGoodwill", "Intangibles", ""),
            new ConceptDetailsDTO(106, 1, 2, 0, false, "PaymentsOfDividends", "Dividends Paid", ""),
            new ConceptDetailsDTO(107, 1, 1, 0, false, "CommonStockSharesOutstanding", "Shares Outstanding", ""),
            new ConceptDetailsDTO(108, 1, 1, 0, false, "AssetsCurrent", "Current Assets", ""),
            new ConceptDetailsDTO(109, 1, 1, 0, false, "LiabilitiesCurrent", "Current Liabilities", ""),
            new ConceptDetailsDTO(110, 1, 2, 0, false,
                "CashCashEquivalentsRestrictedCashAndRestrictedCashEquivalentsPeriodIncreaseDecreaseIncludingExchangeRateEffect",
                "Cash Change", ""),
            new ConceptDetailsDTO(111, 1, 2, 0, false, "PaymentsToAcquirePropertyPlantAndEquipment", "CapEx", ""),
        ], _ct);

        var reportDate = new DateOnly(2024, 9, 28);
        await _dbm.BulkInsertSubmissions([
            new Submission(10, CompanyId, "ref-1", FilingType.TenK, FilingCategory.Annual, reportDate, null)
        ], _ct);

        await _dbm.BulkInsertDataPoints([
            MakeDataPoint(1000, 10, 100, 200_000_000_000m, reportDate, reportDate),  // Equity
            MakeDataPoint(1001, 10, 101, 50_000_000_000m, reportDate, reportDate),   // Retained Earnings
            MakeDataPoint(1002, 10, 102, 25_000_000_000m, reportDate, reportDate),   // Net Income
            MakeDataPoint(1003, 10, 103, 30_000_000_000m, reportDate, reportDate),   // Debt
            MakeDataPoint(1004, 10, 104, 5_000_000_000m, reportDate, reportDate),    // Goodwill
            MakeDataPoint(1005, 10, 105, 2_000_000_000m, reportDate, reportDate),    // Intangibles
            MakeDataPoint(1006, 10, 106, 3_000_000_000m, reportDate, reportDate),    // Dividends
            MakeDataPoint(1007, 10, 107, 15_000_000_000m, reportDate, reportDate),   // Shares Outstanding
            MakeDataPoint(1008, 10, 108, 100_000_000_000m, reportDate, reportDate),  // Current Assets
            MakeDataPoint(1009, 10, 109, 80_000_000_000m, reportDate, reportDate),   // Current Liabilities
            MakeDataPoint(1010, 10, 110, 10_000_000_000m, reportDate, reportDate),   // Cash Change
            MakeDataPoint(1011, 10, 111, 8_000_000_000m, reportDate, reportDate),    // CapEx
        ], _ct);
    }

    private static DataPoint MakeDataPoint(ulong dpId, ulong submissionId, long conceptId,
        decimal value, DateOnly startDate, DateOnly endDate) {
        return new DataPoint(
            dpId, CompanyId, "fact", "ref",
            new DatePair(startDate, endDate),
            value,
            new DataPointUnit(1, "USD"),
            endDate,
            submissionId,
            conceptId);
    }

    [Fact]
    public async Task GetScoring_ReturnsOk_WithValidCik() {
        await SeedScoringData();

        HttpResponseMessage response = await _client.GetAsync($"/api/companies/{CompanyCik}/scoring");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("scorecard", body);
        Assert.Contains("overallScore", body);
        Assert.Contains("metrics", body);
    }

    [Fact]
    public async Task GetScoring_ReturnsNotFound_WithInvalidCik() {
        HttpResponseMessage response = await _client.GetAsync("/api/companies/999999/scoring");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetScoring_ResponseContainsScorecard_With13Checks() {
        await SeedScoringData();

        HttpResponseMessage response = await _client.GetAsync($"/api/companies/{CompanyCik}/scoring");
        string body = await response.Content.ReadAsStringAsync();

        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;
        JsonElement scorecard = root.GetProperty("scorecard");
        Assert.Equal(13, scorecard.GetArrayLength());

        // Verify each check has required fields
        foreach (JsonElement check in scorecard.EnumerateArray()) {
            Assert.True(check.TryGetProperty("checkNumber", out _));
            Assert.True(check.TryGetProperty("name", out _));
            Assert.True(check.TryGetProperty("result", out _));
            string result = check.GetProperty("result").GetString()!;
            Assert.Contains(result, new[] { "pass", "fail", "na" });
        }
    }

    [Fact]
    public async Task GetScoring_ResponseContainsDerivedMetrics() {
        await SeedScoringData();

        HttpResponseMessage response = await _client.GetAsync($"/api/companies/{CompanyCik}/scoring");
        string body = await response.Content.ReadAsStringAsync();

        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;
        JsonElement metrics = root.GetProperty("metrics");

        Assert.True(metrics.TryGetProperty("bookValue", out _));
        Assert.True(metrics.TryGetProperty("debtToEquityRatio", out _));
        Assert.True(metrics.TryGetProperty("priceToBookRatio", out _));
        Assert.True(metrics.TryGetProperty("debtToBookRatio", out _));
        Assert.True(metrics.TryGetProperty("adjustedRetainedEarnings", out _));
        Assert.True(metrics.TryGetProperty("estimatedReturnCF", out _));
        Assert.True(metrics.TryGetProperty("estimatedReturnOE", out _));

        // Verify price and shares are present
        Assert.True(root.TryGetProperty("pricePerShare", out _));
        Assert.True(root.TryGetProperty("sharesOutstanding", out _));
        Assert.True(root.TryGetProperty("yearsOfData", out _));
    }
}
