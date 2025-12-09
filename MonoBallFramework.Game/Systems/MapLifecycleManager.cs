using Arch.Core;
using Arch.Core.Extensions;
using Arch.Relationships;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Engine.Common.Logging;
using MonoBallFramework.Game.Engine.Core.Events;
using MonoBallFramework.Game.Engine.Core.Events.Map;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.Engine.Rendering.Assets;
using MonoBallFramework.Game.Engine.Systems.Management;
using MonoBallFramework.Game.Engine.Systems.Pooling;
using MonoBallFramework.Game.Components;
using MonoBallFramework.Game.Ecs.Components;
using MonoBallFramework.Game.Ecs.Components.Maps;
using MonoBallFramework.Game.Ecs.Components.Movement;
using MonoBallFramework.Game.Ecs.Components.Relationships;
using MonoBallFramework.Game.Ecs.Components.Rendering;
using MonoBallFramework.Game.Ecs.Components.NPCs;
using MonoBallFramework.Game.Ecs.Components.Tiles;
using MonoBallFramework.Game.GameSystems.Spatial;
using MonoBallFramework.Game.Scripting.Systems;
using MonoBallFramework.Game.Systems.Rendering;

namespace MonoBallFramework.Game.Systems;

/// <summary>
///     Manages map lifecycle: loading, unloading, and memory cleanup.
///     Ensures only active maps remain in memory to prevent entity/texture accumulation.
///     Supports entity pooling for tile entities to reduce GC pressure.
/// </summary>
public class MapLifecycleManager(
    World world,
    IAssetProvider assetProvider,
    SpriteTextureLoader spriteTextureLoader,
    SpatialHashSystem spatialHashSystem,
    IEventBus? eventBus = null,
    EntityPoolManager? poolManager = null,
    ILogger<MapLifecycleManager>? logger = null
)
{
    private readonly Dictionary<string, MapMetadata> _loadedMaps = new();
    private GameMapId? _currentMapId;
    private GameMapId? _previousMapId;
    private NPCBehaviorSystem? _npcBehaviorSystem;

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
    ///     Gets the current active map ID
    /// </summary>
    public GameMapId? CurrentMapId => _currentMapId;

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
        if (_currentMapId != null && _currentMapId.Value == newMapId.Value)
        {
            logger?.LogDebug("Already on map {MapId}, skipping transition", newMapId.Value);
            return;
        }

        GameMapId? oldMapId = _currentMapId;
        _previousMapId = _currentMapId;
        _currentMapId = newMapId;

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
                (_currentMapId == null || id != _currentMapId.Value) &&
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
        int spritesUnloaded = UnloadSpriteTextures(mapId, metadata.SpriteTextureIds);

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
    ///     Destroys or releases all entities belonging to a specific map.
    ///     Uses BelongsToMap relationship for unified entity collection.
    ///     Pooled tile entities are released back to pool for reuse.
    /// </summary>
    private int DestroyMapEntities(GameMapId mapId)
    {
        // CRITICAL FIX: Collect entities first, then process (can't modify during query)
        var pooledEntities = new List<Entity>();
        var entitiesToDestroy = new List<Entity>();

        // 1. Find the MapInfo entity for this map
        Entity? mapInfoEntity = null;
        QueryDescription mapInfoQuery = QueryCache.Get<MapInfo>();
        world.Query(
            mapInfoQuery,
            (Entity entity, ref MapInfo info) =>
            {
                if (info.MapId == mapId)
                {
                    mapInfoEntity = entity;
                    entitiesToDestroy.Add(entity);
                }
            }
        );

        // Debug: Check poolManager status
        if (poolManager == null)
        {
            logger?.LogWarning(
                "PoolManager is NULL - all entities will be destroyed instead of released to pool!"
            );
        }

        // 2. If we found the map entity, iterate its children using ParentOf relationships
        if (
            mapInfoEntity.HasValue
            && world.IsAlive(mapInfoEntity.Value)
            && mapInfoEntity.Value.HasRelationship<ParentOf>()
        )
        {
            ref Relationship<ParentOf> mapChildren =
                ref mapInfoEntity.Value.GetRelationships<ParentOf>();
            foreach (KeyValuePair<Entity, ParentOf> kvp in mapChildren)
            {
                Entity childEntity = kvp.Key;
                if (!world.IsAlive(childEntity))
                {
                    continue;
                }

                // Separate pooled tile entities from non-pooled for reuse
                if (poolManager != null && childEntity.Has<Pooled>())
                {
                    pooledEntities.Add(childEntity);
                }
                else
                {
                    entitiesToDestroy.Add(childEntity);
                }
            }
        }

        logger?.LogInformation(
            "Collected entities for map {MapId}: {PooledCount} pooled, {DestroyCount} to destroy",
            mapId.Value,
            pooledEntities.Count,
            entitiesToDestroy.Count
        );

        // Release pooled entities back to pool for reuse
        int releasedCount = 0;
        foreach (Entity entity in pooledEntities)
        {
            try
            {
                // CRITICAL: Remove ParentOf relationship BEFORE releasing to pool
                // The automatic cleanup only happens when the parent is destroyed,
                // but we're releasing children to pool BEFORE destroying the parent!
                if (mapInfoEntity.HasValue && world.IsAlive(mapInfoEntity.Value))
                {
                    mapInfoEntity.Value.RemoveRelationship<ParentOf>(entity);
                }

                // Strip tile-specific components before releasing
                StripTileComponents(entity);
                poolManager!.Release(entity);
                releasedCount++;
            }
            catch (Exception ex)
            {
                // If release fails, destroy the entity instead
                logger?.LogWarning(
                    ex,
                    "Failed to release entity {EntityId} to pool, destroying",
                    entity.Id
                );
                world.Destroy(entity);
            }
        }

        // Destroy non-pooled entities
        int behaviorsCleanedUp = 0;
        foreach (Entity entity in entitiesToDestroy)
        {
            if (world.IsAlive(entity))
            {
                // CRITICAL: Clean up behavior scripts BEFORE destroying entity
                // This ensures event subscriptions are disposed to prevent AccessViolationException
                if (_npcBehaviorSystem != null && entity.Has<Behavior>())
                {
                    _npcBehaviorSystem.CleanupEntityBehavior(entity);
                    behaviorsCleanedUp++;
                }

                world.Destroy(entity);
            }
        }

        int totalProcessed = releasedCount + entitiesToDestroy.Count;
        logger?.LogInformation(
            "Processed {Count} entities for map {MapId} (released: {Released}, destroyed: {Destroyed}, behaviors: {Behaviors})",
            totalProcessed,
            mapId.Value,
            releasedCount,
            entitiesToDestroy.Count,
            behaviorsCleanedUp
        );

        return totalProcessed;
    }

    /// <summary>
    ///     Strips tile-specific components from an entity before releasing to pool.
    ///     This ensures clean reuse without stale component data.
    ///     CRITICAL: Elevation, AnimatedTile, and BelongsToMap MUST be removed to prevent
    ///     rendering corruption and stale relationships when tiles are reused.
    /// </summary>
    private void StripTileComponents(Entity entity)
    {
        // CRITICAL: Remove Elevation first - this makes the tile invisible to the renderer!
        // The renderer queries for TilePosition + TileSprite + Elevation, so removing
        // Elevation prevents pooled tiles from being rendered at stale positions.
        // Without this, pooled tiles with old MapIds render at (0,0) causing visual overlap.
        if (entity.Has<Elevation>())
        {
            world.Remove<Elevation>(entity);
        }

        // CRITICAL: Remove AnimatedTile - this component contains tileset-specific
        // data (FrameSourceRects, TilesetFirstGid) that becomes invalid when reused
        // for a different map. Failure to remove this causes rendering corruption.
        if (entity.Has<AnimatedTile>())
        {
            world.Remove<AnimatedTile>(entity);
        }

        // Note: ParentOf relationship is now manually removed in DestroyMapEntities BEFORE
        // releasing to pool (see line ~190). It must be removed explicitly because tiles
        // are released to pool BEFORE the parent map entity is destroyed.

        // Remove optional tile components that may have been added
        // Keep TilePosition, TileSprite - they'll be overwritten on reuse
        // Remove variable components that may not be present on next use

        if (entity.Has<LayerOffset>())
        {
            world.Remove<LayerOffset>(entity);
        }

        if (entity.Has<TerrainType>())
        {
            world.Remove<TerrainType>(entity);
        }

        if (entity.Has<TileScript>())
        {
            world.Remove<TileScript>(entity);
        }

        // Remove collision component if present (added via property mappers)
        if (entity.Has<Collision>())
        {
            world.Remove<Collision>(entity);
        }
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
    private int UnloadSpriteTextures(GameMapId mapId, HashSet<string> spriteTextureKeys)
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
            .Where(id => _currentMapId == null || id != _currentMapId.Value)
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

        // Reset map tracking state
        _currentMapId = null;
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
    ///     Destroys ALL map-related entities in the world, regardless of registration status.
    ///     Uses BelongsToMap relationship for unified cleanup.
    ///     Used for complete world cleanup during warp transitions.
    ///     IMPORTANT: Pooled entities are released back to pool, not destroyed.
    /// </summary>
    private int DestroyAllMapEntities()
    {
        var pooledEntities = new List<(Entity parent, Entity child)>();
        var entitiesToDestroy = new List<Entity>();

        // 1. Collect ALL MapInfo entities and their children
        QueryDescription mapInfoQuery = QueryCache.Get<MapInfo>();
        world.Query(
            mapInfoQuery,
            entity =>
            {
                // Add the map entity itself (MapInfo entities are never pooled)
                entitiesToDestroy.Add(entity);

                // If it has children, collect them all
                if (entity.HasRelationship<ParentOf>())
                {
                    ref Relationship<ParentOf> mapChildren =
                        ref entity.GetRelationships<ParentOf>();
                    foreach (KeyValuePair<Entity, ParentOf> kvp in mapChildren)
                    {
                        Entity childEntity = kvp.Key;
                        if (world.IsAlive(childEntity))
                        {
                            // Separate pooled entities from non-pooled for reuse
                            if (poolManager != null && childEntity.Has<Pooled>())
                            {
                                // Store both parent and child for relationship cleanup
                                pooledEntities.Add((entity, childEntity));
                            }
                            else
                            {
                                entitiesToDestroy.Add(childEntity);
                            }
                        }
                    }
                }
            }
        );

        logger?.LogInformation(
            "Collected ALL map entities: {PooledCount} pooled, {DestroyCount} to destroy",
            pooledEntities.Count,
            entitiesToDestroy.Count
        );

        // Release pooled entities back to pool for reuse
        int releasedCount = 0;
        foreach ((Entity parentEntity, Entity childEntity) in pooledEntities)
        {
            try
            {
                // CRITICAL: Remove ParentOf relationship BEFORE releasing to pool
                // The automatic cleanup only happens when the parent is destroyed,
                // but we're releasing children to pool BEFORE destroying the parent!
                if (world.IsAlive(parentEntity))
                {
                    parentEntity.RemoveRelationship<ParentOf>(childEntity);
                }

                // Strip tile-specific components before releasing
                StripTileComponents(childEntity);
                poolManager!.Release(childEntity);
                releasedCount++;
            }
            catch (Exception ex)
            {
                // If release fails, destroy the entity instead
                logger?.LogWarning(
                    ex,
                    "Failed to release entity {EntityId} to pool during full cleanup, destroying",
                    childEntity.Id
                );
                world.Destroy(childEntity);
            }
        }

        // Destroy non-pooled entities
        int behaviorsCleanedUp = 0;
        foreach (Entity entity in entitiesToDestroy)
        {
            if (world.IsAlive(entity))
            {
                // CRITICAL: Clean up behavior scripts BEFORE destroying entity
                // This ensures event subscriptions are disposed to prevent AccessViolationException
                if (_npcBehaviorSystem != null && entity.Has<Behavior>())
                {
                    _npcBehaviorSystem.CleanupEntityBehavior(entity);
                    behaviorsCleanedUp++;
                }

                world.Destroy(entity);
            }
        }

        int totalProcessed = releasedCount + entitiesToDestroy.Count;
        logger?.LogInformation(
            "Destroyed {Count} map entities during full cleanup (released: {Released}, destroyed: {Destroyed}, behaviors: {Behaviors})",
            totalProcessed,
            releasedCount,
            entitiesToDestroy.Count,
            behaviorsCleanedUp
        );
        return totalProcessed;
    }

    private record MapMetadata(
        string Name,
        HashSet<string> TilesetTextureIds,
        HashSet<string> SpriteTextureIds
    );
}
