using Microsoft.Xna.Framework;
using PokeSharp.Engine.UI.Debug.Components.Base;
using PokeSharp.Engine.UI.Debug.Core;
using PokeSharp.Engine.UI.Debug.Input;
using PokeSharp.Engine.UI.Debug.Layout;

namespace PokeSharp.Engine.UI.Debug.Components.Controls;

/// <summary>
///     A dropdown/combobox component with auto-complete style suggestions.
/// </summary>
public class Dropdown : UIComponent
{
    private int _hoveredItemIndex = -1;

    /// <summary>Available options</summary>
    public List<string> Options { get; set; } = new();

    /// <summary>Currently selected index (-1 for none)</summary>
    public int SelectedIndex { get; set; } = -1;

    /// <summary>Whether the dropdown is currently open</summary>
    public bool IsOpen { get; set; }

    /// <summary>Maximum visible items</summary>
    public int MaxVisibleItems { get; set; } = 10;

    /// <summary>Callback when selection changes</summary>
    public Action<int>? OnSelectionChanged { get; set; }

    protected override bool IsInteractive()
    {
        return Enabled;
    }

    protected override void OnRender(UIContext context)
    {
        // Draw main button
        Color backgroundColor = IsOpen || IsHovered() ? Theme.ButtonHover : Theme.ButtonNormal;
        context.Renderer.DrawRectangle(Rect, backgroundColor);
        context.Renderer.DrawRectangleOutline(Rect, Theme.BorderPrimary, Theme.BorderWidth);

        // Draw selected text
        string displayText =
            SelectedIndex >= 0 && SelectedIndex < Options.Count
                ? Options[SelectedIndex]
                : "Select...";

        float textX = Rect.X + Theme.PaddingMedium;
        float textY = Rect.Y + ((Rect.Height - context.Renderer.GetLineHeight()) / 2);
        context.Renderer.DrawText(displayText, textX, textY, Theme.TextPrimary);

        // Draw dropdown arrow
        DrawDropdownArrow(context);

        // Handle click to open/close on mouse RELEASE
        if (IsHovered() && context.Input.IsMouseButtonReleased(MouseButton.Left))
        {
            IsOpen = !IsOpen;
        }

        // Draw dropdown list if open
        if (IsOpen && Options.Count > 0)
        {
            DrawDropdownList(context);
        }
    }

    private void DrawDropdownArrow(UIContext context)
    {
        // Dropdown arrow indicator using Nerd Font icon
        float arrowX = Rect.Right - 20;
        float arrowY = Rect.CenterY;

        // Draw dropdown arrow icon
        context.Renderer.DrawText(
            NerdFontIcons.DropdownArrow,
            arrowX,
            arrowY - 8,
            Theme.TextSecondary
        );
    }

    private void DrawDropdownList(UIContext context)
    {
        int visibleCount = Math.Min(MaxVisibleItems, Options.Count);
        float itemHeight = Theme.DropdownItemHeight;
        float listHeight = visibleCount * itemHeight;

        // Position below the button
        var listRect = new LayoutRect(Rect.X, Rect.Bottom, Rect.Width, listHeight);

        // Background
        context.Renderer.DrawRectangle(listRect, Theme.BackgroundElevated);
        context.Renderer.DrawRectangleOutline(listRect, Theme.BorderPrimary, Theme.BorderWidth);

        // Items
        float itemY = listRect.Y;
        _hoveredItemIndex = -1;

        for (int i = 0; i < visibleCount; i++)
        {
            var itemRect = new LayoutRect(listRect.X, itemY, listRect.Width, itemHeight);

            // Check if hovered
            bool isItemHovered = itemRect.Contains(context.Input.MousePosition);
            if (isItemHovered)
            {
                _hoveredItemIndex = i;
                context.Renderer.DrawRectangle(itemRect, Theme.ButtonHover);
            }

            // Highlight selected
            if (i == SelectedIndex)
            {
                context.Renderer.DrawRectangle(itemRect, Theme.ButtonPressed);
            }

            // Draw text
            float textX = itemRect.X + Theme.PaddingMedium;
            float textY = itemRect.Y + ((itemRect.Height - context.Renderer.GetLineHeight()) / 2);
            context.Renderer.DrawText(Options[i], textX, textY, Theme.TextPrimary);

            // Handle selection on mouse RELEASE
            if (isItemHovered && context.Input.IsMouseButtonReleased(MouseButton.Left))
            {
                SelectedIndex = i;
                IsOpen = false;
                OnSelectionChanged?.Invoke(i);
            }

            itemY += itemHeight;
        }

        // Close on click outside (use release for consistency)
        if (
            !Rect.Contains(context.Input.MousePosition)
            && !listRect.Contains(context.Input.MousePosition)
            && context.Input.IsMouseButtonReleased(MouseButton.Left)
        )
        {
            IsOpen = false;
        }
    }
}
