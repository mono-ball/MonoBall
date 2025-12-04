using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using PokeSharp.Game.Engine.UI.Debug.Components.Base;
using PokeSharp.Game.Engine.UI.Debug.Components.Controls;
using PokeSharp.Game.Engine.UI.Debug.Core;
using PokeSharp.Game.Engine.UI.Debug.Input;
using PokeSharp.Game.Engine.UI.Debug.Layout;
using PokeSharp.Game.Engine.UI.Debug.Models;

namespace PokeSharp.Game.Engine.UI.Debug.Components.Debug;

/// <summary>
///     Sort modes for the Event Inspector table.
/// </summary>
public enum EventInspectorSortMode
{
    ByName,
    BySubscribers,
    ByAvgTime,
    ByMaxTime,
    ByCount,
}

/// <summary>
///     Encapsulates column layout calculations with content-aware sizing.
///     Measures actual text widths to prevent column overlap.
/// </summary>
internal readonly struct ColumnLayout
{
    public readonly float EventNameX;
    public readonly float EventNameWidth;
    public readonly float BarX;
    public readonly float BarWidth;
    public readonly float SubsX;
    public readonly float SubsWidth;
    public readonly float CountX;
    public readonly float CountWidth;
    public readonly float TimeX;
    public readonly float TimeWidth;

    public readonly bool ShowBar;

    public readonly float TotalWidth;

    private ColumnLayout(
        float eventNameX,
        float eventNameWidth,
        float barX,
        float barWidth,
        float subsX,
        float subsWidth,
        float countX,
        float countWidth,
        float timeX,
        float timeWidth,
        bool showBar,
        float totalWidth
    )
    {
        EventNameX = eventNameX;
        EventNameWidth = eventNameWidth;
        BarX = barX;
        BarWidth = barWidth;
        SubsX = subsX;
        SubsWidth = subsWidth;
        CountX = countX;
        CountWidth = countWidth;
        TimeX = timeX;
        TimeWidth = timeWidth;
        ShowBar = showBar;
        TotalWidth = totalWidth;
    }

    /// <summary>
    ///     Calculates column layout by measuring actual content widths.
    ///     Right-aligned columns (Subs, Count, Time) are sized to fit their content.
    ///     Event Name has a fixed width, and the Bar fills remaining space.
    /// </summary>
    public static ColumnLayout CalculateFromContent(
        float availableWidth,
        float startX,
        IReadOnlyList<EventTypeInfo> events,
        UIRenderer renderer,
        UITheme theme
    )
    {
        float padding = theme.PaddingMedium;
        float rightPad = theme.PaddingSmall;

        // Measure header text widths
        float subsHeaderWidth = renderer.MeasureText("Subs").X + padding;
        float countHeaderWidth = renderer.MeasureText("Count").X + padding;
        float timeHeaderWidth = renderer.MeasureText("Avg/Max").X + padding;

        // Initialize with header widths as minimum
        float maxSubsWidth = subsHeaderWidth;
        float maxCountWidth = countHeaderWidth;
        float maxTimeWidth = timeHeaderWidth;

        // Measure content widths from actual data
        foreach (EventTypeInfo evt in events)
        {
            // Subs column
            string subsText = evt.SubscriberCount.ToString();
            float subsWidth = renderer.MeasureText(subsText).X + padding + rightPad;
            maxSubsWidth = Math.Max(maxSubsWidth, subsWidth);

            // Count column
            string countText = FormatCountStatic(evt.PublishCount);
            float countWidth = renderer.MeasureText(countText).X + padding + rightPad;
            maxCountWidth = Math.Max(maxCountWidth, countWidth);

            // Time column (avg/max format)
            if (evt.PublishCount > 0)
            {
                string timeText = $"{evt.AverageTimeMs:F2}/{evt.MaxTimeMs:F2}";
                float timeWidth = renderer.MeasureText(timeText).X + padding + rightPad;
                maxTimeWidth = Math.Max(maxTimeWidth, timeWidth);
            }
        }

        // Apply minimum widths from constants
        maxSubsWidth = Math.Max(maxSubsWidth, PanelConstants.EventInspector.MinSubsColumnWidth);
        maxCountWidth = Math.Max(maxCountWidth, PanelConstants.EventInspector.MinCountColumnWidth);
        maxTimeWidth = Math.Max(maxTimeWidth, PanelConstants.EventInspector.MinTimeColumnWidth);

        // Event name uses fixed width (like Profiler's System column)
        float eventNameWidth = PanelConstants.EventInspector.EventNameColumnWidth;

        // Calculate bar width (fills remaining space)
        // Account for column spacing between Subs->Count and Count->Time
        float columnSpacing = padding;
        float rightColumnsWidth =
            maxSubsWidth + columnSpacing + maxCountWidth + columnSpacing + maxTimeWidth;
        float barWidth = availableWidth - eventNameWidth - rightColumnsWidth;

        // Determine if we have room for the bar
        bool showBar = barWidth >= PanelConstants.EventInspector.MinBarColumnWidth;

        // If bar doesn't fit, shrink event name to minimum and recalculate
        if (!showBar)
        {
            eventNameWidth = PanelConstants.EventInspector.MinEventNameColumnWidth;
            barWidth = availableWidth - eventNameWidth - rightColumnsWidth;
            showBar = barWidth >= PanelConstants.EventInspector.MinBarColumnWidth;
        }

        // If still no room for bar, hide it entirely
        if (!showBar)
        {
            barWidth = 0;
        }

        // Calculate column positions (left to right) with inter-column spacing
        // Note: columnSpacing already declared above for barWidth calculation
        float x = startX;

        float eventNameX = x;
        x += eventNameWidth;

        float barX = x;
        x += barWidth;

        float subsX = x;
        x += maxSubsWidth + columnSpacing; // Add spacing after Subs

        float countX = x;
        x += maxCountWidth + columnSpacing; // Add spacing after Count

        float timeX = x;
        x += maxTimeWidth;

        float totalWidth = x - startX;

        return new ColumnLayout(
            eventNameX,
            eventNameWidth,
            barX,
            barWidth,
            subsX,
            maxSubsWidth,
            countX,
            maxCountWidth,
            timeX,
            maxTimeWidth,
            showBar,
            totalWidth
        );
    }

    /// <summary>
    ///     Static version of FormatCount for use in column calculation.
    /// </summary>
    private static string FormatCountStatic(long count)
    {
        return count switch
        {
            >= 1_000_000 => $"{count / 1_000_000.0:F1}M",
            >= 1_000 => $"{count / 1_000.0:F1}K",
            _ => count.ToString(),
        };
    }
}

/// <summary>
///     Content area for the Event Inspector panel.
///     Displays event types, subscriptions, and performance metrics in a scrollable view.
///     Follows the UIComponent pattern used by ProfilerContent and StatsContent.
/// </summary>
public class EventInspectorContent : UIComponent
{
    // ═══════════════════════════════════════════════════════════════════════════
    // Layout Constants from PanelConstants (matching Profiler pattern)
    // ═══════════════════════════════════════════════════════════════════════════

    // Fixed-width columns
    private const float EventNameColWidth = PanelConstants.EventInspector.EventNameColumnWidth; // 200px like Profiler
    private const float SubsColWidth = PanelConstants.EventInspector.SubsColumnWidth;
    private const float CountColWidth = PanelConstants.EventInspector.CountColumnWidth;

    private const float TimeColWidth = PanelConstants.EventInspector.TimeColumnWidth;

    // Note: Execution Time bar column is DYNAMIC - fills remaining space

    // Row and section layout
    private const float RowHeight = PanelConstants.EventInspector.RowHeight;

    // Performance thresholds (values in MILLISECONDS for consistency)
    private const float MaxBarTimeMs = PanelConstants.EventInspector.MaxBarTimeMs;
    private const float WarningThresholdMs = PanelConstants.EventInspector.WarningThresholdMs;

    // ═══════════════════════════════════════════════════════════════════════════
    // Fields
    // ═══════════════════════════════════════════════════════════════════════════

    private readonly ScrollbarComponent _scrollbar = new();
    private readonly SortableTableHeader<EventInspectorSortMode> _tableHeader;
    private EventInspectorData? _cachedData;

    private Func<EventInspectorData>? _dataProvider;

    // Time-based refresh
    private double _lastUpdateTime;
    private double _refreshIntervalSeconds = 0.5;
    private int _selectedEventIndex = -1;
    private bool _showSubscriptions = true;
    private List<EventTypeInfo> _sortedEvents = new();
    private EventInspectorSortMode _sortMode = EventInspectorSortMode.BySubscribers;

    // Layout tracking
    private float _totalContentHeight;
    private float _visibleHeight;

    // ═══════════════════════════════════════════════════════════════════════════
    // Constructor
    // ═══════════════════════════════════════════════════════════════════════════

    public EventInspectorContent()
    {
        Id = "event_inspector_content";

        // Initialize sortable table header
        _tableHeader = new SortableTableHeader<EventInspectorSortMode>(
            EventInspectorSortMode.BySubscribers
        );
        _tableHeader.SortChanged += OnSortChanged;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Properties
    // ═══════════════════════════════════════════════════════════════════════════

    public bool HasProvider => _dataProvider != null;

    private void OnSortChanged(EventInspectorSortMode newSort)
    {
        _sortMode = newSort;
        SortEvents();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Public Methods
    // ═══════════════════════════════════════════════════════════════════════════

    public void SetDataProvider(Func<EventInspectorData>? provider)
    {
        _dataProvider = provider;
        _cachedData = null;
        RefreshData();
    }

    public void SetRefreshInterval(int frameInterval)
    {
        _refreshIntervalSeconds = Math.Max(0.016, frameInterval / 60.0);
    }

    public int GetRefreshInterval()
    {
        return (int)(_refreshIntervalSeconds * 60);
    }

    public void ToggleSubscriptions()
    {
        _showSubscriptions = !_showSubscriptions;
    }

    public void SelectNextEvent()
    {
        if (_sortedEvents.Count == 0)
        {
            return;
        }

        _selectedEventIndex = (_selectedEventIndex + 1) % _sortedEvents.Count;
        if (_cachedData != null)
        {
            _cachedData.SelectedEventType = _sortedEvents[_selectedEventIndex].EventTypeName;
        }
    }

    public void SelectPreviousEvent()
    {
        if (_sortedEvents.Count == 0)
        {
            return;
        }

        _selectedEventIndex =
            _selectedEventIndex <= 0 ? _sortedEvents.Count - 1 : _selectedEventIndex - 1;
        if (_cachedData != null)
        {
            _cachedData.SelectedEventType = _sortedEvents[_selectedEventIndex].EventTypeName;
        }
    }

    public void Refresh()
    {
        RefreshData();
    }

    public void ScrollUp(int lines = 1)
    {
        _scrollbar.ScrollOffset = Math.Max(0, _scrollbar.ScrollOffset - (lines * RowHeight));
    }

    public void ScrollDown(int lines = 1)
    {
        float maxScroll = Math.Max(0, _totalContentHeight - _visibleHeight);
        _scrollbar.ScrollOffset = Math.Min(
            maxScroll,
            _scrollbar.ScrollOffset + (lines * RowHeight)
        );
    }

    public (
        int EventCount,
        int TotalSubscribers,
        double SlowestEventMs,
        string SlowestEventName
    ) GetStatistics()
    {
        if (_cachedData == null || _cachedData.Events.Count == 0)
        {
            return (0, 0, 0, "N/A");
        }

        int eventCount = _cachedData.Events.Count;
        int totalSubscribers = _cachedData.Events.Sum(e => e.SubscriberCount);

        EventTypeInfo? slowest = _cachedData
            .Events.Where(e => e.PublishCount > 0)
            .OrderByDescending(e => e.AverageTimeMs)
            .FirstOrDefault();

        double slowestMs = slowest?.AverageTimeMs ?? 0;
        string slowestName = slowest?.EventTypeName ?? "N/A";

        return (eventCount, totalSubscribers, slowestMs, slowestName);
    }

    /// <summary>
    ///     Gets the current cached event inspector data.
    /// </summary>
    public EventInspectorData GetCurrentData()
    {
        // Refresh if needed
        if (_cachedData == null && _dataProvider != null)
        {
            RefreshData();
        }

        return _cachedData
            ?? new EventInspectorData
            {
                Events = new List<EventTypeInfo>(),
                RecentEvents = new List<EventLogEntry>(),
                Filters = new EventFilterOptions(),
            };
    }

    public EventInspectorSortMode GetSortMode()
    {
        return _sortMode;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // UIComponent Overrides
    // ═══════════════════════════════════════════════════════════════════════════

    protected override bool IsInteractive()
    {
        return true;
    }

    protected override void OnRender(UIContext context)
    {
        UITheme theme = Theme;
        UIRenderer renderer = Renderer;
        InputState? input = context.Input;

        int lineHeight = renderer.GetLineHeight();
        int linePadding = DebugPanelBase.StandardLinePadding;
        int scrollbarWidth = theme.ScrollbarWidth;

        float y = Rect.Y + linePadding;
        float contentX = Rect.X + linePadding;
        float contentWidth = Rect.Width - (linePadding * 2);

        // Time-based refresh
        if (input?.GameTime != null)
        {
            double currentTime = input.GameTime.TotalGameTime.TotalSeconds;
            if (currentTime - _lastUpdateTime >= _refreshIntervalSeconds)
            {
                _lastUpdateTime = currentTime;
                RefreshData();
            }
        }

        // Empty state handling
        if (_dataProvider == null)
        {
            EmptyStateComponent.DrawLeftAligned(
                renderer,
                theme,
                contentX,
                y,
                "No data provider configured.",
                "Waiting for event inspector data..."
            );
            return;
        }

        if (_cachedData == null || _cachedData.Events.Count == 0)
        {
            EmptyStateComponent.DrawLeftAligned(
                renderer,
                theme,
                contentX,
                y,
                "No events registered.",
                "Events will appear when the game dispatches them."
            );
            return;
        }

        // Calculate total content height and scrollbar needs
        _visibleHeight = Rect.Height - (linePadding * 2);
        _totalContentHeight = CalculateTotalContentHeight(lineHeight, theme);

        bool needsScrollbar = _totalContentHeight > _visibleHeight;
        float tableContentWidth = needsScrollbar
            ? contentWidth - scrollbarWidth - theme.PaddingSmall
            : contentWidth;

        // Handle input
        if (input != null)
        {
            HandleInput(context, input);
        }

        // Create clip rect for scrollable content
        LayoutRect clipRect = new(
            Rect.X,
            Rect.Y,
            tableContentWidth + (linePadding * 2),
            Rect.Height
        );
        renderer.PushClip(clipRect);

        // Render content with scroll offset
        float renderY = y - _scrollbar.ScrollOffset;

        // ═══════════════════════════════════════════════════════════════════════
        // Section 1: Summary Header (always visible at top)
        // ═══════════════════════════════════════════════════════════════════════
        renderY = RenderSummaryHeader(
            renderer,
            theme,
            contentX,
            renderY,
            tableContentWidth,
            lineHeight
        );

        // ═══════════════════════════════════════════════════════════════════════
        // Section 2: Events Table with Performance Bars
        // ═══════════════════════════════════════════════════════════════════════
        renderY = RenderEventsTable(
            renderer,
            theme,
            input,
            contentX,
            renderY,
            tableContentWidth,
            lineHeight
        );

        // ═══════════════════════════════════════════════════════════════════════
        // Section 3: Selected Event Subscriptions
        // ═══════════════════════════════════════════════════════════════════════
        renderY = RenderSubscriptionsSection(
            renderer,
            theme,
            contentX,
            renderY,
            tableContentWidth,
            lineHeight
        );

        // ═══════════════════════════════════════════════════════════════════════
        // Section 4: Recent Events Log
        // ═══════════════════════════════════════════════════════════════════════
        RenderRecentEvents(renderer, theme, contentX, renderY, tableContentWidth, lineHeight);

        renderer.PopClip();

        // Draw scrollbar if needed
        if (needsScrollbar)
        {
            LayoutRect scrollbarRect = new(
                Rect.X + Rect.Width - scrollbarWidth - (linePadding / 2),
                Rect.Y + linePadding,
                scrollbarWidth,
                Rect.Height - (linePadding * 2)
            );
            _scrollbar.Draw(renderer, theme, scrollbarRect, _totalContentHeight, _visibleHeight);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Input Handling
    // ═══════════════════════════════════════════════════════════════════════════

    private void HandleInput(UIContext context, InputState input)
    {
        // Handle scrollbar input
        _scrollbar.HandleInput(context, input, Rect, _totalContentHeight, _visibleHeight, Id);

        // Handle mouse wheel
        if (Rect.Contains(input.MousePosition))
        {
            int wheelDelta = input.ScrollWheelDelta;
            if (wheelDelta != 0)
            {
                float maxScroll = Math.Max(0, _totalContentHeight - _visibleHeight);
                _scrollbar.ScrollOffset = Math.Clamp(
                    _scrollbar.ScrollOffset - (wheelDelta * Theme.ScrollWheelSensitivity),
                    0,
                    maxScroll
                );
            }
        }

        // Handle table header clicks
        if (_tableHeader.HandleInput(input))
        {
            // Sort changed
        }

        // Handle keyboard input
        if (input.IsKeyPressed(Keys.Up))
        {
            SelectPreviousEvent();
        }
        else if (input.IsKeyPressed(Keys.Down))
        {
            SelectNextEvent();
        }
        else if (input.IsKeyPressed(Keys.Tab))
        {
            ToggleSubscriptions();
        }
        else if (input.IsKeyPressed(Keys.R))
        {
            RefreshData();
        }
        else if (input.IsKeyPressed(Keys.PageUp))
        {
            ScrollUp((int)(_visibleHeight / RowHeight));
        }
        else if (input.IsKeyPressed(Keys.PageDown))
        {
            ScrollDown((int)(_visibleHeight / RowHeight));
        }
        else if (input.IsKeyPressed(Keys.Home))
        {
            _scrollbar.ScrollOffset = 0;
        }
        else if (input.IsKeyPressed(Keys.End))
        {
            _scrollbar.ScrollOffset = Math.Max(0, _totalContentHeight - _visibleHeight);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Rendering Sections
    // ═══════════════════════════════════════════════════════════════════════════

    private float RenderSummaryHeader(
        UIRenderer renderer,
        UITheme theme,
        float x,
        float y,
        float width,
        int lineHeight
    )
    {
        if (_cachedData == null)
        {
            return y;
        }

        (
            int EventCount,
            int TotalSubscribers,
            double SlowestEventMs,
            string SlowestEventName
        ) stats = GetStatistics();

        // Title line (left) + Sort mode (right) - matches Profiler pattern
        renderer.DrawText("Event Inspector", x, y, theme.Info);
        string sortText = $"Sort: {_sortMode}";
        float sortWidth = renderer.MeasureText(sortText).X;
        renderer.DrawText(sortText, x + width - sortWidth, y, theme.TextSecondary);
        y += lineHeight + theme.SpacingTight;

        // Stats line (left) + Slowest event (right) - matches Profiler pattern
        string statsText = $"Events: {stats.EventCount} | Subscribers: {stats.TotalSubscribers}";
        renderer.DrawText(statsText, x, y, theme.TextSecondary);

        // Right-aligned slowest event info with color coding
        Color slowestColor =
            stats.SlowestEventMs >= 1.0f ? theme.Error
            : stats.SlowestEventMs >= 0.5f ? theme.Warning
            : theme.Success;
        string slowestText =
            stats.SlowestEventMs > 0 ? $"Slowest: {stats.SlowestEventMs:F2}ms" : "No activity";
        float slowestWidth = renderer.MeasureText(slowestText).X;
        renderer.DrawText(slowestText, x + width - slowestWidth, y, slowestColor);
        y += lineHeight + theme.SpacingRelaxed;

        return y;
    }

    private float RenderEventsTable(
        UIRenderer renderer,
        UITheme theme,
        InputState? input,
        float x,
        float y,
        float width,
        int lineHeight
    )
    {
        if (_cachedData == null)
        {
            return y;
        }

        // Check if panel is too narrow to display meaningfully
        if (width < PanelConstants.EventInspector.MinPanelWidth)
        {
            string warningText = "Panel too narrow - resize to view table";
            renderer.DrawText(warningText, x, y, theme.Warning);
            y += lineHeight + theme.SpacingNormal;
            return y;
        }

        // Calculate content-aware column layout (measures actual text widths)
        var layout = ColumnLayout.CalculateFromContent(width, x, _sortedEvents, renderer, theme);

        // Configure and draw table header
        _tableHeader.ClearColumns();

        // Event Type column (always visible, fixed width)
        _tableHeader.AddColumn(
            new SortableTableHeader<EventInspectorSortMode>.Column
            {
                Label = "Event Type",
                SortMode = EventInspectorSortMode.ByName,
                X = layout.EventNameX,
                MaxWidth = layout.EventNameWidth,
                Ascending = true,
            }
        );

        // Execution Time bar (visible when space allows)
        if (layout.ShowBar)
        {
            _tableHeader.AddColumn(
                new SortableTableHeader<EventInspectorSortMode>.Column
                {
                    Label = "Execution Time",
                    SortMode = EventInspectorSortMode.ByAvgTime,
                    X = layout.BarX,
                    MaxWidth = layout.BarWidth,
                }
            );
        }

        // Subscribers column (auto-sized to content)
        _tableHeader.AddColumn(
            new SortableTableHeader<EventInspectorSortMode>.Column
            {
                Label = "Subs",
                SortMode = EventInspectorSortMode.BySubscribers,
                X = layout.SubsX,
                MaxWidth = layout.SubsWidth,
                Alignment = SortableTableHeader<EventInspectorSortMode>.HorizontalAlignment.Right,
            }
        );

        // Count column (auto-sized to content)
        _tableHeader.AddColumn(
            new SortableTableHeader<EventInspectorSortMode>.Column
            {
                Label = "Count",
                SortMode = EventInspectorSortMode.ByCount,
                X = layout.CountX,
                MaxWidth = layout.CountWidth,
                Alignment = SortableTableHeader<EventInspectorSortMode>.HorizontalAlignment.Right,
            }
        );

        // Avg/Max time column (auto-sized to content)
        _tableHeader.AddColumn(
            new SortableTableHeader<EventInspectorSortMode>.Column
            {
                Label = "Avg/Max",
                SortMode = EventInspectorSortMode.ByMaxTime,
                X = layout.TimeX,
                MaxWidth = layout.TimeWidth,
                Alignment = SortableTableHeader<EventInspectorSortMode>.HorizontalAlignment.Right,
            }
        );

        _tableHeader.SetSort(_sortMode);
        _tableHeader.DrawWithHover(renderer, theme, input, y, lineHeight);
        y += lineHeight + theme.SpacingTight;

        // Header separator
        renderer.DrawRectangle(new LayoutRect(x, y, width, 1), theme.BorderPrimary);
        y += theme.SpacingNormal;

        // Render event rows
        foreach (EventTypeInfo eventInfo in _sortedEvents)
        {
            bool isSelected = eventInfo.EventTypeName == _cachedData.SelectedEventType;

            // Draw selection highlight
            if (isSelected)
            {
                LayoutRect highlightRect = new(
                    x - theme.PaddingSmall,
                    y - theme.PaddingTiny,
                    width + (theme.PaddingSmall * 2),
                    RowHeight
                );
                renderer.DrawRectangle(highlightRect, theme.InputSelection);
            }

            // Determine color based on performance (no icon, matches Profiler pattern)
            Color eventColor = GetPerformanceColor(eventInfo.AverageTimeMs, theme);

            // Event name (truncated with ellipsis)
            string displayName = renderer.TruncateWithEllipsis(
                eventInfo.EventTypeName,
                layout.EventNameWidth - theme.PaddingMedium
            );
            renderer.DrawText(
                displayName,
                layout.EventNameX,
                y,
                isSelected ? theme.TextPrimary : eventColor
            );

            // Execution Time bar (only if visible in layout)
            if (layout.ShowBar)
            {
                float barWidth = layout.BarWidth - theme.PaddingMedium;
                RenderPerformanceBar(
                    renderer,
                    theme,
                    layout.BarX,
                    y,
                    barWidth,
                    lineHeight,
                    eventInfo
                );
            }

            // Subscribers count (right-aligned, auto-sized to content)
            string subsText = eventInfo.SubscriberCount.ToString();
            float subsTextWidth = renderer.MeasureText(subsText).X;
            float subsTextX = layout.SubsX + layout.SubsWidth - subsTextWidth - theme.PaddingSmall;
            renderer.DrawText(subsText, subsTextX, y, theme.TextSecondary);

            // Publish count (right-aligned)
            string countText = FormatCount(eventInfo.PublishCount);
            float countTextWidth = renderer.MeasureText(countText).X;
            float countTextX =
                layout.CountX + layout.CountWidth - countTextWidth - theme.PaddingSmall;

            // Ensure count text doesn't overflow into next column
            countText = renderer.TruncateWithEllipsis(
                countText,
                layout.CountWidth - theme.PaddingSmall
            );
            renderer.DrawText(countText, countTextX, y, theme.TextSecondary);

            // Avg/Max time (right-aligned)
            string timeText;
            Color timeColor;

            if (eventInfo.PublishCount > 0)
            {
                timeText = FormatTimeRange(
                    eventInfo.AverageTimeMs,
                    eventInfo.MaxTimeMs,
                    layout.TimeWidth
                );
                timeColor = GetPerformanceColor(eventInfo.MaxTimeMs, theme);
            }
            else
            {
                timeText = "-/-";
                timeColor = theme.TextDim;
            }

            float timeTextWidth = renderer.MeasureText(timeText).X;
            float timeTextX = layout.TimeX + layout.TimeWidth - timeTextWidth - theme.PaddingSmall;
            renderer.DrawText(timeText, timeTextX, y, timeColor);

            y += RowHeight;
        }

        y += theme.SectionSpacing;
        return y;
    }

    private void RenderPerformanceBar(
        UIRenderer renderer,
        UITheme theme,
        float x,
        float y,
        float width,
        int lineHeight,
        EventTypeInfo eventInfo
    )
    {
        float barHeight = lineHeight - (theme.ProfilerBarInset * 2);
        float barY = y + theme.ProfilerBarInset;

        // Bar background
        var barRect = new LayoutRect(x, barY, width, barHeight);
        renderer.DrawRectangle(barRect, theme.BackgroundElevated);

        // Values are now in milliseconds for consistency with other panels
        if (eventInfo.PublishCount > 0 && eventInfo.AverageTimeMs > 0)
        {
            // Bar fill (matching Profiler pattern: time / maxTime / maxScale)
            float barPercent =
                Math.Min((float)(eventInfo.AverageTimeMs / MaxBarTimeMs), theme.ProfilerBarMaxScale)
                / theme.ProfilerBarMaxScale;
            float filledWidth = width * barPercent;

            if (filledWidth > 0)
            {
                Color barColor = GetPerformanceColor(eventInfo.AverageTimeMs, theme);
                var filledRect = new LayoutRect(x, barY, filledWidth, barHeight);
                renderer.DrawRectangle(filledRect, barColor);
            }

            // Warning threshold marker (at 1ms)
            float warningX =
                x + (width * (WarningThresholdMs / MaxBarTimeMs / theme.ProfilerBarMaxScale));
            if (warningX > x && warningX < x + width)
            {
                renderer.DrawRectangle(
                    new LayoutRect(warningX, barY, 1, barHeight),
                    theme.Warning * theme.ProfilerBudgetLineOpacity
                );
            }
        }
    }

    private float RenderSubscriptionsSection(
        UIRenderer renderer,
        UITheme theme,
        float x,
        float y,
        float width,
        int lineHeight
    )
    {
        if (_cachedData == null || !_showSubscriptions)
        {
            return y;
        }

        if (string.IsNullOrEmpty(_cachedData.SelectedEventType))
        {
            return y;
        }

        EventTypeInfo? selectedEvent = _cachedData.Events.FirstOrDefault(e =>
            e.EventTypeName == _cachedData.SelectedEventType
        );

        if (selectedEvent == null || selectedEvent.Subscriptions.Count == 0)
        {
            return y;
        }

        // Section header with separator
        renderer.DrawRectangle(new LayoutRect(x, y, width, 1), theme.BorderPrimary);
        y += theme.SpacingNormal;

        renderer.DrawText($"Subscriptions: {selectedEvent.EventTypeName}", x, y, theme.Info);
        y += lineHeight + theme.SpacingTight;

        // Subscription list with tree structure
        var sortedSubs = selectedEvent.Subscriptions.OrderByDescending(s => s.Priority).ToList();
        for (int i = 0; i < sortedSubs.Count; i++)
        {
            SubscriptionInfo sub = sortedSubs[i];
            bool isLast = i == sortedSubs.Count - 1;

            // Tree connector
            string treePrefix = isLast ? NerdFontIcons.TreeLast : NerdFontIcons.TreeBranch;

            string source = string.IsNullOrEmpty(sub.Source)
                ? $"Handler #{sub.HandlerId}"
                : sub.Source;

            Color priorityColor =
                sub.Priority >= 100 ? theme.Warning
                : sub.Priority >= 50 ? theme.Info
                : theme.TextPrimary;

            renderer.DrawText($"{treePrefix}{NerdFontIcons.TreeHorizontal} ", x, y, theme.TextDim);
            renderer.DrawText(
                $"[P{sub.Priority}]",
                x + PanelConstants.EventInspector.TreeIndentLevel1,
                y,
                priorityColor
            );
            renderer.DrawText(
                source,
                x + PanelConstants.EventInspector.TreeIndentLevel2,
                y,
                theme.TextPrimary
            );

            // Performance inline
            if (sub.InvocationCount > 0)
            {
                Color perfColor = GetPerformanceColor(sub.AverageTimeMs, theme);
                string perfText = $" ({sub.AverageTimeMs:F2}ms avg, {sub.InvocationCount} calls)";
                float sourceWidth = renderer.MeasureText(source).X;
                renderer.DrawText(
                    perfText,
                    x + PanelConstants.EventInspector.TreeIndentLevel2 + sourceWidth,
                    y,
                    perfColor
                );
            }

            y += RowHeight;
        }

        y += theme.SectionSpacing;
        return y;
    }

    private float RenderRecentEvents(
        UIRenderer renderer,
        UITheme theme,
        float x,
        float y,
        float width,
        int lineHeight
    )
    {
        if (_cachedData == null || _cachedData.RecentEvents.Count == 0)
        {
            return y;
        }

        // Section separator
        renderer.DrawRectangle(new LayoutRect(x, y, width, 1), theme.BorderPrimary);
        y += theme.SpacingNormal;

        // Section header
        renderer.DrawText("Recent Events (last 10)", x, y, theme.Info);
        y += lineHeight + theme.SpacingTight;

        foreach (EventLogEntry entry in _cachedData.RecentEvents.TakeLast(10))
        {
            string timestamp = entry.Timestamp.ToString("HH:mm:ss.fff");
            string operationIcon =
                entry.Operation == "Publish" ? NerdFontIcons.ArrowRight : NerdFontIcons.ArrowLeft;
            Color perfColor = GetPerformanceColor(entry.DurationMs, theme);
            string handler = entry.HandlerId.HasValue ? $" #{entry.HandlerId}" : "";

            // Timestamp
            renderer.DrawText(timestamp, x, y, theme.TextDim);

            // Operation icon and event type
            renderer.DrawText(
                $" {operationIcon} ",
                x + PanelConstants.EventInspector.TimestampColumnWidth,
                y,
                theme.TextSecondary
            );
            renderer.DrawText(
                entry.EventType,
                x
                    + PanelConstants.EventInspector.TimestampColumnWidth
                    + PanelConstants.EventInspector.OperationIconWidth,
                y,
                theme.TextPrimary
            );

            // Handler and duration
            string suffix = $"{handler} ({entry.DurationMs:F2}ms)";
            float eventWidth = renderer.MeasureText(entry.EventType).X;
            renderer.DrawText(suffix, x + 105 + eventWidth, y, perfColor);

            y += RowHeight;
        }

        return y;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Helper Methods
    // ═══════════════════════════════════════════════════════════════════════════

    private void RefreshData()
    {
        if (_dataProvider == null)
        {
            return;
        }

        _cachedData = _dataProvider();
        SortEvents();
    }

    private void SortEvents()
    {
        if (_cachedData == null)
        {
            _sortedEvents = new List<EventTypeInfo>();
            return;
        }

        _sortedEvents = _sortMode switch
        {
            EventInspectorSortMode.ByName => _cachedData
                .Events.OrderBy(e => e.EventTypeName)
                .ToList(),
            EventInspectorSortMode.BySubscribers => _cachedData
                .Events.OrderByDescending(e => e.SubscriberCount)
                .ThenBy(e => e.EventTypeName)
                .ToList(),
            EventInspectorSortMode.ByAvgTime => _cachedData
                .Events.OrderByDescending(e => e.AverageTimeMs)
                .ThenBy(e => e.EventTypeName)
                .ToList(),
            EventInspectorSortMode.ByMaxTime => _cachedData
                .Events.OrderByDescending(e => e.MaxTimeMs)
                .ThenBy(e => e.EventTypeName)
                .ToList(),
            EventInspectorSortMode.ByCount => _cachedData
                .Events.OrderByDescending(e => e.PublishCount)
                .ThenBy(e => e.EventTypeName)
                .ToList(),
            _ => _cachedData.Events.ToList(),
        };
    }

    private float CalculateTotalContentHeight(int lineHeight, UITheme theme)
    {
        if (_cachedData == null)
        {
            return 0;
        }

        float height = 0;

        // Summary header
        height += lineHeight + theme.SpacingTight + 1 + theme.SpacingNormal;

        // Events table header
        height += lineHeight + theme.SpacingTight + 1 + theme.SpacingNormal;

        // Event rows
        height += _sortedEvents.Count * RowHeight;
        height += theme.SectionSpacing;

        // Subscriptions section (if visible and event selected)
        if (_showSubscriptions && !string.IsNullOrEmpty(_cachedData.SelectedEventType))
        {
            EventTypeInfo? selected = _cachedData.Events.FirstOrDefault(e =>
                e.EventTypeName == _cachedData.SelectedEventType
            );
            if (selected != null && selected.Subscriptions.Count > 0)
            {
                height += 1 + theme.SpacingNormal; // separator
                height += lineHeight + theme.SpacingTight; // header
                height += selected.Subscriptions.Count * RowHeight;
                height += theme.SectionSpacing;
            }
        }

        // Recent events section
        if (_cachedData.RecentEvents.Count > 0)
        {
            height += 1 + theme.SpacingNormal; // separator
            height += lineHeight + theme.SpacingTight; // header
            height += Math.Min(_cachedData.RecentEvents.Count, 10) * RowHeight;
        }

        return height;
    }

    /// <summary>
    ///     Gets performance color based on time value in milliseconds.
    ///     Uses relative thresholds matching the Profiler panel approach.
    /// </summary>
    private Color GetPerformanceColor(double timeMs, UITheme theme)
    {
        // Thresholds relative to WarningThresholdMs (1ms):
        // - Error: >= 1ms
        // - Warning: >= 0.5ms - uses theme's ProfilerBarWarningThreshold
        // - Mild: >= 0.25ms - uses theme's ProfilerBarMildThreshold
        // - Good: < 0.25ms

        if (timeMs >= WarningThresholdMs)
        {
            return theme.Error; // Critical (>= 1ms)
        }

        if (timeMs >= WarningThresholdMs * theme.ProfilerBarWarningThreshold)
        {
            return theme.Warning; // Warning (>= 0.5ms with default 0.5 threshold)
        }

        if (timeMs >= WarningThresholdMs * theme.ProfilerBarMildThreshold)
        {
            return theme.WarningMild; // Mild (>= 0.25ms with default 0.25 threshold)
        }

        return theme.Success; // Good
    }

    private static string FormatCount(long count)
    {
        return count switch
        {
            >= 1_000_000 => $"{count / 1_000_000.0:F1}M",
            >= 1_000 => $"{count / 1_000.0:F1}K",
            _ => count.ToString(),
        };
    }

    /// <summary>
    ///     Truncates a number to fit within a maximum character length.
    ///     Uses abbreviated format (K, M) for large numbers.
    /// </summary>
    private static string TruncateNumber(long number, int maxChars)
    {
        string text = number switch
        {
            >= 1_000_000_000 => $"{number / 1_000_000_000.0:F1}B",
            >= 1_000_000 => $"{number / 1_000_000.0:F1}M",
            >= 1_000 => $"{number / 1_000.0:F1}K",
            _ => number.ToString(),
        };

        // If still too long, use scientific notation
        if (text.Length > maxChars && number >= 1000)
        {
            return $"{number / 1000:F0}K+";
        }

        return text;
    }

    /// <summary>
    ///     Formats time range (avg/max) with responsive abbreviation based on available width.
    ///     Falls back to shorter formats when space is constrained.
    /// </summary>
    private static string FormatTimeRange(double avgMs, double maxMs, float availableWidth)
    {
        // Try full format first: "12.34/56.78"
        string fullFormat = $"{avgMs:F2}/{maxMs:F2}";

        // If we have plenty of space (or text fits), use full format
        // Assuming average character width of ~8px for our font
        if (availableWidth >= 90f || fullFormat.Length <= 11)
        {
            return fullFormat;
        }

        // Compact format: "12.3/56.7"
        string compactFormat = $"{avgMs:F1}/{maxMs:F1}";
        if (availableWidth >= 75f || compactFormat.Length <= 9)
        {
            return compactFormat;
        }

        // Minimal format: "12/56"
        string minimalFormat = $"{avgMs:F0}/{maxMs:F0}";
        if (availableWidth >= 50f)
        {
            return minimalFormat;
        }

        // Extreme constraint: show just max with indicator
        return $"{maxMs:F0}+";
    }
}
