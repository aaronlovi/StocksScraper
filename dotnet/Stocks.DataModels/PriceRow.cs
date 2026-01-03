using System;

namespace Stocks.DataModels;

/// <summary>
/// Represents a single daily price record for a stock.
/// Includes identifiers, metadata, and OHLCV values.
/// </summary>
/// <param name="PriceId">Unique identifier for this price row.</param>
/// <param name="Cik">SEC Central Index Key for the issuer.</param>
/// <param name="Ticker">Normalized ticker symbol.</param>
/// <param name="Exchange">Exchange name, if known.</param>
/// <param name="StooqSymbol">Source-specific symbol used for downloads.</param>
/// <param name="PriceDate">Trading date for the price.</param>
/// <param name="Open">Open price for the day.</param>
/// <param name="High">High price for the day.</param>
/// <param name="Low">Low price for the day.</param>
/// <param name="Close">Close price for the day.</param>
/// <param name="Volume">Volume for the day.</param>
public record PriceRow(
    ulong PriceId,
    ulong Cik,
    string Ticker,
    string? Exchange,
    string StooqSymbol,
    DateOnly PriceDate,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    long Volume
);
