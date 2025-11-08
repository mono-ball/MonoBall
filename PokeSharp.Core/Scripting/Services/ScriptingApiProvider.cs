using PokeSharp.Core.ScriptingApi;

namespace PokeSharp.Core.Scripting.Services;

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
    private readonly DialogueApiService _dialogueApi =
        dialogueApi ?? throw new ArgumentNullException(nameof(dialogueApi));

    private readonly EffectApiService _effectApi =
        effectApi ?? throw new ArgumentNullException(nameof(effectApi));

    private readonly GameStateApiService _gameStateApi =
        gameStateApi ?? throw new ArgumentNullException(nameof(gameStateApi));

    private readonly MapApiService _mapApi = mapApi ?? throw new ArgumentNullException(nameof(mapApi));

    private readonly NpcApiService _npcApi = npcApi ?? throw new ArgumentNullException(nameof(npcApi));

    private readonly PlayerApiService _playerApi =
        playerApi ?? throw new ArgumentNullException(nameof(playerApi));

    /// <inheritdoc />
    public PlayerApiService Player => _playerApi;

    /// <inheritdoc />
    public NpcApiService Npc => _npcApi;

    /// <inheritdoc />
    public MapApiService Map => _mapApi;

    /// <inheritdoc />
    public GameStateApiService GameState => _gameStateApi;

    /// <inheritdoc />
    public DialogueApiService Dialogue => _dialogueApi;

    /// <inheritdoc />
    public EffectApiService Effects => _effectApi;
}
