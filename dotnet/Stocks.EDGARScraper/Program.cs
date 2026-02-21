using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using EDGARScraper.Services;
using EDGARScraper.Options;
using Stocks.DataModels;
using Stocks.DataModels.EdgarFileModels;
using Stocks.DataModels.Enums;
using Stocks.EDGARScraper.Models.Statements;
using Stocks.EDGARScraper.Services.Statements;
using Stocks.EDGARScraper.Services.Taxonomies;
using Stocks.Persistence.Database;
using Stocks.Persistence.Database.DTO.Taxonomies;
using Stocks.Persistence.Database.Migrations;
using Stocks.Shared;
using Stocks.Shared.Models;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace EDGARScraper;

internal partial class Program {
    private const string DefaultPortStr = "7001";
    private const int ParseBulkEdgarSubmissionsBatchSize = 1000;
    private const int ParseBulkXbrlNumDataPointsBatchSize = 1000;

    private static EdgarHttpClientService? _httpClientService;
    private static EdgarHttpClientService HttpClientService => _httpClientService ??= new();

    private static ILogger _logger;
    private static IDbmService? _dbm;
    private static IServiceProvider? _svp;

    static Program() {
        _logger = HostingUtils.GetBootstrapLogger<Program>();
    }

    private static async Task<int> Main(string[] args) {
        try {
            DateTime start = DateTime.UtcNow;

            _logger.LogInformation("Building the host");
            IHost host = BuildHost<Startup>(args);
            _logger.LogInformation("Running the host");

            if (args.Length == 0) {
                _logger.LogWarning("Please provide a command-line switch: --fetch, --parse, or --download");
                return 2;
            }

            _logger.LogInformation("Command line: {CommandLine}", string.Join(" ", args));

            _svp = host.Services;
            _dbm = _svp.GetRequiredService<IDbmService>();

            int result = await HandleCommandAsync(args);

            DateTime end = DateTime.UtcNow;
            TimeSpan timeTaken = end - start;
            _logger.LogInformation("Service execution completed in {TimeTaken}s", timeTaken.TotalSeconds);

            return result;
        } catch (Exception ex) {
            _logger.LogError(ex, "Service execution is terminated with an error");
            return 1;
        } finally {
            Log.CloseAndFlush();
        }
    }

    private static async Task<int> HandleCommandAsync(string[] args) {
        switch (args[0].ToLowerInvariant()) {
            case "--drop-all-tables": {
                _ = await _dbm!.DropAllTables(CancellationToken.None);
                return 0;
            }
            case "--get-full-cik-list": {
                await DownloadAndSaveFullCikList();
                return 0;
            }
            case "--parse-bulk-edgar-submissions-list": {
                await ParseBulkEdgarSubmissionsList();
                return 0;
            }
            case "--parse-bulk-xbrl-archive": {
                await ParseBulkXbrlArchive();
                return 0;
            }
            case "--run-all": {
                await DownloadAndSaveFullCikList();
                await ParseBulkEdgarSubmissionsList();
                await ParseBulkXbrlArchive();
                return 0;
            }
            case "--load-taxonomy-concepts": {
                UsGaap2025ConceptsFileProcessor processor = _svp!.GetRequiredService<UsGaap2025ConceptsFileProcessor>();
                _ = await processor.Process();
                return 0;
            }
            case "--load-taxonomy-presentations": {
                UsGaap2025PresentationFileProcessor processor = _svp!.GetRequiredService<UsGaap2025PresentationFileProcessor>();
                _ = await processor.Process();
                return 0;
            }
            case "--load-taxonomy-year": {
                int year = ParseYearArg(args);
                if (year == 0) {
                    _logger.LogError("Missing or invalid --year for --load-taxonomy-year");
                    return 2;
                }
                Result res = await ImportTaxonomyYearAsync(year);
                if (res.IsFailure) {
                    _logger.LogError("ImportTaxonomyYear failed: {Error}", res.ErrorMessage);
                    return 2;
                }
                return 0;
            }
            case "--load-taxonomy-all": {
                Result res = await ImportAllTaxonomiesAsync();
                if (res.IsFailure) {
                    _logger.LogError("ImportAllTaxonomies failed: {Error}", res.ErrorMessage);
                    return 2;
                }
                return 0;
            }
            case "--print-statement": {
                return await HandlePrintStatementAsync(args);
            }
            case "--download-sec-ticker-mappings": {
                Result res = await DownloadSecTickerMappingsAsync();
                if (res.IsFailure) {
                    _logger.LogError("DownloadSecTickerMappings failed: {Error}", res.ErrorMessage);
                    return 2;
                }
                return 0;
            }
            case "--import-sec-ticker-mappings": {
                Result res = await ImportSecTickerMappingsAsync();
                if (res.IsFailure) {
                    _logger.LogError("ImportSecTickerMappings failed: {Error}", res.ErrorMessage);
                    return 2;
                }
                return 0;
            }
            case "--import-prices-stooq": {
                Result res = await ImportStooqPricesAsync();
                if (res.IsFailure) {
                    _logger.LogError("ImportStooqPrices failed: {Error}", res.ErrorMessage);
                    return 2;
                }
                return 0;
            }
            case "--import-prices-stooq-bulk": {
                Result res = await ImportStooqBulkPricesAsync(args);
                if (res.IsFailure) {
                    _logger.LogError("ImportStooqBulkPrices failed: {Error}", res.ErrorMessage);
                    return 2;
                }
                return 0;
            }
            case "--import-inline-xbrl-shares": {
                Result res = await ImportInlineXbrlSharesAsync();
                if (res.IsFailure) {
                    _logger.LogError("ImportInlineXbrlShares failed: {Error}", res.ErrorMessage);
                    return 2;
                }
                return 0;
            }
            case "--compute-all-scores": {
                Result res = await ComputeAndStoreAllScoresAsync();
                if (res.IsFailure) {
                    _logger.LogError("ComputeAllScores failed: {Error}", res.ErrorMessage);
                    return 2;
                }
                return 0;
            }
            default: {
                _logger.LogError("Invalid command-line switch. Please use --get-full-cik-list, or --parse-bulk-xbrl-archive");
                return 3;
            }
        }
    }

    private static async Task<int> HandlePrintStatementAsync(string[] args) {
        PrintStatementArgs parsed = ParsePrintStatementArgs(args);

        if (string.IsNullOrWhiteSpace(parsed.Cik) || (!parsed.ListStatements && (string.IsNullOrWhiteSpace(parsed.Concept) || parsed.Date == default || string.IsNullOrWhiteSpace(parsed.Format)))) {
            parsed = parsed with { ShowUsage = true };
        }

        if (parsed.ShowUsage) {
            Console.Error.WriteLine("USAGE: dotnet run --print-statement --cik <CIK> [--concept <ConceptName>] [--date <YYYY-MM-DD>] [--format <csv|html|json>] [--max-depth <N>] [--role <RoleName>] [--list-statements] [--taxonomy-year <YYYY>]");
            return 4;
        }

        int taxonomyYear = parsed.TaxonomyYear ?? parsed.Date.Year;
        Result<TaxonomyTypeInfo> taxonomyTypeResult = await _dbm!.GetTaxonomyTypeByNameVersion("us-gaap", taxonomyYear, CancellationToken.None);
        if (taxonomyTypeResult.IsFailure || taxonomyTypeResult.Value is null) {
            _logger.LogError("Could not find taxonomy type for us-gaap {Year}", taxonomyYear);
            Console.Error.WriteLine($"ERROR: Could not find taxonomy type for us-gaap {taxonomyYear}.");
            return 2;
        }
        int taxonomyTypeId = taxonomyTypeResult.Value.TaxonomyTypeId;

        var printer = new StatementPrinter(
            _dbm!,
            parsed.Cik!,
            parsed.Concept ?? string.Empty,
            parsed.Date,
            parsed.MaxDepth,
            parsed.Format,
            parsed.RoleName,
            parsed.ListStatements,
            taxonomyTypeId,
            Console.Out,
            Console.Error,
            CancellationToken.None
        );
        return await printer.PrintStatement();
    }

    private static PrintStatementArgs ParsePrintStatementArgs(string[] args) {
        string? cik = null;
        string? concept = null;
        DateOnly date = default;
        int maxDepth = 10;
        string format = "csv";
        string? roleName = null;
        bool listStatements = false;
        bool showUsage = false;
        int? taxonomyYear = null;

        for (int i = 1; i < args.Length; i++) {
            switch (args[i]) {
                case "--cik": {
                    if (i + 1 < args.Length)
                        cik = args[++i];
                    else
                        showUsage = true;
                    break;
                }
                case "--concept": {
                    if (i + 1 < args.Length)
                        concept = args[++i];
                    else
                        showUsage = true;
                    break;
                }
                case "--date": {
                    if (i + 1 < args.Length && DateOnly.TryParse(args[++i], out DateOnly d))
                        date = d;
                    else
                        showUsage = true;
                    break;
                }
                case "--max-depth": {
                    if (i + 1 < args.Length && int.TryParse(args[++i], out int md))
                        maxDepth = md;
                    else
                        showUsage = true;
                    break;
                }
                case "--format": {
                    if (i + 1 < args.Length)
                        format = args[++i].ToLowerInvariant();
                    else
                        showUsage = true;
                    break;
                }
                case "--role": {
                    if (i + 1 < args.Length)
                        roleName = args[++i];
                    else
                        showUsage = true;
                    break;
                }
                case "--list-statements": {
                    listStatements = true;
                    break;
                }
                case "--taxonomy-year": {
                    if (i + 1 < args.Length && int.TryParse(args[++i], out int ty))
                        taxonomyYear = ty;
                    else
                        showUsage = true;
                    break;
                }
                case "--help": {
                    showUsage = true;
                    break;
                }
            }
        }
        return new PrintStatementArgs(cik, concept, date, maxDepth, format, roleName, listStatements, showUsage, taxonomyYear);
    }

    private static IHost BuildHost<TStartup>(string[] args) where TStartup : class {
        IHost host = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            )
            .ConfigureWebHostDefaults(webBuilder => webBuilder.UseStartup<TStartup>())
            .ConfigureServices((context, services) => {

                int grpcPort = int.Parse(context.Configuration!.GetSection("Ports")["Grpc"] ?? DefaultPortStr, CultureInfo.InvariantCulture);

                _ = services
                    .Configure<KestrelServerOptions>(opt => {
                        opt.ListenAnyIP(grpcPort, options => options.Protocols = HttpProtocols.Http2);
                        opt.AllowAlternateSchemes = true;
                    });

                _ = services
                    .AddHttpClient()
                    .AddSingleton<PostgresExecutor>()
                    .AddSingleton<DbMigrations>()
                    .Configure<StooqPricesOptions>(context.Configuration.GetSection("StooqPrices"))
                    .Configure<StooqImportOptions>(context.Configuration.GetSection("StooqImport"))
                    .Configure<StooqBulkImportOptions>(context.Configuration.GetSection("StooqBulkImport"))
                    .Configure<TaxonomyImportOptions>(context.Configuration.GetSection("TaxonomyImport"))
                    .Configure<SecTickerMappingsOptions>(context.Configuration.GetSection("SecTickerMappings"))
                    .ConfigureTaxonomyConceptsFileProcessor(context)
                    .AddTransient<Func<string, ParseBulkXbrlArchiveContext, XBRLFileParser>>(
                        sp => (content, context) => new XBRLFileParser(content, context, sp));

                if (DoesConfigContainConnectionString(context.Configuration))
                    _ = services.AddSingleton<IDbmService, DbmService>();

                _ = services.AddGrpc();
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

    private static void LogConfig(IConfiguration _) {
        _logger.LogInformation("==========BEGIN CRITICAL CONFIGURATION==========");
        _logger.LogInformation("CompanyFactsBulkZipPath: {CompanyFactsBulkZipPath}", GetConfigValue("CompanyFactsBulkZipPath"));
        _logger.LogInformation("EdgarSubmissionsBulkZipPath: {EdgarSubmissionsBulkZipPath}", GetConfigValue("EdgarSubmissionsBulkZipPath"));
        //LogConfigSection(config, GoogleCredentialsOptions.GoogleCredentials);
        //LogConfigSection(config, HostedServicesOptions.HostedServices);
        //LogConfigSection(config, FeatureFlagsOptions.FeatureFlags);
        _logger.LogInformation("==========END CRITICAL CONFIGURATION==========");
    }

    private static async Task DownloadAndSaveFullCikList() {
        const int BatchSize = 1000;
        const string url = "https://www.sec.gov/Archives/edgar/cik-lookup-data.txt";

        string? content = await HttpClientService.FetchContentAsync(url);
        if (string.IsNullOrEmpty(content)) {
            _logger.LogWarning("Failed to download CIK list from {Url}", url);
            return;
        }

        _ = await _dbm!.EmptyCompaniesTables(CancellationToken.None);

        int i = 0;
        int numCompanies = 0;
        int numCompanyNames = 0;
        var companiesBatch = new List<Company>();
        var companyNamesBatch = new List<CompanyName>();
        var tasks = new List<Task>();
        var companyIdsByCiks = new Dictionary<ulong, ulong>(); // CIK -> CompanyId

        string? line;
        using var reader = new StringReader(content);
        while ((line = reader.ReadLine()) != null) {
            ++i;

            if (i % 1000 == 0)
                _logger.LogInformation("Processed {NumLines} lines", i);

            // Remove the trailing colon
            if (line.EndsWith(':'))
                line = line[..^1];

            int lastColonIndex = line.LastIndexOf(':');
            if (lastColonIndex == -1) {
                _logger.LogWarning("Failed to parse line {Line}", line);
                continue;
            }

            string companyName = line[..lastColonIndex].Trim();
            string cikStr = line[(lastColonIndex + 1)..].Trim();

            if (string.IsNullOrEmpty(companyName) || string.IsNullOrEmpty(cikStr)) {
                _logger.LogWarning("Failed to parse line {Line}", line);
                continue;
            }

            if (!ulong.TryParse(cikStr, out ulong cik)) {
                _logger.LogWarning("Failed to parse CIK {Cik}", cikStr);
                continue;
            }

            if (!companyIdsByCiks.TryGetValue(cik, out ulong companyId)) {
                companyId = await _dbm!.GetNextId64(CancellationToken.None);
                companyIdsByCiks.Add(cik, companyId);
                var company = new Company(companyId, cik, ModelsConstants.EdgarDataSource);
                companiesBatch.Add(company);
            }

            ulong companyNameId = await _dbm!.GetNextId64(CancellationToken.None);
            var companyNameObj = new CompanyName(companyNameId, companyId, companyName);
            companyNamesBatch.Add(companyNameObj);

            if (companiesBatch.Count >= BatchSize)
                BulkInsertCompaniesAndClearBatch();
            if (companyNamesBatch.Count >= BatchSize)
                BulkInsertCompanyNamesAndClearBatch();
        }

        // Save any remaining objects

        if (companiesBatch.Count > 0)
            BulkInsertCompaniesAndClearBatch();
        if (companyNamesBatch.Count > 0)
            BulkInsertCompanyNamesAndClearBatch();

        await Task.WhenAll(tasks);

        _logger.LogInformation("Processed {NumLines} lines; {NumCompanies} companies; {NumCompanyNames} company Names",
            i, numCompanies, numCompanyNames);

        // Local helper methods

        void BulkInsertCompaniesAndClearBatch() {
            numCompanies += companiesBatch.Count;
            BulkInsertCompanies(companiesBatch, tasks);
            companiesBatch.Clear();
        }

        void BulkInsertCompanyNamesAndClearBatch() {
            numCompanyNames += companyNamesBatch.Count;
            BulkInsertCompanyNames(companyNamesBatch, tasks);
            companyNamesBatch.Clear();
        }
    }

    private static async Task<Result> DownloadSecTickerMappingsAsync() {
        IConfiguration configuration = _svp!.GetRequiredService<IConfiguration>();
        string? outputDir = configuration["EdgarDataDir"];
        if (string.IsNullOrWhiteSpace(outputDir))
            return Result.Failure(ErrorCodes.GenericError, "Missing config: EdgarDataDir");

        IOptions<SecTickerMappingsOptions> options = _svp!.GetRequiredService<IOptions<SecTickerMappingsOptions>>();
        string userAgent = options.Value.ResolveUserAgent();

        IHttpClientFactory httpClientFactory = _svp!.GetRequiredService<IHttpClientFactory>();
        HttpClient httpClient = httpClientFactory.CreateClient();
        ILogger<SecTickerMappingsDownloader> logger = _svp!.GetRequiredService<ILogger<SecTickerMappingsDownloader>>();
        var downloader = new SecTickerMappingsDownloader(httpClient, logger);
        return await downloader.DownloadAsync(outputDir, userAgent, CancellationToken.None);
    }

    private static async Task<Result> ImportSecTickerMappingsAsync() {
        if (_svp is null)
            return Result.Failure(ErrorCodes.ValidationError, "Service provider is not initialized.");

        IConfiguration configuration = _svp.GetRequiredService<IConfiguration>();
        string? edgarDataDir = configuration["EdgarDataDir"];
        if (string.IsNullOrWhiteSpace(edgarDataDir))
            return Result.Failure(ErrorCodes.GenericError, "Missing config: EdgarDataDir");

        const int batchSize = 1000;
        ILogger<SecTickerMappingsImporter> logger = _svp.GetRequiredService<ILogger<SecTickerMappingsImporter>>();
        var importer = new SecTickerMappingsImporter(_dbm!, logger);
        return await importer.ImportAsync(edgarDataDir, batchSize, CancellationToken.None);
    }

    private static async Task<Result> ImportStooqPricesAsync() {
        if (_svp is null)
            return Result.Failure(ErrorCodes.ValidationError, "Service provider is not initialized.");

        IConfiguration configuration = _svp.GetRequiredService<IConfiguration>();
        string? edgarDataDir = configuration["EdgarDataDir"];
        if (string.IsNullOrWhiteSpace(edgarDataDir))
            return Result.Failure(ErrorCodes.GenericError, "Missing config: EdgarDataDir");

        IOptions<StooqPricesOptions> pricesOptions = _svp.GetRequiredService<IOptions<StooqPricesOptions>>();
        string outputDir = pricesOptions.Value.ResolveOutputDir(edgarDataDir);

        IOptions<StooqImportOptions> importOptions = _svp.GetRequiredService<IOptions<StooqImportOptions>>();
        int maxTickers = importOptions.Value.ResolveMaxTickersPerRun();
        int batchSize = importOptions.Value.ResolveBatchSize();

        ILogger<StooqPriceImporter> logger = _svp.GetRequiredService<ILogger<StooqPriceImporter>>();
        var importer = new StooqPriceImporter(_dbm!, logger);
        return await importer.ImportAsync(edgarDataDir, outputDir, maxTickers, batchSize, CancellationToken.None);
    }

    private static async Task<Result> ImportStooqBulkPricesAsync(string[] args) {
        if (_svp is null)
            return Result.Failure(ErrorCodes.ValidationError, "Service provider is not initialized.");

        IConfiguration configuration = _svp.GetRequiredService<IConfiguration>();
        string? edgarDataDir = configuration["EdgarDataDir"];
        if (string.IsNullOrWhiteSpace(edgarDataDir))
            return Result.Failure(ErrorCodes.GenericError, "Missing config: EdgarDataDir");

        IOptions<StooqBulkImportOptions> options = _svp.GetRequiredService<IOptions<StooqBulkImportOptions>>();
        string rootDir = options.Value.ResolveRootDir();
        int batchSize = options.Value.ResolveBatchSize();

        for (int i = 1; i < args.Length; i++) {
            if (string.Equals(args[i], "--root-dir", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length) {
                rootDir = args[i + 1];
                i++;
            }
        }

        if (string.IsNullOrWhiteSpace(rootDir))
            return Result.Failure(ErrorCodes.ValidationError, "Missing root dir for bulk Stooq import.");

        ILogger<StooqBulkPriceImporter> logger = _svp.GetRequiredService<ILogger<StooqBulkPriceImporter>>();
        var importer = new StooqBulkPriceImporter(_dbm!, logger);
        return await importer.ImportAsync(rootDir, edgarDataDir, batchSize, CancellationToken.None);
    }

    private static int ParseYearArg(string[] args) {
        for (int i = 0; i < args.Length - 1; i++) {
            if (string.Equals(args[i], "--year", StringComparison.OrdinalIgnoreCase)) {
                if (int.TryParse(args[i + 1], NumberStyles.None, CultureInfo.InvariantCulture, out int year))
                    return year;
            }
        }
        return 0;
    }

    private static async Task<Result> ImportTaxonomyYearAsync(int year) {
        if (_svp is null)
            return Result.Failure(ErrorCodes.ValidationError, "Service provider is not initialized.");

        IOptions<TaxonomyImportOptions> options = _svp.GetRequiredService<IOptions<TaxonomyImportOptions>>();
        string rootDir = options.Value.ResolveRootDir();
        if (string.IsNullOrWhiteSpace(rootDir))
            return Result.Failure(ErrorCodes.ValidationError, "Missing taxonomy root dir.");

        ILogger<UsGaapTaxonomyImporter> logger = _svp.GetRequiredService<ILogger<UsGaapTaxonomyImporter>>();
        var importer = new UsGaapTaxonomyImporter(_dbm!, logger);
        return await importer.ImportYearAsync(year, rootDir, CancellationToken.None);
    }

    private static async Task<Result> ImportAllTaxonomiesAsync() {
        if (_svp is null)
            return Result.Failure(ErrorCodes.ValidationError, "Service provider is not initialized.");

        IOptions<TaxonomyImportOptions> options = _svp.GetRequiredService<IOptions<TaxonomyImportOptions>>();
        string rootDir = options.Value.ResolveRootDir();
        if (string.IsNullOrWhiteSpace(rootDir))
            return Result.Failure(ErrorCodes.ValidationError, "Missing taxonomy root dir.");

        ILogger<UsGaapTaxonomyImporter> logger = _svp.GetRequiredService<ILogger<UsGaapTaxonomyImporter>>();
        var importer = new UsGaapTaxonomyImporter(_dbm!, logger);
        IReadOnlyList<int> years = importer.DiscoverYears(rootDir);
        if (years.Count == 0)
            return Result.Failure(ErrorCodes.NotFound, $"No taxonomy years found in {rootDir}");

        foreach (int year in years) {
            Result res = await importer.ImportYearAsync(year, rootDir, CancellationToken.None);
            if (res.IsFailure)
                return res;
        }

        return Result.Success;
    }

    private static void BulkInsertCompanies(IReadOnlyCollection<Company> companies, List<Task> tasks) {
        var batch = new List<Company>(companies);
        tasks.Add(Task.Run(async () => {
            Result res = await _dbm!.BulkInsertCompanies(batch, CancellationToken.None);
            if (res.IsFailure)
                _logger.LogWarning("Failed to save batch of companies. Error: {Error}", res.ErrorMessage);
        }));
    }

    private static void BulkInsertCompanyNames(IReadOnlyCollection<CompanyName> companyNames, List<Task> tasks) {
        var batch = new List<CompanyName>(companyNames);
        tasks.Add(Task.Run(async () => {
            Result res = await _dbm!.BulkInsertCompanyNames(batch, CancellationToken.None);
            if (res.IsFailure)
                _logger.LogWarning("Failed to save batch of company names. Error: {Error}", res.ErrorMessage);
        }));
    }

    private static async Task ParseBulkEdgarSubmissionsList() {
        string archivePath = GetConfigValue("EdgarSubmissionsBulkZipPath");

        using var zipReader = new ZipFileReader(archivePath);

        var context = new ParseBulkEdgarSubmissionsContext(_svp!);

        Result<IReadOnlyCollection<Company>> companyResults =
            await _dbm!.GetAllCompaniesByDataSource(ModelsConstants.EdgarDataSource, CancellationToken.None);
        if (companyResults.IsFailure) {
            _logger.LogError("ParseBulkEdgarSubmissionsList - Failed to get companies. Error: {Error}", companyResults.ErrorMessage);
            return;
        }

        foreach (Company c in companyResults.Value!)
            context.CompanyIdsByCiks[c.Cik] = c.CompanyId;

        foreach (string fileName in zipReader.EnumerateFileNames()) {
            if (!fileName.EndsWith(".json"))
                continue;

            context.IsCurrentFileSubmissionsFile = fileName.Contains("-submissions-");
            ++context.NumFiles;
            if ((context.NumFiles % 100) == 0)
                context.LogProgress();

            context.CurrentFileName = fileName;
            await ParseOneFileSubmission(zipReader, context);
        }

        // Save any remaining objects

        if (context.SubmissionsBatch.Count != 0)
            context.NumSubmissions += BulkInsertSubmissionsAndClearBatch(context.SubmissionsBatch, context.Tasks);

        await Task.WhenAll(context.Tasks);

        context.LogProgress();
    }

    private static async Task ParseOneFileSubmission(ZipFileReader zipReader, ParseBulkEdgarSubmissionsContext context) {
        try {
            string fileContent = zipReader.ExtractFileContent(context.CurrentFileName);
            context.TotalLength += fileContent.Length;

            (FilingsDetails? filingsDetails, ulong companyId) = GetFilingsDetails(fileContent, context);
            if (filingsDetails is null) {
                _logger.LogWarning("ParseOneFileSubmission - Failed to parse {FileName}", context.CurrentFileName);
                return;
            }

            if (companyId == ulong.MaxValue) {
                _logger.LogWarning("ParseOneFileSubmission - Failed to find company ID for {FileName}", context.CurrentFileName);
                return;
            }

            IReadOnlyCollection<Submission> submissions = context.JsonConverter.ToSubmissions(filingsDetails);
            foreach (Submission s in submissions) {
                ulong submissionId = await _dbm!.GetNextId64(CancellationToken.None);
                Submission submissionToInsert = s with { SubmissionId = submissionId, CompanyId = companyId };
                context.SubmissionsBatch.Add(submissionToInsert);

                if (context.SubmissionsBatch.Count >= ParseBulkEdgarSubmissionsBatchSize)
                    context.NumSubmissions += BulkInsertSubmissionsAndClearBatch(context.SubmissionsBatch, context.Tasks);
            }
        } catch (Exception ex) {
            _logger.LogError(ex, "ParseOneFileSubmission - Failed to process {FileName}", context.CurrentFileName);
        }
    }

    private static (FilingsDetails?, ulong) GetFilingsDetails(string fileContent, ParseBulkEdgarSubmissionsContext context) {
        if (context.IsCurrentFileSubmissionsFile) {
            FilingsDetails? filingsDetails = JsonSerializer.Deserialize<FilingsDetails>(fileContent, Conventions.DefaultOptions);

            // Get company CIK from the file name, e.g. CIK0000829323-submissions-001.json
            if (context.CurrentFileName.Length < 13)
                return (filingsDetails, ulong.MaxValue);

            string cikStr = context.CurrentFileName[..13][3..];
            return !ulong.TryParse(cikStr, out ulong cik)
                ? (filingsDetails, ulong.MaxValue)
                : !context.CompanyIdsByCiks.TryGetValue(cik, out ulong companyId)
                ? (filingsDetails, ulong.MaxValue)
                : ((FilingsDetails?, ulong))(filingsDetails, companyId);
        } else {
            RecentFilingsContainer? submissionsJson = JsonSerializer.Deserialize<RecentFilingsContainer>(fileContent, Conventions.DefaultOptions);

            if (submissionsJson is null)
                return (null, ulong.MaxValue);

            FilingsDetails filingsDetails = submissionsJson.Filings.Recent;

            return !context.CompanyIdsByCiks.TryGetValue(submissionsJson.Cik, out ulong companyId)
                ? (filingsDetails, ulong.MaxValue)
                : ((FilingsDetails?, ulong))(filingsDetails, companyId);
        }
    }

    private static int BulkInsertSubmissionsAndClearBatch(List<Submission> submissionsBatch, List<Task> tasks) {
        int numDataPointsInBatch = submissionsBatch.Count;

        BulkInsertSubmissions(submissionsBatch, tasks);
        submissionsBatch.Clear();

        return numDataPointsInBatch;
    }

    private static void BulkInsertSubmissions(List<Submission> submissionsBatch, List<Task> tasks) {
        var batch = new List<Submission>(submissionsBatch);

        tasks.Add(Task.Run(async () => {
            Result res = await _dbm!.BulkInsertSubmissions(batch, CancellationToken.None);
            if (res.IsFailure)
                _logger.LogWarning("Failed to save batch of submissions. Error: {Error}", res.ErrorMessage);
        }));
    }

    private static async Task ParseBulkXbrlArchive() {
        string archivePath = GetConfigValue("CompanyFactsBulkZipPath");

        using var zipReader = new ZipFileReader(archivePath);

        var context = new ParseBulkXbrlArchiveContext(_svp!);

        // For collecting unmatched fact_names
        var unmatchedFactNames = new List<(string FactName, ulong CompanyId, string FileName)>();

        Result<IReadOnlyCollection<Company>> companyResults =
            await _dbm!.GetAllCompaniesByDataSource(ModelsConstants.EdgarDataSource, CancellationToken.None);
        if (companyResults.IsFailure) {
            _logger.LogError("ParseBulkXbrlArchive - Failed to get companies. Error: {Error}", companyResults.ErrorMessage);
            return;
        }

        foreach (Company c in companyResults.Value!)
            context.CompanyIdsByCik[c.Cik] = c.CompanyId;

        Result<IReadOnlyCollection<DataPointUnit>> dpuResults =
            await _dbm!.GetDataPointUnits(CancellationToken.None);
        if (dpuResults.IsFailure) {
            _logger.LogError("ParseBulkXbrlArchive - Failed to get data point units. Error: {Error}", dpuResults.ErrorMessage);
            return;
        }

        foreach (DataPointUnit dpu in dpuResults.Value!)
            context.UnitsByUnitName[dpu.UnitName] = dpu;

        Result<IReadOnlyCollection<Submission>> submissionResults =
            await _dbm!.GetSubmissions(CancellationToken.None);
        if (submissionResults.IsFailure) {
            _logger.LogError("ParseBulkXbrlArchive - Failed to get submissions. Error: {Error}", submissionResults.ErrorMessage);
            return;
        }

        // Collect distinct report years from submissions to load per-year taxonomy concepts
        var reportYears = new HashSet<int>();
        foreach (Submission s in submissionResults.Value!) {
            List<Submission> companySubmissions = context.SubmissionsByCompanyId.GetOrCreateEntry(s.CompanyId);
            companySubmissions.Add(s);
            reportYears.Add(s.ReportDate.Year);
        }

        // Load taxonomy concepts for each report year (us-gaap + dei merged into same map)
        int latestYear = 0;
        string[] taxonomyPrefixes = ["us-gaap", "dei"];
        foreach (int year in reportYears) {
            var yearMap = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

            foreach (string prefix in taxonomyPrefixes) {
                Result<TaxonomyTypeInfo> taxTypeResult =
                    await _dbm!.GetTaxonomyTypeByNameVersion(prefix, year, CancellationToken.None);
                if (taxTypeResult.IsFailure || taxTypeResult.Value is null)
                    continue;

                Result<IReadOnlyCollection<ConceptDetailsDTO>> conceptsResult =
                    await _dbm!.GetTaxonomyConceptsByTaxonomyType(taxTypeResult.Value.TaxonomyTypeId, CancellationToken.None);
                if (conceptsResult.IsFailure || conceptsResult.Value is null)
                    continue;

                foreach (ConceptDetailsDTO concept in conceptsResult.Value) {
                    if (concept.PeriodTypeId == (int)TaxonomyPeriodTypes.None)
                        continue;
                    yearMap[concept.Name.Trim()] = concept.ConceptId;
                }
            }

            if (yearMap.Count > 0) {
                context.TaxonomyConceptIdsByYear[year] = yearMap;
                if (year > latestYear)
                    latestYear = year;
            }

            _logger.LogInformation("ParseBulkXbrlArchive - Loaded {Count} concepts for taxonomy year {Year}",
                yearMap.Count, year);
        }

        context.LatestTaxonomyYear = latestYear;

        if (context.TaxonomyConceptIdsByYear.Count == 0) {
            _logger.LogError("ParseBulkXbrlArchive - No taxonomy concepts loaded for any year. Aborting.");
            return;
        }

        foreach (string fileName in zipReader.EnumerateFileNames()) {
            if (!fileName.EndsWith(".json"))
                continue;

            ++context.NumFiles;
            if ((context.NumFiles % 100) == 0)
                context.LogProgress();

            context.CurrentFileName = fileName;
            await ParseOneFileXBRL(zipReader, context, unmatchedFactNames);
        }

        // Save any remaining objects

        if (context.DataPointsBatch.Count != 0)
            context.NumDataPoints += BulkInsertDataPointsAndClearBatch(context.DataPointsBatch, context.Tasks);

        await Task.WhenAll(context.Tasks);

        // Log unmatched fact_names at the end
        if (unmatchedFactNames.Count > 0) {
            foreach ((string FactName, ulong CompanyId, string FileName) in unmatchedFactNames) {
                _logger.LogWarning("Unmatched fact_name (not inserted): FactName: '{FactName}', CompanyId: {CompanyId}, File: {FileName}",
                    FactName, CompanyId, FileName);
            }
        }

        context.LogProgress();
    }

    // Overload to pass taxonomyConceptIdByName and unmatchedFactNames
    private static async Task ParseOneFileXBRL(ZipFileReader zipReader, ParseBulkXbrlArchiveContext context, List<(string FactName, ulong CompanyId, string FileName)> unmatchedFactNames) {
        try {
            string fileContent = zipReader.ExtractFileContent(context.CurrentFileName);
            context.TotalLength += fileContent.Length;

            XBRLFileParser parser = CreateParser(fileContent);
            XBRLParserResult res = parser.Parse();
            if (res.IsFailure) {
                LogParseFailure(res);
                return;
            }

            foreach (DataPoint dp in parser.DataPoints) {
                await ProcessDataPoint(dp, context, unmatchedFactNames);
            }
        } catch (Exception ex) {
            _logger.LogError(ex, "ParseOneFileXBRL - Failed to process {FileName}", context.CurrentFileName);
        }

        // Local helper methods

        XBRLFileParser CreateParser(string fileContent) {
            Func<string, ParseBulkXbrlArchiveContext, XBRLFileParser> parserFactory = _svp!.GetRequiredService<Func<string, ParseBulkXbrlArchiveContext, XBRLFileParser>>();
            return parserFactory(fileContent, context);
        }

        void LogParseFailure(XBRLParserResult res) {
            const string msgTemplate = "ParseOneFileXBRL - Failed to parse {FileName}. Error: {ErrMsg}";
            if (res.IsWarningLevel)
                _logger.LogWarning(msgTemplate, context.CurrentFileName, res.ErrorMessage);
            else
                _logger.LogInformation(msgTemplate, context.CurrentFileName, res.ErrorMessage);
        }
    }

    private static async Task ProcessDataPoint(
        DataPoint dp,
        ParseBulkXbrlArchiveContext context,
        List<(string FactName, ulong CompanyId, string FileName)> unmatchedFactNames) {
        if (!context.SubmissionsByCompanyId.TryGetValue(dp.CompanyId, out List<Submission>? submissions)) {
            _logger.LogWarning("ParseOneFileXBRL:ProcessDataPoint - Failed to find submissions for company: {CompanyId},{DataPoint}",
                dp.CompanyId, dp);
            return;
        }

        Result<Submission> findSubmissionForDataPointResults = CorrelateDataPointToSubmission(dp, submissions);
        if (findSubmissionForDataPointResults.IsFailure) {
            _logger.LogWarning("ParseOneFileXBRL:ProcessDataPoint - Failed to find submission for data point. {CompanyId},{DataPoint}. Error: {Error}",
                dp.CompanyId, dp, findSubmissionForDataPointResults.ErrorMessage);
            return;
        }

        // Resolve taxonomy concept ID using the submission's report year
        Submission matchedSubmission = findSubmissionForDataPointResults.Value!;
        int reportYear = matchedSubmission.ReportDate.Year;
        string factNameKey = dp.FactName.Trim();

        long conceptId = 0;
        if (context.TaxonomyConceptIdsByYear.TryGetValue(reportYear, out Dictionary<string, long>? yearMap)) {
            yearMap.TryGetValue(factNameKey, out conceptId);
        }
        // Fallback to latest taxonomy if the report year's taxonomy doesn't have this concept
        if (conceptId == 0 && reportYear != context.LatestTaxonomyYear) {
            if (context.TaxonomyConceptIdsByYear.TryGetValue(context.LatestTaxonomyYear, out Dictionary<string, long>? latestMap))
                latestMap.TryGetValue(factNameKey, out conceptId);
        }
        if (conceptId == 0) {
            unmatchedFactNames.Add((dp.FactName, dp.CompanyId, context.CurrentFileName));
            return;
        }

        DataPoint dpWithConcept = dp with { TaxonomyConceptId = conceptId };

        string unitName = dpWithConcept.Units.UnitNameNormalized;
        if (!context.UnitsByUnitName.TryGetValue(unitName, out DataPointUnit? dpu)) {
            ++context.NumDataPointUnits;
            ulong dpuId = await _dbm!.GetNextId64(CancellationToken.None);
            dpu = context.UnitsByUnitName[unitName] = new DataPointUnit(dpuId, unitName);
            _ = await _dbm.InsertDataPointUnit(dpu, CancellationToken.None);
            _logger.LogInformation("ParseOneFileXBRL:ProcessDataPoint - Inserted data point unit: {DataPointUnit}", dpu);
        }

        ulong dpId = await _dbm!.GetNextId64(CancellationToken.None);
        DataPoint dataPointToInsert = dpWithConcept with { DataPointId = dpId, Units = dpu };
        context.DataPointsBatch.Add(dataPointToInsert);

        if (context.DataPointsBatch.Count >= ParseBulkXbrlNumDataPointsBatchSize)
            context.NumDataPoints += BulkInsertDataPointsAndClearBatch(context.DataPointsBatch, context.Tasks);
    }

    private static Result<Submission> CorrelateDataPointToSubmission(DataPoint dp, List<Submission> submissions) {
        foreach (Submission submission in submissions) {
            if (dp.FilingReference == submission.FilingReference)
                return Result<Submission>.Success(submission);
        }

        return Result<Submission>.Failure(ErrorCodes.NotFound, "No matching submission found for the given data point.");
    }

    private static int BulkInsertDataPointsAndClearBatch(List<DataPoint> dataPointsBatch, List<Task> tasks) {
        int numDataPointsInBatch = dataPointsBatch.Count;

        BulkInsertDataPoints(dataPointsBatch, tasks);
        dataPointsBatch.Clear();

        return numDataPointsInBatch;
    }

    private static void BulkInsertDataPoints(IReadOnlyCollection<DataPoint> dataPointsBatch, List<Task> tasks) {
        var batch = new List<DataPoint>(dataPointsBatch);

        tasks.Add(Task.Run(async () => {
            Result res = await _dbm!.BulkInsertDataPoints(batch, CancellationToken.None);
            if (res.IsFailure)
                _logger.LogWarning("Failed to save batch of data points. Error: {Error}", res.ErrorMessage);
        }));
    }

    private static async Task<Result> ImportInlineXbrlSharesAsync() {
        string submissionsZipPath = GetConfigValue("EdgarSubmissionsBulkZipPath");

        using var httpClient = new Services.RateLimitedHttpClient(_logger);
        var importer = new Services.InlineXbrlSharesImporter(_dbm!, _logger);

        return await importer.ImportAsync(submissionsZipPath, httpClient, CancellationToken.None);
    }

    private static async Task<Result> ComputeAndStoreAllScoresAsync() {
        var scoringService = new Stocks.Persistence.Services.ScoringService(_dbm!, _logger);
        CancellationToken ct = CancellationToken.None;

        _logger.LogInformation("Computing scores for all companies...");
        Result<IReadOnlyCollection<Stocks.DataModels.Scoring.CompanyScoreSummary>> computeResult =
            await scoringService.ComputeAllScores(ct);
        if (computeResult.IsFailure || computeResult.Value is null)
            return Result.Failure(ErrorCodes.GenericError, computeResult.ErrorMessage);

        List<Stocks.DataModels.Scoring.CompanyScoreSummary> scores = new(computeResult.Value);
        _logger.LogInformation("Computed {NumScores} company scores, truncating old scores...", scores.Count);

        Result truncateResult = await _dbm!.TruncateCompanyScores(ct);
        if (truncateResult.IsFailure)
            return truncateResult;

        _logger.LogInformation("Inserting {NumScores} company scores...", scores.Count);
        Result insertResult = await _dbm.BulkInsertCompanyScores(scores, ct);
        if (insertResult.IsFailure)
            return insertResult;

        _logger.LogInformation("Successfully computed and stored {NumScores} company scores", scores.Count);
        return Result.Success;
    }

    private static string GetConfigValue(string key) {
        if (_svp is null)
            throw new InvalidOperationException("Service provider is not initialized");
        IConfiguration configuration = _svp.GetRequiredService<IConfiguration>();
        return configuration[key] ?? throw new InvalidOperationException($"Configuration key '{key}' not found");
    }
}
