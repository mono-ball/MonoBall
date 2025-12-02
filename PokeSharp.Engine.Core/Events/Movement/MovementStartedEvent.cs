using Arch.Core;

namespace PokeSharp.Engine.Core.Events.Movement;

/// <summary>
///     Event published before an entity begins movement from one tile to another.
///     This is a cancellable event, allowing handlers to prevent movement before it occurs.
/// </summary>
/// <remarks>
///     Published by the MovementSystem before updating position or starting animation.
///     Handlers can cancel this event to block movement (e.g., for scripted events, collision, or restrictions).
///     If cancelled, the entity will not move and a MovementBlockedEvent may be published instead.
///
///     Common use cases:
///     - Validate movement legality (collision detection)
///     - Anti-cheat systems checking movement speed/distance
///     - Scripted events blocking player movement
///     - Mod systems implementing custom movement rules
///
///     See EventSystemArchitecture.md lines 136-158 for full movement flow.
/// </remarks>
public sealed record MovementStartedEvent : ICancellableEvent
{
    /// <inheritdoc />
    public Guid EventId { get; init; } = Guid.NewGuid();

    /// <inheritdoc />
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    ///     Gets the entity that is attempting to move.
    /// </summary>
    public required Entity Entity { get; init; }

    /// <summary>
    ///     Gets the starting grid X coordinate before movement.
    /// </summary>
    public required int FromX { get; init; }

    /// <summary>
    ///     Gets the starting grid Y coordinate before movement.
    /// </summary>
    public required int FromY { get; init; }

    /// <summary>
    ///     Gets the target grid X coordinate after movement.
    /// </summary>
    public required int ToX { get; init; }

    /// <summary>
    ///     Gets the target grid Y coordinate after movement.
    /// </summary>
    public required int ToY { get; init; }

    /// <summary>
    ///     Gets the direction of movement (0=South, 1=West, 2=East, 3=North).
    /// </summary>
    public required int Direction { get; init; }

    /// <summary>
    ///     Gets the movement speed in tiles per second.
    ///     Used for calculating animation duration and detecting speed hacks.
    /// </summary>
    public float MovementSpeed { get; init; } = 1.0f;

    /// <inheritdoc />
    public bool IsCancelled { get; private set; }

    /// <inheritdoc />
    public string? CancellationReason { get; private set; }

    /// <inheritdoc />
    public void PreventDefault(string? reason = null)
    {
        IsCancelled = true;
        CancellationReason = reason ?? "Movement prevented";
    }
}
