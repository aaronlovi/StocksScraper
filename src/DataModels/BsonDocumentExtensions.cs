using MongoDB.Bson;
using Stocks.DataModels;
using System.Collections.Generic;

namespace DataModels;

public static class BsonDocumentExtensions
{
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
                        { "Unit", dataPoint.Units.UnitName },
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
