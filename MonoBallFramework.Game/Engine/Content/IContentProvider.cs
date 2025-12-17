namespace MonoBallFramework.Game.Engine.Content;

/// <summary>
///     Provides content path resolution with mod support and caching.
///     Resolves content paths by checking mods (by priority) then the base game.
/// </summary>
public interface IContentProvider
{
    /// <summary>
    ///     Resolves the full path to a content file, checking mods by priority then the base game.
    /// </summary>
    /// <param name="contentType">The type of content (e.g., "Definitions", "Graphics", "Audio").</param>
    /// <param name="relativePath">The relative path within the content type folder.</param>
    /// <returns>The resolved absolute path, or null if the content doesn't exist.</returns>
    string? ResolveContentPath(string contentType, string relativePath);

    /// <summary>
    ///     Gets all content paths matching the specified pattern across all sources (mods and base game).
    /// </summary>
    /// <param name="contentType">The type of content to search.</param>
    /// <param name="pattern">The search pattern (e.g., "*.json"). Default is "*.json".</param>
    /// <returns>An enumerable of all matching absolute paths.</returns>
    IEnumerable<string> GetAllContentPaths(string contentType, string pattern = "*.json");

    /// <summary>
    ///     Checks if content exists in any source (mods or base game).
    /// </summary>
    /// <param name="contentType">The type of content to check.</param>
    /// <param name="relativePath">The relative path within the content type folder.</param>
    /// <returns>True if the content exists, false otherwise.</returns>
    bool ContentExists(string contentType, string relativePath);

    /// <summary>
    ///     Gets the source (mod ID or "base") that provides the specified content.
    /// </summary>
    /// <param name="contentType">The type of content.</param>
    /// <param name="relativePath">The relative path within the content type folder.</param>
    /// <returns>The mod ID that provides the content, "base" for base game content, or null if not found.</returns>
    string? GetContentSource(string contentType, string relativePath);

    /// <summary>
    ///     Invalidates the content path cache for the specified content type or all types.
    /// </summary>
    /// <param name="contentType">The specific content type to invalidate, or null to invalidate all cached entries.</param>
    void InvalidateCache(string? contentType = null);

    /// <summary>
    ///     Gets cache statistics for monitoring and optimization.
    /// </summary>
    /// <returns>A <see cref="ContentProviderStats" /> object containing cache performance metrics.</returns>
    ContentProviderStats GetStats();

    /// <summary>
    ///     Gets the base directory path for a content type.
    /// </summary>
    /// <param name="contentType">The type of content (e.g., "Definitions", "Graphics", "Root").</param>
    /// <returns>The absolute path to the content type directory, or null if the content type is not configured.</returns>
    string? GetContentDirectory(string contentType);
}

/// <summary>
///     Contains statistics about content provider cache performance.
/// </summary>
public record ContentProviderStats
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ContentProviderStats" /> record.
    /// </summary>
    public ContentProviderStats()
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ContentProviderStats" /> record with specified values.
    /// </summary>
    /// <param name="cacheHits">The number of cache hits.</param>
    /// <param name="cacheMisses">The number of cache misses.</param>
    /// <param name="totalResolutions">The total number of resolutions.</param>
    /// <param name="cachedEntries">The number of cached entries.</param>
    public ContentProviderStats(long cacheHits, long cacheMisses, long totalResolutions, int cachedEntries)
    {
        CacheHits = cacheHits;
        CacheMisses = cacheMisses;
        TotalResolutions = totalResolutions;
        CachedEntries = cachedEntries;
    }

    /// <summary>
    ///     The total number of cache hits (successful lookups in cache).
    /// </summary>
    public long CacheHits { get; init; }

    /// <summary>
    ///     The total number of cache misses (lookups requiring file system access).
    /// </summary>
    public long CacheMisses { get; init; }

    /// <summary>
    ///     The total number of content path resolutions performed.
    /// </summary>
    public long TotalResolutions { get; init; }

    /// <summary>
    ///     The current number of entries stored in the cache.
    /// </summary>
    public int CachedEntries { get; init; }

    /// <summary>
    ///     The cache hit rate as a percentage (0.0 to 1.0).
    /// </summary>
    public double HitRate => TotalResolutions > 0
        ? (double)CacheHits / TotalResolutions
        : 0.0;
}
