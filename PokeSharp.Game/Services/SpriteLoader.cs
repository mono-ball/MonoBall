using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace PokeSharp.Game.Services;

/// <summary>
/// Service for loading sprites extracted from Pokemon Emerald.
/// Sprites are organized in two main categories:
/// - Players: brendan/, may/ (each with variants like normal, surfing, machbike)
/// - NPCs: generic/, elite_four/, gym_leaders/, team_aqua/, team_magma/, frontier_brains/
/// Implements caching for performance - use ClearCache() to free memory when needed.
/// </summary>
public class SpriteLoader
{
    private readonly ILogger<SpriteLoader> _logger;
    private readonly List<string> _spritesBasePaths;
    // PERFORMANCE: Manifest cache for O(1) lookups
    // MEMORY: Call ClearCache() during map transitions to prevent memory buildup
    private Dictionary<string, SpriteManifest>? _spriteCache;
    private Dictionary<string, string>? _spritePathLookup; // Maps "category/spriteId" -> full directory path
    private List<SpriteManifest>? _allSprites;

    public SpriteLoader(ILogger<SpriteLoader> logger)
    {
        _logger = logger;
        _spritesBasePaths = new List<string> { "Assets/Sprites/Players", "Assets/Sprites/NPCs" };
    }

    /// <summary>
    /// Load all available sprites by scanning directories for individual manifests.
    /// Scans both Assets/Sprites/Players and Assets/Sprites/NPCs.
    /// </summary>
    public async Task<List<SpriteManifest>> LoadAllSpritesAsync()
    {
        if (_allSprites != null)
        {
            return _allSprites;
        }

        _allSprites = new List<SpriteManifest>();
        _spritePathLookup = new Dictionary<string, string>();

        foreach (var spritesBasePath in _spritesBasePaths)
        {
            if (!Directory.Exists(spritesBasePath))
            {
                _logger.LogWarning(
                    "Sprites directory not found at {Path}. Run the sprite extractor tool.",
                    spritesBasePath
                );
                continue;
            }

            _logger.LogInformation("Scanning for sprite manifests in {Path}", spritesBasePath);

            // Find all manifest.json files in subdirectories
            var manifestFiles = Directory.GetFiles(
                spritesBasePath,
                "manifest.json",
                SearchOption.AllDirectories
            );

            foreach (var manifestFile in manifestFiles)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(manifestFile);
                    var manifest = JsonSerializer.Deserialize<SpriteManifest>(
                        json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    );

                    if (manifest != null)
                    {
                        _allSprites.Add(manifest);

                        // Build lookup: "category/spriteId" -> full directory path
                        var manifestDir = Path.GetDirectoryName(manifestFile);
                        if (!string.IsNullOrEmpty(manifestDir))
                        {
                            var lookupKey = $"{manifest.Category}/{manifest.Name}";
                            _spritePathLookup[lookupKey] = manifestDir;
                            _logger.LogDebug(
                                "Registered sprite: {Key} -> {Path}",
                                lookupKey,
                                manifestDir
                            );
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load manifest from {Path}", manifestFile);
                }
            }
        }

        _logger.LogInformation(
            "Loaded {Count} total sprites from all categories",
            _allSprites.Count
        );

        return _allSprites;
    }

    /// <summary>
    /// Load a specific sprite by name
    /// </summary>
    public async Task<SpriteManifest?> LoadSpriteAsync(string spriteName)
    {
        if (_spriteCache == null)
        {
            _spriteCache = new Dictionary<string, SpriteManifest>();
            var allSprites = await LoadAllSpritesAsync();
            foreach (var sprite in allSprites)
            {
                _spriteCache[sprite.Name] = sprite;
            }
        }

        if (_spriteCache.TryGetValue(spriteName, out var manifest))
        {
            return manifest;
        }

        _logger.LogWarning("Sprite {SpriteName} not found in manifest", spriteName);
        return null;
    }

    /// <summary>
    /// Get all sprites in a specific category
    /// </summary>
    public async Task<List<SpriteManifest>> GetSpritesByCategoryAsync(string category)
    {
        var allSprites = await LoadAllSpritesAsync();
        return allSprites.Where(s => s.Category == category).ToList();
    }

    /// <summary>
    /// Get all available categories
    /// </summary>
    public async Task<List<string>> GetCategoriesAsync()
    {
        var allSprites = await LoadAllSpritesAsync();
        return allSprites.Select(s => s.Category).Distinct().OrderBy(c => c).ToList();
    }

    /// <summary>
    /// Get the full path to a sprite's directory by category and name.
    /// NOTE: LoadAllSpritesAsync() must be called before using this method.
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

        var lookupKey = $"{category}/{spriteName}";
        if (_spritePathLookup.TryGetValue(lookupKey, out var path))
        {
            return path;
        }

        _logger.LogWarning("Sprite path not found for {LookupKey}", lookupKey);
        return null;
    }

    /// <summary>
    /// Get the full path to a sprite's directory (manifest version)
    /// </summary>
    public string? GetSpritePath(SpriteManifest manifest)
    {
        return GetSpritePath(manifest.Category, manifest.Name);
    }

    /// <summary>
    /// Get the full path to the sprite sheet
    /// </summary>
    public string? GetSpriteSheetPath(string category, string spriteName)
    {
        var spritePath = GetSpritePath(category, spriteName);
        return spritePath != null ? Path.Combine(spritePath, "spritesheet.png") : null;
    }

    /// <summary>
    /// Get the full path to the sprite sheet (manifest version)
    /// </summary>
    public string? GetSpriteSheetPath(SpriteManifest manifest)
    {
        return GetSpriteSheetPath(manifest.Category, manifest.Name);
    }

    /// <summary>
    /// Clears the manifest cache to free memory.
    /// Call this when transitioning between maps or during cleanup.
    /// </summary>
    public void ClearCache()
    {
        _spriteCache?.Clear();
        _spriteCache = null;

        _allSprites?.Clear();
        _allSprites = null;

        _spritePathLookup?.Clear();
        _spritePathLookup = null;

        _logger.LogDebug("Sprite manifest cache cleared");
    }

    /// <summary>
    /// Clears specific sprite from cache by ID.
    /// Useful for selective cleanup of unused sprites.
    /// </summary>
    public void ClearSprite(string category, string spriteName)
    {
        var key = $"{category}/{spriteName}";

        if (_spriteCache?.Remove(key) == true)
        {
            _logger.LogDebug("Cleared sprite manifest from cache: {SpriteKey}", key);
        }

        // Note: We don't remove from _allSprites or _spritePathLookup as those are
        // rebuilt from disk on next LoadAllSpritesAsync() call
    }

    /// <summary>
    /// Gets current cache statistics for monitoring.
    /// </summary>
    public (int ManifestCount, int PathCount) GetCacheStats()
    {
        return (
            _spriteCache?.Count ?? 0,
            _spritePathLookup?.Count ?? 0
        );
    }
}

/// <summary>
/// Manifest for a sprite extracted from Pokemon Emerald.
/// Contains metadata about frames, animations, and the sprite's category.
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
/// Information about a single sprite frame with source rectangle in sprite sheet
/// </summary>
public class SpriteFrameInfo
{
    [JsonPropertyName("Index")]
    public int Index { get; set; }

    /// <summary>
    /// X position in sprite sheet (source rectangle)
    /// </summary>
    [JsonPropertyName("X")]
    public int X { get; set; }

    /// <summary>
    /// Y position in sprite sheet (source rectangle)
    /// </summary>
    [JsonPropertyName("Y")]
    public int Y { get; set; }

    /// <summary>
    /// Width of frame (source rectangle)
    /// </summary>
    [JsonPropertyName("Width")]
    public int Width { get; set; }

    /// <summary>
    /// Height of frame (source rectangle)
    /// </summary>
    [JsonPropertyName("Height")]
    public int Height { get; set; }
}

/// <summary>
/// Animation definition for a sprite
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
    public float FrameDuration { get; set; }

    [JsonPropertyName("FlipHorizontal")]
    public bool FlipHorizontal { get; set; }
}
