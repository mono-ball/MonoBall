using Microsoft.Xna.Framework;

namespace PokeSharp.Engine.Core.Types.Events;

/// <summary>
///     Event raised when a script requests dialogue to be displayed.
///     This is caught by the game UI system (PokeSharp.Game/PokeSharp.Scripting)
///     which is responsible for rendering the dialogue box.
/// </summary>
public sealed record DialogueRequestedEvent : TypeEventBase
{
    /// <summary>
    ///     The message text to display in the dialogue box.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    ///     Optional speaker name for attribution (e.g., "Professor Oak", "Rival").
    ///     If null, the dialogue is presented as narration.
    /// </summary>
    public string? SpeakerName { get; init; }

    /// <summary>
    ///     Display priority - higher values are shown first.
    ///     Default is 0 for normal priority.
    /// </summary>
    public int Priority { get; init; } = 0;

    /// <summary>
    ///     Optional color tint for the dialogue box or text.
    /// </summary>
    public Color? Tint { get; init; }
}
