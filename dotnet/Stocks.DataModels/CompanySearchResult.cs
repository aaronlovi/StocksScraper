using System;

namespace Stocks.DataModels;

public record CompanySearchResult(ulong CompanyId, string Cik, string CompanyName, string? Ticker, string? Exchange, decimal? LatestPrice, DateOnly? LatestPriceDate);
