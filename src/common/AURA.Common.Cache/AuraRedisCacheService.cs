using System.Text.Json;
using StackExchange.Redis;

namespace AURA.Common.Cache;

public class AuraRedisCacheService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _db;

    public AuraRedisCacheService(IConnectionMultiplexer redis)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _db = _redis.GetDatabase();
    }

    public async Task<bool> SetAsync<T>(string key, T value, TimeSpan? ttl = null)
    {
        var json = JsonSerializer.Serialize(value);
        return await _db.StringSetAsync(key, json, ttl);
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        var value = await _db.StringGetAsync(key);
        if (!value.HasValue) return default;
        return JsonSerializer.Deserialize<T>(value.ToString());
    }

    public async Task<bool> DeleteAsync(string key)
    {
        return await _db.KeyDeleteAsync(key);
    }

    public async Task<bool> AcquireLockAsync(string lockKey, TimeSpan ttl)
    {
        var token = Guid.NewGuid().ToString("N");
        return await _db.StringSetAsync(lockKey, token, ttl, When.NotExists);
    }

    public async Task<bool> ReleaseLockAsync(string lockKey)
    {
        return await _db.KeyDeleteAsync(lockKey);
    }

    public async Task<TimeSpan> PingAsync()
    {
        return await _db.PingAsync();
    }
}
