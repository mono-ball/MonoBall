using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoBallFramework.Game.Engine.UI.Components.Base;
using MonoBallFramework.Game.Engine.UI.Components.Controls;
using MonoBallFramework.Game.Engine.UI.Core;
using MonoBallFramework.Game.Engine.UI.Layout;

namespace MonoBallFramework.Game.Engine.UI.Components.Debug;

/// <summary>
///     Content component for StatsPanel that renders performance statistics.
///     Follows the same pattern as TextBuffer for consistent layout behavior.
///     Supports scrolling when content exceeds visible area.
/// </summary>
public class StatsContent : UIComponent
{
    // Performance thresholds for color coding (domain-specific constants)
    private const float FpsExcellent = 60f;
    private const float FpsGood = 55f;
    private const float FpsFair = 30f;
    private const float FrameTimeGood = 16.67f; // 60 FPS target
    private const float FrameTimeWarning = 25f;
    private const float FrameTimeMax = 33.33f; // 30 FPS minimum
    private const double MemoryGood = 256;
    private const double MemoryWarning = 512;
    private const double MemoryMax = 768;
    private const int GcGen0WarningThreshold = 10;
    private const int GcGen1WarningThreshold = 2;
    private const float SystemTimeGood = 2f;
    private const float SystemTimeWarning = 5f;
    private const float TotalSystemTimeGood = 10f;
    private const float TotalSystemTimeWarning = 16f;

    // Content-specific layout constants
    private const int MaxSystemNameLength = 18;
    private const int TruncatedNameLength = 15;

    // Sparkline component for frame time history
    private readonly Sparkline _frameTimeSparkline;

    // Scrolling support
    private readonly ScrollbarComponent _scrollbar = new();

    // Cached stats
    private StatsData _cachedStats = new();
    private double _gcDeltaTimer;
    private int _gen0Delta;
    private int _gen1Delta;
    private int _gen2Delta;

    // GC delta tracking
    private int _lastGen0;
    private int _lastGen1;
    private int _lastGen2;

    // Refresh timing (time-based for frame rate independence)
    private double _lastUpdateTime;
    private float _lastVisibleHeight;
    private double _refreshIntervalSeconds = 0.033; // ~30fps updates by default
    private Func<StatsData>? _statsProvider;
    private float _totalContentHeight;

    public StatsContent(string id)
    {
        Id = id;

        // Create sparkline for frame time history
        _frameTimeSparkline = Sparkline.ForFrameTime(id + "_sparkline");
    }

    // Layout values from theme (accessed dynamically for theme switching)
    private int SectionSpacing => ThemeManager.Current.SectionSpacing;
    private int GcColumnWidth => ThemeManager.Current.TableColumnWidth;
    private int ScrollSpeed => ThemeManager.Current.ScrollSpeed;

    public bool HasProvider { get; private set; }

    // Stats accessors
    public float CurrentFps => _cachedStats.Fps;
    public float CurrentFrameTimeMs => _cachedStats.FrameTimeMs;
    public double CurrentMemoryMB => _cachedStats.MemoryMB;
    public int CurrentEntityCount => _cachedStats.EntityCount;

    public (int Gen0, int Gen1, int Gen2) GCCollections =>
        (_cachedStats.Gen0Collections, _cachedStats.Gen1Collections, _cachedStats.Gen2Collections);

    public (float Min, float Max, float Avg) FrameTimeStats =>
        (_cachedStats.MinFrameTimeMs, _cachedStats.MaxFrameTimeMs, _cachedStats.FrameTimeMs);

    public (int Gen0Delta, int Gen1Delta, int Gen2Delta) GCDeltas =>
        (_gen0Delta, _gen1Delta, _gen2Delta);

    public void SetStatsProvider(Func<StatsData>? provider)
    {
        _statsProvider = provider;
        HasProvider = provider != null;
    }

    /// <summary>
    ///     Sets the refresh interval in seconds.
    /// </summary>
    public void SetRefreshInterval(double intervalSeconds)
    {
        _refreshIntervalSeconds = Math.Clamp(intervalSeconds, 0.016, 1.0);
    }

    /// <summary>
    ///     Gets the refresh interval in seconds.
    /// </summary>
    public double GetRefreshIntervalSeconds()
    {
        return _refreshIntervalSeconds;
    }

    /// <summary>
    ///     Sets the refresh interval in frames (for backward compatibility).
    ///     Converts to seconds assuming 60fps.
    /// </summary>
    public void SetRefreshInterval(int frameInterval)
    {
        _refreshIntervalSeconds = Math.Clamp(frameInterval, 1, 60) / 60.0;
    }

    /// <summary>
    ///     Gets the refresh interval in frames (for backward compatibility).
    /// </summary>
    public int GetRefreshInterval()
    {
        return (int)Math.Round(_refreshIntervalSeconds * 60);
    }

    public void Refresh()
    {
        RefreshStats();
    }

    public (string indicator, Color color, bool isHealthy) GetOverallHealth(UITheme theme)
    {
        bool isHealthy =
            _cachedStats.Fps >= FpsGood && _cachedStats.MemoryMB < MemoryWarning && _gen2Delta < 1;
        bool isWarning = _cachedStats.Fps >= FpsFair && _cachedStats.MemoryMB < MemoryMax;

        if (isHealthy)
        {
            return (NerdFontIcons.StatusHealthy, theme.Success, true);
        }

        if (isWarning)
        {
            return (NerdFontIcons.StatusWarning, theme.Warning, false);
        }

        return (NerdFontIcons.StatusError, theme.Error, false);
    }

    private void RefreshStats()
    {
        if (_statsProvider != null)
        {
            _cachedStats = _statsProvider();
        }

        // Update sparkline
        _frameTimeSparkline.AddValue(_cachedStats.FrameTimeMs);

        // Calculate GC deltas (per second)
        if (_gcDeltaTimer >= 1.0)
        {
            _gen0Delta = _cachedStats.Gen0Collections - _lastGen0;
            _gen1Delta = _cachedStats.Gen1Collections - _lastGen1;
            _gen2Delta = _cachedStats.Gen2Collections - _lastGen2;
            _lastGen0 = _cachedStats.Gen0Collections;
            _lastGen1 = _cachedStats.Gen1Collections;
            _lastGen2 = _cachedStats.Gen2Collections;
            _gcDeltaTimer = 0;
        }
    }

    protected override void OnRender(UIContext context)
    {
        UITheme theme = ThemeManager.Current;
        UIRenderer renderer = Renderer;

        int lineHeight = renderer.GetLineHeight();
        // Use DebugPanelBase.StandardLinePadding for consistent alignment
        // Parent panel already applies Constraint.Padding, so we only add internal line padding
        int linePadding = DebugPanelBase.StandardLinePadding;
        int scrollbarWidth = theme.ScrollbarWidth;
        float visibleHeight = Rect.Height;
        _lastVisibleHeight = visibleHeight;

        // Check if scrollbar is needed
        bool needsScrollbar = _totalContentHeight > visibleHeight;
        float contentWidth = Rect.Width - (linePadding * 2) - (needsScrollbar ? scrollbarWidth : 0);
        float contentX = Rect.X + linePadding;

        float maxScroll = Math.Max(0, _totalContentHeight - visibleHeight);

        // Handle scrollbar input first (before other input to get priority)
        if (context.Input != null && needsScrollbar)
        {
            var scrollbarRect = new LayoutRect(
                Rect.Right - scrollbarWidth,
                Rect.Y + linePadding,
                scrollbarWidth,
                Rect.Height - (linePadding * 2)
            );
            _scrollbar.HandleInput(
                context,
                context.Input,
                scrollbarRect,
                _totalContentHeight,
                visibleHeight,
                Id
            );
        }

        // Handle scroll input (mouse wheel and keyboard)
        if (
            context.Input != null
            && Rect.Contains(context.Input.MousePosition)
            && !_scrollbar.IsDragging
        )
        {
            // Mouse wheel scrolling
            _scrollbar.HandleMouseWheel(context.Input, _totalContentHeight, visibleHeight);

            // Keyboard scrolling
            if (context.Input.IsKeyPressedWithRepeat(Keys.PageUp))
            {
                _scrollbar.ScrollOffset = Math.Max(
                    0,
                    _scrollbar.ScrollOffset - (visibleHeight * 0.8f)
                );
            }
            else if (context.Input.IsKeyPressedWithRepeat(Keys.PageDown))
            {
                _scrollbar.ScrollOffset = Math.Min(
                    maxScroll,
                    _scrollbar.ScrollOffset + (visibleHeight * 0.8f)
                );
            }
            else if (context.Input.IsKeyPressed(Keys.Home))
            {
                _scrollbar.ScrollToTop();
            }
            else if (context.Input.IsKeyPressed(Keys.End))
            {
                _scrollbar.ScrollOffset = maxScroll;
            }
        }

        // Starting Y position with scroll offset applied
        float baseY = Rect.Y + linePadding;
        float y = baseY - _scrollbar.ScrollOffset;

        // Empty state handling
        if (!HasProvider)
        {
            y = EmptyStateComponent.DrawLeftAligned(
                renderer,
                theme,
                contentX,
                y,
                "Stats provider not configured.",
                "Waiting for stats data..."
            );
            _totalContentHeight = y - baseY + _scrollbar.ScrollOffset + linePadding;
            return;
        }

        // Time-based refresh for frame rate independence
        if (context.Input?.GameTime != null)
        {
            double currentTime = context.Input.GameTime.TotalGameTime.TotalSeconds;
            if (currentTime - _lastUpdateTime >= _refreshIntervalSeconds)
            {
                _lastUpdateTime = currentTime;
                RefreshStats();

                // Update GC delta tracking (1 second window)
                _gcDeltaTimer += _refreshIntervalSeconds;
            }
        }

        // Push clip rect to prevent drawing outside visible area
        var clipRect = new LayoutRect(
            Rect.X,
            Rect.Y,
            Rect.Width - (needsScrollbar ? scrollbarWidth : 0),
            Rect.Height
        );
        renderer.PushClip(clipRect);

        // Layout constants from PanelConstants
        float labelWidth = PanelConstants.Stats.LabelWidth;
        float barWidth = PanelConstants.Stats.BarWidth;
        float valueX = contentX + labelWidth;
        float barX = contentX + labelWidth + PanelConstants.Stats.ValueOffset;
        float rowHeight = lineHeight + PanelConstants.Stats.RowSpacing;

        // === HEADER ===
        renderer.DrawText("Performance Stats", contentX, y, theme.Info);
        string frameText = $"Frame: {_cachedStats.FrameNumber:N0}";
        float frameWidth = renderer.MeasureText(frameText).X;
        renderer.DrawText(frameText, contentX + contentWidth - frameWidth, y, theme.TextSecondary);
        y += lineHeight + SectionSpacing;

        // Separator
        renderer.DrawRectangle(new LayoutRect(contentX, y, contentWidth, 1), theme.BorderPrimary);
        y += SectionSpacing;

        // === FPS ROW ===
        Color fpsColor = GetFpsColor(_cachedStats.Fps, theme);
        renderer.DrawText("FPS:", contentX, y, theme.TextSecondary);
        renderer.DrawText($"{_cachedStats.Fps:F1}", valueX, y, fpsColor);
        renderer.DrawRectangle(new LayoutRect(valueX + 50, y + 4, 8, 8), fpsColor);
        string fpsRating = GetFpsRating(_cachedStats.Fps);
        float ratingWidth = renderer.MeasureText(fpsRating).X;
        renderer.DrawText(fpsRating, contentX + contentWidth - ratingWidth, y, fpsColor);
        y += rowHeight;

        // === FRAME TIME ROW ===
        Color frameTimeColor = GetFrameTimeColor(_cachedStats.FrameTimeMs, theme);
        renderer.DrawText("Frame Time:", contentX, y, theme.TextSecondary);
        renderer.DrawText($"{_cachedStats.FrameTimeMs:F2}ms", valueX, y, frameTimeColor);
        DrawProgressBar(
            renderer,
            barX,
            y + 2,
            barWidth,
            lineHeight - 4,
            _cachedStats.FrameTimeMs / FrameTimeMax,
            frameTimeColor,
            theme
        );
        float budgetX = barX + (barWidth * 0.5f);
        renderer.DrawRectangle(new LayoutRect(budgetX, y, 2, lineHeight), theme.Warning);
        y += rowHeight;

        // === FRAME TIME RANGE ===
        renderer.DrawText("Range:", contentX, y, theme.TextSecondary);
        renderer.DrawText(
            $"{_cachedStats.MinFrameTimeMs:F1} - {_cachedStats.MaxFrameTimeMs:F1}ms",
            valueX,
            y,
            theme.TextPrimary
        );
        float budgetPercent = _cachedStats.FrameTimeMs / FrameTimeGood * 100f;
        string budgetText = $"{budgetPercent:F0}% of {FrameTimeGood:F2}ms";
        Color budgetColor =
            budgetPercent <= 80 ? theme.Success
            : budgetPercent <= 100 ? theme.Warning
            : theme.Error;
        float budgetWidth = renderer.MeasureText(budgetText).X;
        renderer.DrawText(budgetText, contentX + contentWidth - budgetWidth, y, budgetColor);
        y += rowHeight;

        // === SPARKLINE ===
        renderer.DrawText("History:", contentX, y, theme.TextSecondary);
        // Draw sparkline inline
        _frameTimeSparkline.Draw(
            renderer,
            valueX,
            y,
            contentWidth - labelWidth - SectionSpacing,
            lineHeight
        );
        y += rowHeight + SectionSpacing;

        // Separator
        renderer.DrawRectangle(new LayoutRect(contentX, y, contentWidth, 1), theme.BorderPrimary);
        y += SectionSpacing;

        // === MEMORY ROW ===
        Color memColor = GetMemoryColor(_cachedStats.MemoryMB, theme);
        renderer.DrawText("Memory:", contentX, y, theme.TextSecondary);
        renderer.DrawText($"{_cachedStats.MemoryMB:F1} MB", valueX, y, memColor);
        DrawProgressBar(
            renderer,
            barX,
            y + 2,
            barWidth,
            lineHeight - 4,
            (float)(_cachedStats.MemoryMB / MemoryWarning),
            memColor,
            theme
        );
        y += rowHeight;

        // === GC ROW ===
        renderer.DrawText("GC:", contentX, y, theme.TextSecondary);
        float gcX = valueX;
        Color gen0Color = _gen0Delta > GcGen0WarningThreshold ? theme.Warning : theme.TextPrimary;
        Color gen1Color = _gen1Delta > GcGen1WarningThreshold ? theme.Warning : theme.TextPrimary;
        Color gen2Color = _gen2Delta > 0 ? theme.Error : theme.TextPrimary;
        renderer.DrawText($"G0: {_cachedStats.Gen0Collections}", gcX, y, gen0Color);
        gcX += GcColumnWidth;
        renderer.DrawText($"G1: {_cachedStats.Gen1Collections}", gcX, y, gen1Color);
        gcX += GcColumnWidth;
        renderer.DrawText($"G2: {_cachedStats.Gen2Collections}", gcX, y, gen2Color);
        string deltaText = $"+{_gen0Delta}/{_gen1Delta}/{_gen2Delta}/s";
        Color deltaColor =
            _gen2Delta > 0 ? theme.Error
            : _gen1Delta > GcGen1WarningThreshold ? theme.Warning
            : theme.TextSecondary;
        float deltaWidth = renderer.MeasureText(deltaText).X;
        renderer.DrawText(deltaText, contentX + contentWidth - deltaWidth, y, deltaColor);
        y += rowHeight + SectionSpacing;

        // Separator
        renderer.DrawRectangle(new LayoutRect(contentX, y, contentWidth, 1), theme.BorderPrimary);
        y += SectionSpacing;

        // === ECS STATS HEADER ===
        renderer.DrawText("ECS World", contentX, y, theme.Info);
        y += lineHeight + SectionSpacing;

        // Separator
        renderer.DrawRectangle(new LayoutRect(contentX, y, contentWidth, 1), theme.BorderPrimary);
        y += SectionSpacing;

        // === ENTITIES ROW ===
        renderer.DrawText("Entities:", contentX, y, theme.TextSecondary);
        string entityText = $"{_cachedStats.EntityCount:N0}";
        renderer.DrawText(entityText, valueX, y, theme.TextPrimary);
        // Right-align archetypes info
        string archetypesText = $"Archetypes: {_cachedStats.ArchetypeCount}";
        float archetypesWidth = renderer.MeasureText(archetypesText).X;
        renderer.DrawText(
            archetypesText,
            contentX + contentWidth - archetypesWidth,
            y,
            theme.TextSecondary
        );
        y += rowHeight;

        // === SYSTEMS ROW ===
        renderer.DrawText("Systems:", contentX, y, theme.TextSecondary);
        renderer.DrawText($"{_cachedStats.SystemCount}", valueX, y, theme.TextPrimary);
        // Right-align total time
        Color systemTimeColor =
            _cachedStats.TotalSystemTimeMs <= TotalSystemTimeGood ? theme.Success
            : _cachedStats.TotalSystemTimeMs <= TotalSystemTimeWarning ? theme.Warning
            : theme.Error;
        string totalTimeText = $"Total: {_cachedStats.TotalSystemTimeMs:F2}ms";
        float totalTimeWidth = renderer.MeasureText(totalTimeText).X;
        renderer.DrawText(
            totalTimeText,
            contentX + contentWidth - totalTimeWidth,
            y,
            systemTimeColor
        );
        y += rowHeight;

        // === SLOWEST SYSTEM ROW ===
        if (!string.IsNullOrEmpty(_cachedStats.SlowestSystemName))
        {
            renderer.DrawText("Slowest:", contentX, y, theme.TextSecondary);
            Color slowestColor =
                _cachedStats.SlowestSystemTimeMs <= SystemTimeGood ? theme.Success
                : _cachedStats.SlowestSystemTimeMs <= SystemTimeWarning ? theme.Warning
                : theme.Error;
            // Show system name (truncate if needed)
            string systemName = _cachedStats.SlowestSystemName;
            // Remove common suffixes for cleaner display
            systemName = systemName.Replace("System", "");
            if (systemName.Length > MaxSystemNameLength)
            {
                systemName = systemName.Substring(0, TruncatedNameLength) + "...";
            }

            renderer.DrawText(systemName, valueX, y, theme.TextPrimary);
            // Right-align slowest time
            string slowestTime = $"{_cachedStats.SlowestSystemTimeMs:F2}ms";
            float slowestTimeWidth = renderer.MeasureText(slowestTime).X;
            renderer.DrawText(
                slowestTime,
                contentX + contentWidth - slowestTimeWidth,
                y,
                slowestColor
            );
            y += rowHeight;
        }

        // === POOL STATS ROW (Entity/Component pools) ===
        if (_cachedStats.PoolCount > 0)
        {
            renderer.DrawText("Entity Pools:", contentX, y, theme.TextSecondary);
            string poolInfoText = $"{_cachedStats.PoolCount} pools";
            renderer.DrawText(poolInfoText, valueX, y, theme.TextPrimary);

            // Build right-aligned pool status
            string activeText = $"{_cachedStats.PooledActive:N0} active";
            string availText = $"{_cachedStats.PooledAvailable:N0} avail";
            string poolStatus = $"{activeText}  {availText}";
            float poolStatusWidth = renderer.MeasureText(poolStatus).X;
            float poolStatusX = contentX + contentWidth - poolStatusWidth;

            // Draw active count in green, available in dim
            renderer.DrawText(activeText, poolStatusX, y, theme.Success);
            float availX = poolStatusX + renderer.MeasureText(activeText + "  ").X;
            renderer.DrawText(availText, availX, y, theme.TextDim);
            y += rowHeight;
        }

        // === EVENT POOL STATS SECTION (if available) ===
        if (_cachedStats.EventPoolCount > 0)
        {
            y += SectionSpacing;

            // Separator
            renderer.DrawRectangle(
                new LayoutRect(contentX, y, contentWidth, 1),
                theme.BorderPrimary
            );
            y += SectionSpacing;

            // Section header
            renderer.DrawText("Event Pools", contentX, y, theme.Info);
            // Show reuse efficiency indicator on the right
            string efficiencyText = $"{_cachedStats.EventPoolAvgReuseRate:P0} reuse";
            Color efficiencyColor =
                _cachedStats.EventPoolAvgReuseRate >= 0.95 ? theme.Success
                : _cachedStats.EventPoolAvgReuseRate >= 0.80 ? theme.Warning
                : theme.Error;
            float efficiencyWidth = renderer.MeasureText(efficiencyText).X;
            renderer.DrawText(
                efficiencyText,
                contentX + contentWidth - efficiencyWidth,
                y,
                efficiencyColor
            );
            y += lineHeight + SectionSpacing;

            // Separator
            renderer.DrawRectangle(
                new LayoutRect(contentX, y, contentWidth, 1),
                theme.BorderPrimary
            );
            y += SectionSpacing;

            // Pool count and allocations
            renderer.DrawText("Event Types:", contentX, y, theme.TextSecondary);
            renderer.DrawText($"{_cachedStats.EventPoolCount}", valueX, y, theme.TextPrimary);
            string allocText = $"{_cachedStats.EventPoolTotalCreated:N0} allocs";
            float allocWidth = renderer.MeasureText(allocText).X;
            renderer.DrawText(allocText, contentX + contentWidth - allocWidth, y, theme.TextDim);
            y += rowHeight;

            // Total rented (published events)
            renderer.DrawText("Published:", contentX, y, theme.TextSecondary);
            string rentedText = $"{_cachedStats.EventPoolTotalRented:N0}";
            renderer.DrawText(rentedText, valueX, y, theme.TextPrimary);
            // Calculate allocation savings
            long saved = _cachedStats.EventPoolTotalRented - _cachedStats.EventPoolTotalCreated;
            if (saved > 0)
            {
                string savedText = $"{saved:N0} saved";
                Color savedColor = theme.Success;
                float savedWidth = renderer.MeasureText(savedText).X;
                renderer.DrawText(savedText, contentX + contentWidth - savedWidth, y, savedColor);
            }

            y += rowHeight;

            // Currently in use (not returned)
            renderer.DrawText("In Flight:", contentX, y, theme.TextSecondary);
            string inUseText = $"{_cachedStats.EventPoolCurrentlyInUse}";
            Color inUseColor =
                _cachedStats.EventPoolCurrentlyInUse < 10 ? theme.Success
                : _cachedStats.EventPoolCurrentlyInUse < 50 ? theme.Warning
                : theme.Error;
            renderer.DrawText(inUseText, valueX, y, inUseColor);
            if (_cachedStats.EventPoolCurrentlyInUse > 0)
            {
                string warningText = "Check for leaks!";
                float warningWidth = renderer.MeasureText(warningText).X;
                renderer.DrawText(
                    warningText,
                    contentX + contentWidth - warningWidth,
                    y,
                    theme.Warning
                );
            }

            y += rowHeight;

            // Most used event type (if available)
            if (!string.IsNullOrEmpty(_cachedStats.MostUsedEventType))
            {
                renderer.DrawText("Hot Event:", contentX, y, theme.TextSecondary);
                // Truncate long event names
                string eventName = _cachedStats.MostUsedEventType.Replace("Event", "");
                if (eventName.Length > MaxSystemNameLength)
                {
                    eventName = eventName.Substring(0, TruncatedNameLength) + "...";
                }

                renderer.DrawText(eventName, valueX, y, theme.Info);
                // Show count on the right
                string countText = $"{_cachedStats.MostUsedEventRented:N0}x";
                float countWidth = renderer.MeasureText(countText).X;
                renderer.DrawText(
                    countText,
                    contentX + contentWidth - countWidth,
                    y,
                    theme.TextPrimary
                );
                y += rowHeight;
            }
        }

        // Pop clip rect
        renderer.PopClip();

        // Calculate total content height (from base to final y, plus padding)
        _totalContentHeight = y - baseY + _scrollbar.ScrollOffset + linePadding;

        // Draw scrollbar if content exceeds visible area
        if (needsScrollbar)
        {
            var scrollbarRect = new LayoutRect(
                Rect.Right - scrollbarWidth,
                Rect.Y + linePadding,
                scrollbarWidth,
                Rect.Height - (linePadding * 2)
            );
            _scrollbar.Draw(renderer, theme, scrollbarRect, _totalContentHeight, visibleHeight);
        }
    }

    private void DrawProgressBar(
        UIRenderer renderer,
        float x,
        float y,
        float width,
        float height,
        float percent,
        Color fillColor,
        UITheme theme
    )
    {
        renderer.DrawRectangle(new LayoutRect(x, y, width, height), theme.BackgroundElevated);
        float fillWidth = width * Math.Clamp(percent, 0, 1);
        if (fillWidth > 0)
        {
            renderer.DrawRectangle(new LayoutRect(x, y, fillWidth, height), fillColor);
        }

        renderer.DrawRectangleOutline(new LayoutRect(x, y, width, height), theme.BorderPrimary);
    }

    private static Color GetFpsColor(float fps, UITheme theme)
    {
        return fps >= FpsGood ? theme.Success
            : fps >= FpsFair ? theme.Warning
            : theme.Error;
    }

    private static string GetFpsRating(float fps)
    {
        return fps >= FpsExcellent ? "Excellent"
            : fps >= FpsGood ? "Good"
            : fps >= FpsFair ? "Fair"
            : "Poor";
    }

    private static Color GetFrameTimeColor(float ms, UITheme theme)
    {
        return ms <= FrameTimeGood ? theme.Success
            : ms <= FrameTimeWarning ? theme.Warning
            : theme.Error;
    }

    private static Color GetMemoryColor(double mb, UITheme theme)
    {
        return mb < MemoryGood ? theme.Success
            : mb < MemoryWarning ? theme.Warning
            : theme.Error;
    }

    protected override bool IsInteractive()
    {
        return true; // Enable input for scrolling
    }

    /// <summary>
    ///     Scrolls to the top of the content.
    /// </summary>
    public void ScrollToTop()
    {
        _scrollbar.ScrollToTop();
    }

    /// <summary>
    ///     Scrolls to the bottom of the content.
    /// </summary>
    public void ScrollToBottom()
    {
        _scrollbar.ScrollToBottom(_totalContentHeight, _lastVisibleHeight);
    }
}
