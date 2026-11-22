using System;

namespace Stocks.DataModels.Scoring;

/// <summary>
/// A company's stored value score row. <see cref="CheckResults"/> holds the 15 check
/// results encoded by <see cref="ScoringCheckResults"/>; null for rows written before
/// they were stored.
/// </summary>
public record CompanyScoreSummary(
    ulong CompanyId,
    string Cik,
    string? CompanyName,
    string? Ticker,
    string? Exchange,
    int OverallScore,
    int ComputableChecks,
    int YearsOfData,
    decimal? BookValue,
    decimal? MarketCap,
    decimal? DebtToEquityRatio,
    decimal? PriceToBookRatio,
    decimal? DebtToBookRatio,
    decimal? AdjustedRetainedEarnings,
    decimal? AverageNetCashFlow,
    decimal? AverageOwnerEarnings,
    decimal? AverageRoeCF,
    decimal? AverageRoeOE,
    decimal? EstimatedReturnCF,
    decimal? EstimatedReturnOE,
    decimal? PricePerShare,
    DateOnly? PriceDate,
    long? SharesOutstanding,
    decimal? CurrentDividendsPaid,
    decimal? MaxBuyPrice,
    decimal? PercentageUpside,
    DateTime ComputedAt,
    string? CheckResults = null);
