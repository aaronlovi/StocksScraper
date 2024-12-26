using HtmlAgilityPack;
using MongoDB.Bson;

namespace EDGARScraper;

internal static class RawFilingParser
{
    /// <summary>
    /// Represents the business logic for parsing the raw filing data
    /// This produces a BsonArray of filings.
    /// </summary>
    internal static BsonArray ParseRawFilingsToBsonArray(RawFilingData rawFilingData)
    {
        var htmlDoc = new HtmlDocument();
        htmlDoc.LoadHtml(rawFilingData.RawHtml);

        var filings = new BsonArray();
        foreach (var row in htmlDoc.DocumentNode.SelectNodes("//table[@class='tableFile2']/tbody/tr"))
        {
            var cells = row.SelectNodes("td");

            if (cells == null || cells.Count < 4) continue;

            filings.Add(new BsonDocument
            {
                { "filing_type", cells[0].InnerText.Trim() },
                { "filing_date", cells[3].InnerText.Trim() },
                { "document_link", "https://www.sec.gov" + cells[1].SelectSingleNode("a")?.Attributes["href"]?.Value }
            });
        }

        return filings;
    }
}
