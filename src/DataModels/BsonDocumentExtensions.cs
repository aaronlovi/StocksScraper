using MongoDB.Bson;
using System.Collections.Generic;

namespace DataModels;

public static class BsonDocumentExtensions
{
    public static BsonDocument ToBsonDocument(this Company company)
    {
        var document = new BsonDocument
        {
            { "cik", company.Cik },
            { "name", company.Name },
            { "data_source", company.DataSource },
        };

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

    public static BsonDocument XBRLFileDataToBsonDocument(
        IReadOnlyDictionary<string, Dictionary<string, Dictionary<DatePair, DataPoint>>> dataPoints)
    {
        var bsonDocument = new BsonDocument();

        foreach (var factEntry in dataPoints)
        {
            var factName = factEntry.Key;
            var unitsDataPoints = new BsonDocument();

            foreach (var unitEntry in factEntry.Value)
            {
                var unitName = unitEntry.Key;
                var datePoints = new BsonArray();

                foreach (var datePairEntry in unitEntry.Value)
                {
                    var datePair = datePairEntry.Key;
                    var dataPoint = datePairEntry.Value;

                    var dataPointDocument = new BsonDocument
                    {
                        { "StartDate", datePair.StartTimeUtc },
                        { "EndDate", datePair.EndTimeUtc },
                        { "Value", dataPoint.Value },
                        { "Unit", dataPoint.Units.Name },
                        { "FiledDate", dataPoint.FiledTimeUtc }
                    };

                    datePoints.Add(dataPointDocument);
                }

                unitsDataPoints.Add(unitName, datePoints);
            }

            bsonDocument.Add(factName, unitsDataPoints);
        }

        return bsonDocument;
    }
}
