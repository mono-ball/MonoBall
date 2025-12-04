namespace MonoBallFramework.Game.Engine.Core.Events;

/// <summary>
///     Interface for events that can be pooled and reused to reduce allocations.
/// </summary>
/// <remarks>
///     Events implementing this interface can be used with EventPool{T}
///     to eliminate allocations on hot paths. The Reset() method is called
///     when an event is rented from the pool to ensure clean state.
///     Usage pattern:
///     <code>
///     var pool = EventPool{MyEvent}.Shared;
///     var evt = pool.Rent();
///     evt.Reset();  // Ensure clean state
///     evt.SomeProperty = value;
///     eventBus.PublishPooled(evt, pool);
///     </code>
/// </remarks>
public interface IPoolableEvent
{
    /// <summary>
    ///     Resets the event to a clean state for reuse.
    ///     Should reset all mutable state to default/initial values.
    /// </summary>
    void Reset();
}
