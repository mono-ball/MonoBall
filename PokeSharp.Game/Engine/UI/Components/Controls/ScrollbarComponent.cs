using Microsoft.Xna.Framework;
using PokeSharp.Game.Engine.UI.Debug.Core;
using PokeSharp.Game.Engine.UI.Debug.Input;
using PokeSharp.Game.Engine.UI.Debug.Layout;

namespace PokeSharp.Game.Engine.UI.Debug.Components.Controls;

/// <summary>
///     Reusable scrollbar component for consistent scrolling behavior across all UI components.
///     Handles both rendering and input for vertical scrollbars.
/// </summary>
public class ScrollbarComponent
{
    private float _dragStartOffset;
    private int _dragStartY;

    /// <summary>
    ///     Current scroll offset (0 = top).
    /// </summary>
    public float ScrollOffset { get; set; }

    /// <summary>
    ///     Whether the scrollbar is currently being dragged.
    /// </summary>
    public bool IsDragging { get; private set; }

    /// <summary>
    ///     Handles scrollbar input interactions including dragging and clicking.
    /// </summary>
    /// <param name="context">UI context for input capture.</param>
    /// <param name="input">Current input state.</param>
    /// <param name="trackRect">Rectangle defining the scrollbar track area.</param>
    /// <param name="contentHeight">Total height of scrollable content.</param>
    /// <param name="visibleHeight">Height of visible viewport.</param>
    /// <param name="componentId">ID of the component for input capture.</param>
    /// <returns>True if the scroll offset was changed.</returns>
    public bool HandleInput(
        UIContext context,
        InputState input,
        LayoutRect trackRect,
        float contentHeight,
        float visibleHeight,
        string componentId
    )
    {
        if (input == null)
        {
            return false;
        }

        UITheme theme = ThemeManager.Current;
        float maxScroll = Math.Max(0, contentHeight - visibleHeight);
        float oldOffset = ScrollOffset;

        // Calculate thumb dimensions
        float thumbHeight = CalculateThumbHeight(
            contentHeight,
            visibleHeight,
            trackRect.Height,
            theme
        );
        float thumbY = CalculateThumbPosition(
            maxScroll,
            trackRect.Y,
            trackRect.Height,
            thumbHeight
        );

        var thumbRect = new LayoutRect(trackRect.X, thumbY, trackRect.Width, thumbHeight);
        bool isOverScrollbar = trackRect.Contains(input.MousePosition);

        // Handle dragging (continues even outside bounds due to input capture)
        if (IsDragging)
        {
            if (input.IsMouseButtonDown(MouseButton.Left))
            {
                int deltaY = input.MousePosition.Y - _dragStartY;
                float scrollRatio = deltaY / trackRect.Height;
                float scrollDelta = scrollRatio * contentHeight;

                ScrollOffset = Math.Clamp(_dragStartOffset + scrollDelta, 0, maxScroll);
            }

            // Handle mouse release (end drag)
            if (input.IsMouseButtonReleased(MouseButton.Left))
            {
                IsDragging = false;
                context.ReleaseCapture();
            }
        }
        // Handle new click on scrollbar
        else if (isOverScrollbar && input.IsMouseButtonPressed(MouseButton.Left))
        {
            // Capture input so drag continues even if mouse leaves scrollbar
            context.CaptureInput(componentId);

            // Check if clicking on thumb (drag) or track (jump)
            if (thumbRect.Contains(input.MousePosition))
            {
                // Start dragging the thumb
                IsDragging = true;
                _dragStartY = input.MousePosition.Y;
                _dragStartOffset = ScrollOffset;
            }
            else
            {
                // Click on track - jump to that position immediately
                float clickRatio = (input.MousePosition.Y - trackRect.Y) / trackRect.Height;
                float targetScroll = (clickRatio * contentHeight) - (visibleHeight / 2);
                ScrollOffset = Math.Clamp(targetScroll, 0, maxScroll);

                // Also start dragging from this new position
                IsDragging = true;
                _dragStartY = input.MousePosition.Y;
                _dragStartOffset = ScrollOffset;
            }

            // Consume the mouse button to prevent other components from processing
            input.ConsumeMouseButton(MouseButton.Left);
        }

        return ScrollOffset != oldOffset;
    }

    /// <summary>
    ///     Draws the scrollbar track and thumb.
    /// </summary>
    /// <param name="renderer">UI renderer.</param>
    /// <param name="theme">Current UI theme.</param>
    /// <param name="trackRect">Rectangle defining the scrollbar track area.</param>
    /// <param name="contentHeight">Total height of scrollable content.</param>
    /// <param name="visibleHeight">Height of visible viewport.</param>
    public void Draw(
        UIRenderer renderer,
        UITheme theme,
        LayoutRect trackRect,
        float contentHeight,
        float visibleHeight
    )
    {
        // Draw track
        renderer.DrawRectangle(trackRect, theme.ScrollbarTrack);

        // Calculate thumb dimensions
        float thumbHeight = CalculateThumbHeight(
            contentHeight,
            visibleHeight,
            trackRect.Height,
            theme
        );
        float maxScroll = Math.Max(0, contentHeight - visibleHeight);
        float thumbY = CalculateThumbPosition(
            maxScroll,
            trackRect.Y,
            trackRect.Height,
            thumbHeight
        );

        // Draw thumb
        var thumbRect = new LayoutRect(trackRect.X, thumbY, trackRect.Width, thumbHeight);
        Color thumbColor = IsDragging ? theme.ScrollbarThumbHover : theme.ScrollbarThumb;
        renderer.DrawRectangle(thumbRect, thumbColor);
    }

    /// <summary>
    ///     Handles mouse wheel scrolling.
    /// </summary>
    /// <param name="input">Current input state.</param>
    /// <param name="contentHeight">Total height of scrollable content.</param>
    /// <param name="visibleHeight">Height of visible viewport.</param>
    /// <param name="pixelsPerTick">Pixels to scroll per wheel tick (defaults to theme.ScrollSpeed).</param>
    /// <returns>True if the scroll offset was changed.</returns>
    public bool HandleMouseWheel(
        InputState input,
        float contentHeight,
        float visibleHeight,
        int? pixelsPerTick = null
    )
    {
        if (input == null || input.ScrollWheelDelta == 0)
        {
            return false;
        }

        int scrollAmount = pixelsPerTick ?? ThemeManager.Current.ScrollSpeed;
        float maxScroll = Math.Max(0, contentHeight - visibleHeight);
        float oldOffset = ScrollOffset;

        if (input.ScrollWheelDelta > 0)
        {
            ScrollOffset = Math.Max(0, ScrollOffset - scrollAmount);
        }
        else
        {
            ScrollOffset = Math.Min(maxScroll, ScrollOffset + scrollAmount);
        }

        return ScrollOffset != oldOffset;
    }

    /// <summary>
    ///     Handles line-based scrolling (for text or list views).
    /// </summary>
    /// <param name="input">Current input state.</param>
    /// <param name="totalLines">Total number of lines/items.</param>
    /// <param name="visibleLines">Number of visible lines/items.</param>
    /// <param name="linesPerTick">Lines to scroll per wheel tick (defaults to theme.ScrollWheelSensitivity).</param>
    /// <returns>True if the scroll offset was changed (in lines).</returns>
    public bool HandleMouseWheelLines(
        InputState input,
        int totalLines,
        int visibleLines,
        int? linesPerTick = null
    )
    {
        if (input == null || input.ScrollWheelDelta == 0)
        {
            return false;
        }

        int scrollLines = linesPerTick ?? ThemeManager.Current.ScrollWheelSensitivity;
        int maxScroll = Math.Max(0, totalLines - visibleLines);
        float oldOffset = ScrollOffset;

        if (input.ScrollWheelDelta > 0)
        {
            ScrollOffset = Math.Max(0, ScrollOffset - scrollLines);
        }
        else
        {
            ScrollOffset = Math.Min(maxScroll, ScrollOffset + scrollLines);
        }

        return ScrollOffset != oldOffset;
    }

    /// <summary>
    ///     Clamps the scroll offset to valid range.
    /// </summary>
    /// <param name="contentHeight">Total height of scrollable content.</param>
    /// <param name="visibleHeight">Height of visible viewport.</param>
    public void ClampOffset(float contentHeight, float visibleHeight)
    {
        float maxScroll = Math.Max(0, contentHeight - visibleHeight);
        ScrollOffset = Math.Clamp(ScrollOffset, 0, maxScroll);
    }

    /// <summary>
    ///     Resets the scrollbar state (useful when content changes).
    /// </summary>
    public void Reset()
    {
        ScrollOffset = 0;
        IsDragging = false;
    }

    /// <summary>
    ///     Scrolls to the top.
    /// </summary>
    public void ScrollToTop()
    {
        ScrollOffset = 0;
    }

    /// <summary>
    ///     Scrolls to the bottom.
    /// </summary>
    /// <param name="contentHeight">Total height of scrollable content.</param>
    /// <param name="visibleHeight">Height of visible viewport.</param>
    public void ScrollToBottom(float contentHeight, float visibleHeight)
    {
        ScrollOffset = Math.Max(0, contentHeight - visibleHeight);
    }

    /// <summary>
    ///     Checks if scrollbar is needed for the given content and viewport.
    /// </summary>
    /// <param name="contentHeight">Total height of scrollable content.</param>
    /// <param name="visibleHeight">Height of visible viewport.</param>
    /// <returns>True if content exceeds viewport height.</returns>
    public static bool IsNeeded(float contentHeight, float visibleHeight)
    {
        return contentHeight > visibleHeight;
    }

    private float CalculateThumbHeight(
        float contentHeight,
        float visibleHeight,
        float trackHeight,
        UITheme theme
    )
    {
        if (contentHeight <= 0)
        {
            return theme.ScrollbarMinThumbHeight;
        }

        float ratio = visibleHeight / contentHeight;
        float thumbHeight = ratio * trackHeight;
        return Math.Max(theme.ScrollbarMinThumbHeight, thumbHeight);
    }

    private float CalculateThumbPosition(
        float maxScroll,
        float trackY,
        float trackHeight,
        float thumbHeight
    )
    {
        if (maxScroll <= 0)
        {
            return trackY;
        }

        float scrollRatio = ScrollOffset / maxScroll;
        return trackY + (scrollRatio * (trackHeight - thumbHeight));
    }
}
