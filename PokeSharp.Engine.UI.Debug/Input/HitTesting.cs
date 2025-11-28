using Microsoft.Xna.Framework;
using PokeSharp.Engine.UI.Debug.Layout;

namespace PokeSharp.Engine.UI.Debug.Input;

/// <summary>
/// Provides hit testing functionality for UI components.
/// Uses resolved layout rectangles for accurate click detection.
/// </summary>
public static class HitTesting
{
    /// <summary>
    /// Tests if a point intersects a layout rectangle.
    /// </summary>
    public static bool HitTest(LayoutRect rect, Point point)
    {
        return rect.Contains(point);
    }

    /// <summary>
    /// Tests if a point intersects a layout rectangle.
    /// </summary>
    public static bool HitTest(LayoutRect rect, float x, float y)
    {
        return rect.Contains(x, y);
    }

    /// <summary>
    /// Tests if a point intersects a layout rectangle with padding.
    /// Shrinks the hit area by the specified padding.
    /// </summary>
    public static bool HitTestWithPadding(LayoutRect rect, Point point, float padding)
    {
        var shrunk = rect.Shrink(padding);
        return shrunk.Contains(point);
    }

    /// <summary>
    /// Finds the topmost (highest Z-order) component at the given point.
    /// </summary>
    public static string? FindTopmostComponent(
        IEnumerable<KeyValuePair<string, Core.ComponentFrameState>> components,
        Point point)
    {
        Core.ComponentFrameState? topmost = null;
        int highestZ = int.MinValue;

        foreach (var kvp in components)
        {
            var component = kvp.Value;
            if (component.IsInteractive &&
                component.IsVisible &&
                component.Rect.Contains(point) &&
                component.ZOrder > highestZ)
            {
                topmost = component;
                highestZ = component.ZOrder;
            }
        }

        return topmost?.Id;
    }

    /// <summary>
    /// Converts screen coordinates to local coordinates within a rectangle.
    /// </summary>
    public static (float x, float y) ScreenToLocal(LayoutRect rect, Point screenPoint)
    {
        return (screenPoint.X - rect.X, screenPoint.Y - rect.Y);
    }

    /// <summary>
    /// Converts local coordinates to screen coordinates.
    /// </summary>
    public static Point LocalToScreen(LayoutRect rect, float localX, float localY)
    {
        return new Point((int)(rect.X + localX), (int)(rect.Y + localY));
    }

    /// <summary>
    /// Tests if a rectangle intersects another rectangle.
    /// </summary>
    public static bool Intersects(LayoutRect a, LayoutRect b)
    {
        return a.ToRectangle().Intersects(b.ToRectangle());
    }
}




