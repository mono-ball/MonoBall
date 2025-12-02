// PROTOTYPE: EVENT-DRIVEN COLLISION SYSTEM
// Demonstrates event-driven collision detection with mod injection points.

using Arch.Core;
using PokeSharp.Engine.Events;
using PokeSharp.Game.Components.Interfaces;
using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Components.Tiles;
using PokeSharp.Game.Systems.Services;

namespace PokeSharp.Game.Systems.EventDriven;

/// <summary>
/// Event-driven collision service that fires events for mod injection.
/// Replaces direct collision queries with event-based checks.
/// </summary>
public class EventDrivenCollisionService : ICollisionService
{
    private readonly EventBus _events;
    private readonly ISpatialQuery _spatialQuery;
    private readonly ILogger? _logger;
    private ITileBehaviorSystem? _tileBehaviorSystem;
    private World? _world;

    public EventDrivenCollisionService(
        EventBus events,
        ISpatialQuery spatialQuery,
        ILogger? logger = null
    )
    {
        _events = events ?? throw new ArgumentNullException(nameof(events));
        _spatialQuery = spatialQuery ?? throw new ArgumentNullException(nameof(spatialQuery));
        _logger = logger;

        // Subscribe to collision events for internal processing
        _events.Subscribe<CollisionCheckEvent>(OnCollisionCheck, priority: 0);
    }

    /// <summary>
    /// Checks if a position is walkable using event-driven approach.
    /// Fires CollisionCheckEvent that mods can respond to.
    /// </summary>
    public bool IsPositionWalkable(
        int mapId,
        int tileX,
        int tileY,
        Direction fromDirection = Direction.None,
        byte entityElevation = Elevation.Default
    )
    {
        // Create collision check event
        var evt = new CollisionCheckEvent
        {
            Entity = Entity.Null, // Will be set by caller if available
            MapId = mapId,
            Position = (tileX, tileY),
            Timestamp = 0f, // Will be set by caller
            FromDirection = fromDirection,
            Elevation = entityElevation,
            IsWalkable = true, // Default: walkable
            IsCancelled = false
        };

        // Publish event - handlers can modify IsWalkable
        _events.Publish(ref evt);

        // If cancelled, treat as unwalkable
        if (evt.IsCancelled)
        {
            return false;
        }

        return evt.IsWalkable;
    }

    /// <summary>
    /// Gets comprehensive collision info in a single query.
    /// Fires multiple events for different collision aspects.
    /// </summary>
    public (bool isJumpTile, Direction allowedJumpDir, bool isWalkable) GetTileCollisionInfo(
        int mapId,
        int tileX,
        int tileY,
        byte entityElevation,
        Direction fromDirection
    )
    {
        // First check basic walkability
        var collisionEvt = new CollisionCheckEvent
        {
            Entity = Entity.Null,
            MapId = mapId,
            Position = (tileX, tileY),
            Timestamp = 0f,
            FromDirection = fromDirection,
            Elevation = entityElevation,
            IsWalkable = true
        };

        _events.Publish(ref collisionEvt);

        bool isWalkable = !collisionEvt.IsCancelled && collisionEvt.IsWalkable;

        // Check for jump behavior
        bool isJumpTile = false;
        Direction allowedJumpDir = Direction.None;

        if (_tileBehaviorSystem != null && _world != null)
        {
            var entities = _spatialQuery.GetEntitiesAt(mapId, tileX, tileY);

            foreach (Entity tileEntity in entities)
            {
                if (!tileEntity.Has<TileBehavior>()) continue;

                // Check elevation
                if (tileEntity.Has<Elevation>())
                {
                    var elevation = tileEntity.Get<Elevation>();
                    if (elevation.Value != entityElevation)
                        continue;
                }

                // Fire jump check event
                var jumpEvt = new JumpCheckEvent
                {
                    Entity = Entity.Null,
                    TileEntity = tileEntity,
                    Timestamp = 0f,
                    FromDirection = fromDirection.Opposite(),
                    JumpDirection = Direction.None,
                    JumpDistance = 2
                };

                _events.Publish(ref jumpEvt);

                if (jumpEvt.JumpDirection != Direction.None)
                {
                    isJumpTile = true;
                    allowedJumpDir = jumpEvt.JumpDirection;
                    break;
                }
            }
        }

        return (isJumpTile, allowedJumpDir, isWalkable);
    }

    /// <summary>
    /// Internal handler for collision checks.
    /// Performs default collision detection logic.
    /// Mods can override by subscribing with higher priority.
    /// </summary>
    private void OnCollisionCheck(ref CollisionCheckEvent evt)
    {
        // Get entities at position
        var entities = _spatialQuery.GetEntitiesAt(evt.MapId, evt.Position.X, evt.Position.Y);

        foreach (Entity entity in entities)
        {
            // Check elevation
            if (entity.Has<Elevation>())
            {
                var elevation = entity.Get<Elevation>();
                if (elevation.Value != evt.Elevation)
                    continue; // Different elevation, no collision
            }

            // Check tile behavior blocking
            if (_tileBehaviorSystem != null && _world != null && entity.Has<TileBehavior>())
            {
                Direction toDirection = evt.FromDirection != Direction.None
                    ? evt.FromDirection.Opposite()
                    : Direction.None;

                if (_tileBehaviorSystem.IsMovementBlocked(_world, entity, evt.FromDirection, toDirection))
                {
                    evt.IsWalkable = false;
                    evt.CancellationReason = "Blocked by tile behavior";

                    // Fire collision occurred event
                    PublishCollisionOccurred(evt.Entity, evt.MapId, evt.Position, entity, evt.FromDirection, CollisionType.Tile);
                    return;
                }
            }

            // Check solid collision
            if (entity.Has<Collision>())
            {
                var collision = entity.Get<Collision>();
                if (collision.IsSolid)
                {
                    evt.IsWalkable = false;
                    evt.CancellationReason = "Solid collision";

                    // Fire collision occurred event
                    PublishCollisionOccurred(evt.Entity, evt.MapId, evt.Position, entity, evt.FromDirection, CollisionType.Entity);
                    return;
                }
            }
        }
    }

    private void PublishCollisionOccurred(
        Entity entity,
        int mapId,
        (int X, int Y) position,
        Entity collidedWith,
        Direction direction,
        CollisionType type
    )
    {
        var evt = new CollisionOccurredEvent
        {
            Entity = entity,
            MapId = mapId,
            Position = position,
            Timestamp = 0f,
            CollidedWith = collidedWith,
            CollisionDirection = direction,
            Type = type
        };

        _events.Publish(ref evt);
    }

    public void SetWorld(World world)
    {
        _world = world;
    }

    public void SetTileBehaviorSystem(ITileBehaviorSystem tileBehaviorSystem)
    {
        _tileBehaviorSystem = tileBehaviorSystem;
    }
}

/// <summary>
/// Extension class for Direction enum (helper for event system).
/// </summary>
public static class DirectionExtensions
{
    public static Direction Opposite(this Direction direction)
    {
        return direction switch
        {
            Direction.North => Direction.South,
            Direction.South => Direction.North,
            Direction.East => Direction.West,
            Direction.West => Direction.East,
            _ => Direction.None
        };
    }
}
