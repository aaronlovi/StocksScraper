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

    [Fact]
    public void ResolveField_SharesChain_FallsBackToEntityCommonStockSharesOutstanding() {
        var data = new Dictionary<string, decimal> {
            ["EntityCommonStockSharesOutstanding"] = 1_600_000_000m,
        };

        decimal? result = ScoringService.ResolveField(data,
            ScoringService.SharesChain, null);

        Assert.Equal(1_600_000_000m, result);
    }

    [Fact]
    public void ResolveField_SharesChain_PrefersCommonStockSharesOverDei() {
        var data = new Dictionary<string, decimal> {
            ["CommonStockSharesOutstanding"] = 500_000_000m,
            ["EntityCommonStockSharesOutstanding"] = 1_600_000_000m,
        };

        decimal? result = ScoringService.ResolveField(data,
            ScoringService.SharesChain, null);

        Assert.Equal(500_000_000m, result);
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

    #region ResolveTotalDa tests (formerly ResolveDepletionAndAmortization)

    [Fact]
    public void ResolveTotalDa_PrefersCombinedOverComponents() {
        var data = new Dictionary<string, decimal> {
            ["Depletion"] = 1_000_000m,
            ["AmortizationOfIntangibleAssets"] = 2_000_000m,
            ["DepreciationDepletionAndAmortization"] = 11_000_000m,
            ["Depreciation"] = 8_000_000m,
        };

        Assert.Equal(11_000_000m, ScoringService.ResolveTotalDa(data));
    }

    [Fact]
    public void ResolveTotalDa_CombinedIncludesDepreciation() {
        var data = new Dictionary<string, decimal> {
            ["DepreciationDepletionAndAmortization"] = 11_700_000_000m,
            ["Depreciation"] = 8_000_000_000m,
        };

        Assert.Equal(11_700_000_000m, ScoringService.ResolveTotalDa(data));
    }

    [Fact]
    public void ResolveTotalDa_ComponentsWithoutCombined() {
        var data = new Dictionary<string, decimal> {
            ["AmortizationOfIntangibleAssets"] = 2_000_000m,
            ["Depreciation"] = 8_000_000m,
        };

        Assert.Equal(10_000_000m, ScoringService.ResolveTotalDa(data));
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
        ["IncreaseDecreaseInReceivables"] = TaxonomyBalanceTypes.Credit,
        ["IncreaseDecreaseInAccountsAndNotesReceivable"] = TaxonomyBalanceTypes.Credit,
        ["IncreaseDecreaseInInventories"] = TaxonomyBalanceTypes.Credit,
        ["IncreaseDecreaseInPrepaidDeferredExpenseAndOtherAssets"] = TaxonomyBalanceTypes.Credit,
        ["IncreaseDecreaseInPrepaidExpense"] = TaxonomyBalanceTypes.Credit,
        ["IncreaseDecreaseInOtherOperatingAssets"] = TaxonomyBalanceTypes.Credit,
        ["IncreaseDecreaseInOtherCurrentAssets"] = TaxonomyBalanceTypes.Credit,
        ["IncreaseDecreaseInOtherNoncurrentAssets"] = TaxonomyBalanceTypes.Credit,
        ["IncreaseDecreaseInIncomeTaxesReceivable"] = TaxonomyBalanceTypes.Credit,
        ["IncreaseDecreaseInContractWithCustomerAsset"] = TaxonomyBalanceTypes.Credit,
        ["IncreaseDecreaseInAccountsPayable"] = TaxonomyBalanceTypes.Debit,
        ["IncreaseDecreaseInAccountsPayableTrade"] = TaxonomyBalanceTypes.Debit,
        ["IncreaseDecreaseInAccruedLiabilities"] = TaxonomyBalanceTypes.Debit,
        ["IncreaseDecreaseInOtherAccruedLiabilities"] = TaxonomyBalanceTypes.Debit,
        ["IncreaseDecreaseInAccountsPayableAndAccruedLiabilities"] = TaxonomyBalanceTypes.Debit,
        ["IncreaseDecreaseInOtherAccountsPayableAndAccruedLiabilities"] = TaxonomyBalanceTypes.Debit,
        ["IncreaseDecreaseInAccruedLiabilitiesAndOtherOperatingLiabilities"] = TaxonomyBalanceTypes.Debit,
        ["IncreaseDecreaseInDeferredRevenue"] = TaxonomyBalanceTypes.Debit,
        ["IncreaseDecreaseInContractWithCustomerLiability"] = TaxonomyBalanceTypes.Debit,
        ["IncreaseDecreaseInOtherOperatingLiabilities"] = TaxonomyBalanceTypes.Debit,
        ["IncreaseDecreaseInOtherCurrentLiabilities"] = TaxonomyBalanceTypes.Debit,
        ["IncreaseDecreaseInOtherNoncurrentLiabilities"] = TaxonomyBalanceTypes.Debit,
        ["IncreaseDecreaseInSelfInsuranceReserve"] = TaxonomyBalanceTypes.Debit,
        ["IncreaseDecreaseInAccruedIncomeTaxesPayable"] = TaxonomyBalanceTypes.Debit,
        ["IncreaseDecreaseInOperatingLeaseLiability"] = TaxonomyBalanceTypes.Debit,
        ["IncreaseDecreaseInEmployeeRelatedLiabilities"] = TaxonomyBalanceTypes.Debit,
        ["IncreaseDecreaseInInterestPayableNet"] = TaxonomyBalanceTypes.Debit,
        // Aggregate and "other, net" (both credit; weight -1 in the indirect cash-flow calc)
        ["IncreaseDecreaseInOperatingCapital"] = TaxonomyBalanceTypes.Credit,
        ["IncreaseDecreaseInOtherOperatingCapitalNet"] = TaxonomyBalanceTypes.Credit,
    };

    [Fact]
    public void ResolveWorkingCapitalChange_UsesAggregateWhenPresent() {
        var data = new Dictionary<string, decimal> {
            ["IncreaseDecreaseInOperatingCapital"] = -5_000_000m,
            ["IncreaseDecreaseInAccountsReceivable"] = 3_000_000m,
            ["IncreaseDecreaseInInventories"] = -1_000_000m,
        };

        decimal result = ScoringService.ResolveWorkingCapitalChange(data, WcBalanceTypes);

        // Aggregate preferred, negated (credit, taxonomy weight -1): operating capital
        // fell by 5M, which provided 5M of cash. (This test used to assert -5M, the bug.)
        Assert.Equal(5_000_000m, result);
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

    [Fact]
    public void ResolveWorkingCapitalChange_FallsBackToReceivables() {
        // IncreaseDecreaseInReceivables is broader than AccountsReceivable (273 exclusive companies)
        var data = new Dictionary<string, decimal> {
            ["IncreaseDecreaseInReceivables"] = 5000m,
        };

        decimal result = ScoringService.ResolveWorkingCapitalChange(data, WcBalanceTypes);

        Assert.Equal(-5000m, result); // Credit → negated
    }

    [Fact]
    public void ResolveWorkingCapitalChange_PrefersReceivablesOverIndividualAR() {
        var data = new Dictionary<string, decimal> {
            ["IncreaseDecreaseInReceivables"] = 5000m,
            ["IncreaseDecreaseInAccountsReceivable"] = 3000m,  // should be ignored
            ["IncreaseDecreaseInOtherReceivables"] = 2000m,     // should be ignored
        };

        decimal result = ScoringService.ResolveWorkingCapitalChange(data, WcBalanceTypes);

        Assert.Equal(-5000m, result); // Credit → negated (only Receivables used)
    }

    [Fact]
    public void ResolveWorkingCapitalChange_FallsBackToAccountsAndNotesReceivable() {
        var data = new Dictionary<string, decimal> {
            ["IncreaseDecreaseInAccountsAndNotesReceivable"] = 4000m,
        };

        decimal result = ScoringService.ResolveWorkingCapitalChange(data, WcBalanceTypes);

        Assert.Equal(-4000m, result); // Credit → negated
    }

    [Fact]
    public void ResolveWorkingCapitalChange_FallsBackToPrepaidExpense() {
        // PrepaidExpense is a fallback when PrepaidDeferredExpenseAndOtherAssets is missing (205 exclusive)
        var data = new Dictionary<string, decimal> {
            ["IncreaseDecreaseInPrepaidExpense"] = 800m,
        };

        decimal result = ScoringService.ResolveWorkingCapitalChange(data, WcBalanceTypes);

        Assert.Equal(-800m, result); // Credit → negated
    }

    [Fact]
    public void ResolveWorkingCapitalChange_PrefersPrepaidDeferredOverPrepaidExpense() {
        var data = new Dictionary<string, decimal> {
            ["IncreaseDecreaseInPrepaidDeferredExpenseAndOtherAssets"] = 1200m,
            ["IncreaseDecreaseInPrepaidExpense"] = 800m, // should be ignored
        };

        decimal result = ScoringService.ResolveWorkingCapitalChange(data, WcBalanceTypes);

        Assert.Equal(-1200m, result); // Credit → negated (broader concept preferred)
    }

    [Fact]
    public void ResolveWorkingCapitalChange_FallsBackToOtherAccountsPayableAndAccruedLiabilities() {
        // OtherAccountsPayableAndAccruedLiabilities as fallback for AP+AL (62 exclusive)
        var data = new Dictionary<string, decimal> {
            ["IncreaseDecreaseInOtherAccountsPayableAndAccruedLiabilities"] = 3000m,
        };

        decimal result = ScoringService.ResolveWorkingCapitalChange(data, WcBalanceTypes);

        Assert.Equal(3000m, result); // Debit, kept as-is
    }

    [Fact]
    public void ResolveWorkingCapitalChange_FallsBackToOtherAccruedLiabilities() {
        // OtherAccruedLiabilities as fallback for AccruedLiabilities (81 exclusive)
        var data = new Dictionary<string, decimal> {
            ["IncreaseDecreaseInAccountsPayable"] = 1000m,
            ["IncreaseDecreaseInOtherAccruedLiabilities"] = 500m,
        };

        decimal result = ScoringService.ResolveWorkingCapitalChange(data, WcBalanceTypes);

        Assert.Equal(1500m, result); // Both debit, kept as-is
    }

    [Fact]
    public void ResolveWorkingCapitalChange_IncludesContractWithCustomerAsset() {
        // ContractWithCustomerAsset is a separate asset concept (52 exclusive)
        var data = new Dictionary<string, decimal> {
            ["IncreaseDecreaseInContractWithCustomerLiability"] = 2000m,
            ["IncreaseDecreaseInContractWithCustomerAsset"] = 500m,
        };

        decimal result = ScoringService.ResolveWorkingCapitalChange(data, WcBalanceTypes);

        // Liability kept: 2000. Asset negated: -500. Total: 1500
        Assert.Equal(1500m, result);
    }

    [Fact]
    public void ResolveWorkingCapitalChange_IncludesOperatingLeaseLiability() {
        var data = new Dictionary<string, decimal> {
            ["IncreaseDecreaseInAccountsPayable"] = 1000m,
            ["IncreaseDecreaseInOperatingLeaseLiability"] = -500m,
        };

        decimal result = ScoringService.ResolveWorkingCapitalChange(data, WcBalanceTypes);

        Assert.Equal(500m, result); // Both debit, kept as-is
    }

    [Fact]
    public void ResolveWorkingCapitalChange_IncludesEmployeeRelatedLiabilities() {
        var data = new Dictionary<string, decimal> {
            ["IncreaseDecreaseInAccountsPayable"] = 1000m,
            ["IncreaseDecreaseInEmployeeRelatedLiabilities"] = 300m,
        };

        decimal result = ScoringService.ResolveWorkingCapitalChange(data, WcBalanceTypes);

        Assert.Equal(1300m, result); // Both debit, kept as-is
    }

    [Fact]
    public void ResolveWorkingCapitalChange_IncludesInterestPayableNet() {
        var data = new Dictionary<string, decimal> {
            ["IncreaseDecreaseInAccountsReceivable"] = 200m,
            ["IncreaseDecreaseInInterestPayableNet"] = 150m,
        };

        decimal result = ScoringService.ResolveWorkingCapitalChange(data, WcBalanceTypes);

        // AR: credit → negated: -200. InterestPayable: debit → kept: 150. Total: -50
        Assert.Equal(-50m, result);
    }

    [Fact]
    public void ResolveWorkingCapitalChange_IncludesIncomeTaxesReceivable() {
        var data = new Dictionary<string, decimal> {
            ["IncreaseDecreaseInAccountsPayable"] = 500m,
            ["IncreaseDecreaseInIncomeTaxesReceivable"] = 200m,
        };

        decimal result = ScoringService.ResolveWorkingCapitalChange(data, WcBalanceTypes);

        // AP: debit → kept: 500. TaxReceivable: credit → negated: -200. Total: 300
        Assert.Equal(300m, result);
    }

    #endregion

    #region ResolveOtherNonCash tests

    [Fact]
    public void ResolveOtherNonCash_UsesAggregateWhenPresent() {
        var data = new Dictionary<string, decimal> {
            ["OtherNoncashIncomeExpense"] = 500_000m,
            ["OtherNoncashExpense"] = 300_000m,
            ["OtherNoncashIncome"] = 100_000m,
        };

        decimal result = ScoringService.ResolveOtherNonCash(data);

        // Aggregate preferred, negated: positive OtherNoncashIncomeExpense is non-cash income,
        // which is subtracted. (This test used to assert +500k, the bug.)
        Assert.Equal(-500_000m, result);
    }

    [Fact]
    public void ResolveOtherNonCash_SumsComponentsWhenAggregateIsMissing() {
        var data = new Dictionary<string, decimal> {
            ["OtherNoncashExpense"] = 300_000m,
            ["OtherNoncashIncome"] = 100_000m,
        };

        decimal result = ScoringService.ResolveOtherNonCash(data);

        Assert.Equal(200_000m, result); // 300K - 100K
    }

    [Fact]
    public void ResolveOtherNonCash_UsesExpenseOnlyWhenIncomeIsMissing() {
        var data = new Dictionary<string, decimal> {
            ["OtherNoncashExpense"] = 300_000m,
        };

        decimal result = ScoringService.ResolveOtherNonCash(data);

        Assert.Equal(300_000m, result);
    }

    [Fact]
    public void ResolveOtherNonCash_NegatesIncomeOnlyWhenExpenseIsMissing() {
        var data = new Dictionary<string, decimal> {
            ["OtherNoncashIncome"] = 200_000m,
        };

        decimal result = ScoringService.ResolveOtherNonCash(data);

        Assert.Equal(-200_000m, result); // 0 - 200K
    }

    [Fact]
    public void ResolveOtherNonCash_ReturnsZeroWhenNothingAvailable() {
        var data = new Dictionary<string, decimal>();

        decimal result = ScoringService.ResolveOtherNonCash(data);

        Assert.Equal(0m, result);
    }

    #endregion

    #region ComputeDerivedMetrics tests

    private static Dictionary<int, IReadOnlyDictionary<string, decimal>> MakeSingleYearData(
        int year, Dictionary<string, decimal> data) {
        return new Dictionary<int, IReadOnlyDictionary<string, decimal>> {
            [year] = data,
        };
    }

    /// <summary>
    /// Adapter for tests that use the old ComputeDerivedMetrics signature.
    /// Derives mostRecentSnapshot and oldestRetainedEarnings from annual data
    /// (simulating the all-10-K case where annual = most recent).
    /// </summary>
    private static DerivedMetrics CallComputeDerivedMetrics(
        Dictionary<int, IReadOnlyDictionary<string, decimal>> rawData,
        decimal? pricePerShare,
        long? sharesOutstanding,
        IReadOnlyDictionary<string, TaxonomyBalanceTypes>? balanceTypes = null) {
        // Find most recent and oldest years
        int mostRecentYear = int.MinValue;
        int oldestYear = int.MaxValue;
        foreach (int year in rawData.Keys) {
            if (year > mostRecentYear) mostRecentYear = year;
            if (year < oldestYear) oldestYear = year;
        }

        var mostRecentSnapshot = rawData.Count > 0
            ? rawData[mostRecentYear]
            : (IReadOnlyDictionary<string, decimal>)new Dictionary<string, decimal>();

        decimal? oldestRetainedEarnings = null;
        if (rawData.Count > 0 && rawData.ContainsKey(oldestYear))
            oldestRetainedEarnings = ScoringService.ResolveField(rawData[oldestYear], ScoringService.RetainedEarningsChain, null);

        return ScoringService.ComputeDerivedMetrics(
            rawData, mostRecentSnapshot, oldestRetainedEarnings,
            pricePerShare, sharesOutstanding, balanceTypes);
    }

    [Fact]
    public void ComputeDerivedMetrics_BookValue_SubtractsGoodwillAndIntangibles() {
        var rawData = MakeSingleYearData(2024, new Dictionary<string, decimal> {
            ["StockholdersEquity"] = 500_000_000m,
            ["Goodwill"] = 100_000_000m,
            ["IntangibleAssetsNetExcludingGoodwill"] = 50_000_000m,
        });

        DerivedMetrics metrics = CallComputeDerivedMetrics(rawData, 150m, 1_000_000);

        Assert.Equal(350_000_000m, metrics.BookValue);
    }

    [Fact]
    public void ComputeDerivedMetrics_BookValue_DefaultsGoodwillToZero() {
        var rawData = MakeSingleYearData(2024, new Dictionary<string, decimal> {
            ["StockholdersEquity"] = 500_000_000m,
        });

        DerivedMetrics metrics = CallComputeDerivedMetrics(rawData, 150m, 1_000_000);

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

        DerivedMetrics metrics = CallComputeDerivedMetrics(rawData, 150m, 1_000_000);

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

        DerivedMetrics metrics = CallComputeDerivedMetrics(rawData, 150m, 1_000_000);

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

        DerivedMetrics metrics = CallComputeDerivedMetrics(rawData, 150m, 1_000_000);

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

        DerivedMetrics metrics = CallComputeDerivedMetrics(rawData, 150m, 1_000_000);

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
            ["RepaymentsOfDebt"] = 8_000_000m, // all-debt total, covers the long-term 5M
            ["RepaymentsOfConvertibleDebt"] = 3_000_000m, // child of the long-term 5M
            ["ProceedsFromIssuanceOfDebt"] = 12_000_000m, // all-debt total, covers the long-term 10M
        });

        DerivedMetrics metrics = CallComputeDerivedMetrics(rawData, 150m, 1_000_000);

        // Nested per the taxonomy, each parent covering its children: proceeds 12M (total),
        // repayments 8M (total). NCF = 50M - (12 - 8) = 46M. (This test used to assert 45M:
        // first-found took the long-term lines and ignored the larger all-debt totals.)
        Assert.NotNull(metrics.AverageNetCashFlow);
        Assert.Equal(46_000_000m, metrics.AverageNetCashFlow!.Value);
    }

    [Fact]
    public void ComputeDerivedMetrics_NetCashFlow_FallsBackToStockOptionsExercised() {
        var rawData = MakeSingleYearData(2024, new Dictionary<string, decimal> {
            ["StockholdersEquity"] = 500_000_000m,
            ["CashAndCashEquivalentsPeriodIncreaseDecrease"] = 50_000_000m,
            ["ProceedsFromStockOptionsExercised"] = 4_000_000m, // fallback (no IssuanceOfCommonStock)
            ["PaymentsForRepurchaseOfCommonStock"] = 1_000_000m,
        });

        DerivedMetrics metrics = CallComputeDerivedMetrics(rawData, 150m, 1_000_000);

        // NCF = 50M - (0 + (4-1) + 0) = 50 - 3 = 47M
        Assert.NotNull(metrics.AverageNetCashFlow);
        Assert.Equal(47_000_000m, metrics.AverageNetCashFlow!.Value);
    }

    [Fact]
    public void ComputeDerivedMetrics_NetCashFlow_FallsBackToRepurchaseOfEquity() {
        var rawData = MakeSingleYearData(2024, new Dictionary<string, decimal> {
            ["StockholdersEquity"] = 500_000_000m,
            ["CashAndCashEquivalentsPeriodIncreaseDecrease"] = 50_000_000m,
            ["ProceedsFromIssuanceOfCommonStock"] = 3_000_000m,
            ["PaymentsForRepurchaseOfEquity"] = 2_000_000m, // fallback (no RepurchaseOfCommonStock)
        });

        DerivedMetrics metrics = CallComputeDerivedMetrics(rawData, 150m, 1_000_000);

        // NCF = 50M - (0 + (3-2) + 0) = 50 - 1 = 49M
        Assert.NotNull(metrics.AverageNetCashFlow);
        Assert.Equal(49_000_000m, metrics.AverageNetCashFlow!.Value);
    }

    [Fact]
    public void ComputeDerivedMetrics_NetCashFlow_FallsBackToSeniorDebtConcepts() {
        var rawData = MakeSingleYearData(2024, new Dictionary<string, decimal> {
            ["StockholdersEquity"] = 500_000_000m,
            ["CashAndCashEquivalentsPeriodIncreaseDecrease"] = 50_000_000m,
            ["ProceedsFromIssuanceOfSeniorLongTermDebt"] = 15_000_000m,
            ["RepaymentsOfSeniorDebt"] = 5_000_000m,
        });

        DerivedMetrics metrics = CallComputeDerivedMetrics(rawData, 150m, 1_000_000);

        // NCF = 50M - ((15-5) + 0 + 0) = 50 - 10 = 40M
        Assert.NotNull(metrics.AverageNetCashFlow);
        Assert.Equal(40_000_000m, metrics.AverageNetCashFlow!.Value);
    }

    [Fact]
    public void ComputeDerivedMetrics_NetCashFlow_FallsBackToRepaymentsOfNotesPayable() {
        var rawData = MakeSingleYearData(2024, new Dictionary<string, decimal> {
            ["StockholdersEquity"] = 500_000_000m,
            ["CashAndCashEquivalentsPeriodIncreaseDecrease"] = 50_000_000m,
            ["ProceedsFromIssuanceOfDebt"] = 8_000_000m,
            ["RepaymentsOfNotesPayable"] = 3_000_000m, // fallback (no LT/generic/convertible)
        });

        DerivedMetrics metrics = CallComputeDerivedMetrics(rawData, 150m, 1_000_000);

        // NCF = 50M - ((8-3) + 0 + 0) = 50 - 5 = 45M
        Assert.NotNull(metrics.AverageNetCashFlow);
        Assert.Equal(45_000_000m, metrics.AverageNetCashFlow!.Value);
    }

    [Fact]
    public void ComputeDerivedMetrics_NetCashFlow_FallsBackToDividendsCommonStockCash() {
        var rawData = MakeSingleYearData(2024, new Dictionary<string, decimal> {
            ["StockholdersEquity"] = 500_000_000m,
            ["CashAndCashEquivalentsPeriodIncreaseDecrease"] = 50_000_000m,
            ["NetIncomeLoss"] = 40_000_000m,
            ["DividendsCommonStockCash"] = 5_000_000m, // fallback (no PaymentsOfDividends/Common/Dividends)
        });

        // Price = 100, Shares = 10M → MarketCap = 1B
        DerivedMetrics metrics = CallComputeDerivedMetrics(rawData, 100m, 10_000_000);

        // Dividends should be picked up from fallback
        Assert.NotNull(metrics.CurrentDividendsPaid);
        Assert.Equal(5_000_000m, metrics.CurrentDividendsPaid!.Value);
    }

    [Fact]
    public void ComputeDerivedMetrics_NetCashFlow_FallsBackToCashChangeExcludingFx() {
        var rawData = MakeSingleYearData(2024, new Dictionary<string, decimal> {
            ["StockholdersEquity"] = 500_000_000m,
            ["CashCashEquivalentsRestrictedCashAndRestrictedCashEquivalentsPeriodIncreaseDecreaseExcludingExchangeRateEffect"] = 50_000_000m,
        });

        DerivedMetrics metrics = CallComputeDerivedMetrics(rawData, 150m, 1_000_000);

        // NCF = 50M (no financing items)
        Assert.NotNull(metrics.AverageNetCashFlow);
        Assert.Equal(50_000_000m, metrics.AverageNetCashFlow!.Value);
    }

    [Fact]
    public void ComputeDerivedMetrics_OwnerEarnings_FallsBackToDepreciationAndAmortization() {
        var rawData = MakeSingleYearData(2024, new Dictionary<string, decimal> {
            ["StockholdersEquity"] = 500_000_000m,
            ["NetIncomeLoss"] = 30_000_000m,
            ["DepreciationAndAmortization"] = 8_000_000m, // fallback (no DepreciationDepletionAndAmortization)
            ["Depreciation"] = 5_000_000m,
            // A capex tag: without one, a year with D&A has unknown capex and leaves the average
            ["PaymentsToAcquirePropertyPlantAndEquipment"] = 2_000_000m,
        });

        DerivedMetrics metrics = CallComputeDerivedMetrics(rawData, 150m, 1_000_000);

        // OE = 30 + 8 (D&A, depreciation included) - 2 = 36M. Before E1 the add-back was
        // D&A - depreciation (3M): depreciation was never added back.
        Assert.NotNull(metrics.AverageOwnerEarnings);
        Assert.Equal(36_000_000m, metrics.AverageOwnerEarnings!.Value);
    }

    [Fact]
    public void ComputeDerivedMetrics_OwnerEarnings_UsesOtherNonCashComponents() {
        var rawData = MakeSingleYearData(2024, new Dictionary<string, decimal> {
            ["StockholdersEquity"] = 500_000_000m,
            ["NetIncomeLoss"] = 30_000_000m,
            ["OtherNoncashExpense"] = 3_000_000m,
            ["OtherNoncashIncome"] = 1_000_000m,
        });

        DerivedMetrics metrics = CallComputeDerivedMetrics(rawData, 150m, 1_000_000);

        // OE = 30 + 0 + 0 + (3-1) - 0 + 0 = 32M
        Assert.NotNull(metrics.AverageOwnerEarnings);
        Assert.Equal(32_000_000m, metrics.AverageOwnerEarnings!.Value);
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

        DerivedMetrics metrics = CallComputeDerivedMetrics(rawData, 150m, 1_000_000);

        // Taxonomy signs: OtherNoncashIncomeExpense +0.5 is income (-0.5); operating capital
        // -2 is a decrease (+2 cash). OE = 30 + 1 + 2 + 3 - 0.5 - 10 + 2 = 27.5M
        // (this test used to assert 24.5M, the old signs).
        Assert.NotNull(metrics.AverageOwnerEarnings);
        Assert.Equal(27_500_000m, metrics.AverageOwnerEarnings!.Value);
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

        DerivedMetrics metrics = CallComputeDerivedMetrics(rawData, 150m, 1_000_000);

        // Average NCF = (10 + 20 + 30) / 3 = 20M (no financing items to subtract)
        Assert.NotNull(metrics.AverageNetCashFlow);
        Assert.Equal(20_000_000m, metrics.AverageNetCashFlow!.Value);

        // Average OE = (5 + 15 + 25) / 3 = 15M (no non-cash or capex items)
        Assert.NotNull(metrics.AverageOwnerEarnings);
        Assert.Equal(15_000_000m, metrics.AverageOwnerEarnings!.Value);
    }

    [Fact]
    public void ComputeDerivedMetrics_AverageRoeCF_AcrossMultipleYears() {
        // Each year has cash flow and equity for ROE(CF) computation
        var rawData = new Dictionary<int, IReadOnlyDictionary<string, decimal>> {
            [2022] = new Dictionary<string, decimal> {
                ["StockholdersEquity"] = 200_000_000m,
                ["CashAndCashEquivalentsPeriodIncreaseDecrease"] = 20_000_000m,
            },
            [2023] = new Dictionary<string, decimal> {
                ["StockholdersEquity"] = 250_000_000m,
                ["CashAndCashEquivalentsPeriodIncreaseDecrease"] = 30_000_000m,
            },
            [2024] = new Dictionary<string, decimal> {
                ["StockholdersEquity"] = 400_000_000m,
                ["CashAndCashEquivalentsPeriodIncreaseDecrease"] = 40_000_000m,
            },
        };

        DerivedMetrics metrics = CallComputeDerivedMetrics(rawData, 150m, 1_000_000);

        // ROE(CF) per year: 2022 = 100*20/200 = 10%, 2023 = 100*30/250 = 12%, 2024 = 100*40/400 = 10%
        // Average = (10 + 12 + 10) / 3 = 32/3
        Assert.NotNull(metrics.AverageRoeCF);
        Assert.Equal(32m / 3m, metrics.AverageRoeCF!.Value);
    }

    [Fact]
    public void ComputeDerivedMetrics_AverageRoeOE_AcrossMultipleYears() {
        // Each year has net income (for OE) and equity
        var rawData = new Dictionary<int, IReadOnlyDictionary<string, decimal>> {
            [2022] = new Dictionary<string, decimal> {
                ["StockholdersEquity"] = 200_000_000m,
                ["NetIncomeLoss"] = 20_000_000m,
            },
            [2023] = new Dictionary<string, decimal> {
                ["StockholdersEquity"] = 250_000_000m,
                ["NetIncomeLoss"] = 30_000_000m,
            },
            [2024] = new Dictionary<string, decimal> {
                ["StockholdersEquity"] = 400_000_000m,
                ["NetIncomeLoss"] = 40_000_000m,
            },
        };

        DerivedMetrics metrics = CallComputeDerivedMetrics(rawData, 150m, 1_000_000);

        // OE per year = NetIncome (no non-cash or capex items)
        // ROE(OE) per year: 2022 = 100*20/200 = 10%, 2023 = 100*30/250 = 12%, 2024 = 100*40/400 = 10%
        // Average = (10 + 12 + 10) / 3 = 32/3
        Assert.NotNull(metrics.AverageRoeOE);
        Assert.Equal(32m / 3m, metrics.AverageRoeOE!.Value);
    }

    [Fact]
    public void ComputeDerivedMetrics_AverageRoeCF_NullWhenNoCashFlow() {
        var rawData = MakeSingleYearData(2024, new Dictionary<string, decimal> {
            ["StockholdersEquity"] = 200_000_000m,
            ["NetIncomeLoss"] = 30_000_000m,
        });

        DerivedMetrics metrics = CallComputeDerivedMetrics(rawData, 150m, 1_000_000);

        Assert.Null(metrics.AverageRoeCF);
    }

    [Fact]
    public void ComputeDerivedMetrics_AverageRoeOE_NullWhenNoNetIncome() {
        var rawData = MakeSingleYearData(2024, new Dictionary<string, decimal> {
            ["StockholdersEquity"] = 200_000_000m,
            ["CashAndCashEquivalentsPeriodIncreaseDecrease"] = 30_000_000m,
        });

        DerivedMetrics metrics = CallComputeDerivedMetrics(rawData, 150m, 1_000_000);

        Assert.Null(metrics.AverageRoeOE);
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
        DerivedMetrics metrics = CallComputeDerivedMetrics(rawData, 100m, 10_000_000);

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

        DerivedMetrics metrics = CallComputeDerivedMetrics(rawData, 150m, 1_000_000);

        Assert.Null(metrics.BookValue);
        Assert.Null(metrics.DebtToEquityRatio);
        Assert.Null(metrics.PriceToBookRatio);
        Assert.Null(metrics.DebtToBookRatio);
    }

    #endregion

    #region GroupAndPartitionData tests

    [Fact]
    public void GroupAndPartitionData_MixedTenKAndTenQ_PartitionsCorrectly() {
        // 2 years of 10-K data + 1 more recent 10-Q
        var values = new List<ScoringConceptValue> {
            // 10-K 2023
            new("StockholdersEquity", 400_000_000m, new DateOnly(2023, 12, 31), 1, 1),
            new("RetainedEarningsAccumulatedDeficit", 200_000_000m, new DateOnly(2023, 12, 31), 2, 1),
            new("NetIncomeLoss", 50_000_000m, new DateOnly(2023, 12, 31), 1, 1),
            // 10-K 2024
            new("StockholdersEquity", 500_000_000m, new DateOnly(2024, 12, 31), 1, 1),
            new("RetainedEarningsAccumulatedDeficit", 250_000_000m, new DateOnly(2024, 12, 31), 2, 1),
            new("NetIncomeLoss", 60_000_000m, new DateOnly(2024, 12, 31), 1, 1),
            // 10-Q 2025 Q1 (most recent)
            new("StockholdersEquity", 520_000_000m, new DateOnly(2025, 3, 31), 1, 2),
            new("RetainedEarningsAccumulatedDeficit", 270_000_000m, new DateOnly(2025, 3, 31), 2, 2),
        };

        ScoringService.GroupedScoringData grouped = ScoringService.GroupAndPartitionData(values);

        // Annual partition should only have 10-K years
        Assert.Equal(2, grouped.AnnualByYear.Count);
        Assert.True(grouped.AnnualByYear.ContainsKey(2023));
        Assert.True(grouped.AnnualByYear.ContainsKey(2024));
        Assert.False(grouped.AnnualByYear.ContainsKey(2025));

        // Most recent snapshot should use the 10-Q data (2025-03-31)
        Assert.Equal(520_000_000m, grouped.MostRecentSnapshot["StockholdersEquity"]);
        Assert.Equal(270_000_000m, grouped.MostRecentSnapshot["RetainedEarningsAccumulatedDeficit"]);

        // Oldest retained earnings should be from 2023 (oldest date across all types)
        Assert.Equal(200_000_000m, grouped.OldestRetainedEarnings);
    }

    [Fact]
    public void GroupAndPartitionData_TenKOnly_IdenticalToPreviousBehavior() {
        var values = new List<ScoringConceptValue> {
            new("StockholdersEquity", 400_000_000m, new DateOnly(2023, 12, 31), 1, 1),
            new("RetainedEarningsAccumulatedDeficit", 200_000_000m, new DateOnly(2023, 12, 31), 2, 1),
            new("StockholdersEquity", 500_000_000m, new DateOnly(2024, 12, 31), 1, 1),
            new("RetainedEarningsAccumulatedDeficit", 250_000_000m, new DateOnly(2024, 12, 31), 2, 1),
        };

        ScoringService.GroupedScoringData grouped = ScoringService.GroupAndPartitionData(values);

        Assert.Equal(2, grouped.AnnualByYear.Count);
        // Most recent snapshot uses the most recent 10-K date
        Assert.Equal(500_000_000m, grouped.MostRecentSnapshot["StockholdersEquity"]);
        // Oldest retained earnings from 2023
        Assert.Equal(200_000_000m, grouped.OldestRetainedEarnings);
    }

    [Fact]
    public void GroupAndPartitionData_TenQOnly_NoAnnualData() {
        var values = new List<ScoringConceptValue> {
            new("StockholdersEquity", 300_000_000m, new DateOnly(2025, 3, 31), 1, 2),
        };

        ScoringService.GroupedScoringData grouped = ScoringService.GroupAndPartitionData(values);

        Assert.Empty(grouped.AnnualByYear);
        Assert.Equal(300_000_000m, grouped.MostRecentSnapshot["StockholdersEquity"]);
    }

    [Fact]
    public void ComputeDerivedMetrics_QuarterlyBalanceSheet_AnnualAverages() {
        // Annual data: 2 years
        var annualData = new Dictionary<int, IReadOnlyDictionary<string, decimal>> {
            [2023] = new Dictionary<string, decimal> {
                ["StockholdersEquity"] = 400_000_000m,
                ["Goodwill"] = 50_000_000m,
                ["LongTermDebt"] = 100_000_000m,
                ["NetIncomeLoss"] = 40_000_000m,
                ["CashCashEquivalentsRestrictedCashAndRestrictedCashEquivalentsPeriodIncreaseDecreaseIncludingExchangeRateEffect"] = 30_000_000m,
            },
            [2024] = new Dictionary<string, decimal> {
                ["StockholdersEquity"] = 500_000_000m,
                ["Goodwill"] = 50_000_000m,
                ["LongTermDebt"] = 100_000_000m,
                ["NetIncomeLoss"] = 50_000_000m,
                ["CashCashEquivalentsRestrictedCashAndRestrictedCashEquivalentsPeriodIncreaseDecreaseIncludingExchangeRateEffect"] = 40_000_000m,
            },
        };

        // Most recent snapshot from 10-Q with updated balance sheet
        var mostRecentSnapshot = new Dictionary<string, decimal> {
            ["StockholdersEquity"] = 520_000_000m,
            ["Goodwill"] = 55_000_000m,
            ["LongTermDebt"] = 90_000_000m,
            ["RetainedEarningsAccumulatedDeficit"] = 300_000_000m,
        };

        decimal? oldestRetainedEarnings = 200_000_000m;

        DerivedMetrics metrics = ScoringService.ComputeDerivedMetrics(
            annualData, mostRecentSnapshot, oldestRetainedEarnings, 150m, 1_000_000);

        // Balance sheet values should come from quarterly snapshot
        // BookValue = Equity(520M) - Goodwill(55M) - Intangibles(0) = 465M
        Assert.Equal(465_000_000m, metrics.BookValue);
        // Debt-to-Equity = 90M / 520M
        Assert.NotNull(metrics.DebtToEquityRatio);
        Assert.Equal(90_000_000m / 520_000_000m, metrics.DebtToEquityRatio!.Value);

        // Cash flow averages from annual data: (30M + 40M) / 2 = 35M
        Assert.NotNull(metrics.AverageNetCashFlow);
        Assert.Equal(35_000_000m, metrics.AverageNetCashFlow!.Value);

        // OE averages from annual data: (40M + 50M) / 2 = 45M (no non-cash or capex)
        Assert.NotNull(metrics.AverageOwnerEarnings);
        Assert.Equal(45_000_000m, metrics.AverageOwnerEarnings!.Value);

        // Oldest retained earnings passed directly
        Assert.Equal(200_000_000m, metrics.OldestRetainedEarnings);
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
            AverageRoeCF: 10.0m,
            AverageRoeOE: 10.0m,
            EstimatedReturnCF: 7.0m,
            EstimatedReturnOE: 6.0m,
            CurrentDividendsPaid: 5_000_000m);
    }

    [Fact]
    public void EvaluateChecks_AllPass_WhenMetricsAreGood() {
        DerivedMetrics metrics = MakeGoodMetrics();
        IReadOnlyList<ScoringCheck> checks = ScoringService.EvaluateChecks(metrics, 5);

        Assert.Equal(15, checks.Count);

        int passCount = 0;
        foreach (ScoringCheck check in checks) {
            if (check.Result == ScoringCheckResult.Pass)
                passCount++;
        }
        Assert.Equal(15, passCount);
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

    #region ComputeMaxBuyPrice tests

    [Fact]
    public void ComputeMaxBuyPrice_TypicalValues_ReturnsMinimumDividedByShares() {
        // bookValue = 600M, avgNCF = 50M, avgOE = 40M, dividends = 5M, shares = 10M
        // val1 = 3 × 600M = 1800M
        // val2 = 20 × (50M - 5M) = 900M
        // val3 = 20 × (40M - 5M) = 700M  ← min
        // maxBuyPrice = 700M / 10M = 70
        decimal? result = ScoringService.ComputeMaxBuyPrice(
            600_000_000m, 50_000_000m, 40_000_000m, 5_000_000m, 10_000_000);

        Assert.NotNull(result);
        Assert.Equal(70m, result!.Value);
    }

    [Fact]
    public void ComputeMaxBuyPrice_ReturnsNull_WhenBookValueMissing() {
        decimal? result = ScoringService.ComputeMaxBuyPrice(
            null, 50_000_000m, 40_000_000m, 5_000_000m, 10_000_000);

        Assert.Null(result);
    }

    [Fact]
    public void ComputeMaxBuyPrice_ReturnsNull_WhenSharesZero() {
        decimal? result = ScoringService.ComputeMaxBuyPrice(
            600_000_000m, 50_000_000m, 40_000_000m, 5_000_000m, 0);

        Assert.Null(result);
    }

    [Fact]
    public void ComputeMaxBuyPrice_ReturnsNull_WhenSharesNegative() {
        decimal? result = ScoringService.ComputeMaxBuyPrice(
            600_000_000m, 50_000_000m, 40_000_000m, 5_000_000m, -1);

        Assert.Null(result);
    }

    [Fact]
    public void ComputeMaxBuyPrice_ReturnsNull_WhenAvgNCFMissing() {
        decimal? result = ScoringService.ComputeMaxBuyPrice(
            600_000_000m, null, 40_000_000m, 5_000_000m, 10_000_000);

        Assert.Null(result);
    }

    [Fact]
    public void ComputeMaxBuyPrice_ReturnsNull_WhenDividendsMissing() {
        decimal? result = ScoringService.ComputeMaxBuyPrice(
            600_000_000m, 50_000_000m, 40_000_000m, null, 10_000_000);

        Assert.Null(result);
    }

    #endregion

    #region ComputePercentageUpside tests

    [Fact]
    public void ComputePercentageUpside_TypicalValues_ReturnsCorrectPercentage() {
        // maxBuyPrice = 70, price = 50 → upside = (70-50)/50 × 100 = 40%
        decimal? result = ScoringService.ComputePercentageUpside(70m, 50m);

        Assert.NotNull(result);
        Assert.Equal(40m, result!.Value);
    }

    [Fact]
    public void ComputePercentageUpside_Negative_WhenOverpriced() {
        // maxBuyPrice = 30, price = 50 → upside = (30-50)/50 × 100 = -40%
        decimal? result = ScoringService.ComputePercentageUpside(30m, 50m);

        Assert.NotNull(result);
        Assert.Equal(-40m, result!.Value);
    }

    [Fact]
    public void ComputePercentageUpside_ReturnsNull_WhenMaxBuyPriceMissing() {
        decimal? result = ScoringService.ComputePercentageUpside(null, 50m);

        Assert.Null(result);
    }

    [Fact]
    public void ComputePercentageUpside_ReturnsNull_WhenPriceZero() {
        decimal? result = ScoringService.ComputePercentageUpside(70m, 0m);

        Assert.Null(result);
    }

    [Fact]
    public void ComputePercentageUpside_ReturnsNull_WhenPriceMissing() {
        decimal? result = ScoringService.ComputePercentageUpside(70m, null);

        Assert.Null(result);
    }

    [Fact]
    public void EvaluateChecks_AverageRoeCF_PassesAt10Percent() {
        DerivedMetrics metrics = MakeGoodMetrics() with { AverageRoeCF = 10.0m };
        IReadOnlyList<ScoringCheck> checks = ScoringService.EvaluateChecks(metrics, 5);

        Assert.Equal(14, checks[13].CheckNumber);
        Assert.Equal(ScoringCheckResult.Pass, checks[13].Result);
    }

    [Fact]
    public void EvaluateChecks_AverageRoeCF_FailsBelow10Percent() {
        DerivedMetrics metrics = MakeGoodMetrics() with { AverageRoeCF = 9.99m };
        IReadOnlyList<ScoringCheck> checks = ScoringService.EvaluateChecks(metrics, 5);

        Assert.Equal(14, checks[13].CheckNumber);
        Assert.Equal(ScoringCheckResult.Fail, checks[13].Result);
    }

    [Fact]
    public void EvaluateChecks_AverageRoeCF_NotAvailableWhenNull() {
        DerivedMetrics metrics = MakeGoodMetrics() with { AverageRoeCF = null };
        IReadOnlyList<ScoringCheck> checks = ScoringService.EvaluateChecks(metrics, 5);

        Assert.Equal(14, checks[13].CheckNumber);
        Assert.Equal(ScoringCheckResult.NotAvailable, checks[13].Result);
    }

    [Fact]
    public void EvaluateChecks_AverageRoeOE_PassesAt10Percent() {
        DerivedMetrics metrics = MakeGoodMetrics() with { AverageRoeOE = 10.0m };
        IReadOnlyList<ScoringCheck> checks = ScoringService.EvaluateChecks(metrics, 5);

        Assert.Equal(15, checks[14].CheckNumber);
        Assert.Equal(ScoringCheckResult.Pass, checks[14].Result);
    }

    [Fact]
    public void EvaluateChecks_AverageRoeOE_FailsBelow10Percent() {
        DerivedMetrics metrics = MakeGoodMetrics() with { AverageRoeOE = 9.99m };
        IReadOnlyList<ScoringCheck> checks = ScoringService.EvaluateChecks(metrics, 5);

        Assert.Equal(15, checks[14].CheckNumber);
        Assert.Equal(ScoringCheckResult.Fail, checks[14].Result);
    }

    [Fact]
    public void EvaluateChecks_AverageRoeOE_NotAvailableWhenNull() {
        DerivedMetrics metrics = MakeGoodMetrics() with { AverageRoeOE = null };
        IReadOnlyList<ScoringCheck> checks = ScoringService.EvaluateChecks(metrics, 5);

        Assert.Equal(15, checks[14].CheckNumber);
        Assert.Equal(ScoringCheckResult.NotAvailable, checks[14].Result);
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
        Assert.Equal(15, scoring.Scorecard.Count);
        Assert.Equal(5, scoring.YearsOfData);
        Assert.Equal(196m, scoring.PricePerShare);
        Assert.Equal(new DateOnly(2025, 1, 15), scoring.PriceDate);
        Assert.Equal(15_000_000_000, scoring.SharesOutstanding);
        Assert.True(scoring.OverallScore <= scoring.ComputableChecks);
        Assert.True(scoring.ComputableChecks <= 15);

        // Verify derived metrics are populated
        Assert.NotNull(scoring.Metrics.BookValue);
        Assert.NotNull(scoring.Metrics.MarketCap);
        Assert.NotNull(scoring.Metrics.AverageNetCashFlow);
        Assert.NotNull(scoring.Metrics.AverageOwnerEarnings);
        Assert.NotNull(scoring.Metrics.AdjustedRetainedEarnings);
    }

    #endregion

    #region Sign rule (P1) tests

    private static ScoringCheckResult CheckResult(IReadOnlyList<ScoringCheck> checks, int number) {
        foreach (ScoringCheck check in checks) {
            if (check.CheckNumber == number)
                return check.Result;
        }
        throw new InvalidOperationException($"No check {number}");
    }

    private static IReadOnlyList<ScoringCheck> EvaluateSingleYear(
        Dictionary<string, decimal> data, decimal? price = null, long? shares = null) {
        DerivedMetrics metrics = CallComputeDerivedMetrics(MakeSingleYearData(2024, data), price, shares);
        return ScoringService.EvaluateChecks(metrics, 5);
    }

    [Fact]
    public void SignRule_NegativeEquity_DebtToEquityFails_AndRatioStillDisplayed() {
        var data = new Dictionary<string, decimal> { ["StockholdersEquity"] = -100m, ["LongTermDebt"] = 500m };
        DerivedMetrics metrics = CallComputeDerivedMetrics(MakeSingleYearData(2024, data), null, null);

        Assert.Equal(-5m, metrics.DebtToEquityRatio);
        Assert.Equal(-100m, metrics.Equity);
        Assert.Equal(ScoringCheckResult.Fail, CheckResult(ScoringService.EvaluateChecks(metrics, 5), 1));
    }

    [Fact]
    public void SignRule_NegativeEquityWithNoDebtTag_DebtToEquityFails() {
        var data = new Dictionary<string, decimal> { ["StockholdersEquity"] = -100m };
        Assert.Equal(ScoringCheckResult.Fail, CheckResult(EvaluateSingleYear(data), 1));
    }

    [Fact]
    public void SignRule_ZeroEquity_DebtToEquityFails() {
        var data = new Dictionary<string, decimal> { ["StockholdersEquity"] = 0m, ["LongTermDebt"] = 500m };
        Assert.Equal(ScoringCheckResult.Fail, CheckResult(EvaluateSingleYear(data), 1));
    }

    [Fact]
    public void SignRule_PositiveEquity_DebtToEquityUnchanged() {
        var data = new Dictionary<string, decimal> { ["StockholdersEquity"] = 1000m, ["LongTermDebt"] = 100m };
        Assert.Equal(ScoringCheckResult.Pass, CheckResult(EvaluateSingleYear(data), 1));
    }

    [Fact]
    public void SignRule_NoEquity_DebtToEquityStaysNotAvailable() {
        var data = new Dictionary<string, decimal> { ["LongTermDebt"] = 100m };
        Assert.Equal(ScoringCheckResult.NotAvailable, CheckResult(EvaluateSingleYear(data), 1));
    }

    [Fact]
    public void SignRule_NegativeBookValue_PriceToBookAndDebtToBookFail() {
        // Equity 100, goodwill 300: book value -200
        var data = new Dictionary<string, decimal> {
            ["StockholdersEquity"] = 100m, ["Goodwill"] = 300m, ["LongTermDebt"] = 500m,
        };
        DerivedMetrics metrics = CallComputeDerivedMetrics(MakeSingleYearData(2024, data), 10m, 100);
        IReadOnlyList<ScoringCheck> checks = ScoringService.EvaluateChecks(metrics, 5);

        Assert.Equal(-5m, metrics.PriceToBookRatio);
        Assert.Equal(-2.5m, metrics.DebtToBookRatio);
        Assert.Equal(ScoringCheckResult.Fail, CheckResult(checks, 3));
        Assert.Equal(ScoringCheckResult.Fail, CheckResult(checks, 10));
    }

    [Fact]
    public void SignRule_ZeroBookValue_PriceToBookAndDebtToBookFail() {
        var data = new Dictionary<string, decimal> {
            ["StockholdersEquity"] = 300m, ["Goodwill"] = 300m, ["LongTermDebt"] = 500m,
        };
        IReadOnlyList<ScoringCheck> checks = EvaluateSingleYear(data, 10m, 100);

        Assert.Equal(ScoringCheckResult.Fail, CheckResult(checks, 3));
        Assert.Equal(ScoringCheckResult.Fail, CheckResult(checks, 10));
    }

    [Fact]
    public void SignRule_NegativeBookValueWithoutMarketCap_PriceToBookStaysNotAvailable() {
        var data = new Dictionary<string, decimal> { ["StockholdersEquity"] = 100m, ["Goodwill"] = 300m };
        IReadOnlyList<ScoringCheck> checks = EvaluateSingleYear(data);

        Assert.Equal(ScoringCheckResult.NotAvailable, CheckResult(checks, 3));
        Assert.Equal(ScoringCheckResult.Fail, CheckResult(checks, 10));
    }

    [Fact]
    public void SignRule_PositiveBookValue_PriceToBookAndDebtToBookUnchanged() {
        var data = new Dictionary<string, decimal> { ["StockholdersEquity"] = 1000m, ["LongTermDebt"] = 100m };
        IReadOnlyList<ScoringCheck> checks = EvaluateSingleYear(data, 10m, 100);

        Assert.Equal(ScoringCheckResult.Pass, CheckResult(checks, 3));
        Assert.Equal(ScoringCheckResult.Pass, CheckResult(checks, 10));
    }

    [Fact]
    public void SignRule_LossOverNegativeEquity_RoeChecksFail_AndAverageStillDisplayed() {
        // NCF -50 and OE -50 over equity -100 give +50%, which must not pass.
        var data = new Dictionary<string, decimal> {
            ["StockholdersEquity"] = -100m,
            ["CashAndCashEquivalentsPeriodIncreaseDecrease"] = -50m,
            ["NetIncomeLoss"] = -50m,
        };
        DerivedMetrics metrics = CallComputeDerivedMetrics(MakeSingleYearData(2024, data), null, null);
        IReadOnlyList<ScoringCheck> checks = ScoringService.EvaluateChecks(metrics, 5);

        Assert.Equal(50m, metrics.AverageRoeCF);
        Assert.Equal(50m, metrics.AverageRoeOE);
        Assert.True(metrics.RoeCfHasNonPositiveEquityYear);
        Assert.True(metrics.RoeOeHasNonPositiveEquityYear);
        Assert.Equal(ScoringCheckResult.Fail, CheckResult(checks, 14));
        Assert.Equal(ScoringCheckResult.Fail, CheckResult(checks, 15));
    }

    [Fact]
    public void SignRule_OneNegativeEquityYearAmongProfitableYears_RoeChecksFail() {
        // 2023: loss over negative equity reads as +50%; with 2024's +50% the average is 50%.
        var rawData = new Dictionary<int, IReadOnlyDictionary<string, decimal>> {
            [2023] = new Dictionary<string, decimal> {
                ["StockholdersEquity"] = -100m,
                ["CashAndCashEquivalentsPeriodIncreaseDecrease"] = -50m,
                ["NetIncomeLoss"] = -50m,
            },
            [2024] = new Dictionary<string, decimal> {
                ["StockholdersEquity"] = 100m,
                ["CashAndCashEquivalentsPeriodIncreaseDecrease"] = 50m,
                ["NetIncomeLoss"] = 50m,
            },
        };
        DerivedMetrics metrics = CallComputeDerivedMetrics(rawData, null, null);
        IReadOnlyList<ScoringCheck> checks = ScoringService.EvaluateChecks(metrics, 5);

        Assert.Equal(ScoringCheckResult.Fail, CheckResult(checks, 14));
        Assert.Equal(ScoringCheckResult.Fail, CheckResult(checks, 15));
    }

    [Fact]
    public void SignRule_ZeroEquityYear_SetsFlagWithoutEnteringAverage() {
        var rawData = new Dictionary<int, IReadOnlyDictionary<string, decimal>> {
            [2023] = new Dictionary<string, decimal> {
                ["StockholdersEquity"] = 0m,
                ["CashAndCashEquivalentsPeriodIncreaseDecrease"] = 50m,
                ["NetIncomeLoss"] = 50m,
            },
            [2024] = new Dictionary<string, decimal> {
                ["StockholdersEquity"] = 100m,
                ["CashAndCashEquivalentsPeriodIncreaseDecrease"] = 50m,
                ["NetIncomeLoss"] = 50m,
            },
        };
        DerivedMetrics metrics = CallComputeDerivedMetrics(rawData, null, null);
        IReadOnlyList<ScoringCheck> checks = ScoringService.EvaluateChecks(metrics, 5);

        Assert.Equal(50m, metrics.AverageRoeCF);
        Assert.Equal(ScoringCheckResult.Fail, CheckResult(checks, 14));
        Assert.Equal(ScoringCheckResult.Fail, CheckResult(checks, 15));
    }

    [Fact]
    public void SignRule_RoeFlagsAreSeparate_CashFlowYearOnly() {
        // Negative-equity year with a cash change but no net income: only ROE (CF) is affected.
        var rawData = new Dictionary<int, IReadOnlyDictionary<string, decimal>> {
            [2023] = new Dictionary<string, decimal> {
                ["StockholdersEquity"] = -100m,
                ["CashAndCashEquivalentsPeriodIncreaseDecrease"] = 50m,
            },
            [2024] = new Dictionary<string, decimal> {
                ["StockholdersEquity"] = 100m,
                ["CashAndCashEquivalentsPeriodIncreaseDecrease"] = 50m,
                ["NetIncomeLoss"] = 50m,
            },
        };
        DerivedMetrics metrics = CallComputeDerivedMetrics(rawData, null, null);
        IReadOnlyList<ScoringCheck> checks = ScoringService.EvaluateChecks(metrics, 5);

        Assert.True(metrics.RoeCfHasNonPositiveEquityYear);
        Assert.False(metrics.RoeOeHasNonPositiveEquityYear);
        Assert.Equal(ScoringCheckResult.Fail, CheckResult(checks, 14));
        Assert.Equal(ScoringCheckResult.Pass, CheckResult(checks, 15));
    }

    [Fact]
    public void SignRule_PositiveEquityYears_RoeChecksUnchanged() {
        var data = new Dictionary<string, decimal> {
            ["StockholdersEquity"] = 100m,
            ["CashAndCashEquivalentsPeriodIncreaseDecrease"] = 50m,
            ["NetIncomeLoss"] = 50m,
        };
        IReadOnlyList<ScoringCheck> checks = EvaluateSingleYear(data);

        Assert.Equal(ScoringCheckResult.Pass, CheckResult(checks, 14));
        Assert.Equal(ScoringCheckResult.Pass, CheckResult(checks, 15));
    }

    #endregion

    #region Latest 10-K balance-sheet fallback (E9) tests

    private static readonly DateOnly TenKDate = new(2025, 12, 31);
    private static readonly DateOnly TenQDate = new(2026, 6, 30);

    [Fact]
    public void LatestTenKFallback_SparseTenQ_DebtComesFromTenK() {
        // CAT-shaped: the 10-Q carries equity only; the 10-K reports long-term debt.
        var values = new List<ScoringConceptValue> {
            new("StockholdersEquity", 1000m, TenKDate, 1, 1),
            new("LongTermDebtNoncurrent", 3000m, TenKDate, 1, 1),
            new("StockholdersEquity", 1100m, TenQDate, 1, 2),
        };

        ScoringService.GroupedScoringData grouped = ScoringService.GroupAndPartitionData(values);

        Assert.Equal(1100m, grouped.MostRecentSnapshot["StockholdersEquity"]);
        Assert.Equal(3000m, ScoringService.ResolveField(grouped.MostRecentSnapshot, ScoringService.DebtChain, 0m));
    }

    [Fact]
    public void LatestTenKFallback_TenQWithOwnDebt_NoFallback() {
        var values = new List<ScoringConceptValue> {
            new("StockholdersEquity", 1000m, TenKDate, 1, 1),
            new("LongTermDebtNoncurrent", 3000m, TenKDate, 1, 1),
            new("StockholdersEquity", 1100m, TenQDate, 1, 2),
            new("LongTermDebt", 2500m, TenQDate, 1, 2),
        };

        ScoringService.GroupedScoringData grouped = ScoringService.GroupAndPartitionData(values);

        Assert.Equal(2500m, ScoringService.ResolveField(grouped.MostRecentSnapshot, ScoringService.DebtChain, 0m));
        Assert.False(grouped.MostRecentSnapshot.ContainsKey("LongTermDebtNoncurrent"));
    }

    [Fact]
    public void LatestTenKFallback_EquityGroupCopiedWhole_NeverMixed() {
        // 10-Q has L&SE but no Liabilities and no direct equity: equity is not derivable.
        // The whole equity group comes from the 10-K; the 10-Q's L&SE is not mixed in.
        var values = new List<ScoringConceptValue> {
            new("LiabilitiesAndStockholdersEquity", 5000m, TenKDate, 1, 1),
            new("Liabilities", 3000m, TenKDate, 1, 1),
            new("MinorityInterest", 100m, TenKDate, 1, 1),
            new("LiabilitiesAndStockholdersEquity", 9000m, TenQDate, 1, 2),
        };

        ScoringService.GroupedScoringData grouped = ScoringService.GroupAndPartitionData(values);

        Assert.Equal(5000m, grouped.MostRecentSnapshot["LiabilitiesAndStockholdersEquity"]);
        Assert.Equal(1900m, ScoringService.ResolveEquity(grouped.MostRecentSnapshot));
    }

    [Fact]
    public void LatestTenKFallback_TenQEquityDerivable_EquityGroupKept() {
        var values = new List<ScoringConceptValue> {
            new("Assets", 5000m, TenKDate, 2, 1),
            new("Liabilities", 3000m, TenKDate, 1, 1),
            new("MinorityInterest", 100m, TenKDate, 1, 1),
            new("Assets", 6000m, TenQDate, 2, 2),
            new("Liabilities", 3500m, TenQDate, 1, 2),
        };

        ScoringService.GroupedScoringData grouped = ScoringService.GroupAndPartitionData(values);

        // 10-Q equity derives (6000 - 3500); its group is not touched, so the 10-K NCI is not mixed in
        Assert.Equal(2500m, ScoringService.ResolveEquity(grouped.MostRecentSnapshot));
    }

    [Fact]
    public void LatestTenKFallback_IndividualFieldsFilledWhenMissing_SharesNot() {
        var values = new List<ScoringConceptValue> {
            new("StockholdersEquity", 1000m, TenKDate, 1, 1),
            new("Goodwill", 200m, TenKDate, 2, 1),
            new("IntangibleAssetsNetExcludingGoodwill", 50m, TenKDate, 2, 1),
            new("RetainedEarningsAccumulatedDeficit", 400m, TenKDate, 1, 1),
            new("CommonStockSharesOutstanding", 77m, TenKDate, 1, 1),
            new("StockholdersEquity", 1100m, TenQDate, 1, 2),
            new("Goodwill", 210m, TenQDate, 2, 2),
        };

        ScoringService.GroupedScoringData grouped = ScoringService.GroupAndPartitionData(values);

        Assert.Equal(210m, grouped.MostRecentSnapshot["Goodwill"]);
        Assert.Equal(50m, grouped.MostRecentSnapshot["IntangibleAssetsNetExcludingGoodwill"]);
        Assert.Equal(400m, grouped.MostRecentSnapshot["RetainedEarningsAccumulatedDeficit"]);
        Assert.Null(ScoringService.ResolveField(grouped.MostRecentSnapshot, ScoringService.SharesChain, null));
    }

    [Fact]
    public void LatestTenKFallback_DurationConceptsNotFilled() {
        var values = new List<ScoringConceptValue> {
            new("StockholdersEquity", 1000m, TenKDate, 1, 1),
            new("NetIncomeLoss", 300m, TenKDate, 1, 1),
            new("StockholdersEquity", 1100m, TenQDate, 1, 2),
        };

        ScoringService.GroupedScoringData grouped = ScoringService.GroupAndPartitionData(values);

        Assert.False(grouped.MostRecentSnapshot.ContainsKey("NetIncomeLoss"));
    }

    [Fact]
    public void LatestTenKFallback_SparseTenQ_DebtToEquityUsesTenKDebt() {
        var values = new List<ScoringConceptValue> {
            new("StockholdersEquity", 1000m, TenKDate, 1, 1),
            new("LongTermDebtNoncurrent", 3000m, TenKDate, 1, 1),
            new("StockholdersEquity", 1000m, TenQDate, 1, 2),
        };
        ScoringService.GroupedScoringData grouped = ScoringService.GroupAndPartitionData(values);
        var annual = new Dictionary<int, IReadOnlyDictionary<string, decimal>>();
        foreach (KeyValuePair<int, Dictionary<string, decimal>> entry in grouped.AnnualByYear)
            annual[entry.Key] = entry.Value;

        DerivedMetrics metrics = ScoringService.ComputeDerivedMetrics(
            annual, grouped.MostRecentSnapshot, grouped.OldestRetainedEarnings, null, null, grouped.BalanceTypes);

        Assert.Equal(3m, metrics.DebtToEquityRatio);
        Assert.Equal(ScoringCheckResult.Fail, CheckResult(ScoringService.EvaluateChecks(metrics, 5), 1));
    }

    #endregion

    #region Taxonomy signs for aggregates (P2) tests

    [Fact]
    public void TaxonomySigns_OperatingCapitalAggregate_IncreaseUsesCash() {
        var data = new Dictionary<string, decimal> { ["IncreaseDecreaseInOperatingCapital"] = 5m };
        Assert.Equal(-5m, ScoringService.ResolveWorkingCapitalChange(data, WcBalanceTypes));
    }

    [Fact]
    public void TaxonomySigns_AggregateAndComponents_AgreeOnSameEconomics() {
        // AR +3 and inventory +2 use 5 of cash; the aggregate reports the same +5 increase.
        var components = new Dictionary<string, decimal> {
            ["IncreaseDecreaseInAccountsReceivable"] = 3m, ["IncreaseDecreaseInInventories"] = 2m,
        };
        var aggregate = new Dictionary<string, decimal> { ["IncreaseDecreaseInOperatingCapital"] = 5m };

        Assert.Equal(-5m, ScoringService.ResolveWorkingCapitalChange(components, WcBalanceTypes));
        Assert.Equal(-5m, ScoringService.ResolveWorkingCapitalChange(aggregate, WcBalanceTypes));
    }

    [Fact]
    public void TaxonomySigns_OtherNoncashAggregate_MatchesIncomeComponent() {
        var aggregate = new Dictionary<string, decimal> { ["OtherNoncashIncomeExpense"] = 4m };
        var component = new Dictionary<string, decimal> { ["OtherNoncashIncome"] = 4m };

        Assert.Equal(-4m, ScoringService.ResolveOtherNonCash(component));
        Assert.Equal(-4m, ScoringService.ResolveOtherNonCash(aggregate));
    }

    [Fact]
    public void TaxonomySigns_OtherOperatingCapitalNet_IsAddedToComponents() {
        var data = new Dictionary<string, decimal> {
            ["IncreaseDecreaseInAccountsReceivable"] = 3m,
            ["IncreaseDecreaseInInventories"] = 2m,
            ["IncreaseDecreaseInAccountsPayable"] = 1m,
            ["IncreaseDecreaseInOtherOperatingCapitalNet"] = 4m,
        };
        // -3 - 2 + 1 - 4 = -8: the "other, net" line is one more component, not the total
        Assert.Equal(-8m, ScoringService.ResolveWorkingCapitalChange(data, WcBalanceTypes));
    }

    [Fact]
    public void TaxonomySigns_OtherOperatingCapitalNet_AloneIsNegated() {
        var data = new Dictionary<string, decimal> { ["IncreaseDecreaseInOtherOperatingCapitalNet"] = 4m };
        Assert.Equal(-4m, ScoringService.ResolveWorkingCapitalChange(data, WcBalanceTypes));
    }

    [Fact]
    public void TaxonomySigns_AggregatePresent_OtherNetChildIgnored() {
        var data = new Dictionary<string, decimal> {
            ["IncreaseDecreaseInOperatingCapital"] = 10m,
            ["IncreaseDecreaseInOtherOperatingCapitalNet"] = 4m,
        };
        Assert.Equal(-10m, ScoringService.ResolveWorkingCapitalChange(data, WcBalanceTypes));
    }

    [Fact]
    public void TaxonomySigns_EveryWorkingCapitalConceptHasTheTaxonomySign() {
        // Each IncreaseDecrease concept the code reads, alone at +1, must move cash by
        // -1 when credit (weight -1) and +1 when debit (weight +1). Balance types are the
        // us-gaap 2025 values in the DB (checked 2026-11-22).
        foreach (string concept in ScoringService.AllConceptNames) {
            if (!concept.StartsWith("IncreaseDecreaseIn", StringComparison.Ordinal))
                continue;
            Assert.True(WcBalanceTypes.ContainsKey(concept), $"{concept} missing from the balance-type map");

            var data = new Dictionary<string, decimal> { [concept] = 1m };
            decimal expected = WcBalanceTypes[concept] == TaxonomyBalanceTypes.Credit ? -1m : 1m;
            Assert.True(expected == ScoringService.ResolveWorkingCapitalChange(data, WcBalanceTypes),
                $"{concept}: expected {expected}");
        }
    }

    #endregion

    #region Other payables as a sibling (E8) tests

    [Fact]
    public void OtherPayables_SummedWithAccountsPayableAndAccrued() {
        // IncreaseDecreaseInOtherAccountsPayableAndAccruedLiabilities is a sibling of AP and
        // accrued under the combined concept, so all three count.
        var data = new Dictionary<string, decimal> {
            ["IncreaseDecreaseInAccountsPayable"] = 1000m,
            ["IncreaseDecreaseInAccruedLiabilities"] = 500m,
            ["IncreaseDecreaseInOtherAccountsPayableAndAccruedLiabilities"] = 300m,
        };
        Assert.Equal(1800m, ScoringService.ResolveWorkingCapitalChange(data, WcBalanceTypes));
    }

    [Fact]
    public void OtherPayables_WithAccountsPayableOnly_BothCount() {
        var data = new Dictionary<string, decimal> {
            ["IncreaseDecreaseInAccountsPayableTrade"] = 1000m,
            ["IncreaseDecreaseInOtherAccountsPayableAndAccruedLiabilities"] = -300m,
        };
        Assert.Equal(700m, ScoringService.ResolveWorkingCapitalChange(data, WcBalanceTypes));
    }

    [Fact]
    public void OtherPayables_CombinedTotalPresent_UsedAlone() {
        var data = new Dictionary<string, decimal> {
            ["IncreaseDecreaseInAccountsPayableAndAccruedLiabilities"] = 2000m,
            ["IncreaseDecreaseInOtherAccountsPayableAndAccruedLiabilities"] = 300m,
            ["IncreaseDecreaseInAccountsPayable"] = 1000m,
        };
        Assert.Equal(2000m, ScoringService.ResolveWorkingCapitalChange(data, WcBalanceTypes));
    }

    #endregion

    #region Financing chains: summing and short-term debt (P3) tests

    private static decimal YearNcf(Dictionary<string, decimal> data, decimal grossCashFlow = 1000m) =>
        ScoringService.ComputeYearNetCashFlow(data, grossCashFlow);

    [Fact]
    public void FinancingChains_LongTermSmallerThanConvertible_BothCounted() {
        // P3a: a parent can't be smaller than its own child, so these are separate lines.
        var rawData = MakeSingleYearData(2024, new Dictionary<string, decimal> {
            ["CashAndCashEquivalentsPeriodIncreaseDecrease"] = 200m,
            ["ProceedsFromIssuanceOfLongTermDebt"] = 50m,
            ["ProceedsFromConvertibleDebt"] = 100m,
        });
        Assert.Equal(50m, CallComputeDerivedMetrics(rawData, null, null).AverageNetCashFlow);
    }

    [Fact]
    public void FinancingChains_ParentCoversChildren_ParentOnly() {
        var data = new Dictionary<string, decimal> {
            ["ProceedsFromIssuanceOfLongTermDebt"] = 100m,
            ["ProceedsFromIssuanceOfSeniorLongTermDebt"] = 60m,
            ["ProceedsFromConvertibleDebt"] = 30m,
        };
        Assert.Equal(900m, YearNcf(data));
    }

    [Fact]
    public void FinancingChains_ParentWithinTolerance_ParentOnly() {
        // 99.95 is within 0.1% of the children's 100: the same line, rounded.
        var data = new Dictionary<string, decimal> {
            ["ProceedsFromIssuanceOfLongTermDebt"] = 99.95m,
            ["ProceedsFromConvertibleDebt"] = 100m,
        };
        Assert.Equal(900.05m, YearNcf(data));
    }

    [Fact]
    public void FinancingChains_EqualChildren_CountedOnce() {
        var data = new Dictionary<string, decimal> {
            ["ProceedsFromIssuanceOfSeniorLongTermDebt"] = 40m,
            ["ProceedsFromConvertibleDebt"] = 40m,
        };
        Assert.Equal(960m, YearNcf(data));
    }

    [Fact]
    public void FinancingChains_TotalCoversLongTerm_TotalOnly() {
        var data = new Dictionary<string, decimal> {
            ["ProceedsFromIssuanceOfDebt"] = 120m,
            ["ProceedsFromIssuanceOfLongTermDebt"] = 100m,
        };
        Assert.Equal(880m, YearNcf(data));
    }

    [Fact]
    public void FinancingChains_TotalSmallerThanLongTerm_BothCounted() {
        var data = new Dictionary<string, decimal> {
            ["ProceedsFromIssuanceOfDebt"] = 50m,
            ["ProceedsFromIssuanceOfLongTermDebt"] = 100m,
        };
        Assert.Equal(850m, YearNcf(data));
    }

    [Fact]
    public void FinancingChains_RepaymentsNested_SameShapeAsProceeds() {
        // Convertible 30 + senior 20 = 50 children; long-term 40 < 50, so 90; total 200 >= 90, so 200.
        var data = new Dictionary<string, decimal> {
            ["RepaymentsOfConvertibleDebt"] = 30m,
            ["RepaymentsOfSeniorDebt"] = 20m,
            ["RepaymentsOfLongTermDebt"] = 40m,
            ["RepaymentsOfDebt"] = 200m,
        };
        Assert.Equal(1200m, YearNcf(data));
    }

    [Fact]
    public void FinancingChains_NotesPayableRepayments_AddedWithoutShortTermTotal() {
        var data = new Dictionary<string, decimal> {
            ["RepaymentsOfLongTermDebt"] = 10m,
            ["RepaymentsOfNotesPayable"] = 5m,
        };
        Assert.Equal(1015m, YearNcf(data));
    }

    [Fact]
    public void FinancingChains_NotesPayableRepayments_IgnoredWhenShortTermTotalPresent() {
        // Notes payable repayments are a child of the net short-term figure.
        var data = new Dictionary<string, decimal> {
            ["RepaymentsOfLongTermDebt"] = 10m,
            ["RepaymentsOfNotesPayable"] = 5m,
            ["ProceedsFromRepaymentsOfShortTermDebt"] = -3m,
        };
        // net debt = -10 + (-3) = -13
        Assert.Equal(1013m, YearNcf(data));
    }

    [Fact]
    public void FinancingChains_LinesOfCredit_NetPreferredOverGross() {
        var data = new Dictionary<string, decimal> {
            ["ProceedsFromRepaymentsOfLinesOfCredit"] = 20m,
            ["ProceedsFromLinesOfCredit"] = 300m,
            ["RepaymentsOfLinesOfCredit"] = 100m,
        };
        Assert.Equal(980m, YearNcf(data));
    }

    [Fact]
    public void FinancingChains_LinesOfCredit_GrossWhenNoNet_PlusCommercialPaper() {
        var data = new Dictionary<string, decimal> {
            ["ProceedsFromLinesOfCredit"] = 30m,
            ["RepaymentsOfLinesOfCredit"] = 10m,
            ["ProceedsFromRepaymentsOfCommercialPaper"] = 5m,
        };
        Assert.Equal(975m, YearNcf(data));
    }

    [Fact]
    public void FinancingChains_ShortTermTotal_PreferredOverLinesOfCredit() {
        var data = new Dictionary<string, decimal> {
            ["ProceedsFromRepaymentsOfShortTermDebt"] = 40m,
            ["ProceedsFromRepaymentsOfLinesOfCredit"] = 20m,
            ["ProceedsFromRepaymentsOfCommercialPaper"] = 5m,
        };
        Assert.Equal(955m, YearNcf(data));
    }

    [Fact]
    public void FinancingChains_StockProceeds_SiblingsSummed() {
        var data = new Dictionary<string, decimal> {
            ["ProceedsFromIssuanceOfCommonStock"] = 4m,
            ["ProceedsFromStockOptionsExercised"] = 2m,
        };
        Assert.Equal(994m, YearNcf(data));
    }

    [Fact]
    public void FinancingChains_StockProceeds_EqualCountedOnce() {
        var data = new Dictionary<string, decimal> {
            ["ProceedsFromIssuanceOfCommonStock"] = 3m,
            ["ProceedsFromStockOptionsExercised"] = 3m,
        };
        Assert.Equal(997m, YearNcf(data));
    }

    [Fact]
    public void FinancingChains_StockProceedsSummed_InAdjustedRetainedEarnings() {
        var rawData = MakeSingleYearData(2024, new Dictionary<string, decimal> {
            ["RetainedEarningsAccumulatedDeficit"] = 100m,
            ["ProceedsFromIssuanceOfCommonStock"] = 4m,
            ["ProceedsFromStockOptionsExercised"] = 2m,
        });
        // 100 + 0 dividends - 6 stock issuance
        Assert.Equal(94m, CallComputeDerivedMetrics(rawData, null, null).AdjustedRetainedEarnings);
    }

    #endregion

    #region CapEx fallback and unknown-capex years (E3) tests

    [Fact]
    public void UnknownCapex_DaWithoutCapexTag_OwnerEarningsUnknown() {
        var rawData = MakeSingleYearData(2024, new Dictionary<string, decimal> {
            ["NetIncomeLoss"] = 100m, ["AmortizationOfIntangibleAssets"] = 30m,
        });
        Assert.Null(CallComputeDerivedMetrics(rawData, null, null).AverageOwnerEarnings);
    }

    [Fact]
    public void UnknownCapex_ProductiveAssetsFallbackTagUsed() {
        var rawData = MakeSingleYearData(2024, new Dictionary<string, decimal> {
            ["NetIncomeLoss"] = 100m, ["AmortizationOfIntangibleAssets"] = 30m,
            ["PaymentsToAcquireProductiveAssets"] = 20m,
        });
        // 100 + 30 - 20
        Assert.Equal(110m, CallComputeDerivedMetrics(rawData, null, null).AverageOwnerEarnings);
    }

    [Fact]
    public void UnknownCapex_PropertyPlantAndEquipmentPreferredOverProductiveAssets() {
        var rawData = MakeSingleYearData(2024, new Dictionary<string, decimal> {
            ["NetIncomeLoss"] = 100m,
            ["PaymentsToAcquirePropertyPlantAndEquipment"] = 20m,
            ["PaymentsToAcquireProductiveAssets"] = 25m,
        });
        Assert.Equal(80m, CallComputeDerivedMetrics(rawData, null, null).AverageOwnerEarnings);
    }

    [Theory]
    [InlineData("PaymentsToAcquireOilAndGasPropertyAndEquipment")]
    [InlineData("PaymentsToAcquireOtherProductiveAssets")]
    [InlineData("PaymentsToAcquireOtherPropertyPlantAndEquipment")]
    [InlineData("PaymentsForCapitalImprovements")]
    public void UnknownCapex_LaterFallbackTagsUsed(string capexConcept) {
        var rawData = MakeSingleYearData(2024, new Dictionary<string, decimal> {
            ["NetIncomeLoss"] = 100m, ["DepreciationDepletionAndAmortization"] = 30m, [capexConcept] = 40m,
        });
        // 100 + 30 D&A - 40 capex
        Assert.Equal(90m, CallComputeDerivedMetrics(rawData, null, null).AverageOwnerEarnings);
    }

    [Fact]
    public void UnknownCapex_RealEstateDevelopment_IsNotCapex() {
        var rawData = MakeSingleYearData(2024, new Dictionary<string, decimal> {
            ["NetIncomeLoss"] = 100m, ["Depreciation"] = 30m, ["PaymentsToDevelopRealEstateAssets"] = 40m,
        });
        Assert.Null(CallComputeDerivedMetrics(rawData, null, null).AverageOwnerEarnings);
    }

    [Fact]
    public void UnknownCapex_NoCapexAndNoDa_CapexIsZero() {
        var rawData = MakeSingleYearData(2024, new Dictionary<string, decimal> { ["NetIncomeLoss"] = 100m });
        Assert.Equal(100m, CallComputeDerivedMetrics(rawData, null, null).AverageOwnerEarnings);
    }

    [Fact]
    public void UnknownCapex_YearLeavesAverage_OtherYearsKept() {
        var rawData = new Dictionary<int, IReadOnlyDictionary<string, decimal>> {
            [2023] = new Dictionary<string, decimal> {
                ["NetIncomeLoss"] = 500m, ["Depreciation"] = 40m,
            },
            [2024] = new Dictionary<string, decimal> {
                ["NetIncomeLoss"] = 100m, ["PaymentsToAcquirePropertyPlantAndEquipment"] = 20m,
            },
        };
        Assert.Equal(80m, CallComputeDerivedMetrics(rawData, null, null).AverageOwnerEarnings);
    }

    [Fact]
    public void UnknownCapex_AllYearsExcluded_OwnerEarningsChecksNotAvailable() {
        var rawData = MakeSingleYearData(2024, new Dictionary<string, decimal> {
            ["StockholdersEquity"] = 1000m,
            ["NetIncomeLoss"] = 100m,
            ["DepreciationDepletionAndAmortization"] = 30m,
        });
        DerivedMetrics metrics = CallComputeDerivedMetrics(rawData, 10m, 100);
        IReadOnlyList<ScoringCheck> checks = ScoringService.EvaluateChecks(metrics, 5);

        Assert.Null(metrics.AverageOwnerEarnings);
        Assert.Null(metrics.AverageRoeOE);
        foreach (int number in new[] { 5, 7, 9, 15 })
            Assert.Equal(ScoringCheckResult.NotAvailable, CheckResult(checks, number));
    }

    [Theory]
    [InlineData("DepreciationDepletionAndAmortization", 30, null, 0, 30)]
    [InlineData("DepreciationAndAmortization", 30, "Depletion", 5, 35)]
    [InlineData("Depreciation", 20, "AmortizationOfIntangibleAssets", 10, 30)]
    [InlineData("Depreciation", 20, "Depletion", 4, 24)]
    public void ResolveTotalDa_FollowsTheChain(string first, int firstValue, string? second, int secondValue, int expected) {
        var data = new Dictionary<string, decimal> { [first] = firstValue };
        if (second is not null)
            data[second] = secondValue;
        Assert.Equal(expected, ScoringService.ResolveTotalDa(data));
    }

    [Fact]
    public void ResolveTotalDa_CombinedTotalUsedOutright() {
        var data = new Dictionary<string, decimal> {
            ["DepreciationDepletionAndAmortization"] = 30m,
            ["Depletion"] = 5m, ["Depreciation"] = 20m, ["AmortizationOfIntangibleAssets"] = 10m,
        };
        Assert.Equal(30m, ScoringService.ResolveTotalDa(data));
    }

    [Fact]
    public void ResolveTotalDa_NothingReported_IsZero() {
        Assert.Equal(0m, ScoringService.ResolveTotalDa(new Dictionary<string, decimal>()));
    }

    #endregion

    #region Depreciation add-back (E1) tests

    private static decimal? OwnerEarningsOf(Dictionary<string, decimal> data) =>
        CallComputeDerivedMetrics(MakeSingleYearData(2024, data), null, null).AverageOwnerEarnings;

    [Fact]
    public void DepreciationAddBack_MaintenanceCapexEqualToDepreciation_OwnerEarningsEqualsNetIncome() {
        var data = new Dictionary<string, decimal> {
            ["NetIncomeLoss"] = 100m, ["Depreciation"] = 20m,
            ["PaymentsToAcquirePropertyPlantAndEquipment"] = 20m,
        };
        Assert.Equal(100m, OwnerEarningsOf(data));
    }

    [Fact]
    public void DepreciationAddBack_OnlyCombinedDdaReported() {
        var data = new Dictionary<string, decimal> {
            ["NetIncomeLoss"] = 100m, ["DepreciationDepletionAndAmortization"] = 20m,
            ["PaymentsToAcquirePropertyPlantAndEquipment"] = 20m,
        };
        Assert.Equal(100m, OwnerEarningsOf(data));
    }

    [Fact]
    public void DepreciationAddBack_DaPlusDepletion() {
        var data = new Dictionary<string, decimal> {
            ["NetIncomeLoss"] = 100m, ["DepreciationAndAmortization"] = 20m, ["Depletion"] = 5m,
            ["PaymentsToAcquirePropertyPlantAndEquipment"] = 10m,
        };
        Assert.Equal(115m, OwnerEarningsOf(data));
    }

    [Fact]
    public void DepreciationAddBack_DaWithAmortization_NotDoubleCounted() {
        // DepreciationAndAmortization already includes amortization.
        var data = new Dictionary<string, decimal> {
            ["NetIncomeLoss"] = 100m, ["DepreciationAndAmortization"] = 20m,
            ["AmortizationOfIntangibleAssets"] = 5m,
            ["PaymentsToAcquirePropertyPlantAndEquipment"] = 10m,
        };
        Assert.Equal(110m, OwnerEarningsOf(data));
    }

    [Fact]
    public void DepreciationAddBack_DepreciationAmortizationAndDepletionSummed() {
        var data = new Dictionary<string, decimal> {
            ["NetIncomeLoss"] = 100m, ["Depreciation"] = 10m,
            ["AmortizationOfIntangibleAssets"] = 5m, ["Depletion"] = 3m,
            ["PaymentsToAcquirePropertyPlantAndEquipment"] = 10m,
        };
        Assert.Equal(108m, OwnerEarningsOf(data));
    }

    #endregion
}
