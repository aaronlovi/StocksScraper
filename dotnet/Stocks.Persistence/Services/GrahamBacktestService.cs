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
/// Simulates a rebalanced portfolio of the top-scoring Graham companies using the stored
/// point-in-time snapshots. At each snapshot date the portfolio reacts to membership
/// changes according to the trade policy (all changes / filing-driven only / price-driven
/// only), holds equal-weighted until the next snapshot date (or today for the last one),
/// and chains the returns. A holding with no newer price is carried at its last known
/// price (0% for the period).
/// </summary>
public sealed class GrahamBacktestService {
    private const string BenchmarkTicker = "SPY";
    private const decimal StartingValue = 1000m;

    private const string TriggerFiling = "filing";
    private const string TriggerPrice = "price";

    private readonly IDbmService _dbm;

    public GrahamBacktestService(IDbmService dbm) {
        _dbm = dbm;
    }

    public async Task<Result<GrahamBacktestReport>> GetBacktest(
        int minScore, GrahamBacktestInterval interval, GrahamBacktestPolicy policy,
        bool confirmChanges, CancellationToken ct) {
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

        var byDate = new Dictionary<DateOnly, Dictionary<ulong, GrahamSnapshotConstituent>>();
        var allConstituentIds = new HashSet<ulong>();
        foreach (GrahamSnapshotConstituent c in constituentsResult.Value) {
            if (!byDate.TryGetValue(c.AsOfDate, out Dictionary<ulong, GrahamSnapshotConstituent>? qualifying)) {
                qualifying = [];
                byDate[c.AsOfDate] = qualifying;
            }
            qualifying[c.CompanyId] = c;
            _ = allConstituentIds.Add(c.CompanyId);
        }

        // Fundamentals per constituent per date, used to attribute membership changes
        // to a new filing vs a pure price move
        var fundamentalsByCompany = new Dictionary<ulong, Dictionary<DateOnly, GrahamSnapshotFundamentals>>();
        if (allConstituentIds.Count > 0) {
            Result<IReadOnlyCollection<GrahamSnapshotFundamentals>> fundamentalsResult =
                await _dbm.GetGrahamSnapshotFundamentals(allConstituentIds, ct);
            if (fundamentalsResult.IsSuccess && fundamentalsResult.Value is not null) {
                foreach (GrahamSnapshotFundamentals f in fundamentalsResult.Value) {
                    if (!fundamentalsByCompany.TryGetValue(f.CompanyId, out Dictionary<DateOnly, GrahamSnapshotFundamentals>? byFundDate)) {
                        byFundDate = [];
                        fundamentalsByCompany[f.CompanyId] = byFundDate;
                    }
                    byFundDate[f.AsOfDate] = f;
                }
            }
        }

        // Pass 1: simulate the positions timeline. At each date, sell held companies that
        // no longer qualify and buy qualifying companies not yet held — but only when the
        // change's trigger matches the trade policy. Under FilingOnly, a price-driven
        // dropout is held until the next filing confirms it (at most ~a quarter), and a
        // price-driven qualifier is bought when a filing confirms it still qualifies.
        // With confirmChanges (hysteresis), a change must persist for two consecutive
        // snapshots before it is acted on, so one-period threshold flicker never trades.
        var heldByPeriod = new List<Dictionary<ulong, GrahamSnapshotConstituent>>(dates.Count);
        var entryTriggersByPeriod = new List<Dictionary<ulong, string>>(dates.Count);
        var exitTriggersAtDate = new List<Dictionary<ulong, string>>(dates.Count);

        var held = new Dictionary<ulong, GrahamSnapshotConstituent>();
        for (int i = 0; i < dates.Count; i++) {
            var entryTriggers = new Dictionary<ulong, string>();
            var exitTriggers = new Dictionary<ulong, string>();

            Dictionary<ulong, GrahamSnapshotConstituent> qualifying =
                byDate.TryGetValue(dates[i], out Dictionary<ulong, GrahamSnapshotConstituent>? q) ? q : [];

            if (i == 0) {
                // Every policy starts from the same portfolio: the full qualifying list
                foreach (KeyValuePair<ulong, GrahamSnapshotConstituent> entry in qualifying)
                    held[entry.Key] = entry.Value;
            } else {
                Dictionary<ulong, GrahamSnapshotConstituent> prevQualifying =
                    byDate.TryGetValue(dates[i - 1], out Dictionary<ulong, GrahamSnapshotConstituent>? pq) ? pq : [];

                // Triggers span the whole window a confirmed change covers
                int triggerFromIndex = confirmChanges ? Math.Max(0, i - 2) : i - 1;

                var toSell = new List<ulong>();
                foreach (KeyValuePair<ulong, GrahamSnapshotConstituent> position in held) {
                    if (qualifying.ContainsKey(position.Key))
                        continue;
                    if (confirmChanges && prevQualifying.ContainsKey(position.Key))
                        continue; // first miss: wait for confirmation
                    string trigger = ResolveTrigger(fundamentalsByCompany, position.Key, dates[triggerFromIndex], dates[i]);
                    if (PolicyActsOn(policy, trigger)) {
                        toSell.Add(position.Key);
                        exitTriggers[position.Key] = trigger;
                    }
                }
                foreach (ulong companyId in toSell)
                    _ = held.Remove(companyId);

                foreach (KeyValuePair<ulong, GrahamSnapshotConstituent> candidate in qualifying) {
                    if (held.ContainsKey(candidate.Key)) {
                        held[candidate.Key] = candidate.Value; // refresh name/ticker info
                        continue;
                    }
                    if (confirmChanges && !prevQualifying.ContainsKey(candidate.Key))
                        continue; // first qualification: wait for confirmation
                    string trigger = ResolveTrigger(fundamentalsByCompany, candidate.Key, dates[triggerFromIndex], dates[i]);
                    if (PolicyActsOn(policy, trigger)) {
                        held[candidate.Key] = candidate.Value;
                        entryTriggers[candidate.Key] = trigger;
                    }
                }
            }

            heldByPeriod.Add(new Dictionary<ulong, GrahamSnapshotConstituent>(held));
            entryTriggersByPeriod.Add(entryTriggers);
            exitTriggersAtDate.Add(exitTriggers);
        }

        // Pass 2: price each period's portfolio and chain the returns
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);

        decimal cumulative = StartingValue;
        decimal benchmarkCumulative = StartingValue;
        bool benchmarkComplete = true;
        int totalConstituents = 0;

        var periods = new List<GrahamBacktestPeriod>(dates.Count);

        for (int i = 0; i < dates.Count; i++) {
            DateOnly startDate = dates[i];
            DateOnly endDate = i + 1 < dates.Count ? dates[i + 1] : today;

            Dictionary<ulong, GrahamSnapshotConstituent> portfolio = heldByPeriod[i];
            Dictionary<ulong, string> entryTriggers = entryTriggersByPeriod[i];
            Dictionary<ulong, string>? nextExitTriggers = i + 1 < dates.Count ? exitTriggersAtDate[i + 1] : null;
            Dictionary<ulong, GrahamSnapshotConstituent>? nextPortfolio = i + 1 < dates.Count ? heldByPeriod[i + 1] : null;

            var tickers = new List<string> { BenchmarkTicker };
            foreach (KeyValuePair<ulong, GrahamSnapshotConstituent> position in portfolio) {
                if (!string.IsNullOrWhiteSpace(position.Value.Ticker))
                    tickers.Add(position.Value.Ticker);
            }
            string[] tickerArray = [.. tickers];

            Task<Result<IReadOnlyCollection<LatestPrice>>> startPricesTask =
                _dbm.GetPricesNearDateForTickers(startDate, tickerArray, ct);
            Task<Result<IReadOnlyCollection<LatestPrice>>> endPricesTask =
                _dbm.GetPricesNearDateForTickers(endDate, tickerArray, ct);
            await Task.WhenAll(startPricesTask, endPricesTask);

            Dictionary<string, LatestPrice> startPrices = ToTickerMap(startPricesTask.Result);
            Dictionary<string, LatestPrice> endPrices = ToTickerMap(endPricesTask.Result);

            decimal returnSum = 0m;
            int returnCount = 0;
            var periodConstituents = new List<GrahamBacktestConstituent>(portfolio.Count);

            foreach (KeyValuePair<ulong, GrahamSnapshotConstituent> position in portfolio) {
                GrahamSnapshotConstituent c = position.Value;
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

                bool entered = entryTriggers.ContainsKey(c.CompanyId);
                bool left = nextPortfolio is not null && !nextPortfolio.ContainsKey(c.CompanyId);

                string? enteredTrigger = entered ? entryTriggers[c.CompanyId] : null;
                string? leftTrigger = left && nextExitTriggers is not null && nextExitTriggers.TryGetValue(c.CompanyId, out string? exitTrigger)
                    ? exitTrigger
                    : null;

                periodConstituents.Add(new GrahamBacktestConstituent(
                    c.CompanyId, c.Cik, c.CompanyName, c.Ticker, c.Exchange,
                    startPrice?.Close, startPrice?.PriceDate,
                    endPrice?.Close, endPrice?.PriceDate,
                    periodReturnPct, entered, left, enteredTrigger, leftTrigger));
            }

            SortByTicker(periodConstituents);

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

            totalConstituents += portfolio.Count;

            periods.Add(new GrahamBacktestPeriod(
                startDate, endDate, portfolio.Count,
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

    private static bool PolicyActsOn(GrahamBacktestPolicy policy, string trigger) {
        return policy switch {
            GrahamBacktestPolicy.FilingOnly => trigger == TriggerFiling,
            GrahamBacktestPolicy.PriceOnly => trigger == TriggerPrice,
            _ => true,
        };
    }

    /// <summary>
    /// Attributes a membership change between two adjacent snapshots: identical
    /// price-independent fundamentals mean only the price moved; any difference (or a
    /// company appearing in / vanishing from the scored universe) means a filing changed
    /// the inputs.
    /// </summary>
    private static string ResolveTrigger(
        Dictionary<ulong, Dictionary<DateOnly, GrahamSnapshotFundamentals>> fundamentalsByCompany,
        ulong companyId, DateOnly fromDate, DateOnly toDate) {

        if (!fundamentalsByCompany.TryGetValue(companyId, out Dictionary<DateOnly, GrahamSnapshotFundamentals>? byFundDate))
            return TriggerFiling;
        if (!byFundDate.TryGetValue(fromDate, out GrahamSnapshotFundamentals? before))
            return TriggerFiling;
        if (!byFundDate.TryGetValue(toDate, out GrahamSnapshotFundamentals? after))
            return TriggerFiling;

        bool unchanged = before.YearsOfData == after.YearsOfData
            && before.BookValue == after.BookValue
            && before.DebtToEquityRatio == after.DebtToEquityRatio
            && before.AverageNetCashFlow == after.AverageNetCashFlow
            && before.AverageOwnerEarnings == after.AverageOwnerEarnings
            && before.AdjustedRetainedEarnings == after.AdjustedRetainedEarnings
            && before.AverageRoeCF == after.AverageRoeCF
            && before.AverageRoeOE == after.AverageRoeOE
            && before.SharesOutstanding == after.SharesOutstanding;

        return unchanged ? TriggerPrice : TriggerFiling;
    }

    private static void SortByTicker(List<GrahamBacktestConstituent> constituents) {
        constituents.Sort((a, b) => {
            int cmp = string.Compare(a.Ticker, b.Ticker, StringComparison.OrdinalIgnoreCase);
            if (cmp == 0)
                cmp = a.CompanyId.CompareTo(b.CompanyId);
            return cmp;
        });
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
