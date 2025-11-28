using Microsoft.Xna.Framework;
using PokeSharp.Engine.UI.Debug.Components.Base;
using PokeSharp.Engine.UI.Debug.Core;
using PokeSharp.Engine.UI.Debug.Layout;
using System;

namespace PokeSharp.Engine.UI.Debug.Components.Controls;

/// <summary>
/// A sparkline component for displaying time-series data as a mini bar chart.
/// Supports color-coded thresholds, reference lines, and auto-scaling.
/// </summary>
public class Sparkline : UIComponent
{
    private readonly float[] _data;
    private int _dataIndex = 0;
    private int _dataCount = 0;

    // Scaling
    private float? _fixedMin;
    private float? _fixedMax;

    // Reference line (e.g., 16.67ms for frame budget)
    private float? _referenceLine;
    private Color? _referenceLineColor;

    // Thresholds for color coding
    private float _warningThreshold = float.MaxValue;
    private float _errorThreshold = float.MaxValue;

    // Visual properties
    private Color? _backgroundColor;
    private Color? _borderColor;
    private Color? _goodColor;
    private Color? _warningColor;
    private Color? _errorColor;
    private float _barGap = 1f;
    private bool _showBorder = true;

    /// <summary>
    /// Creates a sparkline with the specified data buffer size.
    /// </summary>
    /// <param name="id">Component ID</param>
    /// <param name="bufferSize">Number of data points to display (default: 60)</param>
    public Sparkline(string id, int bufferSize = 60)
    {
        Id = id;
        _data = new float[Math.Max(1, bufferSize)];
    }

    #region Configuration

    /// <summary>
    /// Sets fixed min/max values for scaling. Use null for auto-scaling.
    /// </summary>
    public Sparkline WithScale(float? min, float? max)
    {
        _fixedMin = min;
        _fixedMax = max;
        return this;
    }

    /// <summary>
    /// Sets the reference line value (drawn as a horizontal line).
    /// </summary>
    public Sparkline WithReferenceLine(float value, Color? color = null)
    {
        _referenceLine = value;
        _referenceLineColor = color;
        return this;
    }

    /// <summary>
    /// Clears the reference line.
    /// </summary>
    public Sparkline WithoutReferenceLine()
    {
        _referenceLine = null;
        return this;
    }

    /// <summary>
    /// Sets color thresholds. Values below warning are "good", between warning and error are "warning",
    /// above error are "error".
    /// </summary>
    public Sparkline WithThresholds(float warning, float error)
    {
        _warningThreshold = warning;
        _errorThreshold = error;
        return this;
    }

    /// <summary>
    /// Sets custom colors. Use null to fall back to theme colors.
    /// </summary>
    public Sparkline WithColors(Color? good = null, Color? warning = null, Color? error = null)
    {
        _goodColor = good;
        _warningColor = warning;
        _errorColor = error;
        return this;
    }

    /// <summary>
    /// Sets background color. Use null for theme default.
    /// </summary>
    public Sparkline WithBackground(Color? color)
    {
        _backgroundColor = color;
        return this;
    }

    /// <summary>
    /// Sets border color. Use null for theme default.
    /// </summary>
    public Sparkline WithBorder(Color? color, bool show = true)
    {
        _borderColor = color;
        _showBorder = show;
        return this;
    }

    /// <summary>
    /// Sets the gap between bars (default: 1).
    /// </summary>
    public Sparkline WithBarGap(float gap)
    {
        _barGap = Math.Max(0, gap);
        return this;
    }

    #endregion

    #region Data Management

    /// <summary>
    /// Adds a data point to the sparkline (circular buffer).
    /// </summary>
    public void AddValue(float value)
    {
        _data[_dataIndex] = value;
        _dataIndex = (_dataIndex + 1) % _data.Length;
        _dataCount = Math.Min(_dataCount + 1, _data.Length);
    }

    /// <summary>
    /// Clears all data.
    /// </summary>
    public void Clear()
    {
        Array.Clear(_data, 0, _data.Length);
        _dataIndex = 0;
        _dataCount = 0;
    }

    /// <summary>
    /// Gets the current data count.
    /// </summary>
    public int DataCount => _dataCount;

    /// <summary>
    /// Gets the buffer size.
    /// </summary>
    public int BufferSize => _data.Length;

    /// <summary>
    /// Gets the latest value, or null if no data.
    /// </summary>
    public float? LatestValue => _dataCount > 0
        ? _data[(_dataIndex - 1 + _data.Length) % _data.Length]
        : null;

    /// <summary>
    /// Gets min/max/avg of current data.
    /// </summary>
    public (float Min, float Max, float Avg) GetStatistics()
    {
        if (_dataCount == 0)
            return (0, 0, 0);

        float min = float.MaxValue;
        float max = float.MinValue;
        float sum = 0;
        int count = 0;

        for (int i = 0; i < _data.Length; i++)
        {
            var val = _data[i];
            if (val > 0 || _dataCount == _data.Length) // Include zeros only if buffer is full
            {
                min = Math.Min(min, val);
                max = Math.Max(max, val);
                sum += val;
                count++;
            }
        }

        return count > 0 ? (min, max, sum / count) : (0, 0, 0);
    }

    #endregion

    #region Rendering

    /// <summary>
    /// Draws the sparkline at the specified position and size.
    /// Use this for inline rendering without layout resolution.
    /// </summary>
    public void Draw(UIRenderer renderer, float x, float y, float width, float height)
    {
        DrawInternal(renderer, new LayoutRect(x, y, width, height));
    }

    protected override void OnRender(UIContext context)
    {
        DrawInternal(Renderer, Rect);
    }

    private void DrawInternal(UIRenderer renderer, LayoutRect rect)
    {
        var theme = ThemeManager.Current;

        // Background
        var bgColor = _backgroundColor ?? theme.BackgroundElevated;
        renderer.DrawRectangle(rect, bgColor);

        // Calculate scaling
        var (dataMin, dataMax, _) = GetStatistics();
        var minVal = _fixedMin ?? (dataMin > 0 ? dataMin * 0.9f : 0);
        var maxVal = _fixedMax ?? Math.Max(dataMax * 1.1f, minVal + 1);
        var range = maxVal - minVal;
        if (range <= 0) range = 1;

        // Draw reference line (behind bars)
        if (_referenceLine.HasValue)
        {
            var refY = rect.Y + rect.Height - ((_referenceLine.Value - minVal) / range * rect.Height);
            if (refY >= rect.Y && refY <= rect.Y + rect.Height)
            {
                var refColor = _referenceLineColor ?? (theme.Warning * 0.5f);
                renderer.DrawRectangle(new LayoutRect(rect.X, refY, rect.Width, 1), refColor);
            }
        }

        // Draw bars
        if (_dataCount > 0)
        {
            var barWidth = (rect.Width - _barGap * (_data.Length - 1)) / _data.Length;
            var goodColor = _goodColor ?? theme.Success;
            var warnColor = _warningColor ?? theme.Warning;
            var errColor = _errorColor ?? theme.Error;

            for (int i = 0; i < _data.Length; i++)
            {
                // Get value in chronological order (oldest first)
                var dataIdx = (_dataIndex + i) % _data.Length;
                var val = _data[dataIdx];

                // Skip uninitialized values
                if (val <= 0 && _dataCount < _data.Length && dataIdx >= _dataCount)
                    continue;

                // Calculate bar dimensions
                var barHeight = Math.Max(1, ((val - minVal) / range) * rect.Height);
                var barX = rect.X + i * (barWidth + _barGap);
                var barY = rect.Y + rect.Height - barHeight;

                // Determine color based on thresholds
                var color = val >= _errorThreshold ? errColor :
                           val >= _warningThreshold ? warnColor : goodColor;

                renderer.DrawRectangle(
                    new LayoutRect(barX, barY, Math.Max(1, barWidth - _barGap), barHeight),
                    color
                );
            }
        }

        // Border
        if (_showBorder)
        {
            var borderColor = _borderColor ?? theme.BorderPrimary;
            renderer.DrawRectangleOutline(rect, borderColor, 1);
        }
    }

    protected override bool IsInteractive() => false;

    #endregion

    #region Static Factory Methods

    /// <summary>
    /// Creates a sparkline configured for frame time display.
    /// Uses auto-scaling to show variation in frame times clearly.
    /// </summary>
    public static Sparkline ForFrameTime(string id, int bufferSize = 60)
    {
        return new Sparkline(id, bufferSize)
            .WithReferenceLine(16.67f) // 60fps target
            .WithThresholds(16.67f, 25f); // Warning at budget, error at 25ms
            // Note: No fixed scale - auto-scales to show frame time variation clearly
    }

    /// <summary>
    /// Creates a sparkline configured for FPS display.
    /// </summary>
    public static Sparkline ForFps(string id, int bufferSize = 60)
    {
        return new Sparkline(id, bufferSize)
            .WithReferenceLine(60f)
            .WithThresholds(55f, 30f) // Note: inverted - higher is better
            .WithScale(0, 120f);
    }

    /// <summary>
    /// Creates a sparkline configured for percentage values (0-100).
    /// </summary>
    public static Sparkline ForPercentage(string id, int bufferSize = 60, float warningPercent = 80f, float errorPercent = 95f)
    {
        return new Sparkline(id, bufferSize)
            .WithThresholds(warningPercent, errorPercent)
            .WithScale(0, 100f);
    }

    #endregion
}

