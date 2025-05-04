using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Stocks.DataService.RawDataService;

namespace Stocks.DataService;

/// <summary>
/// This helper class is used by the main startup config. This allows to keep registered classes as 'internal'.
/// </summary>
internal static class GrpcHostConfig
{
    public static void ConfigureServices(IServiceCollection _) { }

    /// <summary>
    /// Used at application startup to configure gRPC endpoints.
    /// In this case, incoming HTTP requests are routed to gRPC endpoints
    /// in the <see cref="RawDataGrpcService"/> class.
    /// </summary>
    public static IEnumerable<GrpcServiceEndpointConventionBuilder> ConfigureEndpoints(IEndpointRouteBuilder endpoints)
    => [ endpoints.MapGrpcService<RawDataGrpcService>() ];
}
