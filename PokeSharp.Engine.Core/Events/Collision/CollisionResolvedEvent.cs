using Arch.Core;
using Microsoft.Xna.Framework;

namespace PokeSharp.Engine.Core.Events.Collision;

/// <summary>
///     Event published after a collision has been resolved and handled.
///     This is a notification event (not cancellable) that indicates how the collision was processed.
/// </summary>
/// <remarks>
///     Published by collision handlers after determining the appropriate response to a collision.
///     This event is used for:
///     - Logging collision responses for debugging
///     - Triggering follow-up actions (animations, sounds)
///     - Analytics and telemetry
///
///     The resolution may be:
///     - Blocked: EntityA stopped, movement cancelled
///     - Pushed: EntityB moved out of the way
///     - Triggered: Interaction or event triggered
///     - Passed: Collision ignored (non-solid entities)
///
///     This event is published AFTER any positional updates or state changes have occurred.
/// </remarks>
public sealed record CollisionResolvedEvent : IGameEvent
{
    /// <inheritdoc />
    public Guid EventId { get; init; } = Guid.NewGuid();

    /// <inheritdoc />
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    ///     Gets the first entity involved in the collision (typically the moving entity).
    /// </summary>
    public required Entity EntityA { get; init; }

    /// <summary>
    ///     Gets the second entity involved in the collision (typically the stationary entity).
    /// </summary>
    public required Entity EntityB { get; init; }

    /// <summary>
    ///     Gets the grid X coordinate where the collision was resolved.
    /// </summary>
    public required int ContactX { get; init; }

    /// <summary>
    ///     Gets the grid Y coordinate where the collision was resolved.
    /// </summary>
    public required int ContactY { get; init; }

    /// <summary>
    ///     Gets how the collision was resolved.
    /// </summary>
    public required CollisionResolution Resolution { get; init; }

    /// <summary>
    ///     Gets additional information about the resolution.
    ///     Used for debugging and logging specific resolution details.
    /// </summary>
    /// <example>
    ///     "NPC interaction started", "Boulder pushed 1 tile south", "Player movement blocked"
    /// </example>
    public string? ResolutionDetails { get; init; }

    /// <summary>
    ///     Gets a value indicating whether the resolution resulted in a state change.
    ///     True if entities moved, interactions triggered, or game state changed.
    /// </summary>
    public bool ResultedInStateChange { get; init; }
}

/// <summary>
///     Specifies how a collision was resolved.
/// </summary>
public enum CollisionResolution
{
    /// <summary>
    ///     Movement was blocked; EntityA stopped.
    /// </summary>
    Blocked,

    /// <summary>
    ///     EntityB was pushed out of the way.
    /// </summary>
    Pushed,

    /// <summary>
    ///     An interaction or event was triggered.
    /// </summary>
    Triggered,

    /// <summary>
    ///     Collision was ignored (non-solid entities).
    /// </summary>
    Passed,

    /// <summary>
    ///     Custom resolution by mod or script.
    /// </summary>
    Custom,
}
