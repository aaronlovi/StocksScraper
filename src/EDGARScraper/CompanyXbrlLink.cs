using MongoDB.Bson;

namespace EDGARScraper;

internal record CompanyXbrlLink(BsonDocument CompanyDoc, string FilingDate, string XbrlUrl)
{
    internal string Company => CompanyDoc.TryGetValue("company", out var companyValue) ? companyValue.AsString : string.Empty;
    internal string Cik => CompanyDoc.TryGetValue("cik", out var cikValue) ? cikValue.AsString : string.Empty;

    internal static CompanyXbrlLink FromBson(BsonDocument doc) =>
        new(doc["company"].AsBsonDocument, doc["filing_date"].AsString, doc["xbrl_link"].AsString);
}
