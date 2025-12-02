namespace PokeSharp.Engine.Core.Events;

/// <summary>
///     Interface for events that can be cancelled by event handlers.
///     Cancellable events are typically published before an action occurs, allowing
///     mods or systems to prevent the action by calling PreventDefault().
/// </summary>
/// <remarks>
///     Cancellable events follow the pattern:
///     1. Event is published before action occurs
///     2. Handlers can inspect and potentially cancel the event
///     3. Source system checks IsCancelled before proceeding
///     4. If cancelled, the action is blocked and a *BlockedEvent may be published instead
///
///     Example flow:
///     - MovementStartedEvent (cancellable) -> if cancelled -> MovementBlockedEvent
///     - CollisionCheckEvent (cancellable) -> if cancelled -> custom collision response
///     - TileSteppedOnEvent (cancellable) -> if cancelled -> movement prevented
/// </remarks>
public interface ICancellableEvent : IGameEvent
{
    /// <summary>
    ///     Gets a value indicating whether this event has been cancelled by a handler.
    ///     When true, the source system should not proceed with the associated action.
    /// </summary>
    bool IsCancelled { get; }

    /// <summary>
    ///     Gets the reason why this event was cancelled, if applicable.
    ///     Useful for debugging and displaying feedback to the player.
    /// </summary>
    string? CancellationReason { get; }

    /// <summary>
    ///     Prevents the default action associated with this event.
    ///     Sets IsCancelled to true and optionally stores a reason for cancellation.
    /// </summary>
    /// <param name="reason">Optional reason for cancellation (e.g., "Blocked by scripted event").</param>
    void PreventDefault(string? reason = null);
}
