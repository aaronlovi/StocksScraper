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
    decimal? CurrentDividendsPaid);
