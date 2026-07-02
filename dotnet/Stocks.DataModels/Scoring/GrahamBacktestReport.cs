using System;
using System.Collections.Generic;

namespace Stocks.DataModels.Scoring;

/// <summary>
/// Which snapshot-date grid the backtest chains over: month-end dates or Friday dates.
/// Both grids can coexist in graham_score_snapshots.
/// </summary>
public enum GrahamBacktestInterval {
    Monthly = 0,
    Weekly = 1,
}

/// <summary>
/// One holding during one backtest period. Entered means the company was not in the
/// previous period's portfolio; Left means it is not in the next period's portfolio.
/// </summary>
public record GrahamBacktestConstituent(
    ulong CompanyId,
    string Cik,
    string? CompanyName,
    string? Ticker,
    string? Exchange,
    decimal? StartPrice,
    DateOnly? StartPriceDate,
    decimal? EndPrice,
    DateOnly? EndPriceDate,
    decimal? PeriodReturnPct,
    bool Entered,
    bool Left);

/// <summary>
/// One rebalance period: buy the qualifying list at StartDate, hold until EndDate.
/// CumulativeValue is the running value of $1000 invested at the first period start.
/// </summary>
public record GrahamBacktestPeriod(
    DateOnly StartDate,
    DateOnly EndDate,
    int ConstituentCount,
    decimal PortfolioReturnPct,
    decimal CumulativeValue,
    decimal? BenchmarkReturnPct,
    decimal? BenchmarkCumulativeValue,
    IReadOnlyList<GrahamBacktestConstituent> Constituents);

public record GrahamBacktestSummary(
    DateOnly FirstDate,
    DateOnly LastDate,
    int PeriodCount,
    decimal AverageConstituents,
    decimal TotalReturnPct,
    decimal? AnnualizedReturnPct,
    decimal FinalValue,
    decimal? BenchmarkTotalReturnPct,
    decimal? BenchmarkAnnualizedReturnPct,
    decimal? BenchmarkFinalValue,
    string BenchmarkTicker,
    int MinScore);

public record GrahamBacktestReport(
    GrahamBacktestSummary Summary,
    IReadOnlyList<GrahamBacktestPeriod> Periods);
