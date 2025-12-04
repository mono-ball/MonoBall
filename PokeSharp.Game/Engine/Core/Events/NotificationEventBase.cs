namespace PokeSharp.Game.Engine.Core.Events;

/// <summary>
///     Base class for notification events that cannot be cancelled.
///     Use this for events that inform about completed actions.
/// </summary>
/// <remarks>
///     Notification events represent facts that have already occurred:
///     - MovementCompletedEvent: Movement has finished
///     - MovementBlockedEvent: Movement was blocked
///     - CollisionDetectedEvent: A collision was detected
///     - CollisionResolvedEvent: A collision was resolved
///     These events cannot be cancelled because the action is already done.
///     Handlers can react to them but cannot prevent them.
///     For events that CAN be cancelled (to prevent an action), use CancellableEventBase.
/// </remarks>
public abstract class NotificationEventBase : IGameEvent, IPoolableEvent
{
    /// <inheritdoc />
    public Guid EventId { get; set; } = Guid.NewGuid();

    /// <inheritdoc />
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     Resets the event to a clean state for reuse from the pool.
    ///     Derived classes should override this and call base.Reset().
    /// </summary>
    /// <remarks>
    ///     PERFORMANCE: Does NOT reset EventId or Timestamp to avoid allocations.
    ///     - EventId is only used for debugging/tracking (not critical)
    ///     - Timestamp is set explicitly by callers anyway
    ///     This makes Reset() allocation-free for zero-GC pooling!
    /// </remarks>
    public virtual void Reset()
    {
        // DO NOT reset EventId (Guid.NewGuid() allocates!)
        // DO NOT reset Timestamp (callers set it explicitly, and DateTime.UtcNow allocates)
        // Only reset actual event data properties in derived classes
    }
}
