namespace MonoBallFramework.Game.Engine.Rendering.Constants;

/// <summary>
///     Constants related to camera behavior, rendering, and tile calculations.
///     Centralizes magic numbers for better maintainability.
/// </summary>
public static class CameraConstants
{
    /// <summary>
    ///     Half tile size in pixels (8 pixels for 16x16 tiles).
    ///     Used for centering calculations (tile position is top-left, add halfTile for center).
    /// </summary>
    public const float HalfTilePixels = 8f;

    /// <summary>
    ///     Margin in tiles around the camera viewport for culling calculations.
    ///     Extra tiles are rendered beyond the visible area to prevent pop-in during scrolling.
    /// </summary>
    public const int ViewportMarginTiles = 2;

    /// <summary>
    ///     Margin in tiles around the camera bounds for border rendering.
    ///     Extends border rendering area for smooth scrolling near map edges.
    /// </summary>
    public const int BorderRenderMarginTiles = 2;

    /// <summary>
    ///     Threshold for snapping zoom to target (prevents infinite lerping).
    /// </summary>
    public const float ZoomSnapThreshold = 0.001f;

    /// <summary>
    ///     Default smoothing speed for camera following (0-1 range).
    ///     Lower values = faster response, higher values = smoother motion.
    /// </summary>
    public const float DefaultSmoothingSpeed = 0.2f;

    /// <summary>
    ///     Default lead distance in tiles for directional prediction.
    ///     The camera leads ahead of the player in the direction of movement.
    /// </summary>
    public const float DefaultLeadDistance = 1.5f;

    /// <summary>
    ///     Default zoom transition speed (0-1 range).
    ///     Controls how fast the camera zooms to target zoom level.
    /// </summary>
    public const float DefaultZoomTransitionSpeed = 0.1f;
}
