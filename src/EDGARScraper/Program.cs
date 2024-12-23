using HtmlAgilityPack;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Net.Http;
using System.Threading.Tasks;

class Program
{
    private static IMongoDatabase? _database = null;
    private static HttpClient? _httpClient = null;

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
        var collection = GetCollection("RawFilings");

        // EDGAR URL for filings (Example: Apple Inc.)
        string cik = "0000320193"; // Apple Inc.
        string url = $"https://www.sec.gov/cgi-bin/browse-edgar?action=getcompany&CIK={cik}&type=10-K&count=5";

        // Fetch filing data
        string? response = await FetchContentAsync(url);
        if (response is null) return;

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
        var rawCollection = GetCollection("RawFilings");
        var parsedCollection = GetCollection("ParsedFilings");

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
        var parsedCollection = GetCollection("ParsedFilings");
        var documentsCollection = GetCollection("FilingDocuments");

        // Fetch parsed filings
        var parsedData = await parsedCollection.Find(new BsonDocument()).FirstOrDefaultAsync();
        if (parsedData == null)
        {
            Console.WriteLine("No parsed data found in the database.");
            return;
        }

        var filings = parsedData["filings"].AsBsonArray;
        
        foreach (var filing in filings)
        {
            var filingDoc = filing.AsBsonDocument;
            string documentLink = filingDoc["document_link"].AsString;

            // Download filing document
            try
            {
                string? documentContent = await FetchContentAsync(documentLink);
                if (documentContent is null) continue;

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

    static IMongoDatabase GetDatabase()
    {
        if (_database is null)
        {
            var client = new MongoClient("mongodb://root:example@localhost:27017");
            _database = client.GetDatabase("EDGAR");
        }
        return _database;
    }

    static IMongoCollection<BsonDocument> GetCollection(string collectionName)
    {
        IMongoDatabase database = GetDatabase();
        return database.GetCollection<BsonDocument>(collectionName);
    }

    static async Task<string?> FetchContentAsync(string url)
    {
        try
        {
            if (_httpClient is null)
            {
                _httpClient = new HttpClient();
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "EDGARScraper (inno.and.logic@gmail.com)");
            }

            return await _httpClient.GetStringAsync(url);
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"Failed to fetch content from {url}. Error: {ex.Message}");
            return null;
        }
    }
}
