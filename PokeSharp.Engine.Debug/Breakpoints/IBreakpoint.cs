namespace PokeSharp.Engine.Debug.Breakpoints;

/// <summary>
///     Represents a breakpoint that can trigger a game pause when its condition is met.
/// </summary>
public interface IBreakpoint
{
    /// <summary>
    ///     Unique identifier for this breakpoint.
    /// </summary>
    int Id { get; }

    /// <summary>
    ///     Type of breakpoint (Expression, Watch, Log, Entity).
    /// </summary>
    BreakpointType Type { get; }

    /// <summary>
    ///     Human-readable description of the breakpoint condition.
    /// </summary>
    string Description { get; }

    /// <summary>
    ///     Whether the breakpoint is currently enabled.
    /// </summary>
    bool IsEnabled { get; set; }

    /// <summary>
    ///     Number of times this breakpoint has been hit.
    /// </summary>
    int HitCount { get; }

    /// <summary>
    ///     Evaluates whether the breakpoint condition is met.
    /// </summary>
    /// <returns>True if the breakpoint should trigger, false otherwise.</returns>
    Task<bool> EvaluateAsync();

    /// <summary>
    ///     Called when the breakpoint is hit (increments hit count).
    /// </summary>
    void OnHit();

    /// <summary>
    ///     Resets the hit count.
    /// </summary>
    void ResetHitCount();
}

/// <summary>
///     Types of breakpoints.
/// </summary>
public enum BreakpointType
{
    /// <summary>Breakpoint based on C# expression evaluation.</summary>
    Expression,

    /// <summary>Breakpoint triggered when a watch alert fires.</summary>
    WatchAlert,

    /// <summary>Breakpoint triggered on a specific log level.</summary>
    LogLevel,

    // Future breakpoint types (not yet implemented):
    // EntitySpawn - Trigger when an entity with specific tag spawns
    // EntityDestroy - Trigger when an entity is destroyed
}
