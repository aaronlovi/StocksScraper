namespace Stocks.DataModels.Scoring;

public record MoatDerivedMetrics(
    decimal? AverageGrossMargin,
    decimal? AverageOperatingMargin,
    decimal? AverageRoeCF,
    decimal? AverageRoeOE,
    decimal? RevenueCagr,
    decimal? CapexRatio,
    decimal? InterestCoverage,
    decimal? DebtToEquityRatio,
    decimal? EstimatedReturnOE,
    decimal? CurrentDividendsPaid,
    decimal? MarketCap,
    decimal? PricePerShare,
    int PositiveOeYears,
    int TotalOeYears,
    int CapitalReturnYears,
    int TotalCapitalReturnYears,
    // Latest balance-sheet equity (the D/E denominator). Null when not derivable.
    decimal? Equity = null,
    // Average owner earnings (the capex ratio denominator). Null when no year has net income.
    decimal? AverageOwnerEarnings = null,
    // True when any year entering the ROE (CF) / ROE (OE) average has equity <= 0.
    bool RoeCfHasNonPositiveEquityYear = false,
    bool RoeOeHasNonPositiveEquityYear = false,
    // True when no debt concept (long-term chain or the other debt concepts) has a
    // non-zero value on the latest balance sheet.
    bool DebtFree = false,
    // Operating income of the latest year that reports it (the coverage numerator).
    decimal? LatestOperatingIncome = null);
