using Microsoft.Xna.Framework;
using PokeSharp.Engine.UI.Debug.Components.Base;
using PokeSharp.Engine.UI.Debug.Core;
using PokeSharp.Engine.UI.Debug.Input;

namespace PokeSharp.Engine.UI.Debug.Components.Controls;

/// <summary>
///     A clickable button component.
/// </summary>
public class Button : UIComponent
{
    /// <summary>Button text</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Callback when button is clicked</summary>
    public Action? OnClick { get; set; }

    protected override bool IsInteractive()
    {
        return Enabled;
    }

    protected override void OnRender(UIContext context)
    {
        // Determine button state color
        Color backgroundColor;
        if (!Enabled)
        {
            backgroundColor = Theme.ButtonNormal * 0.5f;
        }
        else if (IsPressed())
        {
            backgroundColor = Theme.ButtonPressed;
        }
        else if (IsHovered())
        {
            backgroundColor = Theme.ButtonHover;
        }
        else
        {
            backgroundColor = Theme.ButtonNormal;
        }

        // Draw background
        context.Renderer.DrawRectangle(Rect, backgroundColor);

        // Draw border
        context.Renderer.DrawRectangleOutline(Rect, Theme.BorderPrimary, Theme.BorderWidth);

        // Draw text (centered)
        if (!string.IsNullOrEmpty(Text))
        {
            Vector2 textSize = context.Renderer.MeasureText(Text);
            float textX = Rect.X + ((Rect.Width - textSize.X) / 2);
            float textY = Rect.Y + ((Rect.Height - textSize.Y) / 2);

            Color textColor = Enabled ? Theme.ButtonText : Theme.TextDim;
            context.Renderer.DrawText(Text, textX, textY, textColor);
        }

        // Handle click
        // Handle click on mouse RELEASE (standard click behavior)
        if (Enabled && IsHovered() && context.Input.IsMouseButtonReleased(MouseButton.Left))
        {
            OnClick?.Invoke();
        }
    }
}
