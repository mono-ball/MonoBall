using Microsoft.Xna.Framework;
using PokeSharp.Engine.UI.Debug.Components.Base;
using PokeSharp.Engine.UI.Debug.Core;
using PokeSharp.Engine.UI.Debug.Layout;

namespace PokeSharp.Engine.UI.Debug.Components.Layout;

/// <summary>
///     A scrollable container with scrollbar.
/// </summary>
public class ScrollView : UIContainer
{
    private float _viewportHeight;

    /// <summary>Current scroll offset (0 = top)</summary>
    public float ScrollOffset { get; set; }

    /// <summary>Total content height</summary>
    public float ContentHeight { get; private set; }

    /// <summary>Whether to show the scrollbar</summary>
    public bool ShowScrollbar { get; set; } = true;

    protected override bool IsInteractive()
    {
        return true;
    }

    protected override void OnRenderContainer(UIContext context)
    {
        // Draw background
        context.Renderer.DrawRectangle(Rect, Theme.BackgroundSecondary);

        // Store viewport height
        _viewportHeight = Rect.Height - (Theme.PaddingMedium * 2);

        // Calculate content height (sum of children)
        ContentHeight = CalculateContentHeight();

        // Clamp scroll offset
        float maxScroll = Math.Max(0, ContentHeight - _viewportHeight);
        ScrollOffset = Math.Clamp(ScrollOffset, 0, maxScroll);
    }

    protected override void OnRenderChildren(UIContext context)
    {
        // Apply scroll offset to children
        float yOffset = -ScrollOffset;

        foreach (UIComponent child in Children)
        {
            LayoutConstraint originalConstraint = child.Constraint;
            child.Constraint = new LayoutConstraint
            {
                Anchor = originalConstraint.Anchor,
                OffsetX = originalConstraint.OffsetX,
                OffsetY = originalConstraint.OffsetY + yOffset,
                Width = originalConstraint.Width,
                Height = originalConstraint.Height,
                WidthPercent = originalConstraint.WidthPercent,
                HeightPercent = originalConstraint.HeightPercent,
                Margin = originalConstraint.Margin,
                Padding = originalConstraint.Padding,
            };

            child.Render(context);

            // Update offset for next child (vertical stacking)
            float childHeight = originalConstraint.Height ?? 30;
            yOffset += childHeight + Theme.MarginSmall;

            child.Constraint = originalConstraint;
        }

        // Draw scrollbar if needed
        if (ShowScrollbar && ContentHeight > _viewportHeight)
        {
            DrawScrollbar(context);
        }
    }

    private void DrawScrollbar(UIContext context)
    {
        float scrollbarX = Rect.Right - Theme.ScrollbarWidth - Theme.PaddingSmall;
        float scrollbarY = Rect.Y + Theme.PaddingSmall;
        float scrollbarHeight = _viewportHeight;

        // Track
        var trackRect = new LayoutRect(
            scrollbarX,
            scrollbarY,
            Theme.ScrollbarWidth,
            scrollbarHeight
        );
        context.Renderer.DrawRectangle(trackRect, Theme.ScrollbarTrack);

        // Thumb
        float thumbHeight = Math.Max(20, _viewportHeight / ContentHeight * scrollbarHeight);
        float thumbY = scrollbarY + (ScrollOffset / ContentHeight * scrollbarHeight);

        var thumbRect = new LayoutRect(scrollbarX, thumbY, Theme.ScrollbarWidth, thumbHeight);
        Color thumbColor = IsHovered() ? Theme.ScrollbarThumbHover : Theme.ScrollbarThumb;
        context.Renderer.DrawRectangle(thumbRect, thumbColor);
    }

    private float CalculateContentHeight()
    {
        float total = 0;
        foreach (UIComponent child in Children)
        {
            float height = child.Constraint.Height ?? 30;
            total += height + Theme.MarginSmall;
        }

        return total;
    }
}
