using System;
using System.Collections.Generic;
using Stocks.DataModels.Scoring;
using Stocks.Persistence.Services;

namespace Stocks.EDGARScraper.Tests.Scoring;

/// <summary>E4: debt = long-term chain + current portion (guarded) + short-term borrowings.</summary>
public class TotalDebtTests {
    [Fact]
    public void ResolveTotalDebt_AddsCurrentPortionAndShortTermToNoncurrentFigure() {
        var snapshot = new Dictionary<string, decimal> {
            ["LongTermDebtNoncurrent"] = 1000m,
            ["LongTermDebtCurrent"] = 100m,
            ["ShortTermBorrowings"] = 50m,
        };
        Assert.Equal(1150m, ScoringService.ResolveTotalDebt(snapshot));
    }

    [Fact]
    public void ResolveTotalDebt_AddsCurrentPortionToNoncurrentDebtAndLeases() {
        var snapshot = new Dictionary<string, decimal> {
            ["LongTermDebtAndCapitalLeaseObligations"] = 800m,
            ["LongTermDebtCurrent"] = 100m,
        };
        Assert.Equal(900m, ScoringService.ResolveTotalDebt(snapshot));
    }

    [Fact]
    public void ResolveTotalDebt_LongTermDebtCoveringNoncurrentPlusCurrent_SkipsCurrent() {
        var snapshot = new Dictionary<string, decimal> {
            ["LongTermDebt"] = 1100m,
            ["LongTermDebtNoncurrent"] = 1000m,
            ["LongTermDebtCurrent"] = 100m,
        };
        Assert.Equal(1100m, ScoringService.ResolveTotalDebt(snapshot));
    }

    [Fact]
    public void ResolveTotalDebt_LongTermDebtEqualToNoncurrent_AddsCurrent() {
        var snapshot = new Dictionary<string, decimal> {
            ["LongTermDebt"] = 1000m,
            ["LongTermDebtNoncurrent"] = 1000m,
            ["LongTermDebtCurrent"] = 100m,
        };
        Assert.Equal(1100m, ScoringService.ResolveTotalDebt(snapshot));
    }

    [Fact]
    public void ResolveTotalDebt_LongTermDebtAlone_TreatedAsTotalPerTaxonomy() {
        var snapshot = new Dictionary<string, decimal> {
            ["LongTermDebt"] = 1100m,
            ["LongTermDebtCurrent"] = 100m,
            ["ShortTermBorrowings"] = 50m,
        };
        Assert.Equal(1150m, ScoringService.ResolveTotalDebt(snapshot));
    }

    [Fact]
    public void ResolveTotalDebt_OnlyShortTermDebt_CountsIt() {
        var snapshot = new Dictionary<string, decimal> {
            ["LongTermDebtCurrent"] = 40m,
            ["ShortTermBorrowings"] = 60m,
        };
        Assert.Equal(100m, ScoringService.ResolveTotalDebt(snapshot));
    }

    [Fact]
    public void ResolveTotalDebt_NoDebtTags_IsZero() {
        Assert.Equal(0m, ScoringService.ResolveTotalDebt(new Dictionary<string, decimal>()));
    }

    [Fact]
    public void ComputeDerivedMetrics_ShortTermOnlyDebt_GivesNonZeroDebtRatios() {
        var annualData = new Dictionary<int, IReadOnlyDictionary<string, decimal>> {
            [2024] = new Dictionary<string, decimal> {
                ["StockholdersEquity"] = 1000m,
                ["NetIncomeLoss"] = 100m,
            },
        };
        var snapshot = new Dictionary<string, decimal> {
            ["StockholdersEquity"] = 1000m,
            ["ShortTermBorrowings"] = 600m,
        };

        DerivedMetrics metrics = ScoringService.ComputeDerivedMetrics(annualData, snapshot, null, null, null);

        Assert.Equal(0.6m, metrics.DebtToEquityRatio);
        Assert.Equal(0.6m, metrics.DebtToBookRatio);
    }

    [Fact]
    public void ComputeMoatDerivedMetrics_DebtToEquity_IncludesCurrentDebt() {
        var annualData = new Dictionary<int, IReadOnlyDictionary<string, decimal>> {
            [2022] = new Dictionary<string, decimal> { ["NetIncomeLoss"] = 100m },
        };
        var snapshot = new Dictionary<string, decimal> {
            ["LongTermDebtNoncurrent"] = 400m,
            ["LongTermDebtCurrent"] = 100m,
            ["LiabilitiesAndStockholdersEquity"] = 2000m,
            ["Liabilities"] = 1000m,
        };

        var (metrics, _) = MoatScoringService.ComputeMoatDerivedMetrics(annualData, snapshot, null, null, null);

        Assert.Equal(0.5m, metrics.DebtToEquityRatio);
    }

    [Fact]
    public void AllConceptNames_FetchesCurrentDebtAndShortTermBorrowings() {
        Assert.Contains("LongTermDebtCurrent", ScoringService.AllConceptNames);
        Assert.Contains("ShortTermBorrowings", ScoringService.AllConceptNames);
    }
}
