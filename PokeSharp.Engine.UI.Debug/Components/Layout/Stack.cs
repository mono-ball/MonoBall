using PokeSharp.Engine.UI.Debug.Components.Base;
using PokeSharp.Engine.UI.Debug.Core;
using PokeSharp.Engine.UI.Debug.Layout;

namespace PokeSharp.Engine.UI.Debug.Components.Layout;

/// <summary>
/// Layout direction for stack containers.
/// </summary>
public enum StackDirection
{
    Vertical,
    Horizontal
}

/// <summary>
/// A container that lays out children in a vertical or horizontal stack.
/// </summary>
public class Stack : UIContainer
{
    /// <summary>Direction to stack children</summary>
    public StackDirection Direction { get; set; } = StackDirection.Vertical;

    /// <summary>Spacing between children</summary>
    public float Spacing { get; set; } = 0;

    protected override void OnRenderChildren(UIContext context)
    {
        float currentOffset = 0;

        foreach (var child in Children)
        {
            // Modify child constraint based on stack direction
            var originalConstraint = child.Constraint;
            child.Constraint = new LayoutConstraint
            {
                Anchor = Direction == StackDirection.Vertical ? Anchor.TopLeft : Anchor.TopLeft,
                OffsetX = Direction == StackDirection.Horizontal ? currentOffset : originalConstraint.OffsetX,
                OffsetY = Direction == StackDirection.Vertical ? currentOffset : originalConstraint.OffsetY,
                Width = originalConstraint.Width,
                Height = originalConstraint.Height,
                WidthPercent = originalConstraint.WidthPercent,
                HeightPercent = originalConstraint.HeightPercent,
                Margin = originalConstraint.Margin,
                Padding = originalConstraint.Padding
            };

            child.Render(context);

            // Calculate size for offset
            // Note: In a real implementation, we'd need the resolved rect
            // For now, use explicit sizes or defaults
            if (Direction == StackDirection.Vertical)
            {
                float height = originalConstraint.Height ?? originalConstraint.HeightPercent * context.CurrentContainer.ContentRect.Height ?? 30;
                currentOffset += height + Spacing;
            }
            else
            {
                float width = originalConstraint.Width ?? originalConstraint.WidthPercent * context.CurrentContainer.ContentRect.Width ?? 100;
                currentOffset += width + Spacing;
            }

            // Restore original constraint
            child.Constraint = originalConstraint;
        }
    }
}




