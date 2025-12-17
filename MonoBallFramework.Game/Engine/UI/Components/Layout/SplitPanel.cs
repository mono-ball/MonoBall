using Microsoft.Xna.Framework;
using MonoBallFramework.Game.Engine.UI.Components.Base;
using MonoBallFramework.Game.Engine.UI.Core;
using MonoBallFramework.Game.Engine.UI.Layout;

namespace MonoBallFramework.Game.Engine.UI.Components.Layout;

/// <summary>
///     Defines the orientation of a SplitPanel.
/// </summary>
public enum SplitOrientation
{
    /// <summary>Panes arranged side by side (left/right)</summary>
    Horizontal,

    /// <summary>Panes arranged top/bottom</summary>
    Vertical
}

/// <summary>
///     A reusable dual-pane container that splits available space between two child components.
///     Supports horizontal (left/right) and vertical (top/bottom) orientations with configurable
///     split ratios, minimum sizes, and optional separator/splitter visuals.
/// </summary>
public class SplitPanel : UIContainer
{
    /// <summary>Background color (null for transparent)</summary>
    public Color? BackgroundColor { get; set; }

    /// <summary>Border color (null for no border)</summary>
    public Color? BorderColor { get; set; }

    /// <summary>Border thickness</summary>
    public int BorderThickness { get; set; } = 1;

    /// <summary>Split orientation (Horizontal = left/right, Vertical = top/bottom)</summary>
    public SplitOrientation Orientation { get; set; } = SplitOrientation.Horizontal;

    /// <summary>
    ///     Split ratio for the first pane (0.0-1.0).
    ///     For Horizontal: ratio of width for left pane.
    ///     For Vertical: ratio of height for top pane.
    ///     Default is 0.5 (50/50 split).
    /// </summary>
    public float SplitRatio { get; set; } = 0.5f;

    /// <summary>Minimum size in pixels for the first pane</summary>
    public float MinFirstPaneSize { get; set; } = 100f;

    /// <summary>Minimum size in pixels for the second pane</summary>
    public float MinSecondPaneSize { get; set; } = 100f;

    /// <summary>Gap/spacing between panes in pixels (the splitter area)</summary>
    public float SplitterSize { get; set; } = 4f;

    /// <summary>Additional padding inside each pane (gap between content and splitter)</summary>
    public float PaneInnerPadding { get; set; } = 4f;

    /// <summary>Color of the splitter/gap (null uses theme border color)</summary>
    public Color? SplitterColor { get; set; }

    /// <summary>Whether to draw the splitter line</summary>
    public bool ShowSplitter { get; set; } = true;

    /// <summary>
    ///     Gets the first pane component (left for horizontal, top for vertical).
    /// </summary>
    public UIComponent? FirstPane { get; private set; }

    /// <summary>
    ///     Gets the second pane component (right for horizontal, bottom for vertical).
    /// </summary>
    public UIComponent? SecondPane { get; private set; }

    /// <summary>
    ///     Gets the calculated rectangle for the first pane (after layout).
    /// </summary>
    public LayoutRect FirstPaneRect { get; private set; }

    /// <summary>
    ///     Gets the calculated rectangle for the second pane (after layout).
    /// </summary>
    public LayoutRect SecondPaneRect { get; private set; }

    /// <summary>
    ///     Sets the first pane component (left for horizontal, top for vertical).
    /// </summary>
    public void SetFirstPane(UIComponent? component)
    {
        if (FirstPane != null)
        {
            RemoveChild(FirstPane);
        }

        FirstPane = component;

        if (FirstPane != null)
        {
            AddChild(FirstPane);
        }
    }

    /// <summary>
    ///     Sets the second pane component (right for horizontal, bottom for vertical).
    /// </summary>
    public void SetSecondPane(UIComponent? component)
    {
        if (SecondPane != null)
        {
            RemoveChild(SecondPane);
        }

        SecondPane = component;

        if (SecondPane != null)
        {
            AddChild(SecondPane);
        }
    }

    protected override void OnRenderContainer(UIContext context)
    {
        // Draw background
        if (BackgroundColor.HasValue)
        {
            context.Renderer.DrawRectangle(Rect, BackgroundColor.Value);
        }

        // Draw border
        if (BorderColor.HasValue && BorderThickness > 0)
        {
            context.Renderer.DrawRectangleOutline(Rect, BorderColor.Value, BorderThickness);
        }

        // Calculate pane layouts
        CalculatePaneLayouts();

        // Draw splitter
        if (ShowSplitter && SplitterSize > 0)
        {
            DrawSplitter(context);
        }
    }

    protected override void OnRenderChildren(UIContext context)
    {
        // Render first pane in its own container context
        if (FirstPane != null)
        {
            context.BeginContainer($"{Id}_first_pane",
                new LayoutConstraint
                {
                    Anchor = Anchor.TopLeft,
                    OffsetX = FirstPaneRect.X - Rect.X,
                    OffsetY = FirstPaneRect.Y - Rect.Y,
                    Width = FirstPaneRect.Width,
                    Height = FirstPaneRect.Height
                });

            // Preserve existing padding while setting fill anchor
            FirstPane.Constraint = new LayoutConstraint
            {
                Anchor = Anchor.Fill, Padding = FirstPane.Constraint.Padding
            };
            FirstPane.Render(context);

            context.EndContainer();
        }

        // Render second pane in its own container context
        if (SecondPane != null)
        {
            context.BeginContainer($"{Id}_second_pane",
                new LayoutConstraint
                {
                    Anchor = Anchor.TopLeft,
                    OffsetX = SecondPaneRect.X - Rect.X,
                    OffsetY = SecondPaneRect.Y - Rect.Y,
                    Width = SecondPaneRect.Width,
                    Height = SecondPaneRect.Height
                });

            // Preserve existing padding while setting fill anchor
            SecondPane.Constraint = new LayoutConstraint
            {
                Anchor = Anchor.Fill, Padding = SecondPane.Constraint.Padding
            };
            SecondPane.Render(context);

            context.EndContainer();
        }
    }

    private void CalculatePaneLayouts()
    {
        float paddingLeft = Constraint.GetPaddingLeft();
        float paddingTop = Constraint.GetPaddingTop();
        float paddingRight = Constraint.GetPaddingRight();
        float paddingBottom = Constraint.GetPaddingBottom();

        float contentX = Rect.X + paddingLeft;
        float contentY = Rect.Y + paddingTop;
        float contentWidth = Rect.Width - paddingLeft - paddingRight;
        float contentHeight = Rect.Height - paddingTop - paddingBottom;

        if (Orientation == SplitOrientation.Horizontal)
        {
            // Horizontal split: left/right panes
            // Account for inner padding on each side of splitter
            float totalPadding = PaneInnerPadding * 2; // padding on right of first pane + left of second pane
            float availableWidth = contentWidth - SplitterSize - totalPadding;
            float firstWidth = CalculateFirstSize(availableWidth, contentWidth);
            float secondWidth = availableWidth - firstWidth;

            // First pane gets inner padding on its right side (before splitter)
            FirstPaneRect = new LayoutRect(contentX, contentY, firstWidth, contentHeight);
            // Second pane starts after splitter + padding, gets inner padding on its left
            SecondPaneRect = new LayoutRect(
                contentX + firstWidth + PaneInnerPadding + SplitterSize + PaneInnerPadding,
                contentY,
                secondWidth,
                contentHeight
            );
        }
        else
        {
            // Vertical split: top/bottom panes
            float totalPadding = PaneInnerPadding * 2;
            float availableHeight = contentHeight - SplitterSize - totalPadding;
            float firstHeight = CalculateFirstSize(availableHeight, contentHeight);
            float secondHeight = availableHeight - firstHeight;

            FirstPaneRect = new LayoutRect(contentX, contentY, contentWidth, firstHeight);
            SecondPaneRect = new LayoutRect(
                contentX,
                contentY + firstHeight + PaneInnerPadding + SplitterSize + PaneInnerPadding,
                contentWidth,
                secondHeight
            );
        }
    }

    private float CalculateFirstSize(float availableSize, float totalSize)
    {
        // Calculate desired size based on ratio
        float desiredFirst = availableSize * Math.Clamp(SplitRatio, 0f, 1f);

        // Apply minimum constraints
        float maxFirst = availableSize - MinSecondPaneSize;
        float firstSize = Math.Clamp(desiredFirst, MinFirstPaneSize, maxFirst);

        // Handle edge case where minimums conflict
        if (MinFirstPaneSize + MinSecondPaneSize > availableSize)
        {
            // Split proportionally when space is too tight
            float ratio = availableSize / (MinFirstPaneSize + MinSecondPaneSize);
            firstSize = MinFirstPaneSize * ratio;
        }

        return Math.Max(0, firstSize);
    }

    private void DrawSplitter(UIContext context)
    {
        Color splitterColor = SplitterColor ?? ThemeManager.Current.BorderPrimary;

        LayoutRect splitterRect;
        if (Orientation == SplitOrientation.Horizontal)
        {
            // Vertical line between left and right panes (after inner padding)
            float splitterX = FirstPaneRect.Right + PaneInnerPadding;
            splitterRect = new LayoutRect(
                splitterX,
                FirstPaneRect.Y,
                SplitterSize,
                FirstPaneRect.Height
            );
        }
        else
        {
            // Horizontal line between top and bottom panes (after inner padding)
            float splitterY = FirstPaneRect.Bottom + PaneInnerPadding;
            splitterRect = new LayoutRect(
                FirstPaneRect.X,
                splitterY,
                FirstPaneRect.Width,
                SplitterSize
            );
        }

        context.Renderer.DrawRectangle(splitterRect, splitterColor);
    }
}
