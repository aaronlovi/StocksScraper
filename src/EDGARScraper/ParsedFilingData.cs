using MongoDB.Bson;

namespace EDGARScraper;

internal record ParsedFilingData(BsonDocument CompanyBson, BsonArray Filings)
{
    internal static readonly ParsedFilingData Empty = new([], []);

    internal string Cik => CompanyBson.TryGetValue("cik", out var cikValue) ? cikValue.AsString : string.Empty;
}
