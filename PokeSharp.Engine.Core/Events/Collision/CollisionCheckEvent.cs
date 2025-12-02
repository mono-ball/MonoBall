using Arch.Core;
using Microsoft.Xna.Framework;

namespace PokeSharp.Engine.Core.Events.Collision;

/// <summary>
///     Event published during collision detection to determine if a position is walkable.
///     This is a cancellable event, allowing handlers to override collision rules.
/// </summary>
/// <remarks>
///     Published by the CollisionSystem before movement validation completes.
///     Handlers can cancel this event to mark the position as blocked, preventing movement.
///
///     The event uses an opt-in blocking model:
///     - Start with IsBlocked = false (walkable)
///     - Handlers call PreventDefault() to mark as blocked
///     - Multiple handlers can contribute to blocking (any block = fully blocked)
///
///     Common handlers:
///     - Tile Behaviors: Check tile blocking flags
///     - Elevation System: Filter by elevation layer
///     - Entity Collision: Check for solid entities at position
///     - Mod Systems: Custom collision rules (one-way tiles, etc.)
///
///     See EventSystemArchitecture.md lines 163-168 for collision check flow.
/// </remarks>
public sealed record CollisionCheckEvent : ICancellableEvent
{
    /// <inheritdoc />
    public Guid EventId { get; init; } = Guid.NewGuid();

    /// <inheritdoc />
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    ///     Gets the entity attempting to move into the position.
    ///     Used to check entity-specific collision rules (e.g., ghost-type Pokémon).
    /// </summary>
    public required Entity Entity { get; init; }

    /// <summary>
    ///     Gets the grid X coordinate being checked for collision.
    /// </summary>
    public required int PositionX { get; init; }

    /// <summary>
    ///     Gets the grid Y coordinate being checked for collision.
    /// </summary>
    public required int PositionY { get; init; }

    /// <summary>
    ///     Gets the direction from which the entity is approaching (0=South, 1=West, 2=East, 3=North).
    ///     Used for directional blocking (e.g., jump ledges block from certain directions).
    /// </summary>
    public int FromDirection { get; init; }

    /// <summary>
    ///     Gets the elevation layer being checked.
    ///     Used to filter collision by elevation (can't collide with different layers).
    /// </summary>
    public int Elevation { get; init; }

    /// <summary>
    ///     Gets a value indicating whether the position is blocked (not walkable).
    ///     Set to true by calling PreventDefault() from any handler.
    /// </summary>
    public bool IsBlocked => IsCancelled;

    /// <inheritdoc />
    public bool IsCancelled { get; private set; }

    /// <inheritdoc />
    public string? CancellationReason { get; private set; }

    /// <inheritdoc />
    public void PreventDefault(string? reason = null)
    {
        IsCancelled = true;
        CancellationReason = reason ?? "Position blocked";
    }
}
