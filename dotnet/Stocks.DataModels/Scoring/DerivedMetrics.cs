namespace Stocks.DataModels.Scoring;

public record DerivedMetrics(
    decimal? BookValue,
    decimal? MarketCap,
    decimal? DebtToEquityRatio,
    decimal? PriceToBookRatio,
    decimal? DebtToBookRatio,
    decimal? AdjustedRetainedEarnings,
    decimal? OldestRetainedEarnings,
    decimal? AverageNetCashFlow,
    decimal? AverageOwnerEarnings,
    decimal? AverageRoeCF,
    decimal? AverageRoeOE,
    decimal? EstimatedReturnCF,
    decimal? EstimatedReturnOE,
    decimal? CurrentDividendsPaid,
    // Latest balance-sheet equity (the D/E denominator). Null when not derivable.
    decimal? Equity = null,
    // True when any year entering the ROE (CF) / ROE (OE) average has equity <= 0.
    // Such a year gives no credit: the ROE check fails (ADR 0013 sign rule).
    bool RoeCfHasNonPositiveEquityYear = false,
    bool RoeOeHasNonPositiveEquityYear = false);
