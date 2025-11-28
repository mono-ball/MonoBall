using Microsoft.Xna.Framework;
using PokeSharp.Engine.Core.Services;
using PokeSharp.Engine.Debug.Breakpoints;
using PokeSharp.Engine.UI.Debug.Components.Debug;
using PokeSharp.Engine.UI.Debug.Core;
using PokeSharp.Engine.UI.Debug.Interfaces;

namespace PokeSharp.Engine.Debug.Commands;

/// <summary>
/// Provides context and services for console command execution.
/// This interface exposes core console operations and panel interfaces as properties.
/// </summary>
/// <remarks>
/// <para>
/// Core operations are exposed directly on this interface:
/// - <see cref="IConsoleOutput"/> - Basic output operations
/// - <see cref="IConsoleLogging"/> - Logging control
/// - <see cref="IConsoleCommands"/> - Command registry
/// - <see cref="IConsoleHistory"/> - Command history
/// - <see cref="IConsoleAliases"/> - Alias management
/// - <see cref="IConsoleScripts"/> - Script operations
/// - <see cref="IConsoleBookmarks"/> - Bookmark management
/// - <see cref="IConsoleNavigation"/> - Tab navigation
/// - <see cref="IConsoleExport"/> - Console output export
/// </para>
/// <para>
/// Optional features exposed as nullable properties:
/// - <see cref="TimeControl"/> - Game time control (pause, step, speed) - null if unavailable
/// </para>
/// <para>
/// Panel operations are exposed as properties to reduce boilerplate delegation:
/// - <see cref="Entities"/> - Entity browser operations
/// - <see cref="Watches"/> - Watch panel operations
/// - <see cref="Variables"/> - Variables panel operations
/// - <see cref="Logs"/> - Logs panel operations
/// </para>
/// <para>
/// Commands use panel properties directly: <c>context.Entities.Refresh()</c> instead of <c>context.RefreshEntities()</c>
/// </para>
/// </remarks>
public interface IConsoleContext :
    IConsoleOutput,
    IConsoleLogging,
    IConsoleCommands,
    IConsoleHistory,
    IConsoleAliases,
    IConsoleScripts,
    IConsoleBookmarks,
    IConsoleNavigation,
    IConsoleExport
{
    /// <summary>
    /// Gets the time control interface, or null if time control is not available.
    /// </summary>
    /// <remarks>
    /// Time control may be unavailable if:
    /// - ITimeControl service is not registered in DI
    /// - The game doesn't support time manipulation
    /// Commands should check for null before using time control features.
    /// </remarks>
    /// <example>
    /// <code>
    /// if (context.TimeControl != null)
    /// {
    ///     context.TimeControl.Pause();
    /// }
    /// </code>
    /// </example>
    ITimeControl? TimeControl { get; }

    /// <summary>
    /// Gets the breakpoint operations for conditional game pausing.
    /// </summary>
    /// <remarks>
    /// May be null if:
    /// - BreakpointManager was not initialized
    /// - Required dependencies (ScriptEvaluator, ConsoleGlobals) are unavailable
    /// </remarks>
    /// <example>
    /// <code>
    /// context.Breakpoints?.AddExpressionBreakpoint("Player.Health &lt; 20");
    /// </code>
    /// </example>
    IBreakpointOperations? Breakpoints { get; }

    /// <summary>
    /// Gets the entity browser operations.
    /// Use for entity inspection, filtering, and management.
    /// </summary>
    /// <remarks>Always available - panels are created with the console.</remarks>
    /// <example>
    /// <code>
    /// context.Entities.Refresh();
    /// context.Entities.SetTagFilter("Player");
    /// </code>
    /// </example>
    IEntityOperations Entities { get; }

    /// <summary>
    /// Gets the watch panel operations.
    /// Use for adding, removing, and managing watch expressions.
    /// </summary>
    /// <remarks>Always available - panels are created with the console.</remarks>
    /// <example>
    /// <code>
    /// context.Watches.Add("playerPos", "player.Position", () => player.Position);
    /// context.Watches.Pin("playerPos");
    /// </code>
    /// </example>
    IWatchOperations Watches { get; }

    /// <summary>
    /// Gets the variables panel operations.
    /// Use for viewing and managing script variables.
    /// </summary>
    /// <remarks>Always available - panels are created with the console.</remarks>
    /// <example>
    /// <code>
    /// context.Variables.SetSearchFilter("position");
    /// context.Variables.Expand("player");
    /// </code>
    /// </example>
    IVariableOperations Variables { get; }

    /// <summary>
    /// Gets the logs panel operations.
    /// Use for filtering and exporting logs.
    /// </summary>
    /// <remarks>Always available - panels are created with the console.</remarks>
    /// <example>
    /// <code>
    /// context.Logs.SetFilterLevel(LogLevel.Warning);
    /// context.Logs.SetSearch("error");
    /// </code>
    /// </example>
    ILogOperations Logs { get; }

    /// <summary>
    /// Gets the profiler panel operations, or null if metrics provider not available.
    /// Use for viewing and sorting system performance metrics.
    /// </summary>
    /// <remarks>
    /// May be null if:
    /// - SystemMetrics provider not configured
    /// - SystemPerformanceTracker not available
    /// </remarks>
    /// <example>
    /// <code>
    /// if (context.Profiler != null)
    /// {
    ///     context.Profiler.SetSortMode(ProfilerSortMode.ByExecutionTime);
    ///     context.Profiler.Refresh();
    /// }
    /// </code>
    /// </example>
    IProfilerOperations? Profiler { get; }

    /// <summary>
    /// Gets the stats panel operations, or null if stats provider not available.
    /// Use for viewing performance statistics (FPS, memory, GC).
    /// </summary>
    /// <example>
    /// <code>
    /// if (context.Stats != null)
    /// {
    ///     var fps = context.Stats.CurrentFps;
    ///     var mem = context.Stats.CurrentMemoryMB;
    /// }
    /// </code>
    /// </example>
    IStatsOperations? Stats { get; }

    // ═══════════════════════════════════════════════════════════════════════════
    // Expression-based Watch Operations (require script evaluation)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Adds a watch using an expression string that will be evaluated.
    /// Use this from commands. For direct panel access with value getters, use <see cref="Watches"/>.
    /// </summary>
    bool AddWatch(string name, string expression);

    /// <summary>
    /// Adds a watch with expression, group, and condition.
    /// The expression will be evaluated using the script engine.
    /// </summary>
    bool AddWatch(string name, string expression, string? group, string? condition);

    // ═══════════════════════════════════════════════════════════════════════════
    // Watch Preset Operations (managed by WatchPresetManager)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Saves current watches as a preset.</summary>
    bool SaveWatchPreset(string name, string description);

    /// <summary>Loads a watch preset by name.</summary>
    bool LoadWatchPreset(string name);

    /// <summary>Lists all watch presets.</summary>
    IEnumerable<(string Name, string Description, int WatchCount, DateTime CreatedAt)> ListWatchPresets();

    /// <summary>Deletes a watch preset.</summary>
    bool DeleteWatchPreset(string name);

    /// <summary>Checks if a preset exists.</summary>
    bool WatchPresetExists(string name);

    /// <summary>Creates built-in watch presets.</summary>
    void CreateBuiltInWatchPresets();
}
