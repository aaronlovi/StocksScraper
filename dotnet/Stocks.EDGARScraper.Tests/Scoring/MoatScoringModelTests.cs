using System;
using System.Collections.Generic;
using Stocks.DataModels.Scoring;

namespace Stocks.EDGARScraper.Tests.Scoring;

public class MoatScoringModelTests {
    [Fact]
    public void MoatDerivedMetrics_AllNulls_CanBeConstructed() {
        var metrics = new MoatDerivedMetrics(
            AverageGrossMargin: null,
            AverageOperatingMargin: null,
            AverageRoeCF: null,
            AverageRoeOE: null,
            RevenueCagr: null,
            CapexRatio: null,
            InterestCoverage: null,
            DebtToEquityRatio: null,
            EstimatedReturnOE: null,
            CurrentDividendsPaid: null,
            MarketCap: null,
            PricePerShare: null,
            PositiveOeYears: 0,
            TotalOeYears: 0,
            CapitalReturnYears: 0,
            TotalCapitalReturnYears: 0);

        Assert.Null(metrics.AverageGrossMargin);
        Assert.Null(metrics.AverageOperatingMargin);
        Assert.Null(metrics.AverageRoeCF);
        Assert.Null(metrics.AverageRoeOE);
        Assert.Null(metrics.RevenueCagr);
        Assert.Null(metrics.CapexRatio);
        Assert.Null(metrics.InterestCoverage);
        Assert.Null(metrics.DebtToEquityRatio);
        Assert.Null(metrics.EstimatedReturnOE);
        Assert.Null(metrics.CurrentDividendsPaid);
        Assert.Null(metrics.MarketCap);
        Assert.Null(metrics.PricePerShare);
        Assert.Equal(0, metrics.PositiveOeYears);
        Assert.Equal(0, metrics.TotalOeYears);
        Assert.Equal(0, metrics.CapitalReturnYears);
        Assert.Equal(0, metrics.TotalCapitalReturnYears);
    }

    [Fact]
    public void MoatScoringResult_CountsPassingChecks() {
        var scorecard = new List<ScoringCheck> {
            new(1, "High ROE (CF) avg", 20.0m, ">= 15%", ScoringCheckResult.Pass),
            new(2, "High ROE (OE) avg", 10.0m, ">= 15%", ScoringCheckResult.Fail),
            new(3, "Gross margin avg", null, ">= 40%", ScoringCheckResult.NotAvailable),
            new(4, "Operating margin avg", 18.0m, ">= 15%", ScoringCheckResult.Pass),
            new(5, "Revenue growth", 5.0m, "> 3%", ScoringCheckResult.Pass),
            new(6, "Positive OE every year", 0m, "0 failing years", ScoringCheckResult.Pass),
            new(7, "Low capex ratio", 30.0m, "< 50%", ScoringCheckResult.Pass),
            new(8, "Consistent dividend/buyback", 80.0m, ">= 75% of years", ScoringCheckResult.Pass),
            new(9, "Debt-to-equity", 0.8m, "< 1.0", ScoringCheckResult.Pass),
            new(10, "Interest coverage", 8.0m, "> 5x", ScoringCheckResult.Pass),
            new(11, "History", 7m, ">= 7 years", ScoringCheckResult.Pass),
            new(12, "Estimated return (OE) floor", 5.0m, "> 3%", ScoringCheckResult.Pass),
            new(13, "Estimated return (OE) cap", 5.0m, "< 40%", ScoringCheckResult.Pass),
        };

        int overallScore = 0;
        int computableChecks = 0;
        foreach (ScoringCheck check in scorecard) {
            if (check.Result != ScoringCheckResult.NotAvailable) {
                computableChecks++;
                if (check.Result == ScoringCheckResult.Pass)
                    overallScore++;
            }
        }

        var metrics = new MoatDerivedMetrics(
            45.0m, 18.0m, 20.0m, 10.0m, 5.0m, 30.0m, 8.0m, 0.8m, 5.0m, 100_000m,
            1_000_000_000m, 150.0m, 5, 5, 4, 5);

        var trendData = new List<MoatYearMetrics> {
            new(2020, 44.0m, 17.0m, 19.0m, 9.0m, 500_000_000m),
            new(2021, 46.0m, 19.0m, 21.0m, 11.0m, 520_000_000m),
        };

        var result = new MoatScoringResult(
            RawDataByYear: new Dictionary<int, IReadOnlyDictionary<string, decimal>>(),
            Metrics: metrics,
            Scorecard: scorecard,
            TrendData: trendData,
            OverallScore: overallScore,
            ComputableChecks: computableChecks,
            YearsOfData: 7,
            PricePerShare: 150.0m,
            PriceDate: new DateOnly(2025, 1, 15),
            SharesOutstanding: 1_000_000);

        Assert.Equal(11, result.OverallScore);
        Assert.Equal(12, result.ComputableChecks);
        Assert.True(result.OverallScore <= result.ComputableChecks);
        Assert.True(result.ComputableChecks <= 13);
        Assert.Equal(2, result.TrendData.Count);
    }

    [Fact]
    public void CompanyMoatScoreSummary_CanBeConstructed() {
        var summary = new CompanyMoatScoreSummary(
            CompanyId: 1,
            Cik: "320193",
            CompanyName: "APPLE INC",
            Ticker: "AAPL",
            Exchange: "NASDAQ",
            OverallScore: 10,
            ComputableChecks: 13,
            YearsOfData: 8,
            AverageGrossMargin: 43.0m,
            AverageOperatingMargin: 30.0m,
            AverageRoeCF: 25.0m,
            AverageRoeOE: 22.0m,
            EstimatedReturnOE: 8.0m,
            RevenueCagr: 7.5m,
            CapexRatio: 15.0m,
            InterestCoverage: 20.0m,
            DebtToEquityRatio: 0.6m,
            PricePerShare: 230.0m,
            PriceDate: new DateOnly(2025, 6, 1),
            SharesOutstanding: 15_000_000_000,
            Return1y: null,
            ComputedAt: new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc));

        Assert.Equal(1UL, summary.CompanyId);
        Assert.Equal("320193", summary.Cik);
        Assert.Equal("APPLE INC", summary.CompanyName);
        Assert.Equal(10, summary.OverallScore);
        Assert.Equal(13, summary.ComputableChecks);
        Assert.Equal(43.0m, summary.AverageGrossMargin);
        Assert.Equal(7.5m, summary.RevenueCagr);
    }

    [Fact]
    public void MoatYearMetrics_CanBeConstructed() {
        var yearMetrics = new MoatYearMetrics(
            Year: 2023,
            GrossMarginPct: 44.5m,
            OperatingMarginPct: 30.2m,
            RoeCfPct: 25.1m,
            RoeOePct: 22.3m,
            Revenue: 394_328_000_000m);

        Assert.Equal(2023, yearMetrics.Year);
        Assert.Equal(44.5m, yearMetrics.GrossMarginPct);
        Assert.Equal(30.2m, yearMetrics.OperatingMarginPct);
        Assert.Equal(25.1m, yearMetrics.RoeCfPct);
        Assert.Equal(22.3m, yearMetrics.RoeOePct);
        Assert.Equal(394_328_000_000m, yearMetrics.Revenue);
    }

    [Fact]
    public void MoatYearMetrics_AllNulls_CanBeConstructed() {
        var yearMetrics = new MoatYearMetrics(
            Year: 2020,
            GrossMarginPct: null,
            OperatingMarginPct: null,
            RoeCfPct: null,
            RoeOePct: null,
            Revenue: null);

        Assert.Equal(2020, yearMetrics.Year);
        Assert.Null(yearMetrics.GrossMarginPct);
        Assert.Null(yearMetrics.OperatingMarginPct);
        Assert.Null(yearMetrics.RoeCfPct);
        Assert.Null(yearMetrics.RoeOePct);
        Assert.Null(yearMetrics.Revenue);
    }
}
