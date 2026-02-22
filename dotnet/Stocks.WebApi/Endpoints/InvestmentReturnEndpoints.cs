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

public static class InvestmentReturnEndpoints {
    public static void MapInvestmentReturnEndpoints(this IEndpointRouteBuilder app) {
        _ = app.MapGet("/api/companies/{cik}/investment-return",
            async (string cik, string? startDate, IDbmService dbm,
                   InvestmentReturnService investmentReturnService, CancellationToken ct) => {
                Result<Company> companyResult = await dbm.GetCompanyByCik(cik, ct);
                if (companyResult.IsFailure)
                    return companyResult.ToHttpResult();

                Company company = companyResult.Value!;

                Result<IReadOnlyCollection<CompanyTicker>> tickersResult =
                    await dbm.GetCompanyTickersByCompanyId(company.CompanyId, ct);
                if (tickersResult.IsFailure)
                    return tickersResult.ToHttpResult();

                string? ticker = null;
                foreach (CompanyTicker companyTicker in tickersResult.Value!) {
                    ticker = companyTicker.Ticker;
                    break;
                }

                if (ticker is null)
                    return Results.NotFound(new { error = $"No ticker found for CIK {cik}" });

                DateOnly parsedStartDate;
                if (!string.IsNullOrWhiteSpace(startDate)) {
                    if (!DateOnly.TryParseExact(startDate, "yyyy-MM-dd", out parsedStartDate))
                        return Results.BadRequest(new { error = $"Invalid startDate format: {startDate}. Expected yyyy-MM-dd." });
                } else {
                    parsedStartDate = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-1);
                }

                Result<InvestmentReturnResult> returnResult =
                    await investmentReturnService.ComputeReturn(ticker, parsedStartDate, ct);
                if (returnResult.IsFailure)
                    return returnResult.ToHttpResult();

                InvestmentReturnResult result = returnResult.Value!;

                return Results.Ok(new {
                    ticker = result.Ticker,
                    startDate = result.StartDate.ToString("yyyy-MM-dd"),
                    endDate = result.EndDate.ToString("yyyy-MM-dd"),
                    startPrice = result.StartPrice,
                    endPrice = result.EndPrice,
                    totalReturnPct = result.TotalReturnPct,
                    annualizedReturnPct = result.AnnualizedReturnPct,
                    currentValueOf1000 = result.CurrentValueOf1000,
                });
            });
    }
}
