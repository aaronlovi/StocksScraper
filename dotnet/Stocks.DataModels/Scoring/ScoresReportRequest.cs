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
    PercentageUpside
}

public enum SortDirection {
    Ascending,
    Descending
}

public record ScoresFilter(int? MinScore, int? MaxScore, string? Exchange);
