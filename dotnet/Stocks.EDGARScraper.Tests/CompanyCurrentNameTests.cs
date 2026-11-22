using System.Collections.Generic;
using System.IO;
using EDGARScraper.Services;
using Stocks.DataModels;
using Stocks.Persistence.Services;

namespace Stocks.EDGARScraper.Tests;

/// <summary>
/// H3: company_names holds every historical name in alphabetical order, so the first is
/// often a former name (RDN: "CMAC INVESTMENT CORP"). The SEC ticker file's title is the
/// current name and wins for listed companies.
/// </summary>
public class CompanyCurrentNameTests {
    [Fact]
    public void LoadBaseMappings_ReadsTitle() {
        string path = Path.GetTempFileName();
        try {
            File.WriteAllText(path, "{\"0\":{\"cik_str\":890926,\"ticker\":\"RDN\",\"title\":\"RADIAN GROUP INC\"}}");
            List<SecTickerMapping> mappings = SecTickerJsonParser.LoadBaseMappings(path, [], []);
            SecTickerMapping mapping = Assert.Single(mappings);
            Assert.Equal("RADIAN GROUP INC", mapping.Title);
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void BuildCompanyNameLookup_PrefersTickerTitle() {
        var names = new List<CompanyName> {
            new(1, 7, "CMAC INVESTMENT CORP"),
            new(2, 7, "RADIAN GROUP INC"),
            new(3, 8, "SOME UNLISTED CO"),
        };
        var tickers = new List<CompanyTicker> { new(7, "RDN", "NYSE", "Radian Group Inc") };

        Dictionary<ulong, string> lookup = ScoringService.BuildCompanyNameLookup(names, tickers);

        Assert.Equal("Radian Group Inc", lookup[7]);
        Assert.Equal("SOME UNLISTED CO", lookup[8]);
    }

    [Fact]
    public void BuildCompanyNameLookup_BlankTitleFallsBackToName() {
        var names = new List<CompanyName> { new(1, 7, "CMAC INVESTMENT CORP") };
        var tickers = new List<CompanyTicker> { new(7, "RDN", "NYSE", " ") };

        Assert.Equal("CMAC INVESTMENT CORP", ScoringService.BuildCompanyNameLookup(names, tickers)[7]);
    }
}
