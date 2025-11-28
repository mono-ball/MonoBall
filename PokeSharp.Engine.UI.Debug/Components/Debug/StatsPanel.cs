using System.Text;
using Microsoft.Xna.Framework;
using PokeSharp.Engine.UI.Debug.Components.Base;
using PokeSharp.Engine.UI.Debug.Components.Controls;
using PokeSharp.Engine.UI.Debug.Core;
using PokeSharp.Engine.UI.Debug.Interfaces;
using PokeSharp.Engine.UI.Debug.Layout;
using PokeSharp.Engine.UI.Debug.Utilities;

namespace PokeSharp.Engine.UI.Debug.Components.Debug;

/// <summary>
///     Panel that displays comprehensive performance and system statistics.
///     Uses a clean layout with progress bars and sparklines.
/// </summary>
public class StatsPanel : DebugPanelBase, IStatsOperations
{
    private readonly StatsContent _content;

    /// <summary>
    ///     Creates a StatsPanel with the specified components.
    ///     Use <see cref="StatsPanelBuilder" /> to construct instances.
    /// </summary>
    internal StatsPanel(StatsContent content, StatusBar statusBar)
        : base(statusBar)
    {
        _content = content;

        Id = "stats_panel";

        // Content fills space above StatusBar
        _content.Constraint.Anchor = Anchor.StretchTop;

        AddChild(_content);
    }

    // Delegate to content
    public void SetStatsProvider(Func<StatsData>? provider)
    {
        _content.SetStatsProvider(provider);
    }

    public void Refresh()
    {
        _content.Refresh();
    }

    public void SetRefreshInterval(int frameInterval)
    {
        _content.SetRefreshInterval(frameInterval);
    }

    public int GetRefreshInterval()
    {
        return _content.GetRefreshInterval();
    }

    // IStatsOperations implementation
    public float CurrentFps => _content.CurrentFps;
    public float CurrentFrameTimeMs => _content.CurrentFrameTimeMs;
    public double CurrentMemoryMB => _content.CurrentMemoryMB;
    public int CurrentEntityCount => _content.CurrentEntityCount;
    public (int Gen0, int Gen1, int Gen2) GCCollections => _content.GCCollections;
    public (float Min, float Max, float Avg) FrameTimeStats => _content.FrameTimeStats;

    // ═══════════════════════════════════════════════════════════════════════════
    // Export Methods
    // ═══════════════════════════════════════════════════════════════════════════

    public (
        float Fps,
        float FrameTimeMs,
        double MemoryMB,
        int Entities,
        int Systems,
        int GCDeltaPerSec
    ) GetStatistics()
    {
        (int Gen0Delta, int Gen1Delta, int Gen2Delta) deltas = _content.GCDeltas;
        return (
            _content.CurrentFps,
            _content.CurrentFrameTimeMs,
            _content.CurrentMemoryMB,
            _content.CurrentEntityCount,
            0, // SystemCount not available from content
            deltas.Gen0Delta + deltas.Gen1Delta + deltas.Gen2Delta
        );
    }

    public string ExportToString()
    {
        (float Min, float Max, float Avg) stats = _content.FrameTimeStats;
        (int Gen0, int Gen1, int Gen2) gc = _content.GCCollections;
        (int Gen0Delta, int Gen1Delta, int Gen2Delta) deltas = _content.GCDeltas;

        var sb = new StringBuilder();
        sb.AppendLine($"# Stats Export - {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine($"FPS:           {_content.CurrentFps:F1}");
        sb.AppendLine(
            $"Frame Time:    {_content.CurrentFrameTimeMs:F2}ms (min: {stats.Min:F2}, max: {stats.Max:F2})"
        );
        sb.AppendLine($"Memory:        {_content.CurrentMemoryMB:F1} MB");
        sb.AppendLine($"GC Gen0:       {gc.Gen0} (+{deltas.Gen0Delta}/s)");
        sb.AppendLine($"GC Gen1:       {gc.Gen1} (+{deltas.Gen1Delta}/s)");
        sb.AppendLine($"GC Gen2:       {gc.Gen2} (+{deltas.Gen2Delta}/s)");
        sb.AppendLine($"Entities:      {_content.CurrentEntityCount:N0}");
        return sb.ToString();
    }

    public string ExportToCsv()
    {
        (float Min, float Max, float Avg) stats = _content.FrameTimeStats;
        (int Gen0, int Gen1, int Gen2) gc = _content.GCCollections;
        (int Gen0Delta, int Gen1Delta, int Gen2Delta) deltas = _content.GCDeltas;

        var sb = new StringBuilder();
        sb.AppendLine("Metric,Value,Unit");
        sb.AppendLine($"FPS,{_content.CurrentFps:F1},fps");
        sb.AppendLine($"FrameTime,{_content.CurrentFrameTimeMs:F2},ms");
        sb.AppendLine($"FrameTimeMin,{stats.Min:F2},ms");
        sb.AppendLine($"FrameTimeMax,{stats.Max:F2},ms");
        sb.AppendLine($"Memory,{_content.CurrentMemoryMB:F1},MB");
        sb.AppendLine($"GCGen0,{gc.Gen0},collections");
        sb.AppendLine($"GCGen1,{gc.Gen1},collections");
        sb.AppendLine($"GCGen2,{gc.Gen2},collections");
        sb.AppendLine($"GCDeltaGen0,{deltas.Gen0Delta},per_second");
        sb.AppendLine($"GCDeltaGen1,{deltas.Gen1Delta},per_second");
        sb.AppendLine($"GCDeltaGen2,{deltas.Gen2Delta},per_second");
        sb.AppendLine($"Entities,{_content.CurrentEntityCount},count");
        return sb.ToString();
    }

    public void CopyToClipboard(bool asCsv = false)
    {
        string text = asCsv ? ExportToCsv() : ExportToString();
        ClipboardManager.SetText(text);
    }

    protected override UIComponent GetContentComponent()
    {
        return _content;
    }

    protected override void UpdateStatusBar()
    {
        // Handle empty state
        if (!_content.HasProvider)
        {
            SetStatusBar("No stats provider configured", "");
            return;
        }

        UITheme theme = ThemeManager.Current;
        (string indicator, Color color, bool isHealthy) health = _content.GetOverallHealth(theme);
        (int Gen0Delta, int Gen1Delta, int Gen2Delta) deltas = _content.GCDeltas;

        string stats =
            $"{health.indicator} FPS: {_content.CurrentFps:F0} | Frame: {_content.CurrentFrameTimeMs:F1}ms | Mem: {_content.CurrentMemoryMB:F0}MB";

        string gcActivity =
            deltas.Gen2Delta > 0 ? "GC: Active"
            : deltas.Gen1Delta > 0 ? "GC: Minor"
            : "";
        int refreshRate = 60 / _content.GetRefreshInterval();
        string hints = $"Refresh: ~{refreshRate}fps";
        if (!string.IsNullOrEmpty(gcActivity))
        {
            hints = $"{gcActivity} | {hints}";
        }

        SetStatusBar(stats, hints);

        if (health.isHealthy)
        {
            StatusBar.ResetStatsColor();
        }
        else
        {
            StatusBar.StatsColor = health.color;
        }
    }
}

/// <summary>
///     Data structure for stats panel metrics.
/// </summary>
public class StatsData
{
    // Performance metrics
    public float Fps { get; set; } = 60f;
    public float FrameTimeMs { get; set; } = 16.67f;
    public float MinFrameTimeMs { get; set; } = 16f;
    public float MaxFrameTimeMs { get; set; } = 17f;
    public double MemoryMB { get; set; } = 128;
    public int Gen0Collections { get; set; }
    public int Gen1Collections { get; set; }
    public int Gen2Collections { get; set; }
    public ulong FrameNumber { get; set; }

    // ECS Entity stats
    public int EntityCount { get; set; }
    public int ArchetypeCount { get; set; }

    // ECS System stats
    public int SystemCount { get; set; }
    public double TotalSystemTimeMs { get; set; }
    public string? SlowestSystemName { get; set; }
    public double SlowestSystemTimeMs { get; set; }

    // Pool stats
    public int PoolCount { get; set; }
    public int PooledActive { get; set; }
    public int PooledAvailable { get; set; }
    public float PoolReuseRate { get; set; }
}
