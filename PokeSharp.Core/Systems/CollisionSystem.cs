using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Extensions.Logging;
using PokeSharp.Core.Components.Movement;
using PokeSharp.Core.Components.Tiles;

namespace PokeSharp.Core.Systems;

/// <summary>
///     System that provides tile-based collision detection for grid movement.
///     Uses spatial hash to query entities with Collision components.
/// </summary>
public class CollisionSystem(ILogger<CollisionSystem>? logger = null) : BaseSystem
{
    private readonly ILogger<CollisionSystem>? _logger = logger;
    private SpatialHashSystem? _spatialHashSystem;

    /// <inheritdoc />
    public override int Priority => SystemPriority.Collision;

    /// <inheritdoc />
    public override void Update(World world, float deltaTime)
    {
        // Collision system doesn't require per-frame updates
        // It provides on-demand collision checking via IsPositionWalkable
        EnsureInitialized();
    }

    /// <summary>
    ///     Sets the spatial hash system for entity lookups.
    ///     Should be called during system initialization.
    /// </summary>
    /// <param name="spatialHashSystem">The spatial hash system instance.</param>
    public void SetSpatialHashSystem(SpatialHashSystem spatialHashSystem)
    {
        _spatialHashSystem = spatialHashSystem;
        _logger?.LogDebug("SpatialHashSystem connected to CollisionSystem");
    }

    /// <summary>
    ///     Checks if a tile position is walkable (not blocked by collision).
    /// </summary>
    /// <param name="spatialHash">The spatial hash system for entity lookups.</param>
    /// <param name="mapId">The map identifier.</param>
    /// <param name="tileX">The X coordinate in tile space.</param>
    /// <param name="tileY">The Y coordinate in tile space.</param>
    /// <returns>True if the position is walkable, false if blocked.</returns>
    public static bool IsPositionWalkable(
        SpatialHashSystem spatialHash,
        int mapId,
        int tileX,
        int tileY
    )
    {
        return IsPositionWalkable(spatialHash, mapId, tileX, tileY, Direction.None);
    }

    /// <summary>
    ///     Checks if a tile position is walkable from a specific direction.
    ///     Queries spatial hash for entities with Collision components.
    ///     Supports Pokemon-style directional blocking (ledges).
    /// </summary>
    /// <param name="spatialHash">The spatial hash system for entity lookups.</param>
    /// <param name="mapId">The map identifier.</param>
    /// <param name="tileX">The X coordinate in tile space.</param>
    /// <param name="tileY">The Y coordinate in tile space.</param>
    /// <param name="fromDirection">Direction moving FROM (player's movement direction).</param>
    /// <returns>True if the position is walkable from this direction, false if blocked.</returns>
    public static bool IsPositionWalkable(
        SpatialHashSystem spatialHash,
        int mapId,
        int tileX,
        int tileY,
        Direction fromDirection
    )
    {
        if (spatialHash == null)
            return false;

        // Get all entities at this position from spatial hash
        var entities = spatialHash.GetEntitiesAt(mapId, tileX, tileY);

        foreach (var entity in entities)
            // Check if entity has Collision component
            if (entity.Has<Collision>())
            {
                ref var collision = ref entity.Get<Collision>();

                if (collision.IsSolid)
                {
                    // Check ledge logic if entity has TileLedge component
                    if (entity.Has<TileLedge>() && fromDirection != Direction.None)
                    {
                        ref var ledge = ref entity.Get<TileLedge>();

                        // Check if ledge blocks this specific direction
                        if (ledge.IsBlockedFrom(fromDirection))
                            return false; // Ledge blocks this direction (can't climb up)
                        // Ledge allows this direction (can jump down) - continue checking other entities
                        // Don't return true yet, there might be other blocking entities
                    }
                    else
                    {
                        // Solid collision without ledge - always blocks
                        return false;
                    }
                }
            }

        // No blocking collisions found
        return true;
    }

    /// <summary>
    ///     Instance method that uses the system's spatial hash.
    ///     Legacy compatibility wrapper.
    /// </summary>
    public bool IsPositionWalkableInstance(
        int mapId,
        int tileX,
        int tileY,
        Direction fromDirection = Direction.None
    )
    {
        if (_spatialHashSystem == null)
            throw new InvalidOperationException(
                "SpatialHashSystem not initialized. Call SetSpatialHashSystem() first."
            );

        return IsPositionWalkable(_spatialHashSystem, mapId, tileX, tileY, fromDirection);
    }

    /// <summary>
    ///     Checks if a tile is a Pokemon-style ledge (has TileLedge component).
    /// </summary>
    /// <param name="spatialHash">The spatial hash system for entity lookups.</param>
    /// <param name="mapId">The map identifier.</param>
    /// <param name="tileX">The X coordinate in tile space.</param>
    /// <param name="tileY">The Y coordinate in tile space.</param>
    /// <returns>True if the tile is a ledge, false otherwise.</returns>
    public static bool IsLedge(SpatialHashSystem spatialHash, int mapId, int tileX, int tileY)
    {
        if (spatialHash == null)
            return false;

        var entities = spatialHash.GetEntitiesAt(mapId, tileX, tileY);

        foreach (var entity in entities)
            if (entity.Has<TileLedge>())
                return true;

        return false;
    }

    /// <summary>
    ///     Gets the allowed jump direction for a ledge tile.
    /// </summary>
    /// <param name="spatialHash">The spatial hash system for entity lookups.</param>
    /// <param name="mapId">The map identifier.</param>
    /// <param name="tileX">The X coordinate in tile space.</param>
    /// <param name="tileY">The Y coordinate in tile space.</param>
    /// <returns>The direction you can jump across this ledge, or None if not a ledge.</returns>
    public static Direction GetLedgeJumpDirection(
        SpatialHashSystem spatialHash,
        int mapId,
        int tileX,
        int tileY
    )
    {
        if (spatialHash == null)
            return Direction.None;

        var entities = spatialHash.GetEntitiesAt(mapId, tileX, tileY);

        foreach (var entity in entities)
            if (entity.Has<TileLedge>())
            {
                ref var ledge = ref entity.Get<TileLedge>();
                return ledge.JumpDirection;
            }

        return Direction.None;
    }
}