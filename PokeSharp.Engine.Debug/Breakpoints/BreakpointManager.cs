using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Core.Services;
using PokeSharp.Engine.Debug.Console.Scripting;

namespace PokeSharp.Engine.Debug.Breakpoints;

/// <summary>
///     Manages breakpoints and evaluates them to trigger game pauses.
///     Thread-safe for concurrent access during evaluation.
/// </summary>
public class BreakpointManager : IBreakpointOperations
{
    private readonly Dictionary<int, IBreakpoint> _breakpoints = new();
    private readonly object _breakpointsLock = new();
    private readonly ConsoleScriptEvaluator _evaluator;
    private readonly ConsoleGlobals _globals;
    private readonly ILogger? _logger;
    private readonly ITimeControl? _timeControl;
    private bool _isPaused;
    private int _nextId = 1;

    public BreakpointManager(
        ConsoleScriptEvaluator evaluator,
        ConsoleGlobals globals,
        ITimeControl? timeControl,
        ILogger? logger = null
    )
    {
        _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
        _globals = globals ?? throw new ArgumentNullException(nameof(globals));
        _timeControl = timeControl;
        _logger = logger;
    }

    /// <summary>
    ///     Gets all active breakpoints.
    ///     Returns a snapshot to ensure thread safety.
    /// </summary>
    public IReadOnlyCollection<IBreakpoint> Breakpoints
    {
        get
        {
            lock (_breakpointsLock)
            {
                return _breakpoints.Values.ToList();
            }
        }
    }

    /// <summary>
    ///     Gets or sets whether breakpoint evaluation is enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    ///     Adds an expression breakpoint.
    /// </summary>
    public int AddExpressionBreakpoint(string expression, bool triggerOnChange = true)
    {
        lock (_breakpointsLock)
        {
            int id = _nextId++;
            var breakpoint = new ExpressionBreakpoint(id, expression, _evaluator, _globals, _logger)
            {
                TriggerOnChange = triggerOnChange,
            };
            _breakpoints[id] = breakpoint;
            _logger?.LogDebug("Added expression breakpoint #{Id}: {Expression}", id, expression);
            return id;
        }
    }

    /// <summary>
    ///     Adds a log level breakpoint.
    /// </summary>
    public int AddLogLevelBreakpoint(LogLevel minLevel)
    {
        lock (_breakpointsLock)
        {
            int id = _nextId++;
            var breakpoint = new LogLevelBreakpoint(id, minLevel);
            _breakpoints[id] = breakpoint;
            _logger?.LogDebug("Added log level breakpoint #{Id}: {Level}+", id, minLevel);
            return id;
        }
    }

    /// <summary>
    ///     Adds a watch alert breakpoint.
    /// </summary>
    public int AddWatchAlertBreakpoint(string watchName, Func<bool> alertChecker)
    {
        lock (_breakpointsLock)
        {
            int id = _nextId++;
            var breakpoint = new WatchAlertBreakpoint(id, watchName, alertChecker, _logger);
            _breakpoints[id] = breakpoint;
            _logger?.LogDebug("Added watch alert breakpoint #{Id}: {WatchName}", id, watchName);
            return id;
        }
    }

    /// <summary>
    ///     Gets a breakpoint by ID.
    /// </summary>
    public IBreakpoint? GetBreakpoint(int id)
    {
        lock (_breakpointsLock)
        {
            return _breakpoints.TryGetValue(id, out IBreakpoint? bp) ? bp : null;
        }
    }

    /// <summary>
    ///     Removes a breakpoint.
    /// </summary>
    public bool RemoveBreakpoint(int id)
    {
        lock (_breakpointsLock)
        {
            if (_breakpoints.Remove(id))
            {
                _logger?.LogDebug("Removed breakpoint #{Id}", id);
                return true;
            }

            return false;
        }
    }

    /// <summary>
    ///     Removes all breakpoints.
    /// </summary>
    public void ClearAllBreakpoints()
    {
        lock (_breakpointsLock)
        {
            _breakpoints.Clear();
            _logger?.LogDebug("Cleared all breakpoints");
        }
    }

    /// <summary>
    ///     Enables a breakpoint.
    /// </summary>
    public bool EnableBreakpoint(int id)
    {
        lock (_breakpointsLock)
        {
            if (_breakpoints.TryGetValue(id, out IBreakpoint? bp))
            {
                bp.IsEnabled = true;
                return true;
            }

            return false;
        }
    }

    /// <summary>
    ///     Disables a breakpoint.
    /// </summary>
    public bool DisableBreakpoint(int id)
    {
        lock (_breakpointsLock)
        {
            if (_breakpoints.TryGetValue(id, out IBreakpoint? bp))
            {
                bp.IsEnabled = false;
                return true;
            }

            return false;
        }
    }

    /// <summary>
    ///     Gets statistics about breakpoints.
    /// </summary>
    public (int Total, int Enabled, int Disabled, int TotalHits) GetStatistics()
    {
        lock (_breakpointsLock)
        {
            int enabled = _breakpoints.Values.Count(bp => bp.IsEnabled);
            int disabled = _breakpoints.Count - enabled;
            int totalHits = _breakpoints.Values.Sum(bp => bp.HitCount);
            return (_breakpoints.Count, enabled, disabled, totalHits);
        }
    }

    /// <summary>
    ///     Fired when a breakpoint is hit.
    /// </summary>
    public event Action<IBreakpoint>? OnBreakpointHit;

    /// <summary>
    ///     Notifies log level breakpoints of a new log message.
    /// </summary>
    public void NotifyLog(LogLevel level, string message)
    {
        // Take snapshot to avoid holding lock during callbacks
        List<IBreakpoint> snapshot;
        lock (_breakpointsLock)
        {
            snapshot = _breakpoints.Values.ToList();
        }

        foreach (IBreakpoint bp in snapshot)
        {
            if (bp is LogLevelBreakpoint logBp && bp.IsEnabled)
            {
                logBp.OnLogReceived(level, message);
            }
        }
    }

    /// <summary>
    ///     Evaluates all breakpoints synchronously and pauses the game if any condition is met.
    ///     Should be called each frame from the update loop.
    /// </summary>
    public void EvaluateBreakpoints()
    {
        if (!IsEnabled)
        {
            return;
        }

        // Take snapshot to avoid holding lock during evaluation
        List<IBreakpoint> snapshot;
        lock (_breakpointsLock)
        {
            if (_breakpoints.Count == 0)
            {
                return;
            }

            snapshot = _breakpoints.Values.ToList();
        }

        // Don't evaluate if already paused from a previous breakpoint
        // Check _isPaused alone - it's set whenever a breakpoint triggers,
        // regardless of whether _timeControl exists
        if (_isPaused)
        {
            return;
        }

        foreach (IBreakpoint bp in snapshot)
        {
            if (!bp.IsEnabled)
            {
                continue;
            }

            try
            {
                // EvaluateAsync returns synchronously for most breakpoint types
                // (LogLevel and WatchAlert are sync, Expression uses cached result)
                bool shouldTrigger = bp.EvaluateAsync().GetAwaiter().GetResult();

                if (shouldTrigger)
                {
                    bp.OnHit();
                    _logger?.LogInformation(
                        "Breakpoint #{Id} hit: {Description}",
                        bp.Id,
                        bp.Description
                    );

                    // Mark as paused to prevent re-triggering on next frame
                    _isPaused = true;

                    // Pause the game if time control is available
                    _timeControl?.Pause();

                    // Fire event
                    OnBreakpointHit?.Invoke(bp);

                    // Only trigger one breakpoint per frame
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error evaluating breakpoint #{Id}", bp.Id);
            }
        }
    }

    /// <summary>
    ///     Resets the paused state (call when user manually resumes).
    /// </summary>
    public void ResetPausedState()
    {
        _isPaused = false;
    }
}
