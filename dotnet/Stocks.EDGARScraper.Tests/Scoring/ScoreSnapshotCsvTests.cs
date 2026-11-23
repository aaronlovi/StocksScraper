using System;
using System.Collections.Generic;
using EDGARScraper.Services;
using Stocks.DataModels.Scoring;

namespace Stocks.EDGARScraper.Tests.Scoring;

public class ScoreSnapshotCsvTests {
    private static List<ScoringCheck> MakeChecks(int count, params ScoringCheckResult[] results) {
        var checks = new List<ScoringCheck>(count);
        for (int i = 1; i <= count; i++)
            checks.Add(new ScoringCheck(i, $"Check {i}", null, "", results[(i - 1) % results.Length]));
        return checks;
    }

    [Fact]
    public void ValueHeader_ListsIdentityChecksAndMetrics() {
        Assert.Equal(
            "company_id,ticker,score,computable,years,"
            + "c01,c02,c03,c04,c05,c06,c07,c08,c09,c10,c11,c12,c13,c14,c15,"
            + "book_value,market_cap,de,pb,db,adj_re,avg_ncf,avg_oe,roe_cf,roe_oe,ret_cf,ret_oe,max_buy,"
            + "sic_code,is_financial",
            ScoreSnapshotCsv.ValueHeader());
    }

    [Fact]
    public void MoatHeader_ListsIdentityChecksAndMetrics() {
        Assert.Equal(
            "company_id,ticker,score,computable,years,"
            + "c01,c02,c03,c04,c05,c06,c07,c08,c09,c10,c11,c12,c13,"
            + "roe_cf,roe_oe,capex_ratio,interest_cov,de,est_ret_oe,gm,om,cagr,"
            + "sic_code,is_financial",
            ScoreSnapshotCsv.MoatHeader());
    }

    [Fact]
    public void FormatValueRow_WritesChecksAsPfnAndNullsAsEmpty() {
        var summary = new CompanyScoreSummary(
            36, "123", "Name, Inc", null, null, 6, 10, 3,
            -2070069m, null, 0m, null, 0m, -10784882m, 2313112.6666666667m, 8974816.333333333m,
            18.4386334m, 63.933803m, null, null,
            null, null, null, 0m, -0.7199734m, null, DateTime.UnixEpoch);
        List<ScoringCheck> checks = MakeChecks(15,
            ScoringCheckResult.Pass, ScoringCheckResult.Fail, ScoringCheckResult.NotAvailable);

        string row = ScoreSnapshotCsv.FormatValueRow(new CompanyScoreDetail(summary, checks));

        Assert.Equal(
            "36,,6,10,3,P,F,N,P,F,N,P,F,N,P,F,N,P,F,N,"
            + "-2070069,,0,,0,-10784882,2313112.666667,8974816.333333,18.438633,63.933803,,,-0.719973,,0",
            row);
    }

    [Fact]
    public void FormatMoatRow_WritesTickerAndMetrics() {
        var summary = new CompanyMoatScoreSummary(
            150, "456", null, "ABC", "NYSE", 6, 11, 2,
            79.951081m, -177.50491m, 8570.217586m, -5.174459m, null, 32.303493m,
            1.962649m, -25.16948m, 0m, 10m, null, 100, DateTime.UnixEpoch);
        List<ScoringCheck> checks = MakeChecks(13, ScoringCheckResult.Pass);

        string row = ScoreSnapshotCsv.FormatMoatRow(new CompanyMoatScoreDetail(summary, checks));

        Assert.Equal(
            "150,ABC,6,11,2,P,P,P,P,P,P,P,P,P,P,P,P,P,"
            + "8570.217586,-5.174459,1.962649,-25.16948,0,,79.951081,-177.50491,32.303493,,0",
            row);
    }

    [Fact]
    public void FormatValueRow_QuotesTickerContainingComma() {
        var summary = new CompanyScoreSummary(
            1, "1", null, "A,B", null, 0, 0, 1,
            null, null, null, null, null, null, null, null, null, null, null, null,
            null, null, null, null, null, null, DateTime.UnixEpoch);

        string row = ScoreSnapshotCsv.FormatValueRow(new CompanyScoreDetail(summary, []));

        Assert.StartsWith("1,\"A,B\",0,0,1,,,", row);
    }

    [Fact]
    public void FormatRows_WriteSicCodeAndFinancialFlag() {
        CompanyScoreSummary bank = new CompanyScoreSummary(
            2, "70858", null, "BAC", null, 0, 0, 1,
            null, null, null, null, null, null, null, null, null, null, null, null,
            null, null, null, null, null, null, DateTime.UnixEpoch) with { SicCode = 6021 };
        CompanyMoatScoreSummary apple = new CompanyMoatScoreSummary(
            1, "320193", null, "AAPL", null, 0, 0, 1,
            null, null, null, null, null, null, null, null, null, null, null, null, DateTime.UnixEpoch) with { SicCode = 3571 };

        Assert.EndsWith(",6021,1", ScoreSnapshotCsv.FormatValueRow(new CompanyScoreDetail(bank, [])));
        Assert.EndsWith(",3571,0", ScoreSnapshotCsv.FormatMoatRow(new CompanyMoatScoreDetail(apple, [])));
    }
}
