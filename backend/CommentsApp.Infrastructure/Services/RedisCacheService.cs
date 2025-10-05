using System.Text.Json;
using StackExchange.Redis;
using CommentsApp.Core.Interfaces;

namespace CommentsApp.Infrastructure.Services;

public class RedisCacheService : ICacheService
{
    private readonly IDatabase _db;
    public RedisCacheService(IConnectionMultiplexer redis) { _db = redis.GetDatabase(); }
    public async Task<T?> GetAsync<T>(string key) { var v = await _db.StringGetAsync(key); return v.HasValue ? JsonSerializer.Deserialize<T>(v default; }
    public async Task SetAsync<T>(string key, T value, TimeSpan? exp = null) => await _db.StringSetAsync(key, JsonSerializer.Serialize(value), exp);
    public async Task RemoveAsync(string key) => await _db.KeyDeleteAsync(key);
}
