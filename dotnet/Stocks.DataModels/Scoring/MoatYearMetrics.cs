namespace Stocks.DataModels.Scoring;

public record MoatYearMetrics(
    int Year,
    decimal? GrossMarginPct,
    decimal? OperatingMarginPct,
    decimal? RoeCfPct,
    decimal? RoeOePct,
    decimal? Revenue);
