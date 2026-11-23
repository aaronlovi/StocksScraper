using System;

namespace Stocks.DataModels.Scoring;

/// <summary>Report filter on the financials flag (<see cref="IndustryClassification.IsFinancial"/>).</summary>
public enum FinancialsFilter {
    All,
    Only,
    Exclude
}

public static class FinancialsFilters {
    /// <summary>True when a company with this SIC code passes the filter. A null code is not financial.</summary>
    public static bool Matches(this FinancialsFilter filter, int? sicCode) => filter switch {
        FinancialsFilter.Only => IndustryClassification.IsFinancial(sicCode),
        FinancialsFilter.Exclude => !IndustryClassification.IsFinancial(sicCode),
        _ => true,
    };

    /// <summary>Parses a query value: "only" or "exclude" (any case); anything else is All.</summary>
    public static FinancialsFilter Parse(string? value) {
        if (string.Equals(value, "only", StringComparison.OrdinalIgnoreCase))
            return FinancialsFilter.Only;
        if (string.Equals(value, "exclude", StringComparison.OrdinalIgnoreCase))
            return FinancialsFilter.Exclude;
        return FinancialsFilter.All;
    }
}
