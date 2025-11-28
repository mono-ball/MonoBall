using PokeSharp.Game.Scripting.Api;

namespace PokeSharp.Game.Scripting.Services;

/// <summary>
///     Default implementation of IScriptingApiProvider that aggregates all domain API services.
///     This facade simplifies dependency injection by grouping all scripting APIs into a single provider.
/// </summary>
public class ScriptingApiProvider(
    PlayerApiService playerApi,
    NpcApiService npcApi,
    MapApiService mapApi,
    GameStateApiService gameStateApi,
    DialogueApiService dialogueApi,
    EffectApiService effectApi
) : IScriptingApiProvider
{
    /// <inheritdoc />
    public PlayerApiService Player { get; } =
        playerApi ?? throw new ArgumentNullException(nameof(playerApi));

    /// <inheritdoc />
    public NpcApiService Npc { get; } = npcApi ?? throw new ArgumentNullException(nameof(npcApi));

    /// <inheritdoc />
    public MapApiService Map { get; } = mapApi ?? throw new ArgumentNullException(nameof(mapApi));

    /// <inheritdoc />
    public GameStateApiService GameState { get; } =
        gameStateApi ?? throw new ArgumentNullException(nameof(gameStateApi));

    /// <inheritdoc />
    public DialogueApiService Dialogue { get; } =
        dialogueApi ?? throw new ArgumentNullException(nameof(dialogueApi));

    /// <inheritdoc />
    public EffectApiService Effects { get; } =
        effectApi ?? throw new ArgumentNullException(nameof(effectApi));
}
