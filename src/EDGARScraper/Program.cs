using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DataModels;
using DataModels.XbrlFileModels;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using Serilog;
using Stocks.Persistence;
using Utilities;

using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace EDGARScraper;

internal class Program
{
    private const string DefaultPortStr = "7001";

    private static EdgarHttpClientService? _httpClientService;
    private static MongoDbService? _mongoDbService;

    private static EdgarHttpClientService HttpClientService => _httpClientService ??= new();
    private static MongoDbService MongoDbService => _mongoDbService ??= new();

    private static ILogger _log;
    private static IDbmService? _dbm;
    static Program()
    {
        _log = GetBootstrapLogger();
    }

    static async Task<int> Main(string[] args)
    {
        try
        {
            _log.LogInformation("Building the host");
            var host = BuildHost<Startup>(args);
            _log.LogInformation("Running the host");

            if (args.Length == 0)
            {
                Console.WriteLine("Please provide a command-line switch: --fetch, --parse, or --download");
                return 2;
            }

            await PuppeteerService.EnsureBrowser();
            await MongoDbService.CreateIndices();

            IServiceProvider svp = host.Services;
            _dbm = svp.GetRequiredService<IDbmService>();

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
                    _log.LogError("Invalid command-line switch. Please use --fetch, --parse, or --download");
                    return 3;
            }

            return 0;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Service execution is terminated with an error");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static ILogger GetBootstrapLogger()
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateBootstrapLogger();

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(Log.Logger);
        });

        var serviceProvider = serviceCollection.BuildServiceProvider();
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        return loggerFactory.CreateLogger<Program>();
    }

    private static IHost BuildHost<TStartup>(string[] args) where TStartup : class
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(webBuilder => { webBuilder.UseStartup<TStartup>(); })
            .ConfigureServices((context, services) => {

                var grpcPort = int.Parse(context.Configuration!.GetSection("Ports")["Grpc"] ?? DefaultPortStr, CultureInfo.InvariantCulture);

                services
                    .Configure<KestrelServerOptions>(opt =>
                    {
                        opt.ListenAnyIP(grpcPort, options => options.Protocols = HttpProtocols.Http2);
                        opt.AllowAlternateSchemes = true;
                    });

                services
                    .AddHttpClient()
                    .AddSingleton<PostgresExecutor>()
                    .AddSingleton<DbMigrations>();

                if (DoesConfigContainConnectionString(context.Configuration))
                    services.AddSingleton<IDbmService, DbmService>();

                services.AddGrpc();
            })
            .ConfigureLogging(builder => {

                Log.Logger = new LoggerConfiguration()
                    .WriteTo.Console()
                    .WriteTo.File(
                        "stocks-data-.txt",
                        rollingInterval: RollingInterval.Day,
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Properties:j} {Message:lj}{NewLine}{Exception}")
                    .CreateLogger();

                _ = builder
                    .ClearProviders()
                    .AddSerilog(Log.Logger);
            })
            .Build();

        _log = host.Services.GetRequiredService<ILogger<Program>>();
        LogConfig(host.Services.GetRequiredService<IConfiguration>());

        return host;
    }

    private static bool DoesConfigContainConnectionString(IConfiguration configuration)
        => configuration.GetConnectionString(DbmService.StocksDataConnectionStringName) is not null;

    private static void LogConfig(IConfiguration _)
    {
        //logger.LogInformation("==========BEGIN CRITICAL CONFIGURATION==========");
        //LogConfigSection(config, GoogleCredentialsOptions.GoogleCredentials);
        //LogConfigSection(config, HostedServicesOptions.HostedServices);
        //LogConfigSection(config, FeatureFlagsOptions.FeatureFlags);
        //logger.LogInformation("==========END CRITICAL CONFIGURATION==========");
    }

    //private static void LogConfigSection(IConfiguration config, string section)
    //{
    //    foreach (var child in config.GetSection(section).GetChildren())
    //        _log.LogInformation("[{Section}] {Key} = {Value}", section, child.Key, child.Value);
    //}

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
        const int BatchSize = 1000;
        const string url = "https://www.sec.gov/Archives/edgar/cik-lookup-data.txt";

        string? content = await HttpClientService.FetchContentAsync(url);
        if (string.IsNullOrEmpty(content))
        {
            _log.LogWarning("Failed to download CIK list from {Url}", url);
            return;
        }

        await _dbm!.EmptyCompaniesTables(CancellationToken.None);

        int i = 0;
        var companiesBatch = new List<Company>();
        var companyNamesBatch = new List<CompanyName>();
        var tasks = new List<Task>();
        var foundCiks = new HashSet<ulong>();

        int numApple = 0;

        string? line;
        using var reader = new StringReader(content);
        while ((line = reader.ReadLine()) != null)
        {
            ++i;

            if (i % 1000 == 0) _log.LogInformation("Processed {NumLines} lines", i);

            // Remove the trailing colon
            if (line.EndsWith(':')) line = line[..^1];

            int lastColonIndex = line.LastIndexOf(':');
            if (lastColonIndex == -1)
            {
                _log.LogWarning("Failed to parse line {Line}", line);
                continue;
            }

            string companyName = line[..lastColonIndex].Trim();
            string cikStr = line[(lastColonIndex + 1)..].Trim();

            if (string.IsNullOrEmpty(companyName) || string.IsNullOrEmpty(cikStr))
            {
                _log.LogWarning("Failed to parse line {Line}", line);
                continue;
            }

            if (!ulong.TryParse(cikStr, out ulong cik))
            {
                _log.LogWarning("Failed to parse CIK {Cik}", cikStr);
                continue;
            }

            if (foundCiks.Add(cik))
            {
                ulong companyId = await _dbm!.GetNextId64(CancellationToken.None);
                var company = new Company(companyId, cik, Constants.EdgarDataSource);
                companiesBatch.Add(company);

                if (cik == 320193)
                {
                    ++numApple;
                    if (numApple > 1)
                        _log.LogWarning("Found more than one entry for Apple Inc. (CIK: {Cik})", cik);
                }
            }

            ulong companyNameId = await _dbm!.GetNextId64(CancellationToken.None);
            var companyNameObj = new CompanyName(companyNameId, cik, companyName);
            companyNamesBatch.Add(companyNameObj);

            if (companiesBatch.Count >= BatchSize)
                SaveCompanyBatch(companiesBatch, tasks);

            if (companyNamesBatch.Count >= BatchSize)
                SaveCompanyNamesBatch(companyNamesBatch, tasks);
        }

        // Save any remaining objects

        if (companiesBatch.Count > 0)
            SaveCompanyBatch(companiesBatch, tasks);

        if (companyNamesBatch.Count > 0)
            SaveCompanyNamesBatch(companyNamesBatch, tasks);
        
        await Task.WhenAll(tasks);

        _log.LogInformation("Processed {NumLines} lines", i);
    }

    private static void SaveCompanyBatch(List<Company> companies, List<Task> tasks)
    {
        var batch = new List<Company>(companies);
        tasks.Add(Task.Run(async () =>
        {
            Results res = await _dbm!.SaveCompaniesBatch(batch, CancellationToken.None);
            if (res.IsError)
                _log.LogWarning("Failed to save batch of companies. Error: {Error}", res.ErrorMessage);
        }));
        companies.Clear();
    }

    private static void SaveCompanyNamesBatch(List<CompanyName> companyNames, List<Task> tasks)
    {
        var batch = new List<CompanyName>(companyNames);
        tasks.Add(Task.Run(async () =>
        {
            Results res = await _dbm!.SaveCompanyNamesBatch(batch, CancellationToken.None);
            if (res.IsError)
                _log.LogWarning("Failed to save batch of company names. Error: {Error}", res.ErrorMessage);
        }));
        companyNames.Clear();
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
