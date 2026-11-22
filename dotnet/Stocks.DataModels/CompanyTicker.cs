namespace Stocks.DataModels;

/// <param name="Title">The SEC's current company name from the ticker mappings file; null if unknown.</param>
public record CompanyTicker(ulong CompanyId, string Ticker, string? Exchange, string? Title = null);
