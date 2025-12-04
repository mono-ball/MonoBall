using MonoBallFramework.Game.Engine.UI.Components.Base;
using MonoBallFramework.Game.Engine.UI.Components.Controls;
using MonoBallFramework.Game.Engine.UI.Core;
using MonoBallFramework.Game.Engine.UI.Layout;

namespace MonoBallFramework.Game.Engine.UI.Components.Layout;

/// <summary>
///     A scrollable container with scrollbar.
/// </summary>
public class ScrollView : UIContainer
{
    private readonly ScrollbarComponent _scrollbar = new();
    private float _viewportHeight;

    /// <summary>Current scroll offset (0 = top)</summary>
    public float ScrollOffset
    {
        get => _scrollbar.ScrollOffset;
        set => _scrollbar.ScrollOffset = value;
    }

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

        // Draw scrollbar if needed (now fully interactive!)
        if (ShowScrollbar && ScrollbarComponent.IsNeeded(ContentHeight, _viewportHeight))
        {
            float scrollbarX = Rect.Right - Theme.ScrollbarWidth - Theme.PaddingSmall;
            float scrollbarY = Rect.Y + Theme.PaddingSmall;
            float scrollbarHeight = _viewportHeight;

            var scrollbarRect = new LayoutRect(
                scrollbarX,
                scrollbarY,
                Theme.ScrollbarWidth,
                scrollbarHeight
            );

            // Handle scrollbar input (dragging, clicking)
            if (context.Input != null)
            {
                _scrollbar.HandleInput(
                    context,
                    context.Input,
                    scrollbarRect,
                    ContentHeight,
                    _viewportHeight,
                    Id
                );

                // Handle mouse wheel when hovering over the scroll view
                if (Rect.Contains(context.Input.MousePosition))
                {
                    _scrollbar.HandleMouseWheel(context.Input, ContentHeight, _viewportHeight);
                }
            }

            // Draw scrollbar
            _scrollbar.Draw(context.Renderer, Theme, scrollbarRect, ContentHeight, _viewportHeight);

            // Clamp after input
            _scrollbar.ClampOffset(ContentHeight, _viewportHeight);
        }
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
