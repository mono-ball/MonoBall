namespace PokeSharp.Game.Configuration;

/// <summary>
///     Configuration for game window settings.
/// </summary>
public class GameWindowConfig
{
    /// <summary>
    ///     Default window width in pixels.
    /// </summary>
    public int Width { get; set; } = 800;

    /// <summary>
    ///     Default window height in pixels.
    /// </summary>
    public int Height { get; set; } = 600;

    /// <summary>
    ///     Whether the mouse cursor should be visible.
    /// </summary>
    public bool IsMouseVisible { get; set; } = true;

    /// <summary>
    ///     Creates a default window configuration.
    /// </summary>
    public static GameWindowConfig CreateDefault()
    {
        return new GameWindowConfig();
    }

    /// <summary>
    ///     Creates a development configuration with larger window.
    /// </summary>
    public static GameWindowConfig CreateDevelopment()
    {
        return new GameWindowConfig
        {
            Width = 1024,
            Height = 768
        };
    }

    /// <summary>
    ///     Creates a production configuration optimized for standard displays.
    /// </summary>
    public static GameWindowConfig CreateProduction()
    {
        return new GameWindowConfig();
    }
}

