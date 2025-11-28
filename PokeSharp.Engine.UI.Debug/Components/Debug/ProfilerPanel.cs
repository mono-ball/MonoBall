using System.Text;
using PokeSharp.Engine.Systems.Management;
using PokeSharp.Engine.UI.Debug.Components.Base;
using PokeSharp.Engine.UI.Debug.Components.Controls;
using PokeSharp.Engine.UI.Debug.Core;
using PokeSharp.Engine.UI.Debug.Interfaces;
using PokeSharp.Engine.UI.Debug.Layout;
using PokeSharp.Engine.UI.Debug.Utilities;

namespace PokeSharp.Engine.UI.Debug.Components.Debug;

/// <summary>
///     Panel that displays system performance metrics with horizontal bar charts.
///     Shows per-system execution times sorted by impact.
/// </summary>
public class ProfilerPanel : DebugPanelBase, IProfilerOperations
{
    private readonly ProfilerContent _content;

    /// <summary>
    ///     Creates a ProfilerPanel with the specified components.
    ///     Use <see cref="ProfilerPanelBuilder" /> to construct instances.
    /// </summary>
    internal ProfilerPanel(ProfilerContent content, StatusBar statusBar)
        : base(statusBar)
    {
        _content = content;

        Id = "profiler_panel";

        // Content fills space above StatusBar
        _content.Constraint.Anchor = Anchor.StretchTop;

        AddChild(_content);
    }

    public void SetSortMode(ProfilerSortMode mode)
    {
        _content.SetSortMode(mode);
    }

    public ProfilerSortMode GetSortMode()
    {
        return _content.GetSortMode();
    }

    public void SetShowOnlyActive(bool showOnlyActive)
    {
        _content.SetShowOnlyActive(showOnlyActive);
    }

    public bool GetShowOnlyActive()
    {
        return _content.GetShowOnlyActive();
    }

    public void SetRefreshInterval(float intervalSeconds)
    {
        _content.SetRefreshInterval(intervalSeconds);
    }

    public void Refresh()
    {
        _content.Refresh();
    }

    public (int SystemCount, float TotalMs, float MaxSystemMs, string SlowestSystem) GetStatistics()
    {
        return _content.GetStatistics();
    }

    public IEnumerable<string> GetSystemNames()
    {
        return _content.GetSystemNames();
    }

    public (double LastMs, double AvgMs, double MaxMs, long UpdateCount)? GetSystemMetrics(
        string systemName
    )
    {
        return _content.GetSystemMetrics(systemName);
    }

    public void ScrollToTop()
    {
        _content.ScrollToTop();
    }

    public void ScrollToBottom()
    {
        _content.ScrollToBottom();
    }

    public int GetScrollOffset()
    {
        return _content.GetScrollOffset();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Export Methods
    // ═══════════════════════════════════════════════════════════════════════════

    public string ExportToString()
    {
        (int SystemCount, float TotalMs, float MaxSystemMs, string SlowestSystem) stats =
            _content.GetStatistics();
        var sb = new StringBuilder();
        sb.AppendLine($"# Profiler Export - {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"# Total Frame Time: {stats.TotalMs:F2}ms");
        sb.AppendLine();
        sb.AppendLine($"{"System", -40} {"Last(ms)", 10} {"Avg(ms)", 10} {"Max(ms)", 10}");
        sb.AppendLine(new string('-', 72));

        foreach (string name in _content.GetSystemNames())
        {
            (double LastMs, double AvgMs, double MaxMs, long UpdateCount)? m =
                _content.GetSystemMetrics(name);
            if (m.HasValue)
            {
                sb.AppendLine(
                    $"{name, -40} {m.Value.LastMs, 10:F3} {m.Value.AvgMs, 10:F3} {m.Value.MaxMs, 10:F3}"
                );
            }
        }

        return sb.ToString();
    }

    public string ExportToCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine("System,LastMs,AvgMs,MaxMs,UpdateCount");

        foreach (string name in _content.GetSystemNames())
        {
            (double LastMs, double AvgMs, double MaxMs, long UpdateCount)? m =
                _content.GetSystemMetrics(name);
            if (m.HasValue)
            {
                sb.AppendLine(
                    $"\"{name}\",{m.Value.LastMs:F3},{m.Value.AvgMs:F3},{m.Value.MaxMs:F3},{m.Value.UpdateCount}"
                );
            }
        }

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

    // Delegate to content
    public void SetMetricsProvider(Func<IReadOnlyDictionary<string, SystemMetrics>?>? provider)
    {
        _content.SetMetricsProvider(provider);
    }

    protected override void UpdateStatusBar()
    {
        // Handle empty state
        if (!_content.HasProvider)
        {
            SetStatusBar("No profiler provider configured", "");
            return;
        }

        (int SystemCount, float TotalMs, float MaxSystemMs, string SlowestSystem) stats =
            _content.GetStatistics();
        float frameBudgetPercent = stats.TotalMs / _content.TargetFrameTimeMs * 100;
        string statusIndicator =
            frameBudgetPercent > 100 ? NerdFontIcons.StatusError
            : frameBudgetPercent > 80 ? NerdFontIcons.StatusWarning
            : NerdFontIcons.StatusHealthy;
        string statsText =
            $"{statusIndicator} Systems: {stats.SystemCount} | Frame: {stats.TotalMs:F2}ms ({frameBudgetPercent:F0}% of budget)";

        string activeFilter = _content.GetShowOnlyActive() ? "Active only" : "All";
        int refreshRate = 60 / _content.RefreshFrameInterval;
        string hints = $"Sort: {_content.GetSortMode()} | {activeFilter} | ~{refreshRate}fps";

        SetStatusBar(statsText, hints);

        UITheme theme = ThemeManager.Current;
        if (frameBudgetPercent <= 80)
        {
            StatusBar.ResetStatsColor();
        }
        else if (frameBudgetPercent <= 100)
        {
            StatusBar.StatsColor = theme.Warning;
        }
        else
        {
            StatusBar.StatsColor = theme.Error;
        }
    }
}
