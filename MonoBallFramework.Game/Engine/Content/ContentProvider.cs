using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MonoBallFramework.Game.Engine.Core.Modding;

namespace MonoBallFramework.Game.Engine.Content;

/// <summary>
/// Provides content path resolution with mod support and LRU caching.
/// Resolves content paths by checking mods (by priority descending) then the base game.
/// Thread-safe implementation with comprehensive logging and security validation.
/// </summary>
public sealed class ContentProvider : IContentProvider
{
    private readonly IModLoader _modLoader;
    private readonly ILogger<ContentProvider> _logger;
    private readonly ContentProviderOptions _options;
    private readonly LruCache<string, string?> _cache;

    // Cache statistics
    private long _cacheHits;
    private long _cacheMisses;
    private long _totalResolutions;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentProvider"/> class.
    /// </summary>
    /// <param name="modLoader">The mod loader for accessing loaded mods.</param>
    /// <param name="logger">The logger for diagnostic information.</param>
    /// <param name="options">The configuration options for the content provider.</param>
    /// <exception cref="ArgumentNullException">Thrown when any required dependency is null.</exception>
    public ContentProvider(
        IModLoader modLoader,
        ILogger<ContentProvider> logger,
        IOptions<ContentProviderOptions> options)
    {
        _modLoader = modLoader ?? throw new ArgumentNullException(nameof(modLoader));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

        // Validate options
        _options.Validate();

        // Initialize LRU cache
        _cache = new LruCache<string, string?>(_options.MaxCacheSize);

        _logger.LogInformation(
            "ContentProvider initialized with cache size: {CacheSize}, base root: {BaseRoot}",
            _options.MaxCacheSize,
            _options.BaseGameRoot);
    }

    /// <inheritdoc />
    public string? ResolveContentPath(string contentType, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            throw new ArgumentException("Content type cannot be null or whitespace.", nameof(contentType));
        }

        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new ArgumentException("Relative path cannot be null or whitespace.", nameof(relativePath));
        }

        // Security: Check for path traversal attempts
        if (!IsPathSafe(relativePath))
        {
            string errorMessage = $"Path traversal detected in relative path: {relativePath}";
            _logger.LogError(errorMessage);

            if (_options.ThrowOnPathTraversal)
            {
                throw new SecurityException(errorMessage);
            }

            return null;
        }

        // Increment total resolutions
        Interlocked.Increment(ref _totalResolutions);

        // Build cache key
        string cacheKey = $"{contentType}:{relativePath}";

        // Check cache first
        if (_cache.TryGet(cacheKey, out string? cachedPath))
        {
            Interlocked.Increment(ref _cacheHits);
            _logger.LogDebug(
                "Cache hit for {ContentType}/{RelativePath} -> {CachedPath}",
                contentType,
                relativePath,
                cachedPath ?? "(not found)");
            return cachedPath;
        }

        // Cache miss - perform resolution
        Interlocked.Increment(ref _cacheMisses);

        if (_options.LogCacheMisses)
        {
            _logger.LogDebug(
                "Cache miss for {ContentType}/{RelativePath}, resolving...",
                contentType,
                relativePath);
        }

        string? resolvedPath = null;

        // Step 1: Check mods by priority (highest to lowest)
        var modsOrderedByPriority = _modLoader.LoadedMods.Values
            .OrderByDescending(m => m.Priority)
            .ToList();

        foreach (var mod in modsOrderedByPriority)
        {
            // Check if this mod has the requested content type
            if (!mod.ContentFolders.TryGetValue(contentType, out string? contentFolder))
            {
                continue;
            }

            // Build the full path
            string candidatePath = Path.Combine(mod.DirectoryPath, contentFolder, relativePath);

            // Check if file exists
            if (File.Exists(candidatePath))
            {
                resolvedPath = candidatePath;
                _logger.LogDebug(
                    "Resolved {ContentType}/{RelativePath} from mod '{ModId}' (priority {Priority}) -> {Path}",
                    contentType,
                    relativePath,
                    mod.Id,
                    mod.Priority,
                    resolvedPath);
                break;
            }
        }

        // Step 2: If not found in mods, check base game
        if (resolvedPath == null)
        {
            if (_options.BaseContentFolders.TryGetValue(contentType, out string? baseContentFolder))
            {
                string basePath = Path.Combine(_options.BaseGameRoot, baseContentFolder, relativePath);

                if (File.Exists(basePath))
                {
                    resolvedPath = basePath;
                    _logger.LogDebug(
                        "Resolved {ContentType}/{RelativePath} from base game -> {Path}",
                        contentType,
                        relativePath,
                        resolvedPath);
                }
            }
        }

        // Step 3: Cache the result (even if null for negative caching)
        _cache.Set(cacheKey, resolvedPath);

        if (resolvedPath == null)
        {
            _logger.LogDebug(
                "Content not found: {ContentType}/{RelativePath}",
                contentType,
                relativePath);
        }

        return resolvedPath;
    }

    /// <inheritdoc />
    public IEnumerable<string> GetAllContentPaths(string contentType, string pattern = "*.json")
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            throw new ArgumentException("Content type cannot be null or whitespace.", nameof(contentType));
        }

        if (string.IsNullOrWhiteSpace(pattern))
        {
            throw new ArgumentException("Pattern cannot be null or whitespace.", nameof(pattern));
        }

        // Security: Validate pattern to prevent directory traversal
        ValidateSearchPattern(pattern);

        var foundPaths = new HashSet<string>();
        var seenRelativePaths = new HashSet<string>();

        _logger.LogDebug(
            "Collecting all content paths for {ContentType} with pattern {Pattern}",
            contentType,
            pattern);

        // Step 1: Collect from mods (highest priority first)
        var modsOrderedByPriority = _modLoader.LoadedMods.Values
            .OrderByDescending(m => m.Priority)
            .ToList();

        foreach (var mod in modsOrderedByPriority)
        {
            if (!mod.ContentFolders.TryGetValue(contentType, out string? contentFolder))
            {
                continue;
            }

            string searchPath = Path.Combine(mod.DirectoryPath, contentFolder);

            if (!Directory.Exists(searchPath))
            {
                continue;
            }

            try
            {
                foreach (string filePath in Directory.EnumerateFiles(searchPath, pattern, SearchOption.AllDirectories))
                {
                    // Calculate relative path for duplicate detection
                    string relativePath = Path.GetRelativePath(searchPath, filePath);

                    // Only add if we haven't seen this relative path before
                    if (seenRelativePaths.Add(relativePath))
                    {
                        foundPaths.Add(filePath);

                        _logger.LogTrace(
                            "Found {RelativePath} in mod '{ModId}'",
                            relativePath,
                            mod.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Error enumerating files in mod '{ModId}' at {Path}",
                    mod.Id,
                    searchPath);
            }
        }

        // Step 2: Collect from base game (only add if not already seen)
        if (_options.BaseContentFolders.TryGetValue(contentType, out string? baseContentFolder))
        {
            string baseSearchPath = Path.Combine(_options.BaseGameRoot, baseContentFolder);

            if (Directory.Exists(baseSearchPath))
            {
                try
                {
                    foreach (string filePath in Directory.EnumerateFiles(baseSearchPath, pattern, SearchOption.AllDirectories))
                    {
                        // Calculate relative path for duplicate detection
                        string relativePath = Path.GetRelativePath(baseSearchPath, filePath);

                        // Only add if we haven't seen this relative path before
                        if (seenRelativePaths.Add(relativePath))
                        {
                            foundPaths.Add(filePath);

                            _logger.LogTrace(
                                "Found {RelativePath} in base game",
                                relativePath);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Error enumerating files in base game at {Path}",
                        baseSearchPath);
                }
            }
        }

        _logger.LogDebug(
            "Collected {Count} unique content paths for {ContentType}",
            foundPaths.Count,
            contentType);

        return foundPaths;
    }

    /// <inheritdoc />
    public bool ContentExists(string contentType, string relativePath)
    {
        return ResolveContentPath(contentType, relativePath) != null;
    }

    /// <inheritdoc />
    public string? GetContentSource(string contentType, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            throw new ArgumentException("Content type cannot be null or whitespace.", nameof(contentType));
        }

        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new ArgumentException("Relative path cannot be null or whitespace.", nameof(relativePath));
        }

        // Security check
        if (!IsPathSafe(relativePath))
        {
            string errorMessage = $"Path traversal detected in relative path: {relativePath}";
            _logger.LogError(errorMessage);

            if (_options.ThrowOnPathTraversal)
            {
                throw new SecurityException(errorMessage);
            }

            return null;
        }

        // Check mods by priority (highest to lowest)
        var modsOrderedByPriority = _modLoader.LoadedMods.Values
            .OrderByDescending(m => m.Priority)
            .ToList();

        foreach (var mod in modsOrderedByPriority)
        {
            if (!mod.ContentFolders.TryGetValue(contentType, out string? contentFolder))
            {
                continue;
            }

            string candidatePath = Path.Combine(mod.DirectoryPath, contentFolder, relativePath);

            if (File.Exists(candidatePath))
            {
                _logger.LogDebug(
                    "Content source for {ContentType}/{RelativePath} is mod '{ModId}'",
                    contentType,
                    relativePath,
                    mod.Id);
                return mod.Id;
            }
        }

        // Check base game
        if (_options.BaseContentFolders.TryGetValue(contentType, out string? baseContentFolder))
        {
            string basePath = Path.Combine(_options.BaseGameRoot, baseContentFolder, relativePath);

            if (File.Exists(basePath))
            {
                _logger.LogDebug(
                    "Content source for {ContentType}/{RelativePath} is base game",
                    contentType,
                    relativePath);
                return "base";
            }
        }

        _logger.LogDebug(
            "No content source found for {ContentType}/{RelativePath}",
            contentType,
            relativePath);

        return null;
    }

    /// <inheritdoc />
    public void InvalidateCache(string? contentType = null)
    {
        if (contentType == null)
        {
            _cache.Clear();
            _logger.LogInformation("Cleared entire content path cache");
        }
        else
        {
            _cache.RemoveWhere(key => key.StartsWith($"{contentType}:", StringComparison.Ordinal));
            _logger.LogInformation("Cleared cache entries for content type: {ContentType}", contentType);
        }
    }

    /// <inheritdoc />
    public ContentProviderStats GetStats()
    {
        long hits = Interlocked.Read(ref _cacheHits);
        long misses = Interlocked.Read(ref _cacheMisses);
        long total = Interlocked.Read(ref _totalResolutions);
        int cached = _cache.Count;

        var stats = new ContentProviderStats(hits, misses, total, cached);

        _logger.LogDebug(
            "Content provider stats: Hits={Hits}, Misses={Misses}, Total={Total}, Cached={Cached}, HitRate={HitRate:P2}",
            stats.CacheHits,
            stats.CacheMisses,
            stats.TotalResolutions,
            stats.CachedEntries,
            stats.HitRate);

        return stats;
    }

    /// <inheritdoc />
    public string? GetContentDirectory(string contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            throw new ArgumentException("Content type cannot be null or whitespace.", nameof(contentType));
        }

        // 1) Prefer mod content folders (highest priority first) so moved assets still resolve
        var modsOrderedByPriority = _modLoader.LoadedMods.Values
            .OrderByDescending(m => m.Priority)
            .ToList();

        foreach (var mod in modsOrderedByPriority)
        {
            if (!mod.ContentFolders.TryGetValue(contentType, out string? modFolder))
            {
                continue;
            }

            string modDirectoryPath = string.IsNullOrEmpty(modFolder)
                ? mod.DirectoryPath
                : Path.Combine(mod.DirectoryPath, modFolder);

            if (!Directory.Exists(modDirectoryPath))
            {
                continue;
            }

            _logger.LogDebug(
                "Resolved content directory for '{ContentType}' from mod '{ModId}' (priority {Priority}) -> {Path}",
                contentType,
                mod.Id,
                mod.Priority,
                modDirectoryPath);

            return modDirectoryPath;
        }

        // 2) Fallback to base game content folders
        if (!_options.BaseContentFolders.TryGetValue(contentType, out string? contentFolder))
        {
            _logger.LogDebug("Content type '{ContentType}' is not configured", contentType);
            return null;
        }

        string baseDirectoryPath = string.IsNullOrEmpty(contentFolder)
            ? _options.BaseGameRoot
            : Path.Combine(_options.BaseGameRoot, contentFolder);

        if (!Directory.Exists(baseDirectoryPath))
        {
            _logger.LogDebug(
                "Content directory for '{ContentType}' does not exist: {Path}",
                contentType,
                baseDirectoryPath);
            return null;
        }

        _logger.LogDebug(
            "Resolved content directory for '{ContentType}' from base game -> {Path}",
            contentType,
            baseDirectoryPath);

        return baseDirectoryPath;
    }

    /// <summary>
    /// Validates that a search pattern is safe and doesn't contain path traversal attempts.
    /// Throws <see cref="SecurityException"/> if the pattern is malicious.
    /// </summary>
    /// <param name="pattern">The search pattern to validate.</param>
    /// <exception cref="SecurityException">Thrown when pattern contains path traversal or other dangerous content.</exception>
    /// <remarks>
    /// Per Microsoft documentation, Directory.EnumerateFiles should reject patterns containing ".."
    /// followed by a directory separator. However, we perform explicit validation for defense in depth
    /// and to provide clear error messages.
    /// See: https://learn.microsoft.com/en-us/dotnet/api/system.io.directory.enumeratefiles
    /// </remarks>
    private void ValidateSearchPattern(string pattern)
    {
        // Block path traversal attempts (e.g., "../*.json", "..\\*.json", "foo/../bar")
        if (pattern.Contains("..", StringComparison.Ordinal))
        {
            var errorMessage = $"Search pattern contains path traversal sequence '..': {pattern}";
            _logger.LogWarning("Security: {Message}", errorMessage);
            throw new SecurityException(errorMessage);
        }

        // Block rooted/absolute paths (e.g., "/etc/*.json", "C:\*.json")
        if (Path.IsPathRooted(pattern))
        {
            var errorMessage = $"Search pattern cannot be an absolute path: {pattern}";
            _logger.LogWarning("Security: {Message}", errorMessage);
            throw new SecurityException(errorMessage);
        }

        // Block null byte injection
        if (pattern.Contains('\0'))
        {
            var errorMessage = "Search pattern contains null byte";
            _logger.LogWarning("Security: {Message}", errorMessage);
            throw new SecurityException(errorMessage);
        }

        // Block patterns starting with directory separator (could bypass base path)
        if (pattern.StartsWith(Path.DirectorySeparatorChar) || pattern.StartsWith(Path.AltDirectorySeparatorChar))
        {
            var errorMessage = $"Search pattern cannot start with directory separator: {pattern}";
            _logger.LogWarning("Security: {Message}", errorMessage);
            throw new SecurityException(errorMessage);
        }
    }

    /// <summary>
    /// Validates that a relative path is safe and doesn't contain path traversal attempts.
    /// </summary>
    /// <param name="relativePath">The relative path to validate.</param>
    /// <returns>True if the path is safe, false otherwise.</returns>
    private static bool IsPathSafe(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        // Block path traversal attempts
        if (relativePath.Contains("..", StringComparison.Ordinal))
        {
            return false;
        }

        // Block rooted paths (absolute paths)
        if (Path.IsPathRooted(relativePath))
        {
            return false;
        }

        // Block null character
        if (relativePath.Contains('\0'))
        {
            return false;
        }

        return true;
    }
}

/// <summary>
/// Exception thrown when a security violation is detected in content path resolution.
/// </summary>
public sealed class SecurityException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SecurityException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public SecurityException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SecurityException"/> class with an inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public SecurityException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
