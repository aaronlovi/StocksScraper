namespace Stocks.DataModels.Scoring;

public enum ScoringCheckResult { Pass, Fail, NotAvailable }

public record ScoringCheck(
    int CheckNumber,
    string Name,
    decimal? ComputedValue,
    string Threshold,
    ScoringCheckResult Result);
