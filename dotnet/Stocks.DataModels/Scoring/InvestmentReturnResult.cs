using System;

namespace Stocks.DataModels.Scoring;

public record InvestmentReturnResult(
    string Ticker,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal StartPrice,
    decimal EndPrice,
    decimal TotalReturnPct,
    decimal? AnnualizedReturnPct,
    decimal CurrentValueOf1000
);
