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
        var collection = database.GetCollection<BsonDocument>("RawFilings");

        // EDGAR URL for filings (Example: Apple Inc.)
        string cik = "0000320193"; // Apple Inc.
        string url = $"https://www.sec.gov/cgi-bin/browse-edgar?action=getcompany&CIK={cik}&type=10-K&count=5";

        // Fetch filing data
        using HttpClient httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("User-Agent", "YourAppName (your-email@example.com)");

        var response = await httpClient.GetStringAsync(url);

        // Save raw HTML response to MongoDB
        var document = new BsonDocument
        {
            { "company", new BsonDocument { { "cik", cik }, { "name", "Apple Inc." } } },
            { "url", url },
            { "raw_data", response },
            { "fetched_at", DateTime.UtcNow }
        };
        collection.InsertOne(document);

        Console.WriteLine("Fetched and saved raw filing data for Apple Inc.");
    }
}
