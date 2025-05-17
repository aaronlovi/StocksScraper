using System;
using System.Diagnostics;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace Stocks.Persistence.DistributedCaching;

public sealed class RedisDistributedLockService : IDistributedLockService {
    private readonly IDatabase _redis;

    public RedisDistributedLockService(IConnectionMultiplexer multiplexer) {
        _redis = multiplexer.GetDatabase();
    }

    public async Task<IDistributedLock> TryAcquireAsync(string lockKey, TimeSpan ttl, TimeSpan? waitTime = null, bool enableAutoRenewal = true) {
        string value = Guid.NewGuid().ToString("N");
        var sleepInterval = TimeSpan.FromMilliseconds(50); // Interval between retries
        var stopWatch = Stopwatch.StartNew();

        while (true) {
            // Try to acquire the lock
            bool acquired = await _redis.StringSetAsync(key: lockKey, value: value, expiry: ttl, when: When.NotExists);

            if (acquired)
                return new RedisDistributedLock(_redis, lockKey, value, true, ttl, enableAutoRenewal);

            if (waitTime is null || stopWatch.Elapsed >= waitTime.Value)
                return new RedisDistributedLock(_redis, lockKey, value, false, TimeSpan.Zero, enableAutoRenewal);

            await Task.Delay(sleepInterval);
        }
    }
}
