namespace Stocks.Persistence.Database.Statements;

/// <summary>
/// The exchange column of price_imports and price_downloads. It is part of each table's
/// primary key, which Postgres makes NOT NULL, so a ticker without a known exchange is
/// stored as '' and read back as null.
/// </summary>
internal static class PriceExchangeColumn {
    internal static string ToDb(string? exchange) => exchange ?? string.Empty;

    internal static string? FromDb(string? stored) => string.IsNullOrEmpty(stored) ? null : stored;
}
