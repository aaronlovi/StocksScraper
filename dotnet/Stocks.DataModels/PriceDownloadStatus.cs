using System;

namespace Stocks.DataModels;

/// <summary>
/// Tracks the most recent download timestamp for a ticker mapping.
/// Used to spread download coverage across symbols.
/// </summary>
/// <param name="Cik">SEC Central Index Key for the issuer.</param>
/// <param name="Ticker">Normalized ticker symbol.</param>
/// <param name="Exchange">Exchange name, if known.</param>
/// <param name="LastDownloadedUtc">UTC timestamp of the last successful download.</param>
public record PriceDownloadStatus(
    ulong Cik,
    string Ticker,
    string? Exchange,
    DateTime LastDownloadedUtc
);
