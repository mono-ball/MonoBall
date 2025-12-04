using Microsoft.Xna.Framework;
using MonoBallFramework.Game.Engine.UI.Components.Base;
using MonoBallFramework.Game.Engine.UI.Core;

namespace MonoBallFramework.Game.Engine.UI.Components.Controls;

/// <summary>
///     A text label component.
/// </summary>
public class Label : UIComponent
{
    /// <summary>Text to display</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Text color</summary>
    public Color? Color { get; set; }

    /// <summary>Whether to auto-size to text</summary>
    public bool AutoSize { get; set; } = true;

    protected override (float width, float height)? GetContentSize()
    {
        if (!AutoSize || string.IsNullOrEmpty(Text))
        {
            return null;
        }

        try
        {
            Vector2 size = Renderer.MeasureText(Text);
            return (size.X, size.Y);
        }
        catch
        {
            return null;
        }
    }

    protected override void OnRender(UIContext context)
    {
        if (string.IsNullOrEmpty(Text))
        {
            return;
        }

        Color color = Color ?? Theme.TextPrimary;
        context.Renderer.DrawText(Text, Rect.X, Rect.Y, color);
    }
}
