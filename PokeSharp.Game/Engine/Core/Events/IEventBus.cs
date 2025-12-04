namespace PokeSharp.Game.Engine.Core.Events;

/// <summary>
///     Event bus interface for publishing and subscribing to events.
///     Provides decoupled communication between systems.
///     Supports both legacy TypeEventBase and new IGameEvent interfaces for backwards compatibility.
/// </summary>
public interface IEventBus
{
    /// <summary>
    ///     Subscribe to events of type TEvent.
    /// </summary>
    /// <typeparam name="TEvent">The event type to subscribe to (any class type).</typeparam>
    /// <param name="handler">The handler to invoke when the event is published.</param>
    /// <returns>A disposable subscription that can be used to unsubscribe.</returns>
    IDisposable Subscribe<TEvent>(Action<TEvent> handler)
        where TEvent : class;

    /// <summary>
    ///     Publish an event to all subscribers.
    /// </summary>
    /// <typeparam name="TEvent">The event type to publish (any class type).</typeparam>
    /// <param name="eventData">The event data.</param>
    void Publish<TEvent>(TEvent eventData)
        where TEvent : class;

    /// <summary>
    ///     Publish a pooled event to all subscribers with automatic pooling management.
    ///     This method rents an event from the pool, configures it, publishes it, and returns it to the pool.
    /// </summary>
    /// <typeparam name="TEvent">The event type to publish (must implement IPoolableEvent).</typeparam>
    /// <param name="configure">Action to configure the event instance.</param>
    /// <remarks>
    ///     Use this for high-frequency notification events (published 10+ times per second).
    ///     For cancellable events that need to check handler modifications, use RentEvent/Publish/ReturnEvent instead.
    ///     Example:
    ///     <code>
    ///     // For notification events (no need to check modifications)
    ///     eventBus.PublishPooled&lt;MovementCompletedEvent&gt;(evt => {
    ///         evt.Entity = entity;
    ///         evt.Direction = Direction.North;
    ///     });
    ///     </code>
    /// </remarks>
    void PublishPooled<TEvent>(Action<TEvent> configure)
        where TEvent : class, IPoolableEvent, new();

    /// <summary>
    ///     Rents an event from the pool for manual configuration and publishing.
    ///     Must be paired with ReturnEvent() after publishing.
    /// </summary>
    /// <typeparam name="TEvent">The event type to rent (must implement IPoolableEvent).</typeparam>
    /// <returns>A clean event instance from the pool.</returns>
    /// <remarks>
    ///     Use this for cancellable events where you need to check handler modifications.
    ///     Always call ReturnEvent() after publishing, preferably in a finally block.
    ///     Example:
    ///     <code>
    ///     var evt = eventBus.RentEvent&lt;MovementStartedEvent&gt;();
    ///     try {
    ///         evt.Entity = entity;
    ///         eventBus.Publish(evt);
    ///         // Check modifications AFTER handlers run
    ///         if (evt.IsCancelled) { /* handle cancellation */ }
    ///     }
    ///     finally {
    ///         eventBus.ReturnEvent(evt);
    ///     }
    ///     </code>
    /// </remarks>
    TEvent RentEvent<TEvent>()
        where TEvent : class, IPoolableEvent, new();

    /// <summary>
    ///     Returns a rented event back to the pool.
    ///     Must be called after RentEvent() and publishing.
    /// </summary>
    /// <typeparam name="TEvent">The event type to return (must implement IPoolableEvent).</typeparam>
    /// <param name="evt">The event instance to return to the pool.</param>
    void ReturnEvent<TEvent>(TEvent evt)
        where TEvent : class, IPoolableEvent, new();

    /// <summary>
    ///     Clear all subscriptions for a specific event type.
    /// </summary>
    /// <typeparam name="TEvent">The event type to clear subscriptions for (any class type).</typeparam>
    void ClearSubscriptions<TEvent>()
        where TEvent : class;

    /// <summary>
    ///     Clear all subscriptions across all event types.
    /// </summary>
    void ClearAllSubscriptions();

    /// <summary>
    ///     Get the number of subscribers for a specific event type.
    /// </summary>
    /// <typeparam name="TEvent">The event type to check (any class type).</typeparam>
    /// <returns>The number of subscribers.</returns>
    int GetSubscriberCount<TEvent>()
        where TEvent : class;

    /// <summary>
    ///     Gets all registered event types for inspection.
    ///     Used by the Event Inspector debug tool.
    /// </summary>
    /// <returns>Collection of all event types that have handlers registered.</returns>
    IReadOnlyCollection<Type> GetRegisteredEventTypes();

    /// <summary>
    ///     Gets all handler IDs for a specific event type.
    ///     Used by the Event Inspector debug tool.
    /// </summary>
    /// <param name="eventType">The event type to get handler IDs for.</param>
    /// <returns>Collection of handler IDs registered for the event type.</returns>
    IReadOnlyCollection<int> GetHandlerIds(Type eventType);

    /// <summary>
    ///     Gets pool statistics for all pooled event types.
    ///     Used for performance monitoring and debugging.
    /// </summary>
    /// <returns>Collection of pool statistics for each pooled event type.</returns>
    IReadOnlyCollection<EventPoolStatistics> GetPoolStatistics();
}
