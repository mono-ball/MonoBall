using MonoBallFramework.Game.Engine.Core.Events;

namespace MonoBallFramework.Game.Engine.Core.Types.Events;

/// <summary>
///     Base event for type-related events.
///     All type lifecycle events should inherit from this to maintain consistency.
/// </summary>
/// <remarks>
///     Properties changed from 'init' to 'set' to support event pooling.
///     Events that are published frequently should use PublishPooled() for zero allocations.
/// </remarks>
public abstract record TypeEventBase : IPoolableEvent
{
    /// <summary>
    ///     The type identifier that this event relates to.
    /// </summary>
    public string TypeId { get; set; } = string.Empty;

    /// <summary>
    ///     Game timestamp when this event was created (in seconds since game start).
    /// </summary>
    public float Timestamp { get; set; }

    /// <summary>
    ///     Resets the event to a clean state for pool reuse.
    /// </summary>
    public virtual void Reset()
    {
        TypeId = string.Empty;
        Timestamp = 0f;
    }
}
