using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Elastic.Channels;
using Elastic.Ingest.Elasticsearch;
using Elastic.Ingest.Elasticsearch.DataStreams;
using Elastic.Serilog.Sinks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Stocks.DataModels;
using Stocks.DataModels.EdgarFileModels;
using Stocks.Persistence;
using Stocks.Shared;

using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace EDGARScraper;

internal class Program
{
    private const string DefaultPortStr = "7001";
    private const int ParseBulkEdgarSubmissionsBatchSize = 1000;
    private const int ParseBulkXbrlNumDataPointsBatchSize = 1000;

    private static EdgarHttpClientService? _httpClientService;
    private static EdgarHttpClientService HttpClientService => _httpClientService ??= new();

    private static ILogger _logger;
    private static IDbmService? _dbm;
    private static IServiceProvider? _svp;

    static Program()
    {
        _logger = HostingUtils.GetBootstrapLogger<Program>();
    }

    static async Task<int> Main(string[] args)
    {
        try
        {
            DateTime start = DateTime.UtcNow;

            _logger.LogInformation("Building the host");
            var host = BuildHost<Startup>(args);
            _logger.LogInformation("Running the host");

            if (args.Length == 0)
            {
                _logger.LogWarning("Please provide a command-line switch: --fetch, --parse, or --download");
                return 2;
            }

            _logger.LogInformation("Command line: {CommandLine}", string.Join(" ", args));

            _svp = host.Services;
            _dbm = _svp.GetRequiredService<IDbmService>();

            switch (args[0].ToLowerInvariant())
            {
                case "--drop-all-tables":
                    {
                        await _dbm!.DropAllTables(CancellationToken.None);
                        break;
                    }
                case "--get-full-cik-list":
                    {
                        await DownloadAndSaveFullCikList();
                        break;
                    }
                case "--parse-bulk-edgar-submissions-list":
                    {
                        await ParseBulkEdgarSubmissionsList();
                        break;
                    }
                case "--parse-bulk-xbrl-archive":
                    {
                        await ParseBulkXbrlArchive();
                        break;
                    }
                case "--run-all":
                    {
                        await DownloadAndSaveFullCikList();
                        await ParseBulkEdgarSubmissionsList();
                        await ParseBulkXbrlArchive();
                        break;
                    }
                default:
                    _logger.LogError("Invalid command-line switch. Please use --get-full-cik-list, or --parse-bulk-xbrl-archive");
                    return 3;
            }

            DateTime end = DateTime.UtcNow;
            TimeSpan timeTaken = end - start;
            _logger.LogInformation("Service execution completed in {TimeTaken}s", timeTaken.TotalSeconds);

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

    private static IHost BuildHost<TStartup>(string[] args) where TStartup : class
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            })
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
                    .AddTransient<Func<string, ParseBulkXbrlArchiveContext, XBRLFileParser>>(
                        sp => (content, context) => new XBRLFileParser(content, context, sp));

                if (DoesConfigContainConnectionString(context.Configuration))
                    services.AddSingleton<IDbmService, DbmService>();

                services.AddGrpc();
            })
            .ConfigureLogging((context, builder) => builder.ClearProviders())
            .UseSerilog((context, LoggerConfiguration) =>
                LoggerConfiguration.ReadFrom.Configuration(context.Configuration)
            )
            .Build();

        _svp = host.Services;
        _logger = _svp.GetRequiredService<ILogger<Program>>();
        LogConfig(_svp.GetRequiredService<IConfiguration>());

        return host;
    }

    private static bool DoesConfigContainConnectionString(IConfiguration configuration)
        => configuration.GetConnectionString(DbmService.StocksDataConnectionStringName) is not null;

    private static void LogConfig(IConfiguration _)
    {
        _logger.LogInformation("==========BEGIN CRITICAL CONFIGURATION==========");
        _logger.LogInformation("CompanyFactsBulkZipPath: {CompanyFactsBulkZipPath}", GetConfigValue("CompanyFactsBulkZipPath"));
        _logger.LogInformation("EdgarSubmissionsBulkZipPath: {EdgarSubmissionsBulkZipPath}", GetConfigValue("EdgarSubmissionsBulkZipPath"));
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

    private static async Task ParseBulkEdgarSubmissionsList()
    {
        string archivePath = GetConfigValue("EdgarSubmissionsBulkZipPath");

        using var zipReader = new ZipFileReader(archivePath);

        var context = new ParseBulkEdgarSubmissionsContext(_svp!);

        GenericResults<IReadOnlyCollection<Company>> companyResults =
            await _dbm!.GetCompaniesByDataSource(ModelsConstants.EdgarDataSource, CancellationToken.None);
        if (companyResults.IsError)
        {
            _logger.LogError("ParseBulkEdgarSubmissionsList - Failed to get companies. Error: {Error}", companyResults.ErrorMessage);
            return;
        }

        foreach (Company c in companyResults.Data!)
            context.CompanyIdsByCiks[c.Cik] = c.CompanyId;

        foreach (string fileName in zipReader.EnumerateFileNames())
        {
            if (!fileName.EndsWith(".json")) continue;
            
            context.IsCurrentFileSubmissionsFile = fileName.Contains("-submissions-");
            ++context.NumFiles;
            if ((context.NumFiles % 100) == 0) context.LogProgress();

            context.CurrentFileName = fileName;
            await ParseOneFileSubmission(zipReader, context);
        }

        // Save any remaining objects

        if (context.SubmissionsBatch.Count != 0)
            context.NumSubmissions += BulkInsertSubmissionsAndClearBatch(context.SubmissionsBatch, context.Tasks);

        await Task.WhenAll(context.Tasks);

        context.LogProgress();
    }

    private static async Task ParseOneFileSubmission(ZipFileReader zipReader, ParseBulkEdgarSubmissionsContext context)
    {
        try
        {
            string fileContent = zipReader.ExtractFileContent(context.CurrentFileName);
            context.TotalLength += fileContent.Length;

            (FilingsDetails? filingsDetails, ulong companyId) = GetFilingsDetails(fileContent, context);
            if (filingsDetails is null)
            {
                _logger.LogWarning("ParseOneFileSubmission - Failed to parse {FileName}", context.CurrentFileName);
                return;
            }

            if (companyId == ulong.MaxValue)
            {
                _logger.LogWarning("ParseOneFileSubmission - Failed to find company ID for {FileName}", context.CurrentFileName);
                return;
            }

            IReadOnlyCollection<Submission> submissions = context.JsonConverter.ToSubmissions(filingsDetails);
            foreach (Submission s in submissions)
            {
                ulong submissionId = await _dbm!.GetNextId64(CancellationToken.None);
                Submission submissionToInsert = s with { SubmissionId = submissionId, CompanyId = companyId };
                context.SubmissionsBatch.Add(submissionToInsert);

                if (context.SubmissionsBatch.Count >= ParseBulkEdgarSubmissionsBatchSize)
                    context.NumSubmissions += BulkInsertSubmissionsAndClearBatch(context.SubmissionsBatch, context.Tasks);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ParseOneFileSubmission - Failed to process {FileName}", context.CurrentFileName);
        }
    }

    private static (FilingsDetails?, ulong) GetFilingsDetails(string fileContent, ParseBulkEdgarSubmissionsContext context)
    {
        if (context.IsCurrentFileSubmissionsFile)
        {
            FilingsDetails? filingsDetails = JsonSerializer.Deserialize<FilingsDetails>(fileContent, Conventions.DefaultOptions);

            // Get company CIK from the file name, e.g. CIK0000829323-submissions-001.json
            if (context.CurrentFileName.Length < 13)
                return (filingsDetails, ulong.MaxValue);

            string cikStr = context.CurrentFileName[..13][3..];
            if (!ulong.TryParse(cikStr, out ulong cik))
                return (filingsDetails, ulong.MaxValue);

            if (!context.CompanyIdsByCiks.TryGetValue(cik, out ulong companyId))
                return (filingsDetails, ulong.MaxValue);

            return (filingsDetails, companyId);
        }
        else
        {
            RecentFilingsContainer? submissionsJson = JsonSerializer.Deserialize<RecentFilingsContainer>(fileContent, Conventions.DefaultOptions);
            
            if (submissionsJson is null) return (null, ulong.MaxValue);

            FilingsDetails filingsDetails = submissionsJson.Filings.Recent;

            if (!context.CompanyIdsByCiks.TryGetValue(submissionsJson.Cik, out ulong companyId))
                return (filingsDetails, ulong.MaxValue);
            else
                return (filingsDetails, companyId);
        }
    }

    private static int BulkInsertSubmissionsAndClearBatch(List<Submission> submissionsBatch, List<Task> tasks)
    {
        int numDataPointsInBatch = submissionsBatch.Count;

        BulkInsertSubmissions(submissionsBatch, tasks);
        submissionsBatch.Clear();

        return numDataPointsInBatch;
    }

    private static void BulkInsertSubmissions(List<Submission> submissionsBatch, List<Task> tasks)
    {
        var batch = new List<Submission>(submissionsBatch);

        tasks.Add(Task.Run(async () =>
        {
            Results res = await _dbm!.BulkInsertSubmissions(batch, CancellationToken.None);
            if (res.IsError)
                _logger.LogWarning("Failed to save batch of submissions. Error: {Error}", res.ErrorMessage);
        }));
    }

    private static async Task ParseBulkXbrlArchive()
    {
        string archivePath = GetConfigValue("CompanyFactsBulkZipPath");

        using var zipReader = new ZipFileReader(archivePath);

        var context = new ParseBulkXbrlArchiveContext(_svp!);

        GenericResults<IReadOnlyCollection<Company>> companyResults =
            await _dbm!.GetCompaniesByDataSource(ModelsConstants.EdgarDataSource, CancellationToken.None);
        if (companyResults.IsError)
        {
            _logger.LogError("ParseBulkXbrlArchive - Failed to get companies. Error: {Error}", companyResults.ErrorMessage);
            return;
        }

        foreach (Company c in companyResults.Data!)
            context.CompanyIdsByCik[c.Cik] = c.CompanyId;

        GenericResults<IReadOnlyCollection<DataPointUnit>> dpuResults =
            await _dbm!.GetDataPointUnits(CancellationToken.None);
        if (dpuResults.IsError)
        {
            _logger.LogError("ParseBulkXbrlArchive - Failed to get data point units. Error: {Error}", dpuResults.ErrorMessage);
            return;
        }

        foreach (DataPointUnit dpu in dpuResults.Data!)
            context.UnitsByUnitName[dpu.UnitName] = dpu;

        GenericResults<IReadOnlyCollection<Submission>> submissionResults =
            await _dbm!.GetSubmissions(CancellationToken.None);
        if (submissionResults.IsError)
        {
            _logger.LogError("ParseBulkXbrlArchive - Failed to get submissions. Error: {Error}", submissionResults.ErrorMessage);
            return;
        }

        foreach (Submission s in submissionResults.Data!)
        {
            List<Submission> companySubmissions = context.SubmissionsByCompanyId.GetOrCreateEntry(s.CompanyId);
            companySubmissions.Add(s);
        }

        foreach (string fileName in zipReader.EnumerateFileNames())
        {
            if (!fileName.EndsWith(".json")) continue;

            ++context.NumFiles;
            if ((context.NumFiles % 100) == 0) context.LogProgress();

            context.CurrentFileName = fileName;
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

            XBRLFileParser parser = CreateParser(fileContent);
            XBRLParserResult res = parser.Parse();
            if (res.IsError)
            {
                LogParseFailure(res);
                return;
            }

            foreach (DataPoint dp in parser.DataPoints)
                await ProcessDataPoint(dp, context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ParseOneFileXBRL - Failed to process {FileName}", context.CurrentFileName);
        }

        // Local helper methods

        XBRLFileParser CreateParser(string fileContent)
        {
            var parserFactory = _svp!.GetRequiredService<Func<string, ParseBulkXbrlArchiveContext, XBRLFileParser>>();
            return parserFactory(fileContent, context);
        }

        void LogParseFailure(XBRLParserResult res)
        {
            const string msgTemplate = "ParseOneFileXBRL - Failed to parse {FileName}. Error: {ErrMsg}";
            if (res.IsWarningLevel)
                _logger.LogWarning(msgTemplate, context.CurrentFileName, res.ErrorMessage);
            else
                _logger.LogInformation(msgTemplate, context.CurrentFileName, res.ErrorMessage);
        }
    }

    private static async Task ProcessDataPoint(DataPoint dp, ParseBulkXbrlArchiveContext context)
    {
        if (!context.SubmissionsByCompanyId.TryGetValue(dp.CompanyId, out List<Submission>? submissions))
        {
            _logger.LogWarning("ParseOneFileXBRL:ProcessDataPoint - Failed to find submissions for company: {CompanyId},{DataPoint}",
                dp.CompanyId, dp);
            return;
        }

        GenericResults<Submission> findSubmissionForDataPointResults = CorrelateDataPointToSubmission(dp, submissions);
        if (findSubmissionForDataPointResults.IsError)
        {
            _logger.LogWarning("ParseOneFileXBRL:ProcessDataPoint - Failed to find submission for data point. {CompanyId},{DataPoint}. Error: {Error}",
                dp.CompanyId, dp, findSubmissionForDataPointResults.ErrorMessage);
            return;
        }

        string unitName = dp.Units.UnitNameNormalized;
        if (!context.UnitsByUnitName.TryGetValue(unitName, out DataPointUnit? dpu))
        {
            ++context.NumDataPointUnits;
            ulong dpuId = await _dbm!.GetNextId64(CancellationToken.None);
            dpu = context.UnitsByUnitName[unitName] = new DataPointUnit(dpuId, unitName);
            _ = await _dbm.InsertDataPointUnit(dpu, CancellationToken.None);
            _logger.LogInformation("ParseOneFileXBRL:ProcessDataPoint - Inserted data point unit: {DataPointUnit}", dpu);
        }

        ulong dpId = await _dbm!.GetNextId64(CancellationToken.None);
        DataPoint dataPointToInsert = dp with { DataPointId = dpId, Units = dpu }; // With data point unit id populated
        context.DataPointsBatch.Add(dataPointToInsert);

        if (context.DataPointsBatch.Count >= ParseBulkXbrlNumDataPointsBatchSize)
            context.NumDataPoints += BulkInsertDataPointsAndClearBatch(context.DataPointsBatch, context.Tasks);
    }

    private static GenericResults<Submission> CorrelateDataPointToSubmission(DataPoint dp, List<Submission> submissions)
    {
        foreach (var submission in submissions)
        {
            if (dp.FilingReference == submission.FilingReference)
                return GenericResults<Submission>.SuccessResult(submission);
        }

        return GenericResults<Submission>.FailureResult("No matching submission found for the given data point.");
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
