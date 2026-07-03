using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Stocks.DataModels;
using Stocks.DataModels.Scoring;
using Stocks.Persistence.Database;
using Stocks.Persistence.Services;
using Stocks.Shared;
using Stocks.WebApi.Middleware;

namespace Stocks.WebApi.Endpoints;

public static class GrahamBacktestEndpoints {
    public static void MapGrahamBacktestEndpoints(this IEndpointRouteBuilder app) {
        _ = app.MapGet("/api/reports/graham-snapshot-dates",
            async (IDbmService dbm, CancellationToken ct) => {
                Result<IReadOnlyCollection<DateOnly>> result = await dbm.GetGrahamScoreSnapshotDates(ct);
                return result.ToHttpResult();
            });

        _ = app.MapGet("/api/reports/graham-snapshot",
            async (string? asOfDate,
                   uint? page, uint? pageSize,
                   string? sortBy, string? sortDir,
                   int? minScore, string? exchange,
                   InvestmentReturnReportService service,
                   CancellationToken ct) => {

                if (string.IsNullOrWhiteSpace(asOfDate) || !DateOnly.TryParse(asOfDate, out DateOnly asOf))
                    return Results.BadRequest(new { error = "asOfDate is required (YYYY-MM-DD)" });

                uint pageNum = page ?? 1;
                uint size = pageSize ?? 50;
                if (pageNum == 0) pageNum = 1;
                if (size == 0) size = 50;
                if (size > PaginationRequest.DefaultMaxPageSize)
                    size = PaginationRequest.DefaultMaxPageSize;

                ReturnsReportSortBy sort = ParseReturnsSortBy(sortBy);
                SortDirection direction = ParseSortDirection(sortDir);

                ReturnsReportFilter? filter = null;
                if (minScore.HasValue || !string.IsNullOrWhiteSpace(exchange))
                    filter = new ReturnsReportFilter(minScore, null, exchange, null);

                var pagination = new PaginationRequest(pageNum, size);

                Result<PagedResults<CompanyScoreReturnSummary>> result =
                    await service.GetGrahamSnapshotReturns(asOf, pagination, sort, direction, filter, ct);

                return result.ToHttpResult();
            });

        _ = app.MapGet("/api/reports/graham-backtest",
            async (int? minScore,
                   string? interval,
                   string? policy,
                   GrahamBacktestService service,
                   CancellationToken ct) => {

                int min = minScore ?? 15;
                if (min < 0) min = 0;
                if (min > 15) min = 15;

                GrahamBacktestInterval grid = string.Equals(interval, "weekly", StringComparison.OrdinalIgnoreCase)
                    ? GrahamBacktestInterval.Weekly
                    : GrahamBacktestInterval.Monthly;

                GrahamBacktestPolicy tradePolicy = GrahamBacktestPolicy.All;
                if (string.Equals(policy, "filing", StringComparison.OrdinalIgnoreCase))
                    tradePolicy = GrahamBacktestPolicy.FilingOnly;
                else if (string.Equals(policy, "price", StringComparison.OrdinalIgnoreCase))
                    tradePolicy = GrahamBacktestPolicy.PriceOnly;

                Result<GrahamBacktestReport> result = await service.GetBacktest(min, grid, tradePolicy, ct);
                return result.ToHttpResult();
            });
    }

    private static ReturnsReportSortBy ParseReturnsSortBy(string? value) {
        if (string.IsNullOrWhiteSpace(value))
            return ReturnsReportSortBy.OverallScore;

        if (string.Equals(value, "totalReturnPct", StringComparison.OrdinalIgnoreCase))
            return ReturnsReportSortBy.TotalReturnPct;
        if (string.Equals(value, "annualizedReturnPct", StringComparison.OrdinalIgnoreCase))
            return ReturnsReportSortBy.AnnualizedReturnPct;
        if (string.Equals(value, "currentValueOf1000", StringComparison.OrdinalIgnoreCase))
            return ReturnsReportSortBy.CurrentValueOf1000;

        return ReturnsReportSortBy.OverallScore;
    }

    private static SortDirection ParseSortDirection(string? value) {
        if (string.IsNullOrWhiteSpace(value))
            return SortDirection.Descending;

        if (string.Equals(value, "asc", StringComparison.OrdinalIgnoreCase))
            return SortDirection.Ascending;

        return SortDirection.Descending;
    }
}
