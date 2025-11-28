using Microsoft.Xna.Framework;
using PokeSharp.Engine.UI.Debug.Components.Base;
using PokeSharp.Engine.UI.Debug.Core;
using PokeSharp.Engine.UI.Debug.Layout;

namespace PokeSharp.Engine.UI.Debug.Components.Controls;

/// <summary>
/// A status bar component for displaying panel statistics and hints.
/// Designed to be positioned at the bottom of panels.
/// Supports both stats text (left-aligned) and hints text (right-aligned or dimmed).
/// </summary>
public class StatusBar : UIComponent
{
    private string _statsText = string.Empty;
    private string _hintsText = string.Empty;

    // Visual properties - nullable for theme fallback
    private Color? _statsColor;
    private Color? _hintsColor;
    private Color? _separatorColor;
    private Color? _backgroundColor;

    public Color StatsColor { get => _statsColor ?? ThemeManager.Current.Success; set => _statsColor = value; }
    public Color HintsColor { get => _hintsColor ?? ThemeManager.Current.TextDim; set => _hintsColor = value; }
    public Color SeparatorColor { get => _separatorColor ?? ThemeManager.Current.BorderPrimary; set => _separatorColor = value; }
    public Color BackgroundColor { get => _backgroundColor ?? ThemeManager.Current.ConsoleBackground; set => _backgroundColor = value; }

    /// <summary>Resets StatsColor to use theme default.</summary>
    public void ResetStatsColor() => _statsColor = null;

    /// <summary>Resets HintsColor to use theme default.</summary>
    public void ResetHintsColor() => _hintsColor = null;

    /// <summary>
    /// Padding around text content. Matches TextBuffer.LinePadding for consistent alignment.
    /// </summary>
    public float Padding { get; set; } = 5f;
    public bool ShowSeparator { get; set; } = true;

    public StatusBar(string id)
    {
        Id = id;
    }

    /// <summary>
    /// Sets the statistics text (left side).
    /// </summary>
    public void SetStats(string text)
    {
        _statsText = text ?? string.Empty;
    }

    /// <summary>
    /// Sets the hints text (right side, dimmer).
    /// </summary>
    public void SetHints(string text)
    {
        _hintsText = text ?? string.Empty;
    }

    /// <summary>
    /// Sets both stats and hints at once.
    /// </summary>
    public void Set(string stats, string hints)
    {
        _statsText = stats ?? string.Empty;
        _hintsText = hints ?? string.Empty;
    }

    /// <summary>
    /// Clears all text.
    /// </summary>
    public void Clear()
    {
        _statsText = string.Empty;
        _hintsText = string.Empty;
    }

    /// <summary>
    /// Gets the current stats text.
    /// </summary>
    public string StatsText => _statsText;

    /// <summary>
    /// Gets the current hints text.
    /// </summary>
    public string HintsText => _hintsText;

    /// <summary>
    /// Always reserve space for the status bar (even when empty).
    /// </summary>
    public bool AlwaysReserveSpace { get; set; } = true;

    /// <summary>
    /// Calculates the desired height for this status bar.
    /// </summary>
    public float GetDesiredHeight(UIRenderer? renderer = null)
    {
        // Return 0 only if we shouldn't reserve space and have no content
        if (!AlwaysReserveSpace && string.IsNullOrEmpty(_statsText) && string.IsNullOrEmpty(_hintsText))
            return 0;

        float lineHeight = 20f; // Default

        if (renderer != null)
        {
            lineHeight = renderer.GetLineHeight();
        }
        else
        {
            try
            {
                if (Renderer != null)
                    lineHeight = Renderer.GetLineHeight();
            }
            catch
            {
                // No context available, use default
            }
        }

        var separatorHeight = ShowSeparator ? 1 : 0;
        return lineHeight + Padding * 2 + separatorHeight;
    }

    protected override void OnRender(UIContext context)
    {
        if (string.IsNullOrEmpty(_statsText) && string.IsNullOrEmpty(_hintsText))
            return;

        var renderer = Renderer;
        var resolvedRect = Rect;

        // Draw background
        if (BackgroundColor.A > 0)
        {
            renderer.DrawRectangle(resolvedRect, BackgroundColor);
        }

        // Draw separator line at top
        if (ShowSeparator)
        {
            renderer.DrawRectangle(
                new LayoutRect(resolvedRect.X, resolvedRect.Y, resolvedRect.Width, 1),
                SeparatorColor);
        }

        var textY = resolvedRect.Y + (ShowSeparator ? 1 : 0) + Padding;

        // Draw stats text (left-aligned)
        if (!string.IsNullOrEmpty(_statsText))
        {
            var statsPos = new Vector2(resolvedRect.X + Padding, textY);
            renderer.DrawText(_statsText, statsPos, StatsColor);
        }

        // Draw hints text (right-aligned)
        if (!string.IsNullOrEmpty(_hintsText))
        {
            var hintsWidth = renderer.MeasureText(_hintsText).X;
            var hintsPos = new Vector2(
                resolvedRect.X + resolvedRect.Width - hintsWidth - Padding,
                textY);
            renderer.DrawText(_hintsText, hintsPos, HintsColor);
        }
    }

    protected override bool IsInteractive() => false;
}

