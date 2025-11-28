using Microsoft.Extensions.Logging;

namespace PokeSharp.Engine.Debug.Breakpoints;

/// <summary>
/// A breakpoint that triggers when a log message of a specific level or higher is logged.
/// </summary>
public class LogLevelBreakpoint : IBreakpoint
{
    private bool _triggered;
    private string? _lastMessage;

    public int Id { get; }
    public BreakpointType Type => BreakpointType.LogLevel;
    public LogLevel MinLevel { get; }
    public string Description => $"on log {MinLevel}+";
    public bool IsEnabled { get; set; } = true;
    public int HitCount { get; private set; }

    /// <summary>
    /// The last log message that triggered this breakpoint.
    /// </summary>
    public string? LastTriggerMessage => _lastMessage;

    public LogLevelBreakpoint(int id, LogLevel minLevel)
    {
        Id = id;
        MinLevel = minLevel;
    }

    /// <summary>
    /// Called when a log message is received.
    /// </summary>
    public void OnLogReceived(LogLevel level, string message)
    {
        if (!IsEnabled)
            return;

        if (level >= MinLevel)
        {
            _triggered = true;
            _lastMessage = $"[{level}] {message}";
        }
    }

    public Task<bool> EvaluateAsync()
    {
        if (!IsEnabled)
            return Task.FromResult(false);

        // Check and reset the triggered flag
        var result = _triggered;
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
        _lastMessage = null;
    }
}

