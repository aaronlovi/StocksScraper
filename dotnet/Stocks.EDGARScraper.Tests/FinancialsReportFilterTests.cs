using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Stocks.DataModels;
using Stocks.DataModels.Scoring;
using Stocks.Persistence.Database;
using Stocks.Shared;

namespace Stocks.EDGARScraper.Tests;

public class FinancialsReportFilterTests {
    private readonly DbmInMemoryService _dbm = new();
    private readonly CancellationToken _ct = CancellationToken.None;
    private static readonly DateTime Now = DateTime.UtcNow;

    // 1 Apple (3571), 2 Bank of America (6021), 3 a REIT (6798), 4 no SIC
    private async Task Seed() {
        var value = new List<CompanyScoreSummary>();
        var moat = new List<CompanyMoatScoreSummary>();
        for (ulong id = 1; id <= 4; id++) {
            value.Add(new CompanyScoreSummary(id, id.ToString(), $"Co {id}", $"T{id}", "NYSE", 10 + (int)id, 15, 5,
                null, null, null, null, null, null, null, null, null, null, null, null,
                null, null, null, null, null, null, Now));
            moat.Add(new CompanyMoatScoreSummary(id, id.ToString(), $"Co {id}", $"T{id}", "NYSE", 8 + (int)id, 13, 5,
                null, null, null, null, null, null, null, null, null, null, null, null, Now));
        }
        _ = await _dbm.BulkInsertCompanyScores(value, _ct);
        _ = await _dbm.BulkInsertCompanyMoatScores(moat, _ct);
        _ = await _dbm.UpdateCompanySic([
            new CompanySic(1, 3571, "Electronic Computers"),
            new CompanySic(2, 6021, "National Commercial Banks"),
            new CompanySic(3, 6798, "Real Estate Investment Trusts"),
        ], _ct);
    }

    private async Task<List<CompanyScoreSummary>> Value(ScoresFilter? filter) {
        Result<PagedResults<CompanyScoreSummary>> r = await _dbm.GetCompanyScores(
            new PaginationRequest(1, 50), ScoresSortBy.OverallScore, SortDirection.Descending, filter, _ct);
        Assert.True(r.IsSuccess);
        Assert.Equal((uint)r.Value!.Items.Count, r.Value.Pagination.TotalItems);
        return [.. r.Value.Items];
    }

    private async Task<List<CompanyMoatScoreSummary>> Moat(ScoresFilter? filter) {
        Result<PagedResults<CompanyMoatScoreSummary>> r = await _dbm.GetCompanyMoatScores(
            new PaginationRequest(1, 50), MoatScoresSortBy.OverallScore, SortDirection.Descending, filter, _ct);
        Assert.True(r.IsSuccess);
        Assert.Equal((uint)r.Value!.Items.Count, r.Value.Pagination.TotalItems);
        return [.. r.Value.Items];
    }

    [Fact]
    public async Task ValueScores_CarrySicAndFinancialFlag() {
        await Seed();

        List<CompanyScoreSummary> rows = await Value(null);

        Assert.Equal(4, rows.Count);
        CompanyScoreSummary bank = rows.Find(r => r.CompanyId == 2)!;
        Assert.Equal(6021, bank.SicCode);
        Assert.Equal("National Commercial Banks", bank.SicDescription);
        Assert.True(bank.IsFinancial);
        Assert.False(rows.Find(r => r.CompanyId == 1)!.IsFinancial);
        Assert.False(rows.Find(r => r.CompanyId == 3)!.IsFinancial);
        CompanyScoreSummary noSic = rows.Find(r => r.CompanyId == 4)!;
        Assert.Null(noSic.SicCode);
        Assert.False(noSic.IsFinancial);
    }

    [Fact]
    public async Task ValueScores_OnlyFinancials() {
        await Seed();

        List<CompanyScoreSummary> rows = await Value(new ScoresFilter(null, null, null, FinancialsFilter.Only));

        Assert.Single(rows);
        Assert.Equal(2ul, rows[0].CompanyId);
    }

    [Fact]
    public async Task ValueScores_ExcludeFinancials_KeepsNullSic() {
        await Seed();

        List<CompanyScoreSummary> rows = await Value(new ScoresFilter(null, null, null, FinancialsFilter.Exclude));

        Assert.Equal([4ul, 3ul, 1ul], rows.ConvertAll(r => r.CompanyId));
    }

    [Fact]
    public async Task ValueScores_FinancialsCombineWithMinScore() {
        await Seed();

        List<CompanyScoreSummary> rows = await Value(new ScoresFilter(13, null, null, FinancialsFilter.Exclude));

        Assert.Equal([4ul, 3ul], rows.ConvertAll(r => r.CompanyId));
    }

    [Fact]
    public async Task MoatScores_CarrySicAndFilter() {
        await Seed();

        List<CompanyMoatScoreSummary> all = await Moat(new ScoresFilter(null, null, null, FinancialsFilter.All));
        List<CompanyMoatScoreSummary> only = await Moat(new ScoresFilter(null, null, null, FinancialsFilter.Only));
        List<CompanyMoatScoreSummary> exclude = await Moat(new ScoresFilter(null, null, null, FinancialsFilter.Exclude));

        Assert.Equal(4, all.Count);
        Assert.Single(only);
        Assert.True(only[0].IsFinancial);
        Assert.Equal("National Commercial Banks", only[0].SicDescription);
        Assert.Equal(3, exclude.Count);
        Assert.DoesNotContain(exclude, r => r.CompanyId == 2);
    }
}
