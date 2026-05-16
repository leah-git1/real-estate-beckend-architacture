using StackExchange.Redis;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace Services;

public class CacheService : ICacheService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<CacheService> _logger;

    public CacheService(IConnectionMultiplexer redis, ILogger<CacheService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    /// <summary>
    /// Gets a value from cache by key. Deserializes JSON if found.
    /// </summary>
    public async Task<T?> GetAsync<T>(string key)
    {
        try
        {
            var db = _redis.GetDatabase();
            var value = await db.StringGetAsync(key);

            if (!value.IsNull)
            {
                _logger.LogInformation($"Cache HIT for key: {key}");
                return JsonSerializer.Deserialize<T>(value.ToString());
            }

            _logger.LogInformation($"Cache MISS for key: {key}");
            return default;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error retrieving cache key: {key}");
            return default;
        }
    }

    /// <summary>
    /// Sets a value in cache with optional TTL. Serializes to JSON.
    /// </summary>
    public async Task SetAsync<T>(string key, T value, TimeSpan? ttl = null)
    {
        try
        {
            var db = _redis.GetDatabase();
            var serialized = JsonSerializer.Serialize(value);
            await db.StringSetAsync(key, serialized, ttl);
            _logger.LogInformation($"Cache SET for key: {key} with TTL: {ttl?.TotalSeconds ?? 0}s");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error setting cache key: {key}");
        }
    }

    /// <summary>
    /// Removes a value from cache by key.
    /// </summary>
    public async Task RemoveAsync(string key)
    {
        try
        {
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync(key);
            _logger.LogInformation($"Cache REMOVED for key: {key}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error removing cache key: {key}");
        }
    }

    /// <summary>
    /// Removes multiple values from cache by key pattern (e.g., "product:*").
    /// Uses SCAN to find keys matching the pattern.
    /// </summary>
    public async Task RemoveByPatternAsync(string pattern)
    {
        try
        {
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            var keysList = new List<RedisKey>();
            
            await foreach (var key in server.KeysAsync(pattern: pattern))
            {
                keysList.Add(key);
            }

            if (keysList.Count > 0)
            {
                var db = _redis.GetDatabase();
                await db.KeyDeleteAsync(keysList.ToArray());
                _logger.LogInformation($"Cache REMOVED {keysList.Count} keys matching pattern: {pattern}");
            }
            else
            {
                _logger.LogInformation($"No cache keys found matching pattern: {pattern}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error removing cache pattern: {pattern}");
        }
    }

    /// <summary>
    /// Checks if a key exists in cache.
    /// </summary>
    public async Task<bool> ExistsAsync(string key)
    {
        try
        {
            var db = _redis.GetDatabase();
            return await db.KeyExistsAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error checking cache key existence: {key}");
            return false;
        }
    }
}
