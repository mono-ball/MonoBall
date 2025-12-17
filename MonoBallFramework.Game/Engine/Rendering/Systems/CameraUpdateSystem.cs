using Arch.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using MonoBallFramework.Game.Engine.Core.Systems;
using MonoBallFramework.Game.Engine.Core.Systems.Base;
using MonoBallFramework.Game.Engine.Rendering.Components;
using MonoBallFramework.Game.Engine.Rendering.Constants;
using MonoBallFramework.Game.Engine.Systems.Management;

namespace MonoBallFramework.Game.Engine.Rendering.Systems;

/// <summary>
///     System responsible for updating camera state each frame.
///     Handles zoom transitions and follow target interpolation.
///     This system contains the logic that was previously in Camera.Update().
/// </summary>
/// <remarks>
///     <para>
///         <b>Responsibilities:</b>
///         - Smooth zoom transitions (Zoom → TargetZoom)
///         - Camera position interpolation when following a target
///         - Setting the IsDirty flag when camera state changes
///     </para>
///     <para>
///         <b>System Priority:</b>
///         Runs at priority 826, after CameraFollowSystem (825) which sets the follow target,
///         and before TileAnimation (850) to ensure camera is updated before rendering calculations.
///     </para>
/// </remarks>
public class CameraUpdateSystem(ILogger<CameraUpdateSystem>? logger = null)
    : SystemBase,
        IUpdateSystem
{
    private readonly ILogger<CameraUpdateSystem>? _logger = logger;

    /// <summary>
    ///     Query for all cameras (not limited to player cameras).
    /// </summary>
    private QueryDescription _cameraQuery;

    /// <summary>
    ///     Gets the update priority. Runs after CameraFollowSystem (825).
    /// </summary>
    public int UpdatePriority => SystemPriority.CameraFollow + 1; // 826

    /// <inheritdoc />
    public override int Priority => SystemPriority.CameraFollow + 1;

    /// <inheritdoc />
    public override void Initialize(World world)
    {
        base.Initialize(world);

        // Query for all cameras (works with MainCamera tag or any camera)
        _cameraQuery = QueryCache.Get<Camera>();

        _logger?.LogDebug("CameraUpdateSystem initialized");
    }

    /// <inheritdoc />
    public override void Update(World world, float deltaTime)
    {
        if (!Enabled)
        {
            return;
        }

        EnsureInitialized();

        // Update all cameras in the world
        world.Query(
            in _cameraQuery,
            (ref Camera camera) =>
            {
                bool dirty = false;

                // 1. Smooth zoom transition
                if (Math.Abs(camera.Zoom - camera.TargetZoom) > CameraConstants.ZoomSnapThreshold)
                {
                    camera.Zoom = MathHelper.Lerp(
                        camera.Zoom,
                        camera.TargetZoom,
                        camera.ZoomTransitionSpeed
                    );
                    dirty = true;

                    // Snap to target when very close to prevent infinite lerping
                    if (Math.Abs(camera.Zoom - camera.TargetZoom) < CameraConstants.ZoomSnapThreshold)
                    {
                        camera.Zoom = camera.TargetZoom;
                    }
                }

                // 2. Follow target if set
                if (camera.FollowTarget.HasValue)
                {
                    Vector2 oldPosition = camera.Position;
                    Vector2 targetPosition = camera.FollowTarget.Value;

                    // Apply smoothing if enabled
                    if (camera.SmoothingSpeed > 0)
                    {
                        camera.Position = Vector2.Lerp(
                            camera.Position,
                            targetPosition,
                            camera.SmoothingSpeed
                        );
                    }
                    else
                    {
                        // Instant following (no smoothing)
                        camera.Position = targetPosition;
                    }

                    // Note: Camera clamping removed to replicate Pokemon Emerald behavior
                    // The camera can now move freely without map bounds restrictions

                    // Mark dirty if position changed
                    if (camera.Position != oldPosition)
                    {
                        dirty = true;
                    }
                }

                // Mark transform as dirty if camera changed
                // The IsDirty flag will be cleared by render systems after they cache the transform
                if (dirty)
                {
                    camera.IsDirty = true;
                }
            }
        );
    }
}
