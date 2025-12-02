using Arch.Core;

namespace PokeSharp.Engine.Core.Events.Movement;

/// <summary>
///     Event published when an entity's movement is blocked or prevented.
///     This is a notification event (not cancellable) that indicates why movement failed.
/// </summary>
/// <remarks>
///     Published by the MovementSystem when:
///     - MovementStartedEvent is cancelled by a handler
///     - Collision detection blocks the movement
///     - Map boundary is reached
///     - Movement validation fails (e.g., surfing without Surf HM)
///
///     This event triggers feedback to the player:
///     - Bump animation against solid objects
///     - Sound effect for collision
///     - Error message display (e.g., "Can't surf here!")
///
///     Handlers can use BlockedReason to determine appropriate feedback.
///
///     See EventSystemArchitecture.md lines 169-172 for blocked movement flow.
/// </remarks>
public sealed record MovementBlockedEvent : IGameEvent
{
    /// <inheritdoc />
    public Guid EventId { get; init; } = Guid.NewGuid();

    /// <inheritdoc />
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    ///     Gets the entity that attempted to move.
    /// </summary>
    public required Entity Entity { get; init; }

    /// <summary>
    ///     Gets the grid X coordinate where the entity attempted to move from.
    /// </summary>
    public required int AttemptedFromX { get; init; }

    /// <summary>
    ///     Gets the grid Y coordinate where the entity attempted to move from.
    /// </summary>
    public required int AttemptedFromY { get; init; }

    /// <summary>
    ///     Gets the grid X coordinate where the entity attempted to move to.
    /// </summary>
    public required int AttemptedToX { get; init; }

    /// <summary>
    ///     Gets the grid Y coordinate where the entity attempted to move to.
    /// </summary>
    public required int AttemptedToY { get; init; }

    /// <summary>
    ///     Gets the direction of the blocked movement attempt (0=South, 1=West, 2=East, 3=North).
    /// </summary>
    public required int Direction { get; init; }

    /// <summary>
    ///     Gets the reason why the movement was blocked.
    ///     Used for displaying feedback and debugging.
    /// </summary>
    /// <example>
    ///     "Solid object", "Map boundary", "No Surf HM", "Scripted event", "Elevation mismatch"
    /// </example>
    public required string BlockedReason { get; init; }

    /// <summary>
    ///     Gets the type of obstruction that blocked movement.
    ///     Used by animation/audio systems to select appropriate feedback.
    /// </summary>
    public BlockedType BlockedBy { get; init; } = BlockedType.Unknown;
}

/// <summary>
///     Specifies the type of obstruction that blocked movement.
/// </summary>
public enum BlockedType
{
    /// <summary>
    ///     Unknown or unspecified blockage.
    /// </summary>
    Unknown,

    /// <summary>
    ///     Blocked by a solid tile (wall, tree, rock).
    /// </summary>
    Tile,

    /// <summary>
    ///     Blocked by another entity (NPC, player, solid object).
    /// </summary>
    Entity,

    /// <summary>
    ///     Blocked by map boundary (edge of map).
    /// </summary>
    MapBoundary,

    /// <summary>
    ///     Blocked by elevation difference (can't walk up ledge).
    /// </summary>
    Elevation,

    /// <summary>
    ///     Blocked by scripted event or custom logic.
    /// </summary>
    Script,
}
