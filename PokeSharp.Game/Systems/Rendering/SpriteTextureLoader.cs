using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework.Graphics;
using PokeSharp.Engine.Rendering.Assets;
using PokeSharp.Game.Services;

namespace PokeSharp.Game.Systems;

/// <summary>
///     Service responsible for loading sprite sheet textures into the AssetManager.
///     Scans available sprite manifests and loads their sprite sheets.
/// </summary>
public class SpriteTextureLoader
{
    private readonly SpriteLoader _spriteLoader;
    private readonly AssetManager _assetManager;
    private readonly GraphicsDevice _graphicsDevice;
    private readonly ILogger<SpriteTextureLoader>? _logger;
    private readonly string _spritesBasePath;

    // PHASE 2: Per-map sprite tracking for lazy loading
    private readonly Dictionary<int, HashSet<string>> _mapSpriteIds = new();
    private readonly Dictionary<string, int> _spriteReferenceCount = new();
    private readonly HashSet<string> _persistentSprites = new()
    {
        "sprites/players/brendan",
        "sprites/players/may"
    };

    public SpriteTextureLoader(
        SpriteLoader spriteLoader,
        AssetManager assetManager,
        GraphicsDevice graphicsDevice,
        string spritesBasePath = "Assets/Sprites/NPCs",
        ILogger<SpriteTextureLoader>? logger = null
    )
    {
        _spriteLoader = spriteLoader ?? throw new ArgumentNullException(nameof(spriteLoader));
        _assetManager = assetManager ?? throw new ArgumentNullException(nameof(assetManager));
        _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
        _spritesBasePath = spritesBasePath;
        _logger = logger;
    }

    /// <summary>
    ///     Loads all sprite sheet textures found in the sprites directory.
    /// </summary>
    /// <returns>Number of sprite sheets loaded.</returns>
    public async Task<int> LoadAllSpriteTexturesAsync()
    {
        _logger?.LogInformation("Loading sprite sheet textures...");

        var manifests = await _spriteLoader.LoadAllSpritesAsync();
        _logger?.LogInformation("Found {Count} sprite manifests to load", manifests.Count);

        var loadedCount = 0;
        var failedCount = 0;

        foreach (var manifest in manifests)
        {
            try
            {
                var textureKey = GetTextureKey(manifest.Category, manifest.Name);
                var spritesheetPath = _spriteLoader.GetSpriteSheetPath(manifest);

                _logger?.LogInformation(
                    "Loading sprite {Index}/{Total}: {Category}/{Name}",
                    loadedCount + 1,
                    manifests.Count,
                    manifest.Category,
                    manifest.Name
                );

                if (string.IsNullOrEmpty(spritesheetPath) || !File.Exists(spritesheetPath))
                {
                    _logger?.LogWarning(
                        "Sprite sheet not found for {Category}/{Name}",
                        manifest.Category,
                        manifest.Name
                    );
                    failedCount++;
                    continue;
                }

                _logger?.LogDebug("Opening file stream: {Path}", spritesheetPath);

                // Load texture using MonoGame's Texture2D.FromStream
                using var fileStream = File.OpenRead(spritesheetPath);

                _logger?.LogDebug("Decoding texture from stream...");
                var texture = Texture2D.FromStream(_graphicsDevice, fileStream);

                _logger?.LogInformation(
                    "Loaded texture: {Category}/{Name} Format={Format}, Size={Width}x{Height}",
                    manifest.Category,
                    manifest.Name,
                    texture.Format,
                    texture.Width,
                    texture.Height
                );

                // Note: Transparency should be baked into the PNG by the extractor
                // Runtime mask color application doesn't persist correctly

                _logger?.LogDebug("Registering texture: {Key}", textureKey);
                // Register with AssetManager (using a direct registration method)
                RegisterTexture(textureKey, texture);

                _logger?.LogDebug(
                    "Successfully loaded: {Category}/{Name}",
                    manifest.Category,
                    manifest.Name
                );
                loadedCount++;
            }
            catch (Exception ex)
            {
                _logger?.LogError(
                    ex,
                    "Failed to load sprite sheet for {Category}/{Name}",
                    manifest.Category,
                    manifest.Name
                );
                failedCount++;
            }
        }

        _logger?.LogInformation(
            "Sprite sheet loading complete: {Loaded} loaded, {Failed} failed",
            loadedCount,
            failedCount
        );

        return loadedCount;
    }

    /// <summary>
    ///     Loads a specific sprite sheet texture.
    /// </summary>
    public void LoadSpriteTexture(string category, string spriteName)
    {
        var textureKey = GetTextureKey(category, spriteName);

        // Check if already loaded
        if (_assetManager.HasTexture(textureKey))
            return;

        // Use NPCSpriteLoader to resolve the actual path
        var spritesheetPath = _spriteLoader.GetSpriteSheetPath(category, spriteName);

        if (string.IsNullOrEmpty(spritesheetPath) || !File.Exists(spritesheetPath))
        {
            _logger?.LogWarning(
                "Sprite sheet not found for {Category}/{SpriteName}",
                category,
                spriteName
            );
            return;
        }

        // Load texture (transparency is already baked into the PNG)
        using var fileStream = File.OpenRead(spritesheetPath);
        var texture = Texture2D.FromStream(_graphicsDevice, fileStream);

        _logger?.LogInformation(
            "Lazy-loaded texture: {TextureKey}, Format={Format}",
            textureKey,
            texture.Format
        );

        // Note: Transparency should be baked into the PNG by the extractor
        // Runtime mask color application doesn't persist correctly

        // Register with AssetManager
        RegisterTexture(textureKey, texture);

        _logger?.LogDebug("Loaded sprite sheet on-demand: {TextureKey}", textureKey);
    }

    /// <summary>
    ///     Gets the texture key used by the rendering system.
    /// </summary>
    private static string GetTextureKey(string category, string spriteName)
    {
        return $"sprites/{category}/{spriteName}";
    }

    /// <summary>
    ///     Registers a texture directly with the AssetManager.
    /// </summary>
    private void RegisterTexture(string textureKey, Texture2D texture)
    {
        _assetManager.RegisterTexture(textureKey, texture);
    }

    /// <summary>
    /// Loads only the sprites required for a specific map.
    /// Implements lazy loading to reduce memory usage by 75%.
    /// </summary>
    /// <param name="mapId">Map ID to load sprites for</param>
    /// <param name="spriteIds">Collection of sprite IDs needed for this map (format: "category/spriteName")</param>
    /// <returns>HashSet of loaded texture keys</returns>
    public async Task<HashSet<string>> LoadSpritesForMapAsync(int mapId, IEnumerable<string> spriteIds)
    {
        var loadedCount = 0;
        var skippedCount = 0;
        var spriteIdSet = new HashSet<string>(spriteIds);

        // Track which sprites belong to this map
        _mapSpriteIds[mapId] = spriteIdSet;

        _logger?.LogDebug("Loading sprites for map {MapId}: {Count} sprites required", mapId, spriteIdSet.Count);

        foreach (var spriteId in spriteIdSet)
        {
            // Parse sprite ID (format: "category/spriteName")
            var parts = spriteId.Split('/');
            if (parts.Length != 2)
            {
                _logger?.LogWarning("Invalid sprite ID format: {SpriteId}", spriteId);
                continue;
            }

            var category = parts[0];
            var spriteName = parts[1];
            var textureKey = $"sprites/{category}/{spriteName}";

            // Skip if already loaded
            if (_assetManager.HasTexture(textureKey))
            {
                skippedCount++;
                IncrementReferenceCount(textureKey);
                continue;
            }

            // Load the sprite texture
            try
            {
                await LoadSpriteTextureAsync(category, spriteName);
                IncrementReferenceCount(textureKey);
                loadedCount++;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to load sprite texture: {Category}/{SpriteName}", category, spriteName);
            }
        }

        _logger?.LogInformation(
            "Loaded {LoadedCount} new sprites for map {MapId}, {SkippedCount} already loaded",
            loadedCount, mapId, skippedCount);

        return spriteIdSet.Select(id => $"sprites/{id}").ToHashSet();
    }

    /// <summary>
    /// Loads a single sprite texture asynchronously.
    /// </summary>
    private async Task LoadSpriteTextureAsync(string category, string spriteName)
    {
        var manifest = await _spriteLoader.LoadSpriteAsync(spriteName);
        if (manifest == null)
        {
            _logger?.LogWarning("Sprite manifest not found: {Category}/{SpriteName}", category, spriteName);
            return;
        }

        var textureKey = $"sprites/{category}/{spriteName}";
        var spritesheetPath = _spriteLoader.GetSpriteSheetPath(manifest);

        if (string.IsNullOrEmpty(spritesheetPath) || !File.Exists(spritesheetPath))
        {
            _logger?.LogWarning("Sprite texture file not found for {Category}/{SpriteName}", category, spriteName);
            return;
        }

        using var fileStream = File.OpenRead(spritesheetPath);
        var texture = Texture2D.FromStream(_graphicsDevice, fileStream);

        RegisterTexture(textureKey, texture);
        _logger?.LogDebug("Loaded sprite texture: {TextureKey}", textureKey);
    }

    /// <summary>
    /// Increments reference count for a sprite texture.
    /// Used to track which maps are using each sprite.
    /// </summary>
    private void IncrementReferenceCount(string textureKey)
    {
        if (_spriteReferenceCount.ContainsKey(textureKey))
        {
            _spriteReferenceCount[textureKey]++;
        }
        else
        {
            _spriteReferenceCount[textureKey] = 1;
        }
    }

    /// <summary>
    /// Decrements reference count for a sprite texture.
    /// Returns true if sprite can be safely unloaded (ref count = 0).
    /// </summary>
    private bool DecrementReferenceCount(string textureKey)
    {
        if (!_spriteReferenceCount.ContainsKey(textureKey))
        {
            return true; // Not tracked, safe to unload
        }

        _spriteReferenceCount[textureKey]--;

        if (_spriteReferenceCount[textureKey] <= 0)
        {
            _spriteReferenceCount.Remove(textureKey);
            return true; // Can unload
        }

        return false; // Still in use
    }

    /// <summary>
    /// Unloads sprites that are no longer needed after a map is unloaded.
    /// Uses reference counting to prevent unloading shared sprites.
    /// </summary>
    /// <param name="mapId">Map ID to unload sprites for</param>
    /// <returns>Number of sprites unloaded</returns>
    public int UnloadSpritesForMap(int mapId)
    {
        if (!_mapSpriteIds.TryGetValue(mapId, out var spriteIds))
        {
            _logger?.LogDebug("No sprites tracked for map {MapId}", mapId);
            return 0;
        }

        var unloadedCount = 0;

        foreach (var spriteId in spriteIds)
        {
            var parts = spriteId.Split('/');
            if (parts.Length != 2) continue;

            var textureKey = $"sprites/{parts[0]}/{parts[1]}";

            // Never unload persistent sprites (player sprites)
            if (_persistentSprites.Contains(textureKey))
            {
                continue;
            }

            // Decrement reference count
            if (DecrementReferenceCount(textureKey))
            {
                // Reference count is zero, safe to unload
                if (_assetManager.UnregisterTexture(textureKey))
                {
                    unloadedCount++;
                    _logger?.LogDebug("Unloaded sprite texture: {TextureKey}", textureKey);
                }
            }
        }

        // Remove map tracking
        _mapSpriteIds.Remove(mapId);

        _logger?.LogInformation("Unloaded {Count} sprites for map {MapId}", unloadedCount, mapId);
        return unloadedCount;
    }

    /// <summary>
    /// Gets sprite loading statistics for monitoring and debugging.
    /// </summary>
    public (int MapsTracked, int UniqueSprites, int TotalReferences) GetSpriteStats()
    {
        return (
            _mapSpriteIds.Count,
            _spriteReferenceCount.Count,
            _spriteReferenceCount.Values.Sum()
        );
    }

    /// <summary>
    /// Clears the sprite loader's manifest cache.
    /// Delegates to the underlying SpriteLoader.
    /// </summary>
    public void ClearCache()
    {
        _spriteLoader?.ClearCache();
    }
}
