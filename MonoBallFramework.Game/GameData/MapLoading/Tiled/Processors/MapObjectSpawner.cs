using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Ecs.Components.Maps;
using MonoBallFramework.Game.Engine.Common.Logging;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.GameData.MapLoading.Tiled.Spawners;
using MonoBallFramework.Game.GameData.MapLoading.Tiled.Tmx;

namespace MonoBallFramework.Game.GameData.MapLoading.Tiled.Processors;

/// <summary>
///     Handles spawning of map objects (NPCs, items, triggers, etc.) from Tiled object layers.
///     Delegates to specialized IEntitySpawner implementations via EntitySpawnerRegistry.
///
///     Design principles:
///     - Single Responsibility: This class only iterates objects and delegates to spawners
///     - Fail-fast: Unrecognized objects are optionally logged but don't crash map loading
///     - Open/Closed: Add new object types by registering new spawners, not modifying this class
/// </summary>
public sealed class MapObjectSpawner
{
    private readonly EntitySpawnerRegistry _spawnerRegistry;
    private readonly ILogger<MapObjectSpawner>? _logger;

    public MapObjectSpawner(
        EntitySpawnerRegistry spawnerRegistry,
        ILogger<MapObjectSpawner>? logger = null)
    {
        _spawnerRegistry = spawnerRegistry;
        _logger = logger;
    }

    /// <summary>
    ///     Spawns entities from map objects (NPCs, items, triggers, etc.).
    ///     Each object is delegated to the appropriate spawner via the registry.
    /// </summary>
    /// <param name="world">The ECS world.</param>
    /// <param name="tmxDoc">The Tiled map document.</param>
    /// <param name="mapInfoEntity">The map info entity for establishing relationships.</param>
    /// <param name="mapId">The game map ID.</param>
    /// <param name="tileWidth">Tile width for X coordinate conversion.</param>
    /// <param name="tileHeight">Tile height for Y coordinate conversion.</param>
    /// <param name="requiredSpriteIds">Collection to track sprite IDs for lazy loading.</param>
    /// <returns>Number of entities created from objects.</returns>
    public int SpawnMapObjects(
        World world,
        TmxDocument tmxDoc,
        Entity mapInfoEntity,
        GameMapId mapId,
        int tileWidth,
        int tileHeight,
        HashSet<GameSpriteId>? requiredSpriteIds = null)
    {
        int created = 0;
        int skipped = 0;

        foreach (TmxObjectGroup objectGroup in tmxDoc.ObjectGroups)
        {
            foreach (TmxObject obj in objectGroup.Objects)
            {
                // Create context for this object
                var context = new EntitySpawnContext
                {
                    World = world,
                    TiledObject = obj,
                    MapInfoEntity = mapInfoEntity,
                    MapId = mapId,
                    TileWidth = tileWidth,
                    TileHeight = tileHeight,
                    RequiredSpriteIds = requiredSpriteIds
                };

                // Try to spawn using registry
                if (_spawnerRegistry.TrySpawn(context, out Entity _))
                {
                    created++;
                }
                else
                {
                    // No spawner registered for this object type
                    // This is expected for decoration objects, tile objects, etc.
                    skipped++;
                    LogSkippedObject(obj);
                }
            }
        }

        // Log summary
        if (created > 0 || skipped > 0)
        {
            _logger?.LogDebug(
                "Map object spawning complete: {Created} entities created, {Skipped} objects skipped",
                created, skipped);
        }

        // Log warp count for diagnostics
        LogWarpCount(mapInfoEntity);

        return created;
    }

    private void LogSkippedObject(TmxObject obj)
    {
        // Only log if object has a type - typeless objects are expected to be skipped
        if (!string.IsNullOrEmpty(obj.Type))
        {
            _logger?.LogOperationSkipped(
                $"Object '{obj.Name}' (type: {obj.Type})",
                "no spawner registered");
        }
    }

    private void LogWarpCount(Entity mapInfoEntity)
    {
        if (!mapInfoEntity.Has<MapWarps>())
        {
            return;
        }

        MapWarps warps = mapInfoEntity.Get<MapWarps>();
        if (warps.Count > 0)
        {
            _logger?.LogDebug(
                "Map has {Count} warps registered in spatial index",
                warps.Count);
        }
    }
}
