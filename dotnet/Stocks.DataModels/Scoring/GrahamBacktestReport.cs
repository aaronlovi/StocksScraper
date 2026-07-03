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
/// Which membership changes the simulated portfolio acts on. All = act on every change
/// (portfolio always equals the qualifying list). FilingOnly = trade a company only when
/// its fundamentals changed between adjacent snapshots (a new filing); pure price
/// flicker is ignored until the next filing confirms it. PriceOnly = the mirror image:
/// act only on changes with unchanged fundamentals.
/// </summary>
public enum GrahamBacktestPolicy {
    All = 0,
    FilingOnly = 1,
    PriceOnly = 2,
}

/// <summary>
/// One holding during one backtest period. Entered means the company was not in the
/// previous period's portfolio; Left means it is not in the next period's portfolio.
/// EnteredTrigger/LeftTrigger say what caused the change: "filing" (fundamentals inputs
/// changed between the adjacent snapshots) or "price" (identical fundamentals, only the
/// price crossed a threshold). Null when the corresponding flag is false.
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
    bool Left,
    string? EnteredTrigger,
    string? LeftTrigger);

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
