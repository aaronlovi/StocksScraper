using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Stocks.DataModels;
using Stocks.Persistence.Database.DTO.Taxonomies;
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
        uint idRange = count - (count % BLOCK_SIZE) + BLOCK_SIZE;
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

    public async Task<Result<PagedResults<CompanySearchResult>>> SearchCompanies(string query, PaginationRequest pagination, CancellationToken ct) {
        var stmt = new SearchCompaniesStmt(query, pagination);
        DbStmtResult res = await _exec.ExecuteQueryWithRetry(stmt, ct);
        if (res.IsSuccess) {
            _logger.LogInformation("SearchCompanies success - Query: {Query}, Results: {NumResults}", query, stmt.Results.Count);
            return Result<PagedResults<CompanySearchResult>>.Success(stmt.GetPagedResults());
        } else {
            _logger.LogWarning("SearchCompanies failed with error {Error}", res.ErrorMessage);
            return Result<PagedResults<CompanySearchResult>>.Failure(res);
        }
    }

    public async Task<Result<Company>> GetCompanyByCik(string cik, CancellationToken ct) {
        if (!ulong.TryParse(cik, out ulong cikValue)) {
            _logger.LogWarning("GetCompanyByCik failed - invalid CIK: {Cik}", cik);
            return Result<Company>.Failure(ErrorCodes.NotFound, $"Invalid CIK: {cik}");
        }

        var stmt = new GetCompanyByCikStmt(cikValue);
        DbStmtResult res = await _exec.ExecuteQueryWithRetry(stmt, ct);
        if (res.IsSuccess && stmt.Company.CompanyId != 0) {
            _logger.LogInformation("GetCompanyByCik success - CIK: {Cik}", cik);
            return Result<Company>.Success(stmt.Company);
        } else {
            _logger.LogWarning("GetCompanyByCik not found - CIK: {Cik}", cik);
            return Result<Company>.Failure(ErrorCodes.NotFound, $"Company not found for CIK: {cik}");
        }
    }

    #endregion

    #region Company tickers

    public async Task<Result> BulkInsertCompanyTickers(List<CompanyTicker> tickers, CancellationToken ct) {
        var stmt = new BulkInsertCompanyTickersStmt(tickers);
        DbStmtResult res = await _exec.ExecuteWithRetry(stmt, ct);
        if (res.IsSuccess)
            _logger.LogInformation("BulkInsertCompanyTickers success - Num tickers: {NumTickers}", tickers.Count);
        else
            _logger.LogError("BulkInsertCompanyTickers failed with error {Error}", res.ErrorMessage);
        return res;
    }

    public async Task<Result<IReadOnlyCollection<CompanyTicker>>> GetCompanyTickersByCompanyId(ulong companyId, CancellationToken ct) {
        var stmt = new GetCompanyTickersByCompanyIdStmt(companyId);
        DbStmtResult res = await _exec.ExecuteQueryWithRetry(stmt, ct);
        if (res.IsSuccess) {
            _logger.LogInformation("GetCompanyTickersByCompanyId success - CompanyId: {CompanyId}, Num tickers: {NumTickers}", companyId, stmt.Tickers.Count);
            return Result<IReadOnlyCollection<CompanyTicker>>.Success(stmt.Tickers);
        } else {
            _logger.LogWarning("GetCompanyTickersByCompanyId failed with error {Error}", res.ErrorMessage);
            return Result<IReadOnlyCollection<CompanyTicker>>.Failure(res);
        }
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

    public async Task<Result> BulkInsertTaxonomyConcepts(List<ConceptDetailsDTO> taxonomyConcepts, CancellationToken ct) {
        var stmt = new BulkInsertTaxonomyConceptsStmt(taxonomyConcepts);
        DbStmtResult res = await _exec.ExecuteWithRetry(stmt, ct);
        if (res.IsSuccess)
            _logger.LogInformation("BulkInsertTaxonomyConcepts success - Num concepts: {NumConcepts}", taxonomyConcepts.Count);
        else
            _logger.LogError("BulkInsertTaxonomyConcepts failed with error {Error}", res.ErrorMessage);
        return res;
    }

    public async Task<Result<IReadOnlyCollection<ConceptDetailsDTO>>> GetTaxonomyConceptsByTaxonomyType(
        int taxonomyTypeId, CancellationToken ct) {
        var stmt = new GetTaxonomyConceptsByTaxonomyTypeStmt(taxonomyTypeId);
        DbStmtResult res = await _exec.ExecuteQueryWithRetry(stmt, ct);
        if (res.IsSuccess) {
            _logger.LogInformation("GetTaxonomyConceptsByTaxonomyType success - Num concepts: {NumConcepts}", stmt.TaxonomyConcepts.Count);
            return Result<IReadOnlyCollection<ConceptDetailsDTO>>.Success(stmt.TaxonomyConcepts);
        } else {
            _logger.LogWarning("GetTaxonomyConceptsByTaxonomyType failed with error {Error}", res.ErrorMessage);
            return Result<IReadOnlyCollection<ConceptDetailsDTO>>.Failure(res);
        }
    }

    public async Task<Result> BulkInsertTaxonomyPresentations(List<PresentationDetailsDTO> taxonomyPresentations, CancellationToken ct) {
        var stmt = new BulkInsertTaxonomyPresentationStmt(taxonomyPresentations);
        DbStmtResult res = await _exec.ExecuteWithRetry(stmt, ct);
        if (res.IsSuccess)
            _logger.LogInformation("BulkInsertTaxonomyPresentations success - Num presentations: {NumPresentations}", taxonomyPresentations.Count);
        else
            _logger.LogError("BulkInsertTaxonomyPresentations failed with error {Error}", res.ErrorMessage);
        return res;
    }

    public async Task<Result<IReadOnlyCollection<PresentationDetailsDTO>>> GetTaxonomyPresentationsByTaxonomyType(int taxonomyTypeId, CancellationToken ct) {
        var stmt = new Statements.GetTaxonomyPresentationsByTaxonomyTypeStmt(taxonomyTypeId);
        DbStmtResult res = await _exec.ExecuteQueryWithRetry(stmt, ct);
        if (res.IsSuccess) {
            _logger.LogInformation("GetTaxonomyPresentationsByTaxonomyType success - Num presentations: {NumPresentations}", stmt.Presentations.Count);
            return Result<IReadOnlyCollection<PresentationDetailsDTO>>.Success(stmt.Presentations);
        } else {
            _logger.LogWarning("GetTaxonomyPresentationsByTaxonomyType failed with error {Error}", res.ErrorMessage);
            return Result<IReadOnlyCollection<PresentationDetailsDTO>>.Failure(res);
        }
    }

    public async Task<Result<IReadOnlyCollection<DataPoint>>> GetDataPointsForSubmission(ulong companyId, ulong submissionId, CancellationToken ct) {
        var stmt = new Statements.GetDataPointsForSubmissionStmt(companyId, submissionId);
        DbStmtResult res = await _exec.ExecuteQueryWithRetry(stmt, ct);
        if (res.IsSuccess) {
            _logger.LogInformation("GetDataPointsForSubmission success - Num data points: {NumDataPoints}", stmt.DataPoints.Count);
            return Result<IReadOnlyCollection<DataPoint>>.Success(stmt.DataPoints);
        } else {
            _logger.LogWarning("GetDataPointsForSubmission failed with error {Error}", res.ErrorMessage);
            return Result<IReadOnlyCollection<DataPoint>>.Failure(res);
        }
    }
    #endregion

    #region Taxonomy types

    public async Task<Result<TaxonomyTypeInfo>> GetTaxonomyTypeByNameVersion(string name, int version, CancellationToken ct) {
        var stmt = new GetTaxonomyTypeByNameVersionStmt(name, version);
        DbStmtResult res = await _exec.ExecuteQueryWithRetry(stmt, ct);
        if (res.IsSuccess && stmt.TaxonomyType is not null) {
            _logger.LogInformation("GetTaxonomyTypeByNameVersion success - Name: {Name}, Version: {Version}", name, version);
            return Result<TaxonomyTypeInfo>.Success(stmt.TaxonomyType);
        } else {
            string error = res.IsFailure ? res.ErrorMessage : "Taxonomy type not found";
            _logger.LogWarning("GetTaxonomyTypeByNameVersion failed - Name: {Name}, Version: {Version}, Error: {Error}", name, version, error);
            return Result<TaxonomyTypeInfo>.Failure(ErrorCodes.NotFound, error);
        }
    }

    public async Task<Result<TaxonomyTypeInfo>> EnsureTaxonomyType(string name, int version, CancellationToken ct) {
        Result<TaxonomyTypeInfo> existing = await GetTaxonomyTypeByNameVersion(name, version, ct);
        if (existing.IsSuccess)
            return existing;

        var maxStmt = new GetMaxTaxonomyTypeIdStmt();
        DbStmtResult maxRes = await _exec.ExecuteQueryWithRetry(maxStmt, ct);
        if (maxRes.IsFailure) {
            _logger.LogError("EnsureTaxonomyType failed to read max id. Error: {Error}", maxRes.ErrorMessage);
            return Result<TaxonomyTypeInfo>.Failure(maxRes);
        }

        int nextId = maxStmt.MaxId + 1;
        var insertStmt = new InsertTaxonomyTypeStmt(nextId, name, version);
        DbStmtResult insertRes = await _exec.ExecuteWithRetry(insertStmt, ct);
        if (insertRes.IsFailure) {
            _logger.LogError("EnsureTaxonomyType failed to insert. Error: {Error}", insertRes.ErrorMessage);
            return Result<TaxonomyTypeInfo>.Failure(insertRes);
        }

        var created = new TaxonomyTypeInfo(nextId, name, version);
        _logger.LogInformation("EnsureTaxonomyType inserted - Name: {Name}, Version: {Version}, Id: {Id}", name, version, nextId);
        return Result<TaxonomyTypeInfo>.Success(created);
    }

    public async Task<Result<int>> GetTaxonomyConceptCountByType(int taxonomyTypeId, CancellationToken ct) {
        var stmt = new GetTaxonomyConceptCountByTypeStmt(taxonomyTypeId);
        DbStmtResult res = await _exec.ExecuteQueryWithRetry(stmt, ct);
        if (res.IsSuccess)
            return Result<int>.Success(stmt.Count);
        _logger.LogWarning("GetTaxonomyConceptCountByType failed - TypeId: {TypeId}, Error: {Error}", taxonomyTypeId, res.ErrorMessage);
        return Result<int>.Failure(res);
    }

    public async Task<Result<int>> GetTaxonomyPresentationCountByType(int taxonomyTypeId, CancellationToken ct) {
        var stmt = new GetTaxonomyPresentationCountByTypeStmt(taxonomyTypeId);
        DbStmtResult res = await _exec.ExecuteQueryWithRetry(stmt, ct);
        if (res.IsSuccess)
            return Result<int>.Success(stmt.Count);
        _logger.LogWarning("GetTaxonomyPresentationCountByType failed - TypeId: {TypeId}, Error: {Error}", taxonomyTypeId, res.ErrorMessage);
        return Result<int>.Failure(res);
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

    public async Task<Result<IReadOnlyCollection<Submission>>> GetSubmissionsByCompanyId(ulong companyId, CancellationToken ct) {
        var stmt = new GetSubmissionsByCompanyIdStmt(companyId);
        DbStmtResult res = await _exec.ExecuteQueryWithRetry(stmt, ct);
        if (res.IsSuccess) {
            _logger.LogInformation("GetSubmissionsByCompanyId success - CompanyId: {CompanyId}, Num submissions: {NumSubmissions}", companyId, stmt.Submissions.Count);
            return Result<IReadOnlyCollection<Submission>>.Success(stmt.Submissions);
        } else {
            _logger.LogWarning("GetSubmissionsByCompanyId failed with error {Error}", res.ErrorMessage);
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
            if (res.IsSuccess) {
                successCount++;
            } else {
                failureCount++;
                _logger.LogWarning("BulkInsertSubmissions failed to insert submission {Submission} with error {Error}",
                    submission, res);
            }
        }
    }

    #endregion

    #region Prices

    public async Task<Result<IReadOnlyCollection<PriceImportStatus>>> GetPriceImportStatuses(CancellationToken ct) {
        var stmt = new GetPriceImportsStmt();
        DbStmtResult res = await _exec.ExecuteQueryWithRetry(stmt, ct);
        if (res.IsSuccess) {
            _logger.LogInformation("GetPriceImportStatuses success - Num imports: {NumImports}", stmt.Imports.Count);
            return Result<IReadOnlyCollection<PriceImportStatus>>.Success(stmt.Imports);
        } else {
            _logger.LogWarning("GetPriceImportStatuses failed with error {Error}", res.ErrorMessage);
            return Result<IReadOnlyCollection<PriceImportStatus>>.Failure(res);
        }
    }

    public async Task<Result<IReadOnlyCollection<PriceRow>>> GetPricesByTicker(string ticker, CancellationToken ct) {
        var stmt = new GetPricesByTickerStmt(ticker);
        DbStmtResult res = await _exec.ExecuteQueryWithRetry(stmt, ct);
        if (res.IsSuccess) {
            _logger.LogInformation("GetPricesByTicker success - Ticker: {Ticker}, Num prices: {NumPrices}", ticker, stmt.Prices.Count);
            return Result<IReadOnlyCollection<PriceRow>>.Success(stmt.Prices);
        } else {
            _logger.LogWarning("GetPricesByTicker failed with error {Error}", res.ErrorMessage);
            return Result<IReadOnlyCollection<PriceRow>>.Failure(res);
        }
    }

    public async Task<Result> UpsertPriceImport(PriceImportStatus status, CancellationToken ct) {
        var stmt = new UpsertPriceImportStmt(status);
        DbStmtResult res = await _exec.ExecuteWithRetry(stmt, ct);
        if (res.IsSuccess) {
            _logger.LogInformation("UpsertPriceImport success - Ticker: {Ticker}, Cik: {Cik}, Exchange: {Exchange}",
                status.Ticker, status.Cik, status.Exchange ?? string.Empty);
        } else {
            _logger.LogError("UpsertPriceImport failed with error {Error}", res.ErrorMessage);
        }

        return res;
    }

    public async Task<Result> DeletePricesForTicker(string ticker, CancellationToken ct) {
        var stmt = new DeletePricesByTickerStmt(ticker);
        DbStmtResult res = await _exec.ExecuteWithRetry(stmt, ct);
        if (res.IsSuccess)
            _logger.LogInformation("DeletePricesForTicker success - Ticker: {Ticker}", ticker);
        else
            _logger.LogError("DeletePricesForTicker failed with error {Error}", res.ErrorMessage);
        return res;
    }

    public async Task<Result> BulkInsertPrices(List<PriceRow> prices, CancellationToken ct) {
        var stmt = new BulkInsertPricesStmt(prices);
        DbStmtResult res = await _exec.ExecuteWithRetry(stmt, ct);
        if (res.IsSuccess)
            _logger.LogInformation("BulkInsertPrices success - Num prices: {NumPrices}", prices.Count);
        else
            _logger.LogError("BulkInsertPrices failed with error {Error}", res.ErrorMessage);
        return res;
    }

    #endregion

    #region Price downloads

    public async Task<Result<IReadOnlyCollection<PriceDownloadStatus>>> GetPriceDownloadStatuses(CancellationToken ct) {
        var stmt = new GetPriceDownloadsStmt();
        DbStmtResult res = await _exec.ExecuteQueryWithRetry(stmt, ct);
        if (res.IsSuccess) {
            _logger.LogInformation("GetPriceDownloadStatuses success - Num downloads: {NumDownloads}", stmt.Downloads.Count);
            return Result<IReadOnlyCollection<PriceDownloadStatus>>.Success(stmt.Downloads);
        } else {
            _logger.LogWarning("GetPriceDownloadStatuses failed with error {Error}", res.ErrorMessage);
            return Result<IReadOnlyCollection<PriceDownloadStatus>>.Failure(res);
        }
    }

    public async Task<Result> UpsertPriceDownload(PriceDownloadStatus status, CancellationToken ct) {
        var stmt = new UpsertPriceDownloadStmt(status);
        DbStmtResult res = await _exec.ExecuteWithRetry(stmt, ct);
        if (res.IsSuccess) {
            _logger.LogInformation("UpsertPriceDownload success - Ticker: {Ticker}, Cik: {Cik}, Exchange: {Exchange}",
                status.Ticker, status.Cik, status.Exchange ?? string.Empty);
        } else {
            _logger.LogError("UpsertPriceDownload failed with error {Error}", res.ErrorMessage);
        }
        return res;
    }

    #endregion

    public void Dispose() {
        // Dispose of Postgres executor
        _exec.Dispose();

        // Dispose of the generator mutex
        _generatorMutex.Dispose();
    }
}
