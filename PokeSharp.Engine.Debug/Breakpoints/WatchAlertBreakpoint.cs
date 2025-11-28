using Microsoft.Extensions.Logging;

namespace PokeSharp.Engine.Debug.Breakpoints;

/// <summary>
///     A breakpoint that triggers when a specific watch's alert condition is met.
///     Uses polling via the alertChecker callback during evaluation.
/// </summary>
public class WatchAlertBreakpoint : IBreakpoint
{
    private readonly Func<bool> _alertChecker;
    private readonly ILogger? _logger;
    private bool _lastAlertState;

    public WatchAlertBreakpoint(
        int id,
        string watchName,
        Func<bool> alertChecker,
        ILogger? logger = null
    )
    {
        Id = id;
        WatchName = watchName ?? throw new ArgumentNullException(nameof(watchName));
        _alertChecker = alertChecker ?? throw new ArgumentNullException(nameof(alertChecker));
        _logger = logger;
    }

    public string WatchName { get; }

    public int Id { get; }
    public BreakpointType Type => BreakpointType.WatchAlert;
    public string Description => $"on watch alert '{WatchName}'";
    public bool IsEnabled { get; set; } = true;
    public int HitCount { get; private set; }

    public Task<bool> EvaluateAsync()
    {
        if (!IsEnabled)
        {
            return Task.FromResult(false);
        }

        // Poll the alert condition via callback
        try
        {
            bool isAlert = _alertChecker();

            // Trigger on transition from false to true (edge-triggered)
            bool shouldTrigger = isAlert && !_lastAlertState;
            _lastAlertState = isAlert;

            return Task.FromResult(shouldTrigger);
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Error checking alert for watch '{WatchName}'", WatchName);
            return Task.FromResult(false);
        }
    }

    public void OnHit()
    {
        HitCount++;
    }

    public void ResetHitCount()
    {
        HitCount = 0;
        _lastAlertState = false;
    }
}
