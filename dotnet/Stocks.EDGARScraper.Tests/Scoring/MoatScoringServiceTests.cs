using System;
using System.Collections.Generic;
using Stocks.DataModels.Scoring;
using Stocks.Persistence.Services;

namespace Stocks.EDGARScraper.Tests.Scoring;

public class MoatScoringServiceTests {

    #region Helper to build annual data

    private static Dictionary<int, IReadOnlyDictionary<string, decimal>> BuildAnnualData(
        params (int year, Dictionary<string, decimal> data)[] years) {
        var result = new Dictionary<int, IReadOnlyDictionary<string, decimal>>();
        foreach (var (year, data) in years)
            result[year] = data;
        return result;
    }

    private static Dictionary<string, decimal> EmptySnapshot() => new();

    #endregion

    #region Gross margin tests

    [Fact]
    public void ComputeMoatDerivedMetrics_GrossMargin_UsesGrossProfitDirectly() {
        var annualData = BuildAnnualData(
            (2020, new Dictionary<string, decimal> { ["GrossProfit"] = 400m, ["Revenues"] = 1000m }),
            (2021, new Dictionary<string, decimal> { ["GrossProfit"] = 500m, ["Revenues"] = 1100m }),
            (2022, new Dictionary<string, decimal> { ["GrossProfit"] = 450m, ["Revenues"] = 1050m }));

        var (metrics, _) = MoatScoringService.ComputeMoatDerivedMetrics(
            annualData, EmptySnapshot(), null, null, null);

        Assert.NotNull(metrics.AverageGrossMargin);
        // (40 + 45.45.. + 42.857..) / 3
        decimal expectedAvg = (400m / 1000m * 100m + 500m / 1100m * 100m + 450m / 1050m * 100m) / 3m;
        Assert.Equal(expectedAvg, metrics.AverageGrossMargin!.Value, 4);
    }

    [Fact]
    public void ComputeMoatDerivedMetrics_GrossMargin_DerivesFromRevenueMinusCogs() {
        var annualData = BuildAnnualData(
            (2020, new Dictionary<string, decimal> { ["Revenues"] = 1000m, ["CostOfGoodsAndServicesSold"] = 600m }),
            (2021, new Dictionary<string, decimal> { ["Revenues"] = 1200m, ["CostOfRevenue"] = 700m }));

        var (metrics, _) = MoatScoringService.ComputeMoatDerivedMetrics(
            annualData, EmptySnapshot(), null, null, null);

        Assert.NotNull(metrics.AverageGrossMargin);
        decimal expectedAvg = (400m / 1000m * 100m + 500m / 1200m * 100m) / 2m;
        Assert.Equal(expectedAvg, metrics.AverageGrossMargin!.Value, 4);
    }

    [Fact]
    public void ComputeMoatDerivedMetrics_GrossMargin_NotAvailable_WhenNoData() {
        var annualData = BuildAnnualData(
            (2020, new Dictionary<string, decimal> { ["NetIncomeLoss"] = 100m }));

        var (metrics, _) = MoatScoringService.ComputeMoatDerivedMetrics(
            annualData, EmptySnapshot(), null, null, null);

        Assert.Null(metrics.AverageGrossMargin);
    }

    #endregion

    #region Operating margin tests

    [Fact]
    public void ComputeMoatDerivedMetrics_OperatingMargin_ComputesCorrectly() {
        var annualData = BuildAnnualData(
            (2020, new Dictionary<string, decimal> { ["OperatingIncomeLoss"] = 200m, ["Revenues"] = 1000m }),
            (2021, new Dictionary<string, decimal> { ["OperatingIncomeLoss"] = 250m, ["Revenues"] = 1200m }));

        var (metrics, _) = MoatScoringService.ComputeMoatDerivedMetrics(
            annualData, EmptySnapshot(), null, null, null);

        Assert.NotNull(metrics.AverageOperatingMargin);
        decimal expectedAvg = (200m / 1000m * 100m + 250m / 1200m * 100m) / 2m;
        Assert.Equal(expectedAvg, metrics.AverageOperatingMargin!.Value, 4);
    }

    [Fact]
    public void ComputeMoatDerivedMetrics_OperatingMargin_NullWhenNoOperatingIncome() {
        var annualData = BuildAnnualData(
            (2020, new Dictionary<string, decimal> { ["Revenues"] = 1000m }));

        var (metrics, _) = MoatScoringService.ComputeMoatDerivedMetrics(
            annualData, EmptySnapshot(), null, null, null);

        Assert.Null(metrics.AverageOperatingMargin);
    }

    #endregion

    #region Revenue CAGR tests

    [Fact]
    public void ComputeMoatDerivedMetrics_RevenueCagr_ComputesOverMultipleYears() {
        // Revenue doubles from 1000 to 2000 over 4 years → CAGR = (2000/1000)^(1/4) - 1 ≈ 18.92%
        var annualData = BuildAnnualData(
            (2019, new Dictionary<string, decimal> { ["Revenues"] = 1000m }),
            (2020, new Dictionary<string, decimal> { ["Revenues"] = 1200m }),
            (2021, new Dictionary<string, decimal> { ["Revenues"] = 1500m }),
            (2022, new Dictionary<string, decimal> { ["Revenues"] = 1800m }),
            (2023, new Dictionary<string, decimal> { ["Revenues"] = 2000m }));

        var (metrics, _) = MoatScoringService.ComputeMoatDerivedMetrics(
            annualData, EmptySnapshot(), null, null, null);

        Assert.NotNull(metrics.RevenueCagr);
        decimal expected = (decimal)(Math.Pow(2000.0 / 1000.0, 1.0 / 4.0) - 1.0) * 100m;
        Assert.Equal(expected, metrics.RevenueCagr!.Value, 2);
    }

    [Fact]
    public void ComputeMoatDerivedMetrics_RevenueCagr_NullWhenLessThanTwoYears() {
        var annualData = BuildAnnualData(
            (2023, new Dictionary<string, decimal> { ["Revenues"] = 1000m }));

        var (metrics, _) = MoatScoringService.ComputeMoatDerivedMetrics(
            annualData, EmptySnapshot(), null, null, null);

        Assert.Null(metrics.RevenueCagr);
    }

    [Fact]
    public void ComputeMoatDerivedMetrics_RevenueCagr_NullWhenLatestRevenueIsNegative() {
        var annualData = BuildAnnualData(
            (2020, new Dictionary<string, decimal> { ["Revenues"] = 1000m }),
            (2023, new Dictionary<string, decimal> { ["Revenues"] = -500m }));

        var (metrics, _) = MoatScoringService.ComputeMoatDerivedMetrics(
            annualData, EmptySnapshot(), null, null, null);

        Assert.Null(metrics.RevenueCagr);
    }

    [Fact]
    public void ComputeMoatDerivedMetrics_RevenueCagr_NoOverflowOnExtremeRatio() {
        // Tiny oldest revenue with large latest revenue produces a ratio whose CAGR
        // would overflow decimal. The method should return null instead of throwing.
        var annualData = BuildAnnualData(
            (2022, new Dictionary<string, decimal> { ["Revenues"] = 0.001m }),
            (2023, new Dictionary<string, decimal> { ["Revenues"] = 999_999_999_999m }));

        var (metrics, _) = MoatScoringService.ComputeMoatDerivedMetrics(
            annualData, EmptySnapshot(), null, null, null);

        // Should not throw; CAGR may be a very large value or null depending on
        // whether the result fits in decimal, but must not throw OverflowException.
        // The important thing is no exception was thrown.
    }

    #endregion

    #region CapEx ratio tests

    [Fact]
    public void ComputeMoatDerivedMetrics_CapexRatio_ComputesCorrectly() {
        // OE = netIncome - capEx (simplified, no D&A etc.)
        // Year 1: OE = 500 - 100 = 400, CapEx = 100
        // Year 2: OE = 600 - 150 = 450, CapEx = 150
        // Avg CapEx = 125, Avg OE = 425
        // Ratio = 125/425 * 100 ≈ 29.41%
        var annualData = BuildAnnualData(
            (2020, new Dictionary<string, decimal> {
                ["NetIncomeLoss"] = 500m,
                ["PaymentsToAcquirePropertyPlantAndEquipment"] = 100m,
            }),
            (2021, new Dictionary<string, decimal> {
                ["NetIncomeLoss"] = 600m,
                ["PaymentsToAcquirePropertyPlantAndEquipment"] = 150m,
            }));

        var (metrics, _) = MoatScoringService.ComputeMoatDerivedMetrics(
            annualData, EmptySnapshot(), null, null, null);

        Assert.NotNull(metrics.CapexRatio);
        // OE per year: 500+0+0+0-100+0=400, 600+0+0+0-150+0=450
        // Avg CapEx = 125, Avg OE = 425
        decimal expectedRatio = 125m / 425m * 100m;
        Assert.Equal(expectedRatio, metrics.CapexRatio!.Value, 2);
    }

    #endregion

    #region Interest coverage tests

    [Fact]
    public void ComputeMoatDerivedMetrics_InterestCoverage_UsesMostRecentYear() {
        var annualData = BuildAnnualData(
            (2020, new Dictionary<string, decimal> {
                ["OperatingIncomeLoss"] = 100m, ["InterestExpense"] = 20m,
            }),
            (2021, new Dictionary<string, decimal> {
                ["OperatingIncomeLoss"] = 200m, ["InterestExpense"] = 25m,
            }));

        var (metrics, _) = MoatScoringService.ComputeMoatDerivedMetrics(
            annualData, EmptySnapshot(), null, null, null);

        Assert.NotNull(metrics.InterestCoverage);
        // Should use 2021: 200/25 = 8
        Assert.Equal(8.0m, metrics.InterestCoverage!.Value);
    }

    [Fact]
    public void ComputeMoatDerivedMetrics_InterestCoverage_NullWhenNoInterestExpense() {
        // Has debt but no interest tag: the coverage is unknown (a debt-free company is a separate case).
        var annualData = BuildAnnualData(
            (2021, new Dictionary<string, decimal> { ["OperatingIncomeLoss"] = 200m }));
        var snapshot = new Dictionary<string, decimal> { ["LongTermDebt"] = 500m };

        var (metrics, _) = MoatScoringService.ComputeMoatDerivedMetrics(
            annualData, snapshot, null, null, null);

        Assert.Null(metrics.InterestCoverage);
        Assert.Equal(ScoringCheckResult.NotAvailable, MoatScoringService.EvaluateMoatChecks(metrics, 8)[9].Result);
    }

    #endregion

    #region Positive OE tracking tests

    [Fact]
    public void ComputeMoatDerivedMetrics_PositiveOeYears_CountsCorrectly() {
        var annualData = BuildAnnualData(
            (2019, new Dictionary<string, decimal> { ["NetIncomeLoss"] = 500m }),   // OE positive
            (2020, new Dictionary<string, decimal> { ["NetIncomeLoss"] = -100m }),  // OE negative
            (2021, new Dictionary<string, decimal> { ["NetIncomeLoss"] = 300m }),   // OE positive
            (2022, new Dictionary<string, decimal> { ["NetIncomeLoss"] = 200m }));  // OE positive

        var (metrics, _) = MoatScoringService.ComputeMoatDerivedMetrics(
            annualData, EmptySnapshot(), null, null, null);

        Assert.Equal(4, metrics.TotalOeYears);
        Assert.Equal(3, metrics.PositiveOeYears);
    }

    #endregion

    #region Capital return tracking tests

    [Fact]
    public void ComputeMoatDerivedMetrics_CapitalReturnYears_CountsDividendsAndBuybacks() {
        var annualData = BuildAnnualData(
            (2019, new Dictionary<string, decimal> { ["PaymentsOfDividends"] = 50m }),
            (2020, new Dictionary<string, decimal> { ["PaymentsForRepurchaseOfCommonStock"] = 100m }),
            (2021, new Dictionary<string, decimal> { ["NetIncomeLoss"] = 200m }),  // no dividends or buybacks
            (2022, new Dictionary<string, decimal> {
                ["PaymentsOfDividends"] = 30m,
                ["PaymentsForRepurchaseOfCommonStock"] = 20m,
            }),
            (2023, new Dictionary<string, decimal> { ["NetIncomeLoss"] = 300m })); // no dividends or buybacks

        var (metrics, _) = MoatScoringService.ComputeMoatDerivedMetrics(
            annualData, EmptySnapshot(), null, null, null);

        Assert.Equal(5, metrics.TotalCapitalReturnYears);
        Assert.Equal(3, metrics.CapitalReturnYears);
    }

    #endregion

    #region Trend data tests

    [Fact]
    public void ComputeMoatDerivedMetrics_TrendData_ReturnsPerYearMetrics() {
        var annualData = BuildAnnualData(
            (2020, new Dictionary<string, decimal> {
                ["Revenues"] = 1000m, ["GrossProfit"] = 400m, ["OperatingIncomeLoss"] = 150m,
            }),
            (2021, new Dictionary<string, decimal> {
                ["Revenues"] = 1200m, ["GrossProfit"] = 500m, ["OperatingIncomeLoss"] = 200m,
            }),
            (2022, new Dictionary<string, decimal> {
                ["Revenues"] = 1400m, ["GrossProfit"] = 600m,
            }));

        var (_, trendData) = MoatScoringService.ComputeMoatDerivedMetrics(
            annualData, EmptySnapshot(), null, null, null);

        Assert.Equal(3, trendData.Count);
        Assert.Equal(2020, trendData[0].Year);
        Assert.Equal(2021, trendData[1].Year);
        Assert.Equal(2022, trendData[2].Year);

        // Year 2020: gross margin = 400/1000*100 = 40%
        Assert.Equal(40.0m, trendData[0].GrossMarginPct);
        Assert.Equal(1000m, trendData[0].Revenue);

        // Year 2022: has gross margin but no operating margin (no OperatingIncomeLoss)
        Assert.NotNull(trendData[2].GrossMarginPct);
        Assert.Null(trendData[2].OperatingMarginPct);
    }

    #endregion

    #region Empty data tests

    [Fact]
    public void ComputeMoatDerivedMetrics_EmptyData_ReturnsAllNulls() {
        var annualData = new Dictionary<int, IReadOnlyDictionary<string, decimal>>();

        var (metrics, trendData) = MoatScoringService.ComputeMoatDerivedMetrics(
            annualData, EmptySnapshot(), null, null, null);

        Assert.Null(metrics.AverageGrossMargin);
        Assert.Null(metrics.AverageOperatingMargin);
        Assert.Null(metrics.AverageRoeCF);
        Assert.Null(metrics.AverageRoeOE);
        Assert.Null(metrics.RevenueCagr);
        Assert.Null(metrics.CapexRatio);
        Assert.Null(metrics.InterestCoverage);
        Assert.Equal(0, metrics.PositiveOeYears);
        Assert.Equal(0, metrics.TotalOeYears);
        Assert.Empty(trendData);
    }

    #endregion

    #region Estimated return tests

    [Fact]
    public void ComputeMoatDerivedMetrics_EstimatedReturnOE_ComputesCorrectly() {
        // OE = 500 - 0 = 500 (year 1), 600 - 0 = 600 (year 2)
        // Avg OE = 550
        // Dividends (most recent year) = 50
        // Market cap = 100 * 100 = 10000
        // Est return = 100 * (550 - 50) / 10000 = 5%
        var annualData = BuildAnnualData(
            (2021, new Dictionary<string, decimal> { ["NetIncomeLoss"] = 500m }),
            (2022, new Dictionary<string, decimal> { ["NetIncomeLoss"] = 600m, ["PaymentsOfDividends"] = 50m }));

        var (metrics, _) = MoatScoringService.ComputeMoatDerivedMetrics(
            annualData, EmptySnapshot(), null, 100m, 100);

        Assert.NotNull(metrics.EstimatedReturnOE);
        Assert.Equal(10000m, metrics.MarketCap);
        // Avg OE = (500 + 600) / 2 = 550
        // Est return = 100 * (550 - 50) / 10000 = 5
        Assert.Equal(5.0m, metrics.EstimatedReturnOE!.Value);
    }

    #endregion

    #region Debt-to-equity tests

    [Fact]
    public void ComputeMoatDerivedMetrics_DebtToEquity_UsesSnapshot() {
        var snapshot = new Dictionary<string, decimal> {
            ["LongTermDebt"] = 500m,
            ["LiabilitiesAndStockholdersEquity"] = 2000m,
            ["Liabilities"] = 1000m,
        };
        // Equity = 2000 - 1000 = 1000, D/E = 500/1000 = 0.5
        var annualData = BuildAnnualData(
            (2022, new Dictionary<string, decimal> { ["NetIncomeLoss"] = 100m }));

        var (metrics, _) = MoatScoringService.ComputeMoatDerivedMetrics(
            annualData, snapshot, null, null, null);

        Assert.NotNull(metrics.DebtToEquityRatio);
        Assert.Equal(0.5m, metrics.DebtToEquityRatio!.Value);
    }

    #endregion

    #region Helper to build MoatDerivedMetrics for check tests

    private static MoatDerivedMetrics MakeMetrics(
        decimal? averageGrossMargin = null,
        decimal? averageOperatingMargin = null,
        decimal? averageRoeCF = null,
        decimal? averageRoeOE = null,
        decimal? revenueCagr = null,
        decimal? capexRatio = null,
        decimal? interestCoverage = null,
        decimal? debtToEquityRatio = null,
        decimal? estimatedReturnOE = null,
        decimal? currentDividendsPaid = null,
        decimal? marketCap = null,
        decimal? pricePerShare = null,
        int positiveOeYears = 0,
        int totalOeYears = 0,
        int capitalReturnYears = 0,
        int totalCapitalReturnYears = 0) {
        return new MoatDerivedMetrics(
            averageGrossMargin, averageOperatingMargin, averageRoeCF, averageRoeOE,
            revenueCagr, capexRatio, interestCoverage, debtToEquityRatio,
            estimatedReturnOE, currentDividendsPaid, marketCap, pricePerShare,
            positiveOeYears, totalOeYears, capitalReturnYears, totalCapitalReturnYears);
    }

    #endregion

    #region Check evaluation tests

    [Fact]
    public void EvaluateMoatChecks_ReturnsAll13Checks() {
        var metrics = MakeMetrics();
        IReadOnlyList<ScoringCheck> checks = MoatScoringService.EvaluateMoatChecks(metrics, 0);
        Assert.Equal(13, checks.Count);
        for (int i = 0; i < 13; i++)
            Assert.Equal(i + 1, checks[i].CheckNumber);
    }

    [Fact]
    public void EvaluateMoatChecks_NullMetrics_ProduceNotAvailable() {
        var metrics = MakeMetrics();
        IReadOnlyList<ScoringCheck> checks = MoatScoringService.EvaluateMoatChecks(metrics, 8);
        int[] nullMetricCheckIndices = { 0, 1, 2, 3, 4, 6, 8, 9, 11, 12 };
        foreach (int idx in nullMetricCheckIndices)
            Assert.Equal(ScoringCheckResult.NotAvailable, checks[idx].Result);
    }

    [Theory]
    [InlineData(20.0, ScoringCheckResult.Pass)]
    [InlineData(10.0, ScoringCheckResult.Fail)]
    public void EvaluateMoatChecks_Check1_RoeCF(double value, ScoringCheckResult expected) {
        var metrics = MakeMetrics(averageRoeCF: (decimal)value);
        IReadOnlyList<ScoringCheck> checks = MoatScoringService.EvaluateMoatChecks(metrics, 8);
        Assert.Equal(expected, checks[0].Result);
    }

    [Theory]
    [InlineData(20.0, ScoringCheckResult.Pass)]
    [InlineData(10.0, ScoringCheckResult.Fail)]
    public void EvaluateMoatChecks_Check2_RoeOE(double value, ScoringCheckResult expected) {
        var metrics = MakeMetrics(averageRoeOE: (decimal)value);
        IReadOnlyList<ScoringCheck> checks = MoatScoringService.EvaluateMoatChecks(metrics, 8);
        Assert.Equal(expected, checks[1].Result);
    }

    [Theory]
    [InlineData(45.0, ScoringCheckResult.Pass)]
    [InlineData(30.0, ScoringCheckResult.Fail)]
    public void EvaluateMoatChecks_Check3_GrossMargin(double value, ScoringCheckResult expected) {
        var metrics = MakeMetrics(averageGrossMargin: (decimal)value);
        IReadOnlyList<ScoringCheck> checks = MoatScoringService.EvaluateMoatChecks(metrics, 8);
        Assert.Equal(expected, checks[2].Result);
    }

    [Theory]
    [InlineData(20.0, ScoringCheckResult.Pass)]
    [InlineData(10.0, ScoringCheckResult.Fail)]
    public void EvaluateMoatChecks_Check4_OperatingMargin(double value, ScoringCheckResult expected) {
        var metrics = MakeMetrics(averageOperatingMargin: (decimal)value);
        IReadOnlyList<ScoringCheck> checks = MoatScoringService.EvaluateMoatChecks(metrics, 8);
        Assert.Equal(expected, checks[3].Result);
    }

    [Theory]
    [InlineData(5.0, ScoringCheckResult.Pass)]
    [InlineData(2.0, ScoringCheckResult.Fail)]
    public void EvaluateMoatChecks_Check5_RevenueCagr(double value, ScoringCheckResult expected) {
        var metrics = MakeMetrics(revenueCagr: (decimal)value);
        IReadOnlyList<ScoringCheck> checks = MoatScoringService.EvaluateMoatChecks(metrics, 8);
        Assert.Equal(expected, checks[4].Result);
    }

    [Theory]
    [InlineData(5, 5, ScoringCheckResult.Pass, 0)]
    [InlineData(4, 5, ScoringCheckResult.Fail, 1)]
    public void EvaluateMoatChecks_Check6_PositiveOe(int positiveYears, int totalYears,
        ScoringCheckResult expected, int expectedFailingYears) {
        var metrics = MakeMetrics(positiveOeYears: positiveYears, totalOeYears: totalYears);
        IReadOnlyList<ScoringCheck> checks = MoatScoringService.EvaluateMoatChecks(metrics, 8);
        Assert.Equal(expected, checks[5].Result);
        Assert.Equal((decimal)expectedFailingYears, checks[5].ComputedValue);
    }

    [Theory]
    [InlineData(30.0, ScoringCheckResult.Pass)]
    [InlineData(60.0, ScoringCheckResult.Fail)]
    public void EvaluateMoatChecks_Check7_CapexRatio(double value, ScoringCheckResult expected) {
        var metrics = MakeMetrics(capexRatio: (decimal)value);
        IReadOnlyList<ScoringCheck> checks = MoatScoringService.EvaluateMoatChecks(metrics, 8);
        Assert.Equal(expected, checks[6].Result);
    }

    [Theory]
    [InlineData(4, 5, ScoringCheckResult.Pass)]
    [InlineData(3, 5, ScoringCheckResult.Fail)]
    public void EvaluateMoatChecks_Check8_ConsistentReturn(int returnYears, int totalYears,
        ScoringCheckResult expected) {
        var metrics = MakeMetrics(capitalReturnYears: returnYears, totalCapitalReturnYears: totalYears);
        IReadOnlyList<ScoringCheck> checks = MoatScoringService.EvaluateMoatChecks(metrics, 8);
        Assert.Equal(expected, checks[7].Result);
    }

    [Theory]
    [InlineData(0.3, ScoringCheckResult.Pass)]
    [InlineData(1.5, ScoringCheckResult.Fail)]
    public void EvaluateMoatChecks_Check9_DebtToEquity(double value, ScoringCheckResult expected) {
        var metrics = MakeMetrics(debtToEquityRatio: (decimal)value);
        IReadOnlyList<ScoringCheck> checks = MoatScoringService.EvaluateMoatChecks(metrics, 8);
        Assert.Equal(expected, checks[8].Result);
    }

    [Theory]
    [InlineData(8.0, ScoringCheckResult.Pass)]
    [InlineData(3.0, ScoringCheckResult.Fail)]
    public void EvaluateMoatChecks_Check10_InterestCoverage(double value, ScoringCheckResult expected) {
        var metrics = MakeMetrics(interestCoverage: (decimal)value);
        IReadOnlyList<ScoringCheck> checks = MoatScoringService.EvaluateMoatChecks(metrics, 8);
        Assert.Equal(expected, checks[9].Result);
    }

    [Theory]
    [InlineData(7, ScoringCheckResult.Pass)]
    [InlineData(5, ScoringCheckResult.Fail)]
    public void EvaluateMoatChecks_Check11_History(int yearsOfData, ScoringCheckResult expected) {
        var metrics = MakeMetrics();
        IReadOnlyList<ScoringCheck> checks = MoatScoringService.EvaluateMoatChecks(metrics, yearsOfData);
        Assert.Equal(expected, checks[10].Result);
    }

    [Theory]
    [InlineData(5.0, ScoringCheckResult.Pass)]
    [InlineData(2.0, ScoringCheckResult.Fail)]
    public void EvaluateMoatChecks_Check12_EstReturnFloor(double value, ScoringCheckResult expected) {
        var metrics = MakeMetrics(estimatedReturnOE: (decimal)value);
        IReadOnlyList<ScoringCheck> checks = MoatScoringService.EvaluateMoatChecks(metrics, 8);
        Assert.Equal(expected, checks[11].Result);
    }

    [Theory]
    [InlineData(10.0, ScoringCheckResult.Pass)]
    [InlineData(50.0, ScoringCheckResult.Fail)]
    public void EvaluateMoatChecks_Check13_EstReturnCap(double value, ScoringCheckResult expected) {
        var metrics = MakeMetrics(estimatedReturnOE: (decimal)value);
        IReadOnlyList<ScoringCheck> checks = MoatScoringService.EvaluateMoatChecks(metrics, 8);
        Assert.Equal(expected, checks[12].Result);
    }

    #endregion

    #region Sign rule (P1) tests

    private static ScoringCheckResult MoatCheckResult(IReadOnlyList<ScoringCheck> checks, int number) {
        foreach (ScoringCheck check in checks) {
            if (check.CheckNumber == number)
                return check.Result;
        }
        throw new InvalidOperationException($"No check {number}");
    }

    private static IReadOnlyList<ScoringCheck> EvaluateMoatSingleYear(
        Dictionary<string, decimal> data, out MoatDerivedMetrics metrics) {
        var annualData = BuildAnnualData((2024, data));
        (metrics, _) = MoatScoringService.ComputeMoatDerivedMetrics(annualData, data, null, null, null);
        return MoatScoringService.EvaluateMoatChecks(metrics, 8);
    }

    [Fact]
    public void SignRule_Moat_NegativeEquity_DebtToEquityAndRoeFail() {
        var data = new Dictionary<string, decimal> {
            ["StockholdersEquity"] = -100m, ["LongTermDebt"] = 500m,
            ["CashAndCashEquivalentsPeriodIncreaseDecrease"] = -50m, ["NetIncomeLoss"] = -50m,
        };
        IReadOnlyList<ScoringCheck> checks = EvaluateMoatSingleYear(data, out MoatDerivedMetrics metrics);

        Assert.Equal(-5m, metrics.DebtToEquityRatio);
        Assert.Equal(50m, metrics.AverageRoeCF);
        Assert.Equal(ScoringCheckResult.Fail, MoatCheckResult(checks, 9));
        Assert.Equal(ScoringCheckResult.Fail, MoatCheckResult(checks, 1));
        Assert.Equal(ScoringCheckResult.Fail, MoatCheckResult(checks, 2));
    }

    [Fact]
    public void SignRule_Moat_ZeroEquity_DebtToEquityFails() {
        var data = new Dictionary<string, decimal> { ["StockholdersEquity"] = 0m, ["LongTermDebt"] = 500m };
        IReadOnlyList<ScoringCheck> checks = EvaluateMoatSingleYear(data, out _);
        Assert.Equal(ScoringCheckResult.Fail, MoatCheckResult(checks, 9));
    }

    [Fact]
    public void SignRule_Moat_PositiveEquity_DebtToEquityAndRoeUnchanged() {
        var data = new Dictionary<string, decimal> {
            ["StockholdersEquity"] = 100m, ["LongTermDebt"] = 50m,
            ["CashAndCashEquivalentsPeriodIncreaseDecrease"] = 50m, ["NetIncomeLoss"] = 50m,
        };
        IReadOnlyList<ScoringCheck> checks = EvaluateMoatSingleYear(data, out _);

        Assert.Equal(ScoringCheckResult.Pass, MoatCheckResult(checks, 9));
        Assert.Equal(ScoringCheckResult.Pass, MoatCheckResult(checks, 1));
        Assert.Equal(ScoringCheckResult.Pass, MoatCheckResult(checks, 2));
    }

    [Fact]
    public void SignRule_Moat_NegativeOwnerEarnings_CapexRatioFails() {
        var data = new Dictionary<string, decimal> {
            ["NetIncomeLoss"] = -100m, ["PaymentsToAcquirePropertyPlantAndEquipment"] = 50m,
        };
        IReadOnlyList<ScoringCheck> checks = EvaluateMoatSingleYear(data, out MoatDerivedMetrics metrics);

        Assert.True(metrics.CapexRatio < 0m);
        Assert.Equal(ScoringCheckResult.Fail, MoatCheckResult(checks, 7));
    }

    [Fact]
    public void SignRule_Moat_ZeroOwnerEarnings_CapexRatioFails() {
        var data = new Dictionary<string, decimal> {
            ["NetIncomeLoss"] = 50m, ["PaymentsToAcquirePropertyPlantAndEquipment"] = 50m,
        };
        IReadOnlyList<ScoringCheck> checks = EvaluateMoatSingleYear(data, out MoatDerivedMetrics metrics);

        Assert.Null(metrics.CapexRatio);
        Assert.Equal(ScoringCheckResult.Fail, MoatCheckResult(checks, 7));
    }

    [Fact]
    public void SignRule_Moat_PositiveOwnerEarnings_CapexRatioUnchanged() {
        var data = new Dictionary<string, decimal> {
            ["NetIncomeLoss"] = 100m, ["PaymentsToAcquirePropertyPlantAndEquipment"] = 20m,
        };
        IReadOnlyList<ScoringCheck> checks = EvaluateMoatSingleYear(data, out _);
        Assert.Equal(ScoringCheckResult.Pass, MoatCheckResult(checks, 7));
    }

    #endregion

    #region Interest coverage for debt-free companies (P4) tests

    private static ScoringCheckResult InterestCoverageCheck(
        Dictionary<string, decimal> latestYear, Dictionary<string, decimal> snapshot,
        out MoatDerivedMetrics metrics) {
        var annualData = BuildAnnualData((2024, latestYear));
        (metrics, _) = MoatScoringService.ComputeMoatDerivedMetrics(annualData, snapshot, null, null, null);
        return MoatCheckResult(MoatScoringService.EvaluateMoatChecks(metrics, 8), 10);
    }

    [Fact]
    public void InterestCoverage_DebtFreeProfitable_Passes() {
        var year = new Dictionary<string, decimal> { ["OperatingIncomeLoss"] = 100m };
        var snapshot = new Dictionary<string, decimal> { ["StockholdersEquity"] = 500m };

        ScoringCheckResult result = InterestCoverageCheck(year, snapshot, out MoatDerivedMetrics metrics);

        Assert.True(metrics.DebtFree);
        Assert.Equal(100m, metrics.LatestOperatingIncome);
        Assert.Null(metrics.InterestCoverage);
        Assert.Equal(ScoringCheckResult.Pass, result);
    }

    [Fact]
    public void InterestCoverage_DebtFreeLossMaker_Fails() {
        var year = new Dictionary<string, decimal> { ["OperatingIncomeLoss"] = -100m };
        var snapshot = new Dictionary<string, decimal> { ["StockholdersEquity"] = 500m };
        Assert.Equal(ScoringCheckResult.Fail, InterestCoverageCheck(year, snapshot, out _));
    }

    [Fact]
    public void InterestCoverage_DebtFreeZeroOperatingIncome_Fails() {
        var year = new Dictionary<string, decimal> { ["OperatingIncomeLoss"] = 0m };
        var snapshot = new Dictionary<string, decimal> { ["StockholdersEquity"] = 500m };
        Assert.Equal(ScoringCheckResult.Fail, InterestCoverageCheck(year, snapshot, out _));
    }

    [Fact]
    public void InterestCoverage_DebtButNoInterestTag_NotAvailable() {
        var year = new Dictionary<string, decimal> { ["OperatingIncomeLoss"] = 100m };
        var snapshot = new Dictionary<string, decimal> { ["LongTermDebt"] = 300m };

        ScoringCheckResult result = InterestCoverageCheck(year, snapshot, out MoatDerivedMetrics metrics);

        Assert.False(metrics.DebtFree);
        Assert.Equal(ScoringCheckResult.NotAvailable, result);
    }

    [Fact]
    public void InterestCoverage_NegativeOperatingIncomeWithInterest_Fails() {
        var year = new Dictionary<string, decimal> { ["OperatingIncomeLoss"] = -100m, ["InterestExpense"] = 10m };
        Assert.Equal(ScoringCheckResult.Fail, InterestCoverageCheck(year, new Dictionary<string, decimal>(), out _));
    }

    [Theory]
    [InlineData("LongTermDebtCurrent")]
    [InlineData("DebtCurrent")]
    [InlineData("ShortTermBorrowings")]
    [InlineData("CommercialPaper")]
    [InlineData("LinesOfCreditCurrent")]
    [InlineData("LongTermDebtAndCapitalLeaseObligationsCurrent")]
    [InlineData("LineOfCredit")]
    [InlineData("LongTermLineOfCredit")]
    [InlineData("NotesPayable")]
    [InlineData("ConvertibleNotesPayable")]
    [InlineData("SeniorNotes")]
    [InlineData("DebtLongtermAndShorttermCombinedAmount")]
    [InlineData("LongTermDebtAndCapitalLeaseObligationsIncludingCurrentMaturities")]
    public void InterestCoverage_AnyOtherDebtConcept_BlocksDebtFree(string debtConcept) {
        var year = new Dictionary<string, decimal> { ["OperatingIncomeLoss"] = 100m };
        var snapshot = new Dictionary<string, decimal> { [debtConcept] = 50m };

        ScoringCheckResult result = InterestCoverageCheck(year, snapshot, out MoatDerivedMetrics metrics);

        Assert.False(metrics.DebtFree);
        Assert.Equal(ScoringCheckResult.NotAvailable, result);
    }

    [Fact]
    public void InterestCoverage_ZeroDebtTag_CountsAsDebtFree() {
        var year = new Dictionary<string, decimal> { ["OperatingIncomeLoss"] = 100m };
        var snapshot = new Dictionary<string, decimal> { ["LongTermDebt"] = 0m, ["DebtCurrent"] = 0m };
        Assert.Equal(ScoringCheckResult.Pass, InterestCoverageCheck(year, snapshot, out _));
    }

    [Fact]
    public void InterestCoverage_SparseTenQWithTenKDebt_NotDebtFree() {
        // CAT-shaped: after the latest-10-K fallback the snapshot carries the 10-K debt.
        var values = new List<ScoringConceptValue> {
            new("StockholdersEquity", 1000m, new DateOnly(2025, 12, 31), 1, 1),
            new("LongTermDebtNoncurrent", 3000m, new DateOnly(2025, 12, 31), 1, 1),
            new("OperatingIncomeLoss", 400m, new DateOnly(2025, 12, 31), 1, 1),
            new("StockholdersEquity", 1100m, new DateOnly(2026, 6, 30), 1, 2),
        };
        ScoringService.GroupedScoringData grouped = ScoringService.GroupAndPartitionData(values);
        var annual = new Dictionary<int, IReadOnlyDictionary<string, decimal>>();
        foreach (KeyValuePair<int, Dictionary<string, decimal>> entry in grouped.AnnualByYear)
            annual[entry.Key] = entry.Value;

        var (metrics, _) = MoatScoringService.ComputeMoatDerivedMetrics(
            annual, grouped.MostRecentSnapshot, null, null, null, grouped.BalanceTypes);

        Assert.False(metrics.DebtFree);
        Assert.Equal(ScoringCheckResult.NotAvailable,
            MoatCheckResult(MoatScoringService.EvaluateMoatChecks(metrics, 8), 10));
    }

    [Fact]
    public void LatestTenKFallback_TenQWithOnlyCreditLine_StillGetsTenKLongTermDebt() {
        // AMN-shaped: the 10-Q tags only LongTermLineOfCredit; the 10-K's LongTermDebt must still fill.
        var values = new List<ScoringConceptValue> {
            new("StockholdersEquity", 1000m, new DateOnly(2025, 12, 31), 1, 1),
            new("LongTermDebt", 1056m, new DateOnly(2025, 12, 31), 1, 1),
            new("LongTermLineOfCredit", 25m, new DateOnly(2025, 12, 31), 1, 1),
            new("StockholdersEquity", 1000m, new DateOnly(2026, 6, 30), 1, 2),
            new("LongTermLineOfCredit", 0m, new DateOnly(2026, 6, 30), 1, 2),
        };
        ScoringService.GroupedScoringData grouped = ScoringService.GroupAndPartitionData(values);

        Assert.Equal(1056m, grouped.MostRecentSnapshot["LongTermDebt"]);
        Assert.Equal(0m, grouped.MostRecentSnapshot["LongTermLineOfCredit"]);
    }

    [Fact]
    public void InterestCoverage_SparseTenQWithTenKCurrentDebtOnly_NotDebtFree() {
        // The debt group copied from the 10-K includes the current/short-term debt concepts.
        var values = new List<ScoringConceptValue> {
            new("StockholdersEquity", 1000m, new DateOnly(2025, 12, 31), 1, 1),
            new("ShortTermBorrowings", 200m, new DateOnly(2025, 12, 31), 1, 1),
            new("StockholdersEquity", 1100m, new DateOnly(2026, 6, 30), 1, 2),
        };
        ScoringService.GroupedScoringData grouped = ScoringService.GroupAndPartitionData(values);
        Assert.Equal(200m, grouped.MostRecentSnapshot["ShortTermBorrowings"]);
    }

    #endregion
}
