using Arch.Core;
using Microsoft.Xna.Framework;

namespace PokeSharp.Engine.Core.Events.NPC;

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
///
///     This event triggers NPC-specific behavior:
///     - Start dialogue sequence
///     - Initiate battle (trainer NPCs)
///     - Give items (gift NPCs)
///     - Trigger scripted events
///     - Open shop interface (merchant NPCs)
///
///     Handlers typically subscribe with priority to ensure correct execution order:
///     1. High priority: Battle system (trainer battles)
///     2. Medium priority: Dialogue system (conversations)
///     3. Low priority: Scripted events (custom behaviors)
///
///     See EventSystemArchitecture.md for mod API event subscription patterns.
/// </remarks>
public sealed record NPCInteractionEvent : IGameEvent
{
    /// <inheritdoc />
    public Guid EventId { get; init; } = Guid.NewGuid();

    /// <inheritdoc />
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    ///     Gets the player entity that initiated the interaction.
    /// </summary>
    public required Entity Player { get; init; }

    /// <summary>
    ///     Gets the NPC entity being interacted with.
    /// </summary>
    public required Entity NPC { get; init; }

    /// <summary>
    ///     Gets the grid X coordinate of the NPC.
    /// </summary>
    public required int NPCX { get; init; }

    /// <summary>
    ///     Gets the grid Y coordinate of the NPC.
    /// </summary>
    public required int NPCY { get; init; }

    /// <summary>
    ///     Gets the direction the player is facing during interaction (0=South, 1=West, 2=East, 3=North).
    ///     Used to orient the NPC to face the player.
    /// </summary>
    public required int PlayerFacingDirection { get; init; }

    /// <summary>
    ///     Gets the type of NPC being interacted with.
    ///     Used to determine appropriate interaction behavior.
    /// </summary>
    public NPCType NPCType { get; init; } = NPCType.Generic;

    /// <summary>
    ///     Gets the unique identifier for this NPC (script name, trainer ID, etc.).
    ///     Used to load NPC-specific data and behaviors.
    /// </summary>
    public string? NPCIdentifier { get; init; }

    /// <summary>
    ///     Gets a value indicating whether this is the first time the player interacted with this NPC.
    ///     Used for one-time events (receiving items, first-time dialogue).
    /// </summary>
    public bool IsFirstInteraction { get; init; }
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
