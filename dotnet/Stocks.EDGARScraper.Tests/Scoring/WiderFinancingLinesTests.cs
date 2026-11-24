using System.Collections.Generic;
using Stocks.Persistence.Services;

namespace Stocks.EDGARScraper.Tests.Scoring;

/// <summary>
/// E12: net cash flow strips the debt and share lines that the original chains missed,
/// nested per the us-gaap calculation tree so a total and its parts count once.
/// </summary>
public class WiderFinancingLinesTests {
    private const decimal CashChange = 1000m;

    // Net cash flow is the cash change minus net financing, so this is the net financing stripped.
    private static decimal Stripped(Dictionary<string, decimal> data) =>
        CashChange - ScoringService.ComputeYearNetCashFlow(data, CashChange);

    [Theory]
    [InlineData("ProceedsFromIssuanceOfSecuredDebt")]
    [InlineData("ProceedsFromIssuanceOfUnsecuredDebt")]
    [InlineData("ProceedsFromIssuanceOfSubordinatedLongTermDebt")]
    [InlineData("ProceedsFromIssuanceOfMediumTermNotes")]
    [InlineData("ProceedsFromIssuanceOfFirstMortgageBond")]
    [InlineData("ProceedsFromIssuanceOfOtherLongTermDebt")]
    [InlineData("ProceedsFromLongTermLinesOfCredit")]
    [InlineData("ProceedsFromRelatedPartyDebt")]
    [InlineData("ProceedsFromNotesPayable")]
    [InlineData("ProceedsFromBankDebt")]
    [InlineData("ProceedsFromOtherShortTermDebt")]
    [InlineData("ProceedsFromUnsecuredLinesOfCredit")]
    [InlineData("ProceedsFromSecuredLinesOfCredit")]
    [InlineData("ProceedsFromShortTermDebt")]
    [InlineData("ProceedsFromRepaymentsOfRelatedPartyDebt")]
    [InlineData("ProceedsFromRepaymentsOfSecuredDebt")]
    [InlineData("ProceedsFromRepaymentsOfNotesPayable")]
    [InlineData("ProceedsFromRepaymentsOfOtherLongTermDebt")]
    [InlineData("ProceedsFromIssuanceOrSaleOfEquity")]
    [InlineData("ProceedsFromIssuanceInitialPublicOffering")]
    [InlineData("ProceedsFromIssuanceOfPrivatePlacement")]
    [InlineData("ProceedsFromIssuanceOfWarrants")]
    [InlineData("ProceedsFromWarrantExercises")]
    [InlineData("ProceedsFromStockPlans")]
    [InlineData("ProceedsFromIssuanceOfCommonLimitedPartnersUnits")]
    [InlineData("ProceedsFromContributedCapital")]
    [InlineData("ProceedsFromIssuanceOfConvertiblePreferredStock")]
    [InlineData("ProceedsFromIssuanceOfSharesUnderIncentiveAndShareBasedCompensationPlansIncludingStockOptions")]
    public void InflowLine_Stripped_AndFetched(string concept) {
        Assert.Equal(40m, Stripped(new Dictionary<string, decimal> { [concept] = 40m }));
        Assert.Contains(concept, ScoringService.AllConceptNames);
    }

    [Theory]
    [InlineData("RepaymentsOfSecuredDebt")]
    [InlineData("RepaymentsOfUnsecuredDebt")]
    [InlineData("RepaymentsOfSubordinatedDebt")]
    [InlineData("RepaymentsOfMediumTermNotes")]
    [InlineData("RepaymentsOfFirstMortgageBond")]
    [InlineData("RepaymentsOfOtherLongTermDebt")]
    [InlineData("RepaymentsOfLongTermLinesOfCredit")]
    [InlineData("RepaymentsOfRelatedPartyDebt")]
    [InlineData("RepaymentsOfBankDebt")]
    [InlineData("RepaymentsOfOtherShortTermDebt")]
    [InlineData("RepaymentsOfShortTermDebt")]
    public void RepaymentLine_AddedBack_AndFetched(string concept) {
        Assert.Equal(-40m, Stripped(new Dictionary<string, decimal> { [concept] = 40m }));
        Assert.Contains(concept, ScoringService.AllConceptNames);
    }

    [Fact]
    public void LongTermTotalCoversWiderChildren_TotalOnly() {
        Assert.Equal(100m, Stripped(new Dictionary<string, decimal> {
            ["ProceedsFromIssuanceOfLongTermDebt"] = 100m,
            ["ProceedsFromIssuanceOfSecuredDebt"] = 60m,
            ["ProceedsFromLongTermLinesOfCredit"] = 40m,
        }));
        Assert.Equal(-100m, Stripped(new Dictionary<string, decimal> {
            ["RepaymentsOfLongTermDebt"] = 100m,
            ["RepaymentsOfSecuredDebt"] = 60m,
            ["RepaymentsOfLongTermLinesOfCredit"] = 40m,
        }));
    }

    [Fact]
    public void LongTermTotalSmallerThanWiderChildren_BothCounted() {
        Assert.Equal(150m, Stripped(new Dictionary<string, decimal> {
            ["ProceedsFromIssuanceOfLongTermDebt"] = 50m,
            ["ProceedsFromIssuanceOfSecuredDebt"] = 70m,
            ["ProceedsFromRelatedPartyDebt"] = 30m,
        }));
    }

    [Fact]
    public void AllDebtTotalCoversWiderChildren_TotalOnly() {
        Assert.Equal(90m, Stripped(new Dictionary<string, decimal> {
            ["ProceedsFromIssuanceOfDebt"] = 90m,
            ["ProceedsFromIssuanceOfUnsecuredDebt"] = 60m,
        }));
    }

    [Fact]
    public void EqualSiblings_CountedOnce() {
        // One line tagged three ways beside a separate one.
        Assert.Equal(55m, Stripped(new Dictionary<string, decimal> {
            ["ProceedsFromIssuanceOfSeniorLongTermDebt"] = 40m,
            ["ProceedsFromIssuanceOfUnsecuredDebt"] = 40m,
            ["ProceedsFromIssuanceOfMediumTermNotes"] = 40m,
            ["ProceedsFromRelatedPartyDebt"] = 15m,
        }));
    }

    [Theory]
    [InlineData("ProceedsFromRepaymentsOfRelatedPartyDebt", "ProceedsFromRelatedPartyDebt", "RepaymentsOfRelatedPartyDebt")]
    [InlineData("ProceedsFromRepaymentsOfSecuredDebt", "ProceedsFromIssuanceOfSecuredDebt", "RepaymentsOfSecuredDebt")]
    [InlineData("ProceedsFromRepaymentsOfNotesPayable", "ProceedsFromNotesPayable", "RepaymentsOfNotesPayable")]
    [InlineData("ProceedsFromRepaymentsOfOtherLongTermDebt", "ProceedsFromIssuanceOfOtherLongTermDebt", "RepaymentsOfOtherLongTermDebt")]
    public void NetOnlyLongTermLine_IgnoredWhenEitherGrossSidePresent(string net, string proceeds, string repayments) {
        Assert.Equal(30m, Stripped(new Dictionary<string, decimal> { [net] = 20m, [proceeds] = 30m }));
        Assert.Equal(-10m, Stripped(new Dictionary<string, decimal> { [net] = 20m, [repayments] = 10m }));
    }

    [Fact]
    public void NotesPayable_CountedOnBothSides() {
        Assert.Equal(20m, Stripped(new Dictionary<string, decimal> {
            ["ProceedsFromNotesPayable"] = 50m,
            ["RepaymentsOfNotesPayable"] = 30m,
        }));
    }

    [Fact]
    public void ShortTermGrossTotals_CoverTheirLines() {
        Assert.Equal(40m, Stripped(new Dictionary<string, decimal> {
            ["ProceedsFromShortTermDebt"] = 100m,
            ["ProceedsFromNotesPayable"] = 50m,
            ["ProceedsFromLinesOfCredit"] = 45m,
            ["RepaymentsOfShortTermDebt"] = 60m,
            ["RepaymentsOfBankDebt"] = 60m,
        }));
    }

    [Fact]
    public void NetShortTermTotal_ReplacesEveryShortTermLine_ExceptCommercialPaperAndNetLongTerm() {
        Assert.Equal(17m, Stripped(new Dictionary<string, decimal> {
            ["ProceedsFromRepaymentsOfShortTermDebt"] = 10m,
            ["ProceedsFromShortTermDebt"] = 100m,
            ["RepaymentsOfBankDebt"] = 30m,
            ["ProceedsFromNotesPayable"] = 50m,
            ["ProceedsFromRepaymentsOfCommercialPaper"] = 5m,
            ["ProceedsFromRepaymentsOfSecuredDebt"] = 2m,
        }));
    }

    [Theory]
    [InlineData("ProceedsFromLinesOfCredit", 30)]
    [InlineData("RepaymentsOfLinesOfCredit", -30)]
    public void NetLinesOfCredit_OnlyWithoutAGrossTag(string gross, int expected) {
        Assert.Equal(20m, Stripped(new Dictionary<string, decimal> { ["ProceedsFromRepaymentsOfLinesOfCredit"] = 20m }));
        Assert.Equal(expected, Stripped(new Dictionary<string, decimal> {
            ["ProceedsFromRepaymentsOfLinesOfCredit"] = 20m,
            [gross] = 30m,
        }));
    }

    [Theory]
    [InlineData(100, 100)] // covers unsecured 60 + secured 40
    [InlineData(70, 170)]  // smaller: a separate line
    public void LinesOfCredit_NestedOverUnsecuredAndSecured(int total, int expected) {
        Assert.Equal(expected, Stripped(new Dictionary<string, decimal> {
            ["ProceedsFromLinesOfCredit"] = total,
            ["ProceedsFromUnsecuredLinesOfCredit"] = 60m,
            ["ProceedsFromSecuredLinesOfCredit"] = 40m,
        }));
    }

    [Theory]
    [InlineData("ProceedsFromUnsecuredLinesOfCredit")]
    [InlineData("ProceedsFromSecuredLinesOfCredit")]
    public void NetLinesOfCredit_IgnoredWithAnUnsecuredOrSecuredLine(string line) {
        Assert.Equal(30m, Stripped(new Dictionary<string, decimal> {
            ["ProceedsFromRepaymentsOfLinesOfCredit"] = 20m,
            [line] = 30m,
        }));
    }

    [Fact]
    public void UnsecuredLine_EqualToAnotherShortTermLine_CountedOnce() {
        Assert.Equal(30m, Stripped(new Dictionary<string, decimal> {
            ["ProceedsFromBankDebt"] = 30m,
            ["ProceedsFromUnsecuredLinesOfCredit"] = 30m,
        }));
    }

    [Fact]
    public void UnsecuredLine_IgnoredWithTheNetShortTermTotal() {
        Assert.Equal(10m, Stripped(new Dictionary<string, decimal> {
            ["ProceedsFromRepaymentsOfShortTermDebt"] = 10m,
            ["ProceedsFromUnsecuredLinesOfCredit"] = 30m,
        }));
    }

    [Fact]
    public void NetLinesOfCredit_AddedToOtherShortTermLines() {
        Assert.Equal(27m, Stripped(new Dictionary<string, decimal> {
            ["ProceedsFromRepaymentsOfLinesOfCredit"] = 20m,
            ["ProceedsFromBankDebt"] = 7m,
        }));
    }

    [Fact]
    public void StockLines_SiblingsSummed_EqualCountedOnce() {
        Assert.Equal(17m, Stripped(new Dictionary<string, decimal> {
            ["ProceedsFromIssuanceOfCommonStock"] = 10m,
            ["ProceedsFromIssuanceInitialPublicOffering"] = 10m,
            ["ProceedsFromWarrantExercises"] = 7m,
        }));
    }

    [Fact]
    public void SharePlanTotal_CoversOptionExercises() {
        Assert.Equal(13m, Stripped(new Dictionary<string, decimal> {
            ["ProceedsFromIssuanceOfCommonStock"] = 4m,
            ["ProceedsFromIssuanceOfSharesUnderIncentiveAndShareBasedCompensationPlansIncludingStockOptions"] = 9m,
            ["ProceedsFromStockOptionsExercised"] = 6m,
        }));
    }

    [Fact]
    public void EquityTotal_CoversCommonAndPreferred_PreferredCountedOnce() {
        // Equity 100 = common 70 + preferred 30: the preferred chain strips 30, this one 70.
        var data = new Dictionary<string, decimal> {
            ["ProceedsFromIssuanceOrSaleOfEquity"] = 100m,
            ["ProceedsFromIssuanceOfCommonStock"] = 70m,
            ["ProceedsFromIssuanceOfPreferredStockAndPreferenceStock"] = 30m,
        };
        Assert.Equal(70m, ScoringService.ResolveStockProceeds(data));
        Assert.Equal(100m, Stripped(data));
    }

    [Fact]
    public void EquityTotalSmallerThanItsLines_BothCounted() {
        Assert.Equal(120m, Stripped(new Dictionary<string, decimal> {
            ["ProceedsFromIssuanceOrSaleOfEquity"] = 50m,
            ["ProceedsFromIssuanceOfCommonStock"] = 70m,
        }));
    }

    [Fact]
    public void PreferredAlone_LeavesStockProceedsAtZero() {
        Assert.Equal(0m, ScoringService.ResolveStockProceeds(new Dictionary<string, decimal> {
            ["ProceedsFromIssuanceOfPreferredStockAndPreferenceStock"] = 30m,
        }));
    }

    [Fact]
    public void WiderStockLines_FeedAdjustedRetainedEarnings() {
        var byYear = new Dictionary<int, IReadOnlyDictionary<string, decimal>> {
            [2023] = new Dictionary<string, decimal> { ["RetainedEarningsAccumulatedDeficit"] = 90m },
            [2024] = new Dictionary<string, decimal> {
                ["RetainedEarningsAccumulatedDeficit"] = 100m,
                ["ProceedsFromWarrantExercises"] = 6m,
            },
        };
        Assert.Equal(94m, ScoringService.ComputeDerivedMetrics(byYear, byYear[2024], 90m, null, null).AdjustedRetainedEarnings);
    }
}
