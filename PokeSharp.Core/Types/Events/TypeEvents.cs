namespace PokeSharp.Core.Types.Events;

/// <summary>
///     Base event for type-related events.
///     All type lifecycle events should inherit from this to maintain consistency.
/// </summary>
public abstract record TypeEventBase
{
    /// <summary>
    ///     The type identifier that this event relates to.
    /// </summary>
    public required string TypeId { get; init; }

    /// <summary>
    ///     Game timestamp when this event was created (in seconds since game start).
    /// </summary>
    public required float Timestamp { get; init; }
}
