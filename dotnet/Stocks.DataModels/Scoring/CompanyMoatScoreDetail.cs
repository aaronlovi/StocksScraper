using System.Collections.Generic;

namespace Stocks.DataModels.Scoring;

/// <summary>
/// A company's moat score summary together with the individual check results that produced it.
/// Used by the read-only score snapshot command.
/// </summary>
public record CompanyMoatScoreDetail(
    CompanyMoatScoreSummary Summary,
    IReadOnlyList<ScoringCheck> Checks);
