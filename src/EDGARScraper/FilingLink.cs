using MongoDB.Bson;

namespace EDGARScraper;

/// <summary>
/// For example:
/// - FilingType: 10-K
/// - FilingDate: 2024-11-01
/// - DocumentLink: https://www.sec.gov/Archives/edgar/data/320193/000032019324000123/0000320193-24-000123-index.htm
/// </summary>
internal record FilingLink(string FilingType, string FilingDate, string DocumentLink)
{
    internal static readonly FilingLink Empty = new(string.Empty, string.Empty, string.Empty);

    internal static FilingLink FromBson(BsonDocument doc)
    {
        return new(
            doc["filing_type"]?.AsString ?? string.Empty,
            doc["filing_date"].AsString ?? string.Empty,
            doc["document_link"].AsString ?? string.Empty);
    }
}
