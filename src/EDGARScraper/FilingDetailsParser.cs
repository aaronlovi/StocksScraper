using HtmlAgilityPack;
using MongoDB.Bson;
using System;

namespace EDGARScraper;

internal static class FilingDetailsParser
{
    private static readonly HtmlNodeCollection EmptyHtmlNodeCollection = new(null);

    internal static BsonDocument? ParseFilingDetailToBsonArray(FilingDetails filingDetails)
    {
        var htmlDoc = new HtmlDocument();
        htmlDoc.LoadHtml(filingDetails.Content);

        var links = new BsonArray();
        HtmlNodeCollection rows = htmlDoc.DocumentNode.SelectNodes("//table[@class='tableFile']/tr") ?? EmptyHtmlNodeCollection;

        foreach (var row in rows)
        {
            var cells = row.SelectNodes("td");

            if (cells == null || cells.Count < 4) continue;

            string description = cells[1].InnerText.Trim();
            string docType = cells[3].InnerText.Trim();

            if (!docType.Equals("XML") || !description.Contains("XBRL")) continue;

            string xbrlLink = "https://www.sec.gov" + cells[2].SelectSingleNode("a")?.Attributes["href"]?.Value;

            return new BsonDocument {
                { "company", filingDetails.CompanyBson },
                { "filing_date", filingDetails.FilingDate },
                { "xbrl_link", xbrlLink },
                { "extracted_at", DateTime.UtcNow }
            };
        }

        return null;
    }
}