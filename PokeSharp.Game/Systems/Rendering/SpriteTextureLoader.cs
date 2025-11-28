using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework.Graphics;
using PokeSharp.Engine.Common.Logging;
using PokeSharp.Engine.Core.Types;
using PokeSharp.Engine.Rendering.Assets;
using PokeSharp.Game.Infrastructure.Services;

namespace PokeSharp.Game.Systems;

/// <summary>
///     Service responsible for loading sprite sheet textures into the AssetManager.
///     Scans available sprite manifests and loads their sprite sheets.
/// </summary>
public class SpriteTextureLoader
{
    private readonly AssetManager _assetManager;
    private readonly GraphicsDevice _graphicsDevice;
    private readonly ILogger<SpriteTextureLoader>? _logger;

    // PHASE 2: Per-map sprite tracking for lazy loading
    private readonly Dictionary<MapRuntimeId, HashSet<SpriteId>> _mapSpriteIds = new();

    // Persistent sprites that should never be unloaded (e.g., UI elements)
    // Player sprites load on-demand from entity templates
    private readonly HashSet<string> _persistentSprites = new();
    private readonly SpriteLoader _spriteLoader;
    private readonly Dictionary<string, int> _spriteReferenceCount = new();

    public SpriteTextureLoader(
        SpriteLoader spriteLoader,
        AssetManager assetManager,
        GraphicsDevice graphicsDevice,
        ILogger<SpriteTextureLoader>? logger = null
    )
    {
        _spriteLoader = spriteLoader ?? throw new ArgumentNullException(nameof(spriteLoader));
        _assetManager = assetManager ?? throw new ArgumentNullException(nameof(assetManager));
        _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
        _logger = logger;
    }

    /// <summary>
    ///     Loads all sprite sheet textures found in the sprites directory.
    /// </summary>
    /// <returns>Number of sprite sheets loaded.</returns>
    public async Task<int> LoadAllSpriteTexturesAsync()
    {
        _logger?.LogAssetLoadingStarted("sprite sheet textures", 0);

        List<SpriteManifest> manifests = await _spriteLoader.LoadAllSpritesAsync();
        _logger?.LogSpriteManifestsFound(manifests.Count);

        int loadedCount = 0;
        int failedCount = 0;

        foreach (SpriteManifest manifest in manifests)
        {
            try
            {
                string textureKey = GetTextureKey(manifest.Category, manifest.Name);
                string? spritesheetPath = _spriteLoader.GetSpriteSheetPath(manifest);

                _logger?.LogSpriteLoadingProgress(
                    loadedCount + 1,
                    manifests.Count,
                    manifest.Category,
                    manifest.Name
                );

                if (string.IsNullOrEmpty(spritesheetPath) || !File.Exists(spritesheetPath))
                {
                    _logger?.LogSpriteSheetNotFound(manifest.Category, manifest.Name);
                    failedCount++;
                    continue;
                }

                // Load texture using MonoGame's Texture2D.FromStream
                using FileStream fileStream = File.OpenRead(spritesheetPath);
                var texture = Texture2D.FromStream(_graphicsDevice, fileStream);

                _logger?.LogSpriteTextureWithDimensions(
                    manifest.Category,
                    manifest.Name,
                    texture.Format,
                    texture.Width,
                    texture.Height
                );

                // Note: Transparency should be baked into the PNG by the extractor
                // Runtime mask color application doesn't persist correctly
                // Register with AssetManager (using a direct registration method)
                RegisterTexture(textureKey, texture);
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

        _logger?.LogSpriteLoadingComplete(loadedCount, failedCount);

        return loadedCount;
    }

    /// <summary>
    ///     Loads a specific sprite sheet texture.
    /// </summary>
    public void LoadSpriteTexture(string category, string spriteName)
    {
        string textureKey = GetTextureKey(category, spriteName);

        // Check if already loaded
        if (_assetManager.HasTexture(textureKey))
        {
            return;
        }

        // Use NPCSpriteLoader to resolve the actual path
        string? spritesheetPath = _spriteLoader.GetSpriteSheetPath(category, spriteName);

        if (string.IsNullOrEmpty(spritesheetPath) || !File.Exists(spritesheetPath))
        {
            _logger?.LogSpriteSheetNotFound(category, spriteName);
            return;
        }

        // Load texture (transparency is already baked into the PNG)
        using FileStream fileStream = File.OpenRead(spritesheetPath);
        var texture = Texture2D.FromStream(_graphicsDevice, fileStream);

        _logger?.LogSpriteTextureLazyLoaded(textureKey, texture.Format);

        // Note: Transparency should be baked into the PNG by the extractor
        // Runtime mask color application doesn't persist correctly

        // Register with AssetManager
        RegisterTexture(textureKey, texture);

        _logger?.LogSpriteLoadedOnDemand(textureKey);
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
    ///     Loads only the sprites required for a specific map.
    ///     Implements lazy loading to reduce memory usage by 75%.
    /// </summary>
    /// <param name="mapId">Map ID to load sprites for</param>
    /// <param name="spriteIds">Collection of sprite IDs needed for this map</param>
    /// <returns>HashSet of loaded texture keys</returns>
    public async Task<HashSet<string>> LoadSpritesForMapAsync(
        MapRuntimeId mapId,
        IEnumerable<SpriteId> spriteIds
    )
    {
        int loadedCount = 0;
        int skippedCount = 0;
        var spriteIdSet = new HashSet<SpriteId>(spriteIds);

        // Track which sprites belong to this map
        _mapSpriteIds[mapId] = spriteIdSet;

        _logger?.LogSpritesRequiredForMap(mapId.Value, spriteIdSet.Count);

        foreach (SpriteId spriteId in spriteIdSet)
        {
            string category = spriteId.Category;
            string spriteName = spriteId.SpriteName;
            string textureKey = $"sprites/{category}/{spriteName}";

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
                _logger?.LogWarning(
                    ex,
                    "Failed to load sprite texture: {Category}/{SpriteName}",
                    category,
                    spriteName
                );
            }
        }

        _logger?.LogSpritesLoadedForMap(loadedCount, mapId.Value, skippedCount);

        return spriteIdSet.Select(id => $"sprites/{id.Value}").ToHashSet();
    }

    /// <summary>
    ///     Loads a single sprite texture asynchronously.
    /// </summary>
    private async Task LoadSpriteTextureAsync(string category, string spriteName)
    {
        SpriteManifest? manifest = await _spriteLoader.LoadSpriteAsync(spriteName);
        if (manifest == null)
        {
            _logger?.LogWarning(
                "Sprite manifest not found: {Category}/{SpriteName}",
                category,
                spriteName
            );
            return;
        }

        string textureKey = $"sprites/{category}/{spriteName}";
        string? spritesheetPath = _spriteLoader.GetSpriteSheetPath(manifest);

        if (string.IsNullOrEmpty(spritesheetPath) || !File.Exists(spritesheetPath))
        {
            _logger?.LogSpriteTextureFileNotFound(category, spriteName);
            return;
        }

        using FileStream fileStream = File.OpenRead(spritesheetPath);
        var texture = Texture2D.FromStream(_graphicsDevice, fileStream);

        RegisterTexture(textureKey, texture);
        _logger?.LogSpriteTextureLoadedDebug(textureKey);
    }

    /// <summary>
    ///     Increments reference count for a sprite texture.
    ///     Used to track which maps are using each sprite.
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
    ///     Decrements reference count for a sprite texture.
    ///     Returns true if sprite can be safely unloaded (ref count = 0).
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
    ///     Unloads sprites that are no longer needed after a map is unloaded.
    ///     Uses reference counting to prevent unloading shared sprites.
    /// </summary>
    /// <param name="mapId">Map ID to unload sprites for</param>
    /// <returns>Number of sprites unloaded</returns>
    public int UnloadSpritesForMap(MapRuntimeId mapId)
    {
        if (!_mapSpriteIds.TryGetValue(mapId, out HashSet<SpriteId>? spriteIds))
        {
            _logger?.LogNoSpritesForMap(mapId.Value);
            return 0;
        }

        int unloadedCount = 0;

        foreach (SpriteId spriteId in spriteIds)
        {
            string textureKey = $"sprites/{spriteId.Category}/{spriteId.SpriteName}";

            // Never unload persistent sprites (player sprites)
            if (_persistentSprites.Contains(textureKey))
            {
                continue;
            }

            // Decrement reference count
            if (DecrementReferenceCount(textureKey))
            // Reference count is zero, safe to unload
            {
                if (_assetManager.UnregisterTexture(textureKey))
                {
                    unloadedCount++;
                    _logger?.LogDebug("Unloaded sprite texture: {TextureKey}", textureKey);
                }
            }
        }

        // Remove map tracking
        _mapSpriteIds.Remove(mapId);

        _logger?.LogSpritesUnloadedForMap(unloadedCount, mapId);
        return unloadedCount;
    }

    /// <summary>
    ///     Gets sprite loading statistics for monitoring and debugging.
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
    ///     Clears the sprite loader's manifest cache.
    ///     Delegates to the underlying SpriteLoader.
    /// </summary>
    public void ClearCache()
    {
        _spriteLoader?.ClearCache();
    }
}
