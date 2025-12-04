namespace PokeSharp.Game.Components.NPCs;

/// <summary>
///     Component that enables player interaction with an entity.
///     Defines the range and conditions for interaction.
///     Pure data component - no methods.
/// </summary>
public struct Interaction
{
    /// <summary>
    ///     Range in tiles from which the player can interact with this entity.
    ///     0 = must be on same tile, 1 = adjacent tiles, etc.
    /// </summary>
    public int InteractionRange { get; set; }

    /// <summary>
    ///     Whether the player must be facing this entity to interact.
    /// </summary>
    public bool RequiresFacing { get; set; }

    /// <summary>
    ///     Whether this entity can be interacted with currently.
    ///     Can be toggled by scripts to enable/disable interaction.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    ///     Dialogue script path to run when interacted with (optional).
    ///     Example: "dialogues/nurse_joy_heal.csx"
    /// </summary>
    public string? DialogueScript { get; set; }

    /// <summary>
    ///     Event name to trigger when interacted with (optional).
    ///     Example: "on_interact_sign", "on_interact_pc"
    /// </summary>
    public string? InteractionEvent { get; set; }

    /// <summary>
    ///     Initializes a new interaction component with default values.
    /// </summary>
    public Interaction()
    {
        InteractionRange = 1; // Adjacent tiles by default
        RequiresFacing = true;
        IsEnabled = true;
        DialogueScript = null;
        InteractionEvent = null;
    }

    /// <summary>
    ///     Initializes a new interaction component with specified range and dialogue.
    /// </summary>
    public Interaction(int range, string? dialogueScript = null)
    {
        InteractionRange = range;
        RequiresFacing = true;
        IsEnabled = true;
        DialogueScript = dialogueScript;
        InteractionEvent = null;
    }
}
