using MonoBallFramework.Game.Scripting.Api;

namespace MonoBallFramework.Game.Scripting.Services;

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
    EntityApiService entityApi,
    RegistryApiService registryApi,
    CustomTypesApiService customTypesApi
) : IScriptingApiProvider
{
    /// <inheritdoc />
    public IPlayerApi Player { get; } =
        playerApi ?? throw new ArgumentNullException(nameof(playerApi));

    /// <inheritdoc />
    public INpcApi Npc { get; } = npcApi ?? throw new ArgumentNullException(nameof(npcApi));

    /// <inheritdoc />
    public IMapApi Map { get; } = mapApi ?? throw new ArgumentNullException(nameof(mapApi));

    /// <inheritdoc />
    public IGameStateApi GameState { get; } =
        gameStateApi ?? throw new ArgumentNullException(nameof(gameStateApi));

    /// <inheritdoc />
    public IDialogueApi Dialogue { get; } =
        dialogueApi ?? throw new ArgumentNullException(nameof(dialogueApi));

    /// <inheritdoc />
    public IEntityApi Entity { get; } =
        entityApi ?? throw new ArgumentNullException(nameof(entityApi));

    /// <inheritdoc />
    public IRegistryApi Registry { get; } =
        registryApi ?? throw new ArgumentNullException(nameof(registryApi));

    /// <inheritdoc />
    public ICustomTypesApi CustomTypes { get; } =
        customTypesApi ?? throw new ArgumentNullException(nameof(customTypesApi));
}
