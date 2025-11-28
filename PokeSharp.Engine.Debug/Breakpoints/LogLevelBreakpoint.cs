using Microsoft.Extensions.Logging;

namespace PokeSharp.Engine.Debug.Breakpoints;

/// <summary>
///     A breakpoint that triggers when a log message of a specific level or higher is logged.
/// </summary>
public class LogLevelBreakpoint : IBreakpoint
{
    private bool _triggered;

    public LogLevelBreakpoint(int id, LogLevel minLevel)
    {
        Id = id;
        MinLevel = minLevel;
    }

    public LogLevel MinLevel { get; }

    /// <summary>
    ///     The last log message that triggered this breakpoint.
    /// </summary>
    public string? LastTriggerMessage { get; private set; }

    public int Id { get; }
    public BreakpointType Type => BreakpointType.LogLevel;
    public string Description => $"on log {MinLevel}+";
    public bool IsEnabled { get; set; } = true;
    public int HitCount { get; private set; }

    public Task<bool> EvaluateAsync()
    {
        if (!IsEnabled)
        {
            return Task.FromResult(false);
        }

        // Check and reset the triggered flag
        bool result = _triggered;
        _triggered = false;
        return Task.FromResult(result);
    }

    public void OnHit()
    {
        HitCount++;
    }

    public void ResetHitCount()
    {
        HitCount = 0;
        _triggered = false;
        LastTriggerMessage = null;
    }

    /// <summary>
    ///     Called when a log message is received.
    /// </summary>
    public void OnLogReceived(LogLevel level, string message)
    {
        if (!IsEnabled)
        {
            return;
        }

        if (level >= MinLevel)
        {
            _triggered = true;
            LastTriggerMessage = $"[{level}] {message}";
        }
    }
}
