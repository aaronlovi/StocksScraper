using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Stocks.DataService;

/// <summary>
/// 1. Application Initialization
/// When the application starts, the Startup class is instantiated and configured.
/// </summary>
internal class Startup(IConfiguration _config) {
    /// <summary>
    /// 2. Service Configuration
    /// The ConfigureServices method is called by the runtime.
    /// This method is used to add services to the DI container.
    /// These services are then available throughout the application
    /// </summary>
    /// <param name="services"></param>
    public void ConfigureServices(IServiceCollection services) {
        _ = services.AddGrpc();

        VerifyCriticalConfiguration();
    }

    /// <summary>
    /// 3. Request Pipeline Configuration
    /// The Configure method is called by the runtime to set up the request processing pipeline.
    /// This method defines how the application will respond to HTTP requests.
    /// </summary>
    public static void Configure(IApplicationBuilder app) {
        _ = app.
            UseRouting().
            UseEndpoints(endpoints => {
                // In this case, incoming HTTP requests are routed to gRPC endpoints,
                // which are configured in ReportingHostConfig.ConfigureEndpoints
                var builders = new List<GrpcServiceEndpointConventionBuilder>();
                builders.AddRange(GrpcHostConfig.ConfigureEndpoints(endpoints));
            });
    }

    #region PRIVATE HELPER METHODS

    private void VerifyCriticalConfiguration() => VerifyConfigurationItem("DatabaseSchema");

    private void VerifyConfigurationItem(string key, string? section = null) {
        string? value;
        if (section is not null) {
            value = _config.GetSection(section)[key];
            if (string.IsNullOrEmpty(value))
                throw new Exception($"Missing '{key}' in '{section}' section of app configuration");
        } else {
            value = _config[key];
            if (string.IsNullOrEmpty(value))
                throw new Exception($"Missing '{key}' in app configuration");
        }
    }

    #endregion
}
