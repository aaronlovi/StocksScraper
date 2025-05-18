namespace Stocks.DataModels.Enums;

/// <summary>
/// Represents whether a "concept" is a credit, debit, or not applicable.
/// See the US-GAAP Taxonomy concepts worksheet
/// </summary>
public enum TaxonomyBalanceTypes {
    None = 0, Credit, Debit, NotApplicable
}

/// <summary>
/// Represents whether a "concept" refers to a duration or instant in time.
/// See the US-GAAP Taxonomy concepts worksheet
/// </summary>
public enum TaxonomyPeriodTypes {
    None = 0, Duration, Instant
}

/// <summary>
/// Represents the type of taxonomy.
/// Currently only US-GAAP is supported.
/// </summary>
public enum TaxonomyTypes {
    None = 0,
    US_GAAP = 1,
}
