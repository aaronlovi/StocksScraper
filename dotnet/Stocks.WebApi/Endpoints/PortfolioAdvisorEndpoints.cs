using System.Collections.Generic;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Stocks.DataModels.Scoring;
using Stocks.Persistence.Services;
using Stocks.Shared;
using Stocks.WebApi.Middleware;

namespace Stocks.WebApi.Endpoints;

public record PortfolioAdvisorRequest(List<string> Tickers);

public static class PortfolioAdvisorEndpoints {
    private const int MaxTickers = 500;

    public static void MapPortfolioAdvisorEndpoints(this IEndpointRouteBuilder app) {
        _ = app.MapPost("/api/reports/portfolio-advisor",
            async (PortfolioAdvisorRequest request,
                   PortfolioAdvisorService service,
                   CancellationToken ct) => {

                if (request.Tickers is null)
                    return Results.BadRequest(new { error = "tickers is required" });
                if (request.Tickers.Count > MaxTickers)
                    return Results.BadRequest(new { error = $"At most {MaxTickers} tickers are supported." });

                Result<PortfolioAdvisorReport> result = await service.GetRecommendations(request.Tickers, ct);
                return result.ToHttpResult();
            });
    }
}
