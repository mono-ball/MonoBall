using System.Text;
using MonoBallFramework.Game.Engine.UI.Components.Base;
using MonoBallFramework.Game.Engine.UI.Components.Controls;
using MonoBallFramework.Game.Engine.UI.Core;
using MonoBallFramework.Game.Engine.UI.Interfaces;
using MonoBallFramework.Game.Engine.UI.Layout;
using MonoBallFramework.Game.Engine.UI.Models;
using MonoBallFramework.Game.Engine.UI.Utilities;

namespace MonoBallFramework.Game.Engine.UI.Components.Debug;

/// <summary>
///     Debug panel for inspecting event bus activity.
///     Shows registered events, subscriptions, and performance metrics in real-time.
/// </summary>
public class EventInspectorPanel : DebugPanelBase, IEventInspectorOperations
{
    private readonly EventInspectorContent _content;

    /// <summary>
    ///     Creates an EventInspectorPanel with the specified components.
    ///     Use <see cref="EventInspectorPanelBuilder" /> to construct instances.
    /// </summary>
    internal EventInspectorPanel(EventInspectorContent content, StatusBar statusBar)
        : base(statusBar)
    {
        _content = content;
        Id = "event_inspector_panel";

        // Content fills space above StatusBar
        _content.Constraint.Anchor = Anchor.StretchTop;
        AddChild(_content);
    }

    public bool HasProvider => _content.HasProvider;

    /// <summary>
    ///     Sets the data provider function for event inspector data.
    /// </summary>
    public void SetDataProvider(Func<EventInspectorData>? provider)
    {
        _content.SetDataProvider(provider);
    }

    /// <summary>
    ///     Refreshes the event inspector display immediately.
    /// </summary>
    public void Refresh()
    {
        _content.Refresh();
    }

    /// <summary>
    ///     Sets the refresh interval in frames.
    /// </summary>
    public void SetRefreshInterval(int frameInterval)
    {
        _content.SetRefreshInterval(frameInterval);
    }

    /// <summary>
    ///     Gets the current refresh interval.
    /// </summary>
    public int GetRefreshInterval()
    {
        return _content.GetRefreshInterval();
    }

    /// <summary>
    ///     Toggles subscription details visibility.
    /// </summary>
    public void ToggleSubscriptions()
    {
        _content.ToggleSubscriptions();
    }

    /// <summary>
    ///     Selects the next event in the list.
    /// </summary>
    public void SelectNextEvent()
    {
        _content.SelectNextEvent();
    }

    /// <summary>
    ///     Selects the previous event in the list.
    /// </summary>
    public void SelectPreviousEvent()
    {
        _content.SelectPreviousEvent();
    }

    /// <summary>
    ///     Scrolls the content up.
    /// </summary>
    public void ScrollUp(int lines = 1)
    {
        _content.ScrollUp(lines);
    }

    /// <summary>
    ///     Scrolls the content down.
    /// </summary>
    public void ScrollDown(int lines = 1)
    {
        _content.ScrollDown(lines);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Statistics & Export Methods
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    ///     Gets consolidated statistics about tracked events.
    /// </summary>
    public (
        int EventCount,
        int TotalSubscribers,
        double SlowestEventMs,
        string SlowestEventName
        ) GetStatistics()
    {
        return _content.GetStatistics();
    }

    /// <summary>
    ///     Gets the current event inspector data including all event types.
    /// </summary>
    public EventInspectorData GetData()
    {
        return _content.GetCurrentData();
    }

    /// <summary>
    ///     Exports event inspector data to a formatted string.
    /// </summary>
    public string ExportToString()
    {
        (
            int EventCount,
            int TotalSubscribers,
            double SlowestEventMs,
            string SlowestEventName
            ) stats = _content.GetStatistics();
        var sb = new StringBuilder();
        sb.AppendLine($"# Event Inspector Export - {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"# Total Events: {stats.EventCount}");
        sb.AppendLine($"# Total Subscribers: {stats.TotalSubscribers}");
        sb.AppendLine($"# Slowest Event: {stats.SlowestEventName} ({stats.SlowestEventMs:F3}ms)");
        sb.AppendLine();
        sb.AppendLine($"{"Event Type",-50} {"Subscribers",12} {"Avg (ms)",12} {"Max (ms)",12}");
        sb.AppendLine(new string('-', 88));

        // Note: Full event list would require caching in content
        // For now, just show summary
        sb.AppendLine("(Use in-game console for detailed event listing)");

        return sb.ToString();
    }

    /// <summary>
    ///     Exports event inspector data to CSV format.
    /// </summary>
    public string ExportToCsv()
    {
        (
            int EventCount,
            int TotalSubscribers,
            double SlowestEventMs,
            string SlowestEventName
            ) stats = _content.GetStatistics();
        var sb = new StringBuilder();
        sb.AppendLine("Metric,Value");
        sb.AppendLine($"TotalEvents,{stats.EventCount}");
        sb.AppendLine($"TotalSubscribers,{stats.TotalSubscribers}");
        sb.AppendLine($"SlowestEventName,\"{stats.SlowestEventName}\"");
        sb.AppendLine($"SlowestEventMs,{stats.SlowestEventMs:F3}");
        return sb.ToString();
    }

    /// <summary>
    ///     Copies event inspector data to clipboard.
    /// </summary>
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
        // Content updates itself via time-based refresh in OnRender

        if (!_content.HasProvider)
        {
            SetStatusBar("No event data provider configured", "");
            return;
        }

        (
            int EventCount,
            int TotalSubscribers,
            double SlowestEventMs,
            string SlowestEventName
            ) stats = _content.GetStatistics();
        UITheme theme = ThemeManager.Current;

        // Determine health indicator based on slowest event time
        string statusIndicator;
        bool isHealthy = true;
        bool isWarning = false;

        if (stats.SlowestEventMs < 0.1)
        {
            statusIndicator = NerdFontIcons.StatusHealthy;
        }
        else if (stats.SlowestEventMs < 0.5)
        {
            statusIndicator = NerdFontIcons.StatusWarning;
            isWarning = true;
            isHealthy = false;
        }
        else
        {
            statusIndicator = NerdFontIcons.StatusError;
            isHealthy = false;
        }

        string statsText =
            $"{statusIndicator} Events: {stats.EventCount} | Subscribers: {stats.TotalSubscribers}";

        if (stats.SlowestEventMs > 0)
        {
            statsText += $" | Slowest: {stats.SlowestEventMs:F2}ms";
        }

        int refreshRate = 60 / _content.GetRefreshInterval();
        string hints =
            $"Click headers to sort | {_content.GetSortMode()} | Tab: Details | ~{refreshRate}fps";

        SetStatusBar(statsText, hints);
        SetStatusBarHealthColor(isHealthy, isWarning);
    }
}
