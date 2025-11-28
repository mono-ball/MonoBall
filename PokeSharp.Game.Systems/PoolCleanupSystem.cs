using Arch.Core;
using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Core.Systems;
using PokeSharp.Engine.Systems.Pooling;

namespace PokeSharp.Game.Systems;

/// <summary>
///     System that monitors pooled entities and handles cleanup.
///     Provides warnings for pool exhaustion and tracks pool health metrics.
/// </summary>
/// <remarks>
///     Priority: 980 (very late update, before relationship system at 990).
///     Runs at the end of each frame to monitor pool status.
/// </remarks>
public class PoolCleanupSystem : SystemBase, IUpdateSystem
{
    private readonly ILogger<PoolCleanupSystem>? _logger;
    private readonly EntityPoolManager _poolManager;

    // Timing for periodic checks (avoid checking every frame)
    private float _checkInterval = 1.0f; // Check every second
    private float _criticalThreshold = 0.95f; // Critical at 95% usage
    private float _timeSinceLastCheck;

    // Configuration thresholds
    private float _warningThreshold = 0.9f; // Warn at 90% usage

    /// <summary>
    ///     Creates a new pool cleanup system.
    /// </summary>
    /// <param name="poolManager">Entity pool manager to monitor</param>
    /// <param name="logger">Optional logger for warnings</param>
    public PoolCleanupSystem(
        EntityPoolManager poolManager,
        ILogger<PoolCleanupSystem>? logger = null
    )
    {
        ArgumentNullException.ThrowIfNull(poolManager);

        _poolManager = poolManager;
        _logger = logger;
    }

    /// <summary>
    ///     Gets the update priority. Lower values execute first.
    ///     Pool cleanup executes at priority 980, very late in the frame.
    /// </summary>
    public int UpdatePriority => SystemPriority.PoolCleanup;

    /// <summary>
    ///     Priority for this system (runs very late to monitor pool health).
    /// </summary>
    public override int Priority => SystemPriority.PoolCleanup;

    public override void Update(World world, float deltaTime)
    {
        // Throttle checks to avoid overhead
        _timeSinceLastCheck += deltaTime;
        if (_timeSinceLastCheck < _checkInterval)
        {
            return;
        }

        _timeSinceLastCheck = 0f;

        // Get pool statistics
        AggregatePoolStatistics stats = _poolManager.GetStatistics();

        // Monitor each pool for health issues
        foreach ((string poolName, PoolStatistics poolStats) in stats.PerPoolStats)
        {
            MonitorPoolHealth(poolName, poolStats);
        }

        // Log overall statistics (debug only)
        if (_logger?.IsEnabled(LogLevel.Debug) ?? false)
        {
            _logger.LogDebug(
                "Pool Manager Stats: {PoolCount} pools, {ActiveCount} active entities, "
                    + "{AvailableCount} available, {ReuseRate:P2} reuse rate",
                stats.TotalPools,
                stats.TotalActive,
                stats.TotalAvailable,
                stats.OverallReuseRate
            );
        }
    }

    /// <summary>
    ///     Configure monitoring thresholds.
    /// </summary>
    /// <param name="warningThreshold">Usage % to trigger warnings (default: 0.9)</param>
    /// <param name="criticalThreshold">Usage % to trigger critical alerts (default: 0.95)</param>
    /// <param name="checkInterval">Seconds between checks (default: 1.0)</param>
    public void ConfigureMonitoring(
        float warningThreshold = 0.9f,
        float criticalThreshold = 0.95f,
        float checkInterval = 1.0f
    )
    {
        if (warningThreshold <= 0f || warningThreshold > 1f)
        {
            throw new ArgumentException("Warning threshold must be between 0 and 1");
        }

        if (criticalThreshold <= warningThreshold || criticalThreshold > 1f)
        {
            throw new ArgumentException(
                "Critical threshold must be between warning threshold and 1"
            );
        }

        if (checkInterval <= 0f)
        {
            throw new ArgumentException("Check interval must be positive");
        }

        _warningThreshold = warningThreshold;
        _criticalThreshold = criticalThreshold;
        _checkInterval = checkInterval;
    }

    private void MonitorPoolHealth(string poolName, PoolStatistics stats)
    {
        // Calculate usage percentage
        float usagePercent = stats.UsagePercent;
        string autoResizeNote = stats.AutoResizeEnabled
            ? " (auto-resize enabled, will expand if needed)"
            : " Consider increasing pool size or optimizing entity lifecycle.";

        // Critical: Pool near exhaustion
        if (usagePercent >= _criticalThreshold)
        {
            if (stats.AutoResizeEnabled)
            {
                _logger?.LogWarning(
                    "Pool '{PoolName}' is {UsagePercent:P0} full "
                        + "({ActiveCount}/{MaxSize} entities active, {AvailableCount} available). "
                        + "Auto-resize enabled - pool will expand if needed. Resized {ResizeCount} times.",
                    poolName,
                    usagePercent,
                    stats.ActiveCount,
                    stats.MaxSize,
                    stats.AvailableCount,
                    stats.ResizeCount
                );
            }
            else
            {
                _logger?.LogError(
                    "CRITICAL: Pool '{PoolName}' is {UsagePercent:P0} full! "
                        + "({ActiveCount}/{MaxSize} entities active, {AvailableCount} available). "
                        + "Consider increasing pool size or enabling auto-resize.",
                    poolName,
                    usagePercent,
                    stats.ActiveCount,
                    stats.MaxSize,
                    stats.AvailableCount
                );
            }
        }
        // Warning: Pool getting full
        else if (usagePercent >= _warningThreshold)
        {
            _logger?.LogWarning(
                "Pool '{PoolName}' is {UsagePercent:P0} full "
                    + "({ActiveCount}/{MaxSize} entities active, {AvailableCount} available).{AutoResizeNote}",
                poolName,
                usagePercent,
                stats.ActiveCount,
                stats.MaxSize,
                stats.AvailableCount,
                autoResizeNote
            );
        }

        // Log performance metrics
        if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
        {
            _logger.LogTrace(
                "Pool '{PoolName}': {ReuseRate:P2} reuse rate, "
                    + "{AcquireTime:F3}ms avg acquire time",
                poolName,
                stats.ReuseRate,
                stats.AverageAcquireTimeMs
            );
        }

        // Detect potential memory leaks (entities not being released)
        if (stats.TotalReleases > 0)
        {
            float releaseRatio = (float)stats.TotalReleases / stats.TotalAcquisitions;
            if (releaseRatio < 0.8f && stats.TotalAcquisitions > 100)
            {
                _logger?.LogWarning(
                    "Pool '{PoolName}' has low release rate ({ReleaseRatio:P0}). "
                        + "Possible memory leak: {Acquisitions} acquisitions but only {Releases} releases.",
                    poolName,
                    releaseRatio,
                    stats.TotalAcquisitions,
                    stats.TotalReleases
                );
            }
        }
    }

    /// <summary>
    ///     Get current pool manager statistics (for debugging/UI).
    /// </summary>
    public AggregatePoolStatistics GetCurrentStatistics()
    {
        return _poolManager.GetStatistics();
    }

    /// <summary>
    ///     Force immediate pool health check (bypasses interval throttling).
    /// </summary>
    public void ForceHealthCheck()
    {
        _timeSinceLastCheck = _checkInterval; // Trigger check on next update
    }
}
