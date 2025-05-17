using System;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Stocks.Persistence.DistributedCaching.Statements;
using Stocks.Shared.Metrics;
using Stocks.Shared.Models;

namespace Stocks.Persistence.DistributedCaching;

public class CacheExecutor : IDisposable {
    private bool _isDisposed;

    private readonly int _maxRetries = 5; // 0 = unlimited
    private readonly int _retryDelayMilliseconds = 1000; // 0 = no delay
    private readonly int _maxConcurrentStatements = 20;
    private readonly int _maxConcurrentReadStatements = 20;
    private readonly SemaphoreSlim _connectionLimiter;
    private readonly SemaphoreSlim _readConnectionLimiter;
    private readonly Meter _meter;
    private readonly IDistributedCache _cache;
    private readonly ILogger<CacheExecutor> _logger;

    public CacheExecutor(IConfiguration cfg, IDistributedCache cache, ILogger<CacheExecutor> logger) {
        _cache = cache;
        _meter = new("Stocks.Persistence.DistributedCaching", "1.0");
        _logger = logger;
        _logger.LogInformation("Caching is enabled");

        Parse("MaxRetries", ref _maxRetries);
        Parse("RetryDelayMilliseconds", ref _retryDelayMilliseconds);
        Parse("MaxConcurrentStatements", ref _maxConcurrentStatements);
        Parse("MaxConcurrentReadStatements", ref _maxConcurrentReadStatements);

        _logger.LogInformation("CacheManager settings: (maxRetries:{MaxRetries}, retryDelayMilliseconds:{RetryDelayMilliseconds}, maxConcurrentStatements:{MaxConcurrentStatements}, maxConcurrentReadStatements:{MaxConcurrentReadStatements})",
            _maxRetries, _retryDelayMilliseconds, _maxConcurrentStatements, _maxConcurrentReadStatements);

        _connectionLimiter = new(_maxConcurrentStatements);
        _readConnectionLimiter = new(_maxConcurrentReadStatements);

        // Local helper methods

        void Parse(string key, ref int value) {
            if (int.TryParse(cfg[$"AppSettings:CacheSettings:{key}"], out int parsedValue))
                value = parsedValue;
            else
                _logger.LogWarning("Failed to parse {Key} from configuration, using default value {Value}", key, value);
        }
    }

    public async Task<CacheStmtResult> ExecuteQuery(QueryFromCacheStmtBase stmt, CancellationToken ct) {
        try {
            using var limiter = new SemaphoreLocker(_readConnectionLimiter);
            await limiter.Acquire(ct);
            using MetricsRecorder recorder = StartQueryMetricsRecorder();
            return await stmt.Execute(_cache, ct);
        } catch (RedisTimeoutException ex) {
            _logger.LogWarning(ex, "Transient error in ExecuteQuery: {Message}", ex.Message);
            throw; // Retriable
        } catch (RedisConnectionException ex) {
            _logger.LogWarning(ex, "Connection error in ExecuteQuery: {Message}", ex.Message);
            throw; // Retriable
        } catch (RedisCommandException ex) {
            _logger.LogError(ex, "Command error in ExecuteQuery: {Message}", ex.Message);
            throw; // Not retriable
        } catch (Exception ex) {
            _logger.LogError(ex, "Unexpected error in ExecuteQuery: {Message}", ex.Message);
            throw; // Not retriable
        }
    }

    public async Task<CacheStmtResult> ExecuteQueryWithRetry(QueryFromCacheStmtBase stmt, CancellationToken ct, int? overrideMaxRetries = null) {
        int effectiveMaxRetries = overrideMaxRetries ?? _maxRetries;
        if (effectiveMaxRetries == 0)
            effectiveMaxRetries = int.MaxValue;

        for (int retries = 0; retries < effectiveMaxRetries; retries++) {
            try {
                return await ExecuteQuery(stmt, ct);
            } catch (Exception ex) {
                if (IsRetriable(ex))
                    await Task.Delay(TimeSpan.FromMilliseconds(_retryDelayMilliseconds), ct);
                else
                    throw;
            }

            // The following is implicit, and occurs on cancellation
            // catch (OperationCanceledException) { throw; }
        }

        return CacheStmtResult.Failure(ErrorCodes.TooManyRetries, "Max retries exceeded");
    }

    public async Task<CacheStmtResult> ExecuteWrite(IWritingDistributedCacheStmt stmt, CancellationToken ct) {
        try {
            using var limiter = new SemaphoreLocker(_connectionLimiter);
            await limiter.Acquire(ct);
            using MetricsRecorder recorder = StartNonQueryMetricsRecorder();
            return await stmt.Execute(_cache, ct);
        } catch (RedisTimeoutException ex) {
            _logger.LogWarning(ex, "Transient error in ExecuteWrite: {Message}", ex.Message);
            throw; // Retriable
        } catch (RedisConnectionException ex) {
            _logger.LogWarning(ex, "Connection error in ExecuteWrite: {Message}", ex.Message);
            throw; // Retriable
        } catch (RedisCommandException ex) {
            _logger.LogError(ex, "Command error in ExecuteWrite: {Message}", ex.Message);
            throw; // Not retriable
        } catch (Exception ex) {
            _logger.LogError(ex, "Unexpected error in ExecuteWrite: {Message}", ex.Message);
            throw; // Not retriable
        }
    }

    public async Task<CacheStmtResult> ExecuteWriteWithRetry(IWritingDistributedCacheStmt stmt, CancellationToken ct, int? overrideMaxRetries = null) {
        int effectiveMaxRetries = overrideMaxRetries ?? _maxRetries;
        if (effectiveMaxRetries == 0)
            effectiveMaxRetries = int.MaxValue;

        for (int retries = 0; retries < effectiveMaxRetries; retries++) {
            try {
                return await ExecuteWrite(stmt, ct);
            } catch (Exception ex) {
                if (IsRetriable(ex))
                    await Task.Delay(TimeSpan.FromMilliseconds(_retryDelayMilliseconds), ct);
                else
                    throw;
            }

            // The following is implicit, and occurs on cancellation
            // catch (OperationCanceledException) { throw; }
        }
        return CacheStmtResult.Failure(ErrorCodes.TooManyRetries, "Max retries exceeded");
    }

    #region IDisposable implementation

    protected virtual void Dispose(bool disposing) {
        if (!_isDisposed) {
            if (disposing) {
                _connectionLimiter.Dispose();
                _readConnectionLimiter.Dispose();
                _meter.Dispose();
            }
            _isDisposed = true;
        }

        // TODO: free unmanaged resources (unmanaged objects) and override finalizer
        // TODO: set large fields to null
        _isDisposed = true;
    }

    public void Dispose() {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    #endregion

    #region PRIVATE HELPER METHODS

    private MetricsRecorder StartQueryMetricsRecorder() => StartMetricsRecorder("cache.query.executiontime", "ms", "Execution time of cached queries");

    private MetricsRecorder StartNonQueryMetricsRecorder() => StartMetricsRecorder("cache.nonquery.executiontime", "ms", "Execution time of non-query cached statements");

    private MetricsRecorder StartMetricsRecorder(
        string name = "cache.general.executiontime",
        string? unit = "ms",
        string? description = "Execution time of cached statements")
        => new(_meter, name, unit, description);

    private static bool IsRetriable(Exception ex) => ex is RedisTimeoutException or RedisConnectionException;

    #endregion
}
