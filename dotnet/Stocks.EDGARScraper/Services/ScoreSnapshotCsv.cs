using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Stocks.DataModels.Scoring;

namespace EDGARScraper.Services;

/// <summary>
/// Formats score details as CSV rows for the read-only <c>--snapshot-scores</c> command.
/// One row per company: identity, score, computable checks, years of data, each check as
/// P/F/N, then the summary's metric columns. Compare two snapshots with
/// <c>scripts/score_snapshot_diff.py</c>.
/// </summary>
internal static class ScoreSnapshotCsv {
    internal const int ValueCheckCount = 15;
    internal const int MoatCheckCount = 13;

    private static readonly string[] ValueMetricColumns = [
        "book_value", "market_cap", "de", "pb", "db", "adj_re", "avg_ncf", "avg_oe",
        "roe_cf", "roe_oe", "ret_cf", "ret_oe", "max_buy",
    ];

    private static readonly string[] MoatMetricColumns = [
        "roe_cf", "roe_oe", "capex_ratio", "interest_cov", "de", "est_ret_oe", "gm", "om", "cagr",
    ];

    internal static string ValueHeader() => BuildHeader(ValueCheckCount, ValueMetricColumns);

    internal static string MoatHeader() => BuildHeader(MoatCheckCount, MoatMetricColumns);

    internal static string FormatValueRow(CompanyScoreDetail detail) {
        CompanyScoreSummary s = detail.Summary;
        var sb = new StringBuilder();
        AppendIdentity(sb, s.CompanyId, s.Ticker, s.OverallScore, s.ComputableChecks, s.YearsOfData);
        AppendChecks(sb, detail.Checks, ValueCheckCount);
        AppendNumbers(sb, [
            s.BookValue, s.MarketCap, s.DebtToEquityRatio, s.PriceToBookRatio, s.DebtToBookRatio,
            s.AdjustedRetainedEarnings, s.AverageNetCashFlow, s.AverageOwnerEarnings,
            s.AverageRoeCF, s.AverageRoeOE, s.EstimatedReturnCF, s.EstimatedReturnOE, s.MaxBuyPrice,
        ]);
        return sb.ToString();
    }

    internal static string FormatMoatRow(CompanyMoatScoreDetail detail) {
        CompanyMoatScoreSummary s = detail.Summary;
        var sb = new StringBuilder();
        AppendIdentity(sb, s.CompanyId, s.Ticker, s.OverallScore, s.ComputableChecks, s.YearsOfData);
        AppendChecks(sb, detail.Checks, MoatCheckCount);
        AppendNumbers(sb, [
            s.AverageRoeCF, s.AverageRoeOE, s.CapexRatio, s.InterestCoverage, s.DebtToEquityRatio,
            s.EstimatedReturnOE, s.AverageGrossMargin, s.AverageOperatingMargin, s.RevenueCagr,
        ]);
        return sb.ToString();
    }

    /// <summary>Formats a number with up to 6 decimals, invariant culture; null is an empty field.</summary>
    internal static string FormatNumber(decimal? value) =>
        value.HasValue
            ? Math.Round(value.Value, 6, MidpointRounding.AwayFromZero).ToString("0.######", CultureInfo.InvariantCulture)
            : string.Empty;

    internal static char FormatResult(ScoringCheckResult result) => ScoringCheckResults.ToLetter(result);

    private static string BuildHeader(int checkCount, string[] metricColumns) {
        var sb = new StringBuilder("company_id,ticker,score,computable,years");
        for (int i = 1; i <= checkCount; i++)
            sb.Append(",c").Append(i.ToString("00", CultureInfo.InvariantCulture));
        foreach (string column in metricColumns)
            sb.Append(',').Append(column);
        return sb.ToString();
    }

    private static void AppendIdentity(StringBuilder sb, ulong companyId, string? ticker,
        int score, int computable, int years) {
        sb.Append(companyId.ToString(CultureInfo.InvariantCulture)).Append(',')
            .Append(EscapeField(ticker ?? string.Empty)).Append(',')
            .Append(score.ToString(CultureInfo.InvariantCulture)).Append(',')
            .Append(computable.ToString(CultureInfo.InvariantCulture)).Append(',')
            .Append(years.ToString(CultureInfo.InvariantCulture));
    }

    private static void AppendChecks(StringBuilder sb, IReadOnlyList<ScoringCheck> checks, int checkCount) {
        var byNumber = new Dictionary<int, ScoringCheckResult>();
        foreach (ScoringCheck check in checks)
            byNumber[check.CheckNumber] = check.Result;

        for (int i = 1; i <= checkCount; i++) {
            sb.Append(',');
            if (byNumber.TryGetValue(i, out ScoringCheckResult result))
                sb.Append(FormatResult(result));
        }
    }

    private static void AppendNumbers(StringBuilder sb, decimal?[] values) {
        foreach (decimal? value in values)
            sb.Append(',').Append(FormatNumber(value));
    }

    private static string EscapeField(string field) {
        if (field.IndexOfAny([',', '"', '\n', '\r']) < 0)
            return field;
        return "\"" + field.Replace("\"", "\"\"") + "\"";
    }
}
