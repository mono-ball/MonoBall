namespace PokeSharp.Game.Initialization.Pipeline;

/// <summary>
///     Constants for initialization progress values.
///     Used to track progress through the game initialization pipeline.
/// </summary>
public static class InitializationProgress
{
    /// <summary>
    ///     Progress value when starting initialization (0%).
    /// </summary>
    public const float Start = 0.0f;

    /// <summary>
    ///     Progress value after loading game data (20%).
    /// </summary>
    public const float GameDataLoaded = 0.2f;

    /// <summary>
    ///     Progress value after initializing template cache (35%).
    /// </summary>
    public const float TemplateCacheInitialized = 0.35f;

    /// <summary>
    ///     Progress value after creating asset manager (40%).
    /// </summary>
    public const float AssetManagerCreated = 0.4f;

    /// <summary>
    ///     Progress value after loading sprite manifests (75%).
    /// </summary>
    public const float SpriteManifestsLoaded = 0.75f;

    /// <summary>
    ///     Progress value after initializing game systems (85%).
    /// </summary>
    public const float GameSystemsInitialized = 0.85f;

    /// <summary>
    ///     Progress value after loading initial map (95%).
    /// </summary>
    public const float InitialMapLoaded = 0.95f;

    /// <summary>
    ///     Progress value when initialization is complete (100%).
    /// </summary>
    public const float Complete = 1.0f;
}
