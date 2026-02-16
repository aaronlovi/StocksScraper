using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Stocks.DataModels;
using Stocks.Persistence.Database;
using Stocks.Shared;
using Stocks.WebApi.Middleware;

namespace Stocks.WebApi.Endpoints;

public static class DashboardEndpoints {
    public static void MapDashboardEndpoints(this IEndpointRouteBuilder app) {
        _ = app.MapGet("/api/dashboard/stats", async (IDbmService dbm, CancellationToken ct) => {
            Result<DashboardStats> result = await dbm.GetDashboardStats(ct);
            return result.ToHttpResult();
        });
    }
}
