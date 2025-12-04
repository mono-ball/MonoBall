using Arch.Core;

namespace MonoBallFramework.Game.Engine.Core.Events.NPC;

/// <summary>
///     Event published when a Pokémon battle is triggered (wild encounter or trainer battle).
///     This is a notification event (not cancellable) that indicates battle initiation.
/// </summary>
/// <remarks>
///     Published by the BattleSystem when:
///     - Player encounters wild Pokémon (tall grass, surfing, fishing)
///     - Player is challenged by trainer (line of sight, forced battle)
///     - Player initiates battle with trainer (NPCInteractionEvent -> battle)
///     - Scripted battle sequence begins
///     This event triggers the battle transition:
///     1. Fade out overworld rendering
///     2. Play battle intro animation/sound
///     3. Load battle scene
///     4. Initialize battle state
///     5. Start battle loop
///     The overworld is paused during battle. After battle completes, a BattleEndedEvent
///     is published and the overworld resumes.
///     Handlers can use this event for:
///     - Battle analytics and logging
///     - Achievement tracking (number of battles, battle types)
///     - Difficulty scaling (adjust wild Pokémon levels)
///     - Custom battle intros (mod-specific animations)
///     This class supports object pooling via EventPool{T} to reduce allocations.
/// </remarks>
public sealed class BattleTriggeredEvent : NotificationEventBase
{
    /// <summary>
    ///     Gets or sets the player entity entering the battle.
    /// </summary>
    public Entity Player { get; set; }

    /// <summary>
    ///     Gets or sets the type of battle being triggered.
    /// </summary>
    public BattleType BattleType { get; set; }

    /// <summary>
    ///     Gets or sets the opponent entity (trainer NPC), if applicable.
    ///     Null for wild Pokémon encounters.
    /// </summary>
    public Entity? Opponent { get; set; }

    /// <summary>
    ///     Gets or sets the grid X coordinate where the battle was triggered.
    ///     Used for battle location-specific features (weather, terrain).
    /// </summary>
    public int BattleLocationX { get; set; }

    /// <summary>
    ///     Gets or sets the grid Y coordinate where the battle was triggered.
    ///     Used for battle location-specific features (weather, terrain).
    /// </summary>
    public int BattleLocationY { get; set; }

    /// <summary>
    ///     Gets or sets the trigger source that caused this battle.
    /// </summary>
    public BattleTrigger Trigger { get; set; } = BattleTrigger.WildEncounter;

    /// <summary>
    ///     Gets or sets the wild Pokémon species identifier, if this is a wild encounter.
    /// </summary>
    /// <example>
    ///     "SPECIES_PIKACHU", "SPECIES_RATTATA"
    /// </example>
    public string? WildPokemonSpecies { get; set; }

    /// <summary>
    ///     Gets or sets the level of the wild Pokémon or opponent's Pokémon.
    /// </summary>
    public int PokemonLevel { get; set; } = 5;

    /// <summary>
    ///     Gets or sets a value indicating whether the player can flee from this battle.
    ///     False for trainer battles, true for wild encounters.
    /// </summary>
    public bool CanFlee { get; set; } = true;

    /// <summary>
    ///     Gets or sets the trainer identifier for trainer battles.
    /// </summary>
    /// <example>
    ///     "TRAINER_RIVAL_1", "TRAINER_YOUNGSTER_JOEY"
    /// </example>
    public string? TrainerIdentifier { get; set; }

    /// <inheritdoc />
    public override void Reset()
    {
        base.Reset();
        Player = default;
        BattleType = BattleType.WildSingle;
        Opponent = null;
        BattleLocationX = 0;
        BattleLocationY = 0;
        Trigger = BattleTrigger.WildEncounter;
        WildPokemonSpecies = null;
        PokemonLevel = 5;
        CanFlee = true;
        TrainerIdentifier = null;
    }
}

/// <summary>
///     Specifies the type of Pokémon battle.
/// </summary>
public enum BattleType
{
    /// <summary>
    ///     Wild Pokémon encounter (single wild Pokémon).
    /// </summary>
    WildSingle,

    /// <summary>
    ///     Wild double battle (two wild Pokémon).
    /// </summary>
    WildDouble,

    /// <summary>
    ///     Single trainer battle (1v1).
    /// </summary>
    TrainerSingle,

    /// <summary>
    ///     Double trainer battle (2v2).
    /// </summary>
    TrainerDouble,

    /// <summary>
    ///     Multi-battle (tag team with partner).
    /// </summary>
    Multi,

    /// <summary>
    ///     Scripted battle (story event, legendary encounter).
    /// </summary>
    Scripted,
}

/// <summary>
///     Specifies what triggered the battle.
/// </summary>
public enum BattleTrigger
{
    /// <summary>
    ///     Random wild encounter (tall grass, cave, etc.).
    /// </summary>
    WildEncounter,

    /// <summary>
    ///     Trainer spotted player (line of sight).
    /// </summary>
    TrainerSpotted,

    /// <summary>
    ///     Player initiated battle (talked to trainer).
    /// </summary>
    PlayerInitiated,

    /// <summary>
    ///     Scripted battle sequence.
    /// </summary>
    Script,

    /// <summary>
    ///     Surfing or fishing encounter.
    /// </summary>
    WaterEncounter,
}
