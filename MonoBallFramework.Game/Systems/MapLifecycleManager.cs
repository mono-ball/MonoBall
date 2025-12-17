using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Ecs.Components.Maps;
using MonoBallFramework.Game.Ecs.Components.Movement;
using MonoBallFramework.Game.Ecs.Components.NPCs;
using MonoBallFramework.Game.Ecs.Components.Player;
using MonoBallFramework.Game.Engine.Common.Logging;
using MonoBallFramework.Game.Engine.Core.Events;
using MonoBallFramework.Game.Engine.Core.Events.Map;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.Engine.Rendering.Assets;
using MonoBallFramework.Game.Engine.Systems.Management;
using MonoBallFramework.Game.GameSystems.Spatial;
using MonoBallFramework.Game.Scripting.Systems;
using MonoBallFramework.Game.Systems.Rendering;

namespace MonoBallFramework.Game.Systems;

/// <summary>
///     Manages map lifecycle: loading, unloading, and memory cleanup.
///     Ensures only active maps remain in memory to prevent entity/texture accumulation.
///     Uses tile cache for efficient cleanup without relationship traversal.
/// </summary>
public class MapLifecycleManager(
    World world,
    IAssetProvider assetProvider,
    SpriteTextureLoader spriteTextureLoader,
    SpatialHashSystem spatialHashSystem,
    IEventBus? eventBus = null,
    ILogger<MapLifecycleManager>? logger = null
)
{
    private readonly Dictionary<string, MapMetadata> _loadedMaps = new();
    private readonly Dictionary<GameMapId, List<Entity>> _mapTileCache = new();
    private NPCBehaviorSystem? _npcBehaviorSystem;
    private GameMapId? _previousMapId;

    /// <summary>
    ///     Gets the current active map ID
    /// </summary>
    public GameMapId? CurrentMapId { get; private set; }

    /// <summary>
    ///     Sets the NPCBehaviorSystem for behavior cleanup during entity destruction.
    ///     Called after NPCBehaviorInitializer creates the system.
    /// </summary>
    public void SetNPCBehaviorSystem(NPCBehaviorSystem npcBehaviorSystem)
    {
        _npcBehaviorSystem = npcBehaviorSystem;
        logger?.LogDebug("NPCBehaviorSystem linked to MapLifecycleManager for cleanup");
    }

    /// <summary>
    ///     Registers tile entities for a map. Called by MapLoader after creating tiles.
    /// </summary>
    public void RegisterMapTiles(GameMapId mapId, List<Entity> tiles)
    {
        _mapTileCache[mapId] = tiles;
        logger?.LogDebug("Registered {Count} tiles for map {MapId}", tiles.Count, mapId.Value);
    }

    /// <summary>
    ///     Gets all tile entities for a map.
    /// </summary>
    public IReadOnlyList<Entity>? GetMapTiles(GameMapId mapId)
    {
        return _mapTileCache.TryGetValue(mapId, out List<Entity>? tiles) ? tiles : null;
    }

    /// <summary>
    ///     Clears tile cache for a map (called after tiles are destroyed).
    /// </summary>
    public void ClearMapTileCache(GameMapId mapId)
    {
        _mapTileCache.Remove(mapId);
    }

    /// <summary>
    ///     Registers a newly loaded map with tileset and sprite textures
    /// </summary>
    public void RegisterMap(
        GameMapId mapId,
        string mapName,
        HashSet<string> tilesetTextureIds,
        HashSet<string> spriteTextureIds
    )
    {
        _loadedMaps[mapId.Value] = new MapMetadata(mapName, tilesetTextureIds, spriteTextureIds);
        logger?.LogWorkflowStatus(
            "Registered map",
            ("mapName", mapName),
            ("mapId", mapId.Value),
            ("tilesetCount", tilesetTextureIds.Count),
            ("spriteCount", spriteTextureIds.Count)
        );
    }

    /// <summary>
    ///     Transitions to a new map, cleaning up old map entities and textures
    /// </summary>
    public void TransitionToMap(GameMapId newMapId)
    {
        if (CurrentMapId != null && CurrentMapId.Value == newMapId.Value)
        {
            logger?.LogDebug("Already on map {MapId}, skipping transition", newMapId.Value);
            return;
        }

        GameMapId? oldMapId = CurrentMapId;
        _previousMapId = CurrentMapId;
        CurrentMapId = newMapId;

        logger?.LogWorkflowStatus(
            "Map transition",
            ("from", oldMapId?.Value ?? "None"),
            ("to", newMapId.Value)
        );

        // Publish map transition event for subscribers (e.g., map popup display)
        PublishMapTransitionEvent(oldMapId, newMapId);

        // Clean up old maps (keep current + previous for smooth transitions)
        var mapsToUnload = _loadedMaps
            .Keys.Where(id =>
                (CurrentMapId == null || id != CurrentMapId.Value) &&
                (_previousMapId == null || id != _previousMapId.Value))
            .ToList();

        foreach (string mapIdValue in mapsToUnload)
        {
            UnloadMap(new GameMapId(mapIdValue));
        }
    }

    /// <summary>
    ///     Publishes a MapTransitionEvent with map metadata for subscribers.
    ///     Extracts DisplayName and RegionSection from map entities.
    /// </summary>
    private void PublishMapTransitionEvent(GameMapId? oldMapId, GameMapId newMapId)
    {
        if (eventBus == null)
        {
            return;
        }

        // Get metadata for the new map
        string? newMapName = _loadedMaps.TryGetValue(newMapId.Value, out MapMetadata? newMetadata)
            ? newMetadata.Name
            : null;

        // Query the world for display name and region section
        string? displayName = null;
        string? regionSection = null;

        QueryDescription mapInfoQuery = QueryCache.Get<MapInfo>();
        world.Query(
            in mapInfoQuery,
            (Entity entity, ref MapInfo info) =>
            {
                if (info.MapId == newMapId)
                {
                    // Get DisplayName if available
                    if (entity.Has<DisplayName>())
                    {
                        displayName = entity.Get<DisplayName>().Value;
                    }

                    // Get RegionSection if available
                    if (entity.Has<RegionSection>())
                    {
                        regionSection = entity.Get<RegionSection>().Value;
                    }
                }
            }
        );

        // Get old map name if available
        string? oldMapName = oldMapId != null && _loadedMaps.TryGetValue(oldMapId.Value, out MapMetadata? oldMetadata)
            ? oldMetadata.Name
            : null;

        // Publish the event
        eventBus.PublishPooled<MapTransitionEvent>(evt =>
        {
            evt.FromMapId = oldMapId;
            evt.FromMapName = oldMapName;
            evt.ToMapId = newMapId;
            evt.ToMapName = displayName ?? newMapName ?? "Unknown Map";
            evt.RegionName = regionSection;
        });

        logger?.LogDebug(
            "Published MapTransitionEvent: {FromMap} -> {ToMap} (Region: {Region})",
            oldMapName ?? "None",
            displayName ?? newMapName,
            regionSection ?? "None"
        );
    }

    /// <summary>
    ///     Unloads a specific map: destroys entities and unloads textures
    /// </summary>
    public void UnloadMap(GameMapId mapId)
    {
        if (!_loadedMaps.TryGetValue(mapId.Value, out MapMetadata? metadata))
        {
            logger?.LogWarning("Attempted to unload unknown map: {MapId}", mapId.Value);
            return;
        }

        logger?.LogWorkflowStatus(
            "Unloading map",
            ("mapName", metadata.Name),
            ("mapId", mapId.Value)
        );

        // 1. Destroy all tile entities for this map
        int tilesDestroyed = DestroyMapEntities(mapId);

        // 2. Unload tileset textures (if AssetManager supports it)
        int tilesetsUnloaded = UnloadMapTextures(metadata.TilesetTextureIds);

        // 3. PHASE 2: Unload sprite textures for this map
        int spritesUnloaded = UnloadSpriteTextures(mapId);

        _loadedMaps.Remove(mapId.Value);

        logger?.LogWorkflowStatus(
            "Map unloaded",
            ("mapName", metadata.Name),
            ("entities", tilesDestroyed),
            ("tilesets", tilesetsUnloaded),
            ("sprites", spritesUnloaded)
        );
    }

    /// <summary>
    ///     Destroys all entities belonging to a specific map.
    ///     Uses tile cache for efficient cleanup without relationship traversal.
    ///     Also queries Position.MapId to catch NPCs and other dynamic entities.
    ///     No pooling - entities are destroyed directly.
    /// </summary>
    private int DestroyMapEntities(GameMapId mapId)
    {
        int destroyedCount = 0;

        // 0. Remove tiles from spatial hash FIRST (incremental removal, avoids full rebuild)
        spatialHashSystem.RemoveMapTiles(mapId);

        // 1. Destroy all cached tile entities (no relationships, no pooling)
        if (_mapTileCache.TryGetValue(mapId, out List<Entity>? tiles))
        {
            foreach (Entity tile in tiles)
            {
                if (world.IsAlive(tile))
                {
                    world.Destroy(tile);
                    destroyedCount++;
                }
            }

            _mapTileCache.Remove(mapId);
            logger?.LogDebug("Destroyed {Count} cached tiles for map {MapId}", destroyedCount, mapId.Value);
        }

        // 2. Find and destroy all entities with Position.MapId matching this map (NPCs, warps, etc.)
        // Explicitly exclude Player entities to avoid destroying the player during map transitions
        var dynamicEntities = new List<Entity>();
        QueryDescription positionQuery = QueryCache.Get<Position>();
        world.Query(positionQuery, (Entity entity, ref Position pos) =>
        {
            if (pos.MapId == mapId && !entity.Has<Player>())
            {
                dynamicEntities.Add(entity);
            }
        });

        int dynamicCount = 0;
        foreach (Entity entity in dynamicEntities)
        {
            if (world.IsAlive(entity))
            {
                // Clean up NPC behaviors before destroying
                if (_npcBehaviorSystem != null && entity.Has<Behavior>())
                {
                    _npcBehaviorSystem.CleanupEntityBehavior(entity);
                }

                world.Destroy(entity);
                dynamicCount++;
                destroyedCount++;
            }
        }

        if (dynamicCount > 0)
        {
            logger?.LogDebug("Destroyed {Count} dynamic entities (NPCs, etc.) for map {MapId}", dynamicCount,
                mapId.Value);
        }

        // 3. Find and destroy the MapInfo entity
        Entity? mapInfoEntity = null;
        QueryDescription mapInfoQuery = QueryCache.Get<MapInfo>();
        world.Query(mapInfoQuery, (Entity entity, ref MapInfo info) =>
        {
            if (info.MapId == mapId)
            {
                mapInfoEntity = entity;
            }
        });

        if (mapInfoEntity.HasValue && world.IsAlive(mapInfoEntity.Value))
        {
            // Clean up NPC behaviors if any (though MapInfo shouldn't have behaviors)
            if (_npcBehaviorSystem != null && mapInfoEntity.Value.Has<Behavior>())
            {
                _npcBehaviorSystem.CleanupEntityBehavior(mapInfoEntity.Value);
            }

            world.Destroy(mapInfoEntity.Value);
            destroyedCount++;
        }

        logger?.LogInformation("Destroyed {Count} entities for map {MapId}", destroyedCount, mapId.Value);
        return destroyedCount;
    }


    /// <summary>
    ///     Unloads textures for a map (if AssetManager supports UnregisterTexture)
    /// </summary>
    private int UnloadMapTextures(HashSet<string> textureIds)
    {
        if (assetProvider is not AssetManager assetManager)
        {
            return 0;
        }

        int unloaded = 0;
        foreach (string textureId in textureIds)
        {
            // Check if texture is used by other loaded maps
            bool isShared = _loadedMaps.Values.Any(m => m.TilesetTextureIds.Contains(textureId));

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
    ///     PHASE 2: Unloads sprite textures for a map (with reference counting).
    /// </summary>
    private int UnloadSpriteTextures(GameMapId mapId)
    {
        try
        {
            int unloaded = spriteTextureLoader.UnloadSpritesForMap(mapId);
            logger?.LogDebug(
                "Unloaded {Count} sprite textures for map {MapId}",
                unloaded,
                mapId.Value
            );
            return unloaded;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to unload sprite textures for map {MapId}", mapId.Value);
            return 0;
        }
    }

    /// <summary>
    ///     Forces cleanup of all inactive maps (emergency memory cleanup)
    /// </summary>
    public void ForceCleanup()
    {
        logger?.LogWarning("Force cleanup triggered - unloading all inactive maps");

        var mapsToUnload = _loadedMaps.Keys
            .Where(id => CurrentMapId == null || id != CurrentMapId.Value)
            .ToList();

        foreach (string mapIdValue in mapsToUnload)
        {
            UnloadMap(new GameMapId(mapIdValue));
        }

        // PHASE 2: Clear sprite missing cache to free memory
        spriteTextureLoader.ClearMissingSpritesCache();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    /// <summary>
    ///     Unloads ALL maps. Used during warp transitions to completely clear the world
    ///     before loading the destination map.
    /// </summary>
    public void UnloadAllMaps()
    {
        logger?.LogInformation("Unloading all maps for warp transition");

        // Aggressively destroy ALL map-related entities, not just registered ones
        // This ensures complete cleanup even for maps that weren't properly registered
        int totalDestroyed = DestroyAllMapEntities();

        // Clear registered map metadata
        _loadedMaps.Clear();

        // Clear tile cache
        _mapTileCache.Clear();

        // Reset map tracking state
        CurrentMapId = null;
        _previousMapId = null;

        // Invalidate spatial hash so it rebuilds for the new map
        // Without this, old map tiles remain in the spatial index
        spatialHashSystem.InvalidateStaticTiles();

        // Clear sprite missing cache
        spriteTextureLoader.ClearMissingSpritesCache();

        logger?.LogInformation(
            "All map entities destroyed ({Count} entities), spatial hash invalidated",
            totalDestroyed
        );
    }

    /// <summary>
    ///     Destroys ALL map-related entities in the world.
    ///     Uses tile cache for efficient cleanup without relationship traversal.
    ///     Also queries Position to catch all dynamic entities (NPCs, warps, etc.).
    ///     Used for complete world cleanup during warp transitions.
    /// </summary>
    private int DestroyAllMapEntities()
    {
        int destroyedCount = 0;
        int behaviorsCleanedUp = 0;

        // 1. Destroy all cached tile entities from all maps
        foreach (KeyValuePair<GameMapId, List<Entity>> kvp in _mapTileCache.ToList())
        {
            GameMapId mapId = kvp.Key;
            List<Entity> tiles = kvp.Value;

            foreach (Entity tile in tiles)
            {
                if (world.IsAlive(tile))
                {
                    world.Destroy(tile);
                    destroyedCount++;
                }
            }

            logger?.LogDebug("Destroyed {Count} cached tiles for map {MapId}", tiles.Count, mapId.Value);
        }

        _mapTileCache.Clear();

        // 2. Destroy all entities with Position (NPCs, warps, etc.) - explicitly excludes player
        var dynamicEntities = new List<Entity>();
        QueryDescription positionQuery = QueryCache.Get<Position>();
        world.Query(positionQuery, (Entity entity, ref Position pos) =>
        {
            // Only destroy entities that have a MapId AND are not the player
            if (pos.MapId != null && !entity.Has<Player>())
            {
                dynamicEntities.Add(entity);
            }
        });

        foreach (Entity entity in dynamicEntities)
        {
            if (world.IsAlive(entity))
            {
                // Clean up behavior scripts BEFORE destroying entity
                if (_npcBehaviorSystem != null && entity.Has<Behavior>())
                {
                    _npcBehaviorSystem.CleanupEntityBehavior(entity);
                    behaviorsCleanedUp++;
                }

                world.Destroy(entity);
                destroyedCount++;
            }
        }

        // 3. Destroy all MapInfo entities
        var mapInfoEntities = new List<Entity>();
        QueryDescription mapInfoQuery = QueryCache.Get<MapInfo>();
        world.Query(mapInfoQuery, entity =>
        {
            mapInfoEntities.Add(entity);
        });

        foreach (Entity entity in mapInfoEntities)
        {
            if (world.IsAlive(entity))
            {
                // Clean up behavior scripts BEFORE destroying entity
                if (_npcBehaviorSystem != null && entity.Has<Behavior>())
                {
                    _npcBehaviorSystem.CleanupEntityBehavior(entity);
                    behaviorsCleanedUp++;
                }

                world.Destroy(entity);
                destroyedCount++;
            }
        }

        logger?.LogInformation(
            "Destroyed {Count} map entities during full cleanup (behaviors: {Behaviors})",
            destroyedCount,
            behaviorsCleanedUp
        );
        return destroyedCount;
    }

    private record MapMetadata(
        string Name,
        HashSet<string> TilesetTextureIds,
        HashSet<string> SpriteTextureIds
    );
}
