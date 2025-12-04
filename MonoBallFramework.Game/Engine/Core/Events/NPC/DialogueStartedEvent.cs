using Arch.Core;

namespace MonoBallFramework.Game.Engine.Core.Events.NPC;

/// <summary>
///     Event published when a dialogue sequence begins with an NPC or scripted event.
///     This is a notification event (not cancellable) that indicates dialogue has started.
/// </summary>
/// <remarks>
///     Published by the DialogueSystem after NPCInteractionEvent or scripted trigger.
///     This event causes the game to:
///     - Pause player input (movement, menu access)
///     - Display dialogue box UI
///     - Orient NPC to face the player
///     - Play dialogue sound effects
///     - Load dialogue text/script
///     The dialogue system takes control of input until the dialogue sequence completes,
///     at which point a DialogueEndedEvent is published.
///     Handlers can use this event for:
///     - Logging dialogue interactions
///     - Triggering dialogue-related achievements
///     - Modifying dialogue content (translation, personalization)
///     - Analytics and telemetry
///     This class supports object pooling via EventPool{T} to reduce allocations.
/// </remarks>
public sealed class DialogueStartedEvent : NotificationEventBase
{
    /// <summary>
    ///     Gets or sets the entity that initiated the dialogue (player or script trigger).
    /// </summary>
    public Entity? Initiator { get; set; }

    /// <summary>
    ///     Gets or sets the NPC entity involved in the dialogue, if applicable.
    /// </summary>
    public Entity? NPC { get; set; }

    /// <summary>
    ///     Gets or sets the unique identifier for this dialogue sequence.
    ///     Used to load dialogue script, text, and choices.
    /// </summary>
    /// <example>
    ///     "professor_oak_intro", "nurse_joy_heal", "rival_battle_1"
    /// </example>
    public string DialogueId { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the source of this dialogue (NPC interaction, scripted event, etc.).
    /// </summary>
    public DialogueSource Source { get; set; } = DialogueSource.NPC;

    /// <summary>
    ///     Gets or sets the initial dialogue text to display.
    ///     May be modified by handlers for localization or personalization.
    /// </summary>
    public string? InitialDialogueText { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether this dialogue includes player choices.
    ///     If true, the player will be prompted to select an option.
    /// </summary>
    public bool HasChoices { get; set; }

    /// <summary>
    ///     Gets or sets the dialogue priority for concurrent dialogue scenarios.
    ///     Higher priority dialogue interrupts lower priority.
    /// </summary>
    public int Priority { get; set; }

    /// <inheritdoc />
    public override void Reset()
    {
        base.Reset();
        Initiator = null;
        NPC = null;
        DialogueId = string.Empty;
        Source = DialogueSource.NPC;
        InitialDialogueText = null;
        HasChoices = false;
        Priority = 0;
    }
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
