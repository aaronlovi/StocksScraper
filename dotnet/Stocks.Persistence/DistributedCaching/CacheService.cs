using System;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Stocks.Persistence.DistributedCaching.Models;
using Stocks.Persistence.DistributedCaching.Statements;
using Stocks.Shared;
using Stocks.Shared.Models;

namespace Stocks.Persistence.DistributedCaching;
public class CacheService : ICacheService {
    private readonly CacheExecutor _exec;
    private readonly IDistributedLockService _distributedLockService;
    private readonly ILogger<CacheService> _logger;
    private readonly Meter _meter;
    private readonly Counter<long> _cacheHitsCounter;
    private readonly Counter<long> _cacheMissesCounter;
    private readonly Counter<long> _cacheErrorsCounter;

    public CacheService(CacheExecutor exec, IDistributedLockService distributedLockService, IConfiguration cfg, ILogger<CacheService> logger) {
        _exec = exec;
        _distributedLockService = distributedLockService;
        _meter = new("Stocks.Persistence.DistributedCaching", "1.0");
        _logger = logger;

        _cacheHitsCounter = _meter.CreateCounter<long>("cache.hits", "hits", "Count of cache hits");
        _cacheMissesCounter = _meter.CreateCounter<long>("cache.misses", "misses", "Count of cache misses");
        _cacheErrorsCounter = _meter.CreateCounter<long>("cache.errors", "errors", "Count of cache errors");

        // Bind configuration section to CacheEntryOptions
        CacheEntryOptions cacheEntryOptions = CacheEntryOptions.Default;
        cfg.GetSection("AppSettings:CacheSettings:CachedUserOptions").Bind(cacheEntryOptions);
        // _cachedUserOptions = cacheEntryOptions.ToDistributedCacheEntryOptions();
        //_logger.LogInformation("CacheService user cache options: {Options}", _cachedUserOptions);
    }

    #region PRIVATE HELPER METHODS

    private void IncrementCacheHitCounter() => _cacheHitsCounter.Add(1);
    private void IncrementCacheMissCounter() => _cacheMissesCounter.Add(1);
    private void IncrementCacheErrorCounter() => _cacheErrorsCounter.Add(1);

#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable CA2254 // Template should be a static expression

    private async Task<Result<T>> GetCachedItem<T>(
        QueryFromCacheStmtBase stmt,
        Func<QueryFromCacheStmtBase, T> resultExtractorFn,
        string notFoundMessage,
        CancellationToken ct,
        [CallerMemberName] string callerMemberFnName = "") {
        try {
            CacheStmtResult result = await _exec.ExecuteQueryWithRetry(stmt, ct);

            if (result.IsFailure || stmt.IsResultsEmpty) {
                _logger.LogInformation(callerMemberFnName + " - item not found in cache");
                IncrementCacheMissCounter();
                return Result<T>.Failure(ErrorCodes.NotFound, notFoundMessage);
            }

            IncrementCacheHitCounter();
            return Result<T>.Success(resultExtractorFn(stmt));
        } catch (Exception ex) {
            _logger.LogError(ex, callerMemberFnName + " - general fault");
            IncrementCacheErrorCounter();
            return Result<T>.Failure(ErrorCodes.GenericError, "An error occurred while retrieving item from cache.");
        }
    }

    private async Task<Result> SetCachedItem(
        WriteToCacheStmtBase stmt,
        string failureMessage,
        CancellationToken ct,
        [CallerMemberName] string callerMemberFnName = "") {
        try {
            CacheStmtResult result = await _exec.ExecuteWriteWithRetry(stmt, ct);

            if (result.IsFailure) {
                _logger.LogError(callerMemberFnName + " - error setting item in cache: {Error}", result.ErrorMessage);
                IncrementCacheErrorCounter();
                return Result.Failure(ErrorCodes.GenericError, failureMessage);
            }

            _logger.LogInformation(callerMemberFnName + " - successfully set item set in cache");
            return Result.Success;
        } catch (Exception ex) {
            _logger.LogError(ex, callerMemberFnName + " - general fault");
            IncrementCacheErrorCounter();
            return Result.Failure(ErrorCodes.GenericError, "An error occurred while setting item in cache.");
        }
    }

    private async Task<Result> InvalidateCachedItem(
        InvalidateCacheStmtBase stmt,
        string failureMessage,
        CancellationToken ct,
        [CallerMemberName] string callerMemberFnName = "") {
        try {
            CacheStmtResult result = await _exec.ExecuteWriteWithRetry(stmt, ct);

            if (result.IsFailure) {
                _logger.LogError(callerMemberFnName + " - error invalidating item in cache: {Error}", result.ErrorMessage);
                IncrementCacheErrorCounter();
                return Result.Failure(ErrorCodes.GenericError, failureMessage);
            }

            _logger.LogInformation(callerMemberFnName + " - successfully invalidated item in cache");
            return Result.Success;
        } catch (Exception ex) {
            _logger.LogError(ex, callerMemberFnName + " - general fault");
            IncrementCacheErrorCounter();
            return Result.Failure(ErrorCodes.GenericError, "An error occurred while invalidating item in cache.");
        }
    }

#pragma warning restore CA2254 // Template should be a static expression
#pragma warning restore IDE0079 // Remove unnecessary suppression

    #endregion
}
