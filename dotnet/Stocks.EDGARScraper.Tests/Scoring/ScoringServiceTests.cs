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

namespace Stocks.EDGARScraper.Tests.Scoring;

public class ScoringServiceTests {

    #region ResolveField tests

    [Fact]
    public void ResolveField_ReturnsPrimaryWhenPresent() {
        var data = new Dictionary<string, decimal> {
            ["StockholdersEquity"] = 500m,
            ["StockholdersEquityIncludingPortionAttributableToNoncontrollingInterest"] = 600m,
        };

        decimal? result = ScoringService.ResolveField(data,
            ScoringService.EquityChain, null);

        Assert.Equal(500m, result);
    }

    [Fact]
    public void ResolveField_ReturnsFallbackWhenPrimaryMissing() {
        var data = new Dictionary<string, decimal> {
            ["StockholdersEquityIncludingPortionAttributableToNoncontrollingInterest"] = 600m,
        };

        decimal? result = ScoringService.ResolveField(data,
            ScoringService.EquityChain, null);

        Assert.Equal(600m, result);
    }

    [Fact]
    public void ResolveField_ReturnsDefaultWhenAllMissing() {
        var data = new Dictionary<string, decimal>();

        decimal? result = ScoringService.ResolveField(data, ScoringService.DebtChain, 0m);

        Assert.Equal(0m, result);
    }

    [Fact]
    public void ResolveField_ReturnsNullDefaultWhenAllMissing() {
        var data = new Dictionary<string, decimal>();

        decimal? result = ScoringService.ResolveField(data, ScoringService.EquityChain, null);

        Assert.Null(result);
    }

    #endregion

    #region ResolveEquity tests

    [Fact]
    public void ResolveEquity_PrefersLiabilitiesAndEquityMinusLiabilities() {
        var data = new Dictionary<string, decimal> {
            ["LiabilitiesAndStockholdersEquity"] = 1000m,
            ["Liabilities"] = 400m,
            ["Assets"] = 1000m,
            ["StockholdersEquity"] = 500m, // stale value — should be ignored
        };

        decimal? result = ScoringService.ResolveEquity(data);

        Assert.Equal(600m, result); // 1000 - 400
    }

    [Fact]
    public void ResolveEquity_FallsBackToAssetsMinusLiabilities() {
        var data = new Dictionary<string, decimal> {
            ["Assets"] = 800m,
            ["Liabilities"] = 300m,
            ["StockholdersEquity"] = 400m,
        };

        decimal? result = ScoringService.ResolveEquity(data);

        Assert.Equal(500m, result); // 800 - 300
    }

    [Fact]
    public void ResolveEquity_FallsBackToDirectEquityConcepts() {
        var data = new Dictionary<string, decimal> {
            ["StockholdersEquity"] = 700m,
        };

        decimal? result = ScoringService.ResolveEquity(data);

        Assert.Equal(700m, result);
    }

    [Fact]
    public void ResolveEquity_FallsBackToMembersEquity() {
        var data = new Dictionary<string, decimal> {
            ["MembersEquity"] = 250m,
        };

        decimal? result = ScoringService.ResolveEquity(data);

        Assert.Equal(250m, result);
    }

    [Fact]
    public void ResolveEquity_ReturnsNullWhenNothingAvailable() {
        var data = new Dictionary<string, decimal>();

        decimal? result = ScoringService.ResolveEquity(data);

        Assert.Null(result);
    }

    [Fact]
    public void ResolveEquity_SubtractsNciFromDerivedEquity() {
        var data = new Dictionary<string, decimal> {
            ["LiabilitiesAndStockholdersEquity"] = 400_000m,
            ["Liabilities"] = 120_000m,
            ["MinorityInterest"] = 3_000m,
        };

        decimal? result = ScoringService.ResolveEquity(data);

        Assert.Equal(277_000m, result); // 400K - 120K - 3K
    }

    [Fact]
    public void ResolveEquity_SubtractsRedeemableNciFromDerivedEquity() {
        var data = new Dictionary<string, decimal> {
            ["Assets"] = 500_000m,
            ["Liabilities"] = 200_000m,
            ["MinorityInterest"] = 5_000m,
            ["RedeemableNoncontrollingInterestEquityCarryingAmount"] = 10_000m,
        };

        decimal? result = ScoringService.ResolveEquity(data);

        Assert.Equal(285_000m, result); // 500K - 200K - 5K - 10K
    }

    [Fact]
    public void ResolveEquity_UsesVieNciWhenMinorityInterestMissing() {
        var data = new Dictionary<string, decimal> {
            ["LiabilitiesAndStockholdersEquity"] = 402_000m,
            ["Liabilities"] = 119_000m,
            ["NoncontrollingInterestInVariableInterestEntity"] = 3_400m,
        };

        decimal? result = ScoringService.ResolveEquity(data);

        Assert.Equal(279_600m, result); // 402K - 119K - 3.4K
    }

    #endregion

    #region ResolveDepletionAndAmortization tests

    [Fact]
    public void ResolveDepletionAndAmortization_UsesDirectConceptsWhenPresent() {
        var data = new Dictionary<string, decimal> {
            ["Depletion"] = 1_000_000m,
            ["AmortizationOfIntangibleAssets"] = 2_000_000m,
            ["DepreciationDepletionAndAmortization"] = 11_000_000m,
            ["Depreciation"] = 8_000_000m,
        };

        decimal result = ScoringService.ResolveDepletionAndAmortization(data);

        Assert.Equal(3_000_000m, result); // 1M + 2M, NOT DDA-Depreciation
    }

    [Fact]
    public void ResolveDepletionAndAmortization_FallsBackToDdaMinusDepreciation() {
        var data = new Dictionary<string, decimal> {
            ["DepreciationDepletionAndAmortization"] = 11_700_000_000m,
            ["Depreciation"] = 8_000_000_000m,
        };

        decimal result = ScoringService.ResolveDepletionAndAmortization(data);

        Assert.Equal(3_700_000_000m, result); // 11.7B - 8B
    }

    [Fact]
    public void ResolveDepletionAndAmortization_UsesPartialDirectConcepts() {
        // Only amortization is tagged, no depletion
        var data = new Dictionary<string, decimal> {
            ["AmortizationOfIntangibleAssets"] = 2_000_000m,
            ["DepreciationDepletionAndAmortization"] = 11_000_000m,
            ["Depreciation"] = 8_000_000m,
        };

        decimal result = ScoringService.ResolveDepletionAndAmortization(data);

        Assert.Equal(2_000_000m, result); // Only amortization, depletion defaults to 0
    }

    [Fact]
    public void ResolveDepletionAndAmortization_ReturnsZeroWhenNothingAvailable() {
        var data = new Dictionary<string, decimal>();

        decimal result = ScoringService.ResolveDepletionAndAmortization(data);

        Assert.Equal(0m, result);
    }

    #endregion

    #region ResolveDeferredTax tests

    [Fact]
    public void ResolveDeferredTax_UsesAggregateWhenPresent() {
        var data = new Dictionary<string, decimal> {
            ["DeferredIncomeTaxExpenseBenefit"] = 5_000_000m,
            ["DeferredFederalIncomeTaxExpenseBenefit"] = 3_000_000m,
            ["DeferredForeignIncomeTaxExpenseBenefit"] = 1_500_000m,
            ["DeferredStateAndLocalIncomeTaxExpenseBenefit"] = 500_000m,
        };

        decimal result = ScoringService.ResolveDeferredTax(data);

        Assert.Equal(5_000_000m, result); // Aggregate preferred
    }

    [Fact]
    public void ResolveDeferredTax_SumsComponentsWhenAggregateIsMissing() {
        var data = new Dictionary<string, decimal> {
            ["DeferredFederalIncomeTaxExpenseBenefit"] = -1_804_000_000m,
            ["DeferredForeignIncomeTaxExpenseBenefit"] = 604_000_000m,
            ["DeferredStateAndLocalIncomeTaxExpenseBenefit"] = -139_000_000m,
        };

        decimal result = ScoringService.ResolveDeferredTax(data);

        Assert.Equal(-1_339_000_000m, result); // Sum of components
    }

    [Fact]
    public void ResolveDeferredTax_SumsPartialComponents() {
        // Only federal is tagged
        var data = new Dictionary<string, decimal> {
            ["DeferredFederalIncomeTaxExpenseBenefit"] = -2_000_000m,
        };

        decimal result = ScoringService.ResolveDeferredTax(data);

        Assert.Equal(-2_000_000m, result);
    }

    [Fact]
    public void ResolveDeferredTax_ReturnsZeroWhenNothingAvailable() {
        var data = new Dictionary<string, decimal>();

        decimal result = ScoringService.ResolveDeferredTax(data);

        Assert.Equal(0m, result);
    }

    #endregion

    #region ResolveWorkingCapitalChange tests

    // Balance type lookup matching the real US-GAAP taxonomy:
    // Credit = asset concepts (positive XBRL value = asset increased = cash outflow → negate)
    // Debit = liability concepts (positive XBRL value = liability increased = cash inflow → keep)
    private static readonly Dictionary<string, TaxonomyBalanceTypes> WcBalanceTypes = new(StringComparer.Ordinal) {
        ["IncreaseDecreaseInAccountsReceivable"] = TaxonomyBalanceTypes.Credit,
        ["IncreaseDecreaseInOtherReceivables"] = TaxonomyBalanceTypes.Credit,
        ["IncreaseDecreaseInAccountsAndOtherReceivables"] = TaxonomyBalanceTypes.Credit,
        ["IncreaseDecreaseInAccountsReceivableAndOtherOperatingAssets"] = TaxonomyBalanceTypes.Credit,
        ["IncreaseDecreaseInInventories"] = TaxonomyBalanceTypes.Credit,
        ["IncreaseDecreaseInPrepaidDeferredExpenseAndOtherAssets"] = TaxonomyBalanceTypes.Credit,
        ["IncreaseDecreaseInOtherOperatingAssets"] = TaxonomyBalanceTypes.Credit,
        ["IncreaseDecreaseInOtherCurrentAssets"] = TaxonomyBalanceTypes.Credit,
        ["IncreaseDecreaseInOtherNoncurrentAssets"] = TaxonomyBalanceTypes.Credit,
        ["IncreaseDecreaseInAccountsPayable"] = TaxonomyBalanceTypes.Debit,
        ["IncreaseDecreaseInAccountsPayableTrade"] = TaxonomyBalanceTypes.Debit,
        ["IncreaseDecreaseInAccruedLiabilities"] = TaxonomyBalanceTypes.Debit,
        ["IncreaseDecreaseInAccountsPayableAndAccruedLiabilities"] = TaxonomyBalanceTypes.Debit,
        ["IncreaseDecreaseInAccruedLiabilitiesAndOtherOperatingLiabilities"] = TaxonomyBalanceTypes.Debit,
        ["IncreaseDecreaseInDeferredRevenue"] = TaxonomyBalanceTypes.Debit,
        ["IncreaseDecreaseInContractWithCustomerLiability"] = TaxonomyBalanceTypes.Debit,
        ["IncreaseDecreaseInOtherOperatingLiabilities"] = TaxonomyBalanceTypes.Debit,
        ["IncreaseDecreaseInOtherCurrentLiabilities"] = TaxonomyBalanceTypes.Debit,
        ["IncreaseDecreaseInOtherNoncurrentLiabilities"] = TaxonomyBalanceTypes.Debit,
        ["IncreaseDecreaseInSelfInsuranceReserve"] = TaxonomyBalanceTypes.Debit,
        ["IncreaseDecreaseInAccruedIncomeTaxesPayable"] = TaxonomyBalanceTypes.Debit,
    };

    [Fact]
    public void ResolveWorkingCapitalChange_UsesAggregateWhenPresent() {
        var data = new Dictionary<string, decimal> {
            ["IncreaseDecreaseInOperatingCapital"] = -5_000_000m,
            ["IncreaseDecreaseInAccountsReceivable"] = 3_000_000m,
            ["IncreaseDecreaseInInventories"] = -1_000_000m,
        };

        decimal result = ScoringService.ResolveWorkingCapitalChange(data, WcBalanceTypes);

        Assert.Equal(-5_000_000m, result); // Aggregate preferred
    }

    [Fact]
    public void ResolveWorkingCapitalChange_NegatesCreditBalanceAssetConcepts() {
        // Credit-balance concepts (assets) should be negated:
        // positive XBRL value = asset increased = cash outflow
        var data = new Dictionary<string, decimal> {
            ["IncreaseDecreaseInAccountsReceivable"] = 6_682_000_000m,
            ["IncreaseDecreaseInInventories"] = -1_400_000_000m,
            ["IncreaseDecreaseInAccountsPayable"] = 902_000_000m,
            ["IncreaseDecreaseInOtherOperatingAssets"] = 9_197_000_000m,
            ["IncreaseDecreaseInOtherOperatingLiabilities"] = -11_076_000_000m,
            ["IncreaseDecreaseInOtherReceivables"] = 347_000_000m,
        };

        decimal result = ScoringService.ResolveWorkingCapitalChange(data, WcBalanceTypes);

        // Asset items negated: -6682 + 1400 - 9197 - 347 = -14826
        // Liability items kept: 902 - 11076 = -10174
        // Total: -14826 + -10174 = -25000
        Assert.Equal(-25_000_000_000m, result);
    }

    [Fact]
    public void ResolveWorkingCapitalChange_PrefersCombinedAPOverIndividual() {
        var data = new Dictionary<string, decimal> {
            ["IncreaseDecreaseInAccountsPayableAndAccruedLiabilities"] = 5_000_000m,
            ["IncreaseDecreaseInAccountsPayable"] = 3_000_000m,     // should be ignored
            ["IncreaseDecreaseInAccruedLiabilities"] = 2_000_000m,  // should be ignored
        };

        decimal result = ScoringService.ResolveWorkingCapitalChange(data, WcBalanceTypes);

        Assert.Equal(5_000_000m, result); // Combined preferred (debit, kept as-is)
    }

    [Fact]
    public void ResolveWorkingCapitalChange_FallsBackToIndividualAPAndAL() {
        var data = new Dictionary<string, decimal> {
            ["IncreaseDecreaseInAccountsPayable"] = 3_000_000m,
            ["IncreaseDecreaseInAccruedLiabilities"] = 2_000_000m,
        };

        decimal result = ScoringService.ResolveWorkingCapitalChange(data, WcBalanceTypes);

        Assert.Equal(5_000_000m, result); // Both debit, kept as-is
    }

    [Fact]
    public void ResolveWorkingCapitalChange_PrefersCombinedAROverIndividual() {
        var data = new Dictionary<string, decimal> {
            ["IncreaseDecreaseInAccountsAndOtherReceivables"] = 7_000_000m,
            ["IncreaseDecreaseInAccountsReceivable"] = 5_000_000m,  // should be ignored
        };

        decimal result = ScoringService.ResolveWorkingCapitalChange(data, WcBalanceTypes);

        Assert.Equal(-7_000_000m, result); // Credit → negated
    }

    [Fact]
    public void ResolveWorkingCapitalChange_PrefersDeferredRevenueOverContractLiability() {
        var data = new Dictionary<string, decimal> {
            ["IncreaseDecreaseInDeferredRevenue"] = 1_000_000m,
            ["IncreaseDecreaseInContractWithCustomerLiability"] = 500_000m, // should be ignored
        };

        decimal result = ScoringService.ResolveWorkingCapitalChange(data, WcBalanceTypes);

        Assert.Equal(1_000_000m, result); // Debit, kept as-is
    }

    [Fact]
    public void ResolveWorkingCapitalChange_ReturnsZeroWhenNothingAvailable() {
        var data = new Dictionary<string, decimal>();

        decimal result = ScoringService.ResolveWorkingCapitalChange(data, WcBalanceTypes);

        Assert.Equal(0m, result);
    }

    [Fact]
    public void ResolveWorkingCapitalChange_PrefersGeneralOtherAssetsOverCurrentNoncurrent() {
        var data = new Dictionary<string, decimal> {
            ["IncreaseDecreaseInOtherOperatingAssets"] = 500m,
            ["IncreaseDecreaseInOtherCurrentAssets"] = 200m,
            ["IncreaseDecreaseInOtherNoncurrentAssets"] = 300m,
        };

        decimal result = ScoringService.ResolveWorkingCapitalChange(data, WcBalanceTypes);

        Assert.Equal(-500m, result); // Credit → negated
    }

    [Fact]
    public void ResolveWorkingCapitalChange_FallsBackToCurrentAndNoncurrentAssets() {
        var data = new Dictionary<string, decimal> {
            ["IncreaseDecreaseInOtherCurrentAssets"] = 200m,
            ["IncreaseDecreaseInOtherNoncurrentAssets"] = 300m,
        };

        decimal result = ScoringService.ResolveWorkingCapitalChange(data, WcBalanceTypes);

        Assert.Equal(-500m, result); // Both credit → negated: -(200) + -(300)
    }

    [Fact]
    public void ResolveWorkingCapitalChange_PrefersGeneralOtherLiabilitiesOverCurrentNoncurrent() {
        var data = new Dictionary<string, decimal> {
            ["IncreaseDecreaseInOtherOperatingLiabilities"] = 400m,
            ["IncreaseDecreaseInOtherCurrentLiabilities"] = 150m,
            ["IncreaseDecreaseInOtherNoncurrentLiabilities"] = 250m,
        };

        decimal result = ScoringService.ResolveWorkingCapitalChange(data, WcBalanceTypes);

        Assert.Equal(400m, result); // Debit, kept as-is
    }

    [Fact]
    public void ResolveWorkingCapitalChange_FallsBackToCurrentAndNoncurrentLiabilities() {
        var data = new Dictionary<string, decimal> {
            ["IncreaseDecreaseInOtherCurrentLiabilities"] = 150m,
            ["IncreaseDecreaseInOtherNoncurrentLiabilities"] = 250m,
        };

        decimal result = ScoringService.ResolveWorkingCapitalChange(data, WcBalanceTypes);

        Assert.Equal(400m, result); // Both debit, kept as-is
    }

    [Fact]
    public void ResolveWorkingCapitalChange_IncludesAccruedIncomeTaxesPayable() {
        var data = new Dictionary<string, decimal> {
            ["IncreaseDecreaseInAccountsReceivable"] = 100m,
            ["IncreaseDecreaseInAccruedIncomeTaxesPayable"] = -38m,
        };

        decimal result = ScoringService.ResolveWorkingCapitalChange(data, WcBalanceTypes);

        // AR: credit → negated: -100. AccruedTax: debit → kept: -38. Total: -138
        Assert.Equal(-138m, result);
    }

    [Fact]
    public void ResolveWorkingCapitalChange_MsftStyleFullComponentSum() {
        // Simulates MSFT's reporting pattern: no aggregate, uses current/noncurrent split
        var data = new Dictionary<string, decimal> {
            ["IncreaseDecreaseInAccountsReceivable"] = 10581m,   // credit → negate
            ["IncreaseDecreaseInInventories"] = -309m,           // credit → negate
            ["IncreaseDecreaseInAccountsPayable"] = 569m,        // debit → keep
            ["IncreaseDecreaseInContractWithCustomerLiability"] = 5438m, // debit → keep
            ["IncreaseDecreaseInOtherCurrentAssets"] = 3044m,    // credit → negate
            ["IncreaseDecreaseInOtherCurrentLiabilities"] = 5922m, // debit → keep
            ["IncreaseDecreaseInOtherNoncurrentAssets"] = 2950m, // credit → negate
            ["IncreaseDecreaseInOtherNoncurrentLiabilities"] = -975m, // debit → keep
            ["IncreaseDecreaseInAccruedIncomeTaxesPayable"] = -38m,   // debit → keep
        };

        decimal result = ScoringService.ResolveWorkingCapitalChange(data, WcBalanceTypes);

        // Assets negated: -10581 + 309 - 3044 - 2950 = -16266
        // Liabilities kept: 569 + 5438 + 5922 - 975 - 38 = 10916
        // Total: -16266 + 10916 = -5350
        Assert.Equal(-5350m, result);
    }

    [Fact]
    public void ResolveWorkingCapitalChange_PrefersARAndOtherAssetsOverSeparateConcepts() {
        // AccountsReceivableAndOtherOperatingAssets subsumes AR + OtherOperatingAssets
        var data = new Dictionary<string, decimal> {
            ["IncreaseDecreaseInAccountsReceivableAndOtherOperatingAssets"] = 3249m,
            ["IncreaseDecreaseInAccountsReceivable"] = 1000m,  // should be ignored
            ["IncreaseDecreaseInOtherOperatingAssets"] = 500m,  // should be ignored
        };

        decimal result = ScoringService.ResolveWorkingCapitalChange(data, WcBalanceTypes);

        Assert.Equal(-3249m, result); // Credit → negated
    }

    [Fact]
    public void ResolveWorkingCapitalChange_ARAndOtherAssetsSkipsCurrentButKeepsNoncurrent() {
        // ARAndOtherOperatingAssets covers current operating assets; noncurrent is separate
        var data = new Dictionary<string, decimal> {
            ["IncreaseDecreaseInAccountsReceivableAndOtherOperatingAssets"] = 3249m,
            ["IncreaseDecreaseInOtherCurrentAssets"] = 200m,     // should be ignored (subsumed)
            ["IncreaseDecreaseInOtherNoncurrentAssets"] = 300m,  // should NOT be ignored (separate line item)
        };

        decimal result = ScoringService.ResolveWorkingCapitalChange(data, WcBalanceTypes);

        // Both credit → negated: -3249 + -300 = -3549
        Assert.Equal(-3549m, result);
    }

    [Fact]
    public void ResolveWorkingCapitalChange_AccruedAndOtherLiabSkipsOtherLiabGroup() {
        // AccruedLiabilitiesAndOtherOperatingLiabilities subsumes AccruedLiabilities + OtherOperatingLiabilities
        // Should still pick up AP separately, but skip "other liabilities" group
        var data = new Dictionary<string, decimal> {
            ["IncreaseDecreaseInAccruedLiabilitiesAndOtherOperatingLiabilities"] = -2904m,
            ["IncreaseDecreaseInAccountsPayable"] = 2972m,
            ["IncreaseDecreaseInOtherOperatingLiabilities"] = 500m,  // should be ignored
            ["IncreaseDecreaseInOtherCurrentLiabilities"] = 200m,    // should be ignored
        };

        decimal result = ScoringService.ResolveWorkingCapitalChange(data, WcBalanceTypes);

        // Both debit → kept: -2904 + 2972 = 68
        Assert.Equal(68m, result);
    }

    [Fact]
    public void ResolveWorkingCapitalChange_IncludesSelfInsuranceReserve() {
        var data = new Dictionary<string, decimal> {
            ["IncreaseDecreaseInAccountsPayable"] = 100m,
            ["IncreaseDecreaseInSelfInsuranceReserve"] = 1300m,
        };

        decimal result = ScoringService.ResolveWorkingCapitalChange(data, WcBalanceTypes);

        Assert.Equal(1400m, result); // Both debit, kept as-is
    }

    [Fact]
    public void ResolveWorkingCapitalChange_AmznStyleFullComponentSum() {
        // Simulates AMZN's reporting pattern: ARAndOtherOperatingAssets covers current,
        // OtherNoncurrentAssets is a separate line item
        var data = new Dictionary<string, decimal> {
            ["IncreaseDecreaseInAccountsReceivableAndOtherOperatingAssets"] = 3249m, // credit → negate
            ["IncreaseDecreaseInInventories"] = 1884m,            // credit → negate
            ["IncreaseDecreaseInAccountsPayable"] = 2972m,        // debit → keep
            ["IncreaseDecreaseInAccruedLiabilitiesAndOtherOperatingLiabilities"] = -2904m, // debit → keep
            ["IncreaseDecreaseInContractWithCustomerLiability"] = 4007m, // debit → keep
            ["IncreaseDecreaseInOtherNoncurrentAssets"] = 14483m, // credit → negate
            ["IncreaseDecreaseInSelfInsuranceReserve"] = 1300m,   // debit → keep
        };

        decimal result = ScoringService.ResolveWorkingCapitalChange(data, WcBalanceTypes);

        // Assets negated: -3249 - 1884 - 14483 = -19616
        // Liabilities kept: 2972 - 2904 + 4007 + 1300 = 5375
        // Total: -19616 + 5375 = -14241
        Assert.Equal(-14241m, result);
    }

    [Fact]
    public void ResolveWorkingCapitalChange_FallsBackToAccountsPayableTrade() {
        // META uses AccountsPayableTrade instead of AccountsPayable
        var data = new Dictionary<string, decimal> {
            ["IncreaseDecreaseInAccountsPayableTrade"] = 373m,
            ["IncreaseDecreaseInAccruedLiabilities"] = 323m,
        };

        decimal result = ScoringService.ResolveWorkingCapitalChange(data, WcBalanceTypes);

        Assert.Equal(696m, result); // Both debit, kept as-is
    }

    [Fact]
    public void ResolveWorkingCapitalChange_PrefersAccountsPayableOverTrade() {
        // When both exist, prefer the broader AccountsPayable
        var data = new Dictionary<string, decimal> {
            ["IncreaseDecreaseInAccountsPayable"] = 1000m,
            ["IncreaseDecreaseInAccountsPayableTrade"] = 373m,
        };

        decimal result = ScoringService.ResolveWorkingCapitalChange(data, WcBalanceTypes);

        Assert.Equal(1000m, result); // Debit, kept as-is
    }

    [Fact]
    public void ResolveWorkingCapitalChange_AccountsPayableTradeWithAccruedAndOtherLiab() {
        // AccountsPayableTrade should still be picked up alongside AccruedLiabilitiesAndOtherOperatingLiabilities
        var data = new Dictionary<string, decimal> {
            ["IncreaseDecreaseInAccruedLiabilitiesAndOtherOperatingLiabilities"] = -2904m,
            ["IncreaseDecreaseInAccountsPayableTrade"] = 373m,
        };

        decimal result = ScoringService.ResolveWorkingCapitalChange(data, WcBalanceTypes);

        // Both debit → kept: -2904 + 373 = -2531
        Assert.Equal(-2531m, result);
    }

    #endregion

    #region ComputeDerivedMetrics tests

    private static Dictionary<int, IReadOnlyDictionary<string, decimal>> MakeSingleYearData(
        int year, Dictionary<string, decimal> data) {
        return new Dictionary<int, IReadOnlyDictionary<string, decimal>> {
            [year] = data,
        };
    }

    [Fact]
    public void ComputeDerivedMetrics_BookValue_SubtractsGoodwillAndIntangibles() {
        var rawData = MakeSingleYearData(2024, new Dictionary<string, decimal> {
            ["StockholdersEquity"] = 500_000_000m,
            ["Goodwill"] = 100_000_000m,
            ["IntangibleAssetsNetExcludingGoodwill"] = 50_000_000m,
        });

        DerivedMetrics metrics = ScoringService.ComputeDerivedMetrics(rawData, 150m, 1_000_000);

        Assert.Equal(350_000_000m, metrics.BookValue);
    }

    [Fact]
    public void ComputeDerivedMetrics_BookValue_DefaultsGoodwillToZero() {
        var rawData = MakeSingleYearData(2024, new Dictionary<string, decimal> {
            ["StockholdersEquity"] = 500_000_000m,
        });

        DerivedMetrics metrics = ScoringService.ComputeDerivedMetrics(rawData, 150m, 1_000_000);

        Assert.Equal(500_000_000m, metrics.BookValue);
    }

    [Fact]
    public void ComputeDerivedMetrics_AdjustedRetainedEarnings_IncludesDividendsAndIssuance() {
        // 3 years of data with dividends and stock issuance
        var rawData = new Dictionary<int, IReadOnlyDictionary<string, decimal>> {
            [2022] = new Dictionary<string, decimal> {
                ["RetainedEarningsAccumulatedDeficit"] = 80_000_000m,
                ["PaymentsOfDividends"] = 5_000_000m,
                ["ProceedsFromIssuanceOfCommonStock"] = 2_000_000m,
                ["PaymentsForRepurchaseOfCommonStock"] = 1_000_000m,
            },
            [2023] = new Dictionary<string, decimal> {
                ["RetainedEarningsAccumulatedDeficit"] = 90_000_000m,
                ["PaymentsOfDividends"] = 6_000_000m,
                ["ProceedsFromIssuanceOfCommonStock"] = 3_000_000m,
                ["PaymentsForRepurchaseOfCommonStock"] = 0m,
            },
            [2024] = new Dictionary<string, decimal> {
                ["StockholdersEquity"] = 500_000_000m,
                ["RetainedEarningsAccumulatedDeficit"] = 100_000_000m,
                ["PaymentsOfDividends"] = 7_000_000m,
                ["ProceedsFromIssuanceOfCommonStock"] = 1_000_000m,
                ["PaymentsForRepurchaseOfCommonStock"] = 0m,
            },
        };

        DerivedMetrics metrics = ScoringService.ComputeDerivedMetrics(rawData, 150m, 1_000_000);

        // Current RE = 100M
        // Total dividends = 5 + 6 + 7 = 18M
        // Total stock issuance (net) = (2-1) + (3-0) + (1-0) = 1 + 3 + 1 = 5M
        // Total preferred = 0
        // Adjusted = 100 + 18 - 5 - 0 = 113M
        Assert.NotNull(metrics.AdjustedRetainedEarnings);
        Assert.Equal(113_000_000m, metrics.AdjustedRetainedEarnings!.Value);

        // Oldest RE = 80M (from 2022)
        Assert.Equal(80_000_000m, metrics.OldestRetainedEarnings);
    }

    [Fact]
    public void ComputeDerivedMetrics_NetCashFlow_SubtractsFinancingFromGross() {
        var rawData = MakeSingleYearData(2024, new Dictionary<string, decimal> {
            ["StockholdersEquity"] = 500_000_000m,
            ["CashAndCashEquivalentsPeriodIncreaseDecrease"] = 50_000_000m,
            ["ProceedsFromIssuanceOfLongTermDebt"] = 10_000_000m,
            ["RepaymentsOfLongTermDebt"] = 5_000_000m,
            ["ProceedsFromIssuanceOfCommonStock"] = 3_000_000m,
            ["PaymentsForRepurchaseOfCommonStock"] = 1_000_000m,
        });

        DerivedMetrics metrics = ScoringService.ComputeDerivedMetrics(rawData, 150m, 1_000_000);

        // NCF = 50M - ((10-5) + (3-1) + 0) = 50 - (5 + 2 + 0) = 50 - 7 = 43M
        Assert.NotNull(metrics.AverageNetCashFlow);
        Assert.Equal(43_000_000m, metrics.AverageNetCashFlow!.Value);
    }

    [Fact]
    public void ComputeDerivedMetrics_NetCashFlow_FallsBackToRepaymentsOfDebt() {
        var rawData = MakeSingleYearData(2024, new Dictionary<string, decimal> {
            ["StockholdersEquity"] = 500_000_000m,
            ["CashAndCashEquivalentsPeriodIncreaseDecrease"] = 50_000_000m,
            ["ProceedsFromIssuanceOfLongTermDebt"] = 40_000_000m,
            ["RepaymentsOfDebt"] = 20_000_000m, // fallback (no RepaymentsOfLongTermDebt)
            ["ProceedsFromIssuanceOfCommonStock"] = 3_000_000m,
            ["PaymentsForRepurchaseOfCommonStock"] = 1_000_000m,
        });

        DerivedMetrics metrics = ScoringService.ComputeDerivedMetrics(rawData, 150m, 1_000_000);

        // NCF = 50M - ((40-20) + (3-1) + 0) = 50 - (20 + 2) = 50 - 22 = 28M
        Assert.NotNull(metrics.AverageNetCashFlow);
        Assert.Equal(28_000_000m, metrics.AverageNetCashFlow!.Value);
    }

    [Fact]
    public void ComputeDerivedMetrics_NetCashFlow_FallsBackToRepaymentsOfConvertibleDebt() {
        var rawData = MakeSingleYearData(2024, new Dictionary<string, decimal> {
            ["StockholdersEquity"] = 500_000_000m,
            ["CashAndCashEquivalentsPeriodIncreaseDecrease"] = 50_000_000m,
            ["ProceedsFromIssuanceOfDebt"] = 6_000_000m, // fallback (no LT variant)
            ["RepaymentsOfConvertibleDebt"] = 2_500_000m, // fallback (no LT or generic)
        });

        DerivedMetrics metrics = ScoringService.ComputeDerivedMetrics(rawData, 150m, 1_000_000);

        // NCF = 50M - ((6-2.5) + 0 + 0) = 50 - 3.5 = 46.5M
        Assert.NotNull(metrics.AverageNetCashFlow);
        Assert.Equal(46_500_000m, metrics.AverageNetCashFlow!.Value);
    }

    [Fact]
    public void ComputeDerivedMetrics_NetCashFlow_PrefersRepaymentsOfLongTermDebtOverGeneric() {
        var rawData = MakeSingleYearData(2024, new Dictionary<string, decimal> {
            ["StockholdersEquity"] = 500_000_000m,
            ["CashAndCashEquivalentsPeriodIncreaseDecrease"] = 50_000_000m,
            ["ProceedsFromIssuanceOfLongTermDebt"] = 10_000_000m,
            ["RepaymentsOfLongTermDebt"] = 5_000_000m,
            ["RepaymentsOfDebt"] = 8_000_000m, // should be ignored since LT variant exists
            ["RepaymentsOfConvertibleDebt"] = 3_000_000m, // should be ignored
            ["ProceedsFromIssuanceOfDebt"] = 12_000_000m, // should be ignored since LT variant exists
        });

        DerivedMetrics metrics = ScoringService.ComputeDerivedMetrics(rawData, 150m, 1_000_000);

        // NCF = 50M - ((10-5) + 0 + 0) = 50 - 5 = 45M  (uses LT variants, not broader ones)
        Assert.NotNull(metrics.AverageNetCashFlow);
        Assert.Equal(45_000_000m, metrics.AverageNetCashFlow!.Value);
    }

    [Fact]
    public void ComputeDerivedMetrics_OwnerEarnings_SimplifiedFormula() {
        var rawData = MakeSingleYearData(2024, new Dictionary<string, decimal> {
            ["StockholdersEquity"] = 500_000_000m,
            ["NetIncomeLoss"] = 30_000_000m,
            ["Depletion"] = 1_000_000m,
            ["AmortizationOfIntangibleAssets"] = 2_000_000m,
            ["DeferredIncomeTaxExpenseBenefit"] = 3_000_000m,
            ["OtherNoncashIncomeExpense"] = 500_000m,
            ["PaymentsToAcquirePropertyPlantAndEquipment"] = 10_000_000m,
            ["IncreaseDecreaseInOperatingCapital"] = -2_000_000m,
        });

        DerivedMetrics metrics = ScoringService.ComputeDerivedMetrics(rawData, 150m, 1_000_000);

        // OE = 30 + 1 + 2 + 3 + 0.5 - 10 + (-2) = 24.5M
        Assert.NotNull(metrics.AverageOwnerEarnings);
        Assert.Equal(24_500_000m, metrics.AverageOwnerEarnings!.Value);
    }

    [Fact]
    public void ComputeDerivedMetrics_Averages_DivideBySumOfYears() {
        // 3 years, each with different NCF-contributing values
        var rawData = new Dictionary<int, IReadOnlyDictionary<string, decimal>> {
            [2022] = new Dictionary<string, decimal> {
                ["CashAndCashEquivalentsPeriodIncreaseDecrease"] = 10_000_000m,
                ["NetIncomeLoss"] = 5_000_000m,
            },
            [2023] = new Dictionary<string, decimal> {
                ["CashAndCashEquivalentsPeriodIncreaseDecrease"] = 20_000_000m,
                ["NetIncomeLoss"] = 15_000_000m,
            },
            [2024] = new Dictionary<string, decimal> {
                ["StockholdersEquity"] = 500_000_000m,
                ["CashAndCashEquivalentsPeriodIncreaseDecrease"] = 30_000_000m,
                ["NetIncomeLoss"] = 25_000_000m,
            },
        };

        DerivedMetrics metrics = ScoringService.ComputeDerivedMetrics(rawData, 150m, 1_000_000);

        // Average NCF = (10 + 20 + 30) / 3 = 20M (no financing items to subtract)
        Assert.NotNull(metrics.AverageNetCashFlow);
        Assert.Equal(20_000_000m, metrics.AverageNetCashFlow!.Value);

        // Average OE = (5 + 15 + 25) / 3 = 15M (no non-cash or capex items)
        Assert.NotNull(metrics.AverageOwnerEarnings);
        Assert.Equal(15_000_000m, metrics.AverageOwnerEarnings!.Value);
    }

    [Fact]
    public void ComputeDerivedMetrics_EstimatedReturn_Formula() {
        var rawData = MakeSingleYearData(2024, new Dictionary<string, decimal> {
            ["StockholdersEquity"] = 500_000_000m,
            ["CashAndCashEquivalentsPeriodIncreaseDecrease"] = 50_000_000m,
            ["NetIncomeLoss"] = 40_000_000m,
            ["PaymentsOfDividends"] = 5_000_000m,
        });

        // Price = 100, Shares = 10M → MarketCap = 1B
        DerivedMetrics metrics = ScoringService.ComputeDerivedMetrics(rawData, 100m, 10_000_000);

        Assert.Equal(1_000_000_000m, metrics.MarketCap);

        // EstReturn_CF = 100 × (50M - 5M) / 1B = 100 × 45M / 1B = 4.5%
        Assert.NotNull(metrics.EstimatedReturnCF);
        Assert.Equal(4.5m, metrics.EstimatedReturnCF!.Value);

        // EstReturn_OE = 100 × (40M - 5M) / 1B = 100 × 35M / 1B = 3.5%
        Assert.NotNull(metrics.EstimatedReturnOE);
        Assert.Equal(3.5m, metrics.EstimatedReturnOE!.Value);
    }

    [Fact]
    public void ComputeDerivedMetrics_ReturnsNullMetrics_WhenEquityMissing() {
        var rawData = MakeSingleYearData(2024, new Dictionary<string, decimal> {
            ["NetIncomeLoss"] = 30_000_000m,
        });

        DerivedMetrics metrics = ScoringService.ComputeDerivedMetrics(rawData, 150m, 1_000_000);

        Assert.Null(metrics.BookValue);
        Assert.Null(metrics.DebtToEquityRatio);
        Assert.Null(metrics.PriceToBookRatio);
        Assert.Null(metrics.DebtToBookRatio);
    }

    #endregion

    #region EvaluateChecks tests

    private static DerivedMetrics MakeGoodMetrics() {
        return new DerivedMetrics(
            BookValue: 200_000_000m,
            MarketCap: 500_000_000m,
            DebtToEquityRatio: 0.3m,
            PriceToBookRatio: 2.5m,
            DebtToBookRatio: 0.4m,
            AdjustedRetainedEarnings: 50_000_000m,
            OldestRetainedEarnings: 30_000_000m,
            AverageNetCashFlow: 40_000_000m,
            AverageOwnerEarnings: 35_000_000m,
            EstimatedReturnCF: 7.0m,
            EstimatedReturnOE: 6.0m,
            CurrentDividendsPaid: 5_000_000m);
    }

    [Fact]
    public void EvaluateChecks_AllPass_WhenMetricsAreGood() {
        DerivedMetrics metrics = MakeGoodMetrics();
        IReadOnlyList<ScoringCheck> checks = ScoringService.EvaluateChecks(metrics, 5);

        Assert.Equal(13, checks.Count);

        int passCount = 0;
        foreach (ScoringCheck check in checks) {
            if (check.Result == ScoringCheckResult.Pass)
                passCount++;
        }
        Assert.Equal(13, passCount);
    }

    [Fact]
    public void EvaluateChecks_DebtToEquity_FailsAboveThreshold() {
        DerivedMetrics metrics = MakeGoodMetrics() with { DebtToEquityRatio = 0.6m };
        IReadOnlyList<ScoringCheck> checks = ScoringService.EvaluateChecks(metrics, 5);

        Assert.Equal(ScoringCheckResult.Fail, checks[0].Result);
        Assert.Equal(1, checks[0].CheckNumber);
    }

    [Fact]
    public void EvaluateChecks_BookValue_FailsBelowThreshold() {
        DerivedMetrics metrics = MakeGoodMetrics() with { BookValue = 100_000_000m };
        IReadOnlyList<ScoringCheck> checks = ScoringService.EvaluateChecks(metrics, 5);

        Assert.Equal(ScoringCheckResult.Fail, checks[1].Result);
        Assert.Equal(2, checks[1].CheckNumber);
    }

    [Fact]
    public void EvaluateChecks_NotAvailable_WhenMetricIsNull() {
        DerivedMetrics metrics = MakeGoodMetrics() with {
            BookValue = null,
            PriceToBookRatio = null,
            DebtToBookRatio = null,
        };
        IReadOnlyList<ScoringCheck> checks = ScoringService.EvaluateChecks(metrics, 5);

        Assert.Equal(ScoringCheckResult.NotAvailable, checks[1].Result); // Book Value
        Assert.Equal(ScoringCheckResult.NotAvailable, checks[2].Result); // Price-to-Book
        Assert.Equal(ScoringCheckResult.NotAvailable, checks[9].Result); // Debt-to-Book
    }

    [Fact]
    public void EvaluateChecks_HistoryCheck_FailsWithLessThanFourYears() {
        DerivedMetrics metrics = MakeGoodMetrics();
        IReadOnlyList<ScoringCheck> checks = ScoringService.EvaluateChecks(metrics, 3);

        Assert.Equal(ScoringCheckResult.Fail, checks[11].Result);
        Assert.Equal(12, checks[11].CheckNumber);
    }

    [Fact]
    public void EvaluateChecks_RetainedEarningsIncreased_ComparesCurrentToOldest() {
        // Adjusted > Oldest → pass
        DerivedMetrics metricsPass = MakeGoodMetrics() with {
            AdjustedRetainedEarnings = 50_000_000m,
            OldestRetainedEarnings = 30_000_000m,
        };
        IReadOnlyList<ScoringCheck> checksPass = ScoringService.EvaluateChecks(metricsPass, 5);
        Assert.Equal(ScoringCheckResult.Pass, checksPass[12].Result);

        // Adjusted < Oldest → fail
        DerivedMetrics metricsFail = MakeGoodMetrics() with {
            AdjustedRetainedEarnings = 20_000_000m,
            OldestRetainedEarnings = 30_000_000m,
        };
        IReadOnlyList<ScoringCheck> checksFail = ScoringService.EvaluateChecks(metricsFail, 5);
        Assert.Equal(ScoringCheckResult.Fail, checksFail[12].Result);
    }

    #endregion

    #region Integration test

    [Fact]
    public async Task ComputeScore_EndToEnd_WithInMemoryData() {
        var dbm = new DbmInMemoryService();
        var ct = CancellationToken.None;

        // Seed company
        await dbm.BulkInsertCompanies([new Company(1, 320193, "EDGAR")], ct);
        await dbm.BulkInsertCompanyNames([new CompanyName(1, 1, "Apple Inc.")], ct);
        await dbm.BulkInsertCompanyTickers([new CompanyTicker(1, "AAPL", "NASDAQ")], ct);

        // Seed taxonomy
        await dbm.EnsureTaxonomyType("us-gaap", 2024, ct);
        await dbm.BulkInsertTaxonomyConcepts([
            new ConceptDetailsDTO(100, 1, 1, 0, false, "StockholdersEquity", "", ""),
            new ConceptDetailsDTO(101, 1, 1, 0, false, "RetainedEarningsAccumulatedDeficit", "", ""),
            new ConceptDetailsDTO(102, 1, 2, 0, false, "NetIncomeLoss", "", ""),
            new ConceptDetailsDTO(103, 1, 2, 0, false, "CashAndCashEquivalentsPeriodIncreaseDecrease", "", ""),
            new ConceptDetailsDTO(104, 1, 1, 0, false, "CommonStockSharesOutstanding", "", ""),
            new ConceptDetailsDTO(105, 1, 1, 0, false, "Goodwill", "", ""),
            new ConceptDetailsDTO(106, 1, 1, 0, false, "LongTermDebt", "", ""),
            new ConceptDetailsDTO(107, 1, 2, 0, false, "PaymentsOfDividends", "", ""),
            new ConceptDetailsDTO(108, 1, 2, 0, false, "PaymentsToAcquirePropertyPlantAndEquipment", "", ""),
        ], ct);

        // Seed 5 years of 10-K filings with data
        var submissions = new List<Submission>();
        var dataPoints = new List<DataPoint>();
        ulong dpId = 1000;

        for (int year = 2020; year <= 2024; year++) {
            ulong subId = (ulong)(10 + year - 2020);
            var reportDate = new DateOnly(year, 9, 28);
            submissions.Add(new Submission(subId, 1, $"ref-{year}", FilingType.TenK,
                FilingCategory.Annual, reportDate, null));

            decimal equity = 300_000_000m + (year - 2020) * 20_000_000m;
            decimal retainedEarnings = 50_000_000m + (year - 2020) * 10_000_000m;
            decimal netIncome = 40_000_000m + (year - 2020) * 5_000_000m;
            decimal cashChange = 30_000_000m + (year - 2020) * 3_000_000m;

            var unit = new DataPointUnit(1, "USD");

            dataPoints.Add(new DataPoint(dpId++, 1, "f", "r",
                new DatePair(reportDate, reportDate), equity, unit, reportDate, subId, 100));
            dataPoints.Add(new DataPoint(dpId++, 1, "f", "r",
                new DatePair(reportDate, reportDate), retainedEarnings, unit, reportDate, subId, 101));
            dataPoints.Add(new DataPoint(dpId++, 1, "f", "r",
                new DatePair(reportDate, reportDate), netIncome, unit, reportDate, subId, 102));
            dataPoints.Add(new DataPoint(dpId++, 1, "f", "r",
                new DatePair(reportDate, reportDate), cashChange, unit, reportDate, subId, 103));
            dataPoints.Add(new DataPoint(dpId++, 1, "f", "r",
                new DatePair(reportDate, reportDate), 15_000_000_000m, unit, reportDate, subId, 104));
            dataPoints.Add(new DataPoint(dpId++, 1, "f", "r",
                new DatePair(reportDate, reportDate), 50_000_000m, unit, reportDate, subId, 105));
            dataPoints.Add(new DataPoint(dpId++, 1, "f", "r",
                new DatePair(reportDate, reportDate), 100_000_000m, unit, reportDate, subId, 106));
            dataPoints.Add(new DataPoint(dpId++, 1, "f", "r",
                new DatePair(reportDate, reportDate), 3_000_000m, unit, reportDate, subId, 107));
            dataPoints.Add(new DataPoint(dpId++, 1, "f", "r",
                new DatePair(reportDate, reportDate), 10_000_000m, unit, reportDate, subId, 108));
        }

        await dbm.BulkInsertSubmissions(submissions, ct);
        await dbm.BulkInsertDataPoints(dataPoints, ct);

        // Seed a price
        await dbm.BulkInsertPrices([
            new PriceRow(1, 320193, "AAPL", "NASDAQ", "AAPL.US",
                new DateOnly(2025, 1, 15), 195m, 198m, 194m, 196m, 50_000_000),
        ], ct);

        // Execute
        var service = new ScoringService(dbm);
        Result<ScoringResult> result = await service.ComputeScore(1, ct);

        Assert.True(result.IsSuccess);
        ScoringResult scoring = result.Value!;

        // Verify structure
        Assert.Equal(13, scoring.Scorecard.Count);
        Assert.Equal(5, scoring.YearsOfData);
        Assert.Equal(196m, scoring.PricePerShare);
        Assert.Equal(new DateOnly(2025, 1, 15), scoring.PriceDate);
        Assert.Equal(15_000_000_000, scoring.SharesOutstanding);
        Assert.True(scoring.OverallScore <= scoring.ComputableChecks);
        Assert.True(scoring.ComputableChecks <= 13);

        // Verify derived metrics are populated
        Assert.NotNull(scoring.Metrics.BookValue);
        Assert.NotNull(scoring.Metrics.MarketCap);
        Assert.NotNull(scoring.Metrics.AverageNetCashFlow);
        Assert.NotNull(scoring.Metrics.AverageOwnerEarnings);
        Assert.NotNull(scoring.Metrics.AdjustedRetainedEarnings);
    }

    #endregion
}
