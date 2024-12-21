using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Net.Http;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        // MongoDB setup
        var client = new MongoClient("mongodb://root:example@localhost:27017");
        var database = client.GetDatabase("EDGAR");
        var parsedCollection = database.GetCollection<BsonDocument>("ParsedFilings");
        var documentsCollection = database.GetCollection<BsonDocument>("FilingDocuments");

        // Fetch parsed filings
        var parsedData = await parsedCollection.Find(new BsonDocument()).FirstOrDefaultAsync();
        if (parsedData == null)
        {
            Console.WriteLine("No parsed data found in the database.");
            return;
        }

        var filings = parsedData["filings"].AsBsonArray;
        using HttpClient httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("User-Agent", "EDGARScraper (inno.and.logic@gmail.com)");

        foreach (var filing in filings)
        {
            var filingDoc = filing.AsBsonDocument;
            string documentLink = filingDoc["document_link"].AsString;

            // Download filing document
            try
            {
                var documentContent = await httpClient.GetStringAsync(documentLink);
                var document = new BsonDocument
                {
                    { "company", parsedData["company"].AsBsonDocument },
                    { "filing_type", filingDoc["filing_type"].AsString },
                    { "filing_date", filingDoc["filing_date"].AsString },
                    { "document_link", documentLink },
                    { "content", documentContent },
                    { "downloaded_at", DateTime.UtcNow }
                };

                // Save to MongoDB
                await documentsCollection.InsertOneAsync(document);
                Console.WriteLine($"Downloaded and saved document: {documentLink}");
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Failed to download document: {documentLink}. Error: {ex.Message}");
            }
        }
    }
}
