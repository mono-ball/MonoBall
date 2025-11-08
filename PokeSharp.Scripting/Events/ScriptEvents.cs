using PokeSharp.Core.Types.Events;

namespace PokeSharp.Scripting.Events;

/// <summary>
///     Event published when a script requests to show a dialogue message.
/// </summary>
/// <remarks>
///     Consumers of this event (UI systems, dialogue boxes) should subscribe
///     and handle the message display appropriately.
/// </remarks>
public sealed record DialogueRequestEvent : TypeEventBase
{
    /// <summary>
    ///     The message text to display to the player.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    ///     Optional speaker name for dialogue attribution.
    /// </summary>
    public string? SpeakerName { get; init; }

    /// <summary>
    ///     Priority level for message display (higher values display first).
    /// </summary>
    public int Priority { get; init; } = 0;
}