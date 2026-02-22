using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stocks.DataModels;
using Stocks.DataModels.Enums;
using Stocks.DataModels.Scoring;
using Stocks.Persistence.Database;
using Stocks.Shared;
using Stocks.Shared.Models;

namespace Stocks.Persistence.Services;

public class MoatScoringService {
    private readonly IDbmService _dbmService;
    private readonly ILogger? _logger;

    public MoatScoringService(IDbmService dbmService, ILogger? logger = null) {
        _dbmService = dbmService;
        _logger = logger;
    }

    // Moat-specific fallback chains
    internal static readonly string[] RevenueChain = [
        "Revenues",
        "RevenueFromContractWithCustomerExcludingAssessedTax",
        "SalesRevenueNet",
        "RevenueFromContractWithCustomerIncludingAssessedTax",
    ];

    internal static readonly string[] CostOfGoodsChain = [
        "CostOfGoodsAndServicesSold",
        "CostOfRevenue",
        "CostOfGoodsSold",
    ];

    internal static readonly string[] GrossProfitChain = [
        "GrossProfit",
    ];

    internal static readonly string[] OperatingIncomeChain = [
        "OperatingIncomeLoss",
    ];

    internal static readonly string[] InterestExpenseChain = [
        "InterestExpense",
        "InterestExpenseDebt",
    ];

    // Combined concept names: all Value Score concepts + Moat-specific ones
    internal static readonly string[] MoatConceptNames = BuildMoatConceptNames();

    private static string[] BuildMoatConceptNames() {
        var moatSpecific = new string[] {
            "Revenues",
            "RevenueFromContractWithCustomerExcludingAssessedTax",
            "SalesRevenueNet",
            "RevenueFromContractWithCustomerIncludingAssessedTax",
            "CostOfGoodsAndServicesSold",
            "CostOfRevenue",
            "CostOfGoodsSold",
            "GrossProfit",
            "OperatingIncomeLoss",
            "InterestExpense",
            "InterestExpenseDebt",
        };

        var combined = new HashSet<string>(ScoringService.AllConceptNames, StringComparer.Ordinal);
        foreach (string concept in moatSpecific)
            combined.Add(concept);

        var result = new string[combined.Count];
        combined.CopyTo(result);
        return result;
    }

    private static readonly Dictionary<string, TaxonomyBalanceTypes> EmptyBalanceTypes = new(StringComparer.Ordinal);

    internal static (MoatDerivedMetrics Metrics, IReadOnlyList<MoatYearMetrics> TrendData)
        ComputeMoatDerivedMetrics(
            IReadOnlyDictionary<int, IReadOnlyDictionary<string, decimal>> annualDataByYear,
            IReadOnlyDictionary<string, decimal> mostRecentSnapshot,
            decimal? oldestRetainedEarnings,
            decimal? pricePerShare,
            long? sharesOutstanding,
            IReadOnlyDictionary<string, TaxonomyBalanceTypes>? balanceTypes = null) {

        if (annualDataByYear.Count == 0)
            return (new MoatDerivedMetrics(null, null, null, null, null, null, null, null, null, null, null, null, 0, 0, 0, 0),
                    new List<MoatYearMetrics>());

        var sortedYears = new List<int>(annualDataByYear.Keys);
        sortedYears.Sort();

        var trendData = new List<MoatYearMetrics>(sortedYears.Count);

        // Accumulators for averaging
        decimal totalGrossMargin = 0m;
        int yearsWithGrossMargin = 0;
        decimal totalOperatingMargin = 0m;
        int yearsWithOperatingMargin = 0;
        decimal totalRoeCF = 0m;
        int yearsWithRoeCF = 0;
        decimal totalRoeOE = 0m;
        int yearsWithRoeOE = 0;
        decimal totalOwnerEarnings = 0m;
        int yearsWithOE = 0;
        decimal totalCapEx = 0m;
        int yearsWithCapEx = 0;
        int positiveOeYears = 0;
        int totalOeYears = 0;
        int capitalReturnYears = 0;
        int totalCapitalReturnYears = 0;

        // For Revenue CAGR
        decimal? oldestRevenue = null;
        int oldestRevenueYear = 0;
        decimal? latestRevenue = null;
        int latestRevenueYear = 0;

        // Most recent year interest coverage
        decimal? latestOperatingIncome = null;
        decimal? latestInterestExpense = null;

        foreach (int year in sortedYears) {
            IReadOnlyDictionary<string, decimal> yearData = annualDataByYear[year];

            // Revenue
            decimal? revenue = ScoringService.ResolveField(yearData, RevenueChain, null);

            // Gross profit (direct or derived)
            decimal? grossProfit = ScoringService.ResolveField(yearData, GrossProfitChain, null);
            if (!grossProfit.HasValue && revenue.HasValue) {
                decimal? cogs = ScoringService.ResolveField(yearData, CostOfGoodsChain, null);
                if (cogs.HasValue)
                    grossProfit = revenue.Value - cogs.Value;
            }

            // Gross margin %
            decimal? grossMarginPct = null;
            if (grossProfit.HasValue && revenue.HasValue && revenue.Value != 0m) {
                grossMarginPct = grossProfit.Value / revenue.Value * 100m;
                totalGrossMargin += grossMarginPct.Value;
                yearsWithGrossMargin++;
            }

            // Operating income & margin
            decimal? operatingIncome = ScoringService.ResolveField(yearData, OperatingIncomeChain, null);
            decimal? operatingMarginPct = null;
            if (operatingIncome.HasValue && revenue.HasValue && revenue.Value != 0m) {
                operatingMarginPct = operatingIncome.Value / revenue.Value * 100m;
                totalOperatingMargin += operatingMarginPct.Value;
                yearsWithOperatingMargin++;
            }

            // Track for interest coverage (latest year)
            if (operatingIncome.HasValue) {
                latestOperatingIncome = operatingIncome;
                decimal? interestExp = ScoringService.ResolveField(yearData, InterestExpenseChain, null);
                latestInterestExpense = interestExp;
            }

            // Equity for ROE computations
            decimal? equity = ScoringService.ResolveEquity(yearData);

            // Net Cash Flow for ROE (CF) — same computation as Value Score
            decimal? roeCfPct = null;
            decimal? grossCashFlow = ScoringService.ResolveField(yearData, ScoringService.CashChangeChain, null);
            if (grossCashFlow.HasValue) {
                decimal debtProceeds = ScoringService.ResolveField(yearData, ScoringService.DebtProceedsChain, 0m)!.Value;
                decimal debtRepayments = ScoringService.ResolveField(yearData, ScoringService.DebtRepaymentsChain, 0m)!.Value;
                decimal netDebtIssuance = debtProceeds - debtRepayments;

                decimal stockProceeds = ScoringService.ResolveField(yearData, ScoringService.StockProceedsChain, 0m)!.Value;
                decimal stockRepurchase = ScoringService.ResolveField(yearData, ScoringService.StockRepurchaseChain, 0m)!.Value;
                decimal netStockIssuance = stockProceeds - stockRepurchase;

                decimal preferredProceeds = ScoringService.ResolveField(yearData, ScoringService.PreferredProceedsChain, 0m)!.Value;
                decimal preferredRepurchase = ScoringService.ResolveField(yearData, ScoringService.PreferredRepurchaseChain, 0m)!.Value;
                decimal netPreferredIssuance = preferredProceeds - preferredRepurchase;

                decimal netCashFlow = grossCashFlow.Value - (netDebtIssuance + netStockIssuance + netPreferredIssuance);

                if (equity.HasValue && equity.Value != 0m) {
                    roeCfPct = 100m * netCashFlow / equity.Value;
                    totalRoeCF += roeCfPct.Value;
                    yearsWithRoeCF++;
                }
            }

            // Owner Earnings for ROE (OE) — same computation as Value Score
            decimal? roeOePct = null;
            decimal? netIncome = ScoringService.ResolveField(yearData, ScoringService.NetIncomeChain, null);
            decimal capEx = ScoringService.ResolveField(yearData, ScoringService.CapExChain, 0m)!.Value;

            if (netIncome.HasValue) {
                decimal depletionAndAmortization = ScoringService.ResolveDepletionAndAmortization(yearData);
                decimal deferredTax = ScoringService.ResolveDeferredTax(yearData);
                decimal otherNonCash = ScoringService.ResolveOtherNonCash(yearData);
                decimal workingCapitalChange = ScoringService.ResolveWorkingCapitalChange(yearData,
                    balanceTypes ?? EmptyBalanceTypes);

                decimal ownerEarnings = netIncome.Value
                    + depletionAndAmortization + deferredTax + otherNonCash
                    - capEx + workingCapitalChange;

                totalOwnerEarnings += ownerEarnings;
                yearsWithOE++;
                totalOeYears++;

                if (ownerEarnings > 0m)
                    positiveOeYears++;

                if (equity.HasValue && equity.Value != 0m) {
                    roeOePct = 100m * ownerEarnings / equity.Value;
                    totalRoeOE += roeOePct.Value;
                    yearsWithRoeOE++;
                }
            }

            // CapEx accumulation for ratio
            if (capEx != 0m || netIncome.HasValue) {
                totalCapEx += capEx;
                yearsWithCapEx++;
            }

            // Revenue tracking for CAGR
            if (revenue.HasValue) {
                if (!oldestRevenue.HasValue) {
                    oldestRevenue = revenue.Value;
                    oldestRevenueYear = year;
                }
                latestRevenue = revenue.Value;
                latestRevenueYear = year;
            }

            // Dividends + buybacks for capital return tracking
            decimal dividends = ScoringService.ResolveField(yearData, ScoringService.DividendsChain, 0m)!.Value;
            decimal buybacks = ScoringService.ResolveField(yearData, ScoringService.StockRepurchaseChain, 0m)!.Value;
            totalCapitalReturnYears++;
            if (dividends + buybacks > 0m)
                capitalReturnYears++;

            // Build per-year trend data
            trendData.Add(new MoatYearMetrics(year, grossMarginPct, operatingMarginPct, roeCfPct, roeOePct, revenue));
        }

        // Post-loop computations
        decimal? averageGrossMargin = yearsWithGrossMargin > 0 ? totalGrossMargin / yearsWithGrossMargin : null;
        decimal? averageOperatingMargin = yearsWithOperatingMargin > 0 ? totalOperatingMargin / yearsWithOperatingMargin : null;
        decimal? averageRoeCF = yearsWithRoeCF > 0 ? totalRoeCF / yearsWithRoeCF : null;
        decimal? averageRoeOE = yearsWithRoeOE > 0 ? totalRoeOE / yearsWithRoeOE : null;

        // Revenue CAGR
        decimal? revenueCagr = null;
        if (oldestRevenue.HasValue && latestRevenue.HasValue && oldestRevenue.Value > 0m && latestRevenue.Value > 0m) {
            int yearsBetween = latestRevenueYear - oldestRevenueYear;
            if (yearsBetween >= 1) {
                double ratio = (double)(latestRevenue.Value / oldestRevenue.Value);
                double cagrDouble = Math.Pow(ratio, 1.0 / yearsBetween) - 1.0;
                if (double.IsFinite(cagrDouble) && Math.Abs(cagrDouble) < (double)decimal.MaxValue)
                    revenueCagr = (decimal)cagrDouble * 100m;
            }
        }

        // CapEx ratio: avg CapEx / avg OE * 100
        decimal? capexRatio = null;
        if (yearsWithOE > 0 && yearsWithCapEx > 0) {
            decimal avgCapEx = totalCapEx / yearsWithCapEx;
            decimal avgOE = totalOwnerEarnings / yearsWithOE;
            if (avgOE != 0m)
                capexRatio = avgCapEx / avgOE * 100m;
        }

        // Interest coverage: operating income / interest expense (most recent year)
        decimal? interestCoverage = null;
        if (latestOperatingIncome.HasValue && latestInterestExpense.HasValue && latestInterestExpense.Value != 0m)
            interestCoverage = latestOperatingIncome.Value / latestInterestExpense.Value;

        // Debt-to-equity from most recent snapshot
        decimal? debt = ScoringService.ResolveField(mostRecentSnapshot, ScoringService.DebtChain, 0m);
        decimal? snapshotEquity = ScoringService.ResolveEquity(mostRecentSnapshot);
        decimal? debtToEquityRatio = null;
        if (debt.HasValue && snapshotEquity.HasValue && snapshotEquity.Value != 0m)
            debtToEquityRatio = debt.Value / snapshotEquity.Value;

        // Market cap
        decimal? marketCap = null;
        if (pricePerShare.HasValue && sharesOutstanding.HasValue)
            marketCap = pricePerShare.Value * sharesOutstanding.Value;

        // Estimated return (OE)
        int mostRecentAnnualYear = sortedYears[sortedYears.Count - 1];
        decimal? currentDividendsPaid = ScoringService.ResolveField(
            annualDataByYear[mostRecentAnnualYear], ScoringService.DividendsChain, 0m);

        decimal? estimatedReturnOE = null;
        decimal? averageOwnerEarnings = yearsWithOE > 0 ? totalOwnerEarnings / yearsWithOE : null;
        if (averageOwnerEarnings.HasValue && currentDividendsPaid.HasValue && marketCap.HasValue && marketCap.Value != 0m)
            estimatedReturnOE = 100m * (averageOwnerEarnings.Value - currentDividendsPaid.Value) / marketCap.Value;

        var metrics = new MoatDerivedMetrics(
            averageGrossMargin,
            averageOperatingMargin,
            averageRoeCF,
            averageRoeOE,
            revenueCagr,
            capexRatio,
            interestCoverage,
            debtToEquityRatio,
            estimatedReturnOE,
            currentDividendsPaid,
            marketCap,
            pricePerShare,
            positiveOeYears,
            totalOeYears,
            capitalReturnYears,
            totalCapitalReturnYears);

        return (metrics, trendData);
    }

    internal static IReadOnlyList<ScoringCheck> EvaluateMoatChecks(MoatDerivedMetrics metrics, int yearsOfData) {
        var checks = new List<ScoringCheck>(13);

        // Check 1: High ROE (CF) avg >= 15%
        checks.Add(ScoringService.MakeCheck(1, "High ROE (CF) avg", metrics.AverageRoeCF, ">= 15%",
            metrics.AverageRoeCF.HasValue
                ? (metrics.AverageRoeCF.Value >= 15m ? ScoringCheckResult.Pass : ScoringCheckResult.Fail)
                : ScoringCheckResult.NotAvailable));

        // Check 2: High ROE (OE) avg >= 15%
        checks.Add(ScoringService.MakeCheck(2, "High ROE (OE) avg", metrics.AverageRoeOE, ">= 15%",
            metrics.AverageRoeOE.HasValue
                ? (metrics.AverageRoeOE.Value >= 15m ? ScoringCheckResult.Pass : ScoringCheckResult.Fail)
                : ScoringCheckResult.NotAvailable));

        // Check 3: Gross margin avg >= 40%
        checks.Add(ScoringService.MakeCheck(3, "Gross margin avg", metrics.AverageGrossMargin, ">= 40%",
            metrics.AverageGrossMargin.HasValue
                ? (metrics.AverageGrossMargin.Value >= 40m ? ScoringCheckResult.Pass : ScoringCheckResult.Fail)
                : ScoringCheckResult.NotAvailable));

        // Check 4: Operating margin avg >= 15%
        checks.Add(ScoringService.MakeCheck(4, "Operating margin avg", metrics.AverageOperatingMargin, ">= 15%",
            metrics.AverageOperatingMargin.HasValue
                ? (metrics.AverageOperatingMargin.Value >= 15m ? ScoringCheckResult.Pass : ScoringCheckResult.Fail)
                : ScoringCheckResult.NotAvailable));

        // Check 5: Revenue growth CAGR > 3%
        checks.Add(ScoringService.MakeCheck(5, "Revenue growth", metrics.RevenueCagr, "> 3%",
            metrics.RevenueCagr.HasValue
                ? (metrics.RevenueCagr.Value > 3m ? ScoringCheckResult.Pass : ScoringCheckResult.Fail)
                : ScoringCheckResult.NotAvailable));

        // Check 6: Positive OE every year (0 failing years)
        int failingOeYears = metrics.TotalOeYears - metrics.PositiveOeYears;
        checks.Add(ScoringService.MakeCheck(6, "Positive OE every year", failingOeYears, "0 failing years",
            metrics.TotalOeYears > 0
                ? (metrics.PositiveOeYears == metrics.TotalOeYears ? ScoringCheckResult.Pass : ScoringCheckResult.Fail)
                : ScoringCheckResult.NotAvailable));

        // Check 7: Low capex ratio < 50%
        checks.Add(ScoringService.MakeCheck(7, "Low capex ratio", metrics.CapexRatio, "< 50%",
            metrics.CapexRatio.HasValue
                ? (metrics.CapexRatio.Value < 50m ? ScoringCheckResult.Pass : ScoringCheckResult.Fail)
                : ScoringCheckResult.NotAvailable));

        // Check 8: Consistent dividend or buyback >= 75% of years
        decimal? capitalReturnPct = metrics.TotalCapitalReturnYears > 0
            ? (decimal)metrics.CapitalReturnYears / metrics.TotalCapitalReturnYears * 100m
            : null;
        checks.Add(ScoringService.MakeCheck(8, "Consistent dividend/buyback", capitalReturnPct, ">= 75% of years",
            metrics.TotalCapitalReturnYears > 0
                ? (metrics.CapitalReturnYears >= 0.75m * metrics.TotalCapitalReturnYears
                    ? ScoringCheckResult.Pass : ScoringCheckResult.Fail)
                : ScoringCheckResult.NotAvailable));

        // Check 9: Debt-to-equity < 1.0
        checks.Add(ScoringService.MakeCheck(9, "Debt-to-equity", metrics.DebtToEquityRatio, "< 1.0",
            metrics.DebtToEquityRatio.HasValue
                ? (metrics.DebtToEquityRatio.Value < 1.0m ? ScoringCheckResult.Pass : ScoringCheckResult.Fail)
                : ScoringCheckResult.NotAvailable));

        // Check 10: Interest coverage > 5x
        checks.Add(ScoringService.MakeCheck(10, "Interest coverage", metrics.InterestCoverage, "> 5x",
            metrics.InterestCoverage.HasValue
                ? (metrics.InterestCoverage.Value > 5m ? ScoringCheckResult.Pass : ScoringCheckResult.Fail)
                : ScoringCheckResult.NotAvailable));

        // Check 11: History >= 7 years
        checks.Add(ScoringService.MakeCheck(11, "History", yearsOfData, ">= 7 years",
            yearsOfData >= 7 ? ScoringCheckResult.Pass : ScoringCheckResult.Fail));

        // Check 12: Estimated return (OE) > 3%
        checks.Add(ScoringService.MakeCheck(12, "Est. return (OE) floor", metrics.EstimatedReturnOE, "> 3%",
            metrics.EstimatedReturnOE.HasValue
                ? (metrics.EstimatedReturnOE.Value > 3m ? ScoringCheckResult.Pass : ScoringCheckResult.Fail)
                : ScoringCheckResult.NotAvailable));

        // Check 13: Estimated return (OE) < 40%
        checks.Add(ScoringService.MakeCheck(13, "Est. return (OE) cap", metrics.EstimatedReturnOE, "< 40%",
            metrics.EstimatedReturnOE.HasValue
                ? (metrics.EstimatedReturnOE.Value < 40m ? ScoringCheckResult.Pass : ScoringCheckResult.Fail)
                : ScoringCheckResult.NotAvailable));

        return checks;
    }

    public async Task<Result<MoatScoringResult>> ComputeScore(ulong companyId, CancellationToken ct) {
        // 1. Get raw scoring data points (8-year window)
        Result<IReadOnlyCollection<ScoringConceptValue>> dataResult =
            await _dbmService.GetScoringDataPoints(companyId, MoatConceptNames, 8, ct);
        if (dataResult.IsFailure || dataResult.Value is null)
            return Result<MoatScoringResult>.Failure(ErrorCodes.GenericError, "Failed to fetch scoring data points.");

        // 2. Get company info
        Result<Company> companyResult = await _dbmService.GetCompanyById(companyId, ct);
        if (companyResult.IsFailure || companyResult.Value is null)
            return Result<MoatScoringResult>.Failure(ErrorCodes.NotFound, "Company not found.");

        // 3. Get tickers to find price
        Result<IReadOnlyCollection<CompanyTicker>> tickersResult =
            await _dbmService.GetCompanyTickersByCompanyId(companyId, ct);

        string? firstTicker = null;
        if (tickersResult.IsSuccess && tickersResult.Value is not null) {
            foreach (CompanyTicker t in tickersResult.Value) {
                firstTicker = t.Ticker;
                break;
            }
        }

        // 4. Get latest price
        decimal? pricePerShare = null;
        DateOnly? priceDate = null;
        if (firstTicker is not null) {
            Result<IReadOnlyCollection<PriceRow>> pricesResult =
                await _dbmService.GetPricesByTicker(firstTicker, ct);
            if (pricesResult.IsSuccess && pricesResult.Value is not null) {
                DateOnly maxDate = DateOnly.MinValue;
                foreach (PriceRow price in pricesResult.Value) {
                    if (price.PriceDate > maxDate) {
                        maxDate = price.PriceDate;
                        pricePerShare = price.Close;
                        priceDate = price.PriceDate;
                    }
                }
            }
        }

        // 5. Group and partition raw data
        ScoringService.GroupedScoringData grouped = ScoringService.GroupAndPartitionData(dataResult.Value);

        var rawDataByYear = new Dictionary<int, IReadOnlyDictionary<string, decimal>>();
        foreach (KeyValuePair<int, Dictionary<string, decimal>> entry in grouped.AnnualByYear)
            rawDataByYear[entry.Key] = entry.Value;

        int yearsOfData = grouped.AnnualByYear.Count;

        // 6. Extract shares outstanding from most recent snapshot
        long? sharesOutstanding = null;
        decimal? sharesValue = ScoringService.ResolveField(grouped.MostRecentSnapshot, ScoringService.SharesChain, null);
        if (sharesValue.HasValue)
            sharesOutstanding = (long)sharesValue.Value;

        // 7. Compute Moat derived metrics
        (MoatDerivedMetrics metrics, IReadOnlyList<MoatYearMetrics> trendData) = ComputeMoatDerivedMetrics(
            rawDataByYear, grouped.MostRecentSnapshot, grouped.OldestRetainedEarnings,
            pricePerShare, sharesOutstanding, grouped.BalanceTypes);

        // 8. Evaluate the 13 checks
        IReadOnlyList<ScoringCheck> scorecard = EvaluateMoatChecks(metrics, yearsOfData);

        // 9. Count scores
        int overallScore = 0;
        int computableChecks = 0;
        foreach (ScoringCheck check in scorecard) {
            if (check.Result != ScoringCheckResult.NotAvailable) {
                computableChecks++;
                if (check.Result == ScoringCheckResult.Pass)
                    overallScore++;
            }
        }

        var result = new MoatScoringResult(
            rawDataByYear,
            metrics,
            scorecard,
            trendData,
            overallScore,
            computableChecks,
            yearsOfData,
            pricePerShare,
            priceDate,
            sharesOutstanding);

        return Result<MoatScoringResult>.Success(result);
    }

    public async Task<Result<IReadOnlyCollection<CompanyMoatScoreSummary>>> ComputeAllMoatScores(CancellationToken ct) {
        // 1. Fetch all scoring data points (8-year window)
        _logger?.LogInformation("Fetching moat scoring data points...");
        Result<IReadOnlyCollection<BatchScoringConceptValue>> dataResult =
            await _dbmService.GetAllScoringDataPoints(MoatConceptNames, 8, ct);
        if (dataResult.IsFailure || dataResult.Value is null)
            return Result<IReadOnlyCollection<CompanyMoatScoreSummary>>.Failure(
                ErrorCodes.GenericError, "Failed to fetch batch scoring data points.");
        _logger?.LogInformation("Fetched {Count} moat scoring data points", dataResult.Value.Count);

        // 2. Fetch latest prices
        _logger?.LogInformation("Fetching latest prices...");
        Result<IReadOnlyCollection<LatestPrice>> pricesResult =
            await _dbmService.GetAllLatestPrices(ct);
        if (pricesResult.IsFailure || pricesResult.Value is null)
            return Result<IReadOnlyCollection<CompanyMoatScoreSummary>>.Failure(
                ErrorCodes.GenericError, "Failed to fetch latest prices.");
        _logger?.LogInformation("Fetched {Count} latest prices", pricesResult.Value.Count);

        // 3. Fetch company tickers
        _logger?.LogInformation("Fetching company tickers...");
        Result<IReadOnlyCollection<CompanyTicker>> tickersResult =
            await _dbmService.GetAllCompanyTickers(ct);
        if (tickersResult.IsFailure || tickersResult.Value is null)
            return Result<IReadOnlyCollection<CompanyMoatScoreSummary>>.Failure(
                ErrorCodes.GenericError, "Failed to fetch company tickers.");
        _logger?.LogInformation("Fetched {Count} company tickers", tickersResult.Value.Count);

        // 4. Fetch company names
        _logger?.LogInformation("Fetching company names...");
        Result<IReadOnlyCollection<CompanyName>> namesResult =
            await _dbmService.GetAllCompanyNames(ct);
        if (namesResult.IsFailure || namesResult.Value is null)
            return Result<IReadOnlyCollection<CompanyMoatScoreSummary>>.Failure(
                ErrorCodes.GenericError, "Failed to fetch company names.");
        _logger?.LogInformation("Fetched {Count} company names", namesResult.Value.Count);

        // 5. Fetch all companies for CIK lookup
        _logger?.LogInformation("Fetching companies...");
        Result<IReadOnlyCollection<Company>> companiesResult =
            await _dbmService.GetAllCompaniesByDataSource("EDGAR", ct);
        if (companiesResult.IsFailure || companiesResult.Value is null)
            return Result<IReadOnlyCollection<CompanyMoatScoreSummary>>.Failure(
                ErrorCodes.GenericError, "Failed to fetch companies.");
        _logger?.LogInformation("Fetched {Count} companies", companiesResult.Value.Count);

        // Build lookups
        var companyLookup = new Dictionary<ulong, Company>();
        foreach (Company c in companiesResult.Value)
            companyLookup[c.CompanyId] = c;

        var companyNameLookup = new Dictionary<ulong, string>();
        foreach (CompanyName cn in namesResult.Value) {
            if (!companyNameLookup.ContainsKey(cn.CompanyId))
                companyNameLookup[cn.CompanyId] = cn.Name;
        }

        var tickerByCompany = new Dictionary<ulong, CompanyTicker>();
        foreach (CompanyTicker ct2 in tickersResult.Value) {
            if (!tickerByCompany.ContainsKey(ct2.CompanyId))
                tickerByCompany[ct2.CompanyId] = ct2;
        }

        var priceByTicker = new Dictionary<string, LatestPrice>(StringComparer.OrdinalIgnoreCase);
        foreach (LatestPrice lp in pricesResult.Value) {
            if (!priceByTicker.ContainsKey(lp.Ticker))
                priceByTicker[lp.Ticker] = lp;
        }

        // 6. Group batch scoring data by company_id
        var dataByCompany = new Dictionary<ulong, List<ScoringConceptValue>>();
        foreach (BatchScoringConceptValue bv in dataResult.Value) {
            if (!dataByCompany.TryGetValue(bv.CompanyId, out List<ScoringConceptValue>? list)) {
                list = new List<ScoringConceptValue>();
                dataByCompany[bv.CompanyId] = list;
            }
            list.Add(new ScoringConceptValue(bv.ConceptName, bv.Value, bv.ReportDate, bv.BalanceTypeId, bv.FilingTypeId));
        }

        // 7. Compute moat scores for each company
        _logger?.LogInformation("Scoring {Count} companies for moat...", dataByCompany.Count);
        DateTime now = DateTime.UtcNow;
        var results = new List<CompanyMoatScoreSummary>(dataByCompany.Count);
        int scored = 0;
        int logInterval = Math.Max(dataByCompany.Count / 10, 1);

        foreach (KeyValuePair<ulong, List<ScoringConceptValue>> entry in dataByCompany) {
            ulong companyId = entry.Key;
            List<ScoringConceptValue> values = entry.Value;

            ScoringService.GroupedScoringData grouped = ScoringService.GroupAndPartitionData(values);

            var rawDataByYear = new Dictionary<int, IReadOnlyDictionary<string, decimal>>();
            foreach (KeyValuePair<int, Dictionary<string, decimal>> yearEntry in grouped.AnnualByYear)
                rawDataByYear[yearEntry.Key] = yearEntry.Value;

            int yearsOfData = grouped.AnnualByYear.Count;

            if (yearsOfData == 0)
                continue;

            long? sharesOutstanding = null;
            decimal? sharesValue = ScoringService.ResolveField(grouped.MostRecentSnapshot, ScoringService.SharesChain, null);
            if (sharesValue.HasValue)
                sharesOutstanding = (long)sharesValue.Value;

            decimal? pricePerShare = null;
            DateOnly? priceDate = null;
            string? ticker = null;
            string? exchange = null;
            if (tickerByCompany.TryGetValue(companyId, out CompanyTicker? companyTicker)) {
                ticker = companyTicker.Ticker;
                exchange = companyTicker.Exchange;
                if (priceByTicker.TryGetValue(companyTicker.Ticker, out LatestPrice? price)) {
                    pricePerShare = price.Close;
                    priceDate = price.PriceDate;
                }
            }

            (MoatDerivedMetrics metrics, _) = ComputeMoatDerivedMetrics(
                rawDataByYear, grouped.MostRecentSnapshot, grouped.OldestRetainedEarnings,
                pricePerShare, sharesOutstanding, grouped.BalanceTypes);

            IReadOnlyList<ScoringCheck> scorecard = EvaluateMoatChecks(metrics, yearsOfData);

            int overallScore = 0;
            int computableChecks = 0;
            foreach (ScoringCheck check in scorecard) {
                if (check.Result != ScoringCheckResult.NotAvailable) {
                    computableChecks++;
                    if (check.Result == ScoringCheckResult.Pass)
                        overallScore++;
                }
            }

            string cik = "0";
            if (companyLookup.TryGetValue(companyId, out Company? company))
                cik = company.Cik.ToString();

            companyNameLookup.TryGetValue(companyId, out string? companyName);

            results.Add(new CompanyMoatScoreSummary(
                companyId, cik, companyName, ticker, exchange,
                overallScore, computableChecks, yearsOfData,
                metrics.AverageGrossMargin, metrics.AverageOperatingMargin,
                metrics.AverageRoeCF, metrics.AverageRoeOE,
                metrics.EstimatedReturnOE, metrics.RevenueCagr,
                metrics.CapexRatio, metrics.InterestCoverage,
                metrics.DebtToEquityRatio, pricePerShare, priceDate,
                sharesOutstanding, now));

            scored++;
            if (scored % logInterval == 0)
                _logger?.LogInformation("Moat scored {Scored}/{Total} companies", scored, dataByCompany.Count);
        }

        _logger?.LogInformation("Finished moat scoring {Count} companies", results.Count);
        return Result<IReadOnlyCollection<CompanyMoatScoreSummary>>.Success(results);
    }
}
