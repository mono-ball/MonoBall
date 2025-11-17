using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Common.Logging;
using PokeSharp.Engine.Core.Types;
using PokeSharp.Engine.Rendering.Assets;
using PokeSharp.Engine.Systems.Queries;
using PokeSharp.Game.Components.Maps;
using PokeSharp.Game.Components.Rendering;
using PokeSharp.Game.Components.Tiles;

namespace PokeSharp.Game.Systems;

/// <summary>
/// Manages map lifecycle: loading, unloading, and memory cleanup.
/// Ensures only active maps remain in memory to prevent entity/texture accumulation.
/// </summary>
public class MapLifecycleManager(
    World world,
    IAssetProvider assetProvider,
    SpriteTextureLoader spriteTextureLoader,
    ILogger<MapLifecycleManager>? logger = null
)
{
    private readonly World _world = world ?? throw new ArgumentNullException(nameof(world));
    private readonly IAssetProvider _assetProvider = assetProvider ?? throw new ArgumentNullException(nameof(assetProvider));
    private readonly SpriteTextureLoader _spriteTextureLoader = spriteTextureLoader ?? throw new ArgumentNullException(nameof(spriteTextureLoader));
    private readonly ILogger<MapLifecycleManager>? _logger = logger;
    private readonly Dictionary<MapRuntimeId, MapMetadata> _loadedMaps = new();
    private MapRuntimeId? _currentMapId;
    private MapRuntimeId? _previousMapId;

    /// <summary>
    /// Gets the current active map ID
    /// </summary>
    public MapRuntimeId? CurrentMapId => _currentMapId;

    /// <summary>
    /// Registers a newly loaded map with tileset and sprite textures
    /// </summary>
    public void RegisterMap(MapRuntimeId mapId, string mapName, HashSet<string> tilesetTextureIds, HashSet<string> spriteTextureIds)
    {
        _loadedMaps[mapId] = new MapMetadata(mapName, tilesetTextureIds, spriteTextureIds);
        _logger?.LogWorkflowStatus(
            "Registered map",
            ("mapName", mapName),
            ("mapId", mapId.Value),
            ("tilesetCount", tilesetTextureIds.Count),
            ("spriteCount", spriteTextureIds.Count)
        );
    }

    /// <summary>
    /// Transitions to a new map, cleaning up old map entities and textures
    /// </summary>
    public void TransitionToMap(MapRuntimeId newMapId)
    {
        if (_currentMapId.HasValue && _currentMapId.Value == newMapId)
        {
            _logger?.LogDebug("Already on map {MapId}, skipping transition", newMapId.Value);
            return;
        }

        var oldMapId = _currentMapId;
        _previousMapId = _currentMapId;
        _currentMapId = newMapId;

        _logger?.LogWorkflowStatus(
            "Map transition",
            ("from", oldMapId?.Value ?? -1),
            ("to", newMapId.Value)
        );

        // Clean up old maps (keep current + previous for smooth transitions)
        var mapsToUnload = _loadedMaps
            .Keys.Where(id => id != _currentMapId && id != _previousMapId)
            .ToList();

        foreach (var mapId in mapsToUnload)
        {
            UnloadMap(mapId);
        }
    }

    /// <summary>
    /// Unloads a specific map: destroys entities and unloads textures
    /// </summary>
    public void UnloadMap(MapRuntimeId mapId)
    {
        if (!_loadedMaps.TryGetValue(mapId, out var metadata))
        {
            _logger?.LogWarning("Attempted to unload unknown map: {MapId}", mapId.Value);
            return;
        }

        _logger?.LogWorkflowStatus("Unloading map", ("mapName", metadata.Name), ("mapId", mapId.Value));

        // 1. Destroy all tile entities for this map
        var tilesDestroyed = DestroyMapEntities(mapId);

        // 2. Unload tileset textures (if AssetManager supports it)
        var tilesetsUnloaded = UnloadMapTextures(metadata.TilesetTextureIds);

        // 3. PHASE 2: Unload sprite textures for this map
        var spritesUnloaded = UnloadSpriteTextures(mapId, metadata.SpriteTextureIds);

        _loadedMaps.Remove(mapId);

        _logger?.LogWorkflowStatus(
            "Map unloaded",
            ("mapName", metadata.Name),
            ("entities", tilesDestroyed),
            ("tilesets", tilesetsUnloaded),
            ("sprites", spritesUnloaded)
        );
    }

    /// <summary>
    /// Destroys all entities belonging to a specific map
    /// </summary>
    private int DestroyMapEntities(MapRuntimeId mapId)
    {
        // CRITICAL FIX: Collect entities first, then destroy (can't modify during query)
        var entitiesToDestroy = new List<Entity>();

        // Use cached query for zero-allocation performance
        _world.Query(
            in Queries.AllTilePositioned,
            (Entity entity, ref TilePosition pos) =>
            {
                if (pos.MapId == mapId)
                {
                    entitiesToDestroy.Add(entity);
                }
            }
        );

        // FIX #10: Destroy ImageLayer entities
        // ImageLayers don't have MapId component, so we destroy all of them
        // (only one map is typically active at a time, so this is safe)
        var imageLayerQuery = new QueryDescription().WithAll<ImageLayer>();
        _world.Query(imageLayerQuery, (Entity entity) =>
        {
            entitiesToDestroy.Add(entity);
        });

        // Now destroy entities outside the query
        foreach (var entity in entitiesToDestroy)
        {
            _world.Destroy(entity);
        }

        _logger?.LogDebug(
            "Destroyed {Count} entities for map {MapId} (including image layers)",
            entitiesToDestroy.Count,
            mapId.Value
        );

        return entitiesToDestroy.Count;
    }

    /// <summary>
    /// Unloads textures for a map (if AssetManager supports UnregisterTexture)
    /// </summary>
    private int UnloadMapTextures(HashSet<string> textureIds)
    {
        if (_assetProvider is not AssetManager assetManager)
            return 0;

        var unloaded = 0;
        foreach (var textureId in textureIds)
        {
            // Check if texture is used by other loaded maps
            var isShared = _loadedMaps.Values.Any(m => m.TilesetTextureIds.Contains(textureId));

            if (!isShared)
            {
                if (assetManager.UnregisterTexture(textureId))
                {
                    unloaded++;
                }
            }
        }

        return unloaded;
    }

    /// <summary>
    /// PHASE 2: Unloads sprite textures for a map (with reference counting).
    /// </summary>
    private int UnloadSpriteTextures(MapRuntimeId mapId, HashSet<string> spriteTextureKeys)
    {
        try
        {
            var unloaded = _spriteTextureLoader.UnloadSpritesForMap(mapId);
            _logger?.LogDebug("Unloaded {Count} sprite textures for map {MapId}", unloaded, mapId.Value);
            return unloaded;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to unload sprite textures for map {MapId}", mapId.Value);
            return 0;
        }
    }

    /// <summary>
    /// Forces cleanup of all inactive maps (emergency memory cleanup)
    /// </summary>
    public void ForceCleanup()
    {
        _logger?.LogWarning("Force cleanup triggered - unloading all inactive maps");

        var mapsToUnload = _loadedMaps.Keys.Where(id => id != _currentMapId).ToList();

        foreach (var mapId in mapsToUnload)
        {
            UnloadMap(mapId);
        }

        // PHASE 2: Clear sprite manifest cache to free memory
        _spriteTextureLoader.ClearCache();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private record MapMetadata(string Name, HashSet<string> TilesetTextureIds, HashSet<string> SpriteTextureIds);
}
