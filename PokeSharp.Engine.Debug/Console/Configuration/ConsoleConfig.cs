using Microsoft.Extensions.Logging;

namespace PokeSharp.Engine.Debug.Console.Configuration;

/// <summary>
/// Console size configuration.
/// </summary>
public enum ConsoleSize
{
    /// <summary>
    /// Small console (25% of screen height).
    /// </summary>
    Small,

    /// <summary>
    /// Medium console (50% of screen height).
    /// </summary>
    Medium,

    /// <summary>
    /// Full console (100% of screen height).
    /// </summary>
    Full
}

/// <summary>
/// Console configuration settings.
/// Immutable record - use 'with' expressions to create modified copies.
/// </summary>
public record ConsoleConfig
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "DebugConsole";

    /// <summary>
    /// Gets the console size.
    /// </summary>
    public ConsoleSize Size { get; init; } = ConsoleSize.Full;

    /// <summary>
    /// Gets the UI theme name.
    /// Available themes: onedark, monokai, dracula, gruvbox, nord, solarized, solarized-light, pokeball
    /// </summary>
    public string Theme { get; init; } = "pokeball";

    /// <summary>
    /// Gets whether syntax highlighting is enabled.
    /// </summary>
    public bool SyntaxHighlightingEnabled { get; init; } = true;

    /// <summary>
    /// Gets whether auto-completion is enabled.
    /// </summary>
    public bool AutoCompleteEnabled { get; init; } = true;

    /// <summary>
    /// Gets whether command history persistence is enabled.
    /// </summary>
    public bool PersistHistory { get; init; } = true;

    /// <summary>
    /// Gets the font size (points).
    /// </summary>
    public int FontSize { get; init; } = 16;

    /// <summary>
    /// Gets whether console logging is enabled.
    /// Redirects game logs to the console output.
    /// </summary>
    public bool LoggingEnabled { get; init; } = false; // Off by default

    /// <summary>
    /// Gets the minimum log level to display in the console.
    /// </summary>
    public LogLevel MinimumLogLevel { get; init; } = LogLevel.Information;

    /// <summary>
    /// Gets whether to auto-load startup script on console initialization.
    /// </summary>
    public bool AutoLoadStartupScript { get; init; } = true; // On by default

    /// <summary>
    /// Gets the name of the startup script file.
    /// </summary>
    public string StartupScriptName { get; init; } = "startup.csx";

    /// <summary>
    /// Gets the height multiplier for the given console size.
    /// </summary>
    public float GetHeightMultiplier() => Size switch
    {
        ConsoleSize.Small => ConsoleConstants.Size.SmallMultiplier,
        ConsoleSize.Medium => ConsoleConstants.Size.MediumMultiplier,
        ConsoleSize.Full => ConsoleConstants.Size.FullMultiplier,
        _ => ConsoleConstants.Size.MediumMultiplier
    };

    /// <summary>
    /// Creates a new config with the specified size.
    /// </summary>
    public ConsoleConfig WithSize(ConsoleSize size) => this with { Size = size };

    /// <summary>
    /// Creates a new config with logging enabled/disabled.
    /// </summary>
    public ConsoleConfig WithLogging(bool enabled) => this with { LoggingEnabled = enabled };

    /// <summary>
    /// Creates a new config with the specified minimum log level.
    /// </summary>
    public ConsoleConfig WithMinimumLogLevel(LogLevel level) => this with { MinimumLogLevel = level };
}

