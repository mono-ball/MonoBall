using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Common.Logging;
using PokeSharp.Engine.Common.Utilities;

namespace PokeSharp.Game.Infrastructure.Diagnostics;

/// <summary>
///     Monitors game performance including frame times, memory usage, and GC statistics.
/// </summary>
public class PerformanceMonitor(ILogger<PerformanceMonitor> logger)
{
    private const float TargetFrameTime = 1000f / 60f; // 60 FPS = 16.67ms per frame
    private const double HighMemoryThresholdMb = 500.0; // Warn above 500MB
    private const int MaxGen0CollectionsPerInterval = 50; // Max GC Gen0 in 5 seconds
    private const int PerformanceLogIntervalFrames = 300; // Log every 5 seconds at 60fps
    private readonly RollingAverage _frameTimeTracker = new(60); // Track last 60 frames (1 second)

    private int _lastGen0Count;
    private int _lastGen1Count;
    private int _lastGen2Count;

    // Public stats for overlay display
    /// <summary>Current FPS (frames per second).</summary>
    public float Fps => _frameTimeTracker.Average > 0 ? 1000f / _frameTimeTracker.Average : 0;

    /// <summary>Average frame time in milliseconds.</summary>
    public float FrameTimeMs => _frameTimeTracker.Average;

    /// <summary>Minimum frame time in the sample window.</summary>
    public float MinFrameTimeMs => _frameTimeTracker.Min;

    /// <summary>Maximum frame time in the sample window.</summary>
    public float MaxFrameTimeMs => _frameTimeTracker.Max;

    /// <summary>Current memory usage in megabytes.</summary>
    public double MemoryMb => GC.GetTotalMemory(false) / 1024.0 / 1024.0;

    /// <summary>Total Gen0 garbage collections.</summary>
    public int Gen0Collections => GC.CollectionCount(0);

    /// <summary>Total Gen1 garbage collections.</summary>
    public int Gen1Collections => GC.CollectionCount(1);

    /// <summary>Total Gen2 garbage collections.</summary>
    public int Gen2Collections => GC.CollectionCount(2);

    /// <summary>Total frame count since start.</summary>
    public ulong FrameCount { get; private set; }

    /// <summary>
    ///     Updates performance metrics for the current frame.
    /// </summary>
    /// <param name="frameTimeMs">Frame time in milliseconds.</param>
    public void Update(float frameTimeMs)
    {
        FrameCount++;
        _frameTimeTracker.Add(frameTimeMs);

        // Warn about slow frames (>50% over budget)
        if (frameTimeMs > TargetFrameTime * 1.5f)
        {
            logger.LogSlowFrame(frameTimeMs, TargetFrameTime);
        }

        // Log frame time statistics every 5 seconds (300 frames at 60fps)
        if (FrameCount % PerformanceLogIntervalFrames == 0)
        {
            float avgMs = _frameTimeTracker.Average;
            float fps = 1000.0f / avgMs;
            logger.LogFramePerformance(avgMs, fps, _frameTimeTracker.Min, _frameTimeTracker.Max);

            // Log memory stats every 5 seconds
            LogMemoryStats();
        }
    }

    /// <summary>
    ///     Logs current memory usage and GC statistics.
    /// </summary>
    private void LogMemoryStats()
    {
        long totalMemoryBytes = GC.GetTotalMemory(false);
        double totalMemoryMb = totalMemoryBytes / 1024.0 / 1024.0;

        int gen0 = GC.CollectionCount(0);
        int gen1 = GC.CollectionCount(1);
        int gen2 = GC.CollectionCount(2);

        // Log memory stats using template
        logger.LogMemoryStatistics(totalMemoryMb, gen0, gen1, gen2);

        // Warn about high memory usage
        if (totalMemoryMb > HighMemoryThresholdMb)
        {
            logger.LogHighMemoryUsage(totalMemoryMb, HighMemoryThresholdMb);
        }

        // Warn about excessive GC activity (more than 10 collections per second)
        int gen0Delta = gen0 - _lastGen0Count;
        int gen1Delta = gen1 - _lastGen1Count;
        int gen2Delta = gen2 - _lastGen2Count;

        if (gen0Delta > MaxGen0CollectionsPerInterval) // >50 Gen0 collections in 5 seconds = >10/sec
        {
            logger.LogWarning(
                "High Gen0 GC activity: {Count} collections in last 5 seconds ({PerSec:F1}/sec)",
                gen0Delta,
                gen0Delta / 5.0
            );
        }

        if (gen2Delta > 0) // Any Gen2 collection is notable
        {
            logger.LogWarning(
                "Gen2 GC occurred: {Count} collections (indicates memory pressure)",
                gen2Delta
            );
        }

        // Update last counts
        _lastGen0Count = gen0;
        _lastGen1Count = gen1;
        _lastGen2Count = gen2;
    }
}
