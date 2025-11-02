using Microsoft.Xna.Framework;

namespace PokeSharp.Rendering.Components;

/// <summary>
/// Camera component for viewport and world-to-screen transformations.
/// Supports smooth following, directional prediction, rotation, and coordinate conversion.
/// </summary>
public struct Camera
{
    /// <summary>
    /// Minimum zoom level allowed (prevents excessive zoom out).
    /// </summary>
    public const float MinZoom = 0.1f;

    /// <summary>
    /// Maximum zoom level allowed (prevents excessive zoom in).
    /// </summary>
    public const float MaxZoom = 10.0f;

    /// <summary>
    /// Gets or sets the camera position in world coordinates (center point).
    /// </summary>
    public Vector2 Position { get; set; }

    /// <summary>
    /// Gets or sets the camera zoom level (1.0 = normal, 2.0 = 2x zoom).
    /// Automatically clamped between MinZoom and MaxZoom.
    /// </summary>
    public float Zoom { get; set; }

    /// <summary>
    /// Gets or sets the camera rotation in radians (clockwise).
    /// Use for camera shake effects, isometric views, or cinematic angles.
    /// </summary>
    public float Rotation { get; set; }

    /// <summary>
    /// Gets or sets the viewport rectangle for rendering bounds.
    /// </summary>
    public Rectangle Viewport { get; set; }

    /// <summary>
    /// Gets or sets the camera smoothing speed (0 = instant, 1 = very smooth).
    /// Lower values = faster response, higher values = smoother motion.
    /// Recommended: 0.1-0.3 for responsive feel, 0.5-0.8 for cinematic feel.
    /// </summary>
    public float SmoothingSpeed { get; set; }

    /// <summary>
    /// Gets or sets the lead distance in tiles for directional prediction.
    /// The camera will lead ahead of the player in the direction of movement.
    /// Recommended: 1-2 tiles for subtle effect, 3-4 tiles for pronounced effect.
    /// </summary>
    public float LeadDistance { get; set; }

    /// <summary>
    /// Gets or sets the map bounds for camera clamping (in pixels).
    /// Prevents the camera from showing areas outside the map.
    /// Set to Rectangle.Empty to disable bounds checking.
    /// </summary>
    public Rectangle MapBounds { get; set; }

    /// <summary>
    /// Initializes a new instance of the Camera struct with default values.
    /// </summary>
    public Camera()
    {
        Position = Vector2.Zero;
        Zoom = 1.0f;
        Rotation = 0f;
        Viewport = Rectangle.Empty;
        SmoothingSpeed = 0.2f;
        LeadDistance = 1.5f;
        MapBounds = Rectangle.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the Camera struct.
    /// </summary>
    /// <param name="viewport">The viewport rectangle for rendering bounds.</param>
    /// <param name="smoothingSpeed">Smoothing speed for lerp (default 0.2).</param>
    /// <param name="leadDistance">Lead distance in tiles for directional prediction (default 1.5).</param>
    public Camera(Rectangle viewport, float smoothingSpeed = 0.2f, float leadDistance = 1.5f)
    {
        Position = Vector2.Zero;
        Zoom = 1.0f;
        Rotation = 0f;
        Viewport = viewport;
        SmoothingSpeed = smoothingSpeed;
        LeadDistance = leadDistance;
        MapBounds = Rectangle.Empty;
    }

    /// <summary>
    /// Gets the camera's bounding rectangle in world space (float precision).
    /// Useful for culling and intersection tests.
    /// </summary>
    public RectangleF BoundingRectangle
    {
        get
        {
            var halfWidth = Viewport.Width / (2f * Zoom);
            var halfHeight = Viewport.Height / (2f * Zoom);

            return new RectangleF(
                Position.X - halfWidth,
                Position.Y - halfHeight,
                halfWidth * 2,
                halfHeight * 2
            );
        }
    }

    /// <summary>
    /// Gets the transformation matrix for this camera.
    /// Includes position, rotation, zoom, and viewport centering.
    /// </summary>
    public readonly Matrix GetTransformMatrix()
    {
        return Matrix.CreateTranslation(-Position.X, -Position.Y, 0) *
               Matrix.CreateRotationZ(Rotation) *
               Matrix.CreateScale(Zoom, Zoom, 1) *
               Matrix.CreateTranslation(Viewport.Width / 2f, Viewport.Height / 2f, 0);
    }

    /// <summary>
    /// Converts screen coordinates to world coordinates.
    /// Useful for mouse/touch input handling and click detection.
    /// </summary>
    /// <param name="screenPosition">Position in screen space (pixels).</param>
    /// <returns>Position in world space.</returns>
    public readonly Vector2 ScreenToWorld(Vector2 screenPosition)
    {
        var matrix = GetTransformMatrix();
        Matrix.Invert(ref matrix, out var invertedMatrix);
        return Vector2.Transform(screenPosition, invertedMatrix);
    }

    /// <summary>
    /// Converts world coordinates to screen coordinates.
    /// Useful for UI positioning and debug rendering.
    /// </summary>
    /// <param name="worldPosition">Position in world space.</param>
    /// <returns>Position in screen space (pixels).</returns>
    public Vector2 WorldToScreen(Vector2 worldPosition)
    {
        return Vector2.Transform(worldPosition, GetTransformMatrix());
    }

    /// <summary>
    /// Moves the camera by a relative offset in world space.
    /// Note: This will be overridden by CameraFollowSystem if following is active.
    /// </summary>
    /// <param name="offset">The offset to move by.</param>
    public void Move(Vector2 offset)
    {
        Position += offset;
    }

    /// <summary>
    /// Sets the camera to look at a specific world position.
    /// Note: This will be overridden by CameraFollowSystem if following is active.
    /// </summary>
    /// <param name="worldPosition">The position to center the camera on.</param>
    public void LookAt(Vector2 worldPosition)
    {
        Position = worldPosition;
    }

    /// <summary>
    /// Increases the zoom level by the specified amount.
    /// Automatically clamped between MinZoom and MaxZoom.
    /// </summary>
    /// <param name="amount">The amount to increase zoom by (default 0.1).</param>
    public void ZoomIn(float amount = 0.1f)
    {
        Zoom = MathHelper.Clamp(Zoom + amount, MinZoom, MaxZoom);
    }

    /// <summary>
    /// Decreases the zoom level by the specified amount.
    /// Automatically clamped between MinZoom and MaxZoom.
    /// </summary>
    /// <param name="amount">The amount to decrease zoom by (default 0.1).</param>
    public void ZoomOut(float amount = 0.1f)
    {
        Zoom = MathHelper.Clamp(Zoom - amount, MinZoom, MaxZoom);
    }

    /// <summary>
    /// Rotates the camera by the specified angle in radians.
    /// Positive values rotate clockwise.
    /// </summary>
    /// <param name="radians">The angle to rotate by in radians.</param>
    public void Rotate(float radians)
    {
        Rotation += radians;
    }

    /// <summary>
    /// Gets the camera's world view bounds as an integer Rectangle.
    /// Useful for tile-based culling and rendering optimization.
    /// </summary>
    /// <returns>A rectangle representing the visible world area.</returns>
    public Rectangle GetWorldViewBounds()
    {
        return BoundingRectangle.ToRectangle();
    }
}
