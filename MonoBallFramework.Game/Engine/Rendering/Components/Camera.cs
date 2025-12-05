using Microsoft.Xna.Framework;
using MonoBallFramework.Game.Engine.Rendering.Constants;

namespace MonoBallFramework.Game.Engine.Rendering.Components;

/// <summary>
///     Camera component for viewport and world-to-screen transformations.
///     This is a pure data component following ECS best practices.
///     All camera logic (following, zooming, etc.) is handled by dedicated systems.
/// </summary>
/// <remarks>
///     <para>
///         <b>Architecture:</b>
///         - Camera: Pure data component (this struct)
///         - CameraUpdateSystem: Handles zoom transitions and follow target logic
///         - CameraFollowSystem: Sets the follow target based on player position
///         - CameraViewportSystem: Handles window resize events
///         - MainCamera: Tag component to mark the active camera
///     </para>
///     <para>
///         This component keeps readonly helper methods for transformations (GetTransformMatrix, ScreenToWorld, etc.)
///         as these are pure computations that don't mutate state.
///     </para>
/// </remarks>
public struct Camera
{
    /// <summary>
    ///     Minimum zoom level allowed (prevents excessive zoom out).
    /// </summary>
    public const float MinZoom = 0.1f;

    /// <summary>
    ///     Maximum zoom level allowed (prevents excessive zoom in).
    /// </summary>
    public const float MaxZoom = 10.0f;

    /// <summary>
    ///     GBA native resolution width (Game Boy Advance).
    /// </summary>
    public const int GbaNativeWidth = 240;

    /// <summary>
    ///     GBA native resolution height (Game Boy Advance).
    /// </summary>
    public const int GbaNativeHeight = 160;

    /// <summary>
    ///     Gets or sets the camera position in world coordinates (center point).
    /// </summary>
    public Vector2 Position { get; set; }

    /// <summary>
    ///     Gets or sets the camera zoom level (1.0 = normal, 2.0 = 2x zoom).
    ///     Automatically clamped between MinZoom and MaxZoom.
    /// </summary>
    public float Zoom { get; set; }

    /// <summary>
    ///     Gets or sets the target zoom level for smooth transitions.
    /// </summary>
    public float TargetZoom { get; set; }

    /// <summary>
    ///     Gets or sets the zoom transition speed (0-1, where 1 = instant).
    ///     Default: 0.1 for smooth transitions.
    /// </summary>
    public float ZoomTransitionSpeed { get; set; }

    /// <summary>
    ///     Gets or sets the camera rotation in radians (clockwise).
    ///     Use for camera shake effects, isometric views, or cinematic angles.
    /// </summary>
    public float Rotation { get; set; }

    /// <summary>
    ///     Gets or sets the viewport rectangle for rendering bounds.
    /// </summary>
    public Rectangle Viewport { get; set; }

    /// <summary>
    ///     Gets or sets the reference (target) width for aspect ratio calculation.
    ///     This is the initial/desired width that the camera should maintain when resizing.
    /// </summary>
    public int ReferenceWidth { get; set; }

    /// <summary>
    ///     Gets or sets the reference (target) height for aspect ratio calculation.
    ///     This is the initial/desired height that the camera should maintain when resizing.
    /// </summary>
    public int ReferenceHeight { get; set; }

    /// <summary>
    ///     Gets or sets the virtual viewport rectangle (viewport with borders applied).
    ///     This is the actual rendering area within the window, accounting for letterboxing/pillarboxing.
    /// </summary>
    public Rectangle VirtualViewport { get; set; }

    /// <summary>
    ///     Gets or sets the camera smoothing speed (0 = instant, 1 = very smooth).
    ///     Lower values = faster response, higher values = smoother motion.
    ///     Recommended: 0.1-0.3 for responsive feel, 0.5-0.8 for cinematic feel.
    /// </summary>
    public float SmoothingSpeed { get; set; }

    /// <summary>
    ///     Gets or sets the lead distance in tiles for directional prediction.
    ///     The camera will lead ahead of the player in the direction of movement.
    ///     Recommended: 1-2 tiles for subtle effect, 3-4 tiles for pronounced effect.
    /// </summary>
    public float LeadDistance { get; set; }

    /// <summary>
    ///     Gets or sets the target position for camera following.
    ///     When set, the camera will smoothly follow this position.
    ///     Set to null to disable following.
    /// </summary>
    public Vector2? FollowTarget { get; set; }

    /// <summary>
    ///     Gets or sets the map bounds for camera clamping (in pixels).
    ///     Prevents the camera from showing areas outside the map.
    ///     Set to Rectangle.Empty to disable bounds checking.
    /// </summary>
    public Rectangle MapBounds { get; set; }

    /// <summary>
    ///     Indicates whether the camera transform needs to be recalculated.
    ///     Set to true when Position, Zoom, or Rotation changes.
    ///     Reset to false after transform is calculated.
    /// </summary>
    /// <remarks>
    ///     Used by render systems to avoid expensive matrix calculations every frame.
    ///     Only recalculate when camera actually moves or changes.
    /// </remarks>
    public bool IsDirty { get; set; }

    /// <summary>
    ///     Initializes a new instance of the Camera struct with default values.
    /// </summary>
    public Camera()
    {
        Position = Vector2.Zero;
        Zoom = 1.0f;
        TargetZoom = 1.0f;
        ZoomTransitionSpeed = CameraConstants.DefaultZoomTransitionSpeed;
        Rotation = 0f;
        Viewport = Rectangle.Empty;
        ReferenceWidth = 0;
        ReferenceHeight = 0;
        VirtualViewport = Rectangle.Empty;
        SmoothingSpeed = CameraConstants.DefaultSmoothingSpeed;
        LeadDistance = CameraConstants.DefaultLeadDistance;
        FollowTarget = null;
        MapBounds = Rectangle.Empty;
        IsDirty = true; // Initial render requires transform calculation
    }

    /// <summary>
    ///     Initializes a new instance of the Camera struct.
    /// </summary>
    /// <param name="viewport">The viewport rectangle for rendering bounds.</param>
    /// <param name="smoothingSpeed">Smoothing speed for lerp.</param>
    /// <param name="leadDistance">Lead distance in tiles for directional prediction.</param>
    public Camera(
        Rectangle viewport,
        float smoothingSpeed = CameraConstants.DefaultSmoothingSpeed,
        float leadDistance = CameraConstants.DefaultLeadDistance
    )
    {
        Position = Vector2.Zero;
        Zoom = 1.0f;
        TargetZoom = 1.0f;
        ZoomTransitionSpeed = CameraConstants.DefaultZoomTransitionSpeed;
        Rotation = 0f;
        Viewport = viewport;
        // ReferenceWidth/Height left at 0 so UpdateViewportForResize can initialize zoom properly
        ReferenceWidth = 0;
        ReferenceHeight = 0;
        VirtualViewport = viewport; // Initially same as viewport
        SmoothingSpeed = smoothingSpeed;
        LeadDistance = leadDistance;
        FollowTarget = null;
        MapBounds = Rectangle.Empty;
        IsDirty = true; // Initial render requires transform calculation
    }

    /// <summary>
    ///     Gets the camera's bounding rectangle in world space (float precision).
    ///     Useful for culling and intersection tests.
    /// </summary>
    public RectangleF BoundingRectangle
    {
        get
        {
            float halfWidth = Viewport.Width / (2f * Zoom);
            float halfHeight = Viewport.Height / (2f * Zoom);

            return new RectangleF(
                Position.X - halfWidth,
                Position.Y - halfHeight,
                halfWidth * 2,
                halfHeight * 2
            );
        }
    }

    /// <summary>
    ///     Gets the camera's bounding rectangle using the ROUNDED position.
    ///     This matches the position used in GetTransformMatrix() and prevents sub-pixel jitter.
    ///     Use this for UI positioning (popups, HUD elements) that need to stay stable.
    /// </summary>
    /// <remarks>
    ///     The camera rounds its position in GetTransformMatrix() to prevent texture bleeding.
    ///     UI elements should use the same rounded position to avoid jitter during smooth camera movement.
    /// </remarks>
    public RectangleF RoundedBoundingRectangle
    {
        get
        {
            // Use the same rounding logic as GetTransformMatrix()
            float roundedX = MathF.Round(Position.X * Zoom) / Zoom;
            float roundedY = MathF.Round(Position.Y * Zoom) / Zoom;

            float halfWidth = Viewport.Width / (2f * Zoom);
            float halfHeight = Viewport.Height / (2f * Zoom);

            return new RectangleF(
                roundedX - halfWidth,
                roundedY - halfHeight,
                halfWidth * 2,
                halfHeight * 2
            );
        }
    }

    /// <summary>
    ///     Gets the transformation matrix for this camera.
    ///     Includes position, rotation, zoom, and viewport centering.
    ///     Rounds the camera position to prevent sub-pixel rendering artifacts (texture bleeding between tiles).
    ///     Since Viewport and VirtualViewport are now the same size (both integer GBA multiples),
    ///     no additional scaling is needed - viewportScale is always 1.0 for pixel-perfect rendering.
    /// </summary>
    public readonly Matrix GetTransformMatrix()
    {
        // Round camera position to nearest pixel after zoom to prevent texture bleeding/seams
        // This ensures tiles always render at integer screen coordinates
        float roundedX = MathF.Round(Position.X * Zoom) / Zoom;
        float roundedY = MathF.Round(Position.Y * Zoom) / Zoom;

        // Use Viewport width/height for centering (VirtualViewport is same size, just offset)
        float centerX = Viewport.Width / 2f;
        float centerY = Viewport.Height / 2f;

        return Matrix.CreateTranslation(-roundedX, -roundedY, 0)
            * Matrix.CreateRotationZ(Rotation)
            * Matrix.CreateScale(Zoom, Zoom, 1)
            * Matrix.CreateTranslation(centerX, centerY, 0);
    }

    /// <summary>
    ///     Converts screen coordinates to world coordinates.
    ///     Useful for mouse/touch input handling and click detection.
    /// </summary>
    /// <param name="screenPosition">Position in screen space (pixels).</param>
    /// <returns>Position in world space.</returns>
    public readonly Vector2 ScreenToWorld(Vector2 screenPosition)
    {
        Matrix matrix = GetTransformMatrix();
        Matrix.Invert(ref matrix, out Matrix invertedMatrix);
        return Vector2.Transform(screenPosition, invertedMatrix);
    }

    /// <summary>
    ///     Converts world coordinates to screen coordinates.
    ///     Useful for UI positioning and debug rendering.
    /// </summary>
    /// <param name="worldPosition">Position in world space.</param>
    /// <returns>Position in screen space (pixels).</returns>
    public Vector2 WorldToScreen(Vector2 worldPosition)
    {
        return Vector2.Transform(worldPosition, GetTransformMatrix());
    }


    /// <summary>
    ///     Gets the camera's world view bounds as an integer Rectangle.
    ///     Useful for tile-based culling and rendering optimization.
    /// </summary>
    /// <returns>A rectangle representing the visible world area.</returns>
    public Rectangle GetWorldViewBounds()
    {
        return BoundingRectangle.ToRectangle();
    }


    /// <summary>
    ///     Updates the viewport to maintain aspect ratio when the window is resized.
    ///     Uses integer scaling based on GBA native resolution (240x160) to maintain pixel-perfect rendering.
    ///     The Viewport is set to an integer GBA multiple, and zoom is set to match GBA's 240x160 world view.
    /// </summary>
    /// <param name="windowWidth">The new window width.</param>
    /// <param name="windowHeight">The new window height.</param>
    public void UpdateViewportForResize(int windowWidth, int windowHeight)
    {
        // Calculate the maximum integer scale from GBA native that fits in the window
        int scaleX = Math.Max(1, windowWidth / GbaNativeWidth);
        int scaleY = Math.Max(1, windowHeight / GbaNativeHeight);

        // Use the smaller scale to ensure the entire viewport fits
        int scale = Math.Min(scaleX, scaleY);

        // Calculate viewport and virtual viewport dimensions using integer scale of GBA native
        // Both are integer multiples of GBA native for pixel-perfect scaling
        int viewportWidth = GbaNativeWidth * scale;
        int viewportHeight = GbaNativeHeight * scale;

        // Set Viewport to the integer GBA multiple
        Viewport = new Rectangle(0, 0, viewportWidth, viewportHeight);

        // VirtualViewport is the same as Viewport (both are integer GBA multiples)
        // This ensures viewportScale = 1.0 always (no scaling artifacts!)
        VirtualViewport = new Rectangle(
            (windowWidth - viewportWidth) / 2,
            (windowHeight - viewportHeight) / 2,
            viewportWidth,
            viewportHeight
        );

        // On first resize (initialization), set zoom to match GBA native resolution
        // This ensures exactly 15x10 tiles (240/16 x 160/16) are visible
        // Zoom = scale means: Viewport pixels / Zoom = GBA native pixels (240x160)
        if (ReferenceWidth == 0 || ReferenceHeight == 0)
        {
            ReferenceWidth = windowWidth;
            ReferenceHeight = windowHeight;

            // Set zoom to the integer scale factor for pixel-perfect GBA viewport
            Zoom = scale;
            TargetZoom = scale;
        }
        else
        {
            // On subsequent resizes, maintain the same world-view ratio
            // Calculate what the previous scale was
            int previousScaleX = Math.Max(1, ReferenceWidth / GbaNativeWidth);
            int previousScaleY = Math.Max(1, ReferenceHeight / GbaNativeHeight);
            int previousScale = Math.Min(previousScaleX, previousScaleY);

            // Scale zoom proportionally to maintain same world view
            if (previousScale > 0)
            {
                float zoomRatio = (float)scale / previousScale;
                Zoom *= zoomRatio;
                TargetZoom *= zoomRatio;
            }

            // Update reference dimensions
            ReferenceWidth = windowWidth;
            ReferenceHeight = windowHeight;
        }

        IsDirty = true;
    }

    /// <summary>
    ///     Clamps the camera position to prevent showing areas outside the map.
    /// </summary>
    /// <param name="position">The desired camera position.</param>
    /// <returns>The clamped camera position.</returns>
    private readonly Vector2 ClampPositionToMapBounds(Vector2 position)
    {
        // Calculate half viewport dimensions in world coordinates (accounting for zoom)
        float halfViewportWidth = Viewport.Width / (2f * Zoom);
        float halfViewportHeight = Viewport.Height / (2f * Zoom);

        // Calculate the bounds where the camera should stop
        float minX = MapBounds.Left + halfViewportWidth;
        float maxX = MapBounds.Right - halfViewportWidth;
        float minY = MapBounds.Top + halfViewportHeight;
        float maxY = MapBounds.Bottom - halfViewportHeight;

        // Handle cases where viewport is larger than map (center the camera)
        if (maxX < minX)
        {
            minX = maxX = (MapBounds.Left + MapBounds.Right) / 2f;
        }

        if (maxY < minY)
        {
            minY = maxY = (MapBounds.Top + MapBounds.Bottom) / 2f;
        }

        // Clamp position
        return new Vector2(
            MathHelper.Clamp(position.X, minX, maxX),
            MathHelper.Clamp(position.Y, minY, maxY)
        );
    }
}
