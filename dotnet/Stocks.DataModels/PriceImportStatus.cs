using System;

namespace Stocks.DataModels;

/// <summary>
/// Tracks the most recent import timestamp for a ticker mapping.
/// Used to rotate imports across symbols.
/// </summary>
/// <param name="Cik">SEC Central Index Key for the issuer.</param>
/// <param name="Ticker">Normalized ticker symbol.</param>
/// <param name="Exchange">Exchange name, if known.</param>
/// <param name="LastImportedUtc">UTC timestamp of the last successful import.</param>
public record PriceImportStatus(
    ulong Cik,
    string Ticker,
    string? Exchange,
    DateTime LastImportedUtc
);
