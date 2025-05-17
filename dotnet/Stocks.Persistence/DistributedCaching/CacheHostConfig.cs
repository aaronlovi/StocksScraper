using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Stocks.Persistence.DistributedCaching;

public static class CacheHostConfig {
    /// <summary>
    /// Configures a distributed cache for the application.
    /// </summary>
    /// <remarks>
    /// - Reads cache settings from the "AppSettings:CcheSettings" configuration section.<br />
    /// - If Redis is enabled in the configuratoin, sets up a Redis-based distributed cache.<br />
    /// - Otherwise, sets up an in-memory distributed cache for local use.<br />
    /// - This method abstracts the cche configuraiton to support multiple environments (e.g., local development vs. production).
    /// </remarks>
    public static IServiceCollection Configure(this IServiceCollection services, IConfiguration cfg) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(cfg);

        IConfigurationSection section = cfg.GetSection("AppSettings:CacheSettings");
        var cacheSettings = CacheOptions.FromConfigSection(section);
        string redisConfig = cacheSettings.RedisCacheOptions.Configuration ?? string.Empty;

        _ = cacheSettings.UseRedis
            ? services.
                AddStackExchangeRedisCache(options => {
                    options.Configuration = redisConfig;
                    options.InstanceName = cacheSettings.RedisCacheOptions.InstanceName;
                }).
                AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConfig)).
                AddSingleton<IDistributedLockService, RedisDistributedLockService>()
            : services.
                AddDistributedMemoryCache().
                AddSingleton<IDistributedLockService, InMemoryDistributedLockService>();

        return services.
            AddSingleton<ICacheService, CacheService>().
            AddSingleton<CacheExecutor>();
    }
}
