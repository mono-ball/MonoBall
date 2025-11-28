namespace PokeSharp.Game.Initialization.Constants;

/// <summary>
///     Constants for game initialization configuration.
///     Contains default values for paths, map names, and spawn coordinates.
/// </summary>
public static class GameInitializationConstants
{
    /// <summary>
    ///     Default asset root directory path.
    /// </summary>
    public const string DefaultAssetRoot = "Assets";

    /// <summary>
    ///     Default game data directory path (relative to asset root).
    /// </summary>
    public const string DefaultDataPath = "Assets/Data";

    /// <summary>
    ///     Default initial map to load when the game starts.
    /// </summary>
    public const string DefaultInitialMap = "littleroot_town";

    /// <summary>
    ///     Default X coordinate for initial player spawn (in tiles).
    /// </summary>
    public const int DefaultPlayerSpawnX = 20;

    /// <summary>
    ///     Default Y coordinate for initial player spawn (in tiles).
    /// </summary>
    public const int DefaultPlayerSpawnY = 15;

    /// <summary>
    ///     Default content root directory for MonoGame content pipeline.
    /// </summary>
    public const string DefaultContentRoot = "Content";

    /// <summary>
    ///     Default window title for the game.
    /// </summary>
    public const string DefaultWindowTitle = "PokeSharp - Week 1 Demo";
}
