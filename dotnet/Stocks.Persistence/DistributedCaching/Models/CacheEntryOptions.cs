using System;
using Microsoft.Extensions.Caching.Distributed;

namespace Stocks.Persistence.DistributedCaching.Models;

/// <summary>
/// Represents cache entry expiratoin options for configuration binding.
/// Inteneded for use as a POCO to bind cache expiration settings (in milliseconds) from configuration,
/// which can then be mappeed to <see cref="Microsoft.Extensions.Caching.Distributed.DistributedCacheEntryOptions"/>.
/// </summary>
/// <param name="SlidingExpiryMsec">
/// The sliding expiration time, in milliseconds. If null or zero, sliding expiration is not set.
/// </param>
/// <param name="AbsoluteExpiryMsec">
/// The absolute expiration time, in milliseconds. If null or zero, absolute expiration is not set.
/// </param>
internal record CacheEntryOptions(uint? SlidingExpiryMsec, uint? AbsoluteExpiryMsec) {
    public static readonly CacheEntryOptions Default = new(120_000, null); // 2 minutes, null

    /// <summary>
    /// Converts this <see cref="CacheEntryOptions" to a <see cref="DistributedCacheEntryOptions"/> instance.
    /// </summary>
    internal DistributedCacheEntryOptions ToDistributedCacheEntryOptions() {
        var options = new DistributedCacheEntryOptions();

        if (SlidingExpiryMsec.HasValue && SlidingExpiryMsec.Value > 0)
            options.SlidingExpiration = TimeSpan.FromMilliseconds(SlidingExpiryMsec.Value);

        if (AbsoluteExpiryMsec.HasValue && AbsoluteExpiryMsec.Value > 0)
            options.AbsoluteExpirationRelativeToNow = TimeSpan.FromMilliseconds(AbsoluteExpiryMsec.Value);

        return options;
    }
}
