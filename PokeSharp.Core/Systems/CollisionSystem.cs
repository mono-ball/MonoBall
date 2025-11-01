using Arch.Core;
using Arch.Core.Extensions;
using PokeSharp.Core.Components;

namespace PokeSharp.Core.Systems;

/// <summary>
/// System that provides tile-based collision detection for grid movement.
/// Checks TileCollider components to determine if positions are walkable.
/// </summary>
public class CollisionSystem : BaseSystem
{
    /// <inheritdoc/>
    public override int Priority => SystemPriority.Collision;

    /// <inheritdoc/>
    public override void Update(World world, float deltaTime)
    {
        // Collision system doesn't require per-frame updates
        // It provides on-demand collision checking via IsPositionWalkable
        EnsureInitialized();
    }

    /// <summary>
    /// Checks if a tile position is walkable (not blocked by collision).
    /// </summary>
    /// <param name="world">The game world to query.</param>
    /// <param name="tileX">The X coordinate in tile space.</param>
    /// <param name="tileY">The Y coordinate in tile space.</param>
    /// <returns>True if the position is walkable, false if blocked or no collision data exists.</returns>
    public static bool IsPositionWalkable(World world, int tileX, int tileY)
    {
        return IsPositionWalkable(world, tileX, tileY, Direction.None);
    }

    /// <summary>
    /// Checks if a tile position is walkable from a specific direction.
    /// Supports Pokemon-style directional blocking (ledges).
    /// </summary>
    /// <param name="world">The game world to query.</param>
    /// <param name="tileX">The X coordinate in tile space.</param>
    /// <param name="tileY">The Y coordinate in tile space.</param>
    /// <param name="fromDirection">Direction moving FROM (player's movement direction).</param>
    /// <returns>True if the position is walkable from this direction, false if blocked.</returns>
    public static bool IsPositionWalkable(World world, int tileX, int tileY, Direction fromDirection)
    {
        if (world == null)
        {
            return false;
        }

        // Query for entities with both TileMap and TileCollider components
        var query = new QueryDescription().WithAll<TileMap, TileCollider>();

        bool isWalkable = true; // Default to walkable if no collision data found

        world.Query(in query, (Entity entity, ref TileMap tileMap, ref TileCollider collider) =>
        {
            // Check if the position is within map bounds
            if (tileX < 0 || tileY < 0 || tileX >= tileMap.Width || tileY >= tileMap.Height)
            {
                isWalkable = false;
                return;
            }

            // Check standard solid collision first
            if (collider.IsSolid(tileX, tileY))
            {
                isWalkable = false;
                return;
            }

            // Check directional blocking (for ledges)
            // If moving in a specific direction, check if that direction is blocked
            if (fromDirection != Direction.None && collider.IsBlockedFromDirection(tileX, tileY, fromDirection))
            {
                isWalkable = false;
            }
        });

        return isWalkable;
    }

    /// <summary>
    /// Checks if a tile is a Pokemon-style ledge.
    /// </summary>
    /// <param name="world">The game world to query.</param>
    /// <param name="tileX">The X coordinate in tile space.</param>
    /// <param name="tileY">The Y coordinate in tile space.</param>
    /// <returns>True if the tile is a ledge, false otherwise.</returns>
    public static bool IsLedge(World world, int tileX, int tileY)
    {
        if (world == null)
        {
            return false;
        }

        var query = new QueryDescription().WithAll<TileMap, TileCollider>();
        bool ledgeFound = false;

        world.Query(in query, (Entity entity, ref TileMap tileMap, ref TileCollider collider) =>
        {
            if (collider.IsLedge(tileX, tileY))
            {
                ledgeFound = true;
            }
        });

        return ledgeFound;
    }

    /// <summary>
    /// Gets the allowed jump direction for a ledge tile.
    /// </summary>
    /// <param name="world">The game world to query.</param>
    /// <param name="tileX">The X coordinate in tile space.</param>
    /// <param name="tileY">The Y coordinate in tile space.</param>
    /// <returns>The direction you can jump across this ledge, or None if not a ledge.</returns>
    public static Direction GetLedgeJumpDirection(World world, int tileX, int tileY)
    {
        if (world == null)
        {
            return Direction.None;
        }

        var query = new QueryDescription().WithAll<TileMap, TileCollider>();
        Direction jumpDirection = Direction.None;

        world.Query(in query, (Entity entity, ref TileMap tileMap, ref TileCollider collider) =>
        {
            jumpDirection = collider.GetLedgeJumpDirection(tileX, tileY);
        });

        return jumpDirection;
    }
}
