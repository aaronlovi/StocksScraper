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

public static class MoatReportEndpoints {
    public static void MapMoatReportEndpoints(this IEndpointRouteBuilder app) {
        _ = app.MapGet("/api/reports/moat-scores",
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

                MoatScoresSortBy sort = ParseMoatSortBy(sortBy);
                SortDirection direction = ParseSortDirection(sortDir);

                ScoresFilter? filter = null;
                if (minScore.HasValue || maxScore.HasValue || !string.IsNullOrWhiteSpace(exchange))
                    filter = new ScoresFilter(minScore, maxScore, exchange);

                var pagination = new PaginationRequest(pageNum, size);

                Result<PagedResults<CompanyMoatScoreSummary>> result =
                    await dbm.GetCompanyMoatScores(pagination, sort, direction, filter, ct);

                return result.ToHttpResult();
            });
    }

    private static MoatScoresSortBy ParseMoatSortBy(string? value) {
        if (string.IsNullOrWhiteSpace(value))
            return MoatScoresSortBy.OverallScore;

        if (string.Equals(value, "averageGrossMargin", StringComparison.OrdinalIgnoreCase))
            return MoatScoresSortBy.AverageGrossMargin;
        if (string.Equals(value, "averageOperatingMargin", StringComparison.OrdinalIgnoreCase))
            return MoatScoresSortBy.AverageOperatingMargin;
        if (string.Equals(value, "averageRoeCF", StringComparison.OrdinalIgnoreCase))
            return MoatScoresSortBy.AverageRoeCF;
        if (string.Equals(value, "averageRoeOE", StringComparison.OrdinalIgnoreCase))
            return MoatScoresSortBy.AverageRoeOE;
        if (string.Equals(value, "estimatedReturnOE", StringComparison.OrdinalIgnoreCase))
            return MoatScoresSortBy.EstimatedReturnOE;
        if (string.Equals(value, "revenueCagr", StringComparison.OrdinalIgnoreCase))
            return MoatScoresSortBy.RevenueCagr;
        if (string.Equals(value, "capexRatio", StringComparison.OrdinalIgnoreCase))
            return MoatScoresSortBy.CapexRatio;
        if (string.Equals(value, "interestCoverage", StringComparison.OrdinalIgnoreCase))
            return MoatScoresSortBy.InterestCoverage;
        if (string.Equals(value, "debtToEquityRatio", StringComparison.OrdinalIgnoreCase))
            return MoatScoresSortBy.DebtToEquityRatio;
        if (string.Equals(value, "return1y", StringComparison.OrdinalIgnoreCase))
            return MoatScoresSortBy.Return1y;

        return MoatScoresSortBy.OverallScore;
    }

    private static SortDirection ParseSortDirection(string? value) {
        if (string.IsNullOrWhiteSpace(value))
            return SortDirection.Descending;

        if (string.Equals(value, "asc", StringComparison.OrdinalIgnoreCase))
            return SortDirection.Ascending;

        return SortDirection.Descending;
    }
}
