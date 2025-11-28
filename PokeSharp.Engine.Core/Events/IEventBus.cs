using PokeSharp.Engine.Core.Types.Events;

namespace PokeSharp.Engine.Core.Events;

/// <summary>
///     Event bus interface for publishing and subscribing to events.
///     Provides decoupled communication between systems.
/// </summary>
public interface IEventBus
{
    /// <summary>
    ///     Subscribe to events of type TEvent.
    /// </summary>
    /// <typeparam name="TEvent">The event type to subscribe to.</typeparam>
    /// <param name="handler">The handler to invoke when the event is published.</param>
    /// <returns>A disposable subscription that can be used to unsubscribe.</returns>
    IDisposable Subscribe<TEvent>(Action<TEvent> handler)
        where TEvent : TypeEventBase;

    /// <summary>
    ///     Publish an event to all subscribers.
    /// </summary>
    /// <typeparam name="TEvent">The event type to publish.</typeparam>
    /// <param name="eventData">The event data.</param>
    void Publish<TEvent>(TEvent eventData)
        where TEvent : TypeEventBase;

    /// <summary>
    ///     Clear all subscriptions for a specific event type.
    /// </summary>
    /// <typeparam name="TEvent">The event type to clear subscriptions for.</typeparam>
    void ClearSubscriptions<TEvent>()
        where TEvent : TypeEventBase;

    /// <summary>
    ///     Clear all subscriptions across all event types.
    /// </summary>
    void ClearAllSubscriptions();

    /// <summary>
    ///     Get the number of subscribers for a specific event type.
    /// </summary>
    /// <typeparam name="TEvent">The event type to check.</typeparam>
    /// <returns>The number of subscribers.</returns>
    int GetSubscriberCount<TEvent>()
        where TEvent : TypeEventBase;
}
