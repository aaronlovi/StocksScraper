using System;

namespace Stocks.DataModels.Scoring;

public record CompanyMoatScoreSummary(
    ulong CompanyId,
    string Cik,
    string? CompanyName,
    string? Ticker,
    string? Exchange,
    int OverallScore,
    int ComputableChecks,
    int YearsOfData,
    decimal? AverageGrossMargin,
    decimal? AverageOperatingMargin,
    decimal? AverageRoeCF,
    decimal? AverageRoeOE,
    decimal? EstimatedReturnOE,
    decimal? RevenueCagr,
    decimal? CapexRatio,
    decimal? InterestCoverage,
    decimal? DebtToEquityRatio,
    decimal? PricePerShare,
    DateOnly? PriceDate,
    long? SharesOutstanding,
    DateTime ComputedAt);
