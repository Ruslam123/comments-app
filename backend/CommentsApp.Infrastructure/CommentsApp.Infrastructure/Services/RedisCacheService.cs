using System.Text.Json;
using StackExchange.Redis;
using CommentsApp.Core.Interfaces;

namespace CommentsApp.Infrastructure.Services;

public class RedisCacheService : ICacheService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _db;
    
    public RedisCacheService(IConnectionMultiplexer redis)
    {
        _redis = redis;
        _db = _redis.GetDatabase();
    }
    
    public async Task<T?> GetAsync<T>(string key)
    {
        var value = await _db.StringGetAsync(key);
        if (!value.HasValue) return default;
        
        return JsonSerializer.Deserialize<T>(value!);
    }
    
    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null)
    {
        var serialized = JsonSerializer.Serialize(value);
        await _db.StringSetAsync(key, serialized, expiration);
    }
    
    public async Task RemoveAsync(string key)
    {
        await _db.KeyDeleteAsync(key);
    }
}



