namespace MonoBallFramework.Game.Engine.Core.Events;

/// <summary>
///     Base class for cancellable events that can be prevented by handlers.
///     Use this for events that represent actions that can be blocked.
/// </summary>
/// <remarks>
///     Cancellable events represent actions that are about to happen:
///     - MovementStartedEvent: Entity is about to move (can be blocked)
///     - CollisionCheckEvent: Checking if position is walkable (can be blocked)
///     Handlers can call PreventDefault() to cancel the event and prevent the action.
///     For events that CANNOT be cancelled (notifications), use NotificationEventBase.
///     Inherits from NotificationEventBase to share common properties (EventId, Timestamp)
///     and adds ICancellableEvent implementation for cancellation support.
/// </remarks>
public abstract class CancellableEventBase : NotificationEventBase, ICancellableEvent
{
    /// <inheritdoc />
    public bool IsCancelled { get; private set; }

    /// <inheritdoc />
    public string? CancellationReason { get; private set; }

    /// <inheritdoc />
    public void PreventDefault(string? reason = null)
    {
        IsCancelled = true;
        CancellationReason = reason;
    }

    /// <inheritdoc />
    public override void Reset()
    {
        base.Reset();
        IsCancelled = false;
        CancellationReason = null;
    }
}

/// <summary>
///     Alias for backwards compatibility. Use CancellableEventBase instead.
/// </summary>
[Obsolete("Use CancellableEventBase instead. This alias will be removed in a future version.")]
public abstract class GameEventBase : CancellableEventBase
{
}
