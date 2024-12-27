using DataModels;
using DataModels.XbrlFileModels;
using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Utilities;

namespace EDGARScraper;

internal class Program
{

    private static EdgarHttpClientService? _httpClientService;
    private static MongoDbService? _mongoDbService;

    private static EdgarHttpClientService HttpClientService => _httpClientService ??= new();
    private static MongoDbService MongoDbService => _mongoDbService ??= new();

    static async Task Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Please provide a command-line switch: --fetch, --parse, or --download");
            return;
        }

        await PuppeteerService.EnsureBrowser();
        await MongoDbService.CreateIndices();

        switch (args[0].ToLowerInvariant())
        {
            case "--fetch":
                await FetchRawData();
                break;
            case "--parse":
                await ParseMetadata();
                break;
            case "--download":
                await DownloadFilingDetailDocuments();
                break;
            case "--extract-xbrl-links":
                await ExtractXBRLLinks();
                await DownloadXBRLDocuments();
                break;
            case "--parse-financial":
                {
                    var xbrlParser = new XBRLParser(MongoDbService);
                    await xbrlParser.ParseXBRL();
                    break;
                }
            case "--get-full-cik-list":
                {
                    await DownloadAndSaveFullCikList();
                    break;
                }
            case "--parse-bulk-xbrl-archive":
                {
                    await ParseBulkXbrlArchive();
                    break;
                }
            default:
                Console.WriteLine("Invalid command-line switch. Please use --fetch, --parse, or --download");
                break;
        }
    }

    /// <summary>
    /// Fetches the landing page containing the list of filings for a company
    /// and saves it to MongoDB.
    /// </summary>
    private static async Task FetchRawData()
    {
        // EDGAR URL for filings (Example: Apple Inc.)
        string cik = "0000320193"; // Apple Inc.
        string url = $"https://www.sec.gov/cgi-bin/browse-edgar?action=getcompany&CIK={cik}&type=10-K&count=5";
        string renderedHtml = await PuppeteerService.FetchRenderedHtmlAsync(url);

        // Save rendered HTML to MongoDB
        await MongoDbService.SaveRawData(cik, url, renderedHtml);
    }

    static async Task ParseMetadata()
    {
        RawFilingData rawFilingData = await MongoDbService.GetRawFilingsRawData();

        if (string.IsNullOrEmpty(rawFilingData.RawHtml))
        {
            Console.WriteLine("No raw HTML found in the database.");
            return;
        }

        BsonArray filings = RawFilingParser.ParseRawFilingsToBsonArray(rawFilingData);
        await MongoDbService.SaveParsedFilings(rawFilingData, filings);
    }

    static async Task DownloadFilingDetailDocuments()
    {
        ParsedFilingData parsedFilingData = await MongoDbService.GetParsedFilings();

        foreach (BsonValue? filing in parsedFilingData.Filings)
        {
            BsonDocument filingDoc = filing.AsBsonDocument;
            FilingLink filingLink = FilingLink.FromBson(filingDoc);

            // Download filing document
            try
            {
                string? documentContent = await HttpClientService.FetchContentAsync(filingLink.DocumentLink);

                if (documentContent is null)
                {
                    Console.WriteLine($"Failed to download document: {filingLink.DocumentLink}");
                    continue;
                }

                var document = new BsonDocument
                {
                    { "company", parsedFilingData.CompanyBson },
                    { "filing_type", filingLink.FilingType },
                    { "filing_date", filingLink.FilingDate },
                    { "document_link", filingLink.DocumentLink },
                    { "content", documentContent },
                    { "downloaded_at", DateTime.UtcNow }
                };

                await MongoDbService.SaveFilingDetailDocuments(
                    parsedFilingData.Cik, filingLink.FilingDate, filingLink.FilingType, filingLink.DocumentLink, document);
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine("Failed to download document: {0}. Error: {1}", filingLink.DocumentLink, ex.Message);
            }
        }
    }

    static async Task ExtractXBRLLinks()
    {
        List<FilingDetails> filingDetailsList = await MongoDbService.GetFilingDetailDocuments();
        foreach (FilingDetails filingDetails in filingDetailsList)
        {
            if (string.IsNullOrEmpty(filingDetails.Cik)) continue;

            BsonDocument? document = FilingDetailsParser.ParseFilingDetailToBsonArray(filingDetails);

            if (document is null)
            {
                Console.WriteLine("No XBRL links found in the filing details for CIK: {0}", filingDetails.Cik);
                continue;
            }

            await MongoDbService.SaveXBRLLinks(filingDetails.Cik, filingDetails.FilingDate, document);
        }
    }

    static async Task DownloadXBRLDocuments()
    {
        List<CompanyXbrlLink> companyXbrlLinks = await MongoDbService.GetXBRLLinks();
        foreach (CompanyXbrlLink companyXbrlLink in companyXbrlLinks)
        {
            try
            {
                BsonDocument? xbrlDoc = await CompanyXbrlParser.ParseXbrlUrlToDocument(companyXbrlLink, HttpClientService);
                
                if (xbrlDoc is null)
                {
                    Console.WriteLine("Failed to download XBRL document (CIK: {0}, URL: {1})",
                        companyXbrlLink.Cik, companyXbrlLink.XbrlUrl);
                    continue;
                }

                await MongoDbService.SaveXBRLDocument(companyXbrlLink, xbrlDoc);
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine("Failed to download XBRL document: {0}. Error: {1}",
                    companyXbrlLink.XbrlUrl, ex.Message);
            }
        }
    }

    static async Task DownloadAndSaveFullCikList()
    {
        string url = "https://www.sec.gov/Archives/edgar/cik-lookup-data.txt";
        string? content = await HttpClientService.FetchContentAsync(url);
        if (string.IsNullOrEmpty(content))
        {
            Console.WriteLine("Failed to download CIK list from {0}", url);
            return;
        }

        using var reader = new StringReader(content);
        string? line;
        int i = 0;
        var companies = new List<Company>();
        const int batchSize = 1000;
        var tasks = new List<Task>();

        while ((line = reader.ReadLine()) != null)
        {
            ++i;

            if (i % 1000 == 0) Console.WriteLine("Processed {0} lines", i);

            // Remove the trailing colon
            if (line.EndsWith(':')) line = line[..^1];

            int lastColonIndex = line.LastIndexOf(':');
            if (lastColonIndex == -1)
            {
                Console.WriteLine("Failed to parse line {0}", line);
                continue;
            }

            string companyName = line[..lastColonIndex].Trim();
            string cikStr = line[(lastColonIndex + 1)..].Trim();

            if (string.IsNullOrEmpty(companyName) || string.IsNullOrEmpty(cikStr))
            {
                Console.WriteLine("Failed to parse line {0}", line);
                continue;
            }

            if (!int.TryParse(cikStr, out int cik))
            {
                Console.WriteLine("Failed to parse CIK {0}", cikStr);
                continue;
            }
            
            cikStr = $"{cik:0}";

            var company = new Company(cikStr, companyName, Constants.EdgarDataSource);
            companies.Add(company);

            if (companies.Count >= batchSize)
            {
                var batch = new List<Company>(companies);
                tasks.Add(Task.Run(async () =>
                {
                    Results res = await MongoDbService.SaveCompaniesBulk(batch);
                    if (res.IsError)
                        Console.WriteLine("Failed to save batch of companies. Error: {0}", res.ErrorMessage);
                }));
                companies.Clear();
            }
        }

        // Save any remaining companies
        if (companies.Count > 0)
        {
            var batch = new List<Company>(companies);
            tasks.Add(Task.Run(async () =>
            {
                Results res = await MongoDbService.SaveCompaniesBulk(batch);
                if (res.IsError)
                    Console.WriteLine("Failed to save batch of companies. Error: {0}", res.ErrorMessage);
            }));
        }

        await Task.WhenAll(tasks);

        Console.WriteLine("Processed {0} lines", i);
    }

    static async Task ParseBulkXbrlArchive()
    {
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string archivePath = Path.Combine(userProfile, "Downloads", "companyfacts.zip");

        using var zipReader = new ZipFileReader(archivePath);

        int i = 0;
        long totalLength = 0;
        var xbrlDataList = new List<(XbrlJson, IReadOnlyDictionary<string, Dictionary<string, Dictionary<DatePair, DataPoint>>>)>();
        const int batchSize = 100;
        var tasks = new List<Task>();

        foreach (string fileName in zipReader.EnumerateFileNames())
        {
            if (!fileName.EndsWith(".json")) continue;

            ++i;

            if ((i % 100) == 0)
                Console.WriteLine("Processed {0} files. Total length: {1:#,###} bytes", i, totalLength);

            try
            {
                string fileContent = zipReader.ExtractFileContent(fileName);
                totalLength += fileContent.Length;
                
                var parser = new XBRLFileParser(fileContent);
                Results res = parser.Parse();
                if (res.IsError)
                {
                    Console.WriteLine("Failed to parse {0}. Error: {1}", fileName, res.ErrorMessage);
                    continue;
                }

                xbrlDataList.Add((parser.XbrlJson!, parser.DataPoints));
                if (xbrlDataList.Count >= batchSize)
                {
                    var batch = new List<(XbrlJson, IReadOnlyDictionary<string, Dictionary<string, Dictionary<DatePair, DataPoint>>>)>(xbrlDataList);
                    tasks.Add(Task.Run(async () =>
                    {
                        Results res = await MongoDbService.SaveXbrlFileDataBatch(batch);
                        if (res.IsError)
                            Console.WriteLine("Failed to save batch of company data. Error: {0}", res.ErrorMessage);
                    }));
                    xbrlDataList.Clear();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to process {0}. Error: {1}", fileName, ex.Message);
            }
        }

        // Save any remaining data
        if (xbrlDataList.Count > 0)
        {
            var batch = new List<(XbrlJson, IReadOnlyDictionary<string, Dictionary<string, Dictionary<DatePair, DataPoint>>>)>(xbrlDataList);
            tasks.Add(Task.Run(async () =>
            {
                Results res = await MongoDbService.SaveXbrlFileDataBatch(batch);
                if (res.IsError)
                    Console.WriteLine("Failed to save batch of company data. Error: {0}", res.ErrorMessage);
            }));
        }

        await Task.WhenAll(tasks);

        Console.WriteLine("Processed {0} files. Total length: {1:#,###} bytes", i, totalLength);
    }
}
