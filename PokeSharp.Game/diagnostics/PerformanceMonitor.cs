using Microsoft.Extensions.Logging;
using PokeSharp.Core.Logging;
using PokeSharp.Core.Utilities;

namespace PokeSharp.Game.Diagnostics;

/// <summary>
///     Monitors game performance including frame times, memory usage, and GC statistics.
/// </summary>
public class PerformanceMonitor(ILogger<PerformanceMonitor> logger)
{
    private const float TargetFrameTime = 1000f / 60f; // 60 FPS = 16.67ms per frame
    private const double HighMemoryThresholdMb = 500.0; // Warn above 500MB
    private const int MaxGen0CollectionsPerInterval = 50; // Max GC Gen0 in 5 seconds
    private const int PerformanceLogIntervalFrames = 300; // Log every 5 seconds at 60fps

    private readonly ILogger<PerformanceMonitor> _logger = logger;
    private readonly RollingAverage _frameTimeTracker = new(60); // Track last 60 frames (1 second)

    private ulong _frameCounter;
    private int _lastGen0Count;
    private int _lastGen1Count;
    private int _lastGen2Count;

    /// <summary>
    ///     Updates performance metrics for the current frame.
    /// </summary>
    /// <param name="frameTimeMs">Frame time in milliseconds.</param>
    public void Update(float frameTimeMs)
    {
        _frameCounter++;
        _frameTimeTracker.Add(frameTimeMs);

        // Warn about slow frames (>50% over budget)
        if (frameTimeMs > TargetFrameTime * 1.5f)
        {
            _logger.LogSlowFrame(frameTimeMs, TargetFrameTime);
        }

        // Log frame time statistics every 5 seconds (300 frames at 60fps)
        if (_frameCounter % PerformanceLogIntervalFrames == 0)
        {
            var avgMs = _frameTimeTracker.Average;
            var fps = 1000.0f / avgMs;
            _logger.LogFramePerformance(avgMs, fps, _frameTimeTracker.Min, _frameTimeTracker.Max);

            // Log memory stats every 5 seconds
            LogMemoryStats();
        }
    }

    /// <summary>
    ///     Logs current memory usage and GC statistics.
    /// </summary>
    private void LogMemoryStats()
    {
        var totalMemoryBytes = GC.GetTotalMemory(false);
        var totalMemoryMb = totalMemoryBytes / 1024.0 / 1024.0;

        var gen0 = GC.CollectionCount(0);
        var gen1 = GC.CollectionCount(1);
        var gen2 = GC.CollectionCount(2);

        // Log memory stats using template
        _logger.LogMemoryStatistics(totalMemoryMb, gen0, gen1, gen2);

        // Warn about high memory usage
        if (totalMemoryMb > HighMemoryThresholdMb)
        {
            _logger.LogHighMemoryUsage(totalMemoryMb, HighMemoryThresholdMb);
        }

        // Warn about excessive GC activity (more than 10 collections per second)
        var gen0Delta = gen0 - _lastGen0Count;
        var gen1Delta = gen1 - _lastGen1Count;
        var gen2Delta = gen2 - _lastGen2Count;

        if (gen0Delta > MaxGen0CollectionsPerInterval) // >50 Gen0 collections in 5 seconds = >10/sec
        {
            _logger.LogWarning(
                "High Gen0 GC activity: {Count} collections in last 5 seconds ({PerSec:F1}/sec)",
                gen0Delta,
                gen0Delta / 5.0
            );
        }

        if (gen2Delta > 0) // Any Gen2 collection is notable
        {
            _logger.LogWarning(
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

