using System;
using System.Collections.Generic;
using System.Linq;
using Stocks.DataModels.Enums;
using Stocks.DataModels.Scoring;
using Stocks.Persistence.Services;

namespace Stocks.EDGARScraper.Tests.Scoring;

/// <summary>E7: each 10-K report date gets its own annual year key.</summary>
public class FiscalYearKeyTests {
    private static ScoringConceptValue TenK(string date, decimal netIncome) =>
        new("NetIncomeLoss", netIncome, DateOnly.Parse(date), 1, (int)FilingType.TenK);

    private static List<int> Years(ScoringService.GroupedScoringData grouped) =>
        grouped.AnnualByYear.Keys.OrderBy(y => y).ToList();

    [Fact]
    public void TwoTenKsInOneCalendarYear_KeepBothAsSeparateYears() {
        // Fiscal-year change: June 2022 year end, then December 2022
        var values = new List<ScoringConceptValue> {
            TenK("2021-06-30", 10m),
            TenK("2022-06-30", 20m),
            TenK("2022-12-31", 30m),
        };

        ScoringService.GroupedScoringData grouped = ScoringService.GroupAndPartitionData(values);

        Assert.Equal([2020, 2021, 2022], Years(grouped));
        Assert.Equal(30m, grouped.AnnualByYear[2022]["NetIncomeLoss"]);
        Assert.Equal(20m, grouped.AnnualByYear[2021]["NetIncomeLoss"]);
        Assert.Equal(10m, grouped.AnnualByYear[2020]["NetIncomeLoss"]);
    }

    [Fact]
    public void FiftyTwoFiftyThreeWeekYearEnds_CascadeDown() {
        var values = new List<ScoringConceptValue> {
            TenK("2021-01-02", 1m),
            TenK("2022-01-01", 2m),
            TenK("2022-12-31", 3m),
        };

        ScoringService.GroupedScoringData grouped = ScoringService.GroupAndPartitionData(values);

        Assert.Equal([2020, 2021, 2022], Years(grouped));
        Assert.Equal(1m, grouped.AnnualByYear[2020]["NetIncomeLoss"]);
        Assert.Equal(2m, grouped.AnnualByYear[2021]["NetIncomeLoss"]);
        Assert.Equal(3m, grouped.AnnualByYear[2022]["NetIncomeLoss"]);
    }

    [Fact]
    public void GapYear_IsKept() {
        var values = new List<ScoringConceptValue> {
            TenK("2020-12-31", 1m),
            TenK("2022-12-31", 3m),
        };

        ScoringService.GroupedScoringData grouped = ScoringService.GroupAndPartitionData(values);

        Assert.Equal([2020, 2022], Years(grouped));
    }

    [Fact]
    public void OrdinaryDecemberYearEnds_Unchanged() {
        var values = new List<ScoringConceptValue> {
            TenK("2022-12-31", 1m),
            TenK("2023-12-31", 2m),
            TenK("2024-12-31", 3m),
        };

        ScoringService.GroupedScoringData grouped = ScoringService.GroupAndPartitionData(values);

        Assert.Equal([2022, 2023, 2024], Years(grouped));
    }

    [Fact]
    public void SameReportDate_SharesOneKeyAndKeepsFirstValue() {
        // A 10-K/A with the same report date
        var values = new List<ScoringConceptValue> {
            TenK("2023-12-31", 5m),
            TenK("2023-12-31", 6m),
            TenK("2024-12-31", 7m),
        };

        ScoringService.GroupedScoringData grouped = ScoringService.GroupAndPartitionData(values);

        Assert.Equal([2023, 2024], Years(grouped));
        Assert.Equal(5m, grouped.AnnualByYear[2023]["NetIncomeLoss"]);
    }
}
