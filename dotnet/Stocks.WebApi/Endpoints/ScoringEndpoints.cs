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

public static class ScoringEndpoints {
    public static void MapScoringEndpoints(this IEndpointRouteBuilder app) {
        _ = app.MapGet("/api/companies/{cik}/scoring",
            async (string cik, IDbmService dbm, ScoringService scoringService, CancellationToken ct) => {
                Result<Company> companyResult = await dbm.GetCompanyByCik(cik, ct);
                if (companyResult.IsFailure)
                    return companyResult.ToHttpResult();

                Company company = companyResult.Value!;

                Result<ScoringResult> scoringResult =
                    await scoringService.ComputeScore(company.CompanyId, ct);
                if (scoringResult.IsFailure)
                    return scoringResult.ToHttpResult();

                ScoringResult result = scoringResult.Value!;

                // Build rawDataByYear with string keys for JSON
                var rawDataByYear = new Dictionary<string, Dictionary<string, decimal>>();
                foreach (KeyValuePair<int, IReadOnlyDictionary<string, decimal>> yearEntry in result.RawDataByYear) {
                    var yearData = new Dictionary<string, decimal>();
                    foreach (KeyValuePair<string, decimal> conceptEntry in yearEntry.Value)
                        yearData[conceptEntry.Key] = conceptEntry.Value;
                    rawDataByYear[yearEntry.Key.ToString()] = yearData;
                }

                // Build scorecard array
                var scorecard = new List<object>();
                foreach (ScoringCheck check in result.Scorecard) {
                    string resultStr = check.Result switch {
                        ScoringCheckResult.Pass => "pass",
                        ScoringCheckResult.Fail => "fail",
                        _ => "na",
                    };
                    scorecard.Add(new {
                        checkNumber = check.CheckNumber,
                        name = check.Name,
                        computedValue = check.ComputedValue,
                        threshold = check.Threshold,
                        result = resultStr,
                    });
                }

                return Results.Ok(new {
                    rawDataByYear,
                    metrics = new {
                        bookValue = result.Metrics.BookValue,
                        marketCap = result.Metrics.MarketCap,
                        debtToEquityRatio = result.Metrics.DebtToEquityRatio,
                        priceToBookRatio = result.Metrics.PriceToBookRatio,
                        debtToBookRatio = result.Metrics.DebtToBookRatio,
                        adjustedRetainedEarnings = result.Metrics.AdjustedRetainedEarnings,
                        oldestRetainedEarnings = result.Metrics.OldestRetainedEarnings,
                        averageNetCashFlow = result.Metrics.AverageNetCashFlow,
                        averageOwnerEarnings = result.Metrics.AverageOwnerEarnings,
                        estimatedReturnCF = result.Metrics.EstimatedReturnCF,
                        estimatedReturnOE = result.Metrics.EstimatedReturnOE,
                        currentDividendsPaid = result.Metrics.CurrentDividendsPaid,
                    },
                    scorecard,
                    overallScore = result.OverallScore,
                    computableChecks = result.ComputableChecks,
                    yearsOfData = result.YearsOfData,
                    pricePerShare = result.PricePerShare,
                    priceDate = result.PriceDate?.ToString("yyyy-MM-dd"),
                    sharesOutstanding = result.SharesOutstanding,
                });
            });
    }
}
