using Arch.Core;
using MonoBallFramework.Game.Engine.Core.Types;

namespace MonoBallFramework.Game.Engine.Core.Events.NPC;

/// <summary>
///     Event published when a player interacts with an NPC (presses A button while facing them).
///     This is a notification event (not cancellable) that indicates an interaction occurred.
/// </summary>
/// <remarks>
///     Published by the NPCInteractionSystem after validating:
///     - Player pressed the interact button (A)
///     - Player is facing an NPC
///     - NPC is within interaction range (adjacent tile)
///     - No other interaction is currently active
///     This event triggers NPC-specific behavior:
///     - Start dialogue sequence
///     - Initiate battle (trainer NPCs)
///     - Give items (gift NPCs)
///     - Trigger scripted events
///     - Open shop interface (merchant NPCs)
///     Handlers typically subscribe with priority to ensure correct execution order:
///     1. High priority: Battle system (trainer battles)
///     2. Medium priority: Dialogue system (conversations)
///     3. Low priority: Scripted events (custom behaviors)
///     This class supports object pooling via EventPool{T} to reduce allocations.
///     See EventSystemArchitecture.md for mod API event subscription patterns.
/// </remarks>
public sealed class NPCInteractionEvent : NotificationEventBase
{
    /// <summary>
    ///     Gets or sets the player entity that initiated the interaction.
    /// </summary>
    public Entity Player { get; set; }

    /// <summary>
    ///     Gets or sets the NPC entity being interacted with.
    /// </summary>
    public Entity NPC { get; set; }

    /// <summary>
    ///     Gets or sets the grid X coordinate of the NPC.
    /// </summary>
    public int NPCX { get; set; }

    /// <summary>
    ///     Gets or sets the grid Y coordinate of the NPC.
    /// </summary>
    public int NPCY { get; set; }

    /// <summary>
    ///     Gets or sets the direction the player is facing during interaction (0=South, 1=West, 2=East, 3=North).
    ///     Used to orient the NPC to face the player.
    /// </summary>
    public int PlayerFacingDirection { get; set; }

    /// <summary>
    ///     Gets or sets the type of NPC being interacted with.
    ///     Used to determine appropriate interaction behavior.
    /// </summary>
    public NPCType NPCType { get; set; } = NPCType.Generic;

    /// <summary>
    ///     Gets or sets the unique identifier for this NPC.
    ///     Used to load NPC-specific data and behaviors.
    /// </summary>
    public GameNpcId? NpcId { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether this is the first time the player interacted with this NPC.
    ///     Used for one-time events (receiving items, first-time dialogue).
    /// </summary>
    public bool IsFirstInteraction { get; set; }

    /// <inheritdoc />
    public override void Reset()
    {
        base.Reset();
        Player = default;
        NPC = default;
        NPCX = 0;
        NPCY = 0;
        PlayerFacingDirection = 0;
        NPCType = NPCType.Generic;
        NpcId = null;
        IsFirstInteraction = false;
    }
}

/// <summary>
///     Specifies the type of NPC being interacted with.
/// </summary>
public enum NPCType
{
    /// <summary>
    ///     Generic NPC with dialogue only.
    /// </summary>
    Generic,

    /// <summary>
    ///     Trainer NPC that can initiate battles.
    /// </summary>
    Trainer,

    /// <summary>
    ///     Merchant NPC that opens a shop interface.
    /// </summary>
    Merchant,

    /// <summary>
    ///     Nurse NPC that heals Pokémon (Nurse Joy).
    /// </summary>
    Nurse,

    /// <summary>
    ///     NPC that gives items or gifts.
    /// </summary>
    ItemGiver,

    /// <summary>
    ///     Quest-related NPC with scripted behaviors.
    /// </summary>
    Quest,
}
