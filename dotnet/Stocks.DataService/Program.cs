using System;
using System.Globalization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Stocks.DataService.RawDataService;
using Stocks.Persistence;
using Stocks.Persistence.Migrations;
using Stocks.Shared;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Stocks.DataService;

internal class Program {
    private const string DefaultPortStr = "7101";

    private static ILogger _logger;
    private static IServiceProvider? _svp;

    static Program() {
        _logger = HostingUtils.GetBootstrapLogger<Program>();
    }

    private static int Main(string[] args) {
        try {
            _logger.LogInformation("Building the host");
            IHost host = BuildHost<Startup>(args);
            _logger.LogInformation("Running the host");
            host.Run();

            _logger.LogInformation("Service execution completed");
            return 0;
        } catch (Exception ex) {
            _logger.LogError(ex, "Service execution is terminated with an error");
            return 1;
        } finally {
            Log.CloseAndFlush();
        }
    }

    private static IHost BuildHost<TStartup>(string[] args) where TStartup : class {
        IHost host = Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(webBuilder => webBuilder.UseStartup<TStartup>())
            .ConfigureServices((context, services) => {
                int grpcPort = int.Parse(context.Configuration!.GetSection("Ports")["Grpc"] ?? DefaultPortStr, CultureInfo.InvariantCulture);
                _ = services.Configure<KestrelServerOptions>(opt => {
                    opt.ListenAnyIP(grpcPort, options => options.Protocols = HttpProtocols.Http2);
                    opt.AllowAlternateSchemes = true;
                });

                _ = services
                    .AddHttpClient()
                    .AddSingleton<PostgresExecutor>()
                    .AddSingleton<DbMigrations>()
                    .AddSingleton<RawDataQueryProcessor>();

                if (DoesConfigContainConnectionString(context.Configuration))
                    _ = services.AddSingleton<IDbmService, DbmService>();

                _ = services.AddHostedService(p => p.GetRequiredService<RawDataQueryProcessor>());
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
        //_logger.LogInformation("CompanyFactsBulkZipPath: {CompanyFactsBulkZipPath}", GetConfigValue("CompanyFactsBulkZipPath"));
        //_logger.LogInformation("EdgarSubmissionsBulkZipPath: {EdgarSubmissionsBulkZipPath}", GetConfigValue("EdgarSubmissionsBulkZipPath"));
        //LogConfigSection(config, GoogleCredentialsOptions.GoogleCredentials);
        //LogConfigSection(config, HostedServicesOptions.HostedServices);
        //LogConfigSection(config, FeatureFlagsOptions.FeatureFlags);
        _logger.LogInformation("==========END CRITICAL CONFIGURATION==========");
    }

    //private static string GetConfigValue(string key)
    //{
    //    if (_svp is null)
    //        throw new InvalidOperationException("Service provider is not initialized");
    //    IConfiguration configuration = _svp.GetRequiredService<IConfiguration>();
    //    return configuration[key] ?? throw new InvalidOperationException($"Configuration key '{key}' not found");
    //}
}