using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Stocks.Persistence.Database;
using Stocks.Persistence.Services;
using Stocks.WebApi.Endpoints;
using Stocks.WebApi.Services;

namespace Stocks.WebApi;

public class Program {
    public static void Main(string[] args) {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        _ = builder.Services.AddSingleton<PostgresExecutor>();

        if (builder.Configuration.GetConnectionString(DbmService.StocksDataConnectionStringName) is not null)
            _ = builder.Services.AddSingleton<IDbmService, DbmService>();

        _ = builder.Services.AddSingleton<StatementDataService>();
        _ = builder.Services.AddSingleton<TypeaheadTrieService>();
        _ = builder.Services.AddHostedService(sp => sp.GetRequiredService<TypeaheadTrieService>());

        _ = builder.Services.AddCors(options => {
            options.AddDefaultPolicy(policy => {
                _ = policy
                    .WithOrigins("http://localhost:4200")
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });

        WebApplication app = builder.Build();

        _ = app.UseCors();

        _ = app.MapGet("/api/health", () => Results.Ok(new { status = "healthy" }));

        app.MapCompanyEndpoints();
        app.MapSubmissionEndpoints();
        app.MapSearchEndpoints();
        app.MapDashboardEndpoints();
        app.MapStatementEndpoints();
        app.MapTypeaheadEndpoints();

        app.Run();
    }
}
