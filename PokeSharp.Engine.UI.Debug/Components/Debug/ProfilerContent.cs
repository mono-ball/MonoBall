using Microsoft.Xna.Framework;
using PokeSharp.Engine.Systems.Management;
using PokeSharp.Engine.UI.Debug.Components.Base;
using PokeSharp.Engine.UI.Debug.Core;
using PokeSharp.Engine.UI.Debug.Input;
using PokeSharp.Engine.UI.Debug.Interfaces;
using PokeSharp.Engine.UI.Debug.Layout;

namespace PokeSharp.Engine.UI.Debug.Components.Debug;

/// <summary>
///     Content component for ProfilerPanel that renders system performance bars.
///     Follows the same pattern as TextBuffer for consistent layout behavior.
/// </summary>
public class ProfilerContent : UIComponent
{
    // Layout constants from PanelConstants
    private const float BottomPadding = PanelConstants.Profiler.BottomPadding;
    private const float NameColumnWidth = PanelConstants.Profiler.NameColumnWidth;
    private const float MsColumnWidth = PanelConstants.Profiler.MsColumnWidth;

    // Cached metrics
    private readonly List<SystemMetricEntry> _cachedMetrics = new();
    private readonly float _warningThresholdMs;
    private Point _lastMousePosition = Point.Zero;

    // Refresh timing (time-based for frame rate independence)
    private double _lastUpdateTime;
    private float _maxSystemTimeMs;

    private Func<IReadOnlyDictionary<string, SystemMetrics>?>? _metricsProvider;
    private double _refreshIntervalSeconds = 0.1; // 100ms default

    // Scrolling support
    private int _scrollOffset;
    private bool _showOnlyActive = true;
    private ProfilerSortMode _sortMode = ProfilerSortMode.ByExecutionTime;

    public ProfilerContent(string id, float targetFrameTimeMs, float warningThresholdMs)
    {
        Id = id;
        TargetFrameTimeMs = targetFrameTimeMs;
        _warningThresholdMs = warningThresholdMs;
    }

    public bool HasProvider { get; private set; }

    public float TotalFrameTimeMs { get; private set; }

    public float TargetFrameTimeMs { get; }

    /// <summary>
    ///     Gets the refresh interval in frames (for backward compatibility).
    /// </summary>
    public int RefreshFrameInterval => (int)Math.Round(_refreshIntervalSeconds * 60);

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
        _scrollOffset = 0;
    }

    public void ScrollToBottom()
    {
        _scrollOffset = Math.Max(0, _cachedMetrics.Count - 5);
    }

    public int GetScrollOffset()
    {
        return _scrollOffset;
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
        float y = Rect.Y + linePadding;
        float contentX = Rect.X + linePadding;
        float contentWidth = Rect.Width - (linePadding * 2);

        // Empty state handling
        if (!HasProvider)
        {
            renderer.DrawText("Profiler provider not configured.", contentX, y, theme.TextDim);
            y += lineHeight;
            renderer.DrawText("Waiting for system metrics...", contentX, y, theme.TextDim);
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

        // Handle scroll input
        if (input != null)
        {
            _lastMousePosition = input.MousePosition;
            if (Rect.Contains(input.MousePosition))
            {
                if (input.ScrollWheelDelta > 0)
                {
                    _scrollOffset = Math.Max(0, _scrollOffset - 3);
                }
                else if (input.ScrollWheelDelta < 0)
                {
                    _scrollOffset = Math.Min(
                        Math.Max(0, _cachedMetrics.Count - 5),
                        _scrollOffset + 3
                    );
                }
            }
        }

        // Header
        renderer.DrawText("System Profiler", contentX, y, theme.Info);
        string sortText = $"Sort: {_sortMode}";
        float sortWidth = renderer.MeasureText(sortText).X;
        renderer.DrawText(sortText, contentX + contentWidth - sortWidth, y, theme.TextSecondary);
        y += lineHeight + 4;

        // Budget info
        string budgetText = $"Frame Budget: {TargetFrameTimeMs:F1}ms (60fps)";
        renderer.DrawText(budgetText, contentX, y, theme.TextSecondary);
        string totalText = $"Total: {TotalFrameTimeMs:F2}ms";
        Color totalColor = TotalFrameTimeMs > TargetFrameTimeMs ? theme.Error : theme.Success;
        float totalWidth = renderer.MeasureText(totalText).X;
        renderer.DrawText(totalText, contentX + contentWidth - totalWidth, y, totalColor);
        y += lineHeight + 8;

        // Column headers
        float nameColWidth = NameColumnWidth;
        float barColStart = contentX + nameColWidth;
        float barColWidth = contentWidth - nameColWidth - MsColumnWidth;
        float contentRightEdge = contentX + contentWidth;

        renderer.DrawText("System", contentX, y, theme.TextSecondary);
        renderer.DrawText("Execution Time", barColStart, y, theme.TextSecondary);
        // Right-align "Last/Avg" header to match right edge padding
        string lastAvgHeader = "Last/Avg";
        float lastAvgHeaderWidth = renderer.MeasureText(lastAvgHeader).X;
        renderer.DrawText(
            lastAvgHeader,
            contentRightEdge - lastAvgHeaderWidth,
            y,
            theme.TextSecondary
        );
        y += lineHeight + 4;

        // Separator
        renderer.DrawRectangle(new LayoutRect(contentX, y, contentWidth, 1), theme.BorderPrimary);
        y += 6;

        // System bars
        int rowHeight = lineHeight + 6;
        int maxBarsVisible = (int)((Rect.Y + Rect.Height - BottomPadding - y) / rowHeight);
        _scrollOffset = Math.Clamp(
            _scrollOffset,
            0,
            Math.Max(0, _cachedMetrics.Count - maxBarsVisible)
        );

        int startIndex = _scrollOffset;
        int endIndex = Math.Min(_cachedMetrics.Count, startIndex + maxBarsVisible);

        for (int i = startIndex; i < endIndex; i++)
        {
            SystemMetricEntry entry = _cachedMetrics[i];
            float rowY = y + ((i - startIndex) * rowHeight);

            Color barColor = GetBarColor(entry.LastMs, theme);
            string displayName = TruncateName(entry.Name, nameColWidth - 8, renderer);
            renderer.DrawText(displayName, contentX, rowY, theme.TextPrimary);

            // Bar background
            var barRect = new LayoutRect(barColStart, rowY + 2, barColWidth, lineHeight - 4);
            renderer.DrawRectangle(barRect, theme.BackgroundElevated);

            // Bar fill
            float barPercent = Math.Min((float)(entry.LastMs / TargetFrameTimeMs), 2.0f) / 2.0f;
            float filledWidth = barColWidth * barPercent;
            if (filledWidth > 0)
            {
                var filledRect = new LayoutRect(barColStart, rowY + 2, filledWidth, lineHeight - 4);
                renderer.DrawRectangle(filledRect, barColor);
            }

            // Budget line
            float budgetLineX = barColStart + (barColWidth * 0.5f);
            renderer.DrawRectangle(
                new LayoutRect(budgetLineX, rowY, 1, lineHeight),
                theme.Warning * 0.7f
            );

            // Ms values - right-aligned to match header
            string msText = $"{entry.LastMs:F2}/{entry.AvgMs:F2}";
            float msTextWidth = renderer.MeasureText(msText).X;
            renderer.DrawText(msText, contentRightEdge - msTextWidth, rowY, theme.TextSecondary);
        }

        // Scroll indicator
        if (_cachedMetrics.Count > maxBarsVisible)
        {
            float moreY = y + (maxBarsVisible * rowHeight);
            string scrollText = $"[{startIndex + 1}-{endIndex} of {_cachedMetrics.Count}] (scroll)";
            renderer.DrawText(scrollText, contentX, moreY, theme.TextSecondary);
        }
    }

    private Color GetBarColor(double ms, UITheme theme)
    {
        if (ms >= _warningThresholdMs)
        {
            return theme.Error;
        }

        if (ms >= _warningThresholdMs * 0.5f)
        {
            return theme.Warning;
        }

        if (ms >= _warningThresholdMs * 0.25f)
        {
            return theme.WarningMild;
        }

        return theme.Success;
    }

    private static string TruncateName(string name, float maxWidth, UIRenderer renderer)
    {
        if (renderer.MeasureText(name).X <= maxWidth)
        {
            return name;
        }

        string ellipsis = NerdFontIcons.Ellipsis;
        float ellipsisWidth = renderer.MeasureText(ellipsis).X;
        float targetWidth = maxWidth - ellipsisWidth;

        for (int len = name.Length - 1; len > 0; len--)
        {
            string truncated = name[..len];
            if (renderer.MeasureText(truncated).X <= targetWidth)
            {
                return truncated + ellipsis;
            }
        }

        return ellipsis;
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
