// EVENT SYSTEM TYPE DEFINITIONS
// This file contains the core event interfaces and common event types for the event-driven architecture.
// All events are designed to be zero-allocation value types where possible.

using Arch.Core;
using Microsoft.Xna.Framework;
using PokeSharp.Game.Components.Movement;

namespace PokeSharp.Engine.Events;

#region Core Event Interfaces

/// <summary>
/// Marker interface for all game events.
/// </summary>
public interface IGameEvent
{
    /// <summary>
    /// Timestamp when the event was created (game time).
    /// </summary>
    float Timestamp { get; }
}

/// <summary>
/// Base interface for ECS system events.
/// </summary>
public interface ISystemEvent : IGameEvent
{
    /// <summary>
    /// The entity associated with this event.
    /// </summary>
    Entity Entity { get; }
}

/// <summary>
/// Interface for events that can be cancelled/prevented.
/// </summary>
public interface ICancellableEvent : IGameEvent
{
    /// <summary>
    /// Whether this event has been cancelled by a handler.
    /// </summary>
    bool IsCancelled { get; set; }

    /// <summary>
    /// Reason for cancellation (optional, for debugging).
    /// </summary>
    string? CancellationReason { get; set; }
}

/// <summary>
/// Interface for movement-related events.
/// </summary>
public interface IMovementEvent : ISystemEvent
{
    /// <summary>
    /// Direction of movement.
    /// </summary>
    Direction Direction { get; }
}

/// <summary>
/// Interface for collision-related events.
/// </summary>
public interface ICollisionEvent : ISystemEvent
{
    /// <summary>
    /// Map ID where collision occurred.
    /// </summary>
    int MapId { get; }

    /// <summary>
    /// Tile coordinates of collision.
    /// </summary>
    (int X, int Y) Position { get; }
}

/// <summary>
/// Interface for tile behavior events.
/// </summary>
public interface ITileBehaviorEvent : ISystemEvent
{
    /// <summary>
    /// The tile entity that triggered the behavior.
    /// </summary>
    Entity TileEntity { get; }
}

/// <summary>
/// Interface for mod injection events (fires before main processing).
/// </summary>
public interface IPreEvent : IGameEvent
{
    /// <summary>
    /// Priority for mod handlers (higher = earlier execution).
    /// </summary>
    int Priority => 0;
}

/// <summary>
/// Interface for mod injection events (fires after main processing).
/// </summary>
public interface IPostEvent : IGameEvent
{
    /// <summary>
    /// Priority for mod handlers (higher = earlier execution).
    /// </summary>
    int Priority => 0;
}

#endregion

#region Movement Events

/// <summary>
/// Event fired when movement is requested (before validation).
/// Cancellable by handlers (e.g., for cutscenes, menus).
/// </summary>
public struct MovementRequestedEvent : IMovementEvent, ICancellableEvent, IPreEvent
{
    public Entity Entity { get; init; }
    public Direction Direction { get; init; }
    public float Timestamp { get; init; }
    public bool IsCancelled { get; set; }
    public string? CancellationReason { get; set; }

    /// <summary>
    /// Movement speed multiplier (1.0 = normal, 2.0 = running).
    /// Can be modified by handlers (e.g., for speed boosts, slow effects).
    /// </summary>
    public float SpeedMultiplier { get; set; }

    /// <summary>
    /// Whether this is a forced movement (ice, conveyor belt).
    /// </summary>
    public bool IsForcedMovement { get; init; }
}

/// <summary>
/// Event fired after movement validation, before starting actual movement.
/// Last chance to cancel or modify movement behavior.
/// </summary>
public struct MovementValidatedEvent : IMovementEvent, ICancellableEvent
{
    public Entity Entity { get; init; }
    public Direction Direction { get; init; }
    public float Timestamp { get; init; }
    public bool IsCancelled { get; set; }
    public string? CancellationReason { get; set; }

    /// <summary>
    /// Target grid position (where entity will move to).
    /// </summary>
    public (int X, int Y) TargetPosition { get; init; }

    /// <summary>
    /// Whether this movement is a jump (ledge).
    /// </summary>
    public bool IsJump { get; init; }

    /// <summary>
    /// Movement distance in tiles (1 = normal, 2 = jump).
    /// </summary>
    public int Distance { get; init; }
}

/// <summary>
/// Event fired when movement starts (after validation).
/// Non-cancellable (movement is committed).
/// </summary>
public struct MovementStartedEvent : IMovementEvent
{
    public Entity Entity { get; init; }
    public Direction Direction { get; init; }
    public float Timestamp { get; init; }

    /// <summary>
    /// Starting pixel position (for interpolation).
    /// </summary>
    public Vector2 StartPosition { get; init; }

    /// <summary>
    /// Target pixel position (for interpolation).
    /// </summary>
    public Vector2 TargetPosition { get; init; }

    /// <summary>
    /// Movement duration in seconds.
    /// </summary>
    public float Duration { get; init; }

    /// <summary>
    /// Whether this is a jump movement.
    /// </summary>
    public bool IsJump { get; init; }
}

/// <summary>
/// Event fired every frame during movement (for smooth interpolation updates).
/// </summary>
public struct MovementProgressEvent : IMovementEvent
{
    public Entity Entity { get; init; }
    public Direction Direction { get; init; }
    public float Timestamp { get; init; }

    /// <summary>
    /// Movement progress (0.0 to 1.0).
    /// </summary>
    public float Progress { get; init; }

    /// <summary>
    /// Current interpolated pixel position.
    /// </summary>
    public Vector2 CurrentPosition { get; init; }
}

/// <summary>
/// Event fired when movement completes successfully.
/// </summary>
public struct MovementCompletedEvent : IMovementEvent, IPostEvent
{
    public Entity Entity { get; init; }
    public Direction Direction { get; init; }
    public float Timestamp { get; init; }

    /// <summary>
    /// Final grid position after movement.
    /// </summary>
    public (int X, int Y) FinalPosition { get; init; }

    /// <summary>
    /// Map ID after movement (may have changed for warps).
    /// </summary>
    public int MapId { get; init; }

    /// <summary>
    /// Total movement time in seconds.
    /// </summary>
    public float TotalTime { get; init; }
}

/// <summary>
/// Event fired when movement is blocked (collision, locked, etc.).
/// </summary>
public struct MovementBlockedEvent : IMovementEvent, IPostEvent
{
    public Entity Entity { get; init; }
    public Direction Direction { get; init; }
    public float Timestamp { get; init; }

    /// <summary>
    /// Reason movement was blocked.
    /// </summary>
    public BlockReason Reason { get; init; }

    /// <summary>
    /// Optional entity that caused the block (e.g., NPC, tree).
    /// </summary>
    public Entity? BlockingEntity { get; init; }

    /// <summary>
    /// Position that was blocked.
    /// </summary>
    public (int X, int Y) BlockedPosition { get; init; }
}

/// <summary>
/// Reason why movement was blocked.
/// </summary>
public enum BlockReason
{
    Collision,      // Solid obstacle
    OutOfBounds,    // Map boundary
    Locked,         // Movement locked (cutscene, menu)
    Elevation,      // Wrong elevation
    Behavior,       // Tile behavior blocked
    Custom          // Mod-defined reason
}

/// <summary>
/// Event fired when entity changes direction without moving (turn in place).
/// </summary>
public struct DirectionChangedEvent : IMovementEvent
{
    public Entity Entity { get; init; }
    public Direction Direction { get; init; }
    public float Timestamp { get; init; }

    /// <summary>
    /// Previous facing direction.
    /// </summary>
    public Direction PreviousDirection { get; init; }
}

#endregion

#region Collision Events

/// <summary>
/// Event fired when checking if a position is walkable (before movement).
/// Handlers can mark position as unwalkable.
/// </summary>
public struct CollisionCheckEvent : ICollisionEvent, ICancellableEvent
{
    public Entity Entity { get; init; }
    public int MapId { get; init; }
    public (int X, int Y) Position { get; init; }
    public float Timestamp { get; init; }
    public bool IsCancelled { get; set; }
    public string? CancellationReason { get; set; }

    /// <summary>
    /// Direction of movement (for directional collision).
    /// </summary>
    public Direction FromDirection { get; init; }

    /// <summary>
    /// Entity elevation for collision filtering.
    /// </summary>
    public byte Elevation { get; init; }

    /// <summary>
    /// Whether position is walkable (set by handlers).
    /// Default: true. Set to false to block movement.
    /// </summary>
    public bool IsWalkable { get; set; }
}

/// <summary>
/// Event fired when entity collides with solid object.
/// </summary>
public struct CollisionOccurredEvent : ICollisionEvent, IPostEvent
{
    public Entity Entity { get; init; }
    public int MapId { get; init; }
    public (int X, int Y) Position { get; init; }
    public float Timestamp { get; init; }

    /// <summary>
    /// Entity that was collided with.
    /// </summary>
    public Entity CollidedWith { get; init; }

    /// <summary>
    /// Direction of collision.
    /// </summary>
    public Direction CollisionDirection { get; init; }

    /// <summary>
    /// Type of collision (entity, tile, boundary).
    /// </summary>
    public CollisionType Type { get; init; }
}

/// <summary>
/// Type of collision that occurred.
/// </summary>
public enum CollisionType
{
    Entity,         // Collided with another entity
    Tile,           // Collided with solid tile
    Boundary,       // Hit map boundary
    Elevation       // Wrong elevation
}

#endregion

#region Tile Behavior Events

/// <summary>
/// Event fired when entity steps onto a tile.
/// Fired after MovementCompletedEvent.
/// </summary>
public struct TileSteppedEvent : ITileBehaviorEvent, IPostEvent
{
    public Entity Entity { get; init; }
    public Entity TileEntity { get; init; }
    public float Timestamp { get; init; }

    /// <summary>
    /// Tile position.
    /// </summary>
    public (int X, int Y) Position { get; init; }

    /// <summary>
    /// Map ID.
    /// </summary>
    public int MapId { get; init; }

    /// <summary>
    /// Direction entity entered from.
    /// </summary>
    public Direction EntryDirection { get; init; }
}

/// <summary>
/// Event fired when checking if tile forces movement (ice, conveyor).
/// </summary>
public struct ForcedMovementCheckEvent : ITileBehaviorEvent
{
    public Entity Entity { get; init; }
    public Entity TileEntity { get; init; }
    public float Timestamp { get; init; }

    /// <summary>
    /// Current movement direction (if any).
    /// </summary>
    public Direction CurrentDirection { get; init; }

    /// <summary>
    /// Forced direction (set by handlers, Direction.None = no force).
    /// </summary>
    public Direction ForcedDirection { get; set; }
}

/// <summary>
/// Event fired when checking if tile allows jumping (ledge).
/// </summary>
public struct JumpCheckEvent : ITileBehaviorEvent
{
    public Entity Entity { get; init; }
    public Entity TileEntity { get; init; }
    public float Timestamp { get; init; }

    /// <summary>
    /// Direction entity is moving from.
    /// </summary>
    public Direction FromDirection { get; init; }

    /// <summary>
    /// Jump direction (set by handlers, Direction.None = no jump).
    /// </summary>
    public Direction JumpDirection { get; set; }

    /// <summary>
    /// Jump distance in tiles (set by handlers, default: 2).
    /// </summary>
    public int JumpDistance { get; set; }
}

#endregion

#region Entity Lifecycle Events

/// <summary>
/// Event fired when entity is created.
/// </summary>
public struct EntityCreatedEvent : ISystemEvent, IPostEvent
{
    public Entity Entity { get; init; }
    public float Timestamp { get; init; }

    /// <summary>
    /// Entity archetype name (if from template).
    /// </summary>
    public string? ArchetypeName { get; init; }
}

/// <summary>
/// Event fired when entity is destroyed.
/// </summary>
public struct EntityDestroyedEvent : ISystemEvent, IPreEvent
{
    public Entity Entity { get; init; }
    public float Timestamp { get; init; }

    /// <summary>
    /// Reason for destruction.
    /// </summary>
    public string? DestructionReason { get; init; }
}

/// <summary>
/// Event fired when component is added to entity.
/// </summary>
public struct ComponentAddedEvent : ISystemEvent, IPostEvent
{
    public Entity Entity { get; init; }
    public float Timestamp { get; init; }

    /// <summary>
    /// Type of component added.
    /// </summary>
    public Type ComponentType { get; init; }
}

/// <summary>
/// Event fired when component is removed from entity.
/// </summary>
public struct ComponentRemovedEvent : ISystemEvent, IPostEvent
{
    public Entity Entity { get; init; }
    public float Timestamp { get; init; }

    /// <summary>
    /// Type of component removed.
    /// </summary>
    public Type ComponentType { get; init; }
}

#endregion

#region Position Events

/// <summary>
/// Event fired when entity's position changes (any cause).
/// Useful for cameras, minimaps, spatial queries.
/// </summary>
public struct PositionChangedEvent : ISystemEvent, IPostEvent
{
    public Entity Entity { get; init; }
    public float Timestamp { get; init; }

    /// <summary>
    /// Previous grid position.
    /// </summary>
    public (int X, int Y) PreviousPosition { get; init; }

    /// <summary>
    /// New grid position.
    /// </summary>
    public (int X, int Y) NewPosition { get; init; }

    /// <summary>
    /// Previous map ID.
    /// </summary>
    public int PreviousMapId { get; init; }

    /// <summary>
    /// New map ID.
    /// </summary>
    public int NewMapId { get; init; }

    /// <summary>
    /// Whether position change was due to movement (vs teleport/warp).
    /// </summary>
    public bool WasMovement { get; init; }
}

#endregion
