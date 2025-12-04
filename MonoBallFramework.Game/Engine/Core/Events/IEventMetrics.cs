namespace MonoBallFramework.Game.Engine.Core.Events;

/// <summary>
///     Interface for collecting event bus performance metrics.
///     Implemented by the Event Inspector debug tool.
/// </summary>
public interface IEventMetrics
{
    /// <summary>
    ///     Gets or sets whether metrics collection is enabled.
    /// </summary>
    bool IsEnabled { get; set; }

    /// <summary>
    ///     Records a publish operation for an event type.
    /// </summary>
    /// <param name="eventTypeName">The name of the event type.</param>
    /// <param name="elapsedMicroseconds">Time taken in microseconds.</param>
    void RecordPublish(string eventTypeName, long elapsedMicroseconds);

    /// <summary>
    ///     Records a handler invocation.
    /// </summary>
    /// <param name="eventTypeName">The name of the event type.</param>
    /// <param name="handlerId">The unique handler ID.</param>
    /// <param name="elapsedMicroseconds">Time taken in microseconds.</param>
    void RecordHandlerInvoke(string eventTypeName, int handlerId, long elapsedMicroseconds);

    /// <summary>
    ///     Records a subscription being added.
    /// </summary>
    /// <param name="eventTypeName">The name of the event type.</param>
    /// <param name="handlerId">The unique handler ID.</param>
    /// <param name="source">Optional source identifier (script name, class name, etc.).</param>
    /// <param name="priority">Optional priority value.</param>
    void RecordSubscription(
        string eventTypeName,
        int handlerId,
        string? source = null,
        int priority = 0
    );

    /// <summary>
    ///     Records a subscription being removed.
    /// </summary>
    /// <param name="eventTypeName">The name of the event type.</param>
    /// <param name="handlerId">The unique handler ID.</param>
    void RecordUnsubscription(string eventTypeName, int handlerId);
}
