using System;
using System.Reflection;
using Stocks.Persistence.Database.Statements;

namespace Stocks.EDGARScraper.Tests.Scoring;

/// <summary>
/// H4: the full-table price scan behind the as-of scores took longer than the default 30 s
/// under load and was cancelled, which failed whole backtest dates.
/// </summary>
public class StatementTimeoutTests {
    private static int TimeoutOf(object statement) {
        PropertyInfo property = statement.GetType().GetProperty(
            "CommandTimeoutSeconds", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (int)property.GetValue(statement)!;
    }

    [Fact]
    public void GetAllPricesNearDate_AllowsAsLongAsTheScoringFetch() {
        Assert.Equal(600, TimeoutOf(new GetAllPricesNearDateStmt(new DateOnly(2025, 5, 31))));
        Assert.Equal(600, TimeoutOf(new GetAllScoringDataPointsStmt(["Assets"])));
    }

    [Fact]
    public void OtherStatements_KeepTheDefault() {
        Assert.Equal(30, TimeoutOf(new GetScoringDataPointsStmt(1, ["Assets"])));
    }
}
