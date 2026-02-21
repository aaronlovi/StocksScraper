using System;
using System.Collections.Generic;
using Stocks.DataModels.Scoring;
using Stocks.WebApi.Endpoints;

namespace Stocks.EDGARScraper.Tests.Scoring;

public class ArRevenueTests {

    [Fact]
    public void ResolveArRevenue_PicksFirstArConceptInPriorityOrder() {
        var data = new Dictionary<string, decimal> {
            ["AccountsReceivableNet"] = 200m,
            ["AccountsReceivableNetCurrent"] = 100m,
            ["Revenues"] = 1000m,
        };

        CompanyEndpoints.ResolveArRevenue(data,
            out decimal? ar, out string? arConcept,
            out decimal? revenue, out string? revenueConcept);

        Assert.Equal(100m, ar);
        Assert.Equal("AccountsReceivableNetCurrent", arConcept);
        Assert.Equal(1000m, revenue);
        Assert.Equal("Revenues", revenueConcept);
    }

    [Fact]
    public void ResolveArRevenue_FallsBackToLaterArConcept() {
        var data = new Dictionary<string, decimal> {
            ["ReceivablesNetCurrent"] = 300m,
            ["Revenues"] = 1000m,
        };

        CompanyEndpoints.ResolveArRevenue(data,
            out decimal? ar, out string? arConcept,
            out _, out _);

        Assert.Equal(300m, ar);
        Assert.Equal("ReceivablesNetCurrent", arConcept);
    }

    [Fact]
    public void ResolveArRevenue_PicksFirstRevenueConceptInPriorityOrder() {
        var data = new Dictionary<string, decimal> {
            ["AccountsReceivableNetCurrent"] = 100m,
            ["RevenueFromContractWithCustomerExcludingAssessedTax"] = 900m,
            ["SalesRevenueNet"] = 800m,
        };

        CompanyEndpoints.ResolveArRevenue(data,
            out _, out _,
            out decimal? revenue, out string? revenueConcept);

        Assert.Equal(900m, revenue);
        Assert.Equal("RevenueFromContractWithCustomerExcludingAssessedTax", revenueConcept);
    }

    [Fact]
    public void ResolveArRevenue_SumsSalesRevenueGoodsAndServicesAsFinalFallback() {
        var data = new Dictionary<string, decimal> {
            ["AccountsReceivableNetCurrent"] = 100m,
            ["SalesRevenueGoodsNet"] = 600m,
            ["SalesRevenueServicesNet"] = 400m,
        };

        CompanyEndpoints.ResolveArRevenue(data,
            out _, out _,
            out decimal? revenue, out string? revenueConcept);

        Assert.Equal(1000m, revenue);
        Assert.Equal("SalesRevenueGoodsNet+SalesRevenueServicesNet", revenueConcept);
    }

    [Fact]
    public void ResolveArRevenue_SalesRevenueGoodsOnlyWhenServicesAbsent() {
        var data = new Dictionary<string, decimal> {
            ["AccountsReceivableNetCurrent"] = 100m,
            ["SalesRevenueGoodsNet"] = 600m,
        };

        CompanyEndpoints.ResolveArRevenue(data,
            out _, out _,
            out decimal? revenue, out string? revenueConcept);

        Assert.Equal(600m, revenue);
        Assert.Equal("SalesRevenueGoodsNet+SalesRevenueServicesNet", revenueConcept);
    }

    [Fact]
    public void ResolveArRevenue_ReturnsNullWhenArMissing() {
        var data = new Dictionary<string, decimal> {
            ["Revenues"] = 1000m,
        };

        CompanyEndpoints.ResolveArRevenue(data,
            out decimal? ar, out string? arConcept,
            out _, out _);

        Assert.Null(ar);
        Assert.Null(arConcept);
    }

    [Fact]
    public void ResolveArRevenue_ReturnsNullWhenRevenueMissing() {
        var data = new Dictionary<string, decimal> {
            ["AccountsReceivableNetCurrent"] = 100m,
        };

        CompanyEndpoints.ResolveArRevenue(data,
            out _, out _,
            out decimal? revenue, out string? revenueConcept);

        Assert.Null(revenue);
        Assert.Null(revenueConcept);
    }

    [Fact]
    public void ResolveArRevenueByYear_ComputesCorrectRatio() {
        var values = new List<ScoringConceptValue> {
            new("AccountsReceivableNetCurrent", 200m, new DateOnly(2023, 12, 31), 2, 1),
            new("Revenues", 1000m, new DateOnly(2023, 12, 31), 1, 1),
        };

        List<object> rows = CompanyEndpoints.ResolveArRevenueByYear(values);

        Assert.Single(rows);
        // Use dynamic to inspect anonymous type
        dynamic row = rows[0];
        Assert.Equal(2023, row.year);
        Assert.Equal(200m, row.accountsReceivable);
        Assert.Equal(1000m, row.revenue);
        Assert.Equal(0.2m, row.ratio);
    }

    [Fact]
    public void ResolveArRevenueByYear_NullRatioWhenRevenueMissing() {
        var values = new List<ScoringConceptValue> {
            new("AccountsReceivableNetCurrent", 200m, new DateOnly(2023, 12, 31), 2, 1),
        };

        List<object> rows = CompanyEndpoints.ResolveArRevenueByYear(values);

        Assert.Single(rows);
        dynamic row = rows[0];
        Assert.Null(row.ratio);
    }

    [Fact]
    public void ResolveArRevenueByYear_MultipleYearsSortedDescending() {
        var values = new List<ScoringConceptValue> {
            new("AccountsReceivableNetCurrent", 100m, new DateOnly(2021, 12, 31), 2, 1),
            new("Revenues", 500m, new DateOnly(2021, 12, 31), 1, 1),
            new("AccountsReceivableNetCurrent", 200m, new DateOnly(2023, 12, 31), 2, 1),
            new("Revenues", 1000m, new DateOnly(2023, 12, 31), 1, 1),
            new("AccountsReceivableNetCurrent", 150m, new DateOnly(2022, 12, 31), 2, 1),
            new("Revenues", 750m, new DateOnly(2022, 12, 31), 1, 1),
        };

        List<object> rows = CompanyEndpoints.ResolveArRevenueByYear(values);

        Assert.Equal(3, rows.Count);
        dynamic first = rows[0];
        dynamic second = rows[1];
        dynamic third = rows[2];
        Assert.Equal(2023, first.year);
        Assert.Equal(2022, second.year);
        Assert.Equal(2021, third.year);
    }
}
