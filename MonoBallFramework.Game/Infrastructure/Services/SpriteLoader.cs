using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Engine.Common.Logging;

namespace MonoBallFramework.Game.Infrastructure.Services;

/// <summary>
///     Service for loading sprites extracted from Pokemon Emerald.
///     Sprites are organized in two main categories:
///     - Players: brendan/, may/ (each with variants like normal, surfing, machbike)
///     - NPCs: generic/, elite_four/, gym_leaders/, team_aqua/, team_magma/, frontier_brains/
///     Implements caching for performance - use ClearCache() to free memory when needed.
/// </summary>
public class SpriteLoader
{
    private readonly ILogger<SpriteLoader> _logger;
    private readonly IAssetPathResolver _pathResolver;
    private readonly List<string> _spritesBasePaths;

    private List<SpriteManifest>? _allSprites;

    // PERFORMANCE: Manifest cache for O(1) lookups
    // MEMORY: Call ClearCache() during map transitions to prevent memory buildup
    private Dictionary<string, SpriteManifest>? _spriteCache;
    private Dictionary<string, string>? _spritePathLookup; // Maps "category/spriteId" -> full directory path

    /// <summary>
    ///     Initializes a new instance of the <see cref="SpriteLoader" /> class.
    /// </summary>
    /// <param name="pathResolver">Path resolver for locating sprite directories.</param>
    /// <param name="logger">Logger instance.</param>
    public SpriteLoader(IAssetPathResolver pathResolver, ILogger<SpriteLoader> logger)
    {
        _pathResolver = pathResolver ?? throw new ArgumentNullException(nameof(pathResolver));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Use path resolver to get the actual paths to sprite directories
        _spritesBasePaths = new List<string>
        {
            _pathResolver.Resolve("Sprites/Players"),
            _pathResolver.Resolve("Sprites/NPCs"),
        };
    }

    /// <summary>
    ///     Load all available sprites by scanning directories for individual manifests.
    ///     Scans both Assets/Sprites/Players and Assets/Sprites/NPCs.
    /// </summary>
    public async Task<List<SpriteManifest>> LoadAllSpritesAsync()
    {
        if (_allSprites != null)
        {
            return _allSprites;
        }

        _allSprites = new List<SpriteManifest>();
        _spritePathLookup = new Dictionary<string, string>();

        foreach (string spritesBasePath in _spritesBasePaths)
        {
            if (!Directory.Exists(spritesBasePath))
            {
                _logger.LogDirectoryNotFound("Sprites", spritesBasePath);
                continue;
            }

            _logger.LogSpriteScanningStarted(spritesBasePath);

            // Find all manifest.json files in subdirectories
            string[] manifestFiles = Directory.GetFiles(
                spritesBasePath,
                "manifest.json",
                SearchOption.AllDirectories
            );

            foreach (string manifestFile in manifestFiles)
            {
                try
                {
                    string json = await File.ReadAllTextAsync(manifestFile);
                    SpriteManifest? manifest = JsonSerializer.Deserialize<SpriteManifest>(
                        json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    );

                    if (manifest != null)
                    {
                        _allSprites.Add(manifest);

                        // Build lookup: "category/spriteId" -> full directory path
                        string? manifestDir = Path.GetDirectoryName(manifestFile);
                        if (!string.IsNullOrEmpty(manifestDir))
                        {
                            string lookupKey = $"{manifest.Category}/{manifest.Name}";
                            _spritePathLookup[lookupKey] = manifestDir;
                            _logger.LogSpriteRegistered(lookupKey, manifestDir);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogSpriteManifestLoadFailed(manifestFile, ex);
                }
            }
        }

        _logger.LogSpritesLoaded(_allSprites.Count);

        // Populate sprite cache for synchronous GetSprite() calls
        // Use capacity hint to avoid reallocations
        _spriteCache = new Dictionary<string, SpriteManifest>(_allSprites.Count);
        foreach (SpriteManifest sprite in _allSprites)
        {
            string cacheKey = $"{sprite.Category}/{sprite.Name}";
            _spriteCache[cacheKey] = sprite;
        }

        return _allSprites;
    }

    /// <summary>
    ///     Load a specific sprite by name (searches across all categories)
    ///     For better performance, use LoadSpriteAsync(category, spriteName) overload.
    /// </summary>
    public async Task<SpriteManifest?> LoadSpriteAsync(string spriteName)
    {
        if (_spriteCache == null)
        {
            _spriteCache = new Dictionary<string, SpriteManifest>();
            List<SpriteManifest> allSprites = await LoadAllSpritesAsync();
            foreach (SpriteManifest sprite in allSprites)
            {
                // FIXED: Use "category/name" as cache key to handle duplicate names
                string cacheKey = $"{sprite.Category}/{sprite.Name}";
                _spriteCache[cacheKey] = sprite;
            }
        }

        // Try to find sprite by name (search all categories)
        SpriteManifest? found = _spriteCache.Values.FirstOrDefault(s => s.Name == spriteName);
        if (found != null)
        {
            return found;
        }

        _logger.LogSpriteNotFound(spriteName);
        return null;
    }

    /// <summary>
    ///     Load a specific sprite by category and name (recommended - faster and more precise)
    /// </summary>
    public async Task<SpriteManifest?> LoadSpriteAsync(string category, string spriteName)
    {
        if (_spriteCache == null)
        {
            _spriteCache = new Dictionary<string, SpriteManifest>();
            List<SpriteManifest> allSprites = await LoadAllSpritesAsync();
            foreach (SpriteManifest sprite in allSprites)
            {
                // Use "category/name" as cache key to handle duplicate names
                string cacheKey = $"{sprite.Category}/{sprite.Name}";
                _spriteCache[cacheKey] = sprite;
            }
        }

        string lookupKey = $"{category}/{spriteName}";
        if (_spriteCache.TryGetValue(lookupKey, out SpriteManifest? manifest))
        {
            return manifest;
        }

        _logger.LogSpriteNotFound(lookupKey);
        return null;
    }

    /// <summary>
    ///     Gets a sprite manifest from the cache synchronously.
    ///     Requires LoadAllSpritesAsync() to be called first during initialization.
    /// </summary>
    /// <param name="category">The sprite category.</param>
    /// <param name="spriteName">The sprite name.</param>
    /// <returns>The sprite manifest if found, null otherwise.</returns>
    public SpriteManifest? GetSprite(string category, string spriteName)
    {
        if (_spriteCache == null)
        {
            _logger.LogWarning(
                "Sprite cache not initialized. Call LoadAllSpritesAsync() during initialization."
            );
            return null;
        }

        string lookupKey = $"{category}/{spriteName}";
        if (_spriteCache.TryGetValue(lookupKey, out SpriteManifest? manifest))
        {
            return manifest;
        }

        _logger.LogSpriteNotFound(lookupKey);
        return null;
    }

    /// <summary>
    ///     Get all sprites in a specific category
    /// </summary>
    public async Task<List<SpriteManifest>> GetSpritesByCategoryAsync(string category)
    {
        List<SpriteManifest> allSprites = await LoadAllSpritesAsync();
        return allSprites.Where(s => s.Category == category).ToList();
    }

    /// <summary>
    ///     Get all available categories
    /// </summary>
    public async Task<List<string>> GetCategoriesAsync()
    {
        List<SpriteManifest> allSprites = await LoadAllSpritesAsync();
        return allSprites.Select(s => s.Category).Distinct().OrderBy(c => c).ToList();
    }

    /// <summary>
    ///     Get the full path to a sprite's directory by category and name.
    ///     NOTE: LoadAllSpritesAsync() must be called before using this method.
    /// </summary>
    public string? GetSpritePath(string category, string spriteName)
    {
        // Ensure sprites are loaded (defensive check)
        if (_spritePathLookup == null)
        {
            throw new InvalidOperationException(
                "Sprite manifests have not been loaded. Call LoadAllSpritesAsync() during initialization."
            );
        }

        string lookupKey = $"{category}/{spriteName}";
        if (_spritePathLookup.TryGetValue(lookupKey, out string? path))
        {
            return path;
        }

        _logger.LogSpritePathNotFound(lookupKey);
        return null;
    }

    /// <summary>
    ///     Get the full path to a sprite's directory (manifest version)
    /// </summary>
    public string? GetSpritePath(SpriteManifest manifest)
    {
        return GetSpritePath(manifest.Category, manifest.Name);
    }

    /// <summary>
    ///     Get the full path to the sprite sheet
    /// </summary>
    public string? GetSpriteSheetPath(string category, string spriteName)
    {
        string? spritePath = GetSpritePath(category, spriteName);
        return spritePath != null ? Path.Combine(spritePath, "spritesheet.png") : null;
    }

    /// <summary>
    ///     Get the full path to the sprite sheet (manifest version)
    /// </summary>
    public string? GetSpriteSheetPath(SpriteManifest manifest)
    {
        return GetSpriteSheetPath(manifest.Category, manifest.Name);
    }

    /// <summary>
    ///     Clears the manifest cache to free memory.
    ///     Call this when transitioning between maps or during cleanup.
    /// </summary>
    public void ClearCache()
    {
        _spriteCache?.Clear();
        _spriteCache = null;

        _allSprites?.Clear();
        _allSprites = null;

        _spritePathLookup?.Clear();
        _spritePathLookup = null;

        _logger.LogSpriteCacheCleared();
    }

    /// <summary>
    ///     Clears specific sprite from cache by ID.
    ///     Useful for selective cleanup of unused sprites.
    /// </summary>
    public void ClearSprite(string category, string spriteName)
    {
        string key = $"{category}/{spriteName}";

        if (_spriteCache?.Remove(key) == true)
        {
            _logger.LogSpriteClearedFromCache(key);
        }

        // Note: We don't remove from _allSprites or _spritePathLookup as those are
        // rebuilt from disk on next LoadAllSpritesAsync() call
    }

    /// <summary>
    ///     Gets current cache statistics for monitoring.
    /// </summary>
    public (int ManifestCount, int PathCount) GetCacheStats()
    {
        return (_spriteCache?.Count ?? 0, _spritePathLookup?.Count ?? 0);
    }
}

/// <summary>
///     Manifest for a sprite extracted from Pokemon Emerald.
///     Contains metadata about frames, animations, and the sprite's category.
/// </summary>
public class SpriteManifest
{
    [JsonPropertyName("Name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("Category")]
    public string Category { get; set; } = "";

    [JsonPropertyName("OriginalPath")]
    public string OriginalPath { get; set; } = "";

    [JsonPropertyName("OutputDirectory")]
    public string OutputDirectory { get; set; } = "";

    [JsonPropertyName("SpriteSheet")]
    public string SpriteSheet { get; set; } = "";

    [JsonPropertyName("FrameWidth")]
    public int FrameWidth { get; set; }

    [JsonPropertyName("FrameHeight")]
    public int FrameHeight { get; set; }

    [JsonPropertyName("FrameCount")]
    public int FrameCount { get; set; }

    [JsonPropertyName("Frames")]
    public List<SpriteFrameInfo> Frames { get; set; } = new();

    [JsonPropertyName("Animations")]
    public List<SpriteAnimationInfo> Animations { get; set; } = new();
}

/// <summary>
///     Information about a single sprite frame with source rectangle in sprite sheet
/// </summary>
public class SpriteFrameInfo
{
    [JsonPropertyName("Index")]
    public int Index { get; set; }

    /// <summary>
    ///     X position in sprite sheet (source rectangle)
    /// </summary>
    [JsonPropertyName("X")]
    public int X { get; set; }

    /// <summary>
    ///     Y position in sprite sheet (source rectangle)
    /// </summary>
    [JsonPropertyName("Y")]
    public int Y { get; set; }

    /// <summary>
    ///     Width of frame (source rectangle)
    /// </summary>
    [JsonPropertyName("Width")]
    public int Width { get; set; }

    /// <summary>
    ///     Height of frame (source rectangle)
    /// </summary>
    [JsonPropertyName("Height")]
    public int Height { get; set; }
}

/// <summary>
///     Animation definition for a sprite
/// </summary>
public class SpriteAnimationInfo
{
    [JsonPropertyName("Name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("Loop")]
    public bool Loop { get; set; }

    [JsonPropertyName("FrameIndices")]
    public int[] FrameIndices { get; set; } = Array.Empty<int>();

    [JsonPropertyName("FrameDuration")]
    public float FrameDuration { get; set; } // Deprecated: kept for backward compatibility, use FrameDurations instead

    /// <summary>
    ///     Per-frame durations in seconds (one per frame in FrameIndices).
    ///     If null or empty, falls back to FrameDuration for all frames.
    /// </summary>
    [JsonPropertyName("FrameDurations")]
    public float[]? FrameDurations { get; set; }

    [JsonPropertyName("FlipHorizontal")]
    public bool FlipHorizontal { get; set; }
}
