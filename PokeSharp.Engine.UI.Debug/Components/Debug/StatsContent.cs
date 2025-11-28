using Microsoft.Xna.Framework;
using PokeSharp.Engine.UI.Debug.Components.Base;
using PokeSharp.Engine.UI.Debug.Components.Controls;
using PokeSharp.Engine.UI.Debug.Core;
using PokeSharp.Engine.UI.Debug.Layout;
using System;

namespace PokeSharp.Engine.UI.Debug.Components.Debug;

/// <summary>
/// Content component for StatsPanel that renders performance statistics.
/// Follows the same pattern as TextBuffer for consistent layout behavior.
/// </summary>
public class StatsContent : UIComponent
{
    private Func<StatsData>? _statsProvider;
    private bool _hasProvider = false;

    // Sparkline component for frame time history
    private readonly Sparkline _frameTimeSparkline;

    // GC delta tracking
    private int _lastGen0 = 0;
    private int _lastGen1 = 0;
    private int _lastGen2 = 0;
    private double _gcDeltaTimer = 0;
    private int _gen0Delta = 0;
    private int _gen1Delta = 0;
    private int _gen2Delta = 0;

    // Refresh timing (time-based for frame rate independence)
    private double _lastUpdateTime = 0;
    private double _refreshIntervalSeconds = 0.033; // ~30fps updates by default

    // Cached stats
    private StatsData _cachedStats = new();

    public StatsContent(string id)
    {
        Id = id;

        // Create sparkline for frame time history
        _frameTimeSparkline = Sparkline.ForFrameTime(id + "_sparkline", 60);
    }

    public void SetStatsProvider(Func<StatsData>? provider)
    {
        _statsProvider = provider;
        _hasProvider = provider != null;
    }

    public bool HasProvider => _hasProvider;

    /// <summary>
    /// Sets the refresh interval in seconds.
    /// </summary>
    public void SetRefreshInterval(double intervalSeconds) =>
        _refreshIntervalSeconds = Math.Clamp(intervalSeconds, 0.016, 1.0);

    /// <summary>
    /// Gets the refresh interval in seconds.
    /// </summary>
    public double GetRefreshIntervalSeconds() => _refreshIntervalSeconds;

    /// <summary>
    /// Sets the refresh interval in frames (for backward compatibility).
    /// Converts to seconds assuming 60fps.
    /// </summary>
    public void SetRefreshInterval(int frameInterval) =>
        _refreshIntervalSeconds = Math.Clamp(frameInterval, 1, 60) / 60.0;

    /// <summary>
    /// Gets the refresh interval in frames (for backward compatibility).
    /// </summary>
    public int GetRefreshInterval() => (int)Math.Round(_refreshIntervalSeconds * 60);
    public void Refresh() => RefreshStats();

    // Stats accessors
    public float CurrentFps => _cachedStats.Fps;
    public float CurrentFrameTimeMs => _cachedStats.FrameTimeMs;
    public double CurrentMemoryMB => _cachedStats.MemoryMB;
    public int CurrentEntityCount => _cachedStats.EntityCount;
    public (int Gen0, int Gen1, int Gen2) GCCollections => (_cachedStats.Gen0Collections, _cachedStats.Gen1Collections, _cachedStats.Gen2Collections);
    public (float Min, float Max, float Avg) FrameTimeStats => (_cachedStats.MinFrameTimeMs, _cachedStats.MaxFrameTimeMs, _cachedStats.FrameTimeMs);
    public (int Gen0Delta, int Gen1Delta, int Gen2Delta) GCDeltas => (_gen0Delta, _gen1Delta, _gen2Delta);

    public (string indicator, Color color, bool isHealthy) GetOverallHealth(UITheme theme)
    {
        var isHealthy = _cachedStats.Fps >= 55 && _cachedStats.MemoryMB < 512 && _gen2Delta < 1;
        var isWarning = _cachedStats.Fps >= 30 && _cachedStats.MemoryMB < 768;

        if (isHealthy) return (Core.NerdFontIcons.StatusHealthy, theme.Success, true);
        if (isWarning) return (Core.NerdFontIcons.StatusWarning, theme.Warning, false);
        return (Core.NerdFontIcons.StatusError, theme.Error, false);
    }

    private void RefreshStats()
    {
        if (_statsProvider != null)
            _cachedStats = _statsProvider();

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
        var theme = ThemeManager.Current;
        var renderer = Renderer;

        var lineHeight = renderer.GetLineHeight();
        // Use DebugPanelBase.StandardLinePadding for consistent alignment
        // Parent panel already applies Constraint.Padding, so we only add internal line padding
        var linePadding = DebugPanelBase.StandardLinePadding;
        var y = Rect.Y + linePadding;
        var contentX = Rect.X + linePadding;
        var contentWidth = Rect.Width - linePadding * 2;

        // Empty state handling
        if (!_hasProvider)
        {
            renderer.DrawText("Stats provider not configured.", contentX, y, theme.TextDim);
            y += lineHeight;
            renderer.DrawText("Waiting for stats data...", contentX, y, theme.TextDim);
            return;
        }

        // Time-based refresh for frame rate independence
        if (context.Input?.GameTime != null)
        {
            var currentTime = context.Input.GameTime.TotalGameTime.TotalSeconds;
            if (currentTime - _lastUpdateTime >= _refreshIntervalSeconds)
            {
                _lastUpdateTime = currentTime;
                RefreshStats();

                // Update GC delta tracking (1 second window)
                _gcDeltaTimer += _refreshIntervalSeconds;
            }
        }

        // Layout constants from PanelConstants
        var labelWidth = PanelConstants.Stats.LabelWidth;
        var barWidth = PanelConstants.Stats.BarWidth;
        var valueX = contentX + labelWidth;
        var barX = contentX + labelWidth + PanelConstants.Stats.ValueOffset;
        var rowHeight = lineHeight + PanelConstants.Stats.RowSpacing;

        // === HEADER ===
        renderer.DrawText("Performance Stats", contentX, y, theme.Info);
        var frameText = $"Frame: {_cachedStats.FrameNumber:N0}";
        var frameWidth = renderer.MeasureText(frameText).X;
        renderer.DrawText(frameText, contentX + contentWidth - frameWidth, y, theme.TextSecondary);
        y += lineHeight + 8;

        // Separator
        renderer.DrawRectangle(new LayoutRect(contentX, y, contentWidth, 1), theme.BorderPrimary);
        y += 8;

        // === FPS ROW ===
        var fpsColor = GetFpsColor(_cachedStats.Fps, theme);
        renderer.DrawText("FPS:", contentX, y, theme.TextSecondary);
        renderer.DrawText($"{_cachedStats.Fps:F1}", valueX, y, fpsColor);
        renderer.DrawRectangle(new LayoutRect(valueX + 50, y + 4, 8, 8), fpsColor);
        var fpsRating = GetFpsRating(_cachedStats.Fps);
        var ratingWidth = renderer.MeasureText(fpsRating).X;
        renderer.DrawText(fpsRating, contentX + contentWidth - ratingWidth, y, fpsColor);
        y += rowHeight;

        // === FRAME TIME ROW ===
        var frameTimeColor = GetFrameTimeColor(_cachedStats.FrameTimeMs, theme);
        renderer.DrawText("Frame Time:", contentX, y, theme.TextSecondary);
        renderer.DrawText($"{_cachedStats.FrameTimeMs:F2}ms", valueX, y, frameTimeColor);
        DrawProgressBar(renderer, barX, y + 2, barWidth, lineHeight - 4,
            _cachedStats.FrameTimeMs / 33.33f, frameTimeColor, theme);
        var budgetX = barX + (barWidth * 0.5f);
        renderer.DrawRectangle(new LayoutRect(budgetX, y, 2, lineHeight), theme.Warning);
        y += rowHeight;

        // === FRAME TIME RANGE ===
        renderer.DrawText("Range:", contentX, y, theme.TextSecondary);
        renderer.DrawText($"{_cachedStats.MinFrameTimeMs:F1} - {_cachedStats.MaxFrameTimeMs:F1}ms", valueX, y, theme.TextPrimary);
        var budgetPercent = (_cachedStats.FrameTimeMs / 16.67f) * 100f;
        var budgetText = $"{budgetPercent:F0}% of 16.67ms";
        var budgetColor = budgetPercent <= 80 ? theme.Success : budgetPercent <= 100 ? theme.Warning : theme.Error;
        var budgetWidth = renderer.MeasureText(budgetText).X;
        renderer.DrawText(budgetText, contentX + contentWidth - budgetWidth, y, budgetColor);
        y += rowHeight;

        // === SPARKLINE ===
        renderer.DrawText("History:", contentX, y, theme.TextSecondary);
        // Draw sparkline inline
        _frameTimeSparkline.Draw(renderer, valueX, y, contentWidth - labelWidth - 8, lineHeight);
        y += rowHeight + 8;

        // Separator
        renderer.DrawRectangle(new LayoutRect(contentX, y, contentWidth, 1), theme.BorderPrimary);
        y += 8;

        // === MEMORY ROW ===
        var memColor = GetMemoryColor(_cachedStats.MemoryMB, theme);
        renderer.DrawText("Memory:", contentX, y, theme.TextSecondary);
        renderer.DrawText($"{_cachedStats.MemoryMB:F1} MB", valueX, y, memColor);
        DrawProgressBar(renderer, barX, y + 2, barWidth, lineHeight - 4,
            (float)(_cachedStats.MemoryMB / 512.0), memColor, theme);
        y += rowHeight;

        // === GC ROW ===
        renderer.DrawText("GC:", contentX, y, theme.TextSecondary);
        var gcX = valueX;
        var gen0Color = _gen0Delta > 10 ? theme.Warning : theme.TextPrimary;
        var gen1Color = _gen1Delta > 2 ? theme.Warning : theme.TextPrimary;
        var gen2Color = _gen2Delta > 0 ? theme.Error : theme.TextPrimary;
        renderer.DrawText($"G0: {_cachedStats.Gen0Collections}", gcX, y, gen0Color);
        gcX += 80;
        renderer.DrawText($"G1: {_cachedStats.Gen1Collections}", gcX, y, gen1Color);
        gcX += 80;
        renderer.DrawText($"G2: {_cachedStats.Gen2Collections}", gcX, y, gen2Color);
        var deltaText = $"+{_gen0Delta}/{_gen1Delta}/{_gen2Delta}/s";
        var deltaColor = _gen2Delta > 0 ? theme.Error : _gen1Delta > 2 ? theme.Warning : theme.TextSecondary;
        var deltaWidth = renderer.MeasureText(deltaText).X;
        renderer.DrawText(deltaText, contentX + contentWidth - deltaWidth, y, deltaColor);
        y += rowHeight + 8;

        // Separator
        renderer.DrawRectangle(new LayoutRect(contentX, y, contentWidth, 1), theme.BorderPrimary);
        y += 8;

        // === WORLD STATS ===
        renderer.DrawText("Entities:", contentX, y, theme.TextSecondary);
        renderer.DrawText($"{_cachedStats.EntityCount:N0}", valueX, y, theme.TextPrimary);
        // Use consistent spacing for Systems label (same labelWidth gap as other rows)
        var systemsLabelX = valueX + 100;
        renderer.DrawText("Systems:", systemsLabelX, y, theme.TextSecondary);
        renderer.DrawText($"{_cachedStats.SystemCount}", systemsLabelX + labelWidth, y, theme.TextPrimary);
    }

    private void DrawProgressBar(UIRenderer renderer, float x, float y, float width, float height, float percent, Color fillColor, UITheme theme)
    {
        renderer.DrawRectangle(new LayoutRect(x, y, width, height), theme.BackgroundElevated);
        var fillWidth = width * Math.Clamp(percent, 0, 1);
        if (fillWidth > 0)
            renderer.DrawRectangle(new LayoutRect(x, y, fillWidth, height), fillColor);
        renderer.DrawRectangleOutline(new LayoutRect(x, y, width, height), theme.BorderPrimary, 1);
    }

    private static Color GetFpsColor(float fps, UITheme theme) =>
        fps >= 55 ? theme.Success : fps >= 30 ? theme.Warning : theme.Error;

    private static string GetFpsRating(float fps) =>
        fps >= 60 ? "Excellent" : fps >= 55 ? "Good" : fps >= 30 ? "Fair" : "Poor";

    private static Color GetFrameTimeColor(float ms, UITheme theme) =>
        ms <= 16.67f ? theme.Success : ms <= 25f ? theme.Warning : theme.Error;

    private static Color GetMemoryColor(double mb, UITheme theme) =>
        mb < 256 ? theme.Success : mb < 512 ? theme.Warning : theme.Error;

    protected override bool IsInteractive() => false;
}

