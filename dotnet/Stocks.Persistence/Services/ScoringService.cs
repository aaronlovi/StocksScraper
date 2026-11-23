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
        // Debt beyond the long-term chain counted in the debt ratios (E4)
        "LongTermDebtCurrent",
        "ShortTermBorrowings",
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
        "ProceedsFromIssuanceOfSecuredDebt",
        "ProceedsFromIssuanceOfUnsecuredDebt",
        "ProceedsFromIssuanceOfSubordinatedLongTermDebt",
        "ProceedsFromIssuanceOfMediumTermNotes",
        "ProceedsFromIssuanceOfFirstMortgageBond",
        "ProceedsFromIssuanceOfOtherLongTermDebt",
        "ProceedsFromLongTermLinesOfCredit",
        "ProceedsFromRelatedPartyDebt",
        // Cash flow — debt repayments (90 exclusive for Notes, 53 for Senior)
        "RepaymentsOfLongTermDebt",
        "RepaymentsOfDebt",
        "RepaymentsOfConvertibleDebt",
        "RepaymentsOfNotesPayable",
        "RepaymentsOfSeniorDebt",
        "RepaymentsOfSecuredDebt",
        "RepaymentsOfUnsecuredDebt",
        "RepaymentsOfSubordinatedDebt",
        "RepaymentsOfMediumTermNotes",
        "RepaymentsOfFirstMortgageBond",
        "RepaymentsOfOtherLongTermDebt",
        "RepaymentsOfLongTermLinesOfCredit",
        "RepaymentsOfRelatedPartyDebt",
        // Cash flow — long-term debt reported net only
        "ProceedsFromRepaymentsOfRelatedPartyDebt",
        "ProceedsFromRepaymentsOfSecuredDebt",
        "ProceedsFromRepaymentsOfNotesPayable",
        "ProceedsFromRepaymentsOfOtherLongTermDebt",
        // Cash flow — dividends (24 exclusive for CommonStockCash)
        "PaymentsOfDividends",
        "PaymentsOfDividendsCommonStock",
        "Dividends",
        "DividendsCommonStockCash",
        // Cash flow — stock issuance/repurchase (540 exclusive for Options, 41 for Equity)
        "ProceedsFromIssuanceOfCommonStock",
        "ProceedsFromStockOptionsExercised",
        "ProceedsFromIssuanceOrSaleOfEquity",
        "ProceedsFromIssuanceInitialPublicOffering",
        "ProceedsFromIssuanceOfPrivatePlacement",
        "ProceedsFromIssuanceOfWarrants",
        "ProceedsFromWarrantExercises",
        "ProceedsFromStockPlans",
        "ProceedsFromIssuanceOfCommonLimitedPartnersUnits",
        "ProceedsFromContributedCapital",
        "ProceedsFromIssuanceOfConvertiblePreferredStock",
        "ProceedsFromIssuanceOfSharesUnderIncentiveAndShareBasedCompensationPlansIncludingStockOptions",
        "PaymentsForRepurchaseOfCommonStock",
        "PaymentsForRepurchaseOfEquity",
        // Cash flow — financing-activities total (net cash flow of financials, E5)
        "NetCashProvidedByUsedInFinancingActivities",
        "NetCashProvidedByUsedInFinancingActivitiesContinuingOperations",
        // Cash flow — short-term debt (net), lines of credit, commercial paper
        "ProceedsFromRepaymentsOfShortTermDebt",
        "ProceedsFromRepaymentsOfLinesOfCredit",
        "ProceedsFromLinesOfCredit",
        "RepaymentsOfLinesOfCredit",
        "ProceedsFromRepaymentsOfCommercialPaper",
        "ProceedsFromShortTermDebt",
        "ProceedsFromNotesPayable",
        "ProceedsFromBankDebt",
        "ProceedsFromOtherShortTermDebt",
        "RepaymentsOfShortTermDebt",
        "RepaymentsOfBankDebt",
        "RepaymentsOfOtherShortTermDebt",
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
        "PaymentsToAcquireProductiveAssets",
        "PaymentsToAcquireOilAndGasPropertyAndEquipment",
        "PaymentsToAcquireOtherProductiveAssets",
        "PaymentsToAcquireOtherPropertyPlantAndEquipment",
        "PaymentsForCapitalImprovements",
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
    internal static readonly string[] FinancingTotalChain = [
        "NetCashProvidedByUsedInFinancingActivities",
        "NetCashProvidedByUsedInFinancingActivitiesContinuingOperations"
    ];
    internal static readonly string[] DividendsChain = [
        "PaymentsOfDividends", "PaymentsOfDividendsCommonStock", "Dividends", "DividendsCommonStockCash"
    ];
    internal static readonly string[] StockRepurchaseChain = [
        "PaymentsForRepurchaseOfCommonStock", "PaymentsForRepurchaseOfEquity"
    ];
    // Debt and share lines for net cash flow (E12), by parent in the us-gaap cash-flow
    // calculation tree. These are siblings to sum, not fallback chains.
    internal static readonly string[] LongTermDebtProceedsLines = [
        "ProceedsFromIssuanceOfSeniorLongTermDebt",
        "ProceedsFromConvertibleDebt",
        "ProceedsFromIssuanceOfSecuredDebt",
        "ProceedsFromIssuanceOfUnsecuredDebt",
        "ProceedsFromIssuanceOfSubordinatedLongTermDebt",
        "ProceedsFromIssuanceOfMediumTermNotes",
        "ProceedsFromIssuanceOfFirstMortgageBond",
        "ProceedsFromIssuanceOfOtherLongTermDebt",
        "ProceedsFromLongTermLinesOfCredit",
        "ProceedsFromRelatedPartyDebt",
    ];
    internal static readonly string[] LongTermDebtRepaymentLines = [
        "RepaymentsOfConvertibleDebt",
        "RepaymentsOfSeniorDebt",
        "RepaymentsOfSecuredDebt",
        "RepaymentsOfUnsecuredDebt",
        "RepaymentsOfSubordinatedDebt",
        "RepaymentsOfMediumTermNotes",
        "RepaymentsOfFirstMortgageBond",
        "RepaymentsOfOtherLongTermDebt",
        "RepaymentsOfLongTermLinesOfCredit",
        "RepaymentsOfRelatedPartyDebt",
    ];
    internal static readonly string[] ShortTermDebtProceedsLines = [
        "ProceedsFromNotesPayable",
        "ProceedsFromBankDebt",
        "ProceedsFromLinesOfCredit",
        "ProceedsFromOtherShortTermDebt",
    ];
    internal static readonly string[] ShortTermDebtRepaymentLines = [
        "RepaymentsOfNotesPayable",
        "RepaymentsOfBankDebt",
        "RepaymentsOfLinesOfCredit",
        "RepaymentsOfOtherShortTermDebt",
    ];
    // Share issuance other than employee plans and preferred stock
    internal static readonly string[] ShareIssuanceLines = [
        "ProceedsFromIssuanceOfCommonStock",
        "ProceedsFromIssuanceInitialPublicOffering",
        "ProceedsFromIssuanceOfPrivatePlacement",
        "ProceedsFromIssuanceOfWarrants",
        "ProceedsFromWarrantExercises",
        "ProceedsFromStockPlans",
        "ProceedsFromIssuanceOfCommonLimitedPartnersUnits",
        "ProceedsFromContributedCapital",
        "ProceedsFromIssuanceOfConvertiblePreferredStock",
    ];
    private sealed record NetDebtLine(string Net, string Proceeds, string Repayments);
    // Net tags that some filers use alone; ignored when either gross side is present.
    private static readonly NetDebtLine[] NetOnlyDebtLines = [
        new("ProceedsFromRepaymentsOfRelatedPartyDebt", "ProceedsFromRelatedPartyDebt", "RepaymentsOfRelatedPartyDebt"),
        new("ProceedsFromRepaymentsOfSecuredDebt", "ProceedsFromIssuanceOfSecuredDebt", "RepaymentsOfSecuredDebt"),
        new("ProceedsFromRepaymentsOfNotesPayable", "ProceedsFromNotesPayable", "RepaymentsOfNotesPayable"),
        new("ProceedsFromRepaymentsOfOtherLongTermDebt", "ProceedsFromIssuanceOfOtherLongTermDebt", "RepaymentsOfOtherLongTermDebt"),
    ];
    internal static readonly string[] PreferredProceedsChain = ["ProceedsFromIssuanceOfPreferredStockAndPreferenceStock"];
    internal static readonly string[] PreferredRepurchaseChain = ["PaymentsForRepurchaseOfPreferredStockAndPreferenceStock"];
    // Alternatives (first found). The later four are what E&Ps, some service companies
    // and others tag instead of PP&E (plan 4.4). Real-estate development is left out: it
    // is investment, not maintenance capex.
    internal static readonly string[] CapExChain = [
        "PaymentsToAcquirePropertyPlantAndEquipment",
        "PaymentsToAcquireProductiveAssets",
        "PaymentsToAcquireOilAndGasPropertyAndEquipment",
        "PaymentsToAcquireOtherProductiveAssets",
        "PaymentsToAcquireOtherPropertyPlantAndEquipment",
        "PaymentsForCapitalImprovements",
    ];
    internal static readonly string[] DeferredTaxChain = ["DeferredIncomeTaxExpenseBenefit", "DeferredIncomeTaxesAndTaxCredits"];
    internal static readonly string[] DepletionChain = ["Depletion"];
    internal static readonly string[] AmortizationChain = ["AmortizationOfIntangibleAssets"];
    internal static readonly string[] DepreciationChain = ["Depreciation"];
    internal static readonly string[] DeferredTaxFederalChain = ["DeferredFederalIncomeTaxExpenseBenefit"];
    internal static readonly string[] DeferredTaxForeignChain = ["DeferredForeignIncomeTaxExpenseBenefit"];
    internal static readonly string[] DeferredTaxStateChain = ["DeferredStateAndLocalIncomeTaxExpenseBenefit"];
    internal static readonly string[] OtherNonCashChain = ["OtherNoncashIncomeExpense"];
    internal static readonly string[] OtherNonCashExpenseChain = ["OtherNoncashExpense"];
    internal static readonly string[] OtherNonCashIncomeChain = ["OtherNoncashIncome"];
    // The aggregate operating-capital change. IncreaseDecreaseInOtherOperatingCapitalNet is
    // its child (an "other, net" line), so it is a component, not an alternative total.
    internal static readonly string[] WorkingCapitalChangeChain = ["IncreaseDecreaseInOperatingCapital"];
    internal static readonly string[] SharesChain = ["CommonStockSharesOutstanding", "WeightedAverageNumberOfSharesOutstandingBasic", "EntityCommonStockSharesOutstanding"];

    // Balance-sheet groups filled from the latest 10-K when a newer 10-Q lacks them.
    // A group is copied whole, never mixed across dates.
    internal static readonly string[] EquityGroupConcepts = [
        "Assets", "Liabilities", "LiabilitiesAndStockholdersEquity",
        "StockholdersEquity", "StockholdersEquityIncludingPortionAttributableToNoncontrollingInterest",
        "MembersEquity", "PartnersCapital",
        "MinorityInterest", "NoncontrollingInterestInVariableInterestEntity",
        "RedeemableNoncontrollingInterestEquityCarryingAmount",
    ];
    // Debt concepts beyond the long-term chain: current portions, short-term borrowings,
    // notes, credit lines, combined totals. Fetched whole by the moat score (debt-free test
    // for interest coverage); the value score fetches only the two that ResolveTotalDebt
    // adds, so its eligible dates move as little as possible.
    internal static readonly string[] OtherDebtConcepts = [
        "LongTermDebtCurrent", "DebtCurrent", "ShortTermBorrowings", "CommercialPaper",
        "LinesOfCreditCurrent", "LongTermDebtAndCapitalLeaseObligationsCurrent", "LineOfCredit",
        "LongTermLineOfCredit", "NotesPayable", "ConvertibleNotesPayable", "SeniorNotes",
        "DebtLongtermAndShorttermCombinedAmount",
        "LongTermDebtAndCapitalLeaseObligationsIncludingCurrentMaturities",
    ];
    internal static readonly string[] DebtGroupConcepts = Concat(DebtChain, OtherDebtConcepts);
    internal static readonly string[] IndividuallyFilledBalanceSheetConcepts = [
        "Goodwill", "IntangibleAssetsNetExcludingGoodwill", "RetainedEarningsAccumulatedDeficit",
        "AssetsCurrent", "LiabilitiesCurrent",
    ];

    private static readonly Dictionary<string, TaxonomyBalanceTypes> EmptyBalanceTypes = new(StringComparer.Ordinal);

    public async Task<Result<IReadOnlyCollection<CompanyScoreSummary>>> ComputeAllScores(CancellationToken ct) =>
        ToSummaries(await ComputeAllScoresCore(null, ct));

    /// <summary>
    /// Computes scores for all companies as of a historical date: only filings publicly
    /// available (accepted) by that date, and the price at/just before that date, are used.
    /// </summary>
    public async Task<Result<IReadOnlyCollection<CompanyScoreSummary>>> ComputeAllScoresAsOf(
        DateOnly asOfDate, CancellationToken ct) =>
        ToSummaries(await ComputeAllScoresCore(asOfDate, ct));

    /// <summary>
    /// Computes scores for all companies (latest data) and returns each summary together
    /// with its individual check results. Reads only; nothing is written.
    /// </summary>
    public Task<Result<IReadOnlyCollection<CompanyScoreDetail>>> ComputeAllScoreDetails(CancellationToken ct) =>
        ComputeAllScoresCore(null, ct);

    private static Result<IReadOnlyCollection<CompanyScoreSummary>> ToSummaries(
        Result<IReadOnlyCollection<CompanyScoreDetail>> detailsResult) {
        if (detailsResult.IsFailure || detailsResult.Value is null)
            return Result<IReadOnlyCollection<CompanyScoreSummary>>.Failure(
                ErrorCodes.GenericError, detailsResult.ErrorMessage);

        var summaries = new List<CompanyScoreSummary>(detailsResult.Value.Count);
        foreach (CompanyScoreDetail detail in detailsResult.Value)
            summaries.Add(detail.Summary);
        return Result<IReadOnlyCollection<CompanyScoreSummary>>.Success(summaries);
    }

    private async Task<Result<IReadOnlyCollection<CompanyScoreDetail>>> ComputeAllScoresCore(
        DateOnly? asOfDate, CancellationToken ct) {
        // 1. Fetch all scoring data points in one query
        _logger?.LogInformation("Fetching scoring data points (as of {AsOfDate})...", asOfDate?.ToString() ?? "now");
        Result<IReadOnlyCollection<BatchScoringConceptValue>> dataResult = asOfDate.HasValue
            ? await _dbmService.GetAllScoringDataPointsAsOf(AllConceptNames, asOfDate.Value, ct)
            : await _dbmService.GetAllScoringDataPoints(AllConceptNames, ct);
        if (dataResult.IsFailure || dataResult.Value is null)
            return Result<IReadOnlyCollection<CompanyScoreDetail>>.Failure(
                ErrorCodes.GenericError, "Failed to fetch batch scoring data points.");
        _logger?.LogInformation("Fetched {Count} scoring data points", dataResult.Value.Count);

        // 2. Fetch prices for all tickers (latest, or at the as-of date)
        _logger?.LogInformation("Fetching prices...");
        Result<IReadOnlyCollection<LatestPrice>> pricesResult = asOfDate.HasValue
            ? await _dbmService.GetAllPricesNearDate(asOfDate.Value, ct)
            : await _dbmService.GetAllLatestPrices(ct);
        if (pricesResult.IsFailure || pricesResult.Value is null)
            return Result<IReadOnlyCollection<CompanyScoreDetail>>.Failure(
                ErrorCodes.GenericError, "Failed to fetch prices.");
        _logger?.LogInformation("Fetched {Count} prices", pricesResult.Value.Count);

        // 3. Fetch company tickers (company_id → ticker)
        _logger?.LogInformation("Fetching company tickers...");
        Result<IReadOnlyCollection<CompanyTicker>> tickersResult =
            await _dbmService.GetAllCompanyTickers(ct);
        if (tickersResult.IsFailure || tickersResult.Value is null)
            return Result<IReadOnlyCollection<CompanyScoreDetail>>.Failure(
                ErrorCodes.GenericError, "Failed to fetch company tickers.");
        _logger?.LogInformation("Fetched {Count} company tickers", tickersResult.Value.Count);

        // 3b. Financial companies (SIC 6000-6499) take the financing-total NCF (E5)
        Result<HashSet<ulong>> financialsResult = await LoadFinancialCompanyIds(_dbmService, ct);
        if (financialsResult.IsFailure || financialsResult.Value is null)
            return Result<IReadOnlyCollection<CompanyScoreDetail>>.Failure(
                ErrorCodes.GenericError, "Failed to fetch company SIC codes.");
        HashSet<ulong> financialIds = financialsResult.Value;

        // 4. Fetch company names (company_id → name)
        _logger?.LogInformation("Fetching company names...");
        Result<IReadOnlyCollection<CompanyName>> namesResult =
            await _dbmService.GetAllCompanyNames(ct);
        if (namesResult.IsFailure || namesResult.Value is null)
            return Result<IReadOnlyCollection<CompanyScoreDetail>>.Failure(
                ErrorCodes.GenericError, "Failed to fetch company names.");
        _logger?.LogInformation("Fetched {Count} company names", namesResult.Value.Count);

        // 5. Fetch all companies for CIK lookup
        _logger?.LogInformation("Fetching companies...");
        Result<IReadOnlyCollection<Company>> companiesResult =
            await _dbmService.GetAllCompaniesByDataSource("EDGAR", ct);
        if (companiesResult.IsFailure || companiesResult.Value is null)
            return Result<IReadOnlyCollection<CompanyScoreDetail>>.Failure(
                ErrorCodes.GenericError, "Failed to fetch companies.");
        _logger?.LogInformation("Fetched {Count} companies", companiesResult.Value.Count);

        // Build lookups
        var companyLookup = new Dictionary<ulong, Company>();
        foreach (Company c in companiesResult.Value)
            companyLookup[c.CompanyId] = c;

        Dictionary<ulong, string> companyNameLookup = BuildCompanyNameLookup(namesResult.Value, tickersResult.Value);

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
        var results = new List<CompanyScoreDetail>(dataByCompany.Count);
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
            NetCashFlowBasis ncfBasis = financialIds.Contains(companyId)
                ? NetCashFlowBasis.FinancingTotal
                : NetCashFlowBasis.Chains;
            DerivedMetrics metrics = ComputeDerivedMetrics(
                rawDataByYear, grouped.MostRecentSnapshot, grouped.OldestRetainedEarnings,
                pricePerShare, sharesOutstanding, grouped.BalanceTypes, ncfBasis);

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

            results.Add(new CompanyScoreDetail(new CompanyScoreSummary(
                companyId, cik, companyName, ticker, exchange,
                overallScore, computableChecks, yearsOfData,
                metrics.BookValue, metrics.MarketCap,
                metrics.DebtToEquityRatio, metrics.PriceToBookRatio, metrics.DebtToBookRatio,
                metrics.AdjustedRetainedEarnings, metrics.AverageNetCashFlow,
                metrics.AverageOwnerEarnings, metrics.AverageRoeCF, metrics.AverageRoeOE,
                metrics.EstimatedReturnCF, metrics.EstimatedReturnOE,
                pricePerShare, priceDate, sharesOutstanding,
                metrics.CurrentDividendsPaid, maxBuyPrice, percentageUpside, now,
                ScoringCheckResults.Encode(scorecard)), scorecard));

            scored++;
            if (scored % logInterval == 0)
                _logger?.LogInformation("Scored {Scored}/{Total} companies", scored, dataByCompany.Count);
        }

        _logger?.LogInformation("Finished scoring {Count} companies", results.Count);
        return Result<IReadOnlyCollection<CompanyScoreDetail>>.Success(results);
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

        // 7. Compute derived metrics (financials take the financing-total NCF, E5)
        Result<NetCashFlowBasis> basisResult = await LoadNetCashFlowBasis(_dbmService, companyId, ct);
        if (basisResult.IsFailure)
            return Result<ScoringResult>.Failure(ErrorCodes.GenericError, "Failed to fetch the company's SIC code.");
        DerivedMetrics metrics = ComputeDerivedMetrics(
            rawDataByYear, grouped.MostRecentSnapshot, grouped.OldestRetainedEarnings,
            pricePerShare, sharesOutstanding, grouped.BalanceTypes, basisResult.Value);

        // 8. Evaluate the 15 checks
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

    /// <summary>
    /// Display name per company: the SEC's current name from the ticker mappings (title)
    /// when the company is listed, else the first of its company_names. company_names comes
    /// from the CIK lookup file, which lists every historical name alphabetically, so its
    /// first entry is often a former name.
    /// </summary>
    internal static Dictionary<ulong, string> BuildCompanyNameLookup(
        IReadOnlyCollection<CompanyName> names, IReadOnlyCollection<CompanyTicker> tickers) {
        var lookup = new Dictionary<ulong, string>();
        foreach (CompanyTicker ticker in tickers) {
            if (!string.IsNullOrWhiteSpace(ticker.Title) && !lookup.ContainsKey(ticker.CompanyId))
                lookup[ticker.CompanyId] = ticker.Title;
        }
        foreach (CompanyName cn in names) {
            if (!lookup.ContainsKey(cn.CompanyId))
                lookup[cn.CompanyId] = cn.Name;
        }
        return lookup;
    }

    /// <summary>
    /// Debt for the debt ratios: the long-term chain (first found) + the current portion of
    /// long-term debt + short-term borrowings; missing tags count 0. Per the taxonomy
    /// LongTermDebt includes current maturities while the other chain concepts are
    /// noncurrent, so when LongTermDebt is the figure used, the current portion is added
    /// only if LongTermDebt does not cover noncurrent + current (within 0.1%). Without a
    /// noncurrent tag to compare against, LongTermDebt is taken as the total.
    /// </summary>
    internal static decimal ResolveTotalDebt(IReadOnlyDictionary<string, decimal> snapshot) {
        string? longTermConcept = null;
        decimal longTerm = 0m;
        foreach (string concept in DebtChain) {
            if (snapshot.TryGetValue(concept, out decimal value)) {
                longTermConcept = concept;
                longTerm = value;
                break;
            }
        }

        decimal current = ValueOrNull(snapshot, "LongTermDebtCurrent") ?? 0m;
        bool longTermIncludesCurrent = false;
        if (longTermConcept == "LongTermDebt" && current != 0m) {
            decimal? noncurrent = ValueOrNull(snapshot, "LongTermDebtNoncurrent");
            longTermIncludesCurrent = !noncurrent.HasValue
                || NestParent(longTerm, noncurrent.Value + current) == longTerm;
        }

        decimal total = longTerm + (ValueOrNull(snapshot, "ShortTermBorrowings") ?? 0m);
        if (!longTermIncludesCurrent)
            total += current;
        return total;
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
    /// Total depreciation, depletion and amortization:
    /// 1. DepreciationDepletionAndAmortization outright;
    /// 2. else DepreciationAndAmortization + Depletion;
    /// 3. else Depreciation + Depletion + AmortizationOfIntangibleAssets (0 for missing).
    /// </summary>
    internal static decimal ResolveTotalDa(IReadOnlyDictionary<string, decimal> yearData) {
        if (yearData.TryGetValue("DepreciationDepletionAndAmortization", out decimal dda))
            return dda;

        decimal depletion = ResolveField(yearData, DepletionChain, 0m)!.Value;
        if (yearData.TryGetValue("DepreciationAndAmortization", out decimal da))
            return da + depletion;

        decimal depreciation = ResolveField(yearData, DepreciationChain, 0m)!.Value;
        decimal amortization = ResolveField(yearData, AmortizationChain, 0m)!.Value;
        return depreciation + depletion + amortization;
    }

    /// <summary>
    /// Owner earnings for one year, shared by the value and moat scores:
    /// NI + total D&amp;A + deferred tax + other non-cash - capex + WC change. Capex is gross,
    /// so depreciation is added back with the rest of D&amp;A (it does not cancel).
    /// Null when the year has no net income, or when it has D&amp;A but no capex tag: its capex
    /// is unknown, not zero, so the year leaves every owner-earnings average. A year with
    /// neither capex nor D&amp;A keeps capex 0.
    /// </summary>
    /// <summary>
    /// Ids of financial companies (SIC 6000-6499), whose net cash flow uses the financing total
    /// (E5). Today's SIC applies to as-of scores too (ADR 0014).
    /// </summary>
    internal static async Task<Result<HashSet<ulong>>> LoadFinancialCompanyIds(IDbmService dbm, CancellationToken ct) {
        Result<IReadOnlyCollection<CompanySic>> sicsResult = await dbm.GetCompanySics(ct);
        if (sicsResult.IsFailure || sicsResult.Value is null)
            return Result<HashSet<ulong>>.Failure(sicsResult);
        var ids = new HashSet<ulong>();
        foreach (CompanySic sic in sicsResult.Value) {
            if (IndustryClassification.IsFinancial(sic.SicCode))
                ids.Add(sic.CompanyId);
        }
        return Result<HashSet<ulong>>.Success(ids);
    }

    /// <summary>One company's net cash flow basis from its SIC code (no code = chains).</summary>
    internal static async Task<Result<NetCashFlowBasis>> LoadNetCashFlowBasis(
        IDbmService dbm, ulong companyId, CancellationToken ct) {
        Result<CompanySic?> sicResult = await dbm.GetCompanySic(companyId, ct);
        if (sicResult.IsFailure)
            return Result<NetCashFlowBasis>.Failure(sicResult);
        return Result<NetCashFlowBasis>.Success(
            IndustryClassification.NetCashFlowBasisFor(sicResult.Value?.SicCode));
    }

    /// <summary>
    /// Sum-over-sum ROE (E6): the ratio in percent (null when no year enters or the sum of
    /// average equity is 0), the sign-rule flag, and each entering year's own percentage.
    /// </summary>
    internal sealed record SumOverSumRoe(
        decimal? Value, bool HasNonPositiveEquityYear, IReadOnlyDictionary<int, decimal> YearlyPct);

    /// <summary>
    /// ROE numerator for the cash-flow variant: net cash flow plus dividends paid (NCF starts
    /// from the change in cash, so it is after dividends). Null when the year has no cash change.
    /// </summary>
    internal static decimal? ComputeYearRoeCfNumerator(IReadOnlyDictionary<string, decimal> yearData,
        NetCashFlowBasis basis = NetCashFlowBasis.Chains) {
        decimal? grossCashFlow = ResolveField(yearData, CashChangeChain, null);
        if (!grossCashFlow.HasValue)
            return null;
        return ComputeYearNetCashFlow(yearData, grossCashFlow.Value, basis)
            + ResolveField(yearData, DividendsChain, 0m)!.Value;
    }

    /// <summary>
    /// Average equity for one annual year: (opening + closing) / 2, where opening is the equity
    /// of annual key year - 1. Without a resolvable prior year, the closing equity alone.
    /// Null when the year's own equity can't be resolved.
    /// </summary>
    internal static decimal? ResolveAverageEquity(
        IReadOnlyDictionary<int, IReadOnlyDictionary<string, decimal>> annualDataByYear, int year) {
        if (!annualDataByYear.TryGetValue(year, out IReadOnlyDictionary<string, decimal>? yearData))
            return null;
        decimal? closing = ResolveEquity(yearData);
        if (!closing.HasValue)
            return null;
        if (annualDataByYear.TryGetValue(year - 1, out IReadOnlyDictionary<string, decimal>? priorData)) {
            decimal? opening = ResolveEquity(priorData);
            if (opening.HasValue)
                return (opening.Value + closing.Value) / 2m;
        }
        return closing.Value;
    }

    /// <summary>
    /// ROE = 100 * sum of numerators / sum of average equity, over the annual years where both
    /// resolve (E6). The ratio is null when no year enters or the sum is 0. Sign rule: the
    /// flag is set when the sum is &lt;= 0 or any entering year's average equity is &lt;= 0, and
    /// the check then fails; the ratio itself is still shown. Per-year values (numerator over
    /// that year's average equity) feed the moat trend chart.
    /// </summary>
    internal static SumOverSumRoe ComputeSumOverSumRoe(
        IReadOnlyDictionary<int, IReadOnlyDictionary<string, decimal>> annualDataByYear,
        Func<IReadOnlyDictionary<string, decimal>, decimal?> numerator) {
        var sortedYears = new List<int>(annualDataByYear.Keys);
        sortedYears.Sort();

        decimal totalNumerator = 0m;
        decimal totalAverageEquity = 0m;
        bool anyYear = false;
        bool hasNonPositiveEquityYear = false;
        var yearlyPct = new Dictionary<int, decimal>();

        foreach (int year in sortedYears) {
            decimal? yearNumerator = numerator(annualDataByYear[year]);
            if (!yearNumerator.HasValue)
                continue;
            decimal? averageEquity = ResolveAverageEquity(annualDataByYear, year);
            if (!averageEquity.HasValue)
                continue;

            anyYear = true;
            totalNumerator += yearNumerator.Value;
            totalAverageEquity += averageEquity.Value;
            if (averageEquity.Value <= 0m)
                hasNonPositiveEquityYear = true;
            if (averageEquity.Value != 0m)
                yearlyPct[year] = 100m * yearNumerator.Value / averageEquity.Value;
        }

        if (!anyYear)
            return new SumOverSumRoe(null, false, yearlyPct);
        if (totalAverageEquity <= 0m)
            hasNonPositiveEquityYear = true;
        decimal? roe = totalAverageEquity != 0m ? 100m * totalNumerator / totalAverageEquity : null;
        return new SumOverSumRoe(roe, hasNonPositiveEquityYear, yearlyPct);
    }

    internal static decimal? ComputeYearOwnerEarnings(
        IReadOnlyDictionary<string, decimal> yearData,
        IReadOnlyDictionary<string, TaxonomyBalanceTypes> balanceTypes) {
        decimal? netIncome = ResolveField(yearData, NetIncomeChain, null);
        if (!netIncome.HasValue)
            return null;

        decimal? capEx = ResolveField(yearData, CapExChain, null);
        if (!capEx.HasValue && ResolveTotalDa(yearData) > 0m)
            return null;

        decimal totalDa = ResolveTotalDa(yearData);
        decimal deferredTax = ResolveDeferredTax(yearData);
        decimal otherNonCash = ResolveOtherNonCash(yearData);
        decimal workingCapitalChange = ResolveWorkingCapitalChange(yearData, balanceTypes);

        return netIncome.Value
            + totalDa + deferredTax + otherNonCash
            - (capEx ?? 0m) + workingCapitalChange;
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
    /// 1. If OtherNoncashIncomeExpense is found, use its negation: it is credit (income
    ///    positive) with weight -1 under AdjustmentsNoncashItems in the indirect cash flow.
    /// 2. Otherwise, sum OtherNoncashExpense - OtherNoncashIncome (0 for missing, but at least one must exist).
    /// 3. If nothing is found, return 0.
    /// </summary>
    internal static decimal ResolveOtherNonCash(IReadOnlyDictionary<string, decimal> yearData) {
        decimal? aggregate = ResolveField(yearData, OtherNonCashChain, null);
        if (aggregate.HasValue)
            return -aggregate.Value;

        decimal? expense = ResolveField(yearData, OtherNonCashExpenseChain, null);
        decimal? income = ResolveField(yearData, OtherNonCashIncomeChain, null);

        if (expense.HasValue || income.HasValue)
            return (expense ?? 0m) - (income ?? 0m);

        return 0m;
    }

    /// <summary>
    /// Resolve working capital change (as its cash-flow impact) with component sum fallback.
    /// 1. If IncreaseDecreaseInOperatingCapital exists, use its negation (credit; taxonomy
    ///    weight -1 in the indirect cash-flow statement: an increase in operating capital uses cash).
    /// 2. Otherwise, sum individual components with overlap-aware grouping:
    ///    - Receivables: prefer combined AccountsAndOtherReceivables, else AR + OtherReceivables
    ///    - Inventories
    ///    - Payables: prefer combined AP+AccruedLiabilities, else AP + AccruedLiabilities
    ///    - Prepaid/deferred assets
    ///    - Revenue: prefer DeferredRevenue, else ContractWithCustomerLiability
    ///    - Other operating assets/liabilities
    ///    - Other operating capital, net (IncreaseDecreaseInOtherOperatingCapitalNet)
    ///
    /// Sign convention: the balance type of an IncreaseDecrease concept records its
    /// cash-flow direction, not the balance-sheet side. Credit (asset increase, liability
    /// decrease) reduces operating cash, so it is negated; debit adds to it.
    /// </summary>
    internal static decimal ResolveWorkingCapitalChange(
        IReadOnlyDictionary<string, decimal> yearData,
        IReadOnlyDictionary<string, TaxonomyBalanceTypes> balanceTypes) {
        decimal? aggregate = ResolveField(yearData, WorkingCapitalChangeChain, null);
        if (aggregate.HasValue)
            return -aggregate.Value;

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
            // The combined AP + accrued total, else its children: AP, accrued, and "other AP and
            // accrued" (a sibling of the two, not an alternative total).
            if (!AccumulateWcField(yearData, balanceTypes, [
                    "IncreaseDecreaseInAccountsPayableAndAccruedLiabilities"], ref sum, ref foundAny)) {
                AccumulateWcField(yearData, balanceTypes, ["IncreaseDecreaseInAccountsPayable", "IncreaseDecreaseInAccountsPayableTrade"], ref sum, ref foundAny);
                AccumulateWcField(yearData, balanceTypes, ["IncreaseDecreaseInAccruedLiabilities", "IncreaseDecreaseInOtherAccruedLiabilities"], ref sum, ref foundAny);
                AccumulateWcField(yearData, balanceTypes, ["IncreaseDecreaseInOtherAccountsPayableAndAccruedLiabilities"], ref sum, ref foundAny);
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

        // Other operating capital, net (credit): an "other" line beside the components above
        AccumulateWcField(yearData, balanceTypes, ["IncreaseDecreaseInOtherOperatingCapitalNet"], ref sum, ref foundAny);

        return foundAny ? sum : 0m;
    }

    /// <summary>
    /// Resolve a WC field from the fallback chain and accumulate into sum with sign correction.
    /// Credit concepts (asset increase, liability decrease: uses cash) are negated; debit
    /// concepts (provide cash) are kept.
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
                // The balance type is the concept's cash-flow direction, not its balance-sheet
                // side. Credit: a positive value reduces operating cash, so negate. Debit: keep.
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
    /// Net cash flow for one year: the change in cash minus net debt, stock and preferred
    /// issuance. Shared by the value score and the moat ROE (CF).
    /// </summary>
    internal static decimal ComputeYearNetCashFlow(IReadOnlyDictionary<string, decimal> yearData, decimal grossCashFlow,
        NetCashFlowBasis basis = NetCashFlowBasis.Chains) {
        // Financials (E5): the financing total also holds deposit and other funding flows that
        // the chains miss. It includes dividends paid as an outflow; adding them back keeps NCF
        // after dividends, as on the chain basis. Without the total, the chains apply.
        if (basis == NetCashFlowBasis.FinancingTotal) {
            decimal? financingTotal = ResolveField(yearData, FinancingTotalChain, null);
            if (financingTotal.HasValue)
                return grossCashFlow - (financingTotal.Value + ResolveField(yearData, DividendsChain, 0m)!.Value);
        }

        decimal netDebtIssuance = ResolveDebtProceeds(yearData) - ResolveDebtRepayments(yearData)
            + ResolveNetOnlyDebt(yearData) + ResolveNetShortTermDebt(yearData);

        decimal stockProceeds = ResolveStockProceeds(yearData);
        decimal stockRepurchase = ResolveField(yearData, StockRepurchaseChain, 0m)!.Value;
        decimal netStockIssuance = stockProceeds - stockRepurchase;

        decimal preferredProceeds = ResolveField(yearData, PreferredProceedsChain, 0m)!.Value;
        decimal preferredRepurchase = ResolveField(yearData, PreferredRepurchaseChain, 0m)!.Value;
        decimal netPreferredIssuance = preferredProceeds - preferredRepurchase;

        return grossCashFlow - (netDebtIssuance + netStockIssuance + netPreferredIssuance);
    }

    // Relative tolerance for "a parent covers its children" (rounding in the filings)
    private const decimal ParentCoverTolerance = 0.001m;

    /// <summary>
    /// Long-term debt proceeds nested per the taxonomy: the instrument lines under long-term,
    /// long-term under the all-debt total. A parent that is at least the sum of its present
    /// children (within 0.1%) contains them; a smaller parent is a separate line and both count.
    /// </summary>
    internal static decimal ResolveDebtProceeds(IReadOnlyDictionary<string, decimal> yearData) =>
        ResolveNestedDebt(yearData, "ProceedsFromIssuanceOfDebt", "ProceedsFromIssuanceOfLongTermDebt",
            LongTermDebtProceedsLines);

    /// <summary>Long-term debt repayments, the same shape as proceeds.</summary>
    internal static decimal ResolveDebtRepayments(IReadOnlyDictionary<string, decimal> yearData) =>
        ResolveNestedDebt(yearData, "RepaymentsOfDebt", "RepaymentsOfLongTermDebt", LongTermDebtRepaymentLines);

    private static decimal ResolveNestedDebt(IReadOnlyDictionary<string, decimal> yearData,
        string allDebt, string longTermDebt, string[] lines) {
        decimal? longTerm = NestParent(ValueOrNull(yearData, longTermDebt), SumDistinctLines(yearData, lines));
        return NestParent(ValueOrNull(yearData, allDebt), longTerm) ?? 0m;
    }

    /// <summary>Net debt lines reported without either gross side.</summary>
    internal static decimal ResolveNetOnlyDebt(IReadOnlyDictionary<string, decimal> yearData) {
        decimal total = 0m;
        foreach (NetDebtLine line in NetOnlyDebtLines) {
            if (yearData.ContainsKey(line.Proceeds) || yearData.ContainsKey(line.Repayments))
                continue;
            total += ValueOrNull(yearData, line.Net) ?? 0m;
        }
        return total;
    }

    /// <summary>
    /// Net short-term borrowing: the net short-term debt figure if present; else the short-term
    /// lines, gross (proceeds and repayments each nested under their total), with net lines of
    /// credit only when neither gross lines-of-credit tag is present. Plus net commercial paper.
    /// </summary>
    internal static decimal ResolveNetShortTermDebt(IReadOnlyDictionary<string, decimal> yearData) {
        decimal commercialPaper = ValueOrNull(yearData, "ProceedsFromRepaymentsOfCommercialPaper") ?? 0m;
        if (yearData.TryGetValue("ProceedsFromRepaymentsOfShortTermDebt", out decimal netShortTerm))
            return netShortTerm + commercialPaper;

        decimal proceeds = NestParent(ValueOrNull(yearData, "ProceedsFromShortTermDebt"),
            SumDistinctLines(yearData, ShortTermDebtProceedsLines)) ?? 0m;
        decimal repayments = NestParent(ValueOrNull(yearData, "RepaymentsOfShortTermDebt"),
            SumDistinctLines(yearData, ShortTermDebtRepaymentLines)) ?? 0m;

        decimal netLines = 0m;
        if (!yearData.ContainsKey("ProceedsFromLinesOfCredit") && !yearData.ContainsKey("RepaymentsOfLinesOfCredit"))
            netLines = ValueOrNull(yearData, "ProceedsFromRepaymentsOfLinesOfCredit") ?? 0m;

        return proceeds - repayments + netLines + commercialPaper;
    }

    /// <summary>
    /// Stock proceeds: the share issuance lines plus the employee-plan total (which contains
    /// option exercises), under the all-equity total. Preferred stock is part of that total,
    /// so it counts for the nesting test, but it is taken out again: it has its own chain.
    /// </summary>
    internal static decimal ResolveStockProceeds(IReadOnlyDictionary<string, decimal> yearData) {
        decimal? plans = NestParent(
            ValueOrNull(yearData, "ProceedsFromIssuanceOfSharesUnderIncentiveAndShareBasedCompensationPlansIncludingStockOptions"),
            ValueOrNull(yearData, "ProceedsFromStockOptionsExercised"));
        decimal? lines = SumDistinct(SumDistinctLines(yearData, ShareIssuanceLines), plans);

        decimal? preferred = ResolveField(yearData, PreferredProceedsChain, null);
        decimal? linesWithPreferred = preferred.HasValue ? (lines ?? 0m) + preferred.Value : lines;
        decimal? total = NestParent(ValueOrNull(yearData, "ProceedsFromIssuanceOrSaleOfEquity"), linesWithPreferred);
        return (total ?? 0m) - (preferred ?? 0m);
    }

    private static decimal? ValueOrNull(IReadOnlyDictionary<string, decimal> yearData, string concept) =>
        yearData.TryGetValue(concept, out decimal value) ? value : null;

    // Sum of the sibling lines present (null when none is); equal values are one line tagged
    // more than once and count once.
    private static decimal? SumDistinctLines(IReadOnlyDictionary<string, decimal> yearData, string[] lines) {
        decimal? sum = null;
        var seen = new HashSet<decimal>();
        foreach (string line in lines) {
            if (!yearData.TryGetValue(line, out decimal value) || !seen.Add(value))
                continue;
            sum = (sum ?? 0m) + value;
        }
        return sum;
    }

    // Sum of two sibling lines; equal values are a duplicate tag and count once.
    private static decimal? SumDistinct(decimal? first, decimal? second) {
        if (!first.HasValue) return second;
        if (!second.HasValue) return first;
        return first.Value == second.Value ? first.Value : first.Value + second.Value;
    }

    // A parent at least the size of its children (within tolerance) contains them;
    // a smaller parent is a separate line, so both count.
    private static decimal? NestParent(decimal? parent, decimal? children) {
        if (!parent.HasValue) return children;
        if (!children.HasValue) return parent;
        decimal threshold = children.Value - Math.Abs(children.Value) * ParentCoverTolerance;
        return parent.Value >= threshold ? parent.Value : parent.Value + children.Value;
    }

    /// <summary>
    /// Computes the maximum buy price:
    /// min(3 × bookValue, 20 × (avgNCF + dividends), 20 × avgOE) / shares.
    /// Net cash flow is after dividends paid, so they are added back; owner earnings are
    /// before dividends. (The tsx formula subtracted dividends from both: counted twice.)
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
        decimal val2 = 20m * (averageNetCashFlow.Value + currentDividendsPaid.Value);
        decimal val3 = 20m * averageOwnerEarnings.Value;

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
        IReadOnlyDictionary<string, TaxonomyBalanceTypes>? balanceTypes = null,
        NetCashFlowBasis ncfBasis = NetCashFlowBasis.Chains) {

        if (annualDataByYear.Count == 0)
            return new DerivedMetrics(null, null, null, null, null, null, null, null, null, null, null, null, null, null);

        // Balance sheet values from most recent snapshot (may be quarterly)
        decimal? equity = ResolveEquity(mostRecentSnapshot);
        decimal? goodwill = ResolveField(mostRecentSnapshot, GoodwillChain, 0m);
        decimal? intangibles = ResolveField(mostRecentSnapshot, IntangiblesChain, 0m);
        decimal? debt = ResolveTotalDebt(mostRecentSnapshot);
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
                decimal netCashFlow = ComputeYearNetCashFlow(yearData, grossCashFlow.Value, ncfBasis);
                totalNetCashFlow += netCashFlow;
                yearsWithNCF++;
            }

            // Owner Earnings for this year (null: no net income, or capex unknown)
            decimal? yearOwnerEarnings = ComputeYearOwnerEarnings(yearData, balanceTypes ?? EmptyBalanceTypes);
            if (yearOwnerEarnings.HasValue) {
                hasAnyOwnerEarnings = true;
                decimal ownerEarnings = yearOwnerEarnings.Value;
                totalOwnerEarnings += ownerEarnings;
                yearsWithOE++;
            }

            // Accumulate totals for Adjusted Retained Earnings. The oldest retained earnings
            // (check 13's baseline) is the oldest annual balance, which already contains the
            // oldest year's flows, so only later years count (E11).
            if (year == sortedYears[0])
                continue;
            decimal dividends = ResolveField(yearData, DividendsChain, 0m)!.Value;
            totalDividends += dividends;

            decimal stockProc = ResolveStockProceeds(yearData);
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

        // ROE: sum of numerators over sum of average equity (E6)
        IReadOnlyDictionary<string, TaxonomyBalanceTypes> types = balanceTypes ?? EmptyBalanceTypes;
        SumOverSumRoe roeCf = ComputeSumOverSumRoe(annualDataByYear, yearData => ComputeYearRoeCfNumerator(yearData, ncfBasis));
        SumOverSumRoe roeOe = ComputeSumOverSumRoe(annualDataByYear, yearData => ComputeYearOwnerEarnings(yearData, types));
        decimal? averageRoeCF = roeCf.Value;
        decimal? averageRoeOE = roeOe.Value;

        // Adjusted Retained Earnings (current RE from snapshot, totals from annual)
        decimal? adjustedRetainedEarnings = null;
        if (retainedEarnings.HasValue)
            adjustedRetainedEarnings = retainedEarnings.Value + totalDividends
                - totalStockIssuance - totalPreferredIssuance;

        // Current dividends from most recent annual year (duration concept, not balance sheet)
        int mostRecentAnnualYear = sortedYears[sortedYears.Count - 1];
        decimal? currentDividendsPaid = ResolveField(annualDataByYear[mostRecentAnnualYear], DividendsChain, 0m);

        // Estimated Returns. NCF is after dividends paid (it starts from the change in cash), so
        // they are added back; owner earnings are before dividends and need no adjustment.
        decimal? estimatedReturnCF = null;
        if (averageNetCashFlow.HasValue && currentDividendsPaid.HasValue && marketCap.HasValue && marketCap.Value != 0m)
            estimatedReturnCF = 100m * (averageNetCashFlow.Value + currentDividendsPaid.Value) / marketCap.Value;

        decimal? estimatedReturnOE = null;
        if (averageOwnerEarnings.HasValue && marketCap.HasValue && marketCap.Value != 0m)
            estimatedReturnOE = 100m * averageOwnerEarnings.Value / marketCap.Value;

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
            averageRoeCF,
            averageRoeOE,
            estimatedReturnCF,
            estimatedReturnOE,
            currentDividendsPaid,
            equity,
            roeCf.HasNonPositiveEquityYear,
            roeOe.HasNonPositiveEquityYear);
    }

    internal static IReadOnlyList<ScoringCheck> EvaluateChecks(DerivedMetrics metrics, int yearsOfData) {
        var checks = new List<ScoringCheck>(15);

        // Sign rule: a ratio over a non-positive denominator fails its check. The rule lives
        // in the check, not the division, so the displayed ratio stays honest.
        bool equityNonPositive = metrics.Equity.HasValue && metrics.Equity.Value <= 0m;
        bool bookValueNonPositive = metrics.BookValue.HasValue && metrics.BookValue.Value <= 0m;

        // Check 1: Debt-to-Equity < 0.5
        checks.Add(MakeCheck(1, "Debt-to-Equity", metrics.DebtToEquityRatio, "< 0.5",
            equityNonPositive
                ? ScoringCheckResult.Fail
                : metrics.DebtToEquityRatio.HasValue
                        ? (metrics.DebtToEquityRatio.Value < 0.5m ? ScoringCheckResult.Pass : ScoringCheckResult.Fail)
                        : ScoringCheckResult.NotAvailable));

        // Check 2: Book Value > $150M
        checks.Add(MakeCheck(2, "Book Value", metrics.BookValue, "> $150M",
            metrics.BookValue.HasValue
                ? (metrics.BookValue.Value > 150_000_000m ? ScoringCheckResult.Pass : ScoringCheckResult.Fail)
                : ScoringCheckResult.NotAvailable));

        // Check 3: Price-to-Book ≤ 3.0
        checks.Add(MakeCheck(3, "Price-to-Book", metrics.PriceToBookRatio, "≤ 3.0",
            bookValueNonPositive && metrics.MarketCap.HasValue
                ? ScoringCheckResult.Fail
                : metrics.PriceToBookRatio.HasValue
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
            bookValueNonPositive
                ? ScoringCheckResult.Fail
                : metrics.DebtToBookRatio.HasValue
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

        // Check 14: Average ROE (CF) >= 10%
        // Sum of (NCF + dividends) over sum of average equity. A sum <= 0, or a year whose
        // average equity is <= 0, fails the check.
        checks.Add(MakeCheck(14, "Avg ROE (CF)", metrics.AverageRoeCF, ">= 10%",
            metrics.RoeCfHasNonPositiveEquityYear
                ? ScoringCheckResult.Fail
                : metrics.AverageRoeCF.HasValue
                    ? (metrics.AverageRoeCF.Value >= 10m ? ScoringCheckResult.Pass : ScoringCheckResult.Fail)
                    : ScoringCheckResult.NotAvailable));

        // Check 15: Average ROE (OE) >= 10%: sum of owner earnings over sum of average equity
        checks.Add(MakeCheck(15, "Avg ROE (OE)", metrics.AverageRoeOE, ">= 10%",
            metrics.RoeOeHasNonPositiveEquityYear
                ? ScoringCheckResult.Fail
                : metrics.AverageRoeOE.HasValue
                    ? (metrics.AverageRoeOE.Value >= 10m ? ScoringCheckResult.Pass : ScoringCheckResult.Fail)
                    : ScoringCheckResult.NotAvailable));

        return checks;
    }

    internal static ScoringCheck MakeCheck(int number, string name, decimal? value, string threshold, ScoringCheckResult result) {
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
        DateOnly latestAnnualDate = DateOnly.MinValue;
        DateOnly oldestDate = DateOnly.MaxValue;

        Dictionary<DateOnly, int> annualYearKeys = BuildAnnualYearKeys(values);

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
                if (v.ReportDate > latestAnnualDate)
                    latestAnnualDate = v.ReportDate;
                int year = annualYearKeys[v.ReportDate];
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

        // A newer 10-Q often carries fewer balance-sheet tags than the 10-K. Fill its gaps
        // from the latest 10-K (at most about a year older), group by group.
        if (latestAnnualDate != DateOnly.MinValue && latestAnnualDate < mostRecentDate) {
            var latestTenKSnapshot = new Dictionary<string, decimal>(StringComparer.Ordinal);
            foreach (ScoringConceptValue v in values) {
                if (v.ReportDate == latestAnnualDate && v.FilingTypeId == (int)FilingType.TenK
                    && !latestTenKSnapshot.ContainsKey(v.ConceptName))
                    latestTenKSnapshot[v.ConceptName] = v.Value;
            }
            FillBalanceSheetGapsFromTenK(mostRecentSnapshot, latestTenKSnapshot);
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

    /// <summary>
    /// Gives each distinct 10-K report date its own annual year key. The newest date keys to
    /// its calendar year; each older date to min(its calendar year, the next newer key − 1).
    /// So two annual reports in one calendar year (a fiscal-year change, or 52/53-week years
    /// ending in early January) are both kept, and a run of early-January year ends shifts
    /// down in a cascade. A real gap year is kept. The DEI fiscal year would be better but is
    /// not in the database (plan E7).
    /// </summary>
    internal static Dictionary<DateOnly, int> BuildAnnualYearKeys(IReadOnlyCollection<ScoringConceptValue> values) {
        var dates = new List<DateOnly>();
        var seen = new HashSet<DateOnly>();
        foreach (ScoringConceptValue v in values) {
            if (v.FilingTypeId == (int)FilingType.TenK && seen.Add(v.ReportDate))
                dates.Add(v.ReportDate);
        }
        dates.Sort((a, b) => b.CompareTo(a));

        var keys = new Dictionary<DateOnly, int>(dates.Count);
        int? newerKey = null;
        foreach (DateOnly date in dates) {
            int key = newerKey.HasValue ? Math.Min(date.Year, newerKey.Value - 1) : date.Year;
            keys[date] = key;
            newerKey = key;
        }
        return keys;
    }

    /// <summary>
    /// Fills balance-sheet fields missing from the most recent snapshot with the latest
    /// 10-K's values. Equity: when equity can't be resolved from the snapshot, the whole
    /// equity group is replaced by the 10-K's (never mixed). Debt: the long-term chain and
    /// the other debt concepts are each copied when none of that subgroup is present. Goodwill, intangibles, retained earnings and current
    /// assets/liabilities: individually. Duration concepts are never filled. Shares are
    /// not filled: a share count up to a year old would give a stale market cap.
    /// </summary>
    internal static void FillBalanceSheetGapsFromTenK(
        Dictionary<string, decimal> snapshot,
        IReadOnlyDictionary<string, decimal> latestTenK) {
        if (ResolveEquity(snapshot) is null && ResolveEquity(latestTenK) is not null) {
            foreach (string concept in EquityGroupConcepts)
                snapshot.Remove(concept);
            CopyConcepts(snapshot, latestTenK, EquityGroupConcepts);
        }

        // Long-term debt and the other debt concepts are separate subgroups: a 10-Q that
        // tags only a credit line must still get the 10-K's long-term debt.
        if (!ContainsAny(snapshot, DebtChain))
            CopyConcepts(snapshot, latestTenK, DebtChain);
        if (!ContainsAny(snapshot, OtherDebtConcepts))
            CopyConcepts(snapshot, latestTenK, OtherDebtConcepts);

        foreach (string concept in IndividuallyFilledBalanceSheetConcepts) {
            if (!snapshot.ContainsKey(concept) && latestTenK.TryGetValue(concept, out decimal value))
                snapshot[concept] = value;
        }
    }

    private static string[] Concat(string[] first, string[] second) {
        var result = new string[first.Length + second.Length];
        first.CopyTo(result, 0);
        second.CopyTo(result, first.Length);
        return result;
    }

    private static bool ContainsAny(IReadOnlyDictionary<string, decimal> data, string[] concepts) {
        foreach (string concept in concepts) {
            if (data.ContainsKey(concept))
                return true;
        }
        return false;
    }

    private static void CopyConcepts(
        Dictionary<string, decimal> target, IReadOnlyDictionary<string, decimal> source, string[] concepts) {
        foreach (string concept in concepts) {
            if (source.TryGetValue(concept, out decimal value))
                target[concept] = value;
        }
    }
}
