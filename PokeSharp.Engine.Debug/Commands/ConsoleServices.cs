using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Debug.Breakpoints;
using PokeSharp.Engine.Debug.Console.Features;
using PokeSharp.Engine.Debug.Console.Scripting;
using PokeSharp.Engine.Debug.Features;
using PokeSharp.Engine.Debug.Scripting;

namespace PokeSharp.Engine.Debug.Commands;

/// <summary>
///     Aggregates the various services and managers used by the console system.
///     This reduces constructor parameter count and groups related dependencies.
/// </summary>
public class ConsoleServices
{
    public ConsoleServices(
        ConsoleCommandRegistry commandRegistry,
        AliasMacroManager aliasManager,
        ScriptManager scriptManager,
        ConsoleScriptEvaluator scriptEvaluator,
        ConsoleGlobals scriptGlobals,
        BookmarkedCommandsManager bookmarkManager,
        WatchPresetManager watchPresetManager,
        BreakpointManager? breakpointManager = null
    )
    {
        CommandRegistry =
            commandRegistry ?? throw new ArgumentNullException(nameof(commandRegistry));
        AliasManager = aliasManager ?? throw new ArgumentNullException(nameof(aliasManager));
        ScriptManager = scriptManager ?? throw new ArgumentNullException(nameof(scriptManager));
        ScriptEvaluator =
            scriptEvaluator ?? throw new ArgumentNullException(nameof(scriptEvaluator));
        ScriptGlobals = scriptGlobals ?? throw new ArgumentNullException(nameof(scriptGlobals));
        BookmarkManager =
            bookmarkManager ?? throw new ArgumentNullException(nameof(bookmarkManager));
        WatchPresetManager =
            watchPresetManager ?? throw new ArgumentNullException(nameof(watchPresetManager));
        BreakpointManager = breakpointManager; // Optional - can be null
    }

    /// <summary>
    ///     The command registry for looking up and executing commands.
    /// </summary>
    public ConsoleCommandRegistry CommandRegistry { get; }

    /// <summary>
    ///     Manager for command aliases.
    /// </summary>
    public AliasMacroManager AliasManager { get; }

    /// <summary>
    ///     Manager for script file operations.
    /// </summary>
    public ScriptManager ScriptManager { get; }

    /// <summary>
    ///     Evaluator for C# script execution.
    /// </summary>
    public ConsoleScriptEvaluator ScriptEvaluator { get; }

    /// <summary>
    ///     Global variables available in scripts.
    /// </summary>
    public ConsoleGlobals ScriptGlobals { get; }

    /// <summary>
    ///     Manager for F-key command bookmarks.
    /// </summary>
    public BookmarkedCommandsManager BookmarkManager { get; }

    /// <summary>
    ///     Manager for watch configuration presets.
    /// </summary>
    public WatchPresetManager WatchPresetManager { get; }

    /// <summary>
    ///     Manager for conditional breakpoints that pause the game.
    /// </summary>
    public BreakpointManager? BreakpointManager { get; }
}

/// <summary>
///     Callbacks for console logging state management.
///     Groups related logging callbacks to reduce parameter count.
/// </summary>
public class ConsoleLoggingCallbacks
{
    public ConsoleLoggingCallbacks(
        Func<bool> isLoggingEnabled,
        Action<bool> setLoggingEnabled,
        Func<LogLevel> getLogLevel,
        Action<LogLevel> setLogLevel
    )
    {
        IsLoggingEnabled =
            isLoggingEnabled ?? throw new ArgumentNullException(nameof(isLoggingEnabled));
        SetLoggingEnabled =
            setLoggingEnabled ?? throw new ArgumentNullException(nameof(setLoggingEnabled));
        GetLogLevel = getLogLevel ?? throw new ArgumentNullException(nameof(getLogLevel));
        SetLogLevel = setLogLevel ?? throw new ArgumentNullException(nameof(setLogLevel));
    }

    /// <summary>
    ///     Gets whether logging is enabled.
    /// </summary>
    public Func<bool> IsLoggingEnabled { get; }

    /// <summary>
    ///     Sets whether logging is enabled.
    /// </summary>
    public Action<bool> SetLoggingEnabled { get; }

    /// <summary>
    ///     Gets the current minimum log level.
    /// </summary>
    public Func<LogLevel> GetLogLevel { get; }

    /// <summary>
    ///     Sets the minimum log level.
    /// </summary>
    public Action<LogLevel> SetLogLevel { get; }
}
