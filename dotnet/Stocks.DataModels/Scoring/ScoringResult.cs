using System;
using System.Collections.Generic;

namespace Stocks.DataModels.Scoring;

public record ScoringResult(
    IReadOnlyDictionary<int, IReadOnlyDictionary<string, decimal>> RawDataByYear,
    DerivedMetrics Metrics,
    IReadOnlyList<ScoringCheck> Scorecard,
    int OverallScore,
    int ComputableChecks,
    int YearsOfData,
    decimal? PricePerShare,
    DateOnly? PriceDate,
    long? SharesOutstanding);
