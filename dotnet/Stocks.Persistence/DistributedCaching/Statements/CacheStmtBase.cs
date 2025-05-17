using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Stocks.Shared.Models;

namespace Stocks.Persistence.DistributedCaching.Statements;

public abstract class QueryFromCacheStmtBase : IDistributedCacheStmt {
    private readonly string _className;

    protected QueryFromCacheStmtBase(string className) {
        _className = className;
    }

    protected string? Serialized { get; private set; }
    public abstract bool IsResultsEmpty { get; }

    protected abstract string GetKey();

    public async Task<CacheStmtResult> Execute(IDistributedCache cache, CancellationToken ct) {
        ClearResults();

        try {
            string key = GetKey();
            int numRows = 0;
            Serialized = await cache.GetStringAsync(key, ct);

            if (!string.IsNullOrEmpty(Serialized)) {
                ProcessResults(Serialized);
                numRows = 1;
            }

            return CacheStmtResult.Success(numRows);
        } catch (Exception ex) {
            string errMsg = $"{_className} failed - {ex.Message}";
            return CacheStmtResult.Failure(ErrorCodes.GenericError, errMsg);
        }
    }

    /// <summary>
    /// Clears any results or state from a previous query execution.
    /// </summary>
    /// <remarks>
    /// This method is designed to reset the state of the derived query statement class,
    /// ensuring that it is ready for a new execution cycle. It should be called before
    /// executing a new queyr to prevent data from previous executions from affecting the
    /// results of the current execution. Derived classes should override this method to
    /// clear specific results or state informatoin related to their query.
    /// </remarks>
    protected virtual void ClearResults() => Serialized = null;

    /// <summary>
    /// Processes the curent row in the query result set.
    /// </summary>
    /// <remarks>
    /// This method is called for each cache queyr execution.
    /// Derived classes must implement htis method to define how individual results
    /// should be processed.
    /// The method provides direct access to the current result through the
    /// <paramref name="serialized"/> parameter, allowing derived classes to read the
    /// necessary data from the result.
    /// </remarks>
    protected abstract void ProcessResults(string serialized);
}

public abstract class WriteToCacheStmtBase : IWritingDistributedCacheStmt {
    private readonly string _className;

    protected WriteToCacheStmtBase(string className) {
        _className = className;
    }

    protected abstract string GetKey();
    protected abstract string GetSerializedValue();

    public async Task<CacheStmtResult> Execute(IDistributedCache cache, CancellationToken ct) {
        try {
            string key = GetKey();
            string value = GetSerializedValue();
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value))
                return CacheStmtResult.Failure(ErrorCodes.GenericError, "Key or value is null or empty.");

            await cache.SetStringAsync(key, value, GetCacheEntryOptions(), ct);

            return CacheStmtResult.Success(1); // 1 indicates one successful operation
        } catch (Exception ex) {
            string errMsg = $"{_className} failed - {ex.Message}";
            return CacheStmtResult.Failure(ErrorCodes.GenericError, errMsg);
        }
    }

    /// <summary>
    /// Provides cache entry options for the operation.
    /// </summary>
    /// <remarks>
    /// Derived classes can override this method to customize cache entry options,
    /// such as expiration time or sliding expiration.
    /// </remarks>
    protected virtual DistributedCacheEntryOptions GetCacheEntryOptions()
        => new() { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30) };
}

public abstract class InvalidateCacheStmtBase : IWritingDistributedCacheStmt {
    private readonly string _className;

    protected InvalidateCacheStmtBase(string className) {
        _className = className;
    }

    /// <summary>
    /// Gets the key of the cache entry to invalidate.
    /// </summary>
    /// <returns>The cache key to remove.</returns>
    protected abstract string GetKey();

    public async Task<CacheStmtResult> Execute(IDistributedCache cache, CancellationToken ct) {
        try {
            string key = GetKey();

            if (string.IsNullOrEmpty(key))
                return CacheStmtResult.Failure(ErrorCodes.GenericError, "Key is null or empty.");

            await cache.RemoveAsync(key, ct);

            return CacheStmtResult.Success(1); // 1 indicates one successful operation
        } catch (Exception ex) {
            string errMsg = $"{_className} failed - {ex.Message}";
            return CacheStmtResult.Failure(ErrorCodes.GenericError, errMsg);
        }
    }

    /// <summary>
    /// Provides cache entry options for the operation.
    /// </summary>
    /// <remarks>
    /// Derived classes can override this method to customize cache entry options,
    /// such as expiration time or sliding expiration.
    /// </remarks>
    protected virtual DistributedCacheEntryOptions GetCacheEntryOptions()
        => new() { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30) };
}
