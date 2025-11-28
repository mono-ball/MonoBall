namespace PokeSharp.Engine.UI.Debug.Layout;

/// <summary>
///     Resolves layout constraints into absolute screen coordinates.
///     This is the core of the constraint-based layout system.
/// </summary>
public static class LayoutResolver
{
    /// <summary>
    ///     Resolves a layout constraint relative to a parent rectangle.
    /// </summary>
    /// <param name="constraint">The constraint to resolve</param>
    /// <param name="parent">The parent rectangle to resolve relative to</param>
    /// <param name="contentSize">Optional content size for auto-sizing (null for explicit sizes)</param>
    /// <returns>Resolved absolute rectangle</returns>
    public static LayoutRect Resolve(
        LayoutConstraint constraint,
        LayoutRect parent,
        (float width, float height)? contentSize = null
    )
    {
        // Calculate width
        float width = CalculateWidth(constraint, parent, contentSize);

        // Calculate height
        float height = CalculateHeight(constraint, parent, contentSize);

        // Get anchor position in parent space
        (float anchorX, float anchorY) = GetAnchorPosition(constraint.Anchor, parent);

        // Apply offset from anchor
        float x = anchorX + constraint.OffsetX + constraint.GetMarginLeft();
        float y = anchorY + constraint.OffsetY + constraint.GetMarginTop();

        // Adjust based on anchor type (anchors define which corner of the element aligns with the anchor point)
        (x, y) = AdjustPositionForAnchor(constraint.Anchor, x, y, width, height);

        return new LayoutRect(x, y, width, height);
    }

    private static float CalculateWidth(
        LayoutConstraint constraint,
        LayoutRect parent,
        (float width, float height)? contentSize
    )
    {
        float width;

        // Check for stretch anchors first
        if (IsHorizontalStretch(constraint.Anchor))
        {
            width = parent.Width - constraint.GetMarginLeft() - constraint.GetMarginRight();
        }
        // Percentage width
        else if (constraint.WidthPercent.HasValue)
        {
            width = parent.Width * constraint.WidthPercent.Value;
        }
        // Explicit width
        else if (constraint.Width.HasValue)
        {
            width = constraint.Width.Value;
        }
        // Auto-size from content
        else if (contentSize.HasValue)
        {
            width =
                contentSize.Value.width
                + constraint.GetPaddingLeft()
                + constraint.GetPaddingRight();
        }
        // Default fallback
        else
        {
            width = 100; // Sensible default
        }

        // Apply constraints
        if (constraint.MinWidth.HasValue)
        {
            width = Math.Max(width, constraint.MinWidth.Value);
        }

        if (constraint.MaxWidth.HasValue)
        {
            width = Math.Min(width, constraint.MaxWidth.Value);
        }

        return width;
    }

    private static float CalculateHeight(
        LayoutConstraint constraint,
        LayoutRect parent,
        (float width, float height)? contentSize
    )
    {
        float height;

        // Check for stretch anchors first
        if (IsVerticalStretch(constraint.Anchor))
        {
            height = parent.Height - constraint.GetMarginTop() - constraint.GetMarginBottom();
        }
        // Percentage height
        else if (constraint.HeightPercent.HasValue)
        {
            height = parent.Height * constraint.HeightPercent.Value;
        }
        // Explicit height
        else if (constraint.Height.HasValue)
        {
            height = constraint.Height.Value;
        }
        // Auto-size from content
        else if (contentSize.HasValue)
        {
            height =
                contentSize.Value.height
                + constraint.GetPaddingTop()
                + constraint.GetPaddingBottom();
        }
        // Default fallback
        else
        {
            height = 30; // Sensible default
        }

        // Apply constraints
        if (constraint.MinHeight.HasValue)
        {
            height = Math.Max(height, constraint.MinHeight.Value);
        }

        if (constraint.MaxHeight.HasValue)
        {
            height = Math.Min(height, constraint.MaxHeight.Value);
        }

        return height;
    }

    private static (float x, float y) GetAnchorPosition(Anchor anchor, LayoutRect parent)
    {
        return anchor switch
        {
            Anchor.TopLeft => (parent.X, parent.Y),
            Anchor.TopCenter => (parent.CenterX, parent.Y),
            Anchor.TopRight => (parent.Right, parent.Y),
            Anchor.MiddleLeft => (parent.X, parent.CenterY),
            Anchor.Center => (parent.CenterX, parent.CenterY),
            Anchor.MiddleRight => (parent.Right, parent.CenterY),
            Anchor.BottomLeft => (parent.X, parent.Bottom),
            Anchor.BottomCenter => (parent.CenterX, parent.Bottom),
            Anchor.BottomRight => (parent.Right, parent.Bottom),
            Anchor.StretchTop => (parent.X, parent.Y),
            Anchor.StretchBottom => (parent.X, parent.Bottom),
            Anchor.StretchLeft => (parent.X, parent.Y),
            Anchor.StretchRight => (parent.Right, parent.Y),
            Anchor.Fill => (parent.X, parent.Y),
            _ => (parent.X, parent.Y),
        };
    }

    private static (float x, float y) AdjustPositionForAnchor(
        Anchor anchor,
        float x,
        float y,
        float width,
        float height
    )
    {
        return anchor switch
        {
            // Top anchors - no Y adjustment needed, element grows downward
            Anchor.TopLeft => (x, y),
            Anchor.TopCenter => (x - (width / 2), y),
            Anchor.TopRight => (x - width, y),

            // Middle anchors - center vertically
            Anchor.MiddleLeft => (x, y - (height / 2)),
            Anchor.Center => (x - (width / 2), y - (height / 2)),
            Anchor.MiddleRight => (x - width, y - (height / 2)),

            // Bottom anchors - element grows upward from anchor
            Anchor.BottomLeft => (x, y - height),
            Anchor.BottomCenter => (x - (width / 2), y - height),
            Anchor.BottomRight => (x - width, y - height),

            // Stretch anchors
            Anchor.StretchTop => (x, y),
            Anchor.StretchBottom => (x, y - height),
            Anchor.StretchLeft => (x, y),
            Anchor.StretchRight => (x - width, y),
            Anchor.Fill => (x, y),

            _ => (x, y),
        };
    }

    private static bool IsHorizontalStretch(Anchor anchor)
    {
        return anchor is Anchor.StretchTop or Anchor.StretchBottom or Anchor.Fill;
    }

    private static bool IsVerticalStretch(Anchor anchor)
    {
        return anchor is Anchor.StretchLeft or Anchor.StretchRight or Anchor.Fill;
    }
}
