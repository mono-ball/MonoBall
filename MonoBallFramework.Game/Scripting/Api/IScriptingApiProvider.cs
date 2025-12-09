namespace MonoBallFramework.Game.Scripting.Api;

/// <summary>
///     Provides unified access to all scripting API services.
///     This facade simplifies dependency injection by grouping all domain APIs
///     into a single provider that can be injected into script contexts and type scripts.
/// </summary>
public interface IScriptingApiProvider
{
    #region Core APIs

    /// <summary>
    ///     Gets the Player API for player-related operations.
    /// </summary>
    IPlayerApi Player { get; }

    /// <summary>
    ///     Gets the NPC API for NPC management operations.
    /// </summary>
    INpcApi Npc { get; }

    /// <summary>
    ///     Gets the Map API for map queries and transitions.
    /// </summary>
    IMapApi Map { get; }

    /// <summary>
    ///     Gets the GameState API for flag and variable management.
    /// </summary>
    IGameStateApi GameState { get; }

    /// <summary>
    ///     Gets the Dialogue API for showing messages and dialogue.
    /// </summary>
    IDialogueApi Dialogue { get; }

    /// <summary>
    ///     Gets the Effects API for visual effect management.
    /// </summary>
    IEffectApi Effects { get; }

    #endregion

    #region Entity & Registry APIs

    /// <summary>
    ///     Gets the Entity API for spawning and managing entities at runtime.
    /// </summary>
    IEntityApi Entity { get; }

    /// <summary>
    ///     Gets the Registry API for querying game definitions and IDs.
    /// </summary>
    IRegistryApi Registry { get; }

    #endregion
}
