using Microsoft.Xna.Framework;

namespace PokeSharp.Rendering.Services;

/// <summary>
/// Manages viewport scaling and zoom calculations for different screen resolutions.
/// Provides presets for GBA (240x160) and NDS (256x192) native resolutions.
/// </summary>
public class ViewportScaleManager
{
    /// <summary>
    /// GBA native resolution width in pixels.
    /// </summary>
    public const int GbaWidth = 240;

    /// <summary>
    /// GBA native resolution height in pixels.
    /// </summary>
    public const int GbaHeight = 160;

    /// <summary>
    /// NDS native resolution width in pixels.
    /// </summary>
    public const int NdsWidth = 256;

    /// <summary>
    /// NDS native resolution height in pixels.
    /// </summary>
    public const int NdsHeight = 192;

    /// <summary>
    /// Standard tile size in pixels (Pokemon uses 16x16 tiles).
    /// </summary>
    public const int TileSize = 16;

    /// <summary>
    /// Gets the current viewport size.
    /// </summary>
    public Point ViewportSize { get; }

    /// <summary>
    /// Gets the current zoom level.
    /// </summary>
    public float CurrentZoom { get; private set; }

    /// <summary>
    /// Gets the target zoom level for smooth transitions.
    /// </summary>
    public float TargetZoom { get; private set; }

    /// <summary>
    /// Gets or sets the zoom transition speed (0-1, where 1 = instant).
    /// </summary>
    public float ZoomTransitionSpeed { get; set; } = 0.1f;

    /// <summary>
    /// Initializes a new instance of the ViewportScaleManager class.
    /// </summary>
    /// <param name="viewportWidth">The viewport width in pixels.</param>
    /// <param name="viewportHeight">The viewport height in pixels.</param>
    /// <param name="initialZoom">The initial zoom level (default 1.0).</param>
    public ViewportScaleManager(int viewportWidth, int viewportHeight, float initialZoom = 1.0f)
    {
        ViewportSize = new Point(viewportWidth, viewportHeight);
        CurrentZoom = initialZoom;
        TargetZoom = initialZoom;
    }

    /// <summary>
    /// Calculates the zoom level to match GBA native resolution (240x160).
    /// </summary>
    /// <returns>The calculated zoom factor.</returns>
    public float CalculateGbaZoom()
    {
        var zoomX = (float)ViewportSize.X / GbaWidth;
        var zoomY = (float)ViewportSize.Y / GbaHeight;
        return Math.Min(zoomX, zoomY); // Use smaller zoom to fit entirely
    }

    /// <summary>
    /// Calculates the zoom level to match NDS native resolution (256x192).
    /// </summary>
    /// <returns>The calculated zoom factor.</returns>
    public float CalculateNdsZoom()
    {
        var zoomX = (float)ViewportSize.X / NdsWidth;
        var zoomY = (float)ViewportSize.Y / NdsHeight;
        return Math.Min(zoomX, zoomY); // Use smaller zoom to fit entirely
    }

    /// <summary>
    /// Calculates the zoom level to show a specific number of tiles on screen.
    /// Useful for dynamic zoom levels based on map size or gameplay requirements.
    /// </summary>
    /// <param name="tilesWide">The desired number of tiles visible horizontally.</param>
    /// <param name="tilesHigh">The desired number of tiles visible vertically.</param>
    /// <returns>The calculated zoom factor.</returns>
    public float GetZoomForTileCount(int tilesWide, int tilesHigh)
    {
        if (tilesWide <= 0 || tilesHigh <= 0)
        {
            throw new ArgumentException("Tile counts must be positive values.");
        }

        var targetWidthPixels = tilesWide * TileSize;
        var targetHeightPixels = tilesHigh * TileSize;

        var zoomX = (float)ViewportSize.X / targetWidthPixels;
        var zoomY = (float)ViewportSize.Y / targetHeightPixels;

        return Math.Min(zoomX, zoomY);
    }

    /// <summary>
    /// Sets the target zoom level for smooth transition.
    /// Call Update() each frame to smoothly interpolate to this zoom.
    /// </summary>
    /// <param name="zoom">The target zoom level.</param>
    public void SetTargetZoom(float zoom)
    {
        if (zoom <= 0)
        {
            throw new ArgumentException("Zoom must be a positive value.", nameof(zoom));
        }

        TargetZoom = zoom;
    }

    /// <summary>
    /// Sets the zoom level instantly without transition.
    /// </summary>
    /// <param name="zoom">The zoom level to set.</param>
    public void SetZoomInstant(float zoom)
    {
        if (zoom <= 0)
        {
            throw new ArgumentException("Zoom must be a positive value.", nameof(zoom));
        }

        CurrentZoom = zoom;
        TargetZoom = zoom;
    }

    /// <summary>
    /// Updates the current zoom with smooth transition towards target zoom.
    /// Call this each frame to enable smooth zoom transitions.
    /// </summary>
    /// <param name="deltaTime">The time elapsed since last update (unused for frame-independent lerp).</param>
    public void Update(float deltaTime)
    {
        if (Math.Abs(CurrentZoom - TargetZoom) > 0.001f)
        {
            CurrentZoom = MathHelper.Lerp(CurrentZoom, TargetZoom, ZoomTransitionSpeed);

            // Snap to target when very close
            if (Math.Abs(CurrentZoom - TargetZoom) < 0.001f)
            {
                CurrentZoom = TargetZoom;
            }
        }
    }

    /// <summary>
    /// Gets the number of tiles visible horizontally at the current zoom level.
    /// </summary>
    /// <returns>The number of visible horizontal tiles.</returns>
    public float GetVisibleTilesWide()
    {
        return ViewportSize.X / (TileSize * CurrentZoom);
    }

    /// <summary>
    /// Gets the number of tiles visible vertically at the current zoom level.
    /// </summary>
    /// <returns>The number of visible vertical tiles.</returns>
    public float GetVisibleTilesHigh()
    {
        return ViewportSize.Y / (TileSize * CurrentZoom);
    }

    /// <summary>
    /// Calculates the viewport bounds in world space (pixels).
    /// Useful for culling off-screen entities.
    /// </summary>
    /// <param name="cameraPosition">The camera's world position.</param>
    /// <returns>A rectangle representing the visible world area.</returns>
    public Rectangle GetWorldViewBounds(Vector2 cameraPosition)
    {
        var halfWidth = ViewportSize.X / (2f * CurrentZoom);
        var halfHeight = ViewportSize.Y / (2f * CurrentZoom);

        return new Rectangle(
            (int)(cameraPosition.X - halfWidth),
            (int)(cameraPosition.Y - halfHeight),
            (int)(halfWidth * 2),
            (int)(halfHeight * 2)
        );
    }
}
