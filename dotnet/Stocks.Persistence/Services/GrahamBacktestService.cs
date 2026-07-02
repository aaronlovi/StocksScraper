using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Stocks.DataModels.Scoring;
using Stocks.Persistence.Database;
using Stocks.Shared;
using Stocks.Shared.Models;

namespace Stocks.Persistence.Services;

/// <summary>
/// Simulates a monthly-rebalanced portfolio of the top-scoring Graham companies using the
/// stored point-in-time snapshots: at each snapshot date, buy the qualifying list
/// equal-weighted; hold until the next snapshot date (or today for the last one).
/// A holding with no newer price is carried at its last known price (0% for the period).
/// </summary>
public sealed class GrahamBacktestService {
    private const string BenchmarkTicker = "SPY";
    private const decimal StartingValue = 1000m;

    private readonly IDbmService _dbm;

    public GrahamBacktestService(IDbmService dbm) {
        _dbm = dbm;
    }

    public async Task<Result<GrahamBacktestReport>> GetBacktest(
        int minScore, GrahamBacktestInterval interval, CancellationToken ct) {
        Result<IReadOnlyCollection<DateOnly>> datesResult = await _dbm.GetGrahamScoreSnapshotDates(ct);
        if (datesResult.IsFailure || datesResult.Value is null)
            return Result<GrahamBacktestReport>.Failure(ErrorCodes.GenericError, datesResult.ErrorMessage);

        var dates = new List<DateOnly>();
        foreach (DateOnly d in datesResult.Value) {
            if (MatchesInterval(d, interval))
                dates.Add(d);
        }
        dates.Sort();
        if (dates.Count == 0)
            return Result<GrahamBacktestReport>.Failure(ErrorCodes.NotFound,
                $"No {(interval == GrahamBacktestInterval.Weekly ? "weekly (Friday)" : "monthly (month-end)")} score snapshots found. Run the --compute-score-snapshots backfill first.");

        Result<IReadOnlyCollection<GrahamSnapshotConstituent>> constituentsResult =
            await _dbm.GetGrahamSnapshotConstituents(minScore, ct);
        if (constituentsResult.IsFailure || constituentsResult.Value is null)
            return Result<GrahamBacktestReport>.Failure(ErrorCodes.GenericError, constituentsResult.ErrorMessage);

        var byDate = new Dictionary<DateOnly, List<GrahamSnapshotConstituent>>();
        foreach (GrahamSnapshotConstituent c in constituentsResult.Value) {
            if (!byDate.TryGetValue(c.AsOfDate, out List<GrahamSnapshotConstituent>? list)) {
                list = [];
                byDate[c.AsOfDate] = list;
            }
            list.Add(c);
        }

        var companyIdsByDate = new Dictionary<DateOnly, HashSet<ulong>>();
        foreach (KeyValuePair<DateOnly, List<GrahamSnapshotConstituent>> entry in byDate) {
            var ids = new HashSet<ulong>();
            foreach (GrahamSnapshotConstituent c in entry.Value)
                _ = ids.Add(c.CompanyId);
            companyIdsByDate[entry.Key] = ids;
        }

        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);

        decimal cumulative = StartingValue;
        decimal benchmarkCumulative = StartingValue;
        bool benchmarkComplete = true;
        int totalConstituents = 0;

        var periods = new List<GrahamBacktestPeriod>(dates.Count);

        for (int i = 0; i < dates.Count; i++) {
            DateOnly startDate = dates[i];
            DateOnly endDate = i + 1 < dates.Count ? dates[i + 1] : today;

            List<GrahamSnapshotConstituent> constituents =
                byDate.TryGetValue(startDate, out List<GrahamSnapshotConstituent>? list) ? list : [];

            var tickers = new List<string> { BenchmarkTicker };
            foreach (GrahamSnapshotConstituent c in constituents) {
                if (!string.IsNullOrWhiteSpace(c.Ticker))
                    tickers.Add(c.Ticker);
            }
            string[] tickerArray = [.. tickers];

            Task<Result<IReadOnlyCollection<LatestPrice>>> startPricesTask =
                _dbm.GetPricesNearDateForTickers(startDate, tickerArray, ct);
            Task<Result<IReadOnlyCollection<LatestPrice>>> endPricesTask =
                _dbm.GetPricesNearDateForTickers(endDate, tickerArray, ct);
            await Task.WhenAll(startPricesTask, endPricesTask);

            Dictionary<string, LatestPrice> startPrices = ToTickerMap(startPricesTask.Result);
            Dictionary<string, LatestPrice> endPrices = ToTickerMap(endPricesTask.Result);

            HashSet<ulong>? previousIds = i > 0 && companyIdsByDate.TryGetValue(dates[i - 1], out HashSet<ulong>? prev) ? prev : null;
            HashSet<ulong>? nextIds = i + 1 < dates.Count && companyIdsByDate.TryGetValue(dates[i + 1], out HashSet<ulong>? next) ? next : null;

            decimal returnSum = 0m;
            int returnCount = 0;
            var periodConstituents = new List<GrahamBacktestConstituent>(constituents.Count);

            foreach (GrahamSnapshotConstituent c in constituents) {
                LatestPrice? startPrice = null;
                LatestPrice? endPrice = null;
                if (c.Ticker is not null) {
                    _ = startPrices.TryGetValue(c.Ticker, out startPrice);
                    _ = endPrices.TryGetValue(c.Ticker, out endPrice);
                }

                decimal? periodReturnPct = null;
                if (startPrice is not null && startPrice.Close > 0m) {
                    // No newer price than the buy price means the position is carried flat
                    decimal ratio = endPrice is not null && endPrice.Close > 0m && endPrice.PriceDate > startPrice.PriceDate
                        ? endPrice.Close / startPrice.Close
                        : 1m;
                    periodReturnPct = Math.Round((ratio - 1m) * 100m, 2);
                    returnSum += ratio - 1m;
                    returnCount++;
                }

                bool entered = i > 0 && previousIds is not null && !previousIds.Contains(c.CompanyId);
                bool left = nextIds is not null && !nextIds.Contains(c.CompanyId);

                periodConstituents.Add(new GrahamBacktestConstituent(
                    c.CompanyId, c.Cik, c.CompanyName, c.Ticker, c.Exchange,
                    startPrice?.Close, startPrice?.PriceDate,
                    endPrice?.Close, endPrice?.PriceDate,
                    periodReturnPct, entered, left));
            }

            // Equal weight across priced holdings; an empty month sits in cash (0%)
            decimal portfolioReturn = returnCount > 0 ? returnSum / returnCount : 0m;
            cumulative *= 1m + portfolioReturn;

            decimal? benchmarkReturnPct = null;
            decimal? benchmarkCumulativeOut = null;
            _ = startPrices.TryGetValue(BenchmarkTicker, out LatestPrice? benchStart);
            _ = endPrices.TryGetValue(BenchmarkTicker, out LatestPrice? benchEnd);
            if (benchStart is not null && benchStart.Close > 0m && benchEnd is not null && benchEnd.Close > 0m) {
                decimal benchReturn = benchEnd.Close / benchStart.Close - 1m;
                benchmarkCumulative *= 1m + benchReturn;
                benchmarkReturnPct = Math.Round(benchReturn * 100m, 2);
                benchmarkCumulativeOut = Math.Round(benchmarkCumulative, 2);
            } else {
                benchmarkComplete = false;
            }

            totalConstituents += constituents.Count;

            periods.Add(new GrahamBacktestPeriod(
                startDate, endDate, constituents.Count,
                Math.Round(portfolioReturn * 100m, 2),
                Math.Round(cumulative, 2),
                benchmarkReturnPct, benchmarkCumulativeOut,
                periodConstituents));
        }

        DateOnly firstDate = dates[0];
        DateOnly lastDate = periods[periods.Count - 1].EndDate;
        int days = lastDate.DayNumber - firstDate.DayNumber;

        decimal totalReturnPct = Math.Round((cumulative / StartingValue - 1m) * 100m, 2);
        decimal? annualizedReturnPct = Annualize(cumulative / StartingValue, days);

        decimal? benchmarkTotalReturnPct = null;
        decimal? benchmarkAnnualizedReturnPct = null;
        decimal? benchmarkFinalValue = null;
        if (benchmarkComplete) {
            benchmarkTotalReturnPct = Math.Round((benchmarkCumulative / StartingValue - 1m) * 100m, 2);
            benchmarkAnnualizedReturnPct = Annualize(benchmarkCumulative / StartingValue, days);
            benchmarkFinalValue = Math.Round(benchmarkCumulative, 2);
        }

        var summary = new GrahamBacktestSummary(
            firstDate, lastDate, periods.Count,
            Math.Round((decimal)totalConstituents / periods.Count, 1),
            totalReturnPct, annualizedReturnPct, Math.Round(cumulative, 2),
            benchmarkTotalReturnPct, benchmarkAnnualizedReturnPct, benchmarkFinalValue,
            BenchmarkTicker, minScore);

        return Result<GrahamBacktestReport>.Success(new GrahamBacktestReport(summary, periods));
    }

    private static bool MatchesInterval(DateOnly date, GrahamBacktestInterval interval) {
        return interval == GrahamBacktestInterval.Weekly
            ? date.DayOfWeek == DayOfWeek.Friday
            : date.Day == DateTime.DaysInMonth(date.Year, date.Month);
    }

    private static Dictionary<string, LatestPrice> ToTickerMap(Result<IReadOnlyCollection<LatestPrice>> pricesResult) {
        var map = new Dictionary<string, LatestPrice>(StringComparer.OrdinalIgnoreCase);
        if (pricesResult.IsSuccess && pricesResult.Value is not null) {
            foreach (LatestPrice p in pricesResult.Value)
                map[p.Ticker] = p;
        }
        return map;
    }

    private static decimal? Annualize(decimal ratio, int days) {
        if (days <= 0 || ratio <= 0m)
            return null;
        double annualized = (Math.Pow((double)ratio, 365.25 / days) - 1.0) * 100.0;
        return double.IsFinite(annualized) ? Math.Round((decimal)annualized, 2) : null;
    }
}
