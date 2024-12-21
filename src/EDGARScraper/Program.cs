using HtmlAgilityPack;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        // MongoDB setup
        var client = new MongoClient("mongodb://root:example@localhost:27017");
        var database = client.GetDatabase("EDGAR");
        var rawCollection = database.GetCollection<BsonDocument>("RawFilings");
        var parsedCollection = database.GetCollection<BsonDocument>("ParsedFilings");

        // Fetch the raw HTML from the database
        var rawData = await rawCollection.Find(new BsonDocument()).FirstOrDefaultAsync();
        if (rawData == null)
        {
            Console.WriteLine("No raw data found in the database.");
            return;
        }

        string rawHtml = rawData["raw_data"].AsString;

        // Parse the HTML
        var htmlDoc = new HtmlDocument();
        htmlDoc.LoadHtml(rawHtml);

        var filings = new BsonArray();
        foreach (var row in htmlDoc.DocumentNode.SelectNodes("//table[@class='tableFile2']/tr"))
        {
            var cells = row.SelectNodes("td");
            if (cells != null && cells.Count >= 4)
            {
                filings.Add(new BsonDocument
                {
                    { "filing_type", cells[0].InnerText.Trim() },
                    { "filing_date", cells[3].InnerText.Trim() },
                    { "document_link", "https://www.sec.gov" + cells[1].SelectSingleNode("a")?.Attributes["href"]?.Value }
                });
            }
        }

        // Save parsed data to MongoDB
        var parsedDocument = new BsonDocument
        {
            { "company", rawData["company"].AsBsonDocument },
            { "filings", filings },
            { "parsed_at", DateTime.UtcNow }
        };
        await parsedCollection.InsertOneAsync(parsedDocument);

        Console.WriteLine("Parsed and saved filing metadata.");
    }
}
