using System;
using System.Collections.Generic;
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

public static class CompanyEndpoints {
    public static void MapCompanyEndpoints(this IEndpointRouteBuilder app) {
        _ = app.MapGet("/api/companies/{cik}", async (string cik, IDbmService dbm, CancellationToken ct) => {
            Result<Company> companyResult = await dbm.GetCompanyByCik(cik, ct);
            if (companyResult.IsFailure)
                return companyResult.ToHttpResult();

            Company company = companyResult.Value!;
            Result<IReadOnlyCollection<CompanyTicker>> tickersResult =
                await dbm.GetCompanyTickersByCompanyId(company.CompanyId, ct);
            Result<IReadOnlyCollection<CompanyName>> namesResult =
                await dbm.GetCompanyNamesByCompanyId(company.CompanyId, ct);

            var tickers = new List<object>();
            string? firstTicker = null;
            if (tickersResult.IsSuccess && tickersResult.Value is not null) {
                foreach (CompanyTicker t in tickersResult.Value) {
                    tickers.Add(new { t.Ticker, t.Exchange });
                    firstTicker ??= t.Ticker;
                }
            }

            string? companyName = null;
            if (namesResult.IsSuccess && namesResult.Value is not null) {
                foreach (CompanyName n in namesResult.Value) {
                    companyName = n.Name;
                    break;
                }
            }

            decimal? latestPrice = null;
            string? latestPriceDate = null;
            if (firstTicker is not null) {
                Result<IReadOnlyCollection<PriceRow>> pricesResult =
                    await dbm.GetPricesByTicker(firstTicker, ct);
                if (pricesResult.IsSuccess && pricesResult.Value is not null) {
                    DateOnly maxDate = DateOnly.MinValue;
                    foreach (PriceRow price in pricesResult.Value) {
                        if (price.PriceDate > maxDate) {
                            maxDate = price.PriceDate;
                            latestPrice = price.Close;
                            latestPriceDate = price.PriceDate.ToString("yyyy-MM-dd");
                        }
                    }
                }
            }

            return Results.Ok(new {
                company.CompanyId,
                company.Cik,
                company.DataSource,
                CompanyName = companyName,
                LatestPrice = latestPrice,
                LatestPriceDate = latestPriceDate,
                Tickers = tickers
            });
        });

        _ = app.MapGet("/api/companies/{cik}/ar-revenue",
            async (string cik, IDbmService dbm, CancellationToken ct) => {
                Result<Company> companyResult = await dbm.GetCompanyByCik(cik, ct);
                if (companyResult.IsFailure)
                    return companyResult.ToHttpResult();

                Company company = companyResult.Value!;

                Result<IReadOnlyCollection<ScoringConceptValue>> dataResult =
                    await dbm.GetScoringDataPoints(company.CompanyId, ArRevenueConceptNames, ct);
                if (dataResult.IsFailure)
                    return dataResult.ToHttpResult();

                List<object> rows = ResolveArRevenueByYear(dataResult.Value!);
                return Results.Ok(rows);
            });
    }

    internal static readonly string[] ArRevenueConceptNames = [
        "AccountsReceivableNetCurrent",
        "AccountsReceivableNet",
        "ReceivablesNetCurrent",
        "AccountsAndOtherReceivablesNetCurrent",
        "Revenues",
        "RevenueFromContractWithCustomerExcludingAssessedTax",
        "SalesRevenueNet",
        "RevenueFromContractWithCustomerIncludingAssessedTax",
        "SalesRevenueGoodsNet",
        "SalesRevenueServicesNet",
    ];

    private static readonly string[] ArFallbackChain = [
        "AccountsReceivableNetCurrent",
        "AccountsReceivableNet",
        "ReceivablesNetCurrent",
        "AccountsAndOtherReceivablesNetCurrent",
    ];

    private static readonly string[] RevenueFallbackChain = [
        "Revenues",
        "RevenueFromContractWithCustomerExcludingAssessedTax",
        "SalesRevenueNet",
        "RevenueFromContractWithCustomerIncludingAssessedTax",
    ];

    internal static List<object> ResolveArRevenueByYear(IReadOnlyCollection<ScoringConceptValue> values) {
        // Group values by year
        var byYear = new Dictionary<int, Dictionary<string, decimal>>();
        foreach (ScoringConceptValue v in values) {
            int year = v.ReportDate.Year;
            if (!byYear.TryGetValue(year, out Dictionary<string, decimal>? yearData)) {
                yearData = new Dictionary<string, decimal>(StringComparer.Ordinal);
                byYear[year] = yearData;
            }
            // Keep first occurrence per concept per year (data is ordered by report_date DESC)
            if (!yearData.ContainsKey(v.ConceptName))
                yearData[v.ConceptName] = v.Value;
        }

        // Build result rows sorted by year descending
        var sortedYears = new List<int>(byYear.Keys);
        sortedYears.Sort((a, b) => b.CompareTo(a));

        var rows = new List<object>();
        foreach (int year in sortedYears) {
            Dictionary<string, decimal> yearData = byYear[year];
            ResolveArRevenue(yearData, out decimal? ar, out string? arConcept,
                out decimal? revenue, out string? revenueConcept);

            decimal? ratio = null;
            if (ar.HasValue && revenue.HasValue && revenue.Value != 0m)
                ratio = ar.Value / revenue.Value;

            rows.Add(new {
                year,
                accountsReceivable = ar,
                revenue,
                ratio,
                arConceptUsed = arConcept,
                revenueConceptUsed = revenueConcept,
            });
        }

        return rows;
    }

    internal static void ResolveArRevenue(
        IReadOnlyDictionary<string, decimal> yearData,
        out decimal? ar, out string? arConcept,
        out decimal? revenue, out string? revenueConcept) {

        ar = null;
        arConcept = null;
        foreach (string concept in ArFallbackChain) {
            if (yearData.TryGetValue(concept, out decimal value)) {
                ar = value;
                arConcept = concept;
                break;
            }
        }

        revenue = null;
        revenueConcept = null;
        foreach (string concept in RevenueFallbackChain) {
            if (yearData.TryGetValue(concept, out decimal value)) {
                revenue = value;
                revenueConcept = concept;
                return;
            }
        }

        // Final fallback: sum SalesRevenueGoodsNet + SalesRevenueServicesNet
        bool hasGoods = yearData.TryGetValue("SalesRevenueGoodsNet", out decimal goods);
        bool hasServices = yearData.TryGetValue("SalesRevenueServicesNet", out decimal services);
        if (hasGoods || hasServices) {
            revenue = (hasGoods ? goods : 0m) + (hasServices ? services : 0m);
            revenueConcept = "SalesRevenueGoodsNet+SalesRevenueServicesNet";
        }
    }
}
