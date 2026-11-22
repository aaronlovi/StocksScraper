using System.Collections.Generic;

namespace Stocks.DataModels.Scoring;

/// <summary>
/// Stores the 15 value-check results as one string: a letter per check in check-number
/// order, P (pass), F (fail) or N (not available). Written to <c>company_scores</c> and
/// <c>graham_score_snapshots</c> so readers such as the Portfolio Advisor use the real
/// results instead of re-deriving them from the stored metric columns.
/// </summary>
public static class ScoringCheckResults {
    public const int CheckCount = 15;

    public static string Encode(IReadOnlyList<ScoringCheck> scorecard) {
        var chars = new char[scorecard.Count];
        for (int i = 0; i < scorecard.Count; i++)
            chars[i] = ToLetter(scorecard[i].Result);
        return new string(chars);
    }

    /// <summary>Decodes a stored string; null when it is missing or malformed.</summary>
    public static IReadOnlyList<ScoringCheckResult>? Decode(string? stored) {
        if (stored is null || stored.Length != CheckCount)
            return null;

        var results = new List<ScoringCheckResult>(CheckCount);
        foreach (char c in stored) {
            switch (c) {
                case 'P': results.Add(ScoringCheckResult.Pass); break;
                case 'F': results.Add(ScoringCheckResult.Fail); break;
                case 'N': results.Add(ScoringCheckResult.NotAvailable); break;
                default: return null;
            }
        }
        return results;
    }

    public static char ToLetter(ScoringCheckResult result) => result switch {
        ScoringCheckResult.Pass => 'P',
        ScoringCheckResult.Fail => 'F',
        _ => 'N',
    };
}
