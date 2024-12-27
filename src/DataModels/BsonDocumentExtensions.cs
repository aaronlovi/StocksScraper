using MongoDB.Bson;

namespace DataModels;

public static class BsonDocumentExtensions
{
    public static BsonDocument ToBsonDocument(this Company company)
    {
        var document = new BsonDocument
        {
            { "name", company.Name }
        };

        if (!string.IsNullOrEmpty(company.Cik))
            document.Add("cik", company.Cik);

        if (company.Instruments is not null)
            AddInstrumentsToCompany(company, document);

        return document;
    }

    private static void AddInstrumentsToCompany(Company company, BsonDocument document)
    {
        if (company.Instruments is null) return;

        var instruments = new BsonArray();
        foreach (var instrument in company.Instruments)
        {
            var instrumentDoc = new BsonDocument
            {
                { "name", instrument.Name },
                { "symbol", instrument.Symbol },
                { "exchange", instrument.Exchange },
            };
            instruments.Add(instrumentDoc);
        }
        document.Add("instruments", instruments);
    }
}
