using PokeSharp.Game.Scripting.Services;

namespace PokeSharp.Game.Scripting.Api;

/// <summary>
///     Provides unified access to all scripting API services.
///     This facade simplifies dependency injection by grouping all domain APIs
///     into a single provider that can be injected into script contexts and type scripts.
/// </summary>
public interface IScriptingApiProvider
{
    /// <summary>
    ///     Gets the Player API service for player-related operations.
    /// </summary>
    PlayerApiService Player { get; }

    /// <summary>
    ///     Gets the NPC API service for NPC management operations.
    /// </summary>
    NpcApiService Npc { get; }

    /// <summary>
    ///     Gets the Map API service for map queries and transitions.
    /// </summary>
    MapApiService Map { get; }

    /// <summary>
    ///     Gets the GameState API service for flag and variable management.
    /// </summary>
    GameStateApiService GameState { get; }

    /// <summary>
    ///     Gets the Dialogue API service for showing messages and dialogue.
    /// </summary>
    DialogueApiService Dialogue { get; }

    /// <summary>
    ///     Gets the Effects API service for visual effect management.
    /// </summary>
    EffectApiService Effects { get; }
}
