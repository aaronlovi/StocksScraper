using System;
using System.Collections.Generic;
using Stocks.DataModels.Scoring;

namespace Stocks.EDGARScraper.Tests.Scoring;

public class ScoringModelTests {
    [Fact]
    public void ScoringResult_OverallScoreDoesNotExceedComputableChecks() {
        var scorecard = new List<ScoringCheck> {
            new(1, "Debt-to-Equity", 0.3m, "< 0.5", ScoringCheckResult.Pass),
            new(2, "Book Value", 200_000_000m, "> $150M", ScoringCheckResult.Pass),
            new(3, "Price-to-Book", 4.0m, "≤ 3.0", ScoringCheckResult.Fail),
            new(4, "Avg NCF Positive", null, "> 0", ScoringCheckResult.NotAvailable),
            new(5, "Avg OE Positive", 50_000m, "> 0", ScoringCheckResult.Pass),
            new(6, "Est Return CF", null, "> 5%", ScoringCheckResult.NotAvailable),
            new(7, "Est Return OE", 8.0m, "> 5%", ScoringCheckResult.Pass),
            new(8, "Est Return CF Not Too Big", null, "< 40%", ScoringCheckResult.NotAvailable),
            new(9, "Est Return OE Not Too Big", 8.0m, "< 40%", ScoringCheckResult.Pass),
            new(10, "Debt-to-Book", 0.5m, "< 1.0", ScoringCheckResult.Pass),
            new(11, "Retained Earnings Positive", 1_000_000m, "> 0", ScoringCheckResult.Pass),
            new(12, "History Long Enough", 5m, "≥ 4 years", ScoringCheckResult.Pass),
            new(13, "Retained Earnings Increased", 1m, "increased", ScoringCheckResult.Pass),
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

        var result = new ScoringResult(
            RawDataByYear: new Dictionary<int, IReadOnlyDictionary<string, decimal>>(),
            Metrics: new DerivedMetrics(null, null, null, null, null, null, null, null, null, null, null, null),
            Scorecard: scorecard,
            OverallScore: overallScore,
            ComputableChecks: computableChecks,
            YearsOfData: 5,
            PricePerShare: 150.0m,
            PriceDate: new DateOnly(2025, 1, 15),
            SharesOutstanding: 1_000_000,
            MaxBuyPrice: null,
            PercentageUpside: null);

        Assert.Equal(9, result.OverallScore);
        Assert.Equal(10, result.ComputableChecks);
        Assert.True(result.OverallScore <= result.ComputableChecks);
        Assert.True(result.ComputableChecks <= 13);
    }

    [Fact]
    public void DerivedMetrics_AllFieldsNullable() {
        var metrics = new DerivedMetrics(
            BookValue: null,
            MarketCap: null,
            DebtToEquityRatio: null,
            PriceToBookRatio: null,
            DebtToBookRatio: null,
            AdjustedRetainedEarnings: null,
            OldestRetainedEarnings: null,
            AverageNetCashFlow: null,
            AverageOwnerEarnings: null,
            EstimatedReturnCF: null,
            EstimatedReturnOE: null,
            CurrentDividendsPaid: null);

        Assert.Null(metrics.BookValue);
        Assert.Null(metrics.MarketCap);
        Assert.Null(metrics.DebtToEquityRatio);
        Assert.Null(metrics.PriceToBookRatio);
        Assert.Null(metrics.DebtToBookRatio);
        Assert.Null(metrics.AdjustedRetainedEarnings);
        Assert.Null(metrics.OldestRetainedEarnings);
        Assert.Null(metrics.AverageNetCashFlow);
        Assert.Null(metrics.AverageOwnerEarnings);
        Assert.Null(metrics.EstimatedReturnCF);
        Assert.Null(metrics.EstimatedReturnOE);
        Assert.Null(metrics.CurrentDividendsPaid);
    }
}
