using System.Collections.Generic;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Stocks.DataModels;
using Stocks.Persistence.Database;
using Stocks.Shared;
using Stocks.WebApi.Middleware;

namespace Stocks.WebApi.Endpoints;

public static class CompanyEndpoints {
    public static void MapCompanyEndpoints(this IEndpointRouteBuilder app) {
        _ = app.MapGet("/api/companies/{cik}", async (string cik, IDbmService dbm, CancellationToken ct) => {
            Result<Company> companyResult = await dbm.GetCompanyByCik(cik, ct);
            if (companyResult.IsFailure)
                return companyResult.ToHttpResult();

            Company company = companyResult.Value!;
            Result<IReadOnlyCollection<CompanyTicker>> tickersResult =
                await dbm.GetCompanyTickersByCompanyId(company.CompanyId, ct);

            var tickers = new List<object>();
            if (tickersResult.IsSuccess && tickersResult.Value is not null) {
                foreach (CompanyTicker t in tickersResult.Value)
                    tickers.Add(new { t.Ticker, t.Exchange });
            }

            return Results.Ok(new {
                company.CompanyId,
                company.Cik,
                company.DataSource,
                Tickers = tickers
            });
        });
    }
}
