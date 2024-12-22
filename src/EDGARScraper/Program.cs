using HtmlAgilityPack;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Net.Http;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Please provide a command-line switch: --fetch, --parse, or --download");
            return;
        }

        switch (args[0].ToLowerInvariant())
        {
            case "--fetch":
                await FetchRawData();
                break;
            case "--parse":
                await ParseMetadata();
                break;
            case "--download":
                await DownloadDocuments();
                break;
            default:
                Console.WriteLine("Invalid command-line switch. Please use --fetch, --parse, or --download");
                break;
        }
    }

    static async Task FetchRawData()
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

    static async Task ParseMetadata()
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

    static async Task DownloadDocuments()
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
