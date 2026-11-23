using System;

namespace Stocks.DataModels.Scoring;

/// <summary>
/// Which of a filing's facts belong to its own period (E13). A line present only in the
/// comparative columns carries an earlier period's amount, so it is not scored. Facts dated
/// after the report date (cover-page share counts) are kept; the few days of slack cover
/// 52/53-week years and report dates rounded to the month end.
/// </summary>
public static class ScoringFactPeriod {
    public const int MaxDaysBeforeReportDate = 30;

    public static bool IsOfFilingPeriod(DateOnly factEndDate, DateOnly reportDate) =>
        factEndDate >= reportDate.AddDays(-MaxDaysBeforeReportDate);
}
