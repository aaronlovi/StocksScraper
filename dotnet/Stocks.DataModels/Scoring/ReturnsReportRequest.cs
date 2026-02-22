namespace Stocks.DataModels.Scoring;

public enum ReturnsReportSortBy
{
    OverallScore,
    TotalReturnPct,
    AnnualizedReturnPct,
    CurrentValueOf1000
}

public record ReturnsReportFilter(int? MinScore, int? MaxScore, string? Exchange);
