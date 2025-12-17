using Arch.Core;
using Microsoft.Xna.Framework;
using MonoBallFramework.Game.Engine.Rendering.Components;
using MonoBallFramework.Game.Engine.Systems.Management;

namespace MonoBallFramework.Game.Engine.Rendering.Services;

/// <summary>
///     Service for centralized camera operations and queries.
///     Provides a clean API for interacting with cameras without direct ECS queries.
/// </summary>
/// <remarks>
///     <para>
///         <b>Benefits:</b>
///         - Single source of truth for camera operations
///         - Validation and error handling in one place
///         - Abstracts ECS queries from gameplay code
///         - Easy to test and mock
///     </para>
///     <para>
///         <b>Usage:</b>
///         Inject ICameraService into systems, scenes, or services that need camera access.
///         Use for high-level operations like zoom, screen/world conversions, camera shake, etc.
///     </para>
/// </remarks>
public interface ICameraService
{
    /// <summary>
    ///     Gets the main camera's transform matrix.
    /// </summary>
    Matrix GetViewMatrix();

    /// <summary>
    ///     Converts screen coordinates to world coordinates using the main camera.
    /// </summary>
    Vector2 ScreenToWorld(Vector2 screenPosition);

    /// <summary>
    ///     Converts world coordinates to screen coordinates using the main camera.
    /// </summary>
    Vector2 WorldToScreen(Vector2 worldPosition);

    /// <summary>
    ///     Sets the main camera's zoom level with smooth transition.
    /// </summary>
    void SetZoom(float targetZoom, bool smooth = true);

    /// <summary>
    ///     Gets the main camera's current position.
    /// </summary>
    Vector2? GetCameraPosition();

    /// <summary>
    ///     Gets the main camera's current zoom level.
    /// </summary>
    float? GetCameraZoom();

    /// <summary>
    ///     Sets the main camera's follow target.
    /// </summary>
    void SetFollowTarget(Vector2? target);

    /// <summary>
    ///     Gets the main camera's bounding rectangle in world space.
    /// </summary>
    RectangleF? GetCameraBounds();
}

/// <summary>
///     Implementation of ICameraService that queries the ECS world for camera data.
/// </summary>
public class CameraService : ICameraService
{
    private readonly QueryDescription _mainCameraQuery;
    private readonly World _world;

    /// <summary>
    ///     Initializes a new instance of the CameraService class.
    /// </summary>
    /// <param name="world">The ECS world containing camera entities.</param>
    public CameraService(World world)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));

        // Query for the main camera
        _mainCameraQuery = QueryCache.Get<Camera, MainCamera>();
    }

    /// <inheritdoc />
    public Matrix GetViewMatrix()
    {
        Matrix result = Matrix.Identity;

        _world.Query(
            in _mainCameraQuery,
            (ref Camera camera, ref MainCamera _) =>
            {
                result = camera.GetTransformMatrix();
            }
        );

        return result;
    }

    /// <inheritdoc />
    public Vector2 ScreenToWorld(Vector2 screenPosition)
    {
        Vector2 result = screenPosition;

        _world.Query(
            in _mainCameraQuery,
            (ref Camera camera, ref MainCamera _) =>
            {
                result = camera.ScreenToWorld(screenPosition);
            }
        );

        return result;
    }

    /// <inheritdoc />
    public Vector2 WorldToScreen(Vector2 worldPosition)
    {
        Vector2 result = worldPosition;

        _world.Query(
            in _mainCameraQuery,
            (ref Camera camera, ref MainCamera _) =>
            {
                result = camera.WorldToScreen(worldPosition);
            }
        );

        return result;
    }

    /// <inheritdoc />
    public void SetZoom(float targetZoom, bool smooth = true)
    {
        // Validate zoom range
        float clampedZoom = MathHelper.Clamp(targetZoom, Camera.MinZoom, Camera.MaxZoom);

        _world.Query(
            in _mainCameraQuery,
            (ref Camera camera, ref MainCamera _) =>
            {
                if (smooth)
                {
                    // Smooth transition
                    camera.TargetZoom = clampedZoom;
                }
                else
                {
                    // Instant zoom
                    camera.Zoom = clampedZoom;
                    camera.TargetZoom = clampedZoom;
                    camera.IsDirty = true;
                }
            }
        );
    }

    /// <inheritdoc />
    public Vector2? GetCameraPosition()
    {
        Vector2? result = null;

        _world.Query(
            in _mainCameraQuery,
            (ref Camera camera, ref MainCamera _) =>
            {
                result = camera.Position;
            }
        );

        return result;
    }

    /// <inheritdoc />
    public float? GetCameraZoom()
    {
        float? result = null;

        _world.Query(
            in _mainCameraQuery,
            (ref Camera camera, ref MainCamera _) =>
            {
                result = camera.Zoom;
            }
        );

        return result;
    }

    /// <inheritdoc />
    public void SetFollowTarget(Vector2? target)
    {
        _world.Query(
            in _mainCameraQuery,
            (ref Camera camera, ref MainCamera _) =>
            {
                camera.FollowTarget = target;
            }
        );
    }

    /// <inheritdoc />
    public RectangleF? GetCameraBounds()
    {
        RectangleF? result = null;

        _world.Query(
            in _mainCameraQuery,
            (ref Camera camera, ref MainCamera _) =>
            {
                result = camera.BoundingRectangle;
            }
        );

        return result;
    }
}
