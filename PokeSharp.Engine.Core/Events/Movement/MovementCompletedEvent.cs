using Arch.Core;

namespace PokeSharp.Engine.Core.Events.Movement;

/// <summary>
///     Event published after an entity successfully completes movement to a new tile.
///     This is a notification event (not cancellable) since the movement has already occurred.
/// </summary>
/// <remarks>
///     Published by the MovementSystem after position has been updated and animation completes.
///     This event triggers follow-up actions like:
///     - Tile behavior activation (OnStep scripts)
///     - Warp detection and execution
///     - Achievement/stat tracking
///     - Audio cues (footstep sounds)
///
///     The timing of this event matters:
///     - Pixel interpolation: Published when PixelX/PixelY reach target
///     - Grid update: Position.X/Y already updated to new coordinates
///     - Animation: Walk animation may still be playing
///
///     See EventSystemArchitecture.md lines 154-157 for movement completion flow.
/// </remarks>
public sealed record MovementCompletedEvent : IGameEvent
{
    /// <inheritdoc />
    public Guid EventId { get; init; } = Guid.NewGuid();

    /// <inheritdoc />
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    ///     Gets the entity that completed movement.
    /// </summary>
    public required Entity Entity { get; init; }

    /// <summary>
    ///     Gets the previous grid X coordinate before movement.
    /// </summary>
    public required int PreviousX { get; init; }

    /// <summary>
    ///     Gets the previous grid Y coordinate before movement.
    /// </summary>
    public required int PreviousY { get; init; }

    /// <summary>
    ///     Gets the current grid X coordinate after movement.
    /// </summary>
    public required int CurrentX { get; init; }

    /// <summary>
    ///     Gets the current grid Y coordinate after movement.
    /// </summary>
    public required int CurrentY { get; init; }

    /// <summary>
    ///     Gets the direction of the completed movement (0=South, 1=West, 2=East, 3=North).
    /// </summary>
    public required int Direction { get; init; }

    /// <summary>
    ///     Gets the actual duration of the movement in seconds.
    ///     Useful for performance analysis and movement speed validation.
    /// </summary>
    public float MovementDuration { get; init; }

    /// <summary>
    ///     Gets a value indicating whether this movement triggered a tile transition.
    ///     True if the entity stepped onto a different tile type (e.g., grass -> water).
    /// </summary>
    public bool TileTransition { get; init; }
}
