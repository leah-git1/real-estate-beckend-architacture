namespace Services;

public interface ICacheService
{
    /// <summary>
    /// Gets a value from cache by key.
    /// </summary>
    Task<T?> GetAsync<T>(string key);

    /// <summary>
    /// Sets a value in cache with optional TTL (time-to-live).
    /// </summary>
    Task SetAsync<T>(string key, T value, TimeSpan? ttl = null);

    /// <summary>
    /// Removes a value from cache by key.
    /// </summary>
    Task RemoveAsync(string key);

    /// <summary>
    /// Removes multiple values from cache by key pattern (e.g., "product:*").
    /// </summary>
    Task RemoveByPatternAsync(string pattern);

    /// <summary>
    /// Checks if a key exists in cache.
    /// </summary>
    Task<bool> ExistsAsync(string key);
}
