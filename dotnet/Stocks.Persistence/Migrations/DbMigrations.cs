using System;
using EvolveDb;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Stocks.Persistence;

/// <summary>
/// Handles database migrations for the application.
/// </summary>
/// <remarks>
/// This class uses the Evolve library to apply database migrations.
/// Migrations are scripts that update the structure of the database,
/// and are located in the "Migrations" directory.
/// The connection string and database schema are provided via the application's configuration.
/// </remarks>
public class DbMigrations
{
    private readonly ILogger<DbMigrations> _logger;
    private readonly string _connectionString;
    private readonly string _databaseSchema;

    public DbMigrations(IServiceProvider svp)
    {
        _logger = svp.GetRequiredService<ILogger<DbMigrations>>();
        IConfiguration config = svp.GetRequiredService<IConfiguration>();

        _connectionString = config.GetConnectionString(DbmService.StocksDataConnectionStringName) ?? string.Empty;
        if (string.IsNullOrEmpty(_connectionString))
            throw new InvalidOperationException("Connection string is empty");

        _databaseSchema = config["DatabaseSchema"] ?? string.Empty;
        if (string.IsNullOrEmpty(_databaseSchema))
            throw new InvalidOperationException("Database schema is empty");
    }

    public void Up()
    {
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            var evolve = new Evolve(connection, msg => _logger.LogInformation("Evolve: {msg}", msg))
            {
                EmbeddedResourceAssemblies = [typeof(DbMigrations).Assembly],
                // EmbeddedResourceFilters = new[] { "dbm-persistence" },
                Schemas = [_databaseSchema],
                IsEraseDisabled = true,
                EnableClusterMode = true
            };
            evolve.Migrate();

            // Reload types to get proper introspection
            connection.Open();
            connection.ReloadTypes();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database migration failed");
            throw;
        }
    }
}
