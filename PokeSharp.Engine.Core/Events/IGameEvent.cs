namespace PokeSharp.Engine.Core.Events;

/// <summary>
///     Base interface for all game events in the ECS event-driven architecture.
///     Events are immutable messages that represent state changes or actions in the game world.
/// </summary>
/// <remarks>
///     All events should be implemented as records with init-only properties to ensure immutability.
///     Events flow through the EventBus system where handlers can subscribe to process them.
///     See /docs/architecture/EventSystemArchitecture.md for event flow patterns.
/// </remarks>
public interface IGameEvent
{
    /// <summary>
    ///     Gets the unique identifier for this event instance.
    ///     Generated at event creation time for debugging and replay purposes.
    /// </summary>
    Guid EventId { get; init; }

    /// <summary>
    ///     Gets the timestamp when this event was created (UTC).
    ///     Used for event ordering, replay, and performance analysis.
    /// </summary>
    DateTime Timestamp { get; init; }

    /// <summary>
    ///     Gets the type name of this event for logging and debugging.
    ///     Should return the concrete event type's simple name (e.g., "MovementStartedEvent").
    /// </summary>
    string EventType => GetType().Name;
}
