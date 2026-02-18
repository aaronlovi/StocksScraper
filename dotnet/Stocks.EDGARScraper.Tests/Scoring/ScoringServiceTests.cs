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
