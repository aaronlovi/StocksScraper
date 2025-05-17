using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;

namespace Stocks.Persistence.DistributedCaching;

public sealed class InMemoryDistributedLockService : IDistributedLockService {
    private readonly IDistributedCache _cache;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _lockGuards = new();

    public InMemoryDistributedLockService(IDistributedCache cache) {
        _cache = cache;
    }

    public async Task<IDistributedLock> TryAcquireAsync(string lockKey, TimeSpan ttl, TimeSpan? waitTime = null, bool enableAutoRenewal = true) {
        await CleanupLockGuardAsync(lockKey);

        SemaphoreSlim semaphore = _lockGuards.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));

        if (waitTime is null) {
            if (!await semaphore.WaitAsync(0)) {
                return Failure();
            } else {
                await _cache.SetAsync(lockKey, Encoding.UTF8.GetBytes(lockKey), GetCacheOptions());
                return Success();
            }
        }

        if (!await semaphore.WaitAsync(waitTime.Value)) {
            return Failure();
        }

        await _cache.SetAsync(lockKey, Encoding.UTF8.GetBytes(lockKey), GetCacheOptions());
        return Success();

        // Local helper functions

        InMemoryDistributedLock Success() => new(lockKey, true, this);
        InMemoryDistributedLock Failure() => new(lockKey, false, this);
        DistributedCacheEntryOptions GetCacheOptions() => new() {
            AbsoluteExpirationRelativeToNow = ttl
        };
    }

    internal async Task ReleaseAsync(string lockKey) {
        // For thread-safety, invalidate the item from the cache before releasing locks
        await _cache.RemoveAsync(lockKey);

        if (_lockGuards.TryGetValue(lockKey, out SemaphoreSlim? semaphore))
            _ = semaphore.Release();

        // Clean up the lock guard if necessary
        await CleanupLockGuardAsync(lockKey);
    }

    #region PRIVATE HELPER METHODS

    private async Task CleanupLockGuardAsync(string lockKey) {
        // The lock may have been previously hed but is now expired from the cache.
        // Just in case, remove it from the lock guards.

        if (await _cache.GetAsync(lockKey) is null)
            _ = _lockGuards.TryRemove(lockKey, out _);
    }

    #endregion
}
