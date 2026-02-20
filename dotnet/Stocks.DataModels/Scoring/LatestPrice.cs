using System;

namespace Stocks.DataModels.Scoring;

public record LatestPrice(string Ticker, decimal Close, DateOnly PriceDate);
