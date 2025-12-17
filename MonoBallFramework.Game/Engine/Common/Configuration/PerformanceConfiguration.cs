namespace MonoBallFramework.Game.Engine.Common.Configuration;

/// <summary>
///     Configuration for system performance monitoring and diagnostics.
///     Controls thresholds for performance warnings and logging behavior.
/// </summary>
public class PerformanceConfiguration
{
    /// <summary>
    ///     Target frame time in milliseconds (default: 16.67ms for 60 FPS).
    /// </summary>
    public float TargetFrameTimeMs { get; set; } = 16.67f;

    /// <summary>
    ///     Threshold percentage of frame budget before warning about slow systems.
    ///     Default: 0.1 (10% of frame budget).
    /// </summary>
    public double SlowSystemThresholdPercent { get; set; } = 0.1;

    /// <summary>
    ///     Minimum number of frames between slow system warnings to avoid spam.
    ///     Default: 300 frames (~5 seconds at 60 FPS).
    /// </summary>
    public ulong SlowSystemWarningCooldownFrames { get; set; } = 300;

    /// <summary>
    ///     Maximum number of cached loggers for NPC behaviors.
    ///     Default: 100 to prevent unbounded memory growth.
    /// </summary>
    public int MaxCachedLoggers { get; set; } = 100;

    /// <summary>
    ///     Default configuration with balanced performance settings.
    /// </summary>
    public static PerformanceConfiguration Default => new();

    /// <summary>
    ///     Configuration optimized for development with aggressive warnings.
    /// </summary>
    public static PerformanceConfiguration Development =>
        new()
        {
            TargetFrameTimeMs = 16.67f,
            SlowSystemThresholdPercent = 0.05, // Warn at 5% instead of 10%
            SlowSystemWarningCooldownFrames = 60, // Warn more frequently (1 second)
            MaxCachedLoggers = 50
        };

    /// <summary>
    ///     Configuration optimized for production with reduced logging overhead.
    /// </summary>
    public static PerformanceConfiguration Production =>
        new()
        {
            TargetFrameTimeMs = 16.67f,
            SlowSystemThresholdPercent = 0.15, // Only warn on very slow systems
            SlowSystemWarningCooldownFrames = 600, // Warn less frequently (10 seconds)
            MaxCachedLoggers = 200 // Allow more caching in production
        };
}
