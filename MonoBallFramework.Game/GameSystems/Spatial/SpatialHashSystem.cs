using Arch.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using MonoBallFramework.Game.Ecs.Components.Movement;
using MonoBallFramework.Game.Ecs.Components.Tiles;
using MonoBallFramework.Game.Engine.Common.Logging;
using MonoBallFramework.Game.Engine.Common.Utilities;
using MonoBallFramework.Game.Engine.Core.Systems;
using MonoBallFramework.Game.Engine.Core.Systems.Base;
using MonoBallFramework.Game.Engine.Core.Types;
using EcsQueries = MonoBallFramework.Game.Engine.Systems.Queries.Queries;

namespace MonoBallFramework.Game.GameSystems.Spatial;

/// <summary>
///     System that builds and maintains a spatial hash for efficient entity lookups by position.
///     Runs very early (Priority: 25) to ensure spatial data is available for other systems.
///     Uses dirty tracking to avoid rebuilding index for static tiles every frame.
/// </summary>
public class SpatialHashSystem(ILogger<SpatialHashSystem>? logger = null)
    : SystemBase,
        IUpdateSystem,
        ISpatialQuery
{
    private readonly SpatialHash _dynamicHash = new(); // For entities with Position (cleared each frame)
    private readonly ILogger<SpatialHashSystem>? _logger = logger;
    private readonly List<Entity> _queryResultBuffer = new(128); // Pooled buffer for query results
    private readonly SpatialHash _staticHash = new(); // For tiles (indexed once)

    private readonly List<Entity>
        _staticQueryBuffer = new(512); // Separate buffer for static-only queries (tile rendering)

    private bool _staticTilesIndexed;

    /// <summary>
    ///     Gets the update priority. Lower values execute first.
    ///     Spatial hash executes at priority 25, after input (0) and before NPC behavior (75).
    /// </summary>
    public int UpdatePriority => SystemPriority.SpatialHash;

    /// <summary>
    ///     Gets all entities at the specified tile position.
    /// </summary>
    /// <param name="mapId">The map identifier.</param>
    /// <param name="x">The X tile coordinate.</param>
    /// <param name="y">The Y tile coordinate.</param>
    /// <returns>Collection of entities at this position.</returns>
    public IReadOnlyList<Entity> GetEntitiesAt(GameMapId mapId, int x, int y)
    {
        _queryResultBuffer.Clear();

        // Add static entities
        foreach (Entity entity in _staticHash.GetAt(mapId, x, y))
        {
            _queryResultBuffer.Add(entity);
        }

        // Add dynamic entities
        foreach (Entity entity in _dynamicHash.GetAt(mapId, x, y))
        {
            _queryResultBuffer.Add(entity);
        }

        return _queryResultBuffer;
    }

    /// <summary>
    ///     Gets all entities within the specified bounds.
    /// </summary>
    /// <param name="mapId">The map identifier.</param>
    /// <param name="bounds">The bounding rectangle in tile coordinates.</param>
    /// <returns>Collection of entities within the bounds.</returns>
    public IReadOnlyList<Entity> GetEntitiesInBounds(GameMapId mapId, Rectangle bounds)
    {
        _queryResultBuffer.Clear();

        // PERFORMANCE: Use non-allocating overload that fills our buffer directly
        _staticHash.GetInBounds(mapId, bounds, _queryResultBuffer);
        _dynamicHash.GetInBounds(mapId, bounds, _queryResultBuffer);

        return _queryResultBuffer;
    }

    /// <summary>
    ///     Gets only static tile entities within the specified bounds.
    ///     Does NOT include dynamic entities (NPCs, player, etc.).
    ///     Optimized for tile rendering - uses separate buffer to avoid conflicts.
    /// </summary>
    /// <param name="mapId">The map identifier.</param>
    /// <param name="bounds">The bounding rectangle in local tile coordinates.</param>
    /// <returns>Collection of static tile entities within the bounds.</returns>
    public IReadOnlyList<Entity> GetStaticEntitiesInBounds(GameMapId mapId, Rectangle bounds)
    {
        _staticQueryBuffer.Clear();

        // PERFORMANCE: Use non-allocating overload - eliminates iterator state machine allocation
        _staticHash.GetInBounds(mapId, bounds, _staticQueryBuffer);

        return _staticQueryBuffer;
    }

    /// <inheritdoc />
    public override int Priority => SystemPriority.SpatialHash;

    /// <inheritdoc />
    public override void Update(World world, float deltaTime)
    {
        EnsureInitialized();

        // Index static tiles once
        if (!_staticTilesIndexed)
        {
            _staticHash.Clear();
            int staticTileCount = 0;

            // Use centralized query for all tile-positioned entities
            world.Query(
                in EcsQueries.AllTilePositioned,
                (Entity entity, ref TilePosition pos) =>
                {
                    // Skip entities without a valid map ID
                    if (pos.MapId != null)
                    {
                        _staticHash.Add(entity, pos.MapId, pos.X, pos.Y);
                        staticTileCount++;
                    }
                }
            );

            _staticTilesIndexed = true;
            _logger?.LogSpatialHashIndexed(staticTileCount);
        }

        // Clear and re-index all dynamic entities each frame
        _dynamicHash.Clear();

        // Use centralized query for all positioned entities
        world.Query(
            in EcsQueries.AllPositioned,
            (Entity entity, ref Position pos) =>
            {
                // Skip entities without a valid map ID
                if (pos.MapId != null)
                {
                    _dynamicHash.Add(entity, pos.MapId, pos.X, pos.Y);
                }
            }
        );
    }

    /// <summary>
    ///     Forces a full rebuild of the spatial hash on the next update.
    ///     Call this when maps are loaded/unloaded or tiles are added/removed.
    /// </summary>
    public void InvalidateStaticTiles()
    {
        _staticTilesIndexed = false;
        _logger?.LogDebug("Static tiles invalidated, will rebuild spatial hash on next update");
    }

    /// <summary>
    ///     Adds tiles from a newly loaded map to the spatial hash without full rebuild.
    ///     This is an O(n) operation where n = number of tiles in the new map, much faster
    ///     than InvalidateStaticTiles() which queries ALL tiles in the world.
    /// </summary>
    /// <param name="mapId">The map identifier for the loaded map.</param>
    /// <param name="tiles">List of tile entities to add to the spatial hash.</param>
    public void AddMapTiles(GameMapId mapId, IReadOnlyList<Entity> tiles)
    {
        int tilesAdded = 0;

        foreach (Entity tile in tiles)
        {
            // Get the TilePosition component from the entity
            if (World?.TryGet(tile, out TilePosition pos) == true && pos.MapId != null)
            {
                _staticHash.Add(tile, pos.MapId, pos.X, pos.Y);
                tilesAdded++;
            }
        }

        _logger?.LogDebug(
            "Incrementally added {TilesAdded} tiles for map {MapId} to spatial hash",
            tilesAdded,
            mapId
        );
    }

    /// <summary>
    ///     Removes all tiles belonging to a specific map from the spatial hash.
    ///     This is an O(1) operation for map removal, much faster than full rebuild.
    /// </summary>
    /// <param name="mapId">The map identifier whose tiles should be removed.</param>
    public void RemoveMapTiles(GameMapId mapId)
    {
        bool removed = _staticHash.RemoveMap(mapId);

        if (removed)
        {
            _logger?.LogDebug("Removed all tiles for map {MapId} from spatial hash", mapId);
        }
        else
        {
            _logger?.LogDebug("Map {MapId} not found in spatial hash (already unloaded?)", mapId);
        }
    }

    /// <summary>
    ///     Gets diagnostic information about the spatial hash.
    /// </summary>
    /// <returns>A tuple with (entity count, occupied position count).</returns>
    public (int entityCount, int occupiedPositions) GetDiagnostics()
    {
        int staticCount = _staticHash.GetEntityCount();
        int dynamicCount = _dynamicHash.GetEntityCount();
        int staticPositions = _staticHash.GetOccupiedPositionCount();
        int dynamicPositions = _dynamicHash.GetOccupiedPositionCount();
        return (staticCount + dynamicCount, staticPositions + dynamicPositions);
    }
}
