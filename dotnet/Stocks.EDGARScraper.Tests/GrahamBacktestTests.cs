using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Stocks.DataModels;
using Stocks.DataModels.Enums;
using Stocks.DataModels.Scoring;
using Stocks.Persistence.Database;
using Stocks.Persistence.Database.DTO.Taxonomies;
using Stocks.Persistence.Services;
using Stocks.Shared;

namespace Stocks.EDGARScraper.Tests;

public class GrahamBacktestTests {
    private readonly DbmInMemoryService _dbm = new();
    private readonly CancellationToken _ct = CancellationToken.None;

    private static readonly string[] TestConceptNames = ["Revenues"];
    private static readonly DataPointUnit UsdUnit = new(1, "USD");

    private static CompanyScoreSummary MakeScore(
        ulong companyId, string cik, string? name, string? ticker, int score,
        decimal? price, DateOnly? priceDate, decimal? bookValue = null) =>
        new(companyId, cik, name, ticker, "NYSE", score, 15, 5,
            bookValue, null, null, null, null, null, null, null, null, null, null, null,
            price, priceDate, null, null, null, null, DateTime.UtcNow);

    // --- As-of scoring data cutoff ---

    private async Task SeedAsOfScoringData() {
        await _dbm.BulkInsertTaxonomyConcepts([
            new ConceptDetailsDTO(1, 1, 1, 2, false, "Revenues", "Revenues", "Total revenues")
        ], _ct);
        await _dbm.BulkInsertCompanies([new Company(1, 111111, "EDGAR")], _ct);

        // 2023 10-K accepted early 2024; 2024 10-K accepted early 2025; one filing never dated
        await _dbm.BulkInsertSubmissions([
            new Submission(100, 1, "0001-100", FilingType.TenK, FilingCategory.Annual,
                new DateOnly(2023, 12, 31), new DateTime(2024, 2, 1, 12, 0, 0, DateTimeKind.Utc)),
            new Submission(101, 1, "0001-101", FilingType.TenK, FilingCategory.Annual,
                new DateOnly(2024, 12, 31), new DateTime(2025, 2, 1, 12, 0, 0, DateTimeKind.Utc)),
            new Submission(102, 1, "0001-102", FilingType.TenK, FilingCategory.Annual,
                new DateOnly(2022, 12, 31), null),
        ], _ct);

        await _dbm.BulkInsertDataPoints([
            new DataPoint(1000, 1, "Revenues", "ref", new DatePair(new DateOnly(2023, 1, 1), new DateOnly(2023, 12, 31)), 100m, UsdUnit, new DateOnly(2024, 2, 1), 100, 1),
            new DataPoint(1001, 1, "Revenues", "ref", new DatePair(new DateOnly(2024, 1, 1), new DateOnly(2024, 12, 31)), 200m, UsdUnit, new DateOnly(2025, 2, 1), 101, 1),
            new DataPoint(1002, 1, "Revenues", "ref", new DatePair(new DateOnly(2022, 1, 1), new DateOnly(2022, 12, 31)), 50m, UsdUnit, new DateOnly(2023, 2, 1), 102, 1),
        ], _ct);
    }

    [Fact]
    public async Task GetAllScoringDataPointsAsOf_ExcludesFilingsAcceptedAfterCutoff() {
        await SeedAsOfScoringData();

        Result<IReadOnlyCollection<BatchScoringConceptValue>> result =
            await _dbm.GetAllScoringDataPointsAsOf(TestConceptNames, new DateOnly(2024, 6, 30), _ct);
        Assert.True(result.IsSuccess);

        // Only the 2023 filing was public by mid-2024; the 2024 filing (accepted 2025)
        // and the filing with no acceptance time are both excluded.
        BatchScoringConceptValue single = Assert.Single(result.Value!);
        Assert.Equal(new DateOnly(2023, 12, 31), single.ReportDate);
        Assert.Equal(100m, single.Value);
    }

    [Fact]
    public async Task GetAllScoringDataPointsAsOf_IncludesFilingAcceptedOnCutoffDay() {
        await SeedAsOfScoringData();

        Result<IReadOnlyCollection<BatchScoringConceptValue>> result =
            await _dbm.GetAllScoringDataPointsAsOf(TestConceptNames, new DateOnly(2025, 2, 1), _ct);
        Assert.True(result.IsSuccess);

        var reportDates = new HashSet<DateOnly>();
        foreach (BatchScoringConceptValue v in result.Value!)
            _ = reportDates.Add(v.ReportDate);

        Assert.Contains(new DateOnly(2024, 12, 31), reportDates);
        Assert.Contains(new DateOnly(2023, 12, 31), reportDates);
    }

    [Fact]
    public async Task GetAllScoringDataPoints_WithoutCutoff_IncludesEverything() {
        await SeedAsOfScoringData();

        Result<IReadOnlyCollection<BatchScoringConceptValue>> result =
            await _dbm.GetAllScoringDataPoints(TestConceptNames, _ct);
        Assert.True(result.IsSuccess);

        // No cutoff: both accepted filings plus the one with a null acceptance time
        Assert.Equal(3, result.Value!.Count);
    }

    // --- Snapshot storage ---

    [Fact]
    public async Task GrahamScoreSnapshots_DeleteThenInsertIsIdempotent() {
        var date = new DateOnly(2025, 12, 31);

        _ = await _dbm.BulkInsertGrahamScoreSnapshots(date,
            [MakeScore(1, "111", "Old Co", "OLD", 15, 10m, date)], _ct);
        _ = await _dbm.DeleteGrahamScoreSnapshotsByDate(date, _ct);
        _ = await _dbm.BulkInsertGrahamScoreSnapshots(date,
            [MakeScore(2, "222", "New Co", "NEW", 15, 20m, date)], _ct);

        Result<PagedResults<CompanyScoreSummary>> result =
            await _dbm.GetGrahamScoreSnapshots(date, new PaginationRequest(1, 100), null, _ct);
        Assert.True(result.IsSuccess);

        CompanyScoreSummary single = Assert.Single(result.Value!.Items);
        Assert.Equal("New Co", single.CompanyName);
    }

    [Fact]
    public async Task GrahamScoreSnapshotDates_ReturnsSortedDistinctDates() {
        var dec = new DateOnly(2025, 12, 31);
        var jan = new DateOnly(2026, 1, 31);

        _ = await _dbm.BulkInsertGrahamScoreSnapshots(jan, [MakeScore(1, "111", "A", "AAA", 15, 10m, jan)], _ct);
        _ = await _dbm.BulkInsertGrahamScoreSnapshots(dec, [MakeScore(1, "111", "A", "AAA", 15, 9m, dec)], _ct);

        Result<IReadOnlyCollection<DateOnly>> result = await _dbm.GetGrahamScoreSnapshotDates(_ct);
        Assert.True(result.IsSuccess);
        Assert.Equal([dec, jan], result.Value!);
    }

    [Fact]
    public async Task GrahamScoreSnapshots_FilterByMinScore() {
        var date = new DateOnly(2025, 12, 31);
        _ = await _dbm.BulkInsertGrahamScoreSnapshots(date, [
            MakeScore(1, "111", "Perfect", "PRF", 15, 10m, date),
            MakeScore(2, "222", "Almost", "ALM", 14, 20m, date),
        ], _ct);

        Result<PagedResults<CompanyScoreSummary>> result = await _dbm.GetGrahamScoreSnapshots(
            date, new PaginationRequest(1, 100), new ScoresFilter(15, null, null), _ct);
        Assert.True(result.IsSuccess);

        CompanyScoreSummary single = Assert.Single(result.Value!.Items);
        Assert.Equal("Perfect", single.CompanyName);
    }

    // --- Backtest math ---

    private async Task SeedBacktestScenario() {
        var jan = new DateOnly(2026, 1, 31);
        var feb = new DateOnly(2026, 2, 28);

        // January portfolio: A and B. February portfolio: A stays, B leaves, C enters.
        _ = await _dbm.BulkInsertGrahamScoreSnapshots(jan, [
            MakeScore(1, "111", "Alpha", "AAA", 15, 100m, jan),
            MakeScore(2, "222", "Beta", "BBB", 15, 50m, jan),
            MakeScore(9, "999", "Filler", "FIL", 10, 5m, jan),
        ], _ct);
        _ = await _dbm.BulkInsertGrahamScoreSnapshots(feb, [
            MakeScore(1, "111", "Alpha", "AAA", 15, 110m, feb),
            MakeScore(3, "333", "Gamma", "CCC", 15, 200m, feb),
        ], _ct);

        await _dbm.BulkInsertPrices([
            new PriceRow(1, 111, "AAA", "NYSE", "aaa.us", jan, 100m, 100m, 100m, 100m, 1000),
            new PriceRow(2, 111, "AAA", "NYSE", "aaa.us", feb, 110m, 110m, 110m, 110m, 1000),
            new PriceRow(3, 111, "AAA", "NYSE", "aaa.us", new DateOnly(2026, 6, 25), 121m, 121m, 121m, 121m, 1000),
            new PriceRow(4, 222, "BBB", "NYSE", "bbb.us", jan, 50m, 50m, 50m, 50m, 1000),
            new PriceRow(5, 222, "BBB", "NYSE", "bbb.us", feb, 45m, 45m, 45m, 45m, 1000),
            new PriceRow(6, 333, "CCC", "NYSE", "ccc.us", feb, 200m, 200m, 200m, 200m, 1000),
            new PriceRow(7, 333, "CCC", "NYSE", "ccc.us", new DateOnly(2026, 6, 25), 210m, 210m, 210m, 210m, 1000),
            new PriceRow(8, 0, "SPY", "NYSE", "spy.us", jan, 500m, 500m, 500m, 500m, 1000),
            new PriceRow(9, 0, "SPY", "NYSE", "spy.us", feb, 505m, 505m, 505m, 505m, 1000),
            new PriceRow(10, 0, "SPY", "NYSE", "spy.us", new DateOnly(2026, 6, 25), 510.05m, 510.05m, 510.05m, 510.05m, 1000),
        ], _ct);
    }

    [Fact]
    public async Task GetBacktest_ChainsEqualWeightedMonthlyReturns() {
        await SeedBacktestScenario();
        var service = new GrahamBacktestService(_dbm);

        Result<GrahamBacktestReport> result = await service.GetBacktest(15, GrahamBacktestInterval.Monthly, GrahamBacktestPolicy.All, false, _ct);
        Assert.True(result.IsSuccess);

        GrahamBacktestReport report = result.Value!;
        Assert.Equal(2, report.Periods.Count);

        // Period 1: AAA +10%, BBB -10% -> equal weight 0%
        GrahamBacktestPeriod first = report.Periods[0];
        Assert.Equal(2, first.ConstituentCount);
        Assert.Equal(0m, first.PortfolioReturnPct);
        Assert.Equal(1000m, first.CumulativeValue);
        Assert.Equal(1m, first.BenchmarkReturnPct); // SPY 500 -> 505

        // Period 2: AAA 110->121 (+10%), CCC 200->210 (+5%) -> +7.5%
        GrahamBacktestPeriod second = report.Periods[1];
        Assert.Equal(2, second.ConstituentCount);
        Assert.Equal(7.5m, second.PortfolioReturnPct);
        Assert.Equal(1075m, second.CumulativeValue);
        Assert.Equal(1m, second.BenchmarkReturnPct); // SPY 505 -> 510.05

        Assert.Equal(7.5m, report.Summary.TotalReturnPct);
        Assert.Equal(1075m, report.Summary.FinalValue);
        Assert.Equal(1020.1m, report.Summary.BenchmarkFinalValue);
    }

    [Fact]
    public async Task GetBacktest_FlagsEnteredAndLeftConstituents() {
        await SeedBacktestScenario();
        var service = new GrahamBacktestService(_dbm);

        Result<GrahamBacktestReport> result = await service.GetBacktest(15, GrahamBacktestInterval.Monthly, GrahamBacktestPolicy.All, false, _ct);
        Assert.True(result.IsSuccess);

        var firstByTicker = new Dictionary<string, GrahamBacktestConstituent>();
        foreach (GrahamBacktestConstituent c in result.Value!.Periods[0].Constituents)
            firstByTicker[c.Ticker!] = c;

        Assert.False(firstByTicker["AAA"].Entered); // first period: nothing is "new"
        Assert.False(firstByTicker["AAA"].Left);    // AAA stays for February
        Assert.True(firstByTicker["BBB"].Left);     // BBB is gone in February

        var secondByTicker = new Dictionary<string, GrahamBacktestConstituent>();
        foreach (GrahamBacktestConstituent c in result.Value!.Periods[1].Constituents)
            secondByTicker[c.Ticker!] = c;

        Assert.False(secondByTicker["AAA"].Entered);
        Assert.True(secondByTicker["CCC"].Entered); // CCC is new in February

        // CCC had no January snapshot at all -> appearing in the universe is a filing event
        Assert.Equal("filing", secondByTicker["CCC"].EnteredTrigger);
        Assert.Null(secondByTicker["AAA"].EnteredTrigger);
        // BBB left after January but has no February snapshot -> also a filing event
        Assert.Equal("filing", firstByTicker["BBB"].LeftTrigger);
    }

    [Fact]
    public async Task GetBacktest_AttributesEnteredTriggerToPriceOrFiling() {
        var jan = new DateOnly(2026, 1, 31);
        var feb = new DateOnly(2026, 2, 28);

        _ = await _dbm.BulkInsertGrahamScoreSnapshots(jan, [
            MakeScore(1, "111", "Anchor", "AAA", 15, 100m, jan, 500m),
            MakeScore(2, "222", "PriceFlip", "PPP", 14, 50m, jan, 300m),
            MakeScore(3, "333", "FilingFlip", "FFF", 14, 20m, jan, 100m),
        ], _ct);
        _ = await _dbm.BulkInsertGrahamScoreSnapshots(feb, [
            MakeScore(1, "111", "Anchor", "AAA", 15, 100m, feb, 500m),
            // Same fundamentals as January: only the price pushed it over the line
            MakeScore(2, "222", "PriceFlip", "PPP", 15, 40m, feb, 300m),
            // Book value changed: a new filing changed the inputs
            MakeScore(3, "333", "FilingFlip", "FFF", 15, 20m, feb, 200m),
        ], _ct);

        var service = new GrahamBacktestService(_dbm);
        Result<GrahamBacktestReport> result = await service.GetBacktest(15, GrahamBacktestInterval.Monthly, GrahamBacktestPolicy.All, false, _ct);
        Assert.True(result.IsSuccess);

        var febByTicker = new Dictionary<string, GrahamBacktestConstituent>();
        foreach (GrahamBacktestConstituent c in result.Value!.Periods[1].Constituents)
            febByTicker[c.Ticker!] = c;

        Assert.True(febByTicker["PPP"].Entered);
        Assert.Equal("price", febByTicker["PPP"].EnteredTrigger);
        Assert.True(febByTicker["FFF"].Entered);
        Assert.Equal("filing", febByTicker["FFF"].EnteredTrigger);
    }

    [Fact]
    public async Task GetBacktest_CarriesHoldingsWithoutNewerPricesFlat() {
        var jan = new DateOnly(2026, 1, 31);
        _ = await _dbm.BulkInsertGrahamScoreSnapshots(jan, [
            MakeScore(1, "111", "Alpha", "AAA", 15, 100m, jan),
            MakeScore(2, "222", "Delisted", "DDD", 15, 10m, jan),
        ], _ct);

        await _dbm.BulkInsertPrices([
            new PriceRow(1, 111, "AAA", "NYSE", "aaa.us", jan, 100m, 100m, 100m, 100m, 1000),
            new PriceRow(2, 111, "AAA", "NYSE", "aaa.us", new DateOnly(2026, 6, 25), 120m, 120m, 120m, 120m, 1000),
            // DDD has no price after January: carried flat at its last known price
            new PriceRow(3, 222, "DDD", "NYSE", "ddd.us", jan, 10m, 10m, 10m, 10m, 1000),
        ], _ct);

        var service = new GrahamBacktestService(_dbm);
        Result<GrahamBacktestReport> result = await service.GetBacktest(15, GrahamBacktestInterval.Monthly, GrahamBacktestPolicy.All, false, _ct);
        Assert.True(result.IsSuccess);

        // AAA +20%, DDD 0% -> equal weight +10%
        GrahamBacktestPeriod period = Assert.Single(result.Value!.Periods);
        Assert.Equal(10m, period.PortfolioReturnPct);
        Assert.Equal(1100m, period.CumulativeValue);
    }

    [Fact]
    public async Task GetBacktest_EmptyMonthSitsInCash() {
        var jan = new DateOnly(2026, 1, 31);
        // The snapshot exists, but nothing scored 15 that month
        _ = await _dbm.BulkInsertGrahamScoreSnapshots(jan, [
            MakeScore(9, "999", "Filler", "FIL", 10, 5m, jan),
        ], _ct);

        var service = new GrahamBacktestService(_dbm);
        Result<GrahamBacktestReport> result = await service.GetBacktest(15, GrahamBacktestInterval.Monthly, GrahamBacktestPolicy.All, false, _ct);
        Assert.True(result.IsSuccess);

        GrahamBacktestPeriod period = Assert.Single(result.Value!.Periods);
        Assert.Equal(0, period.ConstituentCount);
        Assert.Equal(0m, period.PortfolioReturnPct);
        Assert.Equal(1000m, period.CumulativeValue);
    }

    [Fact]
    public async Task GetBacktest_IntervalFiltersSnapshotDateGrid() {
        var friday = new DateOnly(2026, 6, 26);   // a Friday, not a month-end
        var monthEnd = new DateOnly(2026, 6, 30); // a month-end, not a Friday

        _ = await _dbm.BulkInsertGrahamScoreSnapshots(friday, [MakeScore(1, "111", "Alpha", "AAA", 15, 100m, friday)], _ct);
        _ = await _dbm.BulkInsertGrahamScoreSnapshots(monthEnd, [MakeScore(1, "111", "Alpha", "AAA", 15, 101m, monthEnd)], _ct);

        await _dbm.BulkInsertPrices([
            new PriceRow(1, 111, "AAA", "NYSE", "aaa.us", friday, 100m, 100m, 100m, 100m, 1000),
            new PriceRow(2, 111, "AAA", "NYSE", "aaa.us", monthEnd, 101m, 101m, 101m, 101m, 1000),
        ], _ct);

        var service = new GrahamBacktestService(_dbm);

        Result<GrahamBacktestReport> monthly = await service.GetBacktest(15, GrahamBacktestInterval.Monthly, GrahamBacktestPolicy.All, false, _ct);
        Assert.True(monthly.IsSuccess);
        GrahamBacktestPeriod monthlyPeriod = Assert.Single(monthly.Value!.Periods);
        Assert.Equal(monthEnd, monthlyPeriod.StartDate);

        Result<GrahamBacktestReport> weekly = await service.GetBacktest(15, GrahamBacktestInterval.Weekly, GrahamBacktestPolicy.All, false, _ct);
        Assert.True(weekly.IsSuccess);
        GrahamBacktestPeriod weeklyPeriod = Assert.Single(weekly.Value!.Periods);
        Assert.Equal(friday, weeklyPeriod.StartDate);
    }

    // Three-date scenario for trade policies:
    // A: anchor, 15/15 throughout with constant fundamentals.
    // P: qualifies at feb on price alone (same fundamentals), filing at mar confirms.
    // D: disqualifies at feb on price alone (same fundamentals), filing at mar confirms.
    private async Task SeedPolicyScenario() {
        var jan = new DateOnly(2026, 1, 31);
        var feb = new DateOnly(2026, 2, 28);
        var mar = new DateOnly(2026, 3, 31);

        _ = await _dbm.BulkInsertGrahamScoreSnapshots(jan, [
            MakeScore(1, "111", "Anchor", "AAA", 15, 100m, jan, 500m),
            MakeScore(2, "222", "PriceIn", "PPP", 14, 50m, jan, 300m),
            MakeScore(3, "333", "PriceOut", "DDD", 15, 20m, jan, 100m),
        ], _ct);
        _ = await _dbm.BulkInsertGrahamScoreSnapshots(feb, [
            MakeScore(1, "111", "Anchor", "AAA", 15, 100m, feb, 500m),
            MakeScore(2, "222", "PriceIn", "PPP", 15, 40m, feb, 300m),
            MakeScore(3, "333", "PriceOut", "DDD", 14, 25m, feb, 100m),
        ], _ct);
        _ = await _dbm.BulkInsertGrahamScoreSnapshots(mar, [
            MakeScore(1, "111", "Anchor", "AAA", 15, 100m, mar, 500m),
            MakeScore(2, "222", "PriceIn", "PPP", 15, 41m, mar, 350m),
            MakeScore(3, "333", "PriceOut", "DDD", 14, 26m, mar, 90m),
        ], _ct);
    }

    private static Dictionary<string, GrahamBacktestConstituent> ByTicker(GrahamBacktestPeriod period) {
        var map = new Dictionary<string, GrahamBacktestConstituent>();
        foreach (GrahamBacktestConstituent c in period.Constituents)
            map[c.Ticker!] = c;
        return map;
    }

    [Fact]
    public async Task GetBacktest_FilingOnlyPolicy_DefersPriceDrivenChangesUntilNextFiling() {
        await SeedPolicyScenario();
        var service = new GrahamBacktestService(_dbm);

        Result<GrahamBacktestReport> result =
            await service.GetBacktest(15, GrahamBacktestInterval.Monthly, GrahamBacktestPolicy.FilingOnly, false, _ct);
        Assert.True(result.IsSuccess);

        IReadOnlyList<GrahamBacktestPeriod> periods = result.Value!.Periods;
        Assert.Equal(3, periods.Count);

        // February: P's price-driven entry is skipped, D's price-driven exit is skipped
        Dictionary<string, GrahamBacktestConstituent> feb = ByTicker(periods[1]);
        Assert.Equal(2, periods[1].ConstituentCount);
        Assert.False(feb.ContainsKey("PPP"));
        Assert.True(feb.ContainsKey("DDD")); // still held despite disqualifying on price
        Assert.True(feb["DDD"].Left);        // sold at March when the filing confirms
        Assert.Equal("filing", feb["DDD"].LeftTrigger);

        // March: filings arrive — P is bought, D is gone
        Dictionary<string, GrahamBacktestConstituent> mar = ByTicker(periods[2]);
        Assert.Equal(2, periods[2].ConstituentCount);
        Assert.True(mar.ContainsKey("PPP"));
        Assert.True(mar["PPP"].Entered);
        Assert.Equal("filing", mar["PPP"].EnteredTrigger);
        Assert.False(mar.ContainsKey("DDD"));
    }

    [Fact]
    public async Task GetBacktest_PriceOnlyPolicy_ActsOnPriceDrivenChangesImmediately() {
        await SeedPolicyScenario();
        var service = new GrahamBacktestService(_dbm);

        Result<GrahamBacktestReport> result =
            await service.GetBacktest(15, GrahamBacktestInterval.Monthly, GrahamBacktestPolicy.PriceOnly, false, _ct);
        Assert.True(result.IsSuccess);

        IReadOnlyList<GrahamBacktestPeriod> periods = result.Value!.Periods;

        // January: D leaves at February on a price move
        Dictionary<string, GrahamBacktestConstituent> jan = ByTicker(periods[0]);
        Assert.True(jan["DDD"].Left);
        Assert.Equal("price", jan["DDD"].LeftTrigger);

        // February: P bought on its price-driven entry, D sold
        Dictionary<string, GrahamBacktestConstituent> feb = ByTicker(periods[1]);
        Assert.True(feb.ContainsKey("PPP"));
        Assert.Equal("price", feb["PPP"].EnteredTrigger);
        Assert.False(feb.ContainsKey("DDD"));
    }

    [Fact]
    public async Task GetBacktest_AllPolicy_MatchesQualifyingListEachPeriod() {
        await SeedPolicyScenario();
        var service = new GrahamBacktestService(_dbm);

        Result<GrahamBacktestReport> result =
            await service.GetBacktest(15, GrahamBacktestInterval.Monthly, GrahamBacktestPolicy.All, false, _ct);
        Assert.True(result.IsSuccess);

        IReadOnlyList<GrahamBacktestPeriod> periods = result.Value!.Periods;
        Assert.Equal(2, periods[0].ConstituentCount); // AAA, DDD
        Assert.Equal(2, periods[1].ConstituentCount); // AAA, PPP
        Assert.True(ByTicker(periods[1]).ContainsKey("PPP"));
        Assert.False(ByTicker(periods[1]).ContainsKey("DDD"));
    }

    [Fact]
    public async Task GetBacktest_ConfirmChanges_SkipsOnePeriodFlicker() {
        var jan = new DateOnly(2026, 1, 31);
        var feb = new DateOnly(2026, 2, 28);
        var mar = new DateOnly(2026, 3, 31);

        // F flickers out for one period only; G drops out and stays out; H flickers in for one period only
        _ = await _dbm.BulkInsertGrahamScoreSnapshots(jan, [
            MakeScore(1, "111", "Flicker", "FFF", 15, 29m, jan, 600m),
            MakeScore(2, "222", "Gone", "GGG", 15, 10m, jan, 200m),
            MakeScore(3, "333", "Hop", "HHH", 14, 5m, jan, 50m),
        ], _ct);
        _ = await _dbm.BulkInsertGrahamScoreSnapshots(feb, [
            MakeScore(1, "111", "Flicker", "FFF", 14, 26m, feb, 600m),
            MakeScore(2, "222", "Gone", "GGG", 14, 9m, feb, 200m),
            MakeScore(3, "333", "Hop", "HHH", 15, 6m, feb, 50m),
        ], _ct);
        _ = await _dbm.BulkInsertGrahamScoreSnapshots(mar, [
            MakeScore(1, "111", "Flicker", "FFF", 15, 28m, mar, 600m),
            MakeScore(2, "222", "Gone", "GGG", 14, 8m, mar, 200m),
            MakeScore(3, "333", "Hop", "HHH", 14, 5m, mar, 50m),
        ], _ct);

        var service = new GrahamBacktestService(_dbm);
        Result<GrahamBacktestReport> result = await service.GetBacktest(
            15, GrahamBacktestInterval.Monthly, GrahamBacktestPolicy.All, true, _ct);
        Assert.True(result.IsSuccess);

        IReadOnlyList<GrahamBacktestPeriod> periods = result.Value!.Periods;

        // FFF's one-period dip is never traded: held throughout
        Assert.True(ByTicker(periods[0]).ContainsKey("FFF"));
        Assert.True(ByTicker(periods[1]).ContainsKey("FFF"));
        Assert.True(ByTicker(periods[2]).ContainsKey("FFF"));
        Assert.False(ByTicker(periods[1])["FFF"].Left);

        // GGG's dropout persists (feb + mar): sold at March
        Assert.True(ByTicker(periods[1]).ContainsKey("GGG"));
        Assert.True(ByTicker(periods[1])["GGG"].Left);
        Assert.False(ByTicker(periods[2]).ContainsKey("GGG"));

        // HHH qualified for one period only: never bought
        Assert.False(ByTicker(periods[1]).ContainsKey("HHH"));
        Assert.False(ByTicker(periods[2]).ContainsKey("HHH"));
    }

    [Fact]
    public async Task GetBacktest_FailsWhenNoSnapshotsExist() {
        var service = new GrahamBacktestService(_dbm);
        Result<GrahamBacktestReport> result = await service.GetBacktest(15, GrahamBacktestInterval.Monthly, GrahamBacktestPolicy.All, false, _ct);
        Assert.True(result.IsFailure);
    }
}
