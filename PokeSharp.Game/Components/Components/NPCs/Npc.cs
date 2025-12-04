namespace PokeSharp.Game.Components.NPCs;

/// <summary>
///     Component identifying an entity as an NPC with game-specific properties.
///     Pure data component - no methods.
/// </summary>
public struct Npc
{
    /// <summary>
    ///     Unique identifier for this NPC (e.g., "rival_oak_lab", "nurse_joy_pewter").
    /// </summary>
    public string NpcId { get; set; }

    /// <summary>
    ///     Whether this NPC is a trainer who can battle the player.
    /// </summary>
    public bool IsTrainer { get; set; }

    /// <summary>
    ///     Whether the player has already defeated this trainer.
    ///     Only relevant if IsTrainer is true.
    /// </summary>
    public bool IsDefeated { get; set; }

    /// <summary>
    ///     Number of tiles the NPC can "see" to spot the player.
    ///     Used for trainer battles and NPC reactions.
    /// </summary>
    public int ViewRange { get; set; }

    /// <summary>
    ///     Initializes a new NPC component with required fields.
    ///     Use <see cref="Components.Common.Name" /> for display names.
    /// </summary>
    /// <param name="npcId">Unique identifier for the NPC.</param>
    public Npc(string npcId)
    {
        NpcId = npcId;
        IsTrainer = false;
        IsDefeated = false;
        ViewRange = 0;
    }
}
