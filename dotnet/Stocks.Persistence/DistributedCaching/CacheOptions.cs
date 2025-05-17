using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;

namespace Stocks.Persistence.DistributedCaching;
internal class CacheOptions {
    public CacheOptions(bool useRedis = false, RedisCacheOptions? redisCacheOptions = null) {
        UseRedis = useRedis;
        RedisCacheOptions = redisCacheOptions ?? new();
    }

    public bool UseRedis { get; set; }
    public RedisCacheOptions RedisCacheOptions { get; init; }

    public static CacheOptions FromConfigSection(IConfigurationSection section) {
        bool useRedis = section.GetValue<bool>("UseRedis");

        IConfigurationSection redisSpecificOptionsSection = section.GetSection("RedisSpecificOptions");
        var redisCacheOptions = new RedisCacheOptions();
        redisSpecificOptionsSection.Bind(redisCacheOptions);

        return new CacheOptions(useRedis, redisCacheOptions);
    }
}
