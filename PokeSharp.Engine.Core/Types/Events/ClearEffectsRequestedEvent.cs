namespace PokeSharp.Engine.Core.Types.Events;

/// <summary>
///     Event raised when a script requests all active visual effects to be cleared.
/// </summary>
public sealed record ClearEffectsRequestedEvent : TypeEventBase
{
    // No additional properties needed - the TypeId and Timestamp are sufficient
}
