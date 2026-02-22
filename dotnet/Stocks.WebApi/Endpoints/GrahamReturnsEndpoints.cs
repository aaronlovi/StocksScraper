using System;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Stocks.DataModels;
using Stocks.DataModels.Scoring;
using Stocks.Persistence.Services;
using Stocks.Shared;
using Stocks.WebApi.Middleware;

namespace Stocks.WebApi.Endpoints;

public static class GrahamReturnsEndpoints {
    public static void MapGrahamReturnsEndpoints(this IEndpointRouteBuilder app) {
        _ = app.MapGet("/api/reports/graham-returns",
            async (string? startDate,
                   uint? page, uint? pageSize,
                   string? sortBy, string? sortDir,
                   int? minScore, string? exchange,
                   InvestmentReturnReportService service,
                   CancellationToken ct) => {

                DateOnly start = ParseStartDate(startDate);

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
                    await service.GetGrahamReturns(start, pagination, sort, direction, filter, ct);

                return result.ToHttpResult();
            });
    }

    private static DateOnly ParseStartDate(string? value) {
        if (!string.IsNullOrWhiteSpace(value) && DateOnly.TryParse(value, out DateOnly parsed))
            return parsed;
        return DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-1);
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
