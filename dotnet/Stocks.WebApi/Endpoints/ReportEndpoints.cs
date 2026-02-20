using System;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Stocks.DataModels;
using Stocks.DataModels.Scoring;
using Stocks.Persistence.Database;
using Stocks.Shared;
using Stocks.WebApi.Middleware;

namespace Stocks.WebApi.Endpoints;

public static class ReportEndpoints {
    public static void MapReportEndpoints(this IEndpointRouteBuilder app) {
        _ = app.MapGet("/api/reports/scores",
            async (uint? page, uint? pageSize,
                   string? sortBy, string? sortDir,
                   int? minScore, int? maxScore, string? exchange,
                   IDbmService dbm, CancellationToken ct) => {

                uint pageNum = page ?? 1;
                uint size = pageSize ?? 50;
                if (pageNum == 0) pageNum = 1;
                if (size == 0) size = 50;
                if (size > PaginationRequest.DefaultMaxPageSize)
                    size = PaginationRequest.DefaultMaxPageSize;

                ScoresSortBy sort = ParseSortBy(sortBy);
                SortDirection direction = ParseSortDirection(sortDir);

                ScoresFilter? filter = null;
                if (minScore.HasValue || maxScore.HasValue || !string.IsNullOrWhiteSpace(exchange))
                    filter = new ScoresFilter(minScore, maxScore, exchange);

                var pagination = new PaginationRequest(pageNum, size);

                Result<PagedResults<CompanyScoreSummary>> result =
                    await dbm.GetCompanyScores(pagination, sort, direction, filter, ct);

                return result.ToHttpResult();
            });
    }

    private static ScoresSortBy ParseSortBy(string? value) {
        if (string.IsNullOrWhiteSpace(value))
            return ScoresSortBy.OverallScore;

        if (string.Equals(value, "bookValue", StringComparison.OrdinalIgnoreCase))
            return ScoresSortBy.BookValue;
        if (string.Equals(value, "marketCap", StringComparison.OrdinalIgnoreCase))
            return ScoresSortBy.MarketCap;
        if (string.Equals(value, "estimatedReturnCF", StringComparison.OrdinalIgnoreCase))
            return ScoresSortBy.EstimatedReturnCF;
        if (string.Equals(value, "estimatedReturnOE", StringComparison.OrdinalIgnoreCase))
            return ScoresSortBy.EstimatedReturnOE;
        if (string.Equals(value, "debtToEquityRatio", StringComparison.OrdinalIgnoreCase))
            return ScoresSortBy.DebtToEquityRatio;
        if (string.Equals(value, "priceToBookRatio", StringComparison.OrdinalIgnoreCase))
            return ScoresSortBy.PriceToBookRatio;

        return ScoresSortBy.OverallScore;
    }

    private static SortDirection ParseSortDirection(string? value) {
        if (string.IsNullOrWhiteSpace(value))
            return SortDirection.Descending;

        if (string.Equals(value, "asc", StringComparison.OrdinalIgnoreCase))
            return SortDirection.Ascending;

        return SortDirection.Descending;
    }
}
