using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using PokeSharp.Engine.UI.Debug.Components.Base;
using PokeSharp.Engine.UI.Debug.Components.Controls;
using PokeSharp.Engine.UI.Debug.Core;
using PokeSharp.Engine.UI.Debug.Input;
using PokeSharp.Engine.UI.Debug.Layout;

namespace PokeSharp.Engine.UI.Debug.Components.Debug;

/// <summary>
///     Content component for StatsPanel that renders performance statistics.
///     Follows the same pattern as TextBuffer for consistent layout behavior.
///     Supports scrolling when content exceeds visible area.
/// </summary>
public class StatsContent : UIComponent
{
    // Performance thresholds for color coding
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

    // Layout constants
    private const int SectionSpacing = 8;
    private const int GcColumnWidth = 80;
    private const int MaxSystemNameLength = 18;
    private const int TruncatedNameLength = 15;

    // Scrolling constants
    private const int ScrollSpeed = 30;

    // Sparkline component for frame time history
    private readonly Sparkline _frameTimeSparkline;

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

    // Scrolling support
    private float _scrollOffset;
    private Func<StatsData>? _statsProvider;
    private float _totalContentHeight;

    // Scrollbar drag tracking
    private bool _isDraggingScrollbar;
    private float _scrollbarDragStartY;
    private float _scrollbarDragStartOffset;

    public StatsContent(string id)
    {
        Id = id;

        // Create sparkline for frame time history
        _frameTimeSparkline = Sparkline.ForFrameTime(id + "_sparkline");
    }

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
            HandleScrollbarInput(context, context.Input, visibleHeight, maxScroll);
        }

        // Handle scroll input (mouse wheel and keyboard)
        if (context.Input != null && Rect.Contains(context.Input.MousePosition) && !_isDraggingScrollbar)
        {
            // Mouse wheel scrolling
            if (context.Input.ScrollWheelDelta != 0)
            {
                _scrollOffset -= context.Input.ScrollWheelDelta > 0 ? ScrollSpeed : -ScrollSpeed;
                _scrollOffset = Math.Clamp(_scrollOffset, 0, maxScroll);
            }

            // Keyboard scrolling
            if (context.Input.IsKeyPressedWithRepeat(Keys.PageUp))
            {
                _scrollOffset = Math.Max(0, _scrollOffset - (visibleHeight * 0.8f));
            }
            else if (context.Input.IsKeyPressedWithRepeat(Keys.PageDown))
            {
                _scrollOffset = Math.Min(maxScroll, _scrollOffset + (visibleHeight * 0.8f));
            }
            else if (context.Input.IsKeyPressed(Keys.Home))
            {
                _scrollOffset = 0;
            }
            else if (context.Input.IsKeyPressed(Keys.End))
            {
                _scrollOffset = maxScroll;
            }
        }

        // Starting Y position with scroll offset applied
        float baseY = Rect.Y + linePadding;
        float y = baseY - _scrollOffset;

        // Empty state handling
        if (!HasProvider)
        {
            renderer.DrawText("Stats provider not configured.", contentX, y, theme.TextDim);
            y += lineHeight;
            renderer.DrawText("Waiting for stats data...", contentX, y, theme.TextDim);
            _totalContentHeight = y - baseY + _scrollOffset + linePadding;
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

        // === POOL STATS ROW (if available) ===
        if (_cachedStats.PoolCount > 0)
        {
            renderer.DrawText("Pools:", contentX, y, theme.TextSecondary);
            // Format: "4 pools | 1601 active | 3500 avail"
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

        // Pop clip rect
        renderer.PopClip();

        // Calculate total content height (from base to final y, plus padding)
        _totalContentHeight = y - baseY + _scrollOffset + linePadding;

        // Draw scrollbar if content exceeds visible area
        if (needsScrollbar)
        {
            DrawScrollbar(renderer, theme, visibleHeight);
        }
    }

    /// <summary>
    ///     Handles all scrollbar mouse interactions with proper input capture.
    /// </summary>
    private void HandleScrollbarInput(
        UIContext context,
        InputState input,
        float visibleHeight,
        float maxScroll
    )
    {
        int scrollbarWidth = ThemeManager.Current.ScrollbarWidth;
        float trackX = Rect.Right - scrollbarWidth;
        float trackY = Rect.Y;
        float trackHeight = Rect.Height;

        var scrollbarRect = new LayoutRect(trackX, trackY, scrollbarWidth, trackHeight);

        // Calculate thumb size and position (minimum thumb height for usability)
        const float MinThumbHeight = 20f;
        float thumbHeight = Math.Max(
            MinThumbHeight,
            visibleHeight / _totalContentHeight * trackHeight
        );
        float scrollRatio = maxScroll > 0 ? _scrollOffset / maxScroll : 0;
        float thumbY = trackY + (scrollRatio * (trackHeight - thumbHeight));

        var thumbRect = new LayoutRect(trackX, thumbY, scrollbarWidth, thumbHeight);

        bool isOverScrollbar = scrollbarRect.Contains(input.MousePosition);

        // Handle dragging (continues even outside bounds due to input capture)
        if (_isDraggingScrollbar)
        {
            if (input.IsMouseButtonDown(MouseButton.Left))
            {
                int deltaY = input.MousePosition.Y - (int)_scrollbarDragStartY;
                float dragRatio = deltaY / trackHeight;
                float scrollDelta = dragRatio * _totalContentHeight;

                _scrollOffset = Math.Clamp(_scrollbarDragStartOffset + scrollDelta, 0, maxScroll);
            }

            // Handle mouse release (end drag)
            if (input.IsMouseButtonReleased(MouseButton.Left))
            {
                _isDraggingScrollbar = false;
                context.ReleaseCapture();
            }
        }
        // Handle new click on scrollbar
        else if (isOverScrollbar && input.IsMouseButtonPressed(MouseButton.Left))
        {
            // Capture input so drag continues even if mouse leaves scrollbar
            context.CaptureInput(Id);

            // Check if clicking on thumb (drag) or track (jump)
            if (thumbRect.Contains(input.MousePosition))
            {
                // Start dragging the thumb
                _isDraggingScrollbar = true;
                _scrollbarDragStartY = input.MousePosition.Y;
                _scrollbarDragStartOffset = _scrollOffset;
            }
            else
            {
                // Click on track - jump to that position immediately
                float clickRatio = (input.MousePosition.Y - trackY) / trackHeight;
                float targetScroll = clickRatio * _totalContentHeight - (visibleHeight / 2);
                _scrollOffset = Math.Clamp(targetScroll, 0, maxScroll);

                // Also start dragging from this new position in case they want to continue dragging
                _isDraggingScrollbar = true;
                _scrollbarDragStartY = input.MousePosition.Y;
                _scrollbarDragStartOffset = _scrollOffset;
            }

            // Consume the mouse button to prevent other components from processing
            input.ConsumeMouseButton(MouseButton.Left);
        }
    }

    /// <summary>
    ///     Draws a scrollbar on the right side of the content area.
    /// </summary>
    private void DrawScrollbar(UIRenderer renderer, UITheme theme, float visibleHeight)
    {
        int scrollbarWidth = theme.ScrollbarWidth;
        float trackX = Rect.Right - scrollbarWidth;
        float trackY = Rect.Y;
        float trackHeight = Rect.Height;

        // Draw track
        var trackRect = new LayoutRect(trackX, trackY, scrollbarWidth, trackHeight);
        renderer.DrawRectangle(trackRect, theme.ScrollbarTrack);

        // Calculate thumb size and position (minimum thumb height for usability)
        const float MinThumbHeight = 20f;
        float thumbHeight = Math.Max(
            MinThumbHeight,
            visibleHeight / _totalContentHeight * trackHeight
        );
        float maxScroll = _totalContentHeight - visibleHeight;
        float scrollRatio = maxScroll > 0 ? _scrollOffset / maxScroll : 0;
        float thumbY = trackY + (scrollRatio * (trackHeight - thumbHeight));

        // Draw thumb
        var thumbRect = new LayoutRect(trackX, thumbY, scrollbarWidth, thumbHeight);
        renderer.DrawRectangle(thumbRect, theme.ScrollbarThumb);
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
        _scrollOffset = 0;
    }

    /// <summary>
    ///     Scrolls to the bottom of the content.
    /// </summary>
    public void ScrollToBottom()
    {
        float maxScroll = Math.Max(0, _totalContentHeight - _lastVisibleHeight);
        _scrollOffset = maxScroll;
    }
}
