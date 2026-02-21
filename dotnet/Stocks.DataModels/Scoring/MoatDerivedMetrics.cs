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
    int TotalCapitalReturnYears);
