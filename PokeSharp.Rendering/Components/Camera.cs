using Microsoft.Xna.Framework;

namespace PokeSharp.Rendering.Components;

/// <summary>
/// Camera component for viewport and world-to-screen transformations.
/// </summary>
public struct Camera
{
    /// <summary>
    /// Gets or sets the camera position in world coordinates.
    /// </summary>
    public Vector2 Position { get; set; }

    /// <summary>
    /// Gets or sets the camera zoom level (1.0 = normal, 2.0 = 2x zoom).
    /// </summary>
    public float Zoom { get; set; }

    /// <summary>
    /// Gets or sets the viewport rectangle for rendering bounds.
    /// </summary>
    public Rectangle Viewport { get; set; }

    /// <summary>
    /// Initializes a new instance of the Camera struct.
    /// </summary>
    public Camera(Rectangle viewport)
    {
        Position = Vector2.Zero;
        Zoom = 1.0f;
        Viewport = viewport;
    }

    /// <summary>
    /// Gets the transformation matrix for this camera.
    /// </summary>
    public Matrix GetTransformMatrix()
    {
        return Matrix.CreateTranslation(-Position.X, -Position.Y, 0) *
               Matrix.CreateScale(Zoom, Zoom, 1) *
               Matrix.CreateTranslation(Viewport.Width / 2f, Viewport.Height / 2f, 0);
    }
}
