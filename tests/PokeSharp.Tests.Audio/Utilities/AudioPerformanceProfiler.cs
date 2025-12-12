using System.Diagnostics;
using PokeSharp.Tests.Audio.Utilities.Interfaces;

namespace PokeSharp.Tests.Audio.Utilities;

/// <summary>
/// Profiles audio system performance metrics
/// </summary>
public class AudioPerformanceProfiler
{
    private readonly Stopwatch _stopwatch = new();
    private readonly List<PerformanceSnapshot> _snapshots = new();

    public void StartProfiling()
    {
        _stopwatch.Restart();
        _snapshots.Clear();
    }

    public void TakeSnapshot(string label, IAudioService audioService)
    {
        var snapshot = new PerformanceSnapshot
        {
            Label = label,
            ElapsedMs = _stopwatch.ElapsedMilliseconds,
            MemoryUsedBytes = GC.GetTotalMemory(false),
            ActiveSoundCount = audioService.GetActiveSoundCount(),
            CachedSoundCount = audioService.GetCachedSoundCount()
        };

        _snapshots.Add(snapshot);
    }

    public void TakeSnapshot(string label, long memoryBytes, int activeSounds, int cachedSounds)
    {
        var snapshot = new PerformanceSnapshot
        {
            Label = label,
            ElapsedMs = _stopwatch.ElapsedMilliseconds,
            MemoryUsedBytes = memoryBytes,
            ActiveSoundCount = activeSounds,
            CachedSoundCount = cachedSounds
        };

        _snapshots.Add(snapshot);
    }

    public PerformanceReport GenerateReport()
    {
        return new PerformanceReport
        {
            Snapshots = _snapshots.ToList(),
            TotalDurationMs = _stopwatch.ElapsedMilliseconds,
            PeakMemoryBytes = _snapshots.Any() ? _snapshots.Max(s => s.MemoryUsedBytes) : 0,
            AverageActiveSounds = _snapshots.Any() ? _snapshots.Average(s => s.ActiveSoundCount) : 0
        };
    }

    public void StopProfiling()
    {
        _stopwatch.Stop();
    }

    public void Reset()
    {
        _stopwatch.Reset();
        _snapshots.Clear();
    }
}

public class PerformanceSnapshot
{
    public string Label { get; set; } = string.Empty;
    public long ElapsedMs { get; set; }
    public long MemoryUsedBytes { get; set; }
    public int ActiveSoundCount { get; set; }
    public int CachedSoundCount { get; set; }
}

public class PerformanceReport
{
    public List<PerformanceSnapshot> Snapshots { get; set; } = new();
    public long TotalDurationMs { get; set; }
    public long PeakMemoryBytes { get; set; }
    public double AverageActiveSounds { get; set; }

    public void PrintReport()
    {
        Console.WriteLine("=== Audio Performance Report ===");
        Console.WriteLine($"Total Duration: {TotalDurationMs}ms");
        Console.WriteLine($"Peak Memory: {PeakMemoryBytes / (1024.0 * 1024.0):F2}MB");
        Console.WriteLine($"Average Active Sounds: {AverageActiveSounds:F2}");
        Console.WriteLine("\nSnapshots:");

        foreach (var snapshot in Snapshots)
        {
            Console.WriteLine($"  [{snapshot.ElapsedMs}ms] {snapshot.Label}");
            Console.WriteLine($"    Memory: {snapshot.MemoryUsedBytes / (1024.0 * 1024.0):F2}MB");
            Console.WriteLine($"    Active: {snapshot.ActiveSoundCount}, Cached: {snapshot.CachedSoundCount}");
        }
    }

    public string GetSummary()
    {
        return $"Duration: {TotalDurationMs}ms, Peak Memory: {PeakMemoryBytes / (1024.0 * 1024.0):F2}MB, " +
               $"Avg Active Sounds: {AverageActiveSounds:F2}";
    }
}
