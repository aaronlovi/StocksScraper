using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Stocks.DataModels;
using Stocks.DataModels.Scoring;
using Stocks.Persistence.Database;
using Stocks.Shared;
using Stocks.Shared.Models;

namespace Stocks.Persistence.Services;

/// <summary>
/// Turns a list of held tickers into buy/sell/hold recommendations against the Graham
/// 15-point scores. "Now" is the live company_scores table (rebuilt by
/// --compute-all-scores after a data refresh); the baseline for "what changed and why"
/// is the most recent stored snapshot. Reasons list the specific checks that flipped,
/// and each change is attributed to a new filing or a pure price move.
/// </summary>
public sealed class PortfolioAdvisorService {
    private const int PerfectScore = 15;
    private const string TriggerFiling = "filing";
    private const string TriggerPrice = "price";

    private readonly IDbmService _dbm;

    public PortfolioAdvisorService(IDbmService dbm) {
        _dbm = dbm;
    }

    public async Task<Result<PortfolioAdvisorReport>> GetRecommendations(
        IReadOnlyCollection<string> tickers, CancellationToken ct) {

        var portfolioTickers = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string raw in tickers) {
            string ticker = raw.Trim().ToUpperInvariant();
            if (ticker.Length == 0 || !seen.Add(ticker))
                continue;
            portfolioTickers.Add(ticker);
        }

        var largePage = new PaginationRequest(1, 50000, 50000);
        Result<PagedResults<CompanyScoreSummary>> scoresResult = await _dbm.GetCompanyScores(
            largePage, ScoresSortBy.OverallScore, SortDirection.Descending, null, ct);
        if (scoresResult.IsFailure || scoresResult.Value is null)
            return Result<PortfolioAdvisorReport>.Failure(ErrorCodes.GenericError, scoresResult.ErrorMessage);

        var currentByTicker = new Dictionary<string, CompanyScoreSummary>(StringComparer.OrdinalIgnoreCase);
        DateTime? scoresComputedAt = null;
        foreach (CompanyScoreSummary s in scoresResult.Value.Items) {
            if (s.Ticker is not null && !currentByTicker.ContainsKey(s.Ticker))
                currentByTicker[s.Ticker] = s;
            if (scoresComputedAt is null || s.ComputedAt > scoresComputedAt)
                scoresComputedAt = s.ComputedAt;
        }

        // Baseline: the most recent stored snapshot (used to explain what changed)
        DateOnly? baselineDate = null;
        var baselineByCompany = new Dictionary<ulong, CompanyScoreSummary>();
        Result<IReadOnlyCollection<DateOnly>> datesResult = await _dbm.GetGrahamScoreSnapshotDates(ct);
        if (datesResult.IsSuccess && datesResult.Value is not null && datesResult.Value.Count > 0) {
            foreach (DateOnly d in datesResult.Value) {
                if (baselineDate is null || d > baselineDate)
                    baselineDate = d;
            }
            Result<PagedResults<CompanyScoreSummary>> baselineResult = await _dbm.GetGrahamScoreSnapshots(
                baselineDate!.Value, largePage, null, ct);
            if (baselineResult.IsSuccess && baselineResult.Value is not null) {
                foreach (CompanyScoreSummary s in baselineResult.Value.Items)
                    baselineByCompany[s.CompanyId] = s;
            }
        }

        var sells = new List<PortfolioRecommendation>();
        var holds = new List<PortfolioRecommendation>();
        var buys = new List<PortfolioRecommendation>();
        var unknowns = new List<PortfolioRecommendation>();

        foreach (string ticker in portfolioTickers) {
            if (!currentByTicker.TryGetValue(ticker, out CompanyScoreSummary? current)) {
                unknowns.Add(new PortfolioRecommendation(
                    ticker, 0, "0", null, "unknown", null, null, null, null, null,
                    ["No Graham score found for this ticker. Check the symbol, or the company may lack sufficient filing data."]));
                continue;
            }

            CompanyScoreSummary? baseline = baselineByCompany.TryGetValue(current.CompanyId, out CompanyScoreSummary? b) ? b : null;

            if (IsPerfect(current)) {
                var reasons = new List<string>();
                if (baseline is not null && !IsPerfect(baseline)) {
                    reasons.Add($"Recovered to 15/15 since {baselineDate} (was {baseline.OverallScore}/{baseline.ComputableChecks}).");
                    reasons.AddRange(DescribeFlippedChecks(baseline, current));
                } else {
                    reasons.Add(baseline is not null
                        ? $"Still 15/15 (unchanged since {baselineDate})."
                        : "Currently 15/15.");
                }
                holds.Add(MakeRecommendation(ticker, current, "hold", ResolveTrigger(baseline, current), reasons));
            } else {
                var reasons = new List<string>();
                if (baseline is not null && IsPerfect(baseline))
                    reasons.Add($"Dropped from 15/15 (as of {baselineDate}) to {current.OverallScore}/{current.ComputableChecks}.");
                else if (baseline is not null)
                    reasons.Add($"Scores {current.OverallScore}/{current.ComputableChecks} (was {baseline.OverallScore}/{baseline.ComputableChecks} as of {baselineDate}).");
                else
                    reasons.Add($"Scores {current.OverallScore}/{current.ComputableChecks}.");

                List<string> flipped = baseline is not null ? DescribeFlippedChecks(baseline, current) : [];
                if (flipped.Count > 0)
                    reasons.AddRange(flipped);
                else
                    reasons.AddRange(DescribeFailingChecks(current));

                sells.Add(MakeRecommendation(ticker, current, "sell", ResolveTrigger(baseline, current), reasons));
            }
        }

        // Buy candidates: every current 15/15 company not already in the portfolio
        var buyRows = new List<CompanyScoreSummary>();
        foreach (CompanyScoreSummary s in scoresResult.Value.Items) {
            if (!IsPerfect(s) || s.Ticker is null || seen.Contains(s.Ticker))
                continue;
            buyRows.Add(s);
        }
        buyRows.Sort((a, b) => string.Compare(a.Ticker, b.Ticker, StringComparison.OrdinalIgnoreCase));

        foreach (CompanyScoreSummary current in buyRows) {
            CompanyScoreSummary? baseline = baselineByCompany.TryGetValue(current.CompanyId, out CompanyScoreSummary? b) ? b : null;
            var reasons = new List<string>();
            if (baseline is null) {
                reasons.Add("Scores 15/15. Not present in the baseline snapshot (new to the scored universe).");
            } else if (IsPerfect(baseline)) {
                reasons.Add($"Already 15/15 at the {baselineDate} baseline — an established qualifier, not a fresh signal.");
            } else {
                reasons.Add($"Newly qualified: was {baseline.OverallScore}/{baseline.ComputableChecks} as of {baselineDate}.");
                reasons.AddRange(DescribeFlippedChecks(baseline, current));
            }
            buys.Add(MakeRecommendation(current.Ticker!, current, "buy", ResolveTrigger(baseline, current), reasons));
        }

        sells.Sort((a, b) => string.Compare(a.Ticker, b.Ticker, StringComparison.OrdinalIgnoreCase));
        holds.Sort((a, b) => string.Compare(a.Ticker, b.Ticker, StringComparison.OrdinalIgnoreCase));

        return Result<PortfolioAdvisorReport>.Success(new PortfolioAdvisorReport(
            scoresComputedAt, baselineDate, sells, buys, holds, unknowns));
    }

    private static bool IsPerfect(CompanyScoreSummary s) =>
        s.OverallScore == PerfectScore && s.ComputableChecks == PerfectScore;

    private static PortfolioRecommendation MakeRecommendation(
        string ticker, CompanyScoreSummary s, string action, string? trigger, List<string> reasons) =>
        new(ticker.ToUpperInvariant(), s.CompanyId, s.Cik, s.CompanyName, action,
            s.OverallScore, s.ComputableChecks, s.PricePerShare, s.PriceDate, trigger, reasons);

    /// <summary>
    /// Rebuilds the 15 checks from a stored score row's metric columns. The oldest
    /// retained-earnings value is not stored, so check 13 evaluates as unavailable here;
    /// callers skip it when diffing.
    /// </summary>
    private static IReadOnlyList<ScoringCheck> BuildChecks(CompanyScoreSummary s) {
        var metrics = new DerivedMetrics(
            s.BookValue, s.MarketCap, s.DebtToEquityRatio, s.PriceToBookRatio, s.DebtToBookRatio,
            s.AdjustedRetainedEarnings, null, s.AverageNetCashFlow, s.AverageOwnerEarnings,
            s.AverageRoeCF, s.AverageRoeOE, s.EstimatedReturnCF, s.EstimatedReturnOE,
            s.CurrentDividendsPaid);
        return ScoringService.EvaluateChecks(metrics, s.YearsOfData);
    }

    private const int RetainedEarningsTrendCheckNumber = 13;

    private static List<string> DescribeFlippedChecks(CompanyScoreSummary baseline, CompanyScoreSummary current) {
        IReadOnlyList<ScoringCheck> before = BuildChecks(baseline);
        IReadOnlyList<ScoringCheck> after = BuildChecks(current);

        var beforeByNumber = new Dictionary<int, ScoringCheck>();
        foreach (ScoringCheck check in before)
            beforeByNumber[check.CheckNumber] = check;

        var reasons = new List<string>();
        foreach (ScoringCheck currentCheck in after) {
            if (currentCheck.CheckNumber == RetainedEarningsTrendCheckNumber)
                continue; // not reconstructible from stored rows
            if (!beforeByNumber.TryGetValue(currentCheck.CheckNumber, out ScoringCheck? baselineCheck))
                continue;
            if (baselineCheck.Result == currentCheck.Result)
                continue;

            string direction = currentCheck.Result switch {
                ScoringCheckResult.Pass => "now PASSES",
                ScoringCheckResult.Fail => "now FAILS",
                _ => "is no longer computable",
            };
            reasons.Add($"{currentCheck.Name} ({currentCheck.Threshold}) {direction}: "
                + $"{FormatValue(baselineCheck.ComputedValue)} → {FormatValue(currentCheck.ComputedValue)}.");
        }

        // The overall score includes the retained-earnings trend check we cannot diff
        if (reasons.Count == 0 && baseline.OverallScore != current.OverallScore)
            reasons.Add("Retained Earnings Increased (check 13) changed — compare the company's scoring page for detail.");

        return reasons;
    }

    private static List<string> DescribeFailingChecks(CompanyScoreSummary current) {
        var reasons = new List<string>();
        foreach (ScoringCheck check in BuildChecks(current)) {
            if (check.Result == ScoringCheckResult.Fail)
                reasons.Add($"{check.Name} ({check.Threshold}) FAILS: {FormatValue(check.ComputedValue)}.");
        }
        if (reasons.Count == 0)
            reasons.Add("Failing check detail not reconstructible from stored metrics — see the company's scoring page.");
        return reasons;
    }

    private static string? ResolveTrigger(CompanyScoreSummary? baseline, CompanyScoreSummary current) {
        if (baseline is null)
            return null;

        bool unchanged = baseline.YearsOfData == current.YearsOfData
            && baseline.BookValue == current.BookValue
            && baseline.DebtToEquityRatio == current.DebtToEquityRatio
            && baseline.AverageNetCashFlow == current.AverageNetCashFlow
            && baseline.AverageOwnerEarnings == current.AverageOwnerEarnings
            && baseline.AdjustedRetainedEarnings == current.AdjustedRetainedEarnings
            && baseline.AverageRoeCF == current.AverageRoeCF
            && baseline.AverageRoeOE == current.AverageRoeOE
            && baseline.SharesOutstanding == current.SharesOutstanding;

        return unchanged ? TriggerPrice : TriggerFiling;
    }

    private static string FormatValue(decimal? value) {
        if (!value.HasValue)
            return "n/a";
        decimal abs = Math.Abs(value.Value);
        if (abs >= 1_000_000_000m)
            return (value.Value / 1_000_000_000m).ToString("0.##", CultureInfo.InvariantCulture) + "B";
        if (abs >= 1_000_000m)
            return (value.Value / 1_000_000m).ToString("0.##", CultureInfo.InvariantCulture) + "M";
        return value.Value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
