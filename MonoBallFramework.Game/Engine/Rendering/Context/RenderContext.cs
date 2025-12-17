using Microsoft.Xna.Framework;
using MonoBallFramework.Game.Engine.Rendering.Components;

namespace MonoBallFramework.Game.Engine.Rendering.Context;

/// <summary>
///     Context object that provides rendering parameters to render systems.
///     Scenes create and configure this context before calling render systems.
///     This ensures scenes control what camera is used for rendering.
/// </summary>
/// <remarks>
///     <para>
///         <b>Industry Standard Pattern:</b>
///         - Scenes OWN cameras (not render systems)
///         - Scenes PROVIDE render context to systems
///         - Render systems are STATELESS (receive all data needed)
///     </para>
///     <para>
///         <b>Benefits:</b>
///         - Scene isolation: Each scene can have its own camera
///         - Testability: Can pass mock cameras to render systems
///         - Flexibility: Easy to swap cameras, add post-processing
///         - Proper ownership: Scene controls rendering, not systems
///     </para>
/// </remarks>
public class RenderContext
{
    /// <summary>
    ///     Initializes a new instance of the RenderContext class.
    /// </summary>
    /// <param name="camera">The camera to use for rendering.</param>
    public RenderContext(Camera camera)
    {
        Camera = camera;
    }

    /// <summary>
    ///     Gets the camera for this render context.
    ///     Render systems use this camera for transformations and culling.
    /// </summary>
    public Camera Camera { get; }

    /// <summary>
    ///     Gets the cached camera transform matrix.
    ///     Computed once per frame to avoid redundant calculations.
    /// </summary>
    public Matrix CameraTransform => Camera.GetTransformMatrix();

    /// <summary>
    ///     Gets the camera's bounding rectangle for culling.
    ///     Entities outside this area can be skipped.
    /// </summary>
    public RectangleF CameraBounds => Camera.BoundingRectangle;

    /// <summary>
    ///     Gets the viewport for rendering.
    /// </summary>
    public Rectangle Viewport => Camera.Viewport;

    /// <summary>
    ///     Gets the virtual viewport (with letterboxing/pillarboxing).
    /// </summary>
    public Rectangle VirtualViewport => Camera.VirtualViewport;
}
