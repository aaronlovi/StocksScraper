using MongoDB.Bson;

namespace EDGARScraper;

/// <summary>
/// Represents what is found on the landing page of a company's filings.
/// It is an HTML page listing the 10-Ks.
/// Parse the HTML to extract the URLs of the 10-Ks.
/// </summary>
internal record RawFilingData(string RawHtml, BsonDocument CompanyBson)
{
    internal static readonly RawFilingData Empty = new(string.Empty, []);

    internal string Cik => CompanyBson.TryGetValue("cik", out var cikValue) ? cikValue.AsString : string.Empty;
}
