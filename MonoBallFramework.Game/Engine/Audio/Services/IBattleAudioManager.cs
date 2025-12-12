namespace MonoBallFramework.Game.Engine.Audio.Services;

/// <summary>
///     Manages audio orchestration during Pokemon battles.
///     Coordinates battle music, move sounds, cries, and UI feedback with context-aware playback.
///     Handles music transitions, low HP warnings, and battle-specific audio effects.
/// </summary>
public interface IBattleAudioManager : IDisposable
{
    /// <summary>
    ///     Gets or sets whether the battle audio system is active.
    ///     When inactive, audio events are ignored to prevent interference with other game states.
    /// </summary>
    bool IsActive { get; set; }

    /// <summary>
    ///     Starts battle music with optional intro sequence based on battle type.
    ///     Automatically selects appropriate music track unless overridden.
    /// </summary>
    /// <param name="battleType">The type of battle for automatic music selection.</param>
    /// <param name="musicName">Optional specific music track identifier, or null for automatic selection based on battleType.</param>
    void StartBattleMusic(BattleType battleType, string? musicName = null);

    /// <summary>
    ///     Stops battle music and prepares for return to previous context (e.g., route music).
    /// </summary>
    /// <param name="fadeOutDuration">Duration of fade-out effect in seconds (default: 1.0s).</param>
    void StopBattleMusic(float fadeOutDuration = 1.0f);

    /// <summary>
    ///     Plays a battle move sound effect with optional type-based variation.
    /// </summary>
    /// <param name="moveName">The name of the Pokemon move (e.g., "Tackle", "Thunderbolt").</param>
    /// <param name="moveType">The elemental type of the move for type-specific sound variations, or null for generic sound.</param>
    void PlayMoveSound(string moveName, string? moveType = null);

    /// <summary>
    ///     Plays the battle encounter transition sound based on encounter type.
    /// </summary>
    /// <param name="encounterType">The type of encounter (wild, trainer, or legendary).</param>
    void PlayEncounterSound(EncounterType encounterType);

    /// <summary>
    ///     Plays a Pokemon cry in battle context with appropriate timing and volume.
    /// </summary>
    /// <param name="speciesId">The National Dex species ID of the Pokemon.</param>
    /// <param name="isPlayerPokemon">Whether this is the player's Pokemon (may affect stereo pan or timing).</param>
    void PlayBattleCry(int speciesId, bool isPlayerPokemon);

    /// <summary>
    ///     Plays UI feedback sound for battle menu actions and selections.
    /// </summary>
    /// <param name="action">The UI action type that occurred.</param>
    void PlayUISound(BattleUIAction action);

    /// <summary>
    ///     Plays a status condition sound effect (poison damage, burn, etc.).
    /// </summary>
    /// <param name="statusCondition">The status condition identifier (e.g., "poison", "burn", "paralysis").</param>
    void PlayStatusSound(string statusCondition);

    /// <summary>
    ///     Starts playing the low HP warning sound in a loop.
    ///     This is the distinctive beep that plays when a Pokemon's HP is critically low.
    /// </summary>
    void PlayLowHealthWarning();

    /// <summary>
    ///     Stops the low HP warning sound loop.
    ///     Should be called when HP is restored or Pokemon faints.
    /// </summary>
    void StopLowHealthWarning();

    /// <summary>
    ///     Updates the battle audio manager state, handling music transitions and looping warning sounds.
    ///     Must be called once per frame during battle.
    /// </summary>
    /// <param name="deltaTime">Time elapsed since last update in seconds.</param>
    void Update(float deltaTime);
}

/// <summary>
///     Types of Pokemon battles for automatic music selection.
/// </summary>
public enum BattleType
{
    /// <summary>Wild Pokemon encounter (random grass/cave encounters).</summary>
    Wild,
    /// <summary>Standard trainer battle.</summary>
    Trainer,
    /// <summary>Gym Leader battle with unique music.</summary>
    GymLeader,
    /// <summary>Elite Four member battle.</summary>
    EliteFour,
    /// <summary>Champion battle with climactic music.</summary>
    Champion,
    /// <summary>Legendary Pokemon encounter with special music.</summary>
    Legendary,
    /// <summary>Rival battle with unique theme.</summary>
    Rival
}

/// <summary>
///     Types of battle encounters for transition sound effects.
/// </summary>
public enum EncounterType
{
    /// <summary>Wild Pokemon encounter (standard transition).</summary>
    Wild,
    /// <summary>Trainer spotted transition (exclamation mark, music cue).</summary>
    Trainer,
    /// <summary>Legendary Pokemon encounter (dramatic transition).</summary>
    Legendary
}

/// <summary>
///     Battle UI actions that trigger sound feedback.
/// </summary>
public enum BattleUIAction
{
    /// <summary>Battle menu opened.</summary>
    MenuOpen,
    /// <summary>Battle menu closed.</summary>
    MenuClose,
    /// <summary>Menu option selected.</summary>
    Select,
    /// <summary>Back/cancel action.</summary>
    Back,
    /// <summary>Invalid selection attempt.</summary>
    Invalid,
    /// <summary>Target selection mode entered.</summary>
    TargetSelect,
    /// <summary>Item used on Pokemon.</summary>
    ItemUse,
    /// <summary>Pokeball thrown.</summary>
    BallThrow,
    /// <summary>Flee/run from battle.</summary>
    Run
}
