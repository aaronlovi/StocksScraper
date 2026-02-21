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

public class ScoringService {
    private readonly IDbmService _dbmService;
    private readonly ILogger? _logger;

    public ScoringService(IDbmService dbmService, ILogger? logger = null) {
        _dbmService = dbmService;
        _logger = logger;
    }

    // US-GAAP concept names needed for scoring
    internal static readonly string[] AllConceptNames = [
        // Balance sheet (instant) — totals for derived equity computation
        "Assets",
        "Liabilities",
        "LiabilitiesAndStockholdersEquity",
        // Balance sheet (instant) — equity concepts
        "StockholdersEquity",
        "StockholdersEquityIncludingPortionAttributableToNoncontrollingInterest",
        "MembersEquity",
        "PartnersCapital",
        // Balance sheet (instant) — noncontrolling interest (for equity derivation)
        "MinorityInterest",
        "NoncontrollingInterestInVariableInterestEntity",
        "RedeemableNoncontrollingInterestEquityCarryingAmount",
        // Balance sheet (instant) — other
        "Goodwill",
        "IntangibleAssetsNetExcludingGoodwill",
        "LongTermDebtAndCapitalLeaseObligations",
        "LongTermDebt",
        "LongTermDebtNoncurrent",
        "RetainedEarningsAccumulatedDeficit",
        "AssetsCurrent",
        "LiabilitiesCurrent",
        "CommonStockSharesOutstanding",
        "WeightedAverageNumberOfSharesOutstandingBasic",
        // DEI cover page concept — aggregate shares for multi-class structures (e.g. Visa)
        "EntityCommonStockSharesOutstanding",
        // Cash flow (duration) — cash change
        "CashCashEquivalentsRestrictedCashAndRestrictedCashEquivalentsPeriodIncreaseDecreaseIncludingExchangeRateEffect",
        "CashCashEquivalentsRestrictedCashAndRestrictedCashEquivalentsPeriodIncreaseDecreaseExcludingExchangeRateEffect",
        "CashAndCashEquivalentsPeriodIncreaseDecrease",
        // Cash flow — debt proceeds (540 exclusive for Senior, 96 for Convertible)
        "ProceedsFromIssuanceOfLongTermDebt",
        "ProceedsFromIssuanceOfSeniorLongTermDebt",
        "ProceedsFromIssuanceOfDebt",
        "ProceedsFromConvertibleDebt",
        // Cash flow — debt repayments (90 exclusive for Notes, 53 for Senior)
        "RepaymentsOfLongTermDebt",
        "RepaymentsOfDebt",
        "RepaymentsOfConvertibleDebt",
        "RepaymentsOfNotesPayable",
        "RepaymentsOfSeniorDebt",
        // Cash flow — dividends (24 exclusive for CommonStockCash)
        "PaymentsOfDividends",
        "PaymentsOfDividendsCommonStock",
        "Dividends",
        "DividendsCommonStockCash",
        // Cash flow — stock issuance/repurchase (540 exclusive for Options, 41 for Equity)
        "ProceedsFromIssuanceOfCommonStock",
        "ProceedsFromStockOptionsExercised",
        "PaymentsForRepurchaseOfCommonStock",
        "PaymentsForRepurchaseOfEquity",
        // Cash flow — preferred stock
        "ProceedsFromIssuanceOfPreferredStockAndPreferenceStock",
        "PaymentsForRepurchaseOfPreferredStockAndPreferenceStock",
        // Working capital — aggregate concepts
        "IncreaseDecreaseInOperatingCapital",
        "IncreaseDecreaseInOtherOperatingCapitalNet",
        // Working capital — receivables group (broadest to narrowest)
        "IncreaseDecreaseInAccountsReceivableAndOtherOperatingAssets",
        "IncreaseDecreaseInReceivables",
        "IncreaseDecreaseInAccountsAndOtherReceivables",
        "IncreaseDecreaseInAccountsAndNotesReceivable",
        "IncreaseDecreaseInAccountsReceivable",
        "IncreaseDecreaseInOtherReceivables",
        // Working capital — inventories
        "IncreaseDecreaseInInventories",
        // Working capital — payables and accrued liabilities group
        "IncreaseDecreaseInAccruedLiabilitiesAndOtherOperatingLiabilities",
        "IncreaseDecreaseInAccountsPayableAndAccruedLiabilities",
        "IncreaseDecreaseInOtherAccountsPayableAndAccruedLiabilities",
        "IncreaseDecreaseInAccountsPayable",
        "IncreaseDecreaseInAccountsPayableTrade",
        "IncreaseDecreaseInAccruedLiabilities",
        "IncreaseDecreaseInOtherAccruedLiabilities",
        // Working capital — prepaid/deferred expenses
        "IncreaseDecreaseInPrepaidDeferredExpenseAndOtherAssets",
        "IncreaseDecreaseInPrepaidExpense",
        // Working capital — deferred revenue / contract liabilities and assets
        "IncreaseDecreaseInDeferredRevenue",
        "IncreaseDecreaseInContractWithCustomerLiability",
        "IncreaseDecreaseInContractWithCustomerAsset",
        // Working capital — other operating assets/liabilities
        "IncreaseDecreaseInOtherOperatingAssets",
        "IncreaseDecreaseInOtherOperatingLiabilities",
        "IncreaseDecreaseInOtherCurrentAssets",
        "IncreaseDecreaseInOtherNoncurrentAssets",
        "IncreaseDecreaseInOtherCurrentLiabilities",
        "IncreaseDecreaseInOtherNoncurrentLiabilities",
        // Working capital — standalone categories (not subsumed by existing groups)
        "IncreaseDecreaseInAccruedIncomeTaxesPayable",
        "IncreaseDecreaseInSelfInsuranceReserve",
        "IncreaseDecreaseInOperatingLeaseLiability",
        "IncreaseDecreaseInEmployeeRelatedLiabilities",
        "IncreaseDecreaseInInterestPayableNet",
        "IncreaseDecreaseInIncomeTaxesReceivable",
        // Cash flow — deferred tax
        "DeferredIncomeTaxExpenseBenefit",
        "DeferredIncomeTaxesAndTaxCredits",
        "DeferredFederalIncomeTaxExpenseBenefit",
        "DeferredForeignIncomeTaxExpenseBenefit",
        "DeferredStateAndLocalIncomeTaxExpenseBenefit",
        // Cash flow — depreciation/amortization
        "Depletion",
        "AmortizationOfIntangibleAssets",
        "DepreciationDepletionAndAmortization",
        "DepreciationAndAmortization",
        "Depreciation",
        // Cash flow — other non-cash
        "OtherNoncashIncomeExpense",
        "OtherNoncashExpense",
        "OtherNoncashIncome",
        // Cash flow — capex
        "PaymentsToAcquirePropertyPlantAndEquipment",
        // Income statement (duration)
        "NetIncomeLoss",
        "IncomeLossFromContinuingOperations",
        "ProfitLoss",
    ];

    // Fallback chains
    internal static readonly string[] EquityChain = ["StockholdersEquity", "StockholdersEquityIncludingPortionAttributableToNoncontrollingInterest", "MembersEquity", "PartnersCapital"];
    internal static readonly string[] AssetsChain = ["Assets"];
    internal static readonly string[] LiabilitiesChain = ["Liabilities"];
    internal static readonly string[] LiabilitiesAndEquityChain = ["LiabilitiesAndStockholdersEquity"];
    internal static readonly string[] NciChain = ["MinorityInterest", "NoncontrollingInterestInVariableInterestEntity"];
    internal static readonly string[] RedeemableNciChain = ["RedeemableNoncontrollingInterestEquityCarryingAmount"];
    internal static readonly string[] DebtChain = ["LongTermDebtAndCapitalLeaseObligations", "LongTermDebt", "LongTermDebtNoncurrent"];
    internal static readonly string[] RetainedEarningsChain = ["RetainedEarningsAccumulatedDeficit"];
    internal static readonly string[] GoodwillChain = ["Goodwill"];
    internal static readonly string[] IntangiblesChain = ["IntangibleAssetsNetExcludingGoodwill"];
    internal static readonly string[] NetIncomeChain = ["NetIncomeLoss", "IncomeLossFromContinuingOperations", "ProfitLoss"];
    internal static readonly string[] CashChangeChain = [
        "CashCashEquivalentsRestrictedCashAndRestrictedCashEquivalentsPeriodIncreaseDecreaseIncludingExchangeRateEffect",
        "CashCashEquivalentsRestrictedCashAndRestrictedCashEquivalentsPeriodIncreaseDecreaseExcludingExchangeRateEffect",
        "CashAndCashEquivalentsPeriodIncreaseDecrease"
    ];
    internal static readonly string[] DebtProceedsChain = [
        "ProceedsFromIssuanceOfLongTermDebt", "ProceedsFromIssuanceOfSeniorLongTermDebt",
        "ProceedsFromIssuanceOfDebt", "ProceedsFromConvertibleDebt"
    ];
    internal static readonly string[] DebtRepaymentsChain = [
        "RepaymentsOfLongTermDebt", "RepaymentsOfDebt", "RepaymentsOfConvertibleDebt",
        "RepaymentsOfNotesPayable", "RepaymentsOfSeniorDebt"
    ];
    internal static readonly string[] DividendsChain = [
        "PaymentsOfDividends", "PaymentsOfDividendsCommonStock", "Dividends", "DividendsCommonStockCash"
    ];
    internal static readonly string[] StockProceedsChain = [
        "ProceedsFromIssuanceOfCommonStock", "ProceedsFromStockOptionsExercised"
    ];
    internal static readonly string[] StockRepurchaseChain = [
        "PaymentsForRepurchaseOfCommonStock", "PaymentsForRepurchaseOfEquity"
    ];
    internal static readonly string[] PreferredProceedsChain = ["ProceedsFromIssuanceOfPreferredStockAndPreferenceStock"];
    internal static readonly string[] PreferredRepurchaseChain = ["PaymentsForRepurchaseOfPreferredStockAndPreferenceStock"];
    internal static readonly string[] CapExChain = ["PaymentsToAcquirePropertyPlantAndEquipment"];
    internal static readonly string[] DeferredTaxChain = ["DeferredIncomeTaxExpenseBenefit", "DeferredIncomeTaxesAndTaxCredits"];
    internal static readonly string[] DepletionChain = ["Depletion"];
    internal static readonly string[] AmortizationChain = ["AmortizationOfIntangibleAssets"];
    internal static readonly string[] DDAChain = ["DepreciationDepletionAndAmortization", "DepreciationAndAmortization"];
    internal static readonly string[] DepreciationChain = ["Depreciation"];
    internal static readonly string[] DeferredTaxFederalChain = ["DeferredFederalIncomeTaxExpenseBenefit"];
    internal static readonly string[] DeferredTaxForeignChain = ["DeferredForeignIncomeTaxExpenseBenefit"];
    internal static readonly string[] DeferredTaxStateChain = ["DeferredStateAndLocalIncomeTaxExpenseBenefit"];
    internal static readonly string[] OtherNonCashChain = ["OtherNoncashIncomeExpense"];
    internal static readonly string[] OtherNonCashExpenseChain = ["OtherNoncashExpense"];
    internal static readonly string[] OtherNonCashIncomeChain = ["OtherNoncashIncome"];
    internal static readonly string[] WorkingCapitalChangeChain = ["IncreaseDecreaseInOperatingCapital", "IncreaseDecreaseInOtherOperatingCapitalNet"];
    internal static readonly string[] SharesChain = ["CommonStockSharesOutstanding", "WeightedAverageNumberOfSharesOutstandingBasic", "EntityCommonStockSharesOutstanding"];

    private static readonly Dictionary<string, TaxonomyBalanceTypes> EmptyBalanceTypes = new(StringComparer.Ordinal);

    public async Task<Result<IReadOnlyCollection<CompanyScoreSummary>>> ComputeAllScores(CancellationToken ct) {
        // 1. Fetch all scoring data points in one query
        _logger?.LogInformation("Fetching scoring data points...");
        Result<IReadOnlyCollection<BatchScoringConceptValue>> dataResult =
            await _dbmService.GetAllScoringDataPoints(AllConceptNames, ct);
        if (dataResult.IsFailure || dataResult.Value is null)
            return Result<IReadOnlyCollection<CompanyScoreSummary>>.Failure(
                ErrorCodes.GenericError, "Failed to fetch batch scoring data points.");
        _logger?.LogInformation("Fetched {Count} scoring data points", dataResult.Value.Count);

        // 2. Fetch latest prices for all tickers
        _logger?.LogInformation("Fetching latest prices...");
        Result<IReadOnlyCollection<LatestPrice>> pricesResult =
            await _dbmService.GetAllLatestPrices(ct);
        if (pricesResult.IsFailure || pricesResult.Value is null)
            return Result<IReadOnlyCollection<CompanyScoreSummary>>.Failure(
                ErrorCodes.GenericError, "Failed to fetch latest prices.");
        _logger?.LogInformation("Fetched {Count} latest prices", pricesResult.Value.Count);

        // 3. Fetch company tickers (company_id → ticker)
        _logger?.LogInformation("Fetching company tickers...");
        Result<IReadOnlyCollection<CompanyTicker>> tickersResult =
            await _dbmService.GetAllCompanyTickers(ct);
        if (tickersResult.IsFailure || tickersResult.Value is null)
            return Result<IReadOnlyCollection<CompanyScoreSummary>>.Failure(
                ErrorCodes.GenericError, "Failed to fetch company tickers.");
        _logger?.LogInformation("Fetched {Count} company tickers", tickersResult.Value.Count);

        // 4. Fetch company names (company_id → name)
        _logger?.LogInformation("Fetching company names...");
        Result<IReadOnlyCollection<CompanyName>> namesResult =
            await _dbmService.GetAllCompanyNames(ct);
        if (namesResult.IsFailure || namesResult.Value is null)
            return Result<IReadOnlyCollection<CompanyScoreSummary>>.Failure(
                ErrorCodes.GenericError, "Failed to fetch company names.");
        _logger?.LogInformation("Fetched {Count} company names", namesResult.Value.Count);

        // 5. Fetch all companies for CIK lookup
        _logger?.LogInformation("Fetching companies...");
        Result<IReadOnlyCollection<Company>> companiesResult =
            await _dbmService.GetAllCompaniesByDataSource("EDGAR", ct);
        if (companiesResult.IsFailure || companiesResult.Value is null)
            return Result<IReadOnlyCollection<CompanyScoreSummary>>.Failure(
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

        // 6. Group batch scoring data by company_id into per-company List<ScoringConceptValue>
        var dataByCompany = new Dictionary<ulong, List<ScoringConceptValue>>();
        foreach (BatchScoringConceptValue bv in dataResult.Value) {
            if (!dataByCompany.TryGetValue(bv.CompanyId, out List<ScoringConceptValue>? list)) {
                list = new List<ScoringConceptValue>();
                dataByCompany[bv.CompanyId] = list;
            }
            list.Add(new ScoringConceptValue(bv.ConceptName, bv.Value, bv.ReportDate, bv.BalanceTypeId, bv.FilingTypeId));
        }

        // 7. Compute scores for each company
        _logger?.LogInformation("Scoring {Count} companies...", dataByCompany.Count);
        DateTime now = DateTime.UtcNow;
        var results = new List<CompanyScoreSummary>(dataByCompany.Count);
        int scored = 0;
        int logInterval = Math.Max(dataByCompany.Count / 10, 1);

        foreach (var entry in dataByCompany) {
            ulong companyId = entry.Key;
            List<ScoringConceptValue> values = entry.Value;

            // Group and partition data (annual vs most recent snapshot)
            GroupedScoringData grouped = GroupAndPartitionData(values);

            var rawDataByYear = new Dictionary<int, IReadOnlyDictionary<string, decimal>>();
            foreach (var yearEntry in grouped.AnnualByYear)
                rawDataByYear[yearEntry.Key] = yearEntry.Value;

            int yearsOfData = grouped.AnnualByYear.Count;

            // Skip companies with no annual (10-K) data — can't produce meaningful scores
            if (yearsOfData == 0)
                continue;

            // Extract shares outstanding from most recent snapshot
            long? sharesOutstanding = null;
            decimal? sharesValue = ResolveField(grouped.MostRecentSnapshot, SharesChain, null);
            if (sharesValue.HasValue)
                sharesOutstanding = (long)sharesValue.Value;

            // Look up ticker and price
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

            // Compute derived metrics
            DerivedMetrics metrics = ComputeDerivedMetrics(
                rawDataByYear, grouped.MostRecentSnapshot, grouped.OldestRetainedEarnings,
                pricePerShare, sharesOutstanding, grouped.BalanceTypes);

            // Evaluate checks
            IReadOnlyList<ScoringCheck> scorecard = EvaluateChecks(metrics, yearsOfData);

            int overallScore = 0;
            int computableChecks = 0;
            foreach (ScoringCheck check in scorecard) {
                if (check.Result != ScoringCheckResult.NotAvailable) {
                    computableChecks++;
                    if (check.Result == ScoringCheckResult.Pass)
                        overallScore++;
                }
            }

            // Look up CIK and name
            string cik = "0";
            if (companyLookup.TryGetValue(companyId, out Company? company))
                cik = company.Cik.ToString();

            companyNameLookup.TryGetValue(companyId, out string? companyName);

            decimal? maxBuyPrice = ComputeMaxBuyPrice(
                metrics.BookValue, metrics.AverageNetCashFlow,
                metrics.AverageOwnerEarnings, metrics.CurrentDividendsPaid,
                sharesOutstanding);
            decimal? percentageUpside = ComputePercentageUpside(maxBuyPrice, pricePerShare);

            results.Add(new CompanyScoreSummary(
                companyId, cik, companyName, ticker, exchange,
                overallScore, computableChecks, yearsOfData,
                metrics.BookValue, metrics.MarketCap,
                metrics.DebtToEquityRatio, metrics.PriceToBookRatio, metrics.DebtToBookRatio,
                metrics.AdjustedRetainedEarnings, metrics.AverageNetCashFlow,
                metrics.AverageOwnerEarnings, metrics.EstimatedReturnCF, metrics.EstimatedReturnOE,
                pricePerShare, priceDate, sharesOutstanding,
                metrics.CurrentDividendsPaid, maxBuyPrice, percentageUpside, now));

            scored++;
            if (scored % logInterval == 0)
                _logger?.LogInformation("Scored {Scored}/{Total} companies", scored, dataByCompany.Count);
        }

        _logger?.LogInformation("Finished scoring {Count} companies", results.Count);
        return Result<IReadOnlyCollection<CompanyScoreSummary>>.Success(results);
    }

    public async Task<Result<ScoringResult>> ComputeScore(ulong companyId, CancellationToken ct) {
        // 1. Get raw scoring data points
        Result<IReadOnlyCollection<ScoringConceptValue>> dataResult =
            await _dbmService.GetScoringDataPoints(companyId, AllConceptNames, ct);
        if (dataResult.IsFailure || dataResult.Value is null)
            return Result<ScoringResult>.Failure(ErrorCodes.GenericError, "Failed to fetch scoring data points.");

        // 2. Get company info (for tickers → price lookup)
        Result<Company> companyResult = await _dbmService.GetCompanyById(companyId, ct);
        if (companyResult.IsFailure || companyResult.Value is null)
            return Result<ScoringResult>.Failure(ErrorCodes.NotFound, "Company not found.");

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
        GroupedScoringData grouped = GroupAndPartitionData(dataResult.Value);

        // Build the IReadOnlyDictionary version (annual only, for rawDataByYear in result)
        var rawDataByYear = new Dictionary<int, IReadOnlyDictionary<string, decimal>>();
        foreach (KeyValuePair<int, Dictionary<string, decimal>> entry in grouped.AnnualByYear)
            rawDataByYear[entry.Key] = entry.Value;

        int yearsOfData = grouped.AnnualByYear.Count;

        // 6. Extract shares outstanding from most recent snapshot
        long? sharesOutstanding = null;
        decimal? sharesValue = ResolveField(grouped.MostRecentSnapshot, SharesChain, null);
        if (sharesValue.HasValue)
            sharesOutstanding = (long)sharesValue.Value;

        // 7. Compute derived metrics
        DerivedMetrics metrics = ComputeDerivedMetrics(
            rawDataByYear, grouped.MostRecentSnapshot, grouped.OldestRetainedEarnings,
            pricePerShare, sharesOutstanding, grouped.BalanceTypes);

        // 8. Evaluate the 13 checks
        IReadOnlyList<ScoringCheck> scorecard = EvaluateChecks(metrics, yearsOfData);

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

        decimal? maxBuyPrice = ComputeMaxBuyPrice(
            metrics.BookValue, metrics.AverageNetCashFlow,
            metrics.AverageOwnerEarnings, metrics.CurrentDividendsPaid,
            sharesOutstanding);
        decimal? percentageUpside = ComputePercentageUpside(maxBuyPrice, pricePerShare);

        var result = new ScoringResult(
            rawDataByYear,
            metrics,
            scorecard,
            overallScore,
            computableChecks,
            yearsOfData,
            pricePerShare,
            priceDate,
            sharesOutstanding,
            maxBuyPrice,
            percentageUpside);

        return Result<ScoringResult>.Success(result);
    }

    internal static decimal? ResolveField(
        IReadOnlyDictionary<string, decimal> yearData,
        string[] fallbackChain,
        decimal? defaultValue) {
        foreach (string conceptName in fallbackChain) {
            if (yearData.TryGetValue(conceptName, out decimal value))
                return value;
        }
        return defaultValue;
    }

    /// <summary>
    /// Resolve equity with derived fallbacks. Some companies (e.g. NVDA, MSFT) only tag
    /// StockholdersEquity in the Statement of Changes in Equity (with stale end_dates),
    /// not on the balance sheet. We prefer equity derived from balance sheet totals:
    ///   1. LiabilitiesAndStockholdersEquity - Liabilities - NCI - RedeemableNCI
    ///   2. Assets - Liabilities - NCI - RedeemableNCI
    ///   3. Direct StockholdersEquity / fallback concepts
    /// NCI (noncontrolling interest) and RedeemableNCI (mezzanine equity) are subtracted
    /// because L&SE includes all equity holders, not just stockholders.
    /// </summary>
    internal static decimal? ResolveEquity(IReadOnlyDictionary<string, decimal> yearData) {
        decimal? liabilitiesAndEquity = ResolveField(yearData, LiabilitiesAndEquityChain, null);
        decimal? liabilities = ResolveField(yearData, LiabilitiesChain, null);
        decimal? assets = ResolveField(yearData, AssetsChain, null);

        decimal? totalEquity = null;
        if (liabilitiesAndEquity.HasValue && liabilities.HasValue)
            totalEquity = liabilitiesAndEquity.Value - liabilities.Value;
        else if (assets.HasValue && liabilities.HasValue)
            totalEquity = assets.Value - liabilities.Value;

        if (totalEquity.HasValue) {
            decimal nci = ResolveField(yearData, NciChain, 0m)!.Value;
            decimal redeemableNci = ResolveField(yearData, RedeemableNciChain, 0m)!.Value;
            return totalEquity.Value - nci - redeemableNci;
        }

        // Direct equity concepts (including MembersEquity, PartnersCapital)
        return ResolveField(yearData, EquityChain, null);
    }

    /// <summary>
    /// Fix F: Resolve depletion + amortization with computed fallback.
    /// 1. If Depletion or AmortizationOfIntangibleAssets is found, use them (0 for missing).
    /// 2. Otherwise, compute DepreciationDepletionAndAmortization - Depreciation.
    /// 3. If that's not possible, return 0.
    /// </summary>
    internal static decimal ResolveDepletionAndAmortization(IReadOnlyDictionary<string, decimal> yearData) {
        decimal? depletion = ResolveField(yearData, DepletionChain, null);
        decimal? amortization = ResolveField(yearData, AmortizationChain, null);

        if (depletion.HasValue || amortization.HasValue)
            return (depletion ?? 0m) + (amortization ?? 0m);

        // Fallback: DDA - Depreciation = depletion + amortization
        decimal? dda = ResolveField(yearData, DDAChain, null);
        decimal? depreciation = ResolveField(yearData, DepreciationChain, null);
        if (dda.HasValue && depreciation.HasValue)
            return dda.Value - depreciation.Value;

        return 0m;
    }

    /// <summary>
    /// Fix G: Resolve deferred tax with component sum fallback.
    /// 1. If DeferredIncomeTaxExpenseBenefit or DeferredIncomeTaxesAndTaxCredits is found, use it.
    /// 2. Otherwise, sum Federal + Foreign + State components (0 for missing, but at least one must exist).
    /// 3. If nothing is found, return 0.
    /// </summary>
    internal static decimal ResolveDeferredTax(IReadOnlyDictionary<string, decimal> yearData) {
        decimal? aggregate = ResolveField(yearData, DeferredTaxChain, null);
        if (aggregate.HasValue)
            return aggregate.Value;

        decimal? federal = ResolveField(yearData, DeferredTaxFederalChain, null);
        decimal? foreign = ResolveField(yearData, DeferredTaxForeignChain, null);
        decimal? state = ResolveField(yearData, DeferredTaxStateChain, null);

        if (federal.HasValue || foreign.HasValue || state.HasValue)
            return (federal ?? 0m) + (foreign ?? 0m) + (state ?? 0m);

        return 0m;
    }

    /// <summary>
    /// Resolve other non-cash items with component sum fallback.
    /// 1. If OtherNoncashIncomeExpense is found, use it.
    /// 2. Otherwise, sum OtherNoncashExpense - OtherNoncashIncome (0 for missing, but at least one must exist).
    /// 3. If nothing is found, return 0.
    /// </summary>
    internal static decimal ResolveOtherNonCash(IReadOnlyDictionary<string, decimal> yearData) {
        decimal? aggregate = ResolveField(yearData, OtherNonCashChain, null);
        if (aggregate.HasValue)
            return aggregate.Value;

        decimal? expense = ResolveField(yearData, OtherNonCashExpenseChain, null);
        decimal? income = ResolveField(yearData, OtherNonCashIncomeChain, null);

        if (expense.HasValue || income.HasValue)
            return (expense ?? 0m) - (income ?? 0m);

        return 0m;
    }

    /// <summary>
    /// Resolve working capital change with component sum fallback.
    /// 1. If IncreaseDecreaseInOperatingCapital or IncreaseDecreaseInOtherOperatingCapitalNet exists, use it.
    /// 2. Otherwise, sum individual components with overlap-aware grouping:
    ///    - Receivables: prefer combined AccountsAndOtherReceivables, else AR + OtherReceivables
    ///    - Inventories
    ///    - Payables: prefer combined AP+AccruedLiabilities, else AP + AccruedLiabilities
    ///    - Prepaid/deferred assets
    ///    - Revenue: prefer DeferredRevenue, else ContractWithCustomerLiability
    ///    - Other operating assets/liabilities
    ///
    /// XBRL sign convention: IncreaseDecrease values represent the direction of change
    /// (positive = asset/liability increased). Credit-balance concepts (assets) must be
    /// negated to convert to cash flow impact: an increase in assets uses cash.
    /// Debit-balance concepts (liabilities) already have the correct sign: an increase
    /// in liabilities provides cash.
    /// </summary>
    internal static decimal ResolveWorkingCapitalChange(
        IReadOnlyDictionary<string, decimal> yearData,
        IReadOnlyDictionary<string, TaxonomyBalanceTypes> balanceTypes) {
        decimal? aggregate = ResolveField(yearData, WorkingCapitalChangeChain, null);
        if (aggregate.HasValue)
            return aggregate.Value;

        decimal sum = 0m;
        bool foundAny = false;

        // Track whether broad combined concepts are used, to avoid double-counting
        bool usedARAndOtherAssets = false;
        bool usedAccruedAndOtherLiab = false;

        // Receivables group: prefer broadest combined concept, then narrower, then individual
        // AccountsReceivableAndOtherOperatingAssets subsumes AR + OtherOperatingAssets (e.g. AMZN)
        if (AccumulateWcField(yearData, balanceTypes, ["IncreaseDecreaseInAccountsReceivableAndOtherOperatingAssets"], ref sum)) {
            foundAny = true;
            usedARAndOtherAssets = true;
        } else {
            // Receivables (broadest pure-receivables, 273 exclusive) > AccountsAndOtherReceivables > AccountsAndNotesReceivable > AR + OtherReceivables
            if (!AccumulateWcField(yearData, balanceTypes, [
                    "IncreaseDecreaseInReceivables",
                    "IncreaseDecreaseInAccountsAndOtherReceivables",
                    "IncreaseDecreaseInAccountsAndNotesReceivable"], ref sum, ref foundAny)) {
                AccumulateWcField(yearData, balanceTypes, ["IncreaseDecreaseInAccountsReceivable"], ref sum, ref foundAny);
                AccumulateWcField(yearData, balanceTypes, ["IncreaseDecreaseInOtherReceivables"], ref sum, ref foundAny);
            }
        }

        // Inventories
        AccumulateWcField(yearData, balanceTypes, ["IncreaseDecreaseInInventories"], ref sum, ref foundAny);

        // Payables + accrued + other liabilities: prefer broadest combined, then narrower
        // AccruedLiabilitiesAndOtherOperatingLiabilities subsumes AccruedLiabilities + OtherOperatingLiabilities (e.g. AMZN)
        if (AccumulateWcField(yearData, balanceTypes, ["IncreaseDecreaseInAccruedLiabilitiesAndOtherOperatingLiabilities"], ref sum)) {
            foundAny = true;
            usedAccruedAndOtherLiab = true;
            // Still pick up AP separately since this combined concept doesn't include AP
            AccumulateWcField(yearData, balanceTypes, ["IncreaseDecreaseInAccountsPayable", "IncreaseDecreaseInAccountsPayableTrade"], ref sum, ref foundAny);
        } else {
            if (!AccumulateWcField(yearData, balanceTypes, [
                    "IncreaseDecreaseInAccountsPayableAndAccruedLiabilities",
                    "IncreaseDecreaseInOtherAccountsPayableAndAccruedLiabilities"], ref sum, ref foundAny)) {
                AccumulateWcField(yearData, balanceTypes, ["IncreaseDecreaseInAccountsPayable", "IncreaseDecreaseInAccountsPayableTrade"], ref sum, ref foundAny);
                AccumulateWcField(yearData, balanceTypes, ["IncreaseDecreaseInAccruedLiabilities", "IncreaseDecreaseInOtherAccruedLiabilities"], ref sum, ref foundAny);
            }
        }

        // Prepaid/deferred expenses and other assets: prefer broader, fall back to narrower
        if (!AccumulateWcField(yearData, balanceTypes, ["IncreaseDecreaseInPrepaidDeferredExpenseAndOtherAssets"], ref sum, ref foundAny))
            AccumulateWcField(yearData, balanceTypes, ["IncreaseDecreaseInPrepaidExpense"], ref sum, ref foundAny);

        // Deferred revenue / contract liabilities: prefer deferred revenue, else contract liability
        if (!AccumulateWcField(yearData, balanceTypes, ["IncreaseDecreaseInDeferredRevenue"], ref sum, ref foundAny))
            AccumulateWcField(yearData, balanceTypes, ["IncreaseDecreaseInContractWithCustomerLiability"], ref sum, ref foundAny);

        // Contract with customer asset (52 companies, credit/asset — separate from contract liability)
        AccumulateWcField(yearData, balanceTypes, ["IncreaseDecreaseInContractWithCustomerAsset"], ref sum, ref foundAny);

        // Other operating assets: prefer general, else current + noncurrent
        if (!usedARAndOtherAssets) {
            if (!AccumulateWcField(yearData, balanceTypes, ["IncreaseDecreaseInOtherOperatingAssets"], ref sum, ref foundAny)) {
                AccumulateWcField(yearData, balanceTypes, ["IncreaseDecreaseInOtherCurrentAssets"], ref sum, ref foundAny);
                AccumulateWcField(yearData, balanceTypes, ["IncreaseDecreaseInOtherNoncurrentAssets"], ref sum, ref foundAny);
            }
        } else {
            // ARAndOtherOperatingAssets covers current operating assets; noncurrent is separate
            AccumulateWcField(yearData, balanceTypes, ["IncreaseDecreaseInOtherNoncurrentAssets"], ref sum, ref foundAny);
        }

        // Other operating liabilities: skip if AccruedLiabilitiesAndOtherOperatingLiabilities already included them
        if (!usedAccruedAndOtherLiab) {
            if (!AccumulateWcField(yearData, balanceTypes, ["IncreaseDecreaseInOtherOperatingLiabilities"], ref sum, ref foundAny)) {
                AccumulateWcField(yearData, balanceTypes, ["IncreaseDecreaseInOtherCurrentLiabilities"], ref sum, ref foundAny);
                AccumulateWcField(yearData, balanceTypes, ["IncreaseDecreaseInOtherNoncurrentLiabilities"], ref sum, ref foundAny);
            }
        }

        // Accrued income taxes payable (standalone, not subsumed by other groups)
        AccumulateWcField(yearData, balanceTypes, ["IncreaseDecreaseInAccruedIncomeTaxesPayable"], ref sum, ref foundAny);

        // Self-insurance reserve (standalone, niche)
        AccumulateWcField(yearData, balanceTypes, ["IncreaseDecreaseInSelfInsuranceReserve"], ref sum, ref foundAny);

        // Operating lease liability (175 exclusive, debit/liability)
        AccumulateWcField(yearData, balanceTypes, ["IncreaseDecreaseInOperatingLeaseLiability"], ref sum, ref foundAny);

        // Employee-related liabilities (146 exclusive, debit/liability)
        AccumulateWcField(yearData, balanceTypes, ["IncreaseDecreaseInEmployeeRelatedLiabilities"], ref sum, ref foundAny);

        // Interest payable (118 exclusive, debit/liability)
        AccumulateWcField(yearData, balanceTypes, ["IncreaseDecreaseInInterestPayableNet"], ref sum, ref foundAny);

        // Income taxes receivable (105 exclusive, credit/asset)
        AccumulateWcField(yearData, balanceTypes, ["IncreaseDecreaseInIncomeTaxesReceivable"], ref sum, ref foundAny);

        return foundAny ? sum : 0m;
    }

    /// <summary>
    /// Resolve a WC field from the fallback chain and accumulate into sum with sign correction.
    /// Credit-balance concepts (asset changes) are negated; debit-balance (liability changes) are kept.
    /// Returns true if a value was found.
    /// </summary>
    private static bool AccumulateWcField(
        IReadOnlyDictionary<string, decimal> yearData,
        IReadOnlyDictionary<string, TaxonomyBalanceTypes> balanceTypes,
        string[] chain,
        ref decimal sum,
        ref bool foundAny) {
        if (AccumulateWcField(yearData, balanceTypes, chain, ref sum)) {
            foundAny = true;
            return true;
        }
        return false;
    }

    private static bool AccumulateWcField(
        IReadOnlyDictionary<string, decimal> yearData,
        IReadOnlyDictionary<string, TaxonomyBalanceTypes> balanceTypes,
        string[] chain,
        ref decimal sum) {
        foreach (string conceptName in chain) {
            if (yearData.TryGetValue(conceptName, out decimal value)) {
                // Credit-balance = asset concept: negate (positive increase = cash outflow)
                // Debit-balance = liability concept: keep as-is (positive increase = cash inflow)
                if (balanceTypes.TryGetValue(conceptName, out TaxonomyBalanceTypes bt)
                    && bt == TaxonomyBalanceTypes.Credit) {
                    sum -= value;
                } else {
                    sum += value;
                }
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Computes the maximum buy price using the TSX formula:
    /// min(3 × bookValue, 20 × (avgNCF - dividends), 20 × (avgOE - dividends)) / shares
    /// Returns null if any required input is missing or shares ≤ 0.
    /// </summary>
    internal static decimal? ComputeMaxBuyPrice(
        decimal? bookValue,
        decimal? averageNetCashFlow,
        decimal? averageOwnerEarnings,
        decimal? currentDividendsPaid,
        long? sharesOutstanding) {
        if (!bookValue.HasValue || !averageNetCashFlow.HasValue
            || !averageOwnerEarnings.HasValue || !currentDividendsPaid.HasValue
            || !sharesOutstanding.HasValue || sharesOutstanding.Value <= 0)
            return null;

        decimal val1 = 3m * bookValue.Value;
        decimal val2 = 20m * (averageNetCashFlow.Value - currentDividendsPaid.Value);
        decimal val3 = 20m * (averageOwnerEarnings.Value - currentDividendsPaid.Value);

        decimal minVal = val1;
        if (val2 < minVal) minVal = val2;
        if (val3 < minVal) minVal = val3;

        return minVal / sharesOutstanding.Value;
    }

    /// <summary>
    /// Computes the percentage upside: (maxBuyPrice - pricePerShare) / pricePerShare × 100.
    /// Returns null if either input is missing or price is zero.
    /// </summary>
    internal static decimal? ComputePercentageUpside(decimal? maxBuyPrice, decimal? pricePerShare) {
        if (!maxBuyPrice.HasValue || !pricePerShare.HasValue || pricePerShare.Value == 0m)
            return null;

        return (maxBuyPrice.Value - pricePerShare.Value) / pricePerShare.Value * 100m;
    }

    internal static DerivedMetrics ComputeDerivedMetrics(
        IReadOnlyDictionary<int, IReadOnlyDictionary<string, decimal>> annualDataByYear,
        IReadOnlyDictionary<string, decimal> mostRecentSnapshot,
        decimal? oldestRetainedEarnings,
        decimal? pricePerShare,
        long? sharesOutstanding,
        IReadOnlyDictionary<string, TaxonomyBalanceTypes>? balanceTypes = null) {

        if (annualDataByYear.Count == 0)
            return new DerivedMetrics(null, null, null, null, null, null, null, null, null, null, null, null);

        // Balance sheet values from most recent snapshot (may be quarterly)
        decimal? equity = ResolveEquity(mostRecentSnapshot);
        decimal? goodwill = ResolveField(mostRecentSnapshot, GoodwillChain, 0m);
        decimal? intangibles = ResolveField(mostRecentSnapshot, IntangiblesChain, 0m);
        decimal? debt = ResolveField(mostRecentSnapshot, DebtChain, 0m);
        decimal? retainedEarnings = ResolveField(mostRecentSnapshot, RetainedEarningsChain, null);

        // Book Value
        decimal? bookValue = null;
        if (equity.HasValue)
            bookValue = equity.Value - (goodwill!.Value + intangibles!.Value);

        // Market Cap
        decimal? marketCap = null;
        if (pricePerShare.HasValue && sharesOutstanding.HasValue)
            marketCap = pricePerShare.Value * sharesOutstanding.Value;

        // Ratios
        decimal? debtToEquityRatio = null;
        if (debt.HasValue && equity.HasValue && equity.Value != 0m)
            debtToEquityRatio = debt.Value / equity.Value;

        decimal? priceToBookRatio = null;
        if (marketCap.HasValue && bookValue.HasValue && bookValue.Value != 0m)
            priceToBookRatio = marketCap.Value / bookValue.Value;

        decimal? debtToBookRatio = null;
        if (debt.HasValue && bookValue.HasValue && bookValue.Value != 0m)
            debtToBookRatio = debt.Value / bookValue.Value;

        // Per-year computations for averages and totals (annual only)
        var sortedYears = new List<int>(annualDataByYear.Keys);
        sortedYears.Sort();

        decimal totalNetCashFlow = 0m;
        decimal totalOwnerEarnings = 0m;
        decimal totalDividends = 0m;
        decimal totalStockIssuance = 0m;
        decimal totalPreferredIssuance = 0m;
        int yearsWithNCF = 0;
        int yearsWithOE = 0;
        bool hasAnyCashFlow = false;
        bool hasAnyOwnerEarnings = false;

        foreach (int year in sortedYears) {
            IReadOnlyDictionary<string, decimal> yearData = annualDataByYear[year];

            // Net Cash Flow for this year
            decimal? grossCashFlow = ResolveField(yearData, CashChangeChain, null);
            if (grossCashFlow.HasValue) {
                hasAnyCashFlow = true;
                decimal debtProceeds = ResolveField(yearData, DebtProceedsChain, 0m)!.Value;
                decimal debtRepayments = ResolveField(yearData, DebtRepaymentsChain, 0m)!.Value;
                decimal netDebtIssuance = debtProceeds - debtRepayments;

                decimal stockProceeds = ResolveField(yearData, StockProceedsChain, 0m)!.Value;
                decimal stockRepurchase = ResolveField(yearData, StockRepurchaseChain, 0m)!.Value;
                decimal netStockIssuance = stockProceeds - stockRepurchase;

                decimal preferredProceeds = ResolveField(yearData, PreferredProceedsChain, 0m)!.Value;
                decimal preferredRepurchase = ResolveField(yearData, PreferredRepurchaseChain, 0m)!.Value;
                decimal netPreferredIssuance = preferredProceeds - preferredRepurchase;

                decimal netCashFlow = grossCashFlow.Value
                    - (netDebtIssuance + netStockIssuance + netPreferredIssuance);
                totalNetCashFlow += netCashFlow;
                yearsWithNCF++;
            }

            // Owner Earnings for this year
            decimal? netIncome = ResolveField(yearData, NetIncomeChain, null);
            if (netIncome.HasValue) {
                hasAnyOwnerEarnings = true;
                decimal depletionAndAmortization = ResolveDepletionAndAmortization(yearData);
                decimal deferredTax = ResolveDeferredTax(yearData);
                decimal otherNonCash = ResolveOtherNonCash(yearData);
                decimal capEx = ResolveField(yearData, CapExChain, 0m)!.Value;
                decimal workingCapitalChange = ResolveWorkingCapitalChange(yearData,
                    balanceTypes ?? EmptyBalanceTypes);

                // Simplified OE formula (Depreciation cancels out)
                decimal ownerEarnings = netIncome.Value
                    + depletionAndAmortization + deferredTax + otherNonCash
                    - capEx + workingCapitalChange;
                totalOwnerEarnings += ownerEarnings;
                yearsWithOE++;
            }

            // Accumulate totals for Adjusted Retained Earnings
            decimal dividends = ResolveField(yearData, DividendsChain, 0m)!.Value;
            totalDividends += dividends;

            decimal stockProc = ResolveField(yearData, StockProceedsChain, 0m)!.Value;
            decimal stockRepurch = ResolveField(yearData, StockRepurchaseChain, 0m)!.Value;
            totalStockIssuance += (stockProc - stockRepurch);

            decimal prefProc = ResolveField(yearData, PreferredProceedsChain, 0m)!.Value;
            decimal prefRepurch = ResolveField(yearData, PreferredRepurchaseChain, 0m)!.Value;
            totalPreferredIssuance += (prefProc - prefRepurch);
        }

        // Averages
        decimal? averageNetCashFlow = null;
        if (hasAnyCashFlow && yearsWithNCF > 0)
            averageNetCashFlow = totalNetCashFlow / yearsWithNCF;

        decimal? averageOwnerEarnings = null;
        if (hasAnyOwnerEarnings && yearsWithOE > 0)
            averageOwnerEarnings = totalOwnerEarnings / yearsWithOE;

        // Adjusted Retained Earnings (current RE from snapshot, totals from annual)
        decimal? adjustedRetainedEarnings = null;
        if (retainedEarnings.HasValue)
            adjustedRetainedEarnings = retainedEarnings.Value + totalDividends
                - totalStockIssuance - totalPreferredIssuance;

        // Current dividends from most recent annual year (duration concept, not balance sheet)
        int mostRecentAnnualYear = sortedYears[sortedYears.Count - 1];
        decimal? currentDividendsPaid = ResolveField(annualDataByYear[mostRecentAnnualYear], DividendsChain, 0m);

        // Estimated Returns
        decimal? estimatedReturnCF = null;
        if (averageNetCashFlow.HasValue && currentDividendsPaid.HasValue && marketCap.HasValue && marketCap.Value != 0m)
            estimatedReturnCF = 100m * (averageNetCashFlow.Value - currentDividendsPaid.Value) / marketCap.Value;

        decimal? estimatedReturnOE = null;
        if (averageOwnerEarnings.HasValue && currentDividendsPaid.HasValue && marketCap.HasValue && marketCap.Value != 0m)
            estimatedReturnOE = 100m * (averageOwnerEarnings.Value - currentDividendsPaid.Value) / marketCap.Value;

        return new DerivedMetrics(
            bookValue,
            marketCap,
            debtToEquityRatio,
            priceToBookRatio,
            debtToBookRatio,
            adjustedRetainedEarnings,
            oldestRetainedEarnings,
            averageNetCashFlow,
            averageOwnerEarnings,
            estimatedReturnCF,
            estimatedReturnOE,
            currentDividendsPaid);
    }

    internal static IReadOnlyList<ScoringCheck> EvaluateChecks(DerivedMetrics metrics, int yearsOfData) {
        var checks = new List<ScoringCheck>(13);

        // Check 1: Debt-to-Equity < 0.5
        checks.Add(MakeCheck(1, "Debt-to-Equity", metrics.DebtToEquityRatio, "< 0.5",
            metrics.DebtToEquityRatio.HasValue
                ? (metrics.DebtToEquityRatio.Value < 0.5m ? ScoringCheckResult.Pass : ScoringCheckResult.Fail)
                : ScoringCheckResult.NotAvailable));

        // Check 2: Book Value > $150M
        checks.Add(MakeCheck(2, "Book Value", metrics.BookValue, "> $150M",
            metrics.BookValue.HasValue
                ? (metrics.BookValue.Value > 150_000_000m ? ScoringCheckResult.Pass : ScoringCheckResult.Fail)
                : ScoringCheckResult.NotAvailable));

        // Check 3: Price-to-Book ≤ 3.0
        checks.Add(MakeCheck(3, "Price-to-Book", metrics.PriceToBookRatio, "≤ 3.0",
            metrics.PriceToBookRatio.HasValue
                ? (metrics.PriceToBookRatio.Value <= 3.0m ? ScoringCheckResult.Pass : ScoringCheckResult.Fail)
                : ScoringCheckResult.NotAvailable));

        // Check 4: Average Net Cash Flow > 0
        checks.Add(MakeCheck(4, "Avg Net Cash Flow Positive", metrics.AverageNetCashFlow, "> 0",
            metrics.AverageNetCashFlow.HasValue
                ? (metrics.AverageNetCashFlow.Value > 0m ? ScoringCheckResult.Pass : ScoringCheckResult.Fail)
                : ScoringCheckResult.NotAvailable));

        // Check 5: Average Owner Earnings > 0
        checks.Add(MakeCheck(5, "Avg Owner Earnings Positive", metrics.AverageOwnerEarnings, "> 0",
            metrics.AverageOwnerEarnings.HasValue
                ? (metrics.AverageOwnerEarnings.Value > 0m ? ScoringCheckResult.Pass : ScoringCheckResult.Fail)
                : ScoringCheckResult.NotAvailable));

        // Check 6: Est. Return (CF) > 5%
        checks.Add(MakeCheck(6, "Est. Return (CF) Big Enough", metrics.EstimatedReturnCF, "> 5%",
            metrics.EstimatedReturnCF.HasValue
                ? (metrics.EstimatedReturnCF.Value > 5m ? ScoringCheckResult.Pass : ScoringCheckResult.Fail)
                : ScoringCheckResult.NotAvailable));

        // Check 7: Est. Return (OE) > 5%
        checks.Add(MakeCheck(7, "Est. Return (OE) Big Enough", metrics.EstimatedReturnOE, "> 5%",
            metrics.EstimatedReturnOE.HasValue
                ? (metrics.EstimatedReturnOE.Value > 5m ? ScoringCheckResult.Pass : ScoringCheckResult.Fail)
                : ScoringCheckResult.NotAvailable));

        // Check 8: Est. Return (CF) < 40%
        checks.Add(MakeCheck(8, "Est. Return (CF) Not Too Big", metrics.EstimatedReturnCF, "< 40%",
            metrics.EstimatedReturnCF.HasValue
                ? (metrics.EstimatedReturnCF.Value < 40m ? ScoringCheckResult.Pass : ScoringCheckResult.Fail)
                : ScoringCheckResult.NotAvailable));

        // Check 9: Est. Return (OE) < 40%
        checks.Add(MakeCheck(9, "Est. Return (OE) Not Too Big", metrics.EstimatedReturnOE, "< 40%",
            metrics.EstimatedReturnOE.HasValue
                ? (metrics.EstimatedReturnOE.Value < 40m ? ScoringCheckResult.Pass : ScoringCheckResult.Fail)
                : ScoringCheckResult.NotAvailable));

        // Check 10: Debt-to-Book < 1.0
        checks.Add(MakeCheck(10, "Debt-to-Book", metrics.DebtToBookRatio, "< 1.0",
            metrics.DebtToBookRatio.HasValue
                ? (metrics.DebtToBookRatio.Value < 1.0m ? ScoringCheckResult.Pass : ScoringCheckResult.Fail)
                : ScoringCheckResult.NotAvailable));

        // Check 11: Retained Earnings Positive (Adjusted) > 0
        checks.Add(MakeCheck(11, "Retained Earnings Positive", metrics.AdjustedRetainedEarnings, "> 0",
            metrics.AdjustedRetainedEarnings.HasValue
                ? (metrics.AdjustedRetainedEarnings.Value > 0m ? ScoringCheckResult.Pass : ScoringCheckResult.Fail)
                : ScoringCheckResult.NotAvailable));

        // Check 12: History ≥ 4 years
        checks.Add(MakeCheck(12, "History Long Enough", yearsOfData, "≥ 4 years",
            yearsOfData >= 4 ? ScoringCheckResult.Pass : ScoringCheckResult.Fail));

        // Check 13: Retained Earnings Increased (Adjusted > Oldest)
        ScoringCheckResult check13Result;
        if (metrics.AdjustedRetainedEarnings.HasValue && metrics.OldestRetainedEarnings.HasValue)
            check13Result = metrics.AdjustedRetainedEarnings.Value > metrics.OldestRetainedEarnings.Value
                ? ScoringCheckResult.Pass : ScoringCheckResult.Fail;
        else
            check13Result = ScoringCheckResult.NotAvailable;

        decimal? check13Value = null;
        if (metrics.AdjustedRetainedEarnings.HasValue && metrics.OldestRetainedEarnings.HasValue)
            check13Value = metrics.AdjustedRetainedEarnings.Value - metrics.OldestRetainedEarnings.Value;

        checks.Add(MakeCheck(13, "Retained Earnings Increased", check13Value, "increased", check13Result));

        return checks;
    }

    private static ScoringCheck MakeCheck(int number, string name, decimal? value, string threshold, ScoringCheckResult result) {
        return new ScoringCheck(number, name, value, threshold, result);
    }

    internal record GroupedScoringData(
        Dictionary<int, Dictionary<string, decimal>> AnnualByYear,
        Dictionary<string, TaxonomyBalanceTypes> BalanceTypes,
        Dictionary<string, decimal> MostRecentSnapshot,
        decimal? OldestRetainedEarnings);

    internal static GroupedScoringData GroupAndPartitionData(IReadOnlyCollection<ScoringConceptValue> values) {
        var balanceTypes = new Dictionary<string, TaxonomyBalanceTypes>(StringComparer.Ordinal);
        var annualByYear = new Dictionary<int, Dictionary<string, decimal>>();

        // Track most recent and oldest report dates across ALL filing types
        DateOnly mostRecentDate = DateOnly.MinValue;
        DateOnly oldestDate = DateOnly.MaxValue;

        foreach (ScoringConceptValue v in values) {
            if (v.ReportDate > mostRecentDate)
                mostRecentDate = v.ReportDate;
            if (v.ReportDate < oldestDate)
                oldestDate = v.ReportDate;

            // Record balance type per concept (consistent across years)
            if (!balanceTypes.ContainsKey(v.ConceptName))
                balanceTypes[v.ConceptName] = (TaxonomyBalanceTypes)v.BalanceTypeId;

            // Annual partition: only 10-K
            if (v.FilingTypeId == (int)FilingType.TenK) {
                int year = v.ReportDate.Year;
                if (!annualByYear.ContainsKey(year))
                    annualByYear[year] = new Dictionary<string, decimal>(StringComparer.Ordinal);

                // Take the first value for each concept per year (skip duplicates)
                if (!annualByYear[year].ContainsKey(v.ConceptName))
                    annualByYear[year][v.ConceptName] = v.Value;
            }
        }

        // Build most recent snapshot from values matching mostRecentDate (any filing type).
        // NOTE: This snapshot contains ALL concepts at the most recent date, including duration
        // concepts (e.g. NetIncomeLoss) that are quarterly if the latest filing is a 10-Q.
        // Callers must only read instant/balance-sheet concepts from this snapshot.
        var mostRecentSnapshot = new Dictionary<string, decimal>(StringComparer.Ordinal);
        decimal? oldestRetainedEarnings = null;

        if (mostRecentDate != DateOnly.MinValue) {
            foreach (ScoringConceptValue v in values) {
                if (v.ReportDate == mostRecentDate && !mostRecentSnapshot.ContainsKey(v.ConceptName))
                    mostRecentSnapshot[v.ConceptName] = v.Value;
            }
        }

        // Extract oldest retained earnings from values matching oldestDate (any filing type)
        if (oldestDate != DateOnly.MaxValue) {
            // Build a lookup for the oldest date's values, then resolve via the chain
            var oldestDateValues = new Dictionary<string, decimal>(StringComparer.Ordinal);
            foreach (ScoringConceptValue v in values) {
                if (v.ReportDate == oldestDate && !oldestDateValues.ContainsKey(v.ConceptName))
                    oldestDateValues[v.ConceptName] = v.Value;
            }
            oldestRetainedEarnings = ResolveField(oldestDateValues, RetainedEarningsChain, null);
        }

        return new GroupedScoringData(annualByYear, balanceTypes, mostRecentSnapshot, oldestRetainedEarnings);
    }
}
