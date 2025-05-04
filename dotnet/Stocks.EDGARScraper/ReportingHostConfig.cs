using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace EDGARScraper;

/// <summary>
/// This helper class is used by the main startup config. This allows to keep registered classes as 'internal'.
/// </summary>
public static class ReportingHostConfig {
    public static void ConfigureServices(IServiceCollection _) { }

    /// <summary>
    /// Used at application startup to configure gRPC endpoints.
    /// In this case, incoming HTTP requests are routed to gRPC endpoints
    /// in the <see cref="StockDataService"/> class.
    /// </summary>
    /// <remarks>
    /// Does not accept GRPC requests yet. This is for the future.
    /// </remarks>
    public static IEnumerable<GrpcServiceEndpointConventionBuilder> ConfigureEndpoints(IEndpointRouteBuilder _)
        => [];

    /*
    For reference:
    /// <summary>
    /// Used at application startup to configure gRPC endpoints.
    /// In this case, incoming HTTP requests are routed to gRPC endpoints
    /// in the <see cref="StockDataService"/> class.
    /// </summary>
    public static IEnumerable<GrpcServiceEndpointConventionBuilder> ConfigureEndpoints(IEndpointRouteBuilder endpoints)
        => new[] { endpoints.MapGrpcService<StockDataSvc>() };
    */
}
