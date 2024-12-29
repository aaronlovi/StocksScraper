using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Stocks.DataModels;
using Stocks.Persistence;
using Utilities;

using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace EDGARScraper;

internal class Program
{
    private const string DefaultPortStr = "7001";
    private const int ParseBulkXbrlNumDataPointsBatchSize = 1000;

    private static EdgarHttpClientService? _httpClientService;
    private static EdgarHttpClientService HttpClientService => _httpClientService ??= new();

    private static ILogger _logger;
    private static IDbmService? _dbm;
    private static IServiceProvider? _svp;

    static Program()
    {
        _logger = GetBootstrapLogger();
    }

    static async Task<int> Main(string[] args)
    {
        try
        {
            _logger.LogInformation("Building the host");
            var host = BuildHost<Startup>(args);
            _logger.LogInformation("Running the host");

            if (args.Length == 0)
            {
                Console.WriteLine("Please provide a command-line switch: --fetch, --parse, or --download");
                return 2;
            }

            _svp = host.Services;
            _dbm = _svp.GetRequiredService<IDbmService>();

            switch (args[0].ToLowerInvariant())
            {
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
                    _logger.LogError("Invalid command-line switch. Please use --get-full-cik-list, or --parse-bulk-xbrl-archive");
                    return 3;
            }

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Service execution is terminated with an error");
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
                    .AddSingleton<DbMigrations>()
                    .AddTransient<Func<string, Dictionary<ulong, ulong>, XBRLFileParser>>(
                        sp => (content, companyIdsByCiks) => new XBRLFileParser(content, companyIdsByCiks, sp));

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

        _logger = host.Services.GetRequiredService<ILogger<Program>>();
        LogConfig(host.Services.GetRequiredService<IConfiguration>());

        return host;
    }

    private static bool DoesConfigContainConnectionString(IConfiguration configuration)
        => configuration.GetConnectionString(DbmService.StocksDataConnectionStringName) is not null;

    private static void LogConfig(IConfiguration _)
    {
        _logger.LogInformation("==========BEGIN CRITICAL CONFIGURATION==========");
        _logger.LogInformation("CompanyFactsBulkZipPath: {CompanyFactsBulkZipPath}", GetConfigValue("CompanyFactsBulkZipPath"));
        //LogConfigSection(config, GoogleCredentialsOptions.GoogleCredentials);
        //LogConfigSection(config, HostedServicesOptions.HostedServices);
        //LogConfigSection(config, FeatureFlagsOptions.FeatureFlags);
        _logger.LogInformation("==========END CRITICAL CONFIGURATION==========");
    }

    static async Task DownloadAndSaveFullCikList()
    {
        const int BatchSize = 1000;
        const string url = "https://www.sec.gov/Archives/edgar/cik-lookup-data.txt";

        string? content = await HttpClientService.FetchContentAsync(url);
        if (string.IsNullOrEmpty(content))
        {
            _logger.LogWarning("Failed to download CIK list from {Url}", url);
            return;
        }

        await _dbm!.EmptyCompaniesTables(CancellationToken.None);

        int i = 0;
        int numCompanies = 0;
        int numCompanyNames = 0;
        var companiesBatch = new List<Company>();
        var companyNamesBatch = new List<CompanyName>();
        var tasks = new List<Task>();
        var companyIdsByCiks = new Dictionary<ulong, ulong>(); // CIK -> CompanyId

        string? line;
        using var reader = new StringReader(content);
        while ((line = reader.ReadLine()) != null)
        {
            ++i;

            if (i % 1000 == 0) _logger.LogInformation("Processed {NumLines} lines", i);

            // Remove the trailing colon
            if (line.EndsWith(':')) line = line[..^1];

            int lastColonIndex = line.LastIndexOf(':');
            if (lastColonIndex == -1)
            {
                _logger.LogWarning("Failed to parse line {Line}", line);
                continue;
            }

            string companyName = line[..lastColonIndex].Trim();
            string cikStr = line[(lastColonIndex + 1)..].Trim();

            if (string.IsNullOrEmpty(companyName) || string.IsNullOrEmpty(cikStr))
            {
                _logger.LogWarning("Failed to parse line {Line}", line);
                continue;
            }

            if (!ulong.TryParse(cikStr, out ulong cik))
            {
                _logger.LogWarning("Failed to parse CIK {Cik}", cikStr);
                continue;
            }

            if (!companyIdsByCiks.TryGetValue(cik, out ulong companyId))
            {
                companyId = await _dbm!.GetNextId64(CancellationToken.None);
                companyIdsByCiks.Add(cik, companyId);
                var company = new Company(companyId, cik, ModelsConstants.EdgarDataSource);
                companiesBatch.Add(company);
            }

            ulong companyNameId = await _dbm!.GetNextId64(CancellationToken.None);
            var companyNameObj = new CompanyName(companyNameId, companyId, companyName);
            companyNamesBatch.Add(companyNameObj);

            if (companiesBatch.Count >= BatchSize) BulkInsertCompaniesAndClearBatch();
            if (companyNamesBatch.Count >= BatchSize) BulkInsertCompanyNamesAndClearBatch();
        }

        // Save any remaining objects

        if (companiesBatch.Count > 0) BulkInsertCompaniesAndClearBatch();
        if (companyNamesBatch.Count > 0) BulkInsertCompanyNamesAndClearBatch();

        await Task.WhenAll(tasks);

        _logger.LogInformation("Processed {NumLines} lines; {NumCompanies} companies; {NumCompanyNames} company Names",
            i, numCompanies, numCompanyNames);

        // Local helper methods

        void BulkInsertCompaniesAndClearBatch()
        {
            numCompanies += companiesBatch.Count;
            BulkInsertCompanies(companiesBatch, tasks);
            companiesBatch.Clear();
        }

        void BulkInsertCompanyNamesAndClearBatch()
        {
            numCompanyNames += companyNamesBatch.Count;
            BulkInsertCompanyNames(companyNamesBatch, tasks);
            companyNamesBatch.Clear();
        }
    }

    private static void BulkInsertCompanies(IReadOnlyCollection<Company> companies, List<Task> tasks)
    {
        var batch = new List<Company>(companies);
        tasks.Add(Task.Run(async () =>
        {
            Results res = await _dbm!.BulkInsertCompanies(batch, CancellationToken.None);
            if (res.IsError)
                _logger.LogWarning("Failed to save batch of companies. Error: {Error}", res.ErrorMessage);
        }));
    }

    private static void BulkInsertCompanyNames(IReadOnlyCollection<CompanyName> companyNames, List<Task> tasks)
    {
        var batch = new List<CompanyName>(companyNames);
        tasks.Add(Task.Run(async () =>
        {
            Results res = await _dbm!.BulkInsertCompanyNames(batch, CancellationToken.None);
            if (res.IsError)
                _logger.LogWarning("Failed to save batch of company names. Error: {Error}", res.ErrorMessage);
        }));
    }

    private class ParseBulkXbrlArchiveContext
    {
        public ParseBulkXbrlArchiveContext()
        {
            CurrentFileName = string.Empty;
            DataPointsBatch = [];
            Tasks = [];
            CompanyIdsByCiks = [];
            UnitsByUnitName = [];
        }

        public int NumFiles { get; set; }
        public int NumDataPoints { get; set; }
        public int NumDataPointUnits { get; set; }
        public long TotalLength { get; set; }
        public string CurrentFileName { get; set; }
        public List<DataPoint> DataPointsBatch { get; init; }
        public List<Task> Tasks { get; init; }
        public Dictionary<ulong, ulong> CompanyIdsByCiks { get; init; }
        public Dictionary<string, DataPointUnit> UnitsByUnitName { get; init; }

        public void LogProgress()
        {
            _logger.LogInformation("Processed {NumFiles} files; {NumDataPoints} data points; {NumDataPointUnits} data point units; Total length: {TotalLength} bytes",
                NumFiles, NumDataPoints, NumDataPointUnits, TotalLength);
        }
    }

    private static async Task ParseBulkXbrlArchive()
    {
        string archivePath = GetConfigValue("CompanyFactsBulkZipPath");

        using var zipReader = new ZipFileReader(archivePath);

        var context = new ParseBulkXbrlArchiveContext();

        GenericResults<IReadOnlyCollection<Company>> companyResults =
            await _dbm!.GetCompaniesByDataSource(ModelsConstants.EdgarDataSource, CancellationToken.None);
        if (companyResults.IsError)
        {
            _logger.LogError("ParseBulkXbrlArchive - Failed to get companies. Error: {Error}", companyResults.ErrorMessage);
            return;
        }

        foreach (Company c in companyResults.Data!)
            context.CompanyIdsByCiks[c.Cik] = c.CompanyId;

        GenericResults<IReadOnlyCollection<DataPointUnit>> dpuResults =
            await _dbm!.GetDataPointUnits(CancellationToken.None);
        if (dpuResults.IsError)
        {
            _logger.LogError("ParseBulkXbrlArchive - Failed to get data point units. Error: {Error}", dpuResults.ErrorMessage);
            return;
        }

        foreach (DataPointUnit dpu in dpuResults.Data!)
            context.UnitsByUnitName[dpu.UnitName] = dpu;

        foreach (string fileName in zipReader.EnumerateFileNames())
        {
            if (!fileName.EndsWith(".json")) continue;

            ++context.NumFiles;
            if ((context.NumFiles % 100) == 0) context.LogProgress();

            await ParseOneFileXBRL(zipReader, context);
        }

        // Save any remaining objects

        if (context.DataPointsBatch.Count != 0)
            context.NumDataPoints += BulkInsertDataPointsAndClearBatch(context.DataPointsBatch, context.Tasks);

        await Task.WhenAll(context.Tasks);

        context.LogProgress();
    }

    private static async Task ParseOneFileXBRL(ZipFileReader zipReader, ParseBulkXbrlArchiveContext context)
    {
        try
        {
            string fileContent = zipReader.ExtractFileContent(context.CurrentFileName);
            context.TotalLength += fileContent.Length;

            var parserFactory = _svp!.GetRequiredService<Func<string, Dictionary<ulong, ulong>, XBRLFileParser>>();
            XBRLFileParser parser = parserFactory(fileContent, context.CompanyIdsByCiks);
            Results res = parser.Parse();
            if (res.IsError)
            {
                _logger.LogWarning("ParseOneFileXBRL - Failed to parse {FileName}. Error: {ErrMsg}",
                    context.CurrentFileName, res.ErrorMessage);
                return;
            }

            foreach (DataPoint dp in parser.DataPoints)
            {
                string unitName = dp.Units.UnitNameNormalized;
                if (!context.UnitsByUnitName.TryGetValue(unitName, out DataPointUnit? dpu))
                {
                    ++context.NumDataPointUnits;
                    ulong dpuId = await _dbm!.GetNextId64(CancellationToken.None);
                    dpu = context.UnitsByUnitName[unitName] = new DataPointUnit(dpuId, unitName);
                    var insertDpuRes = await _dbm.InsertDataPointUnit(dpu, CancellationToken.None);
                    _logger.LogInformation("ParseOneFileXBRL - Inserted data point unit: {DataPointUnit}", dpu);
                }

                ulong dpId = await _dbm!.GetNextId64(CancellationToken.None);
                DataPoint dataPointToInsert = dp with { DataPointId = dpId, Units = dpu }; // With data point unit id populated
                context.DataPointsBatch.Add(dataPointToInsert);

                if (context.DataPointsBatch.Count >= ParseBulkXbrlNumDataPointsBatchSize)
                    context.NumDataPoints += BulkInsertDataPointsAndClearBatch(context.DataPointsBatch, context.Tasks);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ParseOneFileXBRL - Failed to process {FileName}", context.CurrentFileName);
        }
    }

    private static int BulkInsertDataPointsAndClearBatch(List<DataPoint> dataPointsBatch, List<Task> tasks)
    {
        int numDataPointsInBatch = dataPointsBatch.Count;

        BulkInsertDataPoints(dataPointsBatch, tasks);
        dataPointsBatch.Clear();

        return numDataPointsInBatch;
    }

    private static void BulkInsertDataPoints(IReadOnlyCollection<DataPoint> dataPointsBatch, List<Task> tasks)
    {
        var batch = new List<DataPoint>(dataPointsBatch);

        tasks.Add(Task.Run(async () =>
        {
            Results res = await _dbm!.BulkInsertDataPoints(batch, CancellationToken.None);
            if (res.IsError)
                _logger.LogWarning("Failed to save batch of data points. Error: {Error}", res.ErrorMessage);
        }));
    }

    private static string GetConfigValue(string key)
    {
        if (_svp is null)
            throw new InvalidOperationException("Service provider is not initialized");
        IConfiguration configuration = _svp.GetRequiredService<IConfiguration>();
        return configuration[key] ?? throw new InvalidOperationException($"Configuration key '{key}' not found");
    }
}
