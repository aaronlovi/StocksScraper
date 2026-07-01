using System;

namespace Stocks.DataModels.Scoring;

/// <summary>
/// Lightweight row from graham_score_snapshots used by the backtest:
/// one qualifying company at one snapshot date.
/// </summary>
public record GrahamSnapshotConstituent(
    DateOnly AsOfDate,
    ulong CompanyId,
    string Cik,
    string? CompanyName,
    string? Ticker,
    string? Exchange,
    int OverallScore,
    int ComputableChecks,
    decimal? PricePerShare,
    DateOnly? PriceDate);
