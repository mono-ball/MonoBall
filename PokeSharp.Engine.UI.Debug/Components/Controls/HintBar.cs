using Microsoft.Xna.Framework;
using PokeSharp.Engine.UI.Debug.Components.Base;
using PokeSharp.Engine.UI.Debug.Core;
using PokeSharp.Engine.UI.Debug.Layout;

namespace PokeSharp.Engine.UI.Debug.Components.Controls;

/// <summary>
///     A reusable component for displaying helpful hints and keyboard shortcuts.
///     Typically rendered at the bottom of input areas or panels.
/// </summary>
public class HintBar : UIComponent
{
    private Color? _backgroundColor;

    // Visual properties - nullable for theme fallback
    private Color? _textColor;

    public HintBar(string id)
    {
        Id = id;
    }

    public Color TextColor
    {
        get => _textColor ?? ThemeManager.Current.TextDim;
        set => _textColor = value;
    }
    public Color BackgroundColor
    {
        get => _backgroundColor ?? Color.Transparent;
        set => _backgroundColor = value;
    }
    public float FontSize { get; set; } = 1.0f; // Scale factor
    public float Padding { get; set; } = 4f;

    /// <summary>
    ///     Gets the current hint text.
    /// </summary>
    public string Text { get; private set; } = string.Empty;

    /// <summary>
    ///     Sets the hint text to display.
    /// </summary>
    public void SetText(string text)
    {
        Text = text ?? string.Empty;
    }

    /// <summary>
    ///     Calculates the desired height for this hint bar.
    /// </summary>
    public float GetDesiredHeight(UIRenderer? renderer = null)
    {
        if (string.IsNullOrEmpty(Text))
        {
            return 0;
        }

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
                {
                    lineHeight = Renderer.GetLineHeight();
                }
            }
            catch
            {
                // No context available, use default
            }
        }

        return lineHeight + (Padding * 2);
    }

    protected override void OnRender(UIContext context)
    {
        if (string.IsNullOrEmpty(Text))
        {
            return;
        }

        UIRenderer renderer = Renderer;
        LayoutRect resolvedRect = Rect;

        // Draw background if specified
        if (BackgroundColor.A > 0)
        {
            renderer.DrawRectangle(resolvedRect, BackgroundColor);
        }

        // Draw hint text
        var textPos = new Vector2(resolvedRect.X + Padding, resolvedRect.Y + Padding);

        renderer.DrawText(Text, textPos, TextColor);
    }

    protected override bool IsInteractive()
    {
        return false;
    }
}
