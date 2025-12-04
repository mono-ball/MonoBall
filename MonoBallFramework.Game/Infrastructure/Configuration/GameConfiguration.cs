namespace MonoBallFramework.Game.Infrastructure.Configuration;

/// <summary>
///     Root configuration class for the game.
///     All game configuration values are bound from appsettings.json.
/// </summary>
public class GameConfiguration
{
    /// <summary>
    ///     Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "Game";

    /// <summary>
    ///     Window configuration settings.
    /// </summary>
    public GameWindowConfig Window { get; set; } = new();

    /// <summary>
    ///     Game initialization configuration.
    /// </summary>
    public GameInitializationConfig Initialization { get; set; } = new();
}

/// <summary>
///     Configuration for game window settings.
///     Default resolution is 1280x800 (5.33x GBA width, 5x GBA height).
/// </summary>
public class GameWindowConfig
{
    /// <summary>
    ///     Window width in pixels. Default: 1280.
    /// </summary>
    public int Width { get; set; } = 1280;

    /// <summary>
    ///     Window height in pixels. Default: 800.
    /// </summary>
    public int Height { get; set; } = 800;

    /// <summary>
    ///     Whether the mouse cursor should be visible.
    /// </summary>
    public bool IsMouseVisible { get; set; } = true;

    /// <summary>
    ///     Window title.
    /// </summary>
    public string Title { get; set; } = "MonoBall Framework - Week 1 Demo";
}

/// <summary>
///     Configuration for game initialization settings.
/// </summary>
public class GameInitializationConfig
{
    /// <summary>
    ///     Root directory for game assets.
    /// </summary>
    public string AssetRoot { get; set; } = "Assets";

    /// <summary>
    ///     Path to game data directory (relative to asset root).
    /// </summary>
    public string DataPath { get; set; } = "Assets/Data";

    /// <summary>
    ///     Content root directory for MonoGame content pipeline.
    /// </summary>
    public string ContentRoot { get; set; } = "Content";

    /// <summary>
    ///     Initial map ID to load when the game starts.
    /// </summary>
    public string InitialMap { get; set; } = string.Empty;

    /// <summary>
    ///     Default X coordinate for initial player spawn (in tiles).
    /// </summary>
    public int PlayerSpawnX { get; set; } = 20;

    /// <summary>
    ///     Default Y coordinate for initial player spawn (in tiles).
    /// </summary>
    public int PlayerSpawnY { get; set; } = 15;
}
