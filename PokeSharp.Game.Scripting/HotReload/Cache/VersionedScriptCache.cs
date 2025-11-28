using System.Collections.Concurrent;

namespace PokeSharp.Game.Scripting.HotReload.Cache;

/// <summary>
///     Thread-safe versioned cache for hot-reloadable scripts with rollback support.
///     Maintains version history, lazy instantiation, and automatic rollback on compilation failure.
///     Target: Enable instant rollback with no game disruption.
///     OPTIMIZATION: Bounded version history (MaxHistoryDepth=3) prevents memory leaks with LRU pruning.
/// </summary>
public class VersionedScriptCache
{
    /// <summary>
    ///     Maximum depth of version history chain to maintain for rollback.
    ///     Older versions are pruned using LRU strategy to prevent unbounded memory growth.
    ///     Default: 3 (current + 2 previous versions)
    /// </summary>
    public const int MaxHistoryDepth = 3;

    private readonly ConcurrentDictionary<string, ScriptCacheEntry> _cache = new();
    private readonly object _versionLock = new();
    private int _currentVersion;

    /// <summary>
    ///     Current global version number. Incremented on each successful compilation.
    /// </summary>
    public int CurrentVersion
    {
        get
        {
            lock (_versionLock)
            {
                return _currentVersion;
            }
        }
    }

    /// <summary>
    ///     Total number of cached script types.
    /// </summary>
    public int CachedScriptCount => _cache.Count;

    /// <summary>
    ///     Update script type to new version. Instance lazily created on first GetOrCreateInstance().
    ///     Maintains link to previous version for rollback support.
    /// </summary>
    /// <param name="typeId">Unique identifier for the script type (e.g., "Pikachu")</param>
    /// <param name="newType">Compiled Type for the new version</param>
    /// <returns>New version number</returns>
    public int UpdateVersion(string typeId, Type newType)
    {
        if (string.IsNullOrEmpty(typeId))
        {
            throw new ArgumentNullException(nameof(typeId));
        }

        if (newType == null)
        {
            throw new ArgumentNullException(nameof(newType));
        }

        lock (_versionLock)
        {
            _currentVersion++;
            int newVersion = _currentVersion;

            _cache.AddOrUpdate(
                typeId,
                // Add new entry
                _ => new ScriptCacheEntry(newVersion, newType),
                // Update existing entry
                (_, oldEntry) =>
                {
                    var newEntry = new ScriptCacheEntry(newVersion, newType)
                    {
                        PreviousVersion = oldEntry, // Link to previous for rollback
                    };

                    // OPTIMIZATION: Prune version history to MaxHistoryDepth using LRU strategy
                    PruneVersionHistory(newEntry, MaxHistoryDepth);

                    return newEntry;
                }
            );

            return newVersion;
        }
    }

    /// <summary>
    ///     Update script type with explicit version number (for restoration from backup).
    /// </summary>
    /// <param name="typeId">Unique identifier for the script type</param>
    /// <param name="type">Compiled Type</param>
    /// <param name="version">Explicit version number to use</param>
    public void UpdateVersion(string typeId, Type type, int version)
    {
        if (string.IsNullOrEmpty(typeId))
        {
            throw new ArgumentNullException(nameof(typeId));
        }

        if (type == null)
        {
            throw new ArgumentNullException(nameof(type));
        }

        _cache.AddOrUpdate(
            typeId,
            _ => new ScriptCacheEntry(version, type),
            (_, oldEntry) =>
                new ScriptCacheEntry(version, type)
                {
                    PreviousVersion = oldEntry.PreviousVersion, // Preserve rollback chain
                }
        );
    }

    /// <summary>
    ///     Get current script instance, creating if needed (lazy instantiation).
    ///     Thread-safe singleton pattern.
    /// </summary>
    /// <param name="typeId">Unique identifier for the script type</param>
    /// <returns>Script instance</returns>
    /// <exception cref="KeyNotFoundException">If typeId not found in cache</exception>
    public object GetOrCreateInstance(string typeId)
    {
        if (string.IsNullOrEmpty(typeId))
        {
            throw new ArgumentNullException(nameof(typeId));
        }

        if (!_cache.TryGetValue(typeId, out ScriptCacheEntry? entry))
        {
            throw new KeyNotFoundException($"Script type '{typeId}' not found in cache");
        }

        return entry.GetOrCreateInstance();
    }

    /// <summary>
    ///     Get current version and instance for a script type (without creating if not exists).
    /// </summary>
    /// <param name="typeId">Unique identifier for the script type</param>
    /// <returns>Tuple of (version, instance) or (-1, null) if not found</returns>
    public (int version, object? instance) GetInstance(string typeId)
    {
        if (string.IsNullOrEmpty(typeId))
        {
            throw new ArgumentNullException(nameof(typeId));
        }

        if (!_cache.TryGetValue(typeId, out ScriptCacheEntry? entry))
        {
            return (-1, null);
        }

        return (entry.Version, entry.Instance);
    }

    /// <summary>
    ///     Get current version number for a specific script type.
    /// </summary>
    /// <param name="typeId">Unique identifier for the script type</param>
    /// <returns>Version number or -1 if not found</returns>
    public int GetVersion(string typeId)
    {
        if (string.IsNullOrEmpty(typeId))
        {
            throw new ArgumentNullException(nameof(typeId));
        }

        if (!_cache.TryGetValue(typeId, out ScriptCacheEntry? entry))
        {
            return -1;
        }

        return entry.Version;
    }

    /// <summary>
    ///     Get current Type for a script.
    /// </summary>
    /// <param name="typeId">Unique identifier for the script type</param>
    /// <returns>Current Type or null if not found</returns>
    public Type? GetScriptType(string typeId)
    {
        if (string.IsNullOrEmpty(typeId))
        {
            throw new ArgumentNullException(nameof(typeId));
        }

        if (!_cache.TryGetValue(typeId, out ScriptCacheEntry? entry))
        {
            return null;
        }

        return entry.ScriptType;
    }

    /// <summary>
    ///     Rollback to previous version (if available).
    ///     Returns true if rollback was successful.
    /// </summary>
    /// <param name="typeId">Unique identifier for the script type</param>
    /// <returns>True if rollback succeeded, false if no previous version available</returns>
    public bool Rollback(string typeId)
    {
        if (string.IsNullOrEmpty(typeId))
        {
            throw new ArgumentNullException(nameof(typeId));
        }

        if (!_cache.TryGetValue(typeId, out ScriptCacheEntry? currentEntry))
        {
            return false;
        }

        if (currentEntry.PreviousVersion == null)
        {
            return false;
        }

        // Restore previous version
        _cache.TryUpdate(typeId, currentEntry.PreviousVersion, currentEntry);

        return true;
    }

    /// <summary>
    ///     Check if a script type exists in the cache.
    /// </summary>
    /// <param name="typeId">Unique identifier for the script type</param>
    /// <returns>True if exists, false otherwise</returns>
    public bool Contains(string typeId)
    {
        if (string.IsNullOrEmpty(typeId))
        {
            return false;
        }

        return _cache.ContainsKey(typeId);
    }

    /// <summary>
    ///     Clear instance for a script (force re-creation on next GetOrCreateInstance).
    ///     Useful for testing or forced refresh.
    /// </summary>
    /// <param name="typeId">Unique identifier for the script type</param>
    /// <returns>True if instance was cleared, false if not found</returns>
    public bool ClearInstance(string typeId)
    {
        if (string.IsNullOrEmpty(typeId))
        {
            throw new ArgumentNullException(nameof(typeId));
        }

        if (!_cache.TryGetValue(typeId, out ScriptCacheEntry? entry))
        {
            return false;
        }

        entry.ClearInstance();
        return true;
    }

    /// <summary>
    ///     Remove a script type from the cache entirely.
    /// </summary>
    /// <param name="typeId">Unique identifier for the script type</param>
    /// <returns>True if removed, false if not found</returns>
    public bool Remove(string typeId)
    {
        if (string.IsNullOrEmpty(typeId))
        {
            throw new ArgumentNullException(nameof(typeId));
        }

        return _cache.TryRemove(typeId, out _);
    }

    /// <summary>
    ///     Clear all cached scripts.
    /// </summary>
    public void Clear()
    {
        _cache.Clear();
        lock (_versionLock)
        {
            _currentVersion = 0;
        }
    }

    /// <summary>
    ///     Get all cached script type IDs.
    /// </summary>
    public IEnumerable<string> GetAllTypeIds()
    {
        return _cache.Keys.ToList();
    }

    /// <summary>
    ///     Get diagnostic information for all cached scripts.
    /// </summary>
    public IEnumerable<CacheEntryInfo> GetDiagnostics()
    {
        return _cache
            .Select(kvp => new CacheEntryInfo
            {
                TypeId = kvp.Key,
                Version = kvp.Value.Version,
                TypeName = kvp.Value.ScriptType.Name,
                IsInstantiated = kvp.Value.IsInstantiated,
                LastUpdated = kvp.Value.LastUpdated,
                HasPreviousVersion = kvp.Value.PreviousVersion != null,
                PreviousVersionNumber = kvp.Value.PreviousVersion?.Version,
            })
            .ToList();
    }

    /// <summary>
    ///     Get version history depth for a script type.
    /// </summary>
    /// <param name="typeId">Unique identifier for the script type</param>
    /// <returns>Number of previous versions available for rollback</returns>
    public int GetVersionHistoryDepth(string typeId)
    {
        if (string.IsNullOrEmpty(typeId))
        {
            throw new ArgumentNullException(nameof(typeId));
        }

        if (!_cache.TryGetValue(typeId, out ScriptCacheEntry? entry))
        {
            return 0;
        }

        int depth = 0;
        ScriptCacheEntry? current = entry.PreviousVersion;
        while (current != null)
        {
            depth++;
            current = current.PreviousVersion;
        }

        return depth;
    }

    /// <summary>
    ///     Prune version history chain to maintain maximum depth using LRU strategy.
    ///     This prevents unbounded memory growth from long version chains.
    ///     PERFORMANCE: O(maxDepth) traversal, typically 3-5 iterations.
    /// </summary>
    /// <param name="entry">Entry to prune starting from</param>
    /// <param name="maxDepth">Maximum depth to maintain (excluding current version)</param>
    private static void PruneVersionHistory(ScriptCacheEntry entry, int maxDepth)
    {
        if (maxDepth <= 0)
        {
            return;
        }

        ScriptCacheEntry? current = entry;
        int depth = 0;

        // Traverse to the depth limit
        while (current.PreviousVersion != null && depth < maxDepth - 1)
        {
            current = current.PreviousVersion;
            depth++;
        }

        // Sever the chain at max depth to allow GC of older versions
        if (current.PreviousVersion != null)
        {
            current.PreviousVersion = null;
        }
    }

    /// <summary>
    ///     Get statistics about version history memory usage.
    ///     Useful for monitoring and performance analysis.
    /// </summary>
    /// <returns>Total version entries across all script types</returns>
    public int GetTotalVersionEntries()
    {
        int total = 0;
        foreach (KeyValuePair<string, ScriptCacheEntry> kvp in _cache)
        {
            total++; // Current version
            ScriptCacheEntry? current = kvp.Value.PreviousVersion;
            while (current != null)
            {
                total++;
                current = current.PreviousVersion;
            }
        }

        return total;
    }
}

/// <summary>
///     Diagnostic information for a cache entry.
/// </summary>
public class CacheEntryInfo
{
    public string TypeId { get; init; } = string.Empty;
    public int Version { get; init; }
    public string TypeName { get; init; } = string.Empty;
    public bool IsInstantiated { get; init; }
    public DateTime LastUpdated { get; init; }
    public bool HasPreviousVersion { get; init; }
    public int? PreviousVersionNumber { get; init; }
}
