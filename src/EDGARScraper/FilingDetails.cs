using MongoDB.Bson;

namespace EDGARScraper;

internal record FilingDetails(BsonDocument CompanyBson, string Content, string FilingDate)
{
    internal string Company => CompanyBson.TryGetValue("company", out var companyValue) ? companyValue.AsString : string.Empty;
    internal string Cik => CompanyBson.TryGetValue("cik", out var cikValue) ? cikValue.AsString : string.Empty;

    internal static FilingDetails FromBson(BsonDocument doc) =>
        new(doc["company"].AsBsonDocument, doc["content"].AsString, doc["filing_date"].AsString);
}
