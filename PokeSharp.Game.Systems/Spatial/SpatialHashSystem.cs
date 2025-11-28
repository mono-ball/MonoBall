using Arch.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using PokeSharp.Engine.Common.Logging;
using PokeSharp.Engine.Common.Utilities;
using PokeSharp.Engine.Core.Systems;
using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Components.Tiles;
using EcsQueries = PokeSharp.Engine.Systems.Queries.Queries;

namespace PokeSharp.Game.Systems;

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
    public IReadOnlyList<Entity> GetEntitiesAt(int mapId, int x, int y)
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
    public IReadOnlyList<Entity> GetEntitiesInBounds(int mapId, Rectangle bounds)
    {
        _queryResultBuffer.Clear();

        // Add static entities
        foreach (Entity entity in _staticHash.GetInBounds(mapId, bounds))
        {
            _queryResultBuffer.Add(entity);
        }

        // Add dynamic entities
        foreach (Entity entity in _dynamicHash.GetInBounds(mapId, bounds))
        {
            _queryResultBuffer.Add(entity);
        }

        return _queryResultBuffer;
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
                    _staticHash.Add(entity, pos.MapId, pos.X, pos.Y);
                    staticTileCount++;
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
                _dynamicHash.Add(entity, pos.MapId, pos.X, pos.Y);
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
