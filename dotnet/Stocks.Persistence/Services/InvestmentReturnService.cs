using System;
using System.Threading;
using System.Threading.Tasks;
using Stocks.DataModels;
using Stocks.DataModels.Scoring;
using Stocks.Persistence.Database;
using Stocks.Shared;
using Stocks.Shared.Models;

namespace Stocks.Persistence.Services;

public class InvestmentReturnService {
    private readonly IDbmService _dbm;

    public InvestmentReturnService(IDbmService dbm) {
        _dbm = dbm;
    }

    public async Task<Result<InvestmentReturnResult>> ComputeReturn(
        string ticker, DateOnly startDate, CancellationToken ct) {
        Result<PriceRow?> startPriceResult = await _dbm.GetPriceNearDate(ticker, startDate, ct);
        if (startPriceResult.IsFailure)
            return Result<InvestmentReturnResult>.Failure(startPriceResult);

        PriceRow? startPrice = startPriceResult.Value;
        if (startPrice is null)
            return Result<InvestmentReturnResult>.Failure(ErrorCodes.NoPriceData,
                $"No price data found for {ticker} on or before {startDate}");

        Result<PriceRow?> endPriceResult = await _dbm.GetLatestPriceByTicker(ticker, ct);
        if (endPriceResult.IsFailure)
            return Result<InvestmentReturnResult>.Failure(endPriceResult);

        PriceRow? endPrice = endPriceResult.Value;
        if (endPrice is null)
            return Result<InvestmentReturnResult>.Failure(ErrorCodes.NoPriceData,
                $"No current price data found for {ticker}");

        if (startPrice.Close <= 0m)
            return Result<InvestmentReturnResult>.Failure(ErrorCodes.NoPriceData,
                $"Start price for {ticker} is zero or negative");

        if (endPrice.Close <= 0m)
            return Result<InvestmentReturnResult>.Failure(ErrorCodes.NoPriceData,
                $"End price for {ticker} is zero or negative");

        decimal totalReturnPct = (endPrice.Close / startPrice.Close - 1m) * 100m;
        decimal currentValueOf1000 = 1000m * endPrice.Close / startPrice.Close;

        int daysHeld = endPrice.PriceDate.DayNumber - startPrice.PriceDate.DayNumber;
        decimal? annualizedReturnPct = null;
        if (daysHeld >= 1) {
            double ratio = (double)(endPrice.Close / startPrice.Close);
            double annualized = Math.Pow(ratio, 365.25 / daysHeld) - 1.0;
            if (double.IsFinite(annualized) && Math.Abs(annualized) < (double)decimal.MaxValue)
                annualizedReturnPct = (decimal)annualized * 100m;
        }

        var result = new InvestmentReturnResult(
            ticker,
            startPrice.PriceDate,
            endPrice.PriceDate,
            startPrice.Close,
            endPrice.Close,
            totalReturnPct,
            annualizedReturnPct,
            currentValueOf1000);

        return Result<InvestmentReturnResult>.Success(result);
    }
}
