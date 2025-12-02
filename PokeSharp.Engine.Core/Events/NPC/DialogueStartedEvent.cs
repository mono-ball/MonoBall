using Arch.Core;

namespace PokeSharp.Engine.Core.Events.NPC;

/// <summary>
///     Event published when a dialogue sequence begins with an NPC or scripted event.
///     This is a notification event (not cancellable) that indicates dialogue has started.
/// </summary>
/// <remarks>
///     Published by the DialogueSystem after NPCInteractionEvent or scripted trigger.
///
///     This event causes the game to:
///     - Pause player input (movement, menu access)
///     - Display dialogue box UI
///     - Orient NPC to face the player
///     - Play dialogue sound effects
///     - Load dialogue text/script
///
///     The dialogue system takes control of input until the dialogue sequence completes,
///     at which point a DialogueEndedEvent is published.
///
///     Handlers can use this event for:
///     - Logging dialogue interactions
///     - Triggering dialogue-related achievements
///     - Modifying dialogue content (translation, personalization)
///     - Analytics and telemetry
/// </remarks>
public sealed record DialogueStartedEvent : IGameEvent
{
    /// <inheritdoc />
    public Guid EventId { get; init; } = Guid.NewGuid();

    /// <inheritdoc />
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    ///     Gets the entity that initiated the dialogue (player or script trigger).
    /// </summary>
    public Entity? Initiator { get; init; }

    /// <summary>
    ///     Gets the NPC entity involved in the dialogue, if applicable.
    /// </summary>
    public Entity? NPC { get; init; }

    /// <summary>
    ///     Gets the unique identifier for this dialogue sequence.
    ///     Used to load dialogue script, text, and choices.
    /// </summary>
    /// <example>
    ///     "professor_oak_intro", "nurse_joy_heal", "rival_battle_1"
    /// </example>
    public required string DialogueId { get; init; }

    /// <summary>
    ///     Gets the source of this dialogue (NPC interaction, scripted event, etc.).
    /// </summary>
    public DialogueSource Source { get; init; } = DialogueSource.NPC;

    /// <summary>
    ///     Gets the initial dialogue text to display.
    ///     May be modified by handlers for localization or personalization.
    /// </summary>
    public string? InitialDialogueText { get; init; }

    /// <summary>
    ///     Gets a value indicating whether this dialogue includes player choices.
    ///     If true, the player will be prompted to select an option.
    /// </summary>
    public bool HasChoices { get; init; }

    /// <summary>
    ///     Gets the dialogue priority for concurrent dialogue scenarios.
    ///     Higher priority dialogue interrupts lower priority.
    /// </summary>
    public int Priority { get; init; } = 0;
}

/// <summary>
///     Specifies the source that triggered the dialogue.
/// </summary>
public enum DialogueSource
{
    /// <summary>
    ///     Dialogue triggered by NPC interaction.
    /// </summary>
    NPC,

    /// <summary>
    ///     Dialogue triggered by scripted event.
    /// </summary>
    Script,

    /// <summary>
    ///     Dialogue triggered by sign or object interaction.
    /// </summary>
    Sign,

    /// <summary>
    ///     Dialogue triggered by system message (berry planted, etc.).
    /// </summary>
    System,
}
