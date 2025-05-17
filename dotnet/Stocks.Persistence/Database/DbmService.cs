using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Stocks.DataModels;
using Stocks.Persistence.Database.Migrations;
using Stocks.Persistence.Database.Statements;
using Stocks.Shared;
using Stocks.Shared.Models;

namespace Stocks.Persistence.Database;

public sealed class DbmService : IDisposable, IDbmService {
    public const string StocksDataConnectionStringName = "stocks-data";

    private readonly ILogger<DbmService> _logger;
    private readonly PostgresExecutor _exec;
    private readonly SemaphoreSlim _generatorMutex;

    private ulong _lastUsed;
    private ulong _endId;

    public DbmService(IServiceProvider svp) {
        _logger = svp.GetRequiredService<ILogger<DbmService>>();
        _exec = svp.GetRequiredService<PostgresExecutor>();

        IConfiguration config = svp.GetRequiredService<IConfiguration>();
        string connStr = config.GetConnectionString(StocksDataConnectionStringName) ?? string.Empty;
        if (string.IsNullOrEmpty(connStr))
            throw new InvalidOperationException("Connection string is empty");

        _generatorMutex = new(1);

        // Perform the DB migrations synchronously
        try {
            DbMigrations migrations = svp.GetRequiredService<DbMigrations>();
            migrations.Up();
        } catch (Exception ex) {
            _logger.LogError(ex, "Failed to perform DB migrations, aborting");
            throw;
        }
    }

    #region Utilities

    public async Task<Result> DropAllTables(CancellationToken ct) {
        var stmt = new DropAllTablesStmt();
        DbStmtResult res = await _exec.ExecuteWithRetry(stmt, ct);
        if (res.IsSuccess)
            _logger.LogInformation("DropAllTables success");
        else
            _logger.LogError("DropAllTables failed with error {Error}", res.ErrorMessage);
        return res;
    }

    #endregion

    #region Generator

    public ValueTask<ulong> GetNextId64(CancellationToken ct) => GetIdRange64(1, ct);

    public async ValueTask<ulong> GetIdRange64(uint count, CancellationToken ct) {
        if (count == 0)
            throw new ArgumentOutOfRangeException(nameof(count), "Count cannot be 0");

        // Optimistic path: in most cases
        lock (_generatorMutex) {
            if (_lastUsed + count <= _endId) {
                ulong result = _lastUsed + 1;
                _lastUsed += count;
                return result;
            }
        }

        // Lock the DB update mutex
        using var locker = new SemaphoreLocker(_generatorMutex);
        await locker.Acquire(ct);

        // May have been changed already by another thread, so check again
        lock (_generatorMutex) {
            if (_lastUsed + count <= _endId) {
                ulong result = _lastUsed + 1;
                _lastUsed += count;
                return result;
            }
        }

        // Update in blocks
        const uint BLOCK_SIZE = 65536;
        uint idRange = count - count % BLOCK_SIZE + BLOCK_SIZE;
        var stmt = new ReserveIdRangeStmt(idRange);
        DbStmtResult res = await _exec.ExecuteWithRetry(stmt, ct, 0);

        if (res.IsSuccess) {
            lock (_generatorMutex) {
                _endId = (ulong)stmt.LastReserved;
                _lastUsed = (ulong)(stmt.LastReserved - BLOCK_SIZE);
                ulong result = _lastUsed + 1;
                _lastUsed += count;
                return result;
            }
        } else {
            throw new InvalidOperationException("Failed to get next id from database");
        }
    }

    #endregion

    #region Companies

    public async Task<Result<Company>> GetCompanyById(ulong companyId, CancellationToken ct) {
        var stmt = new GetCompanyByIdStmt(companyId);
        DbStmtResult res = await _exec.ExecuteQueryWithRetry(stmt, ct);
        if (res.IsSuccess) {
            _logger.LogInformation("GetCompanyById success - Company: {Company}", stmt.Company);
            return Result<Company>.Success(stmt.Company);
        } else {
            _logger.LogWarning("GetCompanyById failed with error {Error}", res.ErrorMessage);
            return Result<Company>.Failure(res);
        }
    }

    public async Task<Result<IReadOnlyCollection<Company>>> GetAllCompaniesByDataSource(
        string dataSource, CancellationToken ct) {
        var stmt = new GetAllCompaniesByDataSourceStmt(dataSource);
        DbStmtResult res = await _exec.ExecuteQueryWithRetry(stmt, ct);
        if (res.IsSuccess) {
            _logger.LogInformation("GetAllCompaniesByDataSource success - Num companies: {NumCompanies}", stmt.Companies.Count);
            return Result<IReadOnlyCollection<Company>>.Success(stmt.Companies);
        } else {
            _logger.LogWarning("GetAllCompaniesByDataSource failed with error {Error}", res.ErrorMessage);
            return Result<IReadOnlyCollection<Company>>.Failure(res);
        }
    }

    public async Task<Result<PagedCompanies>> GetPagedCompaniesByDataSource(
        string dataSource, PaginationRequest pagination, CancellationToken ct) {
        var stmt = new GetPagedCompaniesByDataSourceStmt(dataSource, pagination);
        DbStmtResult res = await _exec.ExecuteQueryWithRetry(stmt, ct);
        if (res.IsSuccess) {
            _logger.LogInformation("GetCompaniesByDataSource success - Num companies: {NumCompanies}", stmt.Companies.Count);
            return Result<PagedCompanies>.Success(stmt.GetPagedCompanies());
        } else {
            _logger.LogWarning("GetCompaniesByDataSource failed with error {Error}", res.ErrorMessage);
            return Result<PagedCompanies>.Failure(res);
        }
    }

    public async Task<Result> EmptyCompaniesTables(CancellationToken ct) {
        var stmt = new TruncateCompaniesTablesStmt();
        DbStmtResult res = await _exec.ExecuteWithRetry(stmt, ct);
        if (res.IsSuccess)
            _logger.LogInformation("EmptyCompaniesTables success");
        else
            _logger.LogError("EmptyCompaniesTables failed with error {Error}", res.ErrorMessage);
        return res;
    }

    public async Task<Result> BulkInsertCompanies(List<Company> companies, CancellationToken ct) {
        var stmt = new BulkInsertCompaniesStmt(companies);
        DbStmtResult res = await _exec.ExecuteWithRetry(stmt, ct);
        if (res.IsSuccess)
            _logger.LogInformation("BulkInsertCompanies success - Num companies: {NumCompanies}", companies.Count);
        else
            _logger.LogError("BulkInsertCompanies failed with error {Error}", res.ErrorMessage);
        return res;
    }

    public async Task<Result> BulkInsertCompanyNames(List<CompanyName> companyNames, CancellationToken ct) {
        var stmt = new BulkInsertCompanyNamesStmt(companyNames);
        DbStmtResult res = await _exec.ExecuteWithRetry(stmt, ct);
        if (res.IsSuccess)
            _logger.LogInformation("BulkInsertCompanyNames success - Num company names: {NumCompanyNames}", companyNames.Count);
        else
            _logger.LogError("BulkInsertCompanyNames failed with error {Error}", res.ErrorMessage);
        return res;
    }

    #endregion

    #region Data points

    public async Task<Result<IReadOnlyCollection<DataPointUnit>>> GetDataPointUnits(CancellationToken ct) {
        var stmt = new GetAllDataPointUnitsStmt();
        DbStmtResult res = await _exec.ExecuteQueryWithRetry(stmt, ct);
        if (res.IsSuccess) {
            _logger.LogInformation("GetDataPointUnits success - Num units: {NumUnits}", stmt.Units.Count);
            return Result<IReadOnlyCollection<DataPointUnit>>.Success(stmt.Units);
        } else {
            _logger.LogWarning("GetDataPointUnits failed with error {Error}", res.ErrorMessage);
            return Result<IReadOnlyCollection<DataPointUnit>>.Failure(res);
        }
    }

    public async Task<Result> InsertDataPointUnit(DataPointUnit dataPointUnit, CancellationToken ct) {
        var stmt = new InsertDataPointUnitStmt(dataPointUnit);
        DbStmtResult res = await _exec.ExecuteWithRetry(stmt, ct);
        if (res.IsSuccess)
            _logger.LogInformation("InsertDataPointUnit success - Unit: {Unit}", dataPointUnit);
        else
            _logger.LogError("InsertDataPointUnit failed with error {Error}", res.ErrorMessage);
        return res;
    }

    public async Task<Result> BulkInsertDataPoints(List<DataPoint> dataPoints, CancellationToken ct) {
        var stmt = new BulkInsertDataPointsStmt(dataPoints);
        DbStmtResult res = await _exec.ExecuteWithRetry(stmt, ct);
        if (res.IsSuccess)
            _logger.LogInformation("BulkInsertDataPoints success - Num data points: {NumDataPoints}", dataPoints.Count);
        else
            _logger.LogError("BulkInsertDataPoints failed with error {Error}", res.ErrorMessage);
        return res;
    }

    #endregion

    #region Company submissions

    public async Task<Result<IReadOnlyCollection<Submission>>> GetSubmissions(CancellationToken ct) {
        var stmt = new GetAllSubmissionsStmt();
        DbStmtResult res = await _exec.ExecuteQueryWithRetry(stmt, ct);
        if (res.IsSuccess) {
            _logger.LogInformation("GetSubmissions success - Num submissions: {NumSubmissions}", stmt.Submissions.Count);
            return Result<IReadOnlyCollection<Submission>>.Success(stmt.Submissions);
        } else {
            _logger.LogError("GetSubmissions failed with error {Error}", res.ErrorMessage);
            return Result<IReadOnlyCollection<Submission>>.Failure(res);
        }
    }

    public async Task<Result> BulkInsertSubmissions(List<Submission> batch, CancellationToken ct) {
        var stmt = new BulkInsertSubmissionsStmt(batch);
        DbStmtResult res = await _exec.ExecuteWithRetry(stmt, ct);

        if (res.IsFailure && res.ErrorCode is ErrorCodes.Duplicate) {
            // Nothing got written. Retry the batch one by one (slow)
            _logger.LogInformation("BulkInsertSubmissions failed due to duplicates, retrying one by one. {NumSubmissions} to insert",
                batch.Count);
            res = await RetryInsertSubmissions(batch, ct);
        } else if (res.IsFailure) {
            _logger.LogError("BulkInsertSubmissions failed with error {Error}", res.ErrorMessage);
        } else {
            _logger.LogInformation("BulkInsertSubmissions success - Num submissions: {NumSubmissions}", batch.Count);
        }
        return res;
    }


    private async Task<DbStmtResult> RetryInsertSubmissions(List<Submission> batch, CancellationToken ct) {
        _logger.LogWarning("Failed to bulk insert submissions due to duplicates, retrying one by one. {NumSubmissions} to insert",
            batch.Count);

        int successCount = 0;
        int failureCount = 0;
        var insertOneSubmissionStmt = new InsertSubmissionStmt();
        foreach (Submission submission in batch) {
            insertOneSubmissionStmt.Submission = submission;
            DbStmtResult res = await _exec.ExecuteWithRetry(insertOneSubmissionStmt, ct);
            ProcessSubmissionResult(ref successCount, ref failureCount, submission, res);
        }

        _logger.LogWarning("BulkInsertSubmissions failed to insert {FailureCount} submissions, succeeded with {SuccessCount}",
            failureCount, successCount);

        return DbStmtResult.StatementFailure(
            ErrorCodes.Duplicate,
            $"BulkInsertSubmissions failed to insert {failureCount} submissions, succeeded with {successCount}");

        // Local helper methods

        void ProcessSubmissionResult(ref int successCount, ref int failureCount, Submission submission, DbStmtResult res) {
            if (res.IsSuccess)                 successCount++;
else {
                failureCount++;
                _logger.LogWarning("BulkInsertSubmissions failed to insert submission {Submission} with error {Error}",
                    submission, res);
            }
        }
    }

    #endregion

    public void Dispose() {
        // Dispose of Postgres executor
        _exec.Dispose();

        // Dispose of the generator mutex
        _generatorMutex.Dispose();
    }
}
