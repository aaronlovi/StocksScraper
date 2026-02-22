using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Stocks.DataModels;
using Stocks.DataModels.Scoring;
using Stocks.Persistence.Database;
using Stocks.Shared;
using Stocks.Shared.Models;

namespace Stocks.Persistence.Services;

public sealed class InvestmentReturnReportService {
    private readonly IDbmService _dbm;

    public InvestmentReturnReportService(IDbmService dbm) {
        _dbm = dbm;
    }

    public async Task<Result<PagedResults<CompanyScoreReturnSummary>>> GetGrahamReturns(
        DateOnly startDate, PaginationRequest pagination, ReturnsReportSortBy sortBy,
        SortDirection sortDir, ReturnsReportFilter? filter, CancellationToken ct) {

        ScoresFilter? scoresFilter = filter is not null
            ? new ScoresFilter(filter.MinScore, filter.MaxScore, filter.Exchange)
            : null;

        var largePage = new PaginationRequest(1, 10000, 10000);
        Result<PagedResults<CompanyScoreSummary>> scoresResult =
            await _dbm.GetCompanyScores(largePage, ScoresSortBy.OverallScore, SortDirection.Descending, scoresFilter, ct);
        if (scoresResult.IsFailure || scoresResult.Value is null)
            return Result<PagedResults<CompanyScoreReturnSummary>>.Failure(ErrorCodes.GenericError, scoresResult.ErrorMessage);

        return await BuildReturnResults(scoresResult.Value.Items, startDate, pagination, sortBy, sortDir, ct);
    }

    public async Task<Result<PagedResults<CompanyScoreReturnSummary>>> GetBuffettReturns(
        DateOnly startDate, PaginationRequest pagination, ReturnsReportSortBy sortBy,
        SortDirection sortDir, ReturnsReportFilter? filter, CancellationToken ct) {

        ScoresFilter? scoresFilter = filter is not null
            ? new ScoresFilter(filter.MinScore, filter.MaxScore, filter.Exchange)
            : null;

        var largePage = new PaginationRequest(1, 10000, 10000);
        Result<PagedResults<CompanyMoatScoreSummary>> scoresResult =
            await _dbm.GetCompanyMoatScores(largePage, MoatScoresSortBy.OverallScore, SortDirection.Descending, scoresFilter, ct);
        if (scoresResult.IsFailure || scoresResult.Value is null)
            return Result<PagedResults<CompanyScoreReturnSummary>>.Failure(ErrorCodes.GenericError, scoresResult.ErrorMessage);

        return await BuildReturnResults(scoresResult.Value.Items, startDate, pagination, sortBy, sortDir, ct);
    }

    private async Task<Result<PagedResults<CompanyScoreReturnSummary>>> BuildReturnResults(
        IReadOnlyCollection<CompanyScoreSummary> scores, DateOnly startDate,
        PaginationRequest pagination, ReturnsReportSortBy sortBy, SortDirection sortDir,
        CancellationToken ct) {

        Task<Result<IReadOnlyCollection<LatestPrice>>> startPricesTask = _dbm.GetAllPricesNearDate(startDate, ct);
        Task<Result<IReadOnlyCollection<LatestPrice>>> endPricesTask = _dbm.GetAllLatestPrices(ct);
        await Task.WhenAll(startPricesTask, endPricesTask);

        Result<IReadOnlyCollection<LatestPrice>> startPricesResult = startPricesTask.Result;
        Result<IReadOnlyCollection<LatestPrice>> endPricesResult = endPricesTask.Result;

        var startByTicker = new Dictionary<string, LatestPrice>(StringComparer.OrdinalIgnoreCase);
        if (startPricesResult.IsSuccess && startPricesResult.Value is not null) {
            foreach (LatestPrice p in startPricesResult.Value)
                startByTicker[p.Ticker] = p;
        }

        var endByTicker = new Dictionary<string, LatestPrice>(StringComparer.OrdinalIgnoreCase);
        if (endPricesResult.IsSuccess && endPricesResult.Value is not null) {
            foreach (LatestPrice p in endPricesResult.Value)
                endByTicker[p.Ticker] = p;
        }

        var enriched = new List<CompanyScoreReturnSummary>(scores.Count);
        foreach (CompanyScoreSummary s in scores) {
            CompanyScoreReturnSummary row = EnrichScore(s, startByTicker, endByTicker);
            enriched.Add(row);
        }

        SortInPlace(enriched, sortBy, sortDir);
        return Paginate(enriched, pagination);
    }

    private async Task<Result<PagedResults<CompanyScoreReturnSummary>>> BuildReturnResults(
        IReadOnlyCollection<CompanyMoatScoreSummary> scores, DateOnly startDate,
        PaginationRequest pagination, ReturnsReportSortBy sortBy, SortDirection sortDir,
        CancellationToken ct) {

        Task<Result<IReadOnlyCollection<LatestPrice>>> startPricesTask = _dbm.GetAllPricesNearDate(startDate, ct);
        Task<Result<IReadOnlyCollection<LatestPrice>>> endPricesTask = _dbm.GetAllLatestPrices(ct);
        await Task.WhenAll(startPricesTask, endPricesTask);

        Result<IReadOnlyCollection<LatestPrice>> startPricesResult = startPricesTask.Result;
        Result<IReadOnlyCollection<LatestPrice>> endPricesResult = endPricesTask.Result;

        var startByTicker = new Dictionary<string, LatestPrice>(StringComparer.OrdinalIgnoreCase);
        if (startPricesResult.IsSuccess && startPricesResult.Value is not null) {
            foreach (LatestPrice p in startPricesResult.Value)
                startByTicker[p.Ticker] = p;
        }

        var endByTicker = new Dictionary<string, LatestPrice>(StringComparer.OrdinalIgnoreCase);
        if (endPricesResult.IsSuccess && endPricesResult.Value is not null) {
            foreach (LatestPrice p in endPricesResult.Value)
                endByTicker[p.Ticker] = p;
        }

        var enriched = new List<CompanyScoreReturnSummary>(scores.Count);
        foreach (CompanyMoatScoreSummary s in scores) {
            CompanyScoreReturnSummary row = EnrichMoatScore(s, startByTicker, endByTicker);
            enriched.Add(row);
        }

        SortInPlace(enriched, sortBy, sortDir);
        return Paginate(enriched, pagination);
    }

    private static CompanyScoreReturnSummary EnrichScore(
        CompanyScoreSummary s,
        Dictionary<string, LatestPrice> startByTicker,
        Dictionary<string, LatestPrice> endByTicker) {

        ComputeReturns(s.Ticker, startByTicker, endByTicker,
            out decimal? totalReturnPct, out decimal? annualizedReturnPct,
            out decimal? currentValueOf1000, out DateOnly? actualStartDate,
            out DateOnly? actualEndDate, out decimal? startPriceVal, out decimal? endPriceVal);

        return new CompanyScoreReturnSummary(
            s.CompanyId, s.Cik, s.CompanyName, s.Ticker, s.Exchange,
            s.OverallScore, s.ComputableChecks, s.PricePerShare,
            totalReturnPct, annualizedReturnPct, currentValueOf1000,
            actualStartDate, actualEndDate, startPriceVal, endPriceVal,
            s.ComputedAt);
    }

    private static CompanyScoreReturnSummary EnrichMoatScore(
        CompanyMoatScoreSummary s,
        Dictionary<string, LatestPrice> startByTicker,
        Dictionary<string, LatestPrice> endByTicker) {

        ComputeReturns(s.Ticker, startByTicker, endByTicker,
            out decimal? totalReturnPct, out decimal? annualizedReturnPct,
            out decimal? currentValueOf1000, out DateOnly? actualStartDate,
            out DateOnly? actualEndDate, out decimal? startPriceVal, out decimal? endPriceVal);

        return new CompanyScoreReturnSummary(
            s.CompanyId, s.Cik, s.CompanyName, s.Ticker, s.Exchange,
            s.OverallScore, s.ComputableChecks, s.PricePerShare,
            totalReturnPct, annualizedReturnPct, currentValueOf1000,
            actualStartDate, actualEndDate, startPriceVal, endPriceVal,
            s.ComputedAt);
    }

    private static void ComputeReturns(
        string? ticker,
        Dictionary<string, LatestPrice> startByTicker,
        Dictionary<string, LatestPrice> endByTicker,
        out decimal? totalReturnPct,
        out decimal? annualizedReturnPct,
        out decimal? currentValueOf1000,
        out DateOnly? actualStartDate,
        out DateOnly? actualEndDate,
        out decimal? startPriceVal,
        out decimal? endPriceVal) {

        totalReturnPct = null;
        annualizedReturnPct = null;
        currentValueOf1000 = null;
        actualStartDate = null;
        actualEndDate = null;
        startPriceVal = null;
        endPriceVal = null;

        if (ticker is null)
            return;

        if (!startByTicker.TryGetValue(ticker, out LatestPrice? startPrice))
            return;
        if (!endByTicker.TryGetValue(ticker, out LatestPrice? endPrice))
            return;
        if (startPrice.Close <= 0 || endPrice.Close <= 0)
            return;

        actualStartDate = startPrice.PriceDate;
        actualEndDate = endPrice.PriceDate;
        startPriceVal = startPrice.Close;
        endPriceVal = endPrice.Close;

        decimal ratio = endPrice.Close / startPrice.Close;
        totalReturnPct = Math.Round((ratio - 1m) * 100m, 2);
        currentValueOf1000 = Math.Round(1000m * ratio, 2);

        int days = endPrice.PriceDate.DayNumber - startPrice.PriceDate.DayNumber;
        if (days > 0) {
            double annualized = (Math.Pow((double)ratio, 365.25 / days) - 1.0) * 100.0;
            if (double.IsFinite(annualized))
                annualizedReturnPct = Math.Round((decimal)annualized, 2);
        }
    }

    private static void SortInPlace(List<CompanyScoreReturnSummary> items,
        ReturnsReportSortBy sortBy, SortDirection sortDir) {

        items.Sort((a, b) => {
            int cmp = sortBy switch {
                ReturnsReportSortBy.TotalReturnPct => CompareNullable(a.TotalReturnPct, b.TotalReturnPct),
                ReturnsReportSortBy.AnnualizedReturnPct => CompareNullable(a.AnnualizedReturnPct, b.AnnualizedReturnPct),
                ReturnsReportSortBy.CurrentValueOf1000 => CompareNullable(a.CurrentValueOf1000, b.CurrentValueOf1000),
                _ => a.OverallScore.CompareTo(b.OverallScore),
            };

            if (sortDir == SortDirection.Descending)
                cmp = -cmp;

            if (cmp == 0)
                cmp = a.CompanyId.CompareTo(b.CompanyId);

            return cmp;
        });
    }

    private static int CompareNullable(decimal? a, decimal? b) {
        if (!a.HasValue && !b.HasValue) return 0;
        if (!a.HasValue) return -1;
        if (!b.HasValue) return 1;
        return a.Value.CompareTo(b.Value);
    }

    private static Result<PagedResults<CompanyScoreReturnSummary>> Paginate(
        List<CompanyScoreReturnSummary> items, PaginationRequest pagination) {

        uint totalItems = (uint)items.Count;
        uint totalPages = totalItems == 0 ? 0 : (uint)Math.Ceiling(totalItems / (double)pagination.PageSize);
        int offset = (int)((pagination.PageNumber - 1) * pagination.PageSize);
        int limit = (int)pagination.PageSize;

        var page = new List<CompanyScoreReturnSummary>();
        for (int i = offset; i < items.Count && page.Count < limit; i++)
            page.Add(items[i]);

        var paginationResponse = new PaginationResponse(pagination.PageNumber, totalItems, totalPages);
        return Result<PagedResults<CompanyScoreReturnSummary>>.Success(
            new PagedResults<CompanyScoreReturnSummary>(page, paginationResponse));
    }
}
