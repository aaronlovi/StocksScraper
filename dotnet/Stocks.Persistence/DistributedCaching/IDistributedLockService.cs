using System;
using System.Threading.Tasks;

namespace Stocks.Persistence.DistributedCaching;

public interface IDistributedLockService {
    /// <summary>
    /// Attempts to acquire a lock for the given key with auto-renewal option.
    /// </summary>
    /// <param name="lockKey">Logical name of the lock (e.g. user.123").</param>
    /// <param name="ttl">ow long the lock should live before automatically expring.</param>
    /// <param name="waitTime">Optoinal. Wait time to acquire the lock.</param>
    /// <param name="enableAutoRenewal">Enable or disable auto-renewal of the lock.</param>
    /// <returns>An <see cref="IDistributedLock"/> that you must dispose.</returns>
    Task<IDistributedLock> TryAcquireAsync(string lockKey, TimeSpan ttl, TimeSpan? waitTime = null, bool enableAutoRenewal = true);
}
