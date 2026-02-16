using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Stocks.DataModels;
using Stocks.DataModels.Enums;
using Stocks.Persistence.Database;
using Stocks.Shared;

namespace Stocks.EDGARScraper.Tests;

public class DashboardStatsTests {
    private readonly DbmInMemoryService _dbm = new();
    private readonly CancellationToken _ct = CancellationToken.None;

    [Fact]
    public async Task GetDashboardStats_EmptyDatabase_ReturnsZeroCounts() {
        Result<DashboardStats> result = await _dbm.GetDashboardStats(_ct);
        Assert.True(result.IsSuccess);

        DashboardStats stats = result.Value!;
        Assert.Equal(0, stats.TotalCompanies);
        Assert.Equal(0, stats.TotalSubmissions);
        Assert.Equal(0, stats.TotalDataPoints);
        Assert.Null(stats.EarliestFilingDate);
        Assert.Null(stats.LatestFilingDate);
        Assert.Equal(0, stats.CompaniesWithPriceData);
        Assert.Empty(stats.SubmissionsByFilingType);
    }

    [Fact]
    public async Task GetDashboardStats_WithData_ReturnsCorrectCounts() {
        _ = await _dbm.BulkInsertCompanies([
            new Company(1, 100, "EDGAR"),
            new Company(2, 200, "EDGAR"),
            new Company(3, 300, "EDGAR")
        ], _ct);

        _ = await _dbm.BulkInsertSubmissions([
            new Submission(10, 1, "ref-1", FilingType.TenK, FilingCategory.Annual,
                new DateOnly(2023, 3, 15), null),
            new Submission(11, 2, "ref-2", FilingType.TenQ, FilingCategory.Quarterly,
                new DateOnly(2024, 6, 30), null)
        ], _ct);

        _ = await _dbm.BulkInsertDataPoints([
            new DataPoint(100, 1, "Revenue", "ref-1",
                new DatePair(new DateOnly(2022, 1, 1), new DateOnly(2022, 12, 31)),
                1000m, new DataPointUnit(1, "USD"), new DateOnly(2023, 3, 15), 10, 0),
            new DataPoint(101, 1, "Assets", "ref-1",
                new DatePair(new DateOnly(2022, 12, 31), new DateOnly(2022, 12, 31)),
                5000m, new DataPointUnit(1, "USD"), new DateOnly(2023, 3, 15), 10, 0),
            new DataPoint(102, 2, "Revenue", "ref-2",
                new DatePair(new DateOnly(2024, 1, 1), new DateOnly(2024, 6, 30)),
                2000m, new DataPointUnit(1, "USD"), new DateOnly(2024, 6, 30), 11, 0)
        ], _ct);

        _ = await _dbm.UpsertPriceImport(
            new PriceImportStatus(100, "AAPL", "NASDAQ", DateTime.UtcNow), _ct);
        _ = await _dbm.UpsertPriceImport(
            new PriceImportStatus(200, "MSFT", "NASDAQ", DateTime.UtcNow), _ct);

        Result<DashboardStats> result = await _dbm.GetDashboardStats(_ct);
        Assert.True(result.IsSuccess);

        DashboardStats stats = result.Value!;
        Assert.Equal(3, stats.TotalCompanies);
        Assert.Equal(2, stats.TotalSubmissions);
        Assert.Equal(3, stats.TotalDataPoints);
        Assert.Equal(new DateOnly(2023, 3, 15), stats.EarliestFilingDate);
        Assert.Equal(new DateOnly(2024, 6, 30), stats.LatestFilingDate);
        Assert.Equal(2, stats.CompaniesWithPriceData);
    }

    [Fact]
    public async Task GetDashboardStats_SubmissionsByFilingType_GroupsCorrectly() {
        _ = await _dbm.BulkInsertCompanies([
            new Company(1, 100, "EDGAR")
        ], _ct);

        _ = await _dbm.BulkInsertSubmissions([
            new Submission(10, 1, "ref-1", FilingType.TenK, FilingCategory.Annual,
                new DateOnly(2023, 3, 15), null),
            new Submission(11, 1, "ref-2", FilingType.TenK, FilingCategory.Annual,
                new DateOnly(2024, 3, 15), null),
            new Submission(12, 1, "ref-3", FilingType.TenQ, FilingCategory.Quarterly,
                new DateOnly(2023, 6, 30), null),
            new Submission(13, 1, "ref-4", FilingType.EightK, FilingCategory.Other,
                new DateOnly(2023, 9, 1), null)
        ], _ct);

        Result<DashboardStats> result = await _dbm.GetDashboardStats(_ct);
        Assert.True(result.IsSuccess);

        IReadOnlyDictionary<string, long> byType = result.Value!.SubmissionsByFilingType;
        Assert.Equal(3, byType.Count);
        Assert.Equal(2, byType["10-K"]);
        Assert.Equal(1, byType["10-Q"]);
        Assert.Equal(1, byType["8-K"]);
    }
}
