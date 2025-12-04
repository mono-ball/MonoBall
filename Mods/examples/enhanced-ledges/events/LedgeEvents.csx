using Arch.Core;
using MonoBallFramework.Engine.Core.Events;

namespace EnhancedLedges.Events;

/// <summary>
///     Event published when an entity successfully jumps over a ledge.
///     This event is informational and cannot be cancelled.
/// </summary>
/// <remarks>
///     Published by ledge tile behaviors after a successful jump.
///     Use this event to:
///     - Track jump statistics
///     - Trigger sound/visual effects
///     - Award achievements
///     - Update jump-related game state
/// </remarks>
public sealed record LedgeJumpedEvent : IGameEvent
{
    /// <inheritdoc />
    public Guid EventId { get; init; } = Guid.NewGuid();

    /// <inheritdoc />
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    ///     Gets the entity that performed the jump.
    /// </summary>
    public required Entity Entity { get; init; }

    /// <summary>
    ///     Gets the direction of the jump (0=South, 1=West, 2=East, 3=North).
    /// </summary>
    public required int Direction { get; init; }

    /// <summary>
    ///     Gets the jump height multiplier (1.0 = normal, >1.0 = boosted).
    /// </summary>
    public float JumpHeight { get; init; } = 1.0f;

    /// <summary>
    ///     Gets the tile position (X coordinate) where the jump occurred.
    /// </summary>
    public required int TileX { get; init; }

    /// <summary>
    ///     Gets the tile position (Y coordinate) where the jump occurred.
    /// </summary>
    public required int TileY { get; init; }

    /// <summary>
    ///     Gets whether this jump was boosted by an item or effect.
    /// </summary>
    public bool IsBoosted { get; init; }
}

/// <summary>
///     Event published when a ledge tile crumbles and becomes impassable.
///     This event is informational and cannot be cancelled.
/// </summary>
/// <remarks>
///     Published by the crumbling ledge behavior when durability reaches zero.
///     Use this event to:
///     - Trigger crumble animations
///     - Play sound effects
///     - Update map state
///     - Notify other systems of terrain changes
/// </remarks>
public sealed record LedgeCrumbledEvent : IGameEvent
{
    /// <inheritdoc />
    public Guid EventId { get; init; } = Guid.NewGuid();

    /// <inheritdoc />
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    ///     Gets the X coordinate of the crumbled ledge tile.
    /// </summary>
    public required int TileX { get; init; }

    /// <summary>
    ///     Gets the Y coordinate of the crumbled ledge tile.
    /// </summary>
    public required int TileY { get; init; }

    /// <summary>
    ///     Gets whether the player was standing on the tile when it crumbled.
    /// </summary>
    public bool WasPlayerOn { get; init; }

    /// <summary>
    ///     Gets the direction the ledge was facing (0=South, 1=West, 2=East, 3=North).
    /// </summary>
    public required int LedgeDirection { get; init; }

    /// <summary>
    ///     Gets the total number of jumps before crumbling.
    /// </summary>
    public int TotalJumps { get; init; }
}

/// <summary>
///     Event published when a jump boost effect is activated on an entity.
///     This event is informational and cannot be cancelled.
/// </summary>
/// <remarks>
///     Published by jump boost items or effects when consumed/activated.
///     Use this event to:
///     - Display boost notifications
///     - Play buff sound effects
///     - Track buff durations
///     - Show visual indicators
/// </remarks>
public sealed record JumpBoostActivatedEvent : IGameEvent
{
    /// <inheritdoc />
    public Guid EventId { get; init; } = Guid.NewGuid();

    /// <inheritdoc />
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    ///     Gets the entity that received the jump boost.
    /// </summary>
    public required Entity Entity { get; init; }

    /// <summary>
    ///     Gets the boost multiplier applied to jump height (e.g., 2.0 = double jump).
    /// </summary>
    public float BoostMultiplier { get; init; } = 2.0f;

    /// <summary>
    ///     Gets the duration of the boost effect in seconds.
    /// </summary>
    public float DurationSeconds { get; init; }

    /// <summary>
    ///     Gets the source of the boost (e.g., "JumpBoostItem", "JumpShoes").
    /// </summary>
    public string BoostSource { get; init; } = "Unknown";

    /// <summary>
    ///     Gets when the boost effect will expire.
    /// </summary>
    public DateTime ExpiresAt { get; init; }
}
