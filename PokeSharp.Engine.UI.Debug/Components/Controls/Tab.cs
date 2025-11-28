using Microsoft.Xna.Framework;
using PokeSharp.Engine.UI.Debug.Components.Base;
using PokeSharp.Engine.UI.Debug.Core;
using PokeSharp.Engine.UI.Debug.Input;
using PokeSharp.Engine.UI.Debug.Layout;

namespace PokeSharp.Engine.UI.Debug.Components.Controls;

/// <summary>
///     Individual tab button component.
/// </summary>
public class Tab : UIComponent
{
    /// <summary>Tab display text</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Whether this tab is currently active</summary>
    public bool IsActive { get; set; }

    /// <summary>Minimum width for the tab</summary>
    public float MinWidth { get; set; } = 80f;

    /// <summary>Padding within the tab</summary>
    public float Padding { get; set; } = 12f;

    /// <summary>Event triggered when tab is clicked</summary>
    public Action? OnClick { get; set; }

    protected override bool IsInteractive()
    {
        return true;
    }

    protected override void OnRender(UIContext context)
    {
        bool isHovered = IsHovered();
        bool isPressed = IsPressed();

        // Determine colors based on state
        Color backgroundColor;
        Color textColor;
        Color? borderColor = null;

        if (IsActive)
        {
            backgroundColor = Theme.TabActive;
            textColor = Theme.TextPrimary;
            borderColor = Theme.TabBorder;
        }
        else if (isPressed)
        {
            backgroundColor = Theme.TabPressed;
            textColor = Theme.ButtonText;
        }
        else if (isHovered)
        {
            backgroundColor = Theme.TabHover;
            textColor = Theme.ButtonText;
        }
        else
        {
            backgroundColor = Theme.TabInactive;
            textColor = Theme.TextSecondary;
        }

        // Draw background
        Renderer.DrawRectangle(Rect, backgroundColor);

        // Draw active indicator at bottom
        if (IsActive)
        {
            var indicatorRect = new LayoutRect(Rect.X, Rect.Y + Rect.Height - 2, Rect.Width, 2);
            Renderer.DrawRectangle(indicatorRect, Theme.TabActiveIndicator);
        }

        // Draw border for active tab
        if (borderColor.HasValue)
        {
            // Draw top, left, and right borders (not bottom for active tab)
            // Top border
            Renderer.DrawRectangle(
                new LayoutRect(Rect.X, Rect.Y, Rect.Width, 1),
                borderColor.Value
            );
            // Left border
            Renderer.DrawRectangle(
                new LayoutRect(Rect.X, Rect.Y, 1, Rect.Height),
                borderColor.Value
            );
            // Right border
            Renderer.DrawRectangle(
                new LayoutRect(Rect.X + Rect.Width - 1, Rect.Y, 1, Rect.Height),
                borderColor.Value
            );
        }

        // Draw text centered
        Vector2 textSize = Renderer.MeasureText(Text);
        float textX = Rect.X + ((Rect.Width - textSize.X) / 2);
        float textY = Rect.Y + ((Rect.Height - textSize.Y) / 2);
        Renderer.DrawText(Text, new Vector2(textX, textY), textColor);

        // Handle click on mouse RELEASE (standard click behavior)
        // This allows user to press, move away, and cancel by releasing outside
        // Note: No consumption - only one tab can be hovered at a time, so no conflict
        bool isReleased = context.Input.IsMouseButtonReleased(MouseButton.Left);
        if (isHovered && isReleased)
        {
            OnClick?.Invoke();
        }
    }

    protected override (float width, float height)? GetContentSize()
    {
        // Auto-size based on text
        Vector2 textSize = Context?.Renderer.MeasureText(Text) ?? Vector2.Zero;
        float width = Math.Max(MinWidth, textSize.X + (Padding * 2));
        int height = Theme.ButtonHeight;
        return (width, height);
    }
}
