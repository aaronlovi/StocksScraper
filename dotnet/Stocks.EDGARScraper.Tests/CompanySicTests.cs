using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Stocks.DataModels;
using Stocks.DataModels.Scoring;
using Stocks.Persistence.Database;
using Stocks.Shared;

namespace Stocks.EDGARScraper.Tests;

public class CompanySicTests {
    private readonly DbmInMemoryService _dbm = new();
    private readonly CancellationToken _ct = CancellationToken.None;

    [Theory]
    [InlineData(6000, true)]   // depository institutions, low end
    [InlineData(6021, true)]   // national commercial banks
    [InlineData(6162, true)]   // mortgage bankers
    [InlineData(6211, true)]   // security brokers
    [InlineData(6331, true)]   // fire, marine and casualty insurance
    [InlineData(6411, true)]   // insurance agents
    [InlineData(6499, true)]   // high end
    [InlineData(3571, false)]  // electronic computers
    [InlineData(5999, false)]
    [InlineData(6500, false)]  // real estate
    [InlineData(6770, false)]  // blank checks
    [InlineData(6798, false)]  // REITs
    public void IsFinancial_FollowsTheSicRange(int sic, bool expected) =>
        Assert.Equal(expected, IndustryClassification.IsFinancial(sic));

    [Fact]
    public void IsFinancial_NullIsNotFinancial() =>
        Assert.False(IndustryClassification.IsFinancial(null));

    [Theory]
    [InlineData("3571", 3571)]
    [InlineData(" 6021 ", 6021)]
    [InlineData("100", 100)]
    public void ParseSic_ReadsDigits(string raw, int expected) =>
        Assert.Equal(expected, IndustryClassification.ParseSic(raw));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("12345")]
    [InlineData("-123")]
    [InlineData("60.2")]
    public void ParseSic_RejectsMissingOrMalformed(string? raw) =>
        Assert.Null(IndustryClassification.ParseSic(raw));

    [Theory]
    [InlineData(FinancialsFilter.All, 6021, true)]
    [InlineData(FinancialsFilter.All, 3571, true)]
    [InlineData(FinancialsFilter.Only, 6021, true)]
    [InlineData(FinancialsFilter.Only, 3571, false)]
    [InlineData(FinancialsFilter.Exclude, 6021, false)]
    [InlineData(FinancialsFilter.Exclude, 3571, true)]
    public void FinancialsFilter_Matches(FinancialsFilter filter, int sic, bool expected) =>
        Assert.Equal(expected, filter.Matches(sic));

    [Fact]
    public void FinancialsFilter_NullSic_OnlyExcludeAndAllKeepIt() {
        Assert.True(FinancialsFilter.All.Matches(null));
        Assert.False(FinancialsFilter.Only.Matches(null));
        Assert.True(FinancialsFilter.Exclude.Matches(null));
    }

    [Theory]
    [InlineData(null, FinancialsFilter.All)]
    [InlineData("", FinancialsFilter.All)]
    [InlineData("only", FinancialsFilter.Only)]
    [InlineData("ONLY", FinancialsFilter.Only)]
    [InlineData("exclude", FinancialsFilter.Exclude)]
    [InlineData("bogus", FinancialsFilter.All)]
    public void FinancialsFilter_Parse(string? raw, FinancialsFilter expected) =>
        Assert.Equal(expected, FinancialsFilters.Parse(raw));

    [Fact]
    public async Task UpdateCompanySic_ThenGetCompanySics_ReturnsNonNullCodes() {
        var sics = new List<CompanySic> {
            new(1, 3571, "Electronic Computers"),
            new(2, 6021, "National Commercial Banks"),
            new(3, null, null),
        };

        Result update = await _dbm.UpdateCompanySic(sics, _ct);
        Result<IReadOnlyCollection<CompanySic>> read = await _dbm.GetCompanySics(_ct);

        Assert.True(update.IsSuccess);
        Assert.True(read.IsSuccess);
        var byId = new Dictionary<ulong, CompanySic>();
        foreach (CompanySic sic in read.Value!)
            byId[sic.CompanyId] = sic;
        Assert.Equal(2, byId.Count);
        Assert.Equal(3571, byId[1].SicCode);
        Assert.Equal("National Commercial Banks", byId[2].SicDescription);
    }

    [Fact]
    public async Task UpdateCompanySic_NullClearsAnEarlierCode() {
        _ = await _dbm.UpdateCompanySic([new CompanySic(1, 6021, "National Commercial Banks")], _ct);
        _ = await _dbm.UpdateCompanySic([new CompanySic(1, null, null)], _ct);

        Result<IReadOnlyCollection<CompanySic>> read = await _dbm.GetCompanySics(_ct);

        Assert.Empty(read.Value!);
    }
}
