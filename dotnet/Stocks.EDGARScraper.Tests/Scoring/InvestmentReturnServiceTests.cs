using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Stocks.DataModels;
using Stocks.DataModels.Scoring;
using Stocks.Persistence.Database;
using Stocks.Persistence.Services;
using Stocks.Shared;
using Stocks.Shared.Models;

namespace Stocks.EDGARScraper.Tests.Scoring;

public class InvestmentReturnServiceTests {
    private readonly DbmInMemoryService _dbm = new();
    private readonly CancellationToken _ct = CancellationToken.None;

    private async Task SeedCompanyWithTicker(ulong companyId, ulong cik, string ticker) {
        await _dbm.BulkInsertCompanies([new Company(companyId, cik, "EDGAR")], _ct);
        await _dbm.BulkInsertCompanyTickers([new CompanyTicker(companyId, ticker, "NYSE")], _ct);
    }

    private async Task SeedPrices(List<PriceRow> prices) {
        await _dbm.BulkInsertPrices(prices, _ct);
    }

    private static PriceRow MakePrice(ulong priceId, ulong cik, string ticker, DateOnly date, decimal close) {
        return new PriceRow(priceId, cik, ticker, "NYSE", ticker.ToLowerInvariant() + ".us",
            date, close - 1m, close + 1m, close - 2m, close, 1_000_000);
    }

    [Theory]
    [InlineData(150, 50, 1500)]    // positive return
    [InlineData(80, -20, 800)]     // negative return
    public async Task ComputeReturn_VariousReturns(int endPriceInt, int expectedReturnPctInt, int expectedValueOf1000Int) {
        decimal endPrice = endPriceInt;
        decimal expectedReturnPct = expectedReturnPctInt;
        decimal expectedValueOf1000 = expectedValueOf1000Int;

        await SeedCompanyWithTicker(1, 320193, "AAPL");
        await SeedPrices([
            MakePrice(1, 320193, "AAPL", new DateOnly(2024, 1, 15), 100m),
            MakePrice(2, 320193, "AAPL", new DateOnly(2025, 1, 15), endPrice),
        ]);

        var service = new InvestmentReturnService(_dbm);
        Result<InvestmentReturnResult> result = await service.ComputeReturn("AAPL", new DateOnly(2024, 1, 15), _ct);

        Assert.True(result.IsSuccess);
        InvestmentReturnResult r = result.Value!;
        Assert.Equal("AAPL", r.Ticker);
        Assert.Equal(new DateOnly(2024, 1, 15), r.StartDate);
        Assert.Equal(new DateOnly(2025, 1, 15), r.EndDate);
        Assert.Equal(100m, r.StartPrice);
        Assert.Equal(endPrice, r.EndPrice);
        Assert.Equal(expectedReturnPct, r.TotalReturnPct);
        Assert.Equal(expectedValueOf1000, r.CurrentValueOf1000);
    }

    [Fact]
    public async Task ComputeReturn_MissingStartPrice_ReturnsFailure() {
        await SeedCompanyWithTicker(1, 320193, "AAPL");
        await SeedPrices([
            MakePrice(1, 320193, "AAPL", new DateOnly(2025, 1, 15), 150m),
        ]);

        var service = new InvestmentReturnService(_dbm);
        Result<InvestmentReturnResult> result = await service.ComputeReturn("AAPL", new DateOnly(2024, 1, 1), _ct);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.NoPriceData, result.ErrorCode);
    }

    [Fact]
    public async Task ComputeReturn_MissingEndPrice_ReturnsFailure() {
        var service = new InvestmentReturnService(_dbm);
        Result<InvestmentReturnResult> result = await service.ComputeReturn("ZZZZ", new DateOnly(2024, 1, 1), _ct);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.NoPriceData, result.ErrorCode);
    }

    [Fact]
    public async Task ComputeReturn_ZeroStartPrice_ReturnsFailure() {
        await SeedCompanyWithTicker(1, 320193, "AAPL");
        await SeedPrices([
            MakePrice(1, 320193, "AAPL", new DateOnly(2024, 1, 15), 0m),
            MakePrice(2, 320193, "AAPL", new DateOnly(2025, 1, 15), 150m),
        ]);

        var service = new InvestmentReturnService(_dbm);
        Result<InvestmentReturnResult> result = await service.ComputeReturn("AAPL", new DateOnly(2024, 1, 15), _ct);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.NoPriceData, result.ErrorCode);
    }

    [Fact]
    public async Task ComputeReturn_ZeroEndPrice_ReturnsFailure() {
        await SeedCompanyWithTicker(1, 320193, "AAPL");
        await SeedPrices([
            MakePrice(1, 320193, "AAPL", new DateOnly(2024, 1, 15), 100m),
            MakePrice(2, 320193, "AAPL", new DateOnly(2025, 1, 15), 0m),
        ]);

        var service = new InvestmentReturnService(_dbm);
        Result<InvestmentReturnResult> result = await service.ComputeReturn("AAPL", new DateOnly(2024, 1, 15), _ct);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.NoPriceData, result.ErrorCode);
    }

    [Fact]
    public async Task ComputeReturn_SameDay_AnnualizedReturnIsNull() {
        await SeedCompanyWithTicker(1, 320193, "AAPL");
        await SeedPrices([
            MakePrice(1, 320193, "AAPL", new DateOnly(2025, 1, 15), 100m),
        ]);

        var service = new InvestmentReturnService(_dbm);
        Result<InvestmentReturnResult> result = await service.ComputeReturn("AAPL", new DateOnly(2025, 1, 15), _ct);

        Assert.True(result.IsSuccess);
        InvestmentReturnResult r = result.Value!;
        Assert.Equal(0m, r.TotalReturnPct);
        Assert.Null(r.AnnualizedReturnPct);
        Assert.Equal(1000m, r.CurrentValueOf1000);
    }

    [Fact]
    public async Task ComputeReturn_WeekendAlignment_UsesFridayPrice() {
        await SeedCompanyWithTicker(1, 320193, "AAPL");
        await SeedPrices([
            MakePrice(1, 320193, "AAPL", new DateOnly(2024, 1, 12), 100m), // Friday
            MakePrice(2, 320193, "AAPL", new DateOnly(2025, 1, 15), 120m),
        ]);

        var service = new InvestmentReturnService(_dbm);
        // Request Saturday Jan 13 — should snap back to Friday Jan 12
        Result<InvestmentReturnResult> result = await service.ComputeReturn("AAPL", new DateOnly(2024, 1, 13), _ct);

        Assert.True(result.IsSuccess);
        InvestmentReturnResult r = result.Value!;
        Assert.Equal(new DateOnly(2024, 1, 12), r.StartDate);
        Assert.Equal(100m, r.StartPrice);
        Assert.Equal(new DateOnly(2025, 1, 15), r.EndDate);
        Assert.Equal(120m, r.EndPrice);
        Assert.Equal(20m, r.TotalReturnPct);
        Assert.Equal(1200m, r.CurrentValueOf1000);
    }

    [Fact]
    public async Task ComputeReturn_OverflowProtection_DoesNotThrow() {
        await SeedCompanyWithTicker(1, 320193, "AAPL");
        await SeedPrices([
            MakePrice(1, 320193, "AAPL", new DateOnly(2025, 1, 14), 0.01m),
            MakePrice(2, 320193, "AAPL", new DateOnly(2025, 1, 15), 10000m),
        ]);

        var service = new InvestmentReturnService(_dbm);
        Result<InvestmentReturnResult> result = await service.ComputeReturn("AAPL", new DateOnly(2025, 1, 14), _ct);

        Assert.True(result.IsSuccess);
        InvestmentReturnResult r = result.Value!;
        Assert.True(r.TotalReturnPct > 0);
        Assert.Null(r.AnnualizedReturnPct);
    }
}
