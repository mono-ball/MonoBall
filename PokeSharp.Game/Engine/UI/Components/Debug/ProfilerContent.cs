using Microsoft.Xna.Framework;
using PokeSharp.Game.Engine.Systems.Management;
using PokeSharp.Game.Engine.UI.Debug.Components.Base;
using PokeSharp.Game.Engine.UI.Debug.Components.Controls;
using PokeSharp.Game.Engine.UI.Debug.Core;
using PokeSharp.Game.Engine.UI.Debug.Input;
using PokeSharp.Game.Engine.UI.Debug.Interfaces;
using PokeSharp.Game.Engine.UI.Debug.Layout;

namespace PokeSharp.Game.Engine.UI.Debug.Components.Debug;

/// <summary>
///     Content component for ProfilerPanel that renders system performance bars.
///     Follows the same pattern as TextBuffer for consistent layout behavior.
/// </summary>
public class ProfilerContent : UIComponent
{
    // Layout constants from PanelConstants
    private const float NameColumnWidth = PanelConstants.Profiler.NameColumnWidth;
    private const float MsColumnWidth = PanelConstants.Profiler.MsColumnWidth;

    // Cached metrics
    private readonly List<SystemMetricEntry> _cachedMetrics = new();

    // Scrolling support
    private readonly ScrollbarComponent _scrollbar = new();

    // Table header
    private readonly SortableTableHeader<ProfilerSortMode> _tableHeader;
    private readonly float _warningThresholdMs;
    private Point _lastMousePosition = Point.Zero;

    // Refresh timing (time-based for frame rate independence)
    private double _lastUpdateTime;
    private float _maxSystemTimeMs;

    private Func<IReadOnlyDictionary<string, SystemMetrics>?>? _metricsProvider;
    private double _refreshIntervalSeconds = 0.1; // 100ms default
    private bool _showOnlyActive = true;
    private ProfilerSortMode _sortMode = ProfilerSortMode.ByExecutionTime;

    public ProfilerContent(string id, float targetFrameTimeMs, float warningThresholdMs)
    {
        Id = id;
        TargetFrameTimeMs = targetFrameTimeMs;
        _warningThresholdMs = warningThresholdMs;

        // Initialize table header
        _tableHeader = new SortableTableHeader<ProfilerSortMode>(ProfilerSortMode.ByExecutionTime);
        _tableHeader.SortChanged += OnSortChanged;
    }

    public bool HasProvider { get; private set; }

    public float TotalFrameTimeMs { get; private set; }

    public float TargetFrameTimeMs { get; }

    /// <summary>
    ///     Gets the refresh interval in frames (for backward compatibility).
    /// </summary>
    public int RefreshFrameInterval => (int)Math.Round(_refreshIntervalSeconds * 60);

    private void OnSortChanged(ProfilerSortMode newSort)
    {
        // Special handling for Last/Avg column - cycle between ByAverageTime and ByMaxTime
        if (newSort == ProfilerSortMode.ByAverageTime)
        {
            // If we're already on ByAverageTime or ByMaxTime, cycle to the other one
            if (_sortMode == ProfilerSortMode.ByAverageTime)
            {
                _sortMode = ProfilerSortMode.ByMaxTime;
            }
            else if (_sortMode == ProfilerSortMode.ByMaxTime)
            {
                _sortMode = ProfilerSortMode.ByAverageTime;
            }
            else
            {
                // Coming from a different sort mode, default to ByAverageTime
                _sortMode = ProfilerSortMode.ByAverageTime;
            }
        }
        else
        {
            _sortMode = newSort;
        }

        RefreshMetrics();
    }

    public void SetMetricsProvider(Func<IReadOnlyDictionary<string, SystemMetrics>?>? provider)
    {
        _metricsProvider = provider;
        HasProvider = provider != null;
        if (HasProvider)
        {
            RefreshMetrics();
        }
    }

    public void SetSortMode(ProfilerSortMode mode)
    {
        _sortMode = mode;
    }

    public ProfilerSortMode GetSortMode()
    {
        return _sortMode;
    }

    public void SetShowOnlyActive(bool showOnlyActive)
    {
        _showOnlyActive = showOnlyActive;
    }

    public bool GetShowOnlyActive()
    {
        return _showOnlyActive;
    }

    /// <summary>
    ///     Sets the refresh interval in seconds.
    /// </summary>
    public void SetRefreshInterval(float intervalSeconds)
    {
        _refreshIntervalSeconds = Math.Clamp(intervalSeconds, 0.016f, 1.0f);
    }

    /// <summary>
    ///     Gets the refresh interval in seconds.
    /// </summary>
    public double GetRefreshIntervalSeconds()
    {
        return _refreshIntervalSeconds;
    }

    public void Refresh()
    {
        RefreshMetrics();
    }

    public (int SystemCount, float TotalMs, float MaxSystemMs, string SlowestSystem) GetStatistics()
    {
        string slowest = _cachedMetrics.FirstOrDefault()?.Name ?? "None";
        return (_cachedMetrics.Count, TotalFrameTimeMs, _maxSystemTimeMs, slowest);
    }

    public IEnumerable<string> GetSystemNames()
    {
        return _cachedMetrics.Select(m => m.Name);
    }

    public (double LastMs, double AvgMs, double MaxMs, long UpdateCount)? GetSystemMetrics(
        string systemName
    )
    {
        SystemMetricEntry? entry = _cachedMetrics.FirstOrDefault(m =>
            m.Name.Equals(systemName, StringComparison.OrdinalIgnoreCase)
        );
        return entry == null ? null : (entry.LastMs, entry.AvgMs, entry.MaxMs, entry.UpdateCount);
    }

    public void ScrollToTop()
    {
        _scrollbar.ScrollToTop();
    }

    public void ScrollToBottom()
    {
        _scrollbar.ScrollOffset = Math.Max(0, _cachedMetrics.Count - 5);
    }

    public int GetScrollOffset()
    {
        return (int)_scrollbar.ScrollOffset;
    }

    private void RefreshMetrics()
    {
        _cachedMetrics.Clear();
        TotalFrameTimeMs = 0f;
        _maxSystemTimeMs = 0f;

        IReadOnlyDictionary<string, SystemMetrics>? metrics = _metricsProvider?.Invoke();
        if (metrics == null || metrics.Count == 0)
        {
            return;
        }

        foreach (KeyValuePair<string, SystemMetrics> kvp in metrics)
        {
            SystemMetrics m = kvp.Value;
            if (_showOnlyActive && m.UpdateCount == 0)
            {
                continue;
            }

            var entry = new SystemMetricEntry
            {
                Name = kvp.Key,
                LastMs = m.LastUpdateMs,
                AvgMs = m.AverageUpdateMs,
                MaxMs = m.MaxUpdateMs,
                UpdateCount = m.UpdateCount,
            };

            _cachedMetrics.Add(entry);
            TotalFrameTimeMs += (float)m.LastUpdateMs;
            if (m.LastUpdateMs > _maxSystemTimeMs)
            {
                _maxSystemTimeMs = (float)m.LastUpdateMs;
            }
        }

        // Sort
        _cachedMetrics.Sort(
            _sortMode switch
            {
                ProfilerSortMode.ByAverageTime => (a, b) => b.AvgMs.CompareTo(a.AvgMs),
                ProfilerSortMode.ByMaxTime => (a, b) => b.MaxMs.CompareTo(a.MaxMs),
                ProfilerSortMode.ByName => (a, b) =>
                    string.Compare(a.Name, b.Name, StringComparison.Ordinal),
                _ => (a, b) => b.LastMs.CompareTo(a.LastMs),
            }
        );
    }

    protected override void OnRender(UIContext context)
    {
        UITheme theme = ThemeManager.Current;
        UIRenderer renderer = Renderer;
        InputState? input = context.Input;

        int lineHeight = renderer.GetLineHeight();
        // Use DebugPanelBase.StandardLinePadding for consistent alignment
        // Parent panel already applies Constraint.Padding, so we only add internal line padding
        int linePadding = DebugPanelBase.StandardLinePadding;
        int scrollbarWidth = theme.ScrollbarWidth;

        float y = Rect.Y + linePadding;
        float contentX = Rect.X + linePadding;
        float contentWidth = Rect.Width - (linePadding * 2);

        // Empty state handling
        if (!HasProvider)
        {
            EmptyStateComponent.DrawLeftAligned(
                renderer,
                theme,
                contentX,
                y,
                "Profiler provider not configured.",
                "Waiting for system metrics..."
            );
            return;
        }

        // Time-based refresh for frame rate independence
        if (context.Input?.GameTime != null)
        {
            double currentTime = context.Input.GameTime.TotalGameTime.TotalSeconds;
            if (currentTime - _lastUpdateTime >= _refreshIntervalSeconds)
            {
                _lastUpdateTime = currentTime;
                RefreshMetrics();
            }
        }

        // Header
        renderer.DrawText("System Profiler", contentX, y, theme.Info);
        string sortText = $"Sort: {_sortMode}";
        float sortWidth = renderer.MeasureText(sortText).X;
        renderer.DrawText(sortText, contentX + contentWidth - sortWidth, y, theme.TextSecondary);
        y += lineHeight + theme.SpacingTight;

        // Budget info
        string budgetText = $"Frame Budget: {TargetFrameTimeMs:F1}ms (60fps)";
        renderer.DrawText(budgetText, contentX, y, theme.TextSecondary);
        string totalText = $"Total: {TotalFrameTimeMs:F2}ms";
        Color totalColor = TotalFrameTimeMs > TargetFrameTimeMs ? theme.Error : theme.Success;
        float totalWidth = renderer.MeasureText(totalText).X;
        renderer.DrawText(totalText, contentX + contentWidth - totalWidth, y, totalColor);
        y += lineHeight + theme.SpacingRelaxed;

        // Pre-calculate scrollbar requirements to determine content width
        // (need to do this BEFORE configuring headers to get correct width)
        float tableStartY = y + lineHeight + theme.SpacingTight + 1 + theme.SpacingNormal;
        int rowHeight = lineHeight + theme.SpacingNormal;
        float tableBottomY = Rect.Y + Rect.Height - linePadding;
        float availableHeight = tableBottomY - tableStartY;
        int maxBarsVisible = Math.Max(1, (int)(availableHeight / rowHeight));
        int maxScrollOffset = Math.Max(0, _cachedMetrics.Count - maxBarsVisible);
        bool needsScrollbar = _cachedMetrics.Count > maxBarsVisible;

        // Adjust content width if scrollbar is needed (with padding)
        float tableContentWidth = needsScrollbar
            ? contentWidth - scrollbarWidth - theme.PaddingSmall
            : contentWidth;
        float tableContentRightEdge = contentX + tableContentWidth;

        // Column headers (now with correct width accounting for scrollbar)
        float nameColWidth = NameColumnWidth;
        float barColStart = contentX + nameColWidth;
        float barColWidth = tableContentWidth - nameColWidth - MsColumnWidth;

        // Configure table header columns
        _tableHeader.ClearColumns();
        _tableHeader.AddColumns(
            new SortableTableHeader<ProfilerSortMode>.Column
            {
                Label = "System",
                SortMode = ProfilerSortMode.ByName,
                X = contentX,
                MaxWidth = nameColWidth,
                Ascending = true, // Name sorts ascending
            },
            new SortableTableHeader<ProfilerSortMode>.Column
            {
                Label = "Execution Time",
                SortMode = ProfilerSortMode.ByExecutionTime,
                X = barColStart,
                MaxWidth = barColWidth,
                Ascending = false, // Time sorts descending
            },
            new SortableTableHeader<ProfilerSortMode>.Column
            {
                Label = _sortMode == ProfilerSortMode.ByMaxTime ? "Last/Avg(Max)" : "Last/Avg",
                SortMode = ProfilerSortMode.ByAverageTime, // Always use Average as the sort mode
                X = tableContentRightEdge - MsColumnWidth,
                MaxWidth = MsColumnWidth,
                Alignment = SortableTableHeader<ProfilerSortMode>.HorizontalAlignment.Right,
                Ascending = false, // Time sorts descending
            }
        );

        // Draw table headers FIRST (to populate click regions)
        _tableHeader.Draw(renderer, theme, y, lineHeight);

        // Handle column header input AFTER drawing (so click regions are populated)
        if (input != null)
        {
            _tableHeader.HandleInput(input);
        }

        y += lineHeight + theme.SpacingTight;

        // Separator
        renderer.DrawRectangle(
            new LayoutRect(contentX, y, tableContentWidth, 1),
            theme.BorderPrimary
        );
        y += theme.SpacingNormal;

        // Handle scrollbar input first (before scroll wheel to get priority)
        if (input != null && needsScrollbar)
        {
            var scrollbarRect = new LayoutRect(
                contentX + tableContentWidth + theme.PaddingSmall,
                tableStartY,
                scrollbarWidth,
                tableBottomY - tableStartY
            );
            _scrollbar.HandleInput(
                context,
                input,
                scrollbarRect,
                _cachedMetrics.Count,
                maxBarsVisible,
                Id
            );
        }

        // Handle scroll input (mouse wheel)
        if (input != null && !_scrollbar.IsDragging && Rect.Contains(input.MousePosition))
        {
            _scrollbar.HandleMouseWheelLines(input, _cachedMetrics.Count, maxBarsVisible);
        }

        // Clamp scroll offset
        _scrollbar.ScrollOffset = Math.Clamp(_scrollbar.ScrollOffset, 0, maxScrollOffset);

        int startIndex = (int)_scrollbar.ScrollOffset;
        int endIndex = Math.Min(_cachedMetrics.Count, startIndex + maxBarsVisible);

        for (int i = startIndex; i < endIndex; i++)
        {
            SystemMetricEntry entry = _cachedMetrics[i];
            float rowY = y + ((i - startIndex) * rowHeight);

            Color barColor = GetBarColor(entry.LastMs, theme);
            string displayName = renderer.TruncateWithEllipsis(entry.Name, nameColWidth - 8);
            renderer.DrawText(displayName, contentX, rowY, theme.TextPrimary);

            // Bar background
            var barRect = new LayoutRect(
                barColStart,
                rowY + theme.ProfilerBarInset,
                barColWidth,
                lineHeight - (theme.ProfilerBarInset * 2)
            );
            renderer.DrawRectangle(barRect, theme.BackgroundElevated);

            // Bar fill
            float barPercent =
                Math.Min((float)(entry.LastMs / TargetFrameTimeMs), theme.ProfilerBarMaxScale)
                / theme.ProfilerBarMaxScale;
            float filledWidth = barColWidth * barPercent;
            if (filledWidth > 0)
            {
                var filledRect = new LayoutRect(
                    barColStart,
                    rowY + theme.ProfilerBarInset,
                    filledWidth,
                    lineHeight - (theme.ProfilerBarInset * 2)
                );
                renderer.DrawRectangle(filledRect, barColor);
            }

            // Budget line
            float budgetLineX = barColStart + (barColWidth * 0.5f);
            renderer.DrawRectangle(
                new LayoutRect(budgetLineX, rowY, 1, lineHeight),
                theme.Warning * theme.ProfilerBudgetLineOpacity
            );

            // Ms values - right-aligned to match header
            string msText = $"{entry.LastMs:F2}/{entry.AvgMs:F2}";
            float msTextWidth = renderer.MeasureText(msText).X;
            renderer.DrawText(
                msText,
                tableContentRightEdge - msTextWidth,
                rowY,
                theme.TextSecondary
            );
        }

        // Scroll indicator (only show if no scrollbar)
        if (_cachedMetrics.Count > maxBarsVisible && !needsScrollbar)
        {
            float moreY = y + (maxBarsVisible * rowHeight);
            string scrollText = $"[{startIndex + 1}-{endIndex} of {_cachedMetrics.Count}]";
            renderer.DrawText(scrollText, contentX, moreY, theme.TextSecondary);
        }

        // Draw scrollbar if needed
        if (needsScrollbar)
        {
            var scrollbarRect = new LayoutRect(
                contentX + tableContentWidth + theme.PaddingSmall,
                tableStartY,
                scrollbarWidth,
                tableBottomY - tableStartY
            );
            _scrollbar.Draw(renderer, theme, scrollbarRect, _cachedMetrics.Count, maxBarsVisible);
        }
    }

    private Color GetBarColor(double ms, UITheme theme)
    {
        if (ms >= _warningThresholdMs)
        {
            return theme.Error;
        }

        if (ms >= _warningThresholdMs * theme.ProfilerBarWarningThreshold)
        {
            return theme.Warning;
        }

        if (ms >= _warningThresholdMs * theme.ProfilerBarMildThreshold)
        {
            return theme.WarningMild;
        }

        return theme.Success;
    }

    protected override bool IsInteractive()
    {
        return true;
    }

    private class SystemMetricEntry
    {
        public string Name { get; set; } = "";
        public double LastMs { get; set; }
        public double AvgMs { get; set; }
        public double MaxMs { get; set; }
        public long UpdateCount { get; set; }
    }
}
