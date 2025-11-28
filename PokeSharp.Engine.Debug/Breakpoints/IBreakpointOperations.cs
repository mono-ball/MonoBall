using Microsoft.Extensions.Logging;

namespace PokeSharp.Engine.Debug.Breakpoints;

/// <summary>
/// Interface for breakpoint management operations.
/// Provides a clean abstraction for managing conditional breakpoints that can pause the game.
/// </summary>
public interface IBreakpointOperations
{
    /// <summary>
    /// Gets all active breakpoints.
    /// </summary>
    IReadOnlyCollection<IBreakpoint> Breakpoints { get; }

    /// <summary>
    /// Gets or sets whether breakpoint evaluation is enabled globally.
    /// </summary>
    bool IsEnabled { get; set; }

    /// <summary>
    /// Adds an expression breakpoint that triggers when a C# expression becomes true.
    /// </summary>
    /// <param name="expression">The C# expression to evaluate.</param>
    /// <param name="triggerOnChange">If true, only triggers on transition from false to true.</param>
    /// <returns>The ID of the new breakpoint.</returns>
    int AddExpressionBreakpoint(string expression, bool triggerOnChange = true);

    /// <summary>
    /// Adds a log level breakpoint that triggers when a log message of the specified level or higher is logged.
    /// </summary>
    /// <param name="minLevel">The minimum log level to trigger on.</param>
    /// <returns>The ID of the new breakpoint.</returns>
    int AddLogLevelBreakpoint(LogLevel minLevel);

    /// <summary>
    /// Adds a watch alert breakpoint that triggers when a specific watch's alert fires.
    /// </summary>
    /// <param name="watchName">The name of the watch to monitor (or * for any).</param>
    /// <param name="alertChecker">Function that returns true if the watch alert is active.</param>
    /// <returns>The ID of the new breakpoint.</returns>
    int AddWatchAlertBreakpoint(string watchName, Func<bool> alertChecker);

    /// <summary>
    /// Gets a breakpoint by its ID.
    /// </summary>
    /// <param name="id">The breakpoint ID.</param>
    /// <returns>The breakpoint, or null if not found.</returns>
    IBreakpoint? GetBreakpoint(int id);

    /// <summary>
    /// Enables a breakpoint.
    /// </summary>
    /// <param name="id">The breakpoint ID.</param>
    /// <returns>True if the breakpoint was found and enabled.</returns>
    bool EnableBreakpoint(int id);

    /// <summary>
    /// Disables a breakpoint.
    /// </summary>
    /// <param name="id">The breakpoint ID.</param>
    /// <returns>True if the breakpoint was found and disabled.</returns>
    bool DisableBreakpoint(int id);

    /// <summary>
    /// Removes a breakpoint.
    /// </summary>
    /// <param name="id">The breakpoint ID.</param>
    /// <returns>True if the breakpoint was found and removed.</returns>
    bool RemoveBreakpoint(int id);

    /// <summary>
    /// Removes all breakpoints.
    /// </summary>
    void ClearAllBreakpoints();

    /// <summary>
    /// Gets statistics about breakpoints.
    /// </summary>
    /// <returns>A tuple of (Total, Enabled, Disabled, TotalHits).</returns>
    (int Total, int Enabled, int Disabled, int TotalHits) GetStatistics();
}

