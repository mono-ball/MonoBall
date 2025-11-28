using PokeSharp.Engine.UI.Debug.Components.Debug;

namespace PokeSharp.Engine.UI.Debug.Interfaces;

/// <summary>
///     Interface for stats panel operations exposed to commands.
/// </summary>
public interface IStatsOperations
{
    /// <summary>
    ///     Gets current FPS.
    /// </summary>
    float CurrentFps { get; }

    /// <summary>
    ///     Gets current frame time in ms.
    /// </summary>
    float CurrentFrameTimeMs { get; }

    /// <summary>
    ///     Gets current memory usage in MB.
    /// </summary>
    double CurrentMemoryMB { get; }

    /// <summary>
    ///     Gets current entity count.
    /// </summary>
    int CurrentEntityCount { get; }

    /// <summary>
    ///     Gets GC collection counts.
    /// </summary>
    (int Gen0, int Gen1, int Gen2) GCCollections { get; }

    /// <summary>
    ///     Gets frame statistics.
    /// </summary>
    (float Min, float Max, float Avg) FrameTimeStats { get; }

    /// <summary>
    ///     Sets the stats data provider.
    /// </summary>
    void SetStatsProvider(Func<StatsData>? provider);

    /// <summary>
    ///     Forces an immediate refresh of stats.
    /// </summary>
    void Refresh();

    /// <summary>
    ///     Sets the refresh interval in frames.
    /// </summary>
    void SetRefreshInterval(int frameInterval);

    /// <summary>
    ///     Gets the current refresh interval in frames.
    /// </summary>
    int GetRefreshInterval();

    /// <summary>
    ///     Gets consolidated statistics as a tuple.
    /// </summary>
    (
        float Fps,
        float FrameTimeMs,
        double MemoryMB,
        int Entities,
        int Systems,
        int GCDeltaPerSec
    ) GetStatistics();

    /// <summary>
    ///     Exports current stats to a formatted string.
    /// </summary>
    string ExportToString();

    /// <summary>
    ///     Exports stats to CSV format.
    /// </summary>
    string ExportToCsv();

    /// <summary>
    ///     Copies current stats to clipboard.
    /// </summary>
    void CopyToClipboard(bool asCsv = false);
}
