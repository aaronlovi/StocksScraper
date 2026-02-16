namespace Stocks.DataModels;

public record CompanyTicker(ulong CompanyId, string Ticker, string? Exchange);
