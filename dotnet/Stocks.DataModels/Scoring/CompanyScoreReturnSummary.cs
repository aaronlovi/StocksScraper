using System;

namespace Stocks.DataModels.Scoring;

public record CompanyScoreReturnSummary(
    ulong CompanyId,
    string Cik,
    string? CompanyName,
    string? Ticker,
    string? Exchange,
    int OverallScore,
    int ComputableChecks,
    decimal? PricePerShare,
    decimal? TotalReturnPct,
    decimal? AnnualizedReturnPct,
    decimal? CurrentValueOf1000,
    DateOnly? StartDate,
    DateOnly? EndDate,
    decimal? StartPrice,
    decimal? EndPrice,
    DateTime ComputedAt);
