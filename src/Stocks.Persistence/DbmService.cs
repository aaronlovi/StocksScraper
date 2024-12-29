using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Stocks.DataModels;
using Utilities;

namespace Stocks.Persistence;

public sealed class DbmService : IDisposable, IDbmService
{
    public const string StocksDataConnectionStringName = "stocks-data";

    private readonly ILogger<DbmService> _logger;
    private readonly PostgresExecutor _exec;
    private readonly SemaphoreSlim _generatorMutex;

    private ulong _lastUsed;
    private ulong _endId;

    public DbmService(IServiceProvider svp)
    {
        _logger = svp.GetRequiredService<ILogger<DbmService>>();
        _exec = svp.GetRequiredService<PostgresExecutor>();

        IConfiguration config = svp.GetRequiredService<IConfiguration>();
        string connStr = config.GetConnectionString(StocksDataConnectionStringName) ?? string.Empty;
        if (string.IsNullOrEmpty(connStr))
            throw new InvalidOperationException("Connection string is empty");

        _generatorMutex = new(1);

        // Perform the DB migrations synchronously
        try
        {
            DbMigrations migrations = svp.GetRequiredService<DbMigrations>();
            migrations.Up();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform DB migrations, aborting");
            throw;
        }
    }

    #region Generator

    public ValueTask<ulong> GetNextId64(CancellationToken ct) => GetIdRange64(1, ct);

    public async ValueTask<ulong> GetIdRange64(uint count, CancellationToken ct)
    {
        if (count == 0)
            throw new ArgumentOutOfRangeException(nameof(count), "Count cannot be 0");

        // Optimistic path: in most cases
        lock (_generatorMutex)
        {
            if (_lastUsed + count <= _endId)
            {
                var result = _lastUsed + 1;
                _lastUsed += count;
                return result;
            }
        }

        // Lock the DB update mutex
        using var locker = new SemaphoreLocker(_generatorMutex);
        await locker.Acquire(ct);

        // May have bene changed already by another thread, so check again
        lock (_generatorMutex)
        {
            if (_lastUsed + count <= _endId)
            {
                var result = _lastUsed + 1;
                _lastUsed += count;
                return result;
            }
        }

        // Update in blocks
        const uint BLOCK_SIZE = 65536;
        uint idRange = count - (count % BLOCK_SIZE) + BLOCK_SIZE;
        var stmt = new ReserveIdRangeStmt(idRange);
        DbStmtResult res = await _exec.ExecuteWithRetry(stmt, ct, 0);

        if (res.IsSuccess)
        {
            lock (_generatorMutex)
            {
                _endId = (ulong)stmt.LastReserved;
                _lastUsed = (ulong)(stmt.LastReserved - BLOCK_SIZE);
                var result = _lastUsed + 1;
                _lastUsed += count;
                return result;
            }
        }
        else
        {
            throw new InvalidOperationException("Failed to get next id from database");
        }
    }

    #endregion

    #region Companies

    public async Task<GenericResults<IReadOnlyCollection<Company>>> GetCompaniesByDataSource(
        string dataSource, CancellationToken ct)
    {
        var stmt = new GetCompaniesByDataSourceStmt(dataSource);
        DbStmtResult res = await _exec.ExecuteWithRetry(stmt, ct);
        if (res.IsSuccess)
        {
            _logger.LogInformation("GetCompaniesByDataSource success - Num companies: {NumCompanies}", stmt.Companies.Count);
            return GenericResults<IReadOnlyCollection<Company>>.SuccessResult(stmt.Companies);
        }
        else
        {
            _logger.LogWarning("GetCompaniesByDataSource failed with error {Error}", res.ErrorMessage);
            return GenericResults<IReadOnlyCollection<Company>>.FailureResult(res.ErrorMessage);
        }
    }

    public async Task<Results> EmptyCompaniesTables(CancellationToken ct)
    {
        var stmt = new TruncateCompaniesTablesStmt();
        return await _exec.ExecuteWithRetry(stmt, ct);
    }

    public async Task<Results> BulkInsertCompanies(List<Company> companies, CancellationToken ct)
    {
        var stmt = new BulkInsertCompaniesStmt(companies);
        return await _exec.ExecuteWithRetry(stmt, ct);
    }

    public async Task<Results> BulkInsertCompanyNames(List<CompanyName> companyNames, CancellationToken ct)
    {
        var stmt = new BulkInsertCompanyNamesStmt(companyNames);
        return await _exec.ExecuteWithRetry(stmt, ct);
    }

    #endregion

    #region Data points

    public async Task<GenericResults<IReadOnlyCollection<DataPointUnit>>> GetDataPointUnits(CancellationToken ct)
    {
        var stmt = new GetAllDataPointUnitsStmt();
        DbStmtResult res = await _exec.ExecuteWithRetry(stmt, ct);
        if (res.IsSuccess)
        {
            _logger.LogInformation("GetDataPointUnits success - Num units: {NumUnits}", stmt.Units.Count);
            return GenericResults<IReadOnlyCollection<DataPointUnit>>.SuccessResult(stmt.Units);
        }
        else
        {
            _logger.LogWarning("GetDataPointUnits failed with error {Error}", res.ErrorMessage);
            return GenericResults<IReadOnlyCollection<DataPointUnit>>.FailureResult(res.ErrorMessage);
        }
    }

    public async Task<Results> InsertDataPointUnit(DataPointUnit dataPointUnit, CancellationToken ct)
    {
        var stmt = new InsertDataPointUnitStmt(dataPointUnit);
        return await _exec.ExecuteWithRetry(stmt, ct);
    }

    public async Task<Results> BulkInsertDataPoints(List<DataPoint> dataPoints, CancellationToken ct)
    {
        var stmt = new BulkInsertDataPointsStmt(dataPoints);
        return await _exec.ExecuteWithRetry(stmt, ct);
    }

    #endregion

    public void Dispose()
    {
        // Dispose of Postgres executor
        _exec.Dispose();

        // Dispose of the generator mutex
        _generatorMutex.Dispose();
    }
}
