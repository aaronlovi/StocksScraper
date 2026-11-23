namespace Stocks.DataModels.Scoring;

public enum ScoresSortBy {
    OverallScore,
    BookValue,
    MarketCap,
    EstimatedReturnCF,
    EstimatedReturnOE,
    DebtToEquityRatio,
    PriceToBookRatio,
    MaxBuyPrice,
    PercentageUpside,
    AverageRoeCF,
    AverageRoeOE
}

public enum SortDirection {
    Ascending,
    Descending
}

/// <summary>
/// Score report filter. <see cref="Financials"/> applies to the value and moat score reports
/// (and the backtest); the Graham snapshot report ignores it.
/// </summary>
public record ScoresFilter(int? MinScore, int? MaxScore, string? Exchange,
    FinancialsFilter Financials = FinancialsFilter.All);
