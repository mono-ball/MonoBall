namespace PokeSharp.Engine.Core.Types.Events;

/// <summary>
///     Event raised when a script requests all pending dialogue messages to be cleared.
/// </summary>
public sealed record ClearMessagesRequestedEvent : TypeEventBase
{
    // No additional properties needed - the TypeId and Timestamp are sufficient
}
