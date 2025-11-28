namespace PokeSharp.Engine.UI.Debug.Layout;

/// <summary>
///     Defines anchor points for positioning UI elements relative to their parent container.
/// </summary>
public enum Anchor
{
    /// <summary>Top-left corner</summary>
    TopLeft,

    /// <summary>Top-center edge</summary>
    TopCenter,

    /// <summary>Top-right corner</summary>
    TopRight,

    /// <summary>Middle-left edge</summary>
    MiddleLeft,

    /// <summary>Center of the container</summary>
    Center,

    /// <summary>Middle-right edge</summary>
    MiddleRight,

    /// <summary>Bottom-left corner</summary>
    BottomLeft,

    /// <summary>Bottom-center edge</summary>
    BottomCenter,

    /// <summary>Bottom-right corner</summary>
    BottomRight,

    /// <summary>Stretch to fill entire width, anchored to top</summary>
    StretchTop,

    /// <summary>Stretch to fill entire width, anchored to bottom</summary>
    StretchBottom,

    /// <summary>Stretch to fill entire height, anchored to left</summary>
    StretchLeft,

    /// <summary>Stretch to fill entire height, anchored to right</summary>
    StretchRight,

    /// <summary>Stretch to fill entire parent</summary>
    Fill,
}
