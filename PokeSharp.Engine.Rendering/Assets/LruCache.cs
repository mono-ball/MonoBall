using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace PokeSharp.Engine.Rendering.Assets;

/// <summary>
///     Least Recently Used (LRU) cache for textures with memory budget enforcement.
///     Automatically evicts least recently used items when memory limit is exceeded.
/// </summary>
/// <typeparam name="TKey">Cache key type</typeparam>
/// <typeparam name="TValue">Cache value type (must implement IDisposable for cleanup)</typeparam>
public class LruCache<TKey, TValue>
    where TKey : notnull
    where TValue : IDisposable
{
    private readonly ConcurrentDictionary<TKey, CacheEntry> _cache = new();
    private readonly object _lock = new();
    private readonly ILogger? _logger;
    private readonly LinkedList<TKey> _lruList = new();
    private readonly long _maxSizeBytes;
    private readonly Func<TValue, long> _sizeCalculator;
    private long _currentSizeBytes;

    public LruCache(
        long maxSizeBytes,
        Func<TValue, long>? sizeCalculator = null,
        ILogger? logger = null
    )
    {
        _maxSizeBytes = maxSizeBytes;
        _sizeCalculator = sizeCalculator ?? (_ => 1024); // Default 1KB estimate
        _logger = logger;
    }

    /// <summary>
    ///     Gets the current memory usage in bytes
    /// </summary>
    public long CurrentSize => Interlocked.Read(ref _currentSizeBytes);

    /// <summary>
    ///     Gets the number of items in the cache
    /// </summary>
    public int Count => _cache.Count;

    /// <summary>
    ///     Gets all keys currently in the cache (for debugging).
    /// </summary>
    public IEnumerable<TKey> Keys
    {
        get
        {
            lock (_lock)
            {
                return _cache.Keys.ToList();
            }
        }
    }

    /// <summary>
    ///     Adds or updates an item in the cache, evicting LRU items if needed
    /// </summary>
    public void AddOrUpdate(TKey key, TValue value)
    {
        lock (_lock)
        {
            long size = _sizeCalculator(value);

            // If item already exists, remove it first
            if (_cache.TryRemove(key, out CacheEntry oldEntry))
            {
                oldEntry.Node?.List?.Remove(oldEntry.Node);
                Interlocked.Add(ref _currentSizeBytes, -oldEntry.Size);
                oldEntry.Value.Dispose();

                _logger?.LogDebug(
                    "Replaced existing cache entry: {Key} (freed {Size:N0} bytes)",
                    key,
                    oldEntry.Size
                );
            }

            // Evict LRU items if adding this would exceed budget
            while (_currentSizeBytes + size > _maxSizeBytes && _lruList.Count > 0)
            {
                EvictLru();
            }

            // Add new entry
            LinkedListNode<TKey> node = _lruList.AddFirst(key);
            _cache[key] = new CacheEntry(value, size, node);
            Interlocked.Add(ref _currentSizeBytes, size);

            _logger?.LogDebug(
                "Added to cache: {Key} ({Size:N0} bytes, total: {Total:N0}/{Max:N0})",
                key,
                size,
                _currentSizeBytes,
                _maxSizeBytes
            );
        }
    }

    /// <summary>
    ///     Gets an item from the cache, updating its LRU position
    /// </summary>
    public bool TryGetValue(TKey key, out TValue? value)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(key, out CacheEntry entry))
            {
                // Move to front (most recently used)
                if (entry.Node != null && entry.Node.List != null)
                {
                    _lruList.Remove(entry.Node);
                    entry.Node = _lruList.AddFirst(key);
                    _cache[key] = entry; // Update node reference
                }

                value = entry.Value;
                return true;
            }

            value = default;
            return false;
        }
    }

    /// <summary>
    ///     Removes a specific item from the cache
    /// </summary>
    public bool Remove(TKey key)
    {
        lock (_lock)
        {
            if (_cache.TryRemove(key, out CacheEntry entry))
            {
                entry.Node?.List?.Remove(entry.Node);
                Interlocked.Add(ref _currentSizeBytes, -entry.Size);
                entry.Value.Dispose();

                _logger?.LogDebug(
                    "Removed from cache: {Key} (freed {Size:N0} bytes)",
                    key,
                    entry.Size
                );
                return true;
            }

            return false;
        }
    }

    /// <summary>
    ///     Clears all items from the cache
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            foreach (CacheEntry entry in _cache.Values)
            {
                entry.Value.Dispose();
            }

            _cache.Clear();
            _lruList.Clear();
            Interlocked.Exchange(ref _currentSizeBytes, 0);

            _logger?.LogInformation("Cache cleared");
        }
    }

    /// <summary>
    ///     Evicts the least recently used item
    /// </summary>
    private void EvictLru()
    {
        // Must be called within lock
        if (_lruList.Last == null)
        {
            return;
        }

        TKey lruKey = _lruList.Last.Value;
        if (_cache.TryRemove(lruKey, out CacheEntry entry))
        {
            _lruList.RemoveLast();
            Interlocked.Add(ref _currentSizeBytes, -entry.Size);
            entry.Value.Dispose();

            _logger?.LogDebug(
                "Evicted LRU item: {Key} (freed {Size:N0} bytes)",
                lruKey,
                entry.Size
            );
        }
    }

    private record struct CacheEntry(TValue Value, long Size, LinkedListNode<TKey>? Node);
}
