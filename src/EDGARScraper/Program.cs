using HtmlAgilityPack;
using MongoDB.Bson;
using MongoDB.Driver;
using PuppeteerSharp;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;

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

        await EnsureBrowser();

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
            case "--extract-xbrl-links":
                await ExtractXBRLLinks();
                await DownloadXBRLDocuments();
                break;
            case "--parse-financial":
                ParseXBRL();
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

        using var browser = await Puppeteer.LaunchAsync(new LaunchOptions { Headless = true });
        using var page = await browser.NewPageAsync();

        // Navigate to the page and wait for network activity to idle
        await page.SetUserAgentAsync("EDGARScraper/0.1 (inno.and.logic@gmail.com)");
        await page.GoToAsync(url, WaitUntilNavigation.Networkidle2);
        string renderedHtml = await page.GetContentAsync();

        // Save rendered HTML to MongoDB
        ReplaceOneResult result = await collection.ReplaceOneAsync(
            filter: Builders<BsonDocument>.Filter.Eq("company.cik", cik), // Match by CIK
            replacement: new BsonDocument
            {
                { "company", new BsonDocument { { "cik", cik }, { "name", "Apple Inc." } } },
                { "url", url },
                { "raw_data", renderedHtml },
                { "fetched_at", DateTime.UtcNow }
            },
            options: new ReplaceOptions { IsUpsert = true } // Insert if not found
        );

        Console.WriteLine("Fetched and saved rendered HTML successfully.");
        Console.WriteLine("Results: Matched={0}, Modified={1}, Upserted={2}",
            result.MatchedCount, result.ModifiedCount, result.UpsertedId != null);
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
        foreach (var row in htmlDoc.DocumentNode.SelectNodes("//table[@class='tableFile2']/tbody/tr"))
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
        var filter = Builders<BsonDocument>.Filter.Eq("company.cik", rawData["company"]["cik"].AsString);
        var updateOptions = new ReplaceOptions { IsUpsert = true };

        var parsedDocument = new BsonDocument
        {
            { "company", rawData["company"].AsBsonDocument },
            { "filings", filings },
            { "parsed_at", DateTime.UtcNow }
        };
        ReplaceOneResult result = await parsedCollection.ReplaceOneAsync(
            filter, parsedDocument, updateOptions);

        Console.WriteLine("Parsed and saved filing metadata. Results: Matched={0}, Modified={1}, Upserted={2}",
            result.MatchedCount, result.ModifiedCount, result.UpsertedId != null);
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

                var filter = Builders<BsonDocument>.Filter.And(
                    Builders<BsonDocument>.Filter.Eq("company.cik", parsedData["company"]["cik"].AsString),
                    Builders<BsonDocument>.Filter.Eq("filing_date", filingDoc["filing_date"].AsString),
                    Builders<BsonDocument>.Filter.Eq("filing_type", filingDoc["filing_type"].AsString)
                );
                var updateOptions = new ReplaceOptions { IsUpsert = true };
                ReplaceOneResult result = await documentsCollection.ReplaceOneAsync(
                    filter, document, updateOptions);

                Console.WriteLine($"Downloaded and saved document: {documentLink}. Results: Matched={result.MatchedCount}, Modified={result.ModifiedCount}, Upserted={result.UpsertedId != null}");
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Failed to download document: {documentLink}. Error: {ex.Message}");
            }
        }
    }

    private static readonly HtmlNodeCollection EmptyHtmlNodeCollection = new(null);

    static async Task ExtractXBRLLinks()
    {
        // MongoDB setup
        var filingDocuments = GetCollection("FilingDocuments");
        var xbrlLinksCollection = GetCollection("XBRLLinks");

        // Fetch each landing page document
        var documents = filingDocuments.Find(new BsonDocument()).ToList();
        foreach (var doc in documents)
        {
            string content = doc["content"].AsString;
            string company = doc["company"]["name"].AsString;
            string filingDate = doc["filing_date"].AsString;

            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(content);

            // Extract XBRL links
            var links = new BsonArray();
            HtmlNodeCollection rows = htmlDoc.DocumentNode.SelectNodes("//table[@class='tableFile']/tr") ?? EmptyHtmlNodeCollection;
            foreach (var row in rows)
            {
                var cells = row.SelectNodes("td");

                if (cells == null || cells.Count < 4) continue;

                string description = cells[1].InnerText.Trim();
                string docType = cells[3].InnerText.Trim();

                if (!docType.Equals("XML") || !description.Contains("XBRL")) continue;

                string xbrlLink = "https://www.sec.gov" + cells[2].SelectSingleNode("a")?.Attributes["href"]?.Value;
                links.Add(xbrlLink);
            }

            // Save extracted links to a new collection
            if (links.Count <= 0) continue;

            var xbrlDocument = new BsonDocument
            {
                { "company", company },
                { "filing_date", filingDate },
                { "xbrl_links", links },
                { "extracted_at", DateTime.UtcNow }
            };
            var filter = Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("company", company),
                Builders<BsonDocument>.Filter.Eq("filing_date", filingDate));
            var updateOptions = new ReplaceOptions { IsUpsert = true };
            ReplaceOneResult result = await xbrlLinksCollection.ReplaceOneAsync(filter, xbrlDocument, updateOptions);
            Console.WriteLine($"Extracted XBRL links for {company} - {filingDate}. Results: Matched={result.MatchedCount}, Modified={result.ModifiedCount}, Upserted={result.UpsertedId != null}");
        }
    }

    static async Task DownloadXBRLDocuments()
    {
        var xbrlLinksCollection = GetCollection("XBRLLinks");
        var xbrlDocumentsCollection = GetCollection("XBRLDocuments");

        // Fetch XBRL links
        var xbrlEntries = xbrlLinksCollection.Find(new BsonDocument()).ToList();
        using HttpClient httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("User-Agent", "EDGARScraper/1.0 (your-email@example.com)");

        foreach (var entry in xbrlEntries)
        {
            var links = entry["xbrl_links"].AsBsonArray;
            foreach (var link in links)
            {
                string xbrlUrl = link.AsString;
                try
                {
                    string xbrlContent = await httpClient.GetStringAsync(xbrlUrl);

                    // Save XBRL content to MongoDB
                    var xbrlDoc = new BsonDocument
                    {
                        { "company", entry["company"].AsString },
                        { "filing_date", entry["filing_date"].AsString },
                        { "xbrl_url", xbrlUrl },
                        { "content", xbrlContent },
                        { "downloaded_at", DateTime.UtcNow }
                    };
                    var filter = Builders<BsonDocument>.Filter.And(
                        Builders<BsonDocument>.Filter.Eq("company", entry["company"].AsString),
                        Builders<BsonDocument>.Filter.Eq("filing_date", entry["filing_date"].AsString),
                        Builders<BsonDocument>.Filter.Eq("xbrl_url", xbrlUrl));
                    var updateOptions = new ReplaceOptions { IsUpsert = true };
                    var result = await xbrlDocumentsCollection.ReplaceOneAsync(filter, xbrlDoc, updateOptions);
                    Console.WriteLine($"Downloaded XBRL document: {xbrlUrl}. Results: Matched={result.MatchedCount}, Modified={result.ModifiedCount}, Upserted={result.UpsertedId != null}");
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Failed to download XBRL document: {xbrlUrl}. Error: {ex.Message}");
                }
            }
        }
    }

    static void ParseXBRL()
    {
        // MongoDB setup
        var xbrlDocumentsCollection = GetCollection("XBRLDocuments");
        var financialDataCollection = GetCollection("FinancialData");

        // Fetch an XBRL document
        var xbrlDoc = xbrlDocumentsCollection.Find(new BsonDocument()).FirstOrDefault();
        if (xbrlDoc == null)
        {
            Console.WriteLine("No XBRL documents found in the database.");
            return;
        }

        string content = xbrlDoc["content"].AsString;

        // Parse the XBRL XML
        var xDoc = XDocument.Parse(content);

        XNamespace? usGaapNamespace = GetUsGaapNamespace(xDoc);
        if (usGaapNamespace is null)
        {
            Console.WriteLine("Could not get us-gaap namespace.");
            return;
        }

        // Extract financial metrics
        var metrics = new BsonDocument
        {
            { "net_income", ExtractXBRLMetric(xDoc, usGaapNamespace, "NetIncomeLoss") },
            { "total_revenue", ExtractXBRLMetric(xDoc, usGaapNamespace, "Revenues") },
            { "depreciation", ExtractXBRLMetric(xDoc, usGaapNamespace, "DepreciationAndAmortization") },
            { "capital_expenditures", ExtractXBRLMetric(xDoc, usGaapNamespace, "PaymentsToAcquirePropertyPlantAndEquipment") },
            { "working_capital", ExtractXBRLMetric(xDoc, usGaapNamespace, "WorkingCapital") }
        };

        // Save parsed financial data
        var financialData = new BsonDocument
        {
            { "company", xbrlDoc["company"].AsBsonDocument },
            { "filing_date", xbrlDoc["filing_date"].AsString },
            { "metrics", metrics },
            { "parsed_at", DateTime.UtcNow }
        };

        financialDataCollection.InsertOne(financialData);
        Console.WriteLine("Parsed and saved financial data successfully.");
    }

    static XNamespace? GetUsGaapNamespace(XDocument xDoc)
    {
        var xbrlElement = xDoc.Root;
        if (xbrlElement != null)
        {
            var usGaapNamespace = xbrlElement.Attributes()
                .FirstOrDefault(attr => attr.IsNamespaceDeclaration && attr.Name.LocalName == "us-gaap")?.Value;
            if (usGaapNamespace != null)
            {
                return XNamespace.Get(usGaapNamespace);
            }
        }

        return null;
    }

    static double ExtractXBRLMetric(XDocument xDoc, XNamespace usGaapNamespace, string metricName)
    {
        // Find the element with the specified metric name
        var element = xDoc.
            Descendants(usGaapNamespace + metricName).
            FirstOrDefault();
        if (element == null)
        {
            Console.WriteLine($"Metric '{metricName}' not found.");
            return double.NaN; // Return NaN if metric is not found
        }

        // Parse the value as a double
        if (double.TryParse(element.Value, out double result))
        {
            return result;
        }
        else
        {
            Console.WriteLine($"Failed to parse value for metric '{metricName}': {element.Value}");
            return double.NaN;
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

    static async Task EnsureBrowser()
    {
        Console.WriteLine("Downloading Chromium...");
        var browserFetcher = new BrowserFetcher();
        var latestRevision = await browserFetcher.DownloadAsync();

        if (latestRevision == null)
        {
            Console.WriteLine("Failed to download Chromium.");
            return;
        }

        Console.WriteLine("Chromium downloaded successfully. Build id: {0}", latestRevision.BuildId);

        // Get all downloaded revisions
        var installedBrowsers = browserFetcher.GetInstalledBrowsers();

        // Delete older revisions
        foreach (var browser in installedBrowsers)
        {
            if (browser.BuildId == latestRevision.BuildId) continue;

            Console.WriteLine($"Removing old Chromium revision: {browser.BuildId}");
            browserFetcher.Uninstall(browser.BuildId);
        }
    }
}
