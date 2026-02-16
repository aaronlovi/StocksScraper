using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Stocks.DataModels;
using Stocks.Persistence.Database;
using Stocks.Shared;
using Stocks.WebApi.Middleware;

namespace Stocks.WebApi.Endpoints;

public static class SearchEndpoints {
    public static void MapSearchEndpoints(this IEndpointRouteBuilder app) {
        _ = app.MapGet("/api/search", async (string q, int page, int pageSize, IDbmService dbm, CancellationToken ct) => {
            if (string.IsNullOrWhiteSpace(q))
                return Results.BadRequest(new { error = "Query parameter 'q' is required" });

            uint pageNum = page > 0 ? (uint)page : 1;
            uint size = pageSize > 0 ? (uint)pageSize : 25;
            var pagination = new PaginationRequest(pageNum, size);

            Result<PagedResults<CompanySearchResult>> result =
                await dbm.SearchCompanies(q, pagination, ct);
            return result.ToHttpResult();
        });
    }
}
