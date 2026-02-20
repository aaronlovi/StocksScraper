using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EDGARScraper;

namespace Stocks.EDGARScraper.Tests;

public class InlineXbrlParserTests {
    private readonly InlineXbrlParser _parser = new();

    private const string HtmlPrefix = @"<html>
<head><meta charset=""utf-8""/></head>
<body>";

    private const string HtmlSuffix = @"</body></html>";

    [Fact]
    public async Task ParseSharesFromHtml_SingleClassNoContext_ReturnsEmpty() {
        // Arrange: ix:nonfraction without a matching context
        string html = HtmlPrefix
            + @"<ix:nonfraction contextref=""c-missing"" name=""dei:EntityCommonStockSharesOutstanding"" unitref=""shares"" decimals=""0"">100</ix:nonfraction>"
            + HtmlSuffix;

        // Act
        IReadOnlyCollection<AggregatedSharesFact> results = await _parser.ParseSharesFromHtmlAsync(html);

        // Assert: no matching context, so nothing returned
        Assert.Empty(results);
    }

    [Fact]
    public async Task ParseSharesFromHtml_SingleClass_ReturnsCorrectValue() {
        // Arrange: one share class with matching context
        string html = HtmlPrefix
            + @"<xbrli:context id=""c-1"">
                  <xbrli:entity><xbrli:identifier>0001234567</xbrli:identifier></xbrli:entity>
                  <xbrli:period><xbrli:instant>2024-09-30</xbrli:instant></xbrli:period>
                </xbrli:context>"
            + @"<ix:nonfraction contextref=""c-1"" name=""dei:EntityCommonStockSharesOutstanding"" unitref=""shares"" decimals=""0"">500000</ix:nonfraction>"
            + HtmlSuffix;

        // Act
        IReadOnlyCollection<AggregatedSharesFact> results = await _parser.ParseSharesFromHtmlAsync(html);

        // Assert
        Assert.Single(results);
        AggregatedSharesFact fact = Assert.Single(results);
        Assert.Equal(new DateOnly(2024, 9, 30), fact.Date);
        Assert.Equal(500000m, fact.TotalShares);
    }

    [Fact]
    public async Task ParseSharesFromHtml_MultiClass_TakesLargestClass() {
        // Arrange: Visa-like scenario with 4 share classes on the same date
        string html = HtmlPrefix
            + @"<xbrli:context id=""c-a"">
                  <xbrli:entity>
                    <xbrli:identifier>0001403161</xbrli:identifier>
                    <xbrli:segment>
                      <xbrldi:explicitmember dimension=""us-gaap:StatementClassOfStockAxis"">us-gaap:CommonClassAMember</xbrldi:explicitmember>
                    </xbrli:segment>
                  </xbrli:entity>
                  <xbrli:period><xbrli:instant>2024-09-30</xbrli:instant></xbrli:period>
                </xbrli:context>
                <xbrli:context id=""c-b1"">
                  <xbrli:entity>
                    <xbrli:identifier>0001403161</xbrli:identifier>
                    <xbrli:segment>
                      <xbrldi:explicitmember dimension=""us-gaap:StatementClassOfStockAxis"">v:ClassB1CommonStockMember</xbrldi:explicitmember>
                    </xbrli:segment>
                  </xbrli:entity>
                  <xbrli:period><xbrli:instant>2024-09-30</xbrli:instant></xbrli:period>
                </xbrli:context>
                <xbrli:context id=""c-b2"">
                  <xbrli:entity>
                    <xbrli:identifier>0001403161</xbrli:identifier>
                    <xbrli:segment>
                      <xbrldi:explicitmember dimension=""us-gaap:StatementClassOfStockAxis"">v:ClassB2CommonStockMember</xbrldi:explicitmember>
                    </xbrli:segment>
                  </xbrli:entity>
                  <xbrli:period><xbrli:instant>2024-09-30</xbrli:instant></xbrli:period>
                </xbrli:context>
                <xbrli:context id=""c-c"">
                  <xbrli:entity>
                    <xbrli:identifier>0001403161</xbrli:identifier>
                    <xbrli:segment>
                      <xbrldi:explicitmember dimension=""us-gaap:StatementClassOfStockAxis"">v:ClassCCommonStockMember</xbrldi:explicitmember>
                    </xbrli:segment>
                  </xbrli:entity>
                  <xbrli:period><xbrli:instant>2024-09-30</xbrli:instant></xbrli:period>
                </xbrli:context>"
            + @"<ix:nonfraction contextref=""c-a"" name=""dei:EntityCommonStockSharesOutstanding"" unitref=""shares"" decimals=""0"">1,687,629,770</ix:nonfraction>"
            + @"<ix:nonfraction contextref=""c-b1"" name=""dei:EntityCommonStockSharesOutstanding"" unitref=""shares"" decimals=""0"">4,835,384</ix:nonfraction>"
            + @"<ix:nonfraction contextref=""c-b2"" name=""dei:EntityCommonStockSharesOutstanding"" unitref=""shares"" decimals=""0"">120,338,948</ix:nonfraction>"
            + @"<ix:nonfraction contextref=""c-c"" name=""dei:EntityCommonStockSharesOutstanding"" unitref=""shares"" decimals=""0"">8,938,707</ix:nonfraction>"
            + HtmlSuffix;

        // Act
        IReadOnlyCollection<AggregatedSharesFact> results = await _parser.ParseSharesFromHtmlAsync(html);

        // Assert: largest class (Class A) is used, not the sum
        Assert.Single(results);
        AggregatedSharesFact fact = Assert.Single(results);
        Assert.Equal(new DateOnly(2024, 9, 30), fact.Date);
        Assert.Equal(1687629770m, fact.TotalShares);
    }

    [Fact]
    public async Task ParseSharesFromHtml_MultiClass_UseSmallestClass_TakesSmallestClass() {
        // Arrange: Berkshire-like scenario — Class A (small count, high price) and Class B (large count, low price)
        string html = HtmlPrefix
            + @"<xbrli:context id=""c-a"">
                  <xbrli:entity>
                    <xbrli:identifier>0001067983</xbrli:identifier>
                    <xbrli:segment>
                      <xbrldi:explicitmember dimension=""us-gaap:StatementClassOfStockAxis"">us-gaap:CommonClassAMember</xbrldi:explicitmember>
                    </xbrli:segment>
                  </xbrli:entity>
                  <xbrli:period><xbrli:instant>2024-12-31</xbrli:instant></xbrli:period>
                </xbrli:context>
                <xbrli:context id=""c-b"">
                  <xbrli:entity>
                    <xbrli:identifier>0001067983</xbrli:identifier>
                    <xbrli:segment>
                      <xbrldi:explicitmember dimension=""us-gaap:StatementClassOfStockAxis"">us-gaap:CommonClassBMember</xbrldi:explicitmember>
                    </xbrli:segment>
                  </xbrli:entity>
                  <xbrli:period><xbrli:instant>2024-12-31</xbrli:instant></xbrli:period>
                </xbrli:context>"
            + @"<ix:nonfraction contextref=""c-a"" name=""dei:EntityCommonStockSharesOutstanding"" unitref=""shares"" decimals=""0"">596,421</ix:nonfraction>"
            + @"<ix:nonfraction contextref=""c-b"" name=""dei:EntityCommonStockSharesOutstanding"" unitref=""shares"" decimals=""0"">1,340,825,436</ix:nonfraction>"
            + HtmlSuffix;

        // Act
        IReadOnlyCollection<AggregatedSharesFact> results = await _parser.ParseSharesFromHtmlAsync(html, useSmallestClass: true);

        // Assert: smallest class (Class A) is used for multi-ticker companies
        Assert.Single(results);
        AggregatedSharesFact fact = Assert.Single(results);
        Assert.Equal(new DateOnly(2024, 12, 31), fact.Date);
        Assert.Equal(596421m, fact.TotalShares);
    }

    [Fact]
    public async Task ParseSharesFromHtml_NoSharesFacts_ReturnsEmpty() {
        // Arrange: HTML with context but no shares ix:nonfraction elements
        string html = HtmlPrefix
            + @"<xbrli:context id=""c-1"">
                  <xbrli:entity><xbrli:identifier>0001234567</xbrli:identifier></xbrli:entity>
                  <xbrli:period><xbrli:instant>2024-09-30</xbrli:instant></xbrli:period>
                </xbrli:context>"
            + @"<ix:nonfraction contextref=""c-1"" name=""us-gaap:Assets"" unitref=""usd"" decimals=""-6"">1000000</ix:nonfraction>"
            + HtmlSuffix;

        // Act
        IReadOnlyCollection<AggregatedSharesFact> results = await _parser.ParseSharesFromHtmlAsync(html);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public async Task ParseSharesFromHtml_MultipleDates_SeparateFacts() {
        // Arrange: shares on two different dates
        string html = HtmlPrefix
            + @"<xbrli:context id=""c-2024"">
                  <xbrli:entity><xbrli:identifier>0001234567</xbrli:identifier></xbrli:entity>
                  <xbrli:period><xbrli:instant>2024-09-30</xbrli:instant></xbrli:period>
                </xbrli:context>
                <xbrli:context id=""c-2023"">
                  <xbrli:entity><xbrli:identifier>0001234567</xbrli:identifier></xbrli:entity>
                  <xbrli:period><xbrli:instant>2023-09-30</xbrli:instant></xbrli:period>
                </xbrli:context>"
            + @"<ix:nonfraction contextref=""c-2024"" name=""dei:EntityCommonStockSharesOutstanding"" unitref=""shares"" decimals=""0"">2000000</ix:nonfraction>"
            + @"<ix:nonfraction contextref=""c-2023"" name=""dei:EntityCommonStockSharesOutstanding"" unitref=""shares"" decimals=""0"">1800000</ix:nonfraction>"
            + HtmlSuffix;

        // Act
        IReadOnlyCollection<AggregatedSharesFact> results = await _parser.ParseSharesFromHtmlAsync(html);

        // Assert: two separate facts for two dates
        Assert.Equal(2, results.Count);
        var resultsList = new List<AggregatedSharesFact>(results);
        resultsList.Sort((a, b) => a.Date.CompareTo(b.Date));
        Assert.Equal(new DateOnly(2023, 9, 30), resultsList[0].Date);
        Assert.Equal(1800000m, resultsList[0].TotalShares);
        Assert.Equal(new DateOnly(2024, 9, 30), resultsList[1].Date);
        Assert.Equal(2000000m, resultsList[1].TotalShares);
    }

    [Fact]
    public async Task ParseSharesFromHtml_ScaleAttribute_AppliedCorrectly() {
        // Arrange: scale="3" means multiply by 1000
        string html = HtmlPrefix
            + @"<xbrli:context id=""c-1"">
                  <xbrli:entity><xbrli:identifier>0001234567</xbrli:identifier></xbrli:entity>
                  <xbrli:period><xbrli:instant>2024-12-31</xbrli:instant></xbrli:period>
                </xbrli:context>"
            + @"<ix:nonfraction contextref=""c-1"" name=""dei:EntityCommonStockSharesOutstanding"" unitref=""shares"" decimals=""0"" scale=""3"">1500</ix:nonfraction>"
            + HtmlSuffix;

        // Act
        IReadOnlyCollection<AggregatedSharesFact> results = await _parser.ParseSharesFromHtmlAsync(html);

        // Assert
        Assert.Single(results);
        AggregatedSharesFact fact = Assert.Single(results);
        Assert.Equal(1500000m, fact.TotalShares);
    }

    [Fact]
    public async Task ParseSharesFromHtml_EmptyHtml_ReturnsEmpty() {
        // Act
        IReadOnlyCollection<AggregatedSharesFact> results = await _parser.ParseSharesFromHtmlAsync("");

        // Assert
        Assert.Empty(results);
    }
}
