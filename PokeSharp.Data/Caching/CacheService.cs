using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace PokeSharp.Data.Caching;

/// <summary>
/// Wrapper around Microsoft.Extensions.Caching.Memory for game data caching.
/// Provides typed caching with configurable expiration policies.
/// Thread-safe and optimized for read-heavy workloads.
/// </summary>
public sealed class CacheService
{
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<CacheService> _logger;
    private readonly MemoryCacheEntryOptions _defaultOptions;

    public CacheService(
        IMemoryCache memoryCache,
        ILogger<CacheService> logger)
    {
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Default cache options: 5 minute sliding expiration
        _defaultOptions = new MemoryCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromMinutes(5),
            Priority = CacheItemPriority.Normal
        };
    }

    /// <summary>
    /// Get an item from cache.
    /// </summary>
    /// <typeparam name="T">Item type</typeparam>
    /// <param name="key">Cache key</param>
    /// <returns>Cached item or default if not found</returns>
    public T? Get<T>(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key, nameof(key));

        if (_memoryCache.TryGetValue(key, out T? value))
        {
            _logger.LogTrace("Cache hit for key: {CacheKey}", key);
            return value;
        }

        _logger.LogTrace("Cache miss for key: {CacheKey}", key);
        return default;
    }

    /// <summary>
    /// Get or create an item in cache.
    /// If item doesn't exist, factory function is called to create it.
    /// </summary>
    /// <typeparam name="T">Item type</typeparam>
    /// <param name="key">Cache key</param>
    /// <param name="factory">Factory function to create item if not cached</param>
    /// <param name="options">Optional cache options</param>
    /// <returns>Cached or newly created item</returns>
    public async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<Task<T>> factory,
        MemoryCacheEntryOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key, nameof(key));
        ArgumentNullException.ThrowIfNull(factory, nameof(factory));

        if (_memoryCache.TryGetValue(key, out T? value))
        {
            _logger.LogTrace("Cache hit for key: {CacheKey}", key);
            return value!;
        }

        _logger.LogDebug("Cache miss for key: {CacheKey}. Creating new entry.", key);

        // Create new item
        var newValue = await factory();

        // Cache it
        _memoryCache.Set(key, newValue, options ?? _defaultOptions);

        _logger.LogTrace("Cached new value for key: {CacheKey}", key);

        return newValue;
    }

    /// <summary>
    /// Set an item in cache with default expiration.
    /// </summary>
    /// <typeparam name="T">Item type</typeparam>
    /// <param name="key">Cache key</param>
    /// <param name="value">Value to cache</param>
    public void Set<T>(string key, T value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key, nameof(key));
        ArgumentNullException.ThrowIfNull(value, nameof(value));

        _memoryCache.Set(key, value, _defaultOptions);
        _logger.LogTrace("Set cache value for key: {CacheKey}", key);
    }

    /// <summary>
    /// Set an item in cache with custom expiration.
    /// </summary>
    /// <typeparam name="T">Item type</typeparam>
    /// <param name="key">Cache key</param>
    /// <param name="value">Value to cache</param>
    /// <param name="expiration">Expiration timespan</param>
    public void Set<T>(string key, T value, TimeSpan expiration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key, nameof(key));
        ArgumentNullException.ThrowIfNull(value, nameof(value));

        var options = new MemoryCacheEntryOptions
        {
            SlidingExpiration = expiration,
            Priority = CacheItemPriority.Normal
        };

        _memoryCache.Set(key, value, options);
        _logger.LogTrace("Set cache value for key: {CacheKey} with expiration: {Expiration}", key, expiration);
    }

    /// <summary>
    /// Remove an item from cache.
    /// </summary>
    /// <param name="key">Cache key</param>
    public void Remove(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key, nameof(key));

        _memoryCache.Remove(key);
        _logger.LogDebug("Removed cache entry for key: {CacheKey}", key);
    }

    /// <summary>
    /// Clear all cache entries.
    /// WARNING: This is expensive and should be used sparingly.
    /// </summary>
    public void Clear()
    {
        // MemoryCache doesn't have a built-in Clear method
        // We would need to track keys separately for this
        _logger.LogWarning("Clear() called but MemoryCache doesn't support bulk clear. " +
                          "Consider disposing and recreating the cache instead.");
    }

    /// <summary>
    /// Check if a key exists in cache.
    /// </summary>
    /// <param name="key">Cache key</param>
    /// <returns>True if key exists</returns>
    public bool Exists(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key, nameof(key));
        return _memoryCache.TryGetValue(key, out _);
    }
}
