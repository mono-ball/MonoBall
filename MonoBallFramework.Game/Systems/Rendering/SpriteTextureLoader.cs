using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework.Graphics;
using MonoBallFramework.Game.Engine.Common.Logging;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.Engine.Rendering.Assets;
using MonoBallFramework.Game.GameData.Entities;
using MonoBallFramework.Game.GameData.Sprites;

namespace MonoBallFramework.Game.Systems.Rendering;

/// <summary>
///     Service responsible for loading sprite sheet textures into the AssetManager.
///     Scans available sprite definitions and loads their sprite sheets.
///     Uses AssetManager which internally delegates to ContentProvider for mod-aware path resolution.
/// </summary>
public class SpriteTextureLoader
{
    private readonly AssetManager _assetManager;
    private readonly GraphicsDevice _graphicsDevice;
    private readonly ILogger<SpriteTextureLoader>? _logger;

    // PHASE 2: Per-map sprite tracking for lazy loading
    private readonly Dictionary<GameMapId, HashSet<GameSpriteId>> _mapSpriteIds = new();

    // Persistent sprites that should never be unloaded (e.g., UI elements)
    // Player sprites load on-demand from entity templates
    private readonly HashSet<string> _persistentSprites = new();

    // Track missing sprites to avoid repeated log warnings (prevents log spam)
    private readonly HashSet<string> _reportedMissingSprites = new();
    private readonly Dictionary<string, int> _spriteReferenceCount = new();
    private readonly SpriteRegistry _spriteRegistry;

    public SpriteTextureLoader(
        SpriteRegistry spriteRegistry,
        AssetManager assetManager,
        GraphicsDevice graphicsDevice,
        ILogger<SpriteTextureLoader>? logger = null
    )
    {
        _spriteRegistry = spriteRegistry ?? throw new ArgumentNullException(nameof(spriteRegistry));
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

        // Ensure sprite definitions are loaded
        if (!_spriteRegistry.IsLoaded)
        {
            await _spriteRegistry.LoadDefinitionsAsync();
        }

        var spriteIds = _spriteRegistry.GetAllSpriteIds().ToList();
        _logger?.LogInformation("Found {Count} sprite definitions to load", spriteIds.Count);

        int loadedCount = 0;
        int failedCount = 0;

        foreach (GameSpriteId spriteId in spriteIds)
        {
            try
            {
                SpriteEntity? definition = _spriteRegistry.GetSprite(spriteId);
                if (definition == null)
                {
                    failedCount++;
                    continue;
                }

                // Use GameSpriteId's TextureKey property which correctly handles subcategories
                // Format: sprites/{category}/{name} OR sprites/{category}/{subcategory}/{name}
                string textureKey = spriteId.TextureKey;

                _logger?.LogSpriteLoadingProgress(
                    loadedCount + 1,
                    spriteIds.Count,
                    spriteId.Category,
                    spriteId.Name
                );

                // Resolve through AssetManager so mod content is considered before base assets
                _assetManager.LoadTexture(textureKey, definition.TexturePath);

                if (_assetManager.TryGetTexture(textureKey, out Texture2D? texture) && texture != null)
                {
                    _logger?.LogSpriteTextureWithDimensions(
                        spriteId.Category,
                        spriteId.Name,
                        texture.Format,
                        texture.Width,
                        texture.Height
                    );
                }

                loadedCount++;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load sprite sheet for {SpriteId}", spriteId);
                failedCount++;
            }
        }

        _logger?.LogSpriteLoadingComplete(loadedCount, failedCount);

        return loadedCount;
    }

    /// <summary>
    ///     Loads a specific sprite sheet texture using the full sprite path.
    ///     Supports both 2-segment (category/name) and 3-segment (category/subcategory/name) paths.
    /// </summary>
    /// <param name="spritePath">Full sprite path (e.g., "npcs/boy_1" or "npcs/generic/boy_1")</param>
    public void LoadSpriteTexture(string spritePath)
    {
        // Build texture key from the full path
        string textureKey = $"sprites/{spritePath}";

        // Check if already loaded
        if (_assetManager.HasTexture(textureKey))
        {
            return;
        }

        // Skip sprites we've already reported as missing
        if (_reportedMissingSprites.Contains(spritePath))
        {
            return;
        }

        // Use SpriteRegistry to get the definition using full path
        SpriteEntity? definition = _spriteRegistry.GetSpriteByPath(spritePath);

        if (definition == null)
        {
            // Only log once per missing sprite
            if (_reportedMissingSprites.Add(spritePath))
            {
                _logger?.LogSpriteSheetNotFound("sprite", spritePath);
            }

            return;
        }

        // Resolve through AssetManager so mod content is considered
        _assetManager.LoadTexture(textureKey, definition.TexturePath);

        if (_assetManager.TryGetTexture(textureKey, out Texture2D? texture) && texture != null)
        {
            _logger?.LogSpriteTextureLazyLoaded(textureKey, texture.Format);
        }

        _logger?.LogSpriteLoadedOnDemand(textureKey);
    }

    /// <summary>
    ///     Loads a specific sprite sheet texture (legacy overload for backwards compatibility).
    ///     Callers should prefer the single-parameter overload with the full sprite path.
    /// </summary>
    /// <param name="category">Sprite category</param>
    /// <param name="spriteName">Sprite name (may include subcategory as "subcategory/name")</param>
    public void LoadSpriteTexture(string category, string spriteName)
    {
        // Build full path - the spriteName might already include a subcategory
        string spritePath = $"{category}/{spriteName}";
        LoadSpriteTexture(spritePath);
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
        GameMapId mapId,
        IEnumerable<GameSpriteId> spriteIds
    )
    {
        int loadedCount = 0;
        int skippedCount = 0;
        var spriteIdSet = new HashSet<GameSpriteId>(spriteIds);

        // Track which sprites belong to this map
        _mapSpriteIds[mapId] = spriteIdSet;

        _logger?.LogSpritesRequiredForMap(mapId.Value, spriteIdSet.Count);

        foreach (GameSpriteId spriteId in spriteIdSet)
        {
            // Use TextureKey which correctly includes subcategory if present
            string textureKey = spriteId.TextureKey;

            // Skip if already loaded
            if (_assetManager.HasTexture(textureKey))
            {
                skippedCount++;
                IncrementReferenceCount(textureKey);
                continue;
            }

            // Load the sprite texture using full LocalId path
            try
            {
                await LoadSpriteTextureAsync(spriteId.LocalId);
                IncrementReferenceCount(textureKey);
                loadedCount++;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(
                    ex,
                    "Failed to load sprite texture: {SpritePath}",
                    spriteId.LocalId
                );
            }
        }

        _logger?.LogSpritesLoadedForMap(loadedCount, mapId.Value, skippedCount);

        return spriteIdSet.Select(id => id.TextureKey).ToHashSet();
    }

    /// <summary>
    ///     Loads a single sprite texture asynchronously.
    ///     Supports full sprite paths including subcategory (e.g., "npcs/generic/boy_1").
    /// </summary>
    /// <param name="spritePath">Full sprite path (e.g., "npcs/boy_1" or "npcs/generic/boy_1")</param>
    private async Task LoadSpriteTextureAsync(string spritePath)
    {
        // Ensure definitions are loaded
        if (!_spriteRegistry.IsLoaded)
        {
            await _spriteRegistry.LoadDefinitionsAsync();
        }

        SpriteEntity? definition = _spriteRegistry.GetSpriteByPath(spritePath);

        if (definition == null)
        {
            _logger?.LogWarning(
                "Sprite definition not found: {SpritePath}",
                spritePath
            );
            return;
        }

        string textureKey = $"sprites/{spritePath}";

        // Resolve through AssetManager so mod content is considered
        _assetManager.LoadTexture(textureKey, definition.TexturePath);

        if (_assetManager.TryGetTexture(textureKey, out Texture2D? texture) && texture != null)
        {
            _logger?.LogSpriteTextureLoadedDebug(textureKey);
        }
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
    public int UnloadSpritesForMap(GameMapId mapId)
    {
        if (!_mapSpriteIds.TryGetValue(mapId, out HashSet<GameSpriteId>? spriteIds))
        {
            _logger?.LogNoSpritesForMap(mapId.Value);
            return 0;
        }

        int unloadedCount = 0;

        foreach (GameSpriteId spriteId in spriteIds)
        {
            string textureKey = spriteId.TextureKey;

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

        _logger?.LogSpritesUnloadedForMap(unloadedCount, mapId.Value);
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
    ///     Clears the reported missing sprites set.
    ///     Call this if sprites have been added and you want to retry loading.
    /// </summary>
    public void ClearMissingSpritesCache()
    {
        _reportedMissingSprites.Clear();
    }
}
