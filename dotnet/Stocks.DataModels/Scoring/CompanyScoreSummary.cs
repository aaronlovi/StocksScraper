using System;

namespace Stocks.DataModels.Scoring;

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
    decimal? EstimatedReturnCF,
    decimal? EstimatedReturnOE,
    decimal? PricePerShare,
    DateOnly? PriceDate,
    long? SharesOutstanding,
    decimal? CurrentDividendsPaid,
    decimal? MaxBuyPrice,
    decimal? PercentageUpside,
    DateTime ComputedAt);
