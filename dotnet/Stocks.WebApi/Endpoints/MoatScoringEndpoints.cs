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

public static class MoatScoringEndpoints {
    public static void MapMoatScoringEndpoints(this IEndpointRouteBuilder app) {
        _ = app.MapGet("/api/companies/{cik}/moat-scoring",
            async (string cik, IDbmService dbm, MoatScoringService moatScoringService, CancellationToken ct) => {
                Result<Company> companyResult = await dbm.GetCompanyByCik(cik, ct);
                if (companyResult.IsFailure)
                    return companyResult.ToHttpResult();

                Company company = companyResult.Value!;

                Result<MoatScoringResult> scoringResult =
                    await moatScoringService.ComputeScore(company.CompanyId, ct);
                if (scoringResult.IsFailure)
                    return scoringResult.ToHttpResult();

                MoatScoringResult result = scoringResult.Value!;

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

                // Build trendData array
                var trendData = new List<object>();
                foreach (MoatYearMetrics yearMetrics in result.TrendData) {
                    trendData.Add(new {
                        year = yearMetrics.Year,
                        grossMarginPct = yearMetrics.GrossMarginPct,
                        operatingMarginPct = yearMetrics.OperatingMarginPct,
                        roeCfPct = yearMetrics.RoeCfPct,
                        roeOePct = yearMetrics.RoeOePct,
                        revenue = yearMetrics.Revenue,
                    });
                }

                return Results.Ok(new {
                    rawDataByYear,
                    metrics = new {
                        averageGrossMargin = result.Metrics.AverageGrossMargin,
                        averageOperatingMargin = result.Metrics.AverageOperatingMargin,
                        averageRoeCF = result.Metrics.AverageRoeCF,
                        averageRoeOE = result.Metrics.AverageRoeOE,
                        revenueCagr = result.Metrics.RevenueCagr,
                        capexRatio = result.Metrics.CapexRatio,
                        interestCoverage = result.Metrics.InterestCoverage,
                        debtToEquityRatio = result.Metrics.DebtToEquityRatio,
                        estimatedReturnOE = result.Metrics.EstimatedReturnOE,
                        currentDividendsPaid = result.Metrics.CurrentDividendsPaid,
                        marketCap = result.Metrics.MarketCap,
                        pricePerShare = result.Metrics.PricePerShare,
                        positiveOeYears = result.Metrics.PositiveOeYears,
                        totalOeYears = result.Metrics.TotalOeYears,
                        capitalReturnYears = result.Metrics.CapitalReturnYears,
                        totalCapitalReturnYears = result.Metrics.TotalCapitalReturnYears,
                    },
                    scorecard,
                    trendData,
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
