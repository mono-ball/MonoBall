using Microsoft.Xna.Framework;

namespace PokeSharp.Engine.Rendering.Components;

/// <summary>
///     Camera component for viewport and world-to-screen transformations.
///     Supports smooth following, directional prediction, rotation, and coordinate conversion.
/// </summary>
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
        ZoomTransitionSpeed = 0.1f;
        Rotation = 0f;
        Viewport = Rectangle.Empty;
        SmoothingSpeed = 0.2f;
        LeadDistance = 1.5f;
        FollowTarget = null;
        MapBounds = Rectangle.Empty;
        IsDirty = true; // Initial render requires transform calculation
    }

    /// <summary>
    ///     Initializes a new instance of the Camera struct.
    /// </summary>
    /// <param name="viewport">The viewport rectangle for rendering bounds.</param>
    /// <param name="smoothingSpeed">Smoothing speed for lerp (default 0.2).</param>
    /// <param name="leadDistance">Lead distance in tiles for directional prediction (default 1.5).</param>
    public Camera(Rectangle viewport, float smoothingSpeed = 0.2f, float leadDistance = 1.5f)
    {
        Position = Vector2.Zero;
        Zoom = 1.0f;
        TargetZoom = 1.0f;
        ZoomTransitionSpeed = 0.1f;
        Rotation = 0f;
        Viewport = viewport;
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
    ///     Gets the transformation matrix for this camera.
    ///     Includes position, rotation, zoom, and viewport centering.
    ///     Rounds the camera position to prevent sub-pixel rendering artifacts (texture bleeding between tiles).
    /// </summary>
    public readonly Matrix GetTransformMatrix()
    {
        // Round camera position to nearest pixel after zoom to prevent texture bleeding/seams
        // This ensures tiles always render at integer screen coordinates
        float roundedX = MathF.Round(Position.X * Zoom) / Zoom;
        float roundedY = MathF.Round(Position.Y * Zoom) / Zoom;

        return Matrix.CreateTranslation(-roundedX, -roundedY, 0)
            * Matrix.CreateRotationZ(Rotation)
            * Matrix.CreateScale(Zoom, Zoom, 1)
            * Matrix.CreateTranslation(Viewport.Width / 2f, Viewport.Height / 2f, 0);
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
    ///     Moves the camera by a relative offset in world space.
    ///     Note: This will be overridden by CameraFollowSystem if following is active.
    /// </summary>
    /// <param name="offset">The offset to move by.</param>
    public void Move(Vector2 offset)
    {
        Position += offset;
    }

    /// <summary>
    ///     Sets the camera to look at a specific world position.
    ///     Note: This will be overridden by CameraFollowSystem if following is active.
    /// </summary>
    /// <param name="worldPosition">The position to center the camera on.</param>
    public void LookAt(Vector2 worldPosition)
    {
        Position = worldPosition;
    }

    /// <summary>
    ///     Increases the zoom level by the specified amount.
    ///     Automatically clamped between MinZoom and MaxZoom.
    /// </summary>
    /// <param name="amount">The amount to increase zoom by (default 0.1).</param>
    public void ZoomIn(float amount = 0.1f)
    {
        Zoom = MathHelper.Clamp(Zoom + amount, MinZoom, MaxZoom);
    }

    /// <summary>
    ///     Decreases the zoom level by the specified amount.
    ///     Automatically clamped between MinZoom and MaxZoom.
    /// </summary>
    /// <param name="amount">The amount to decrease zoom by (default 0.1).</param>
    public void ZoomOut(float amount = 0.1f)
    {
        Zoom = MathHelper.Clamp(Zoom - amount, MinZoom, MaxZoom);
    }

    /// <summary>
    ///     Rotates the camera by the specified angle in radians.
    ///     Positive values rotate clockwise.
    /// </summary>
    /// <param name="radians">The angle to rotate by in radians.</param>
    public void Rotate(float radians)
    {
        Rotation += radians;
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
    ///     Updates the camera's zoom and position based on FollowTarget and TargetZoom.
    ///     Call this each frame to enable smooth zoom transitions and camera following.
    /// </summary>
    /// <param name="deltaTime">Time elapsed since last frame (for frame-independent smoothing).</param>
    public void Update(float deltaTime)
    {
        bool dirty = false;

        // 1. Smooth zoom transition
        if (Math.Abs(Zoom - TargetZoom) > 0.001f)
        {
            Zoom = MathHelper.Lerp(Zoom, TargetZoom, ZoomTransitionSpeed);
            dirty = true;

            // Snap to target when very close
            if (Math.Abs(Zoom - TargetZoom) < 0.001f)
            {
                Zoom = TargetZoom;
            }
        }

        // 2. Follow target if set
        if (FollowTarget.HasValue)
        {
            Vector2 oldPosition = Position;
            Vector2 targetPosition = FollowTarget.Value;

            // Apply smoothing if enabled
            if (SmoothingSpeed > 0)
            {
                Position = Vector2.Lerp(Position, targetPosition, SmoothingSpeed);
            }
            else
            {
                Position = targetPosition;
            }

            // Note: Camera clamping removed to replicate Pokemon Emerald behavior
            // The camera can now move freely without map bounds restrictions

            // Mark dirty if position changed
            if (Position != oldPosition)
            {
                dirty = true;
            }
        }

        // Mark transform as dirty if camera changed
        if (dirty)
        {
            IsDirty = true;
        }
    }

    /// <summary>
    ///     Calculates the zoom level to match GBA native resolution (240x160).
    /// </summary>
    /// <returns>The calculated zoom factor.</returns>
    public readonly float CalculateGbaZoom()
    {
        const int gbaWidth = 240;
        const int gbaHeight = 160;

        float zoomX = (float)Viewport.Width / gbaWidth;
        float zoomY = (float)Viewport.Height / gbaHeight;
        return Math.Min(zoomX, zoomY); // Use smaller zoom to fit entirely
    }

    /// <summary>
    ///     Calculates the zoom level to match NDS native resolution (256x192).
    /// </summary>
    /// <returns>The calculated zoom factor.</returns>
    public readonly float CalculateNdsZoom()
    {
        const int ndsWidth = 256;
        const int ndsHeight = 192;

        float zoomX = (float)Viewport.Width / ndsWidth;
        float zoomY = (float)Viewport.Height / ndsHeight;
        return Math.Min(zoomX, zoomY); // Use smaller zoom to fit entirely
    }

    /// <summary>
    ///     Sets the target zoom level for smooth transition.
    ///     Automatically clamped between MinZoom and MaxZoom.
    /// </summary>
    /// <param name="targetZoom">The desired zoom level.</param>
    public void SetZoomSmooth(float targetZoom)
    {
        TargetZoom = MathHelper.Clamp(targetZoom, MinZoom, MaxZoom);
    }

    /// <summary>
    ///     Sets the zoom level instantly without transition.
    ///     Automatically clamped between MinZoom and MaxZoom.
    /// </summary>
    /// <param name="zoom">The zoom level to set.</param>
    public void SetZoomInstant(float zoom)
    {
        Zoom = MathHelper.Clamp(zoom, MinZoom, MaxZoom);
        TargetZoom = Zoom;
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
