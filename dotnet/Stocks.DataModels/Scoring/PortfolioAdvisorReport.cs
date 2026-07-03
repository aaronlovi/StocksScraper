using System;
using System.Collections.Generic;

namespace Stocks.DataModels.Scoring;

/// <summary>
/// One buy/sell/hold recommendation for a ticker, comparing its current Graham score
/// against the most recent stored snapshot. Trigger says whether the change came from a
/// new filing (fundamentals differ from the baseline) or a pure price move.
/// </summary>
public record PortfolioRecommendation(
    string Ticker,
    ulong CompanyId,
    string Cik,
    string? CompanyName,
    string Action,              // "buy" | "sell" | "hold" | "unknown"
    int? OverallScore,
    int? ComputableChecks,
    decimal? PricePerShare,
    DateOnly? PriceDate,
    string? Trigger,            // "filing" | "price" | null
    IReadOnlyList<string> Reasons);

public record PortfolioAdvisorReport(
    DateTime? ScoresComputedAt,
    DateOnly? BaselineSnapshotDate,
    IReadOnlyList<PortfolioRecommendation> Sells,
    IReadOnlyList<PortfolioRecommendation> Buys,
    IReadOnlyList<PortfolioRecommendation> Holds,
    IReadOnlyList<PortfolioRecommendation> Unknowns);
