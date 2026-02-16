using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Stocks.DataModels;
using Stocks.DataModels.Enums;
using Stocks.Persistence.Database;
using Stocks.Shared;

namespace Stocks.EDGARScraper.Tests;

public class CompanyTickersTests {
    private readonly DbmInMemoryService _dbm = new();
    private readonly CancellationToken _ct = CancellationToken.None;

    [Fact]
    public async Task BulkInsertCompanyTickers_InsertsAndRetrievesByCompanyId() {
        _ = await _dbm.BulkInsertCompanies([
            new Company(1, 100, "EDGAR"),
            new Company(2, 200, "EDGAR")
        ], _ct);

        _ = await _dbm.BulkInsertCompanyTickers([
            new CompanyTicker(1, "AAPL", "NASDAQ"),
            new CompanyTicker(1, "AAPL.US", "NYSE"),
            new CompanyTicker(2, "MSFT", "NASDAQ")
        ], _ct);

        Result<IReadOnlyCollection<CompanyTicker>> result = await _dbm.GetCompanyTickersByCompanyId(1, _ct);
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count);

        var tickers = new List<CompanyTicker>(result.Value);
        Assert.Contains(tickers, t => t.Ticker == "AAPL" && t.Exchange == "NASDAQ");
        Assert.Contains(tickers, t => t.Ticker == "AAPL.US" && t.Exchange == "NYSE");
    }

    [Fact]
    public async Task BulkInsertCompanyTickers_UpsertUpdatesExchange() {
        _ = await _dbm.BulkInsertCompanies([new Company(1, 100, "EDGAR")], _ct);

        _ = await _dbm.BulkInsertCompanyTickers([
            new CompanyTicker(1, "AAPL", "NASDAQ")
        ], _ct);

        _ = await _dbm.BulkInsertCompanyTickers([
            new CompanyTicker(1, "AAPL", "NYSE")
        ], _ct);

        Result<IReadOnlyCollection<CompanyTicker>> result = await _dbm.GetCompanyTickersByCompanyId(1, _ct);
        Assert.True(result.IsSuccess);
        CompanyTicker single = Assert.Single(result.Value!);
        Assert.Equal("NYSE", single.Exchange);
    }

    [Fact]
    public async Task GetCompanyTickersByCompanyId_ReturnsEmptyForUnknownCompany() {
        Result<IReadOnlyCollection<CompanyTicker>> result = await _dbm.GetCompanyTickersByCompanyId(999, _ct);
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public async Task GetSubmissionsByCompanyId_ReturnsDescByReportDate() {
        _ = await _dbm.BulkInsertCompanies([new Company(1, 100, "EDGAR")], _ct);

        _ = await _dbm.BulkInsertSubmissions([
            new Submission(10, 1, "ref-a", FilingType.TenK, FilingCategory.Annual,
                new DateOnly(2023, 3, 15), null),
            new Submission(11, 1, "ref-b", FilingType.TenQ, FilingCategory.Quarterly,
                new DateOnly(2024, 6, 30), null),
            new Submission(12, 1, "ref-c", FilingType.TenK, FilingCategory.Annual,
                new DateOnly(2022, 12, 31), null)
        ], _ct);

        Result<IReadOnlyCollection<Submission>> result = await _dbm.GetSubmissionsByCompanyId(1, _ct);
        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value!.Count);

        var submissions = new List<Submission>(result.Value);
        Assert.Equal(new DateOnly(2024, 6, 30), submissions[0].ReportDate);
        Assert.Equal(new DateOnly(2023, 3, 15), submissions[1].ReportDate);
        Assert.Equal(new DateOnly(2022, 12, 31), submissions[2].ReportDate);
    }

    [Fact]
    public async Task GetSubmissionsByCompanyId_ReturnsEmptyForUnknownCompany() {
        Result<IReadOnlyCollection<Submission>> result = await _dbm.GetSubmissionsByCompanyId(999, _ct);
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public async Task GetCompanyByCik_Found_ReturnsCompany() {
        _ = await _dbm.BulkInsertCompanies([new Company(1, 320193, "EDGAR")], _ct);

        Result<Company> result = await _dbm.GetCompanyByCik("320193", _ct);
        Assert.True(result.IsSuccess);
        Assert.Equal(1UL, result.Value!.CompanyId);
        Assert.Equal(320193UL, result.Value.Cik);
    }

    [Fact]
    public async Task GetCompanyByCik_NotFound_ReturnsFailure() {
        Result<Company> result = await _dbm.GetCompanyByCik("999999", _ct);
        Assert.True(result.IsFailure);
    }
}
