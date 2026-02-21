using System;
using System.Collections.Generic;

namespace Stocks.DataModels.Scoring;

public record MoatScoringResult(
    IReadOnlyDictionary<int, IReadOnlyDictionary<string, decimal>> RawDataByYear,
    MoatDerivedMetrics Metrics,
    IReadOnlyList<ScoringCheck> Scorecard,
    IReadOnlyList<MoatYearMetrics> TrendData,
    int OverallScore,
    int ComputableChecks,
    int YearsOfData,
    decimal? PricePerShare,
    DateOnly? PriceDate,
    long? SharesOutstanding);
