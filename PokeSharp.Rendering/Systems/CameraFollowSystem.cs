using Arch.Core;
using Microsoft.Xna.Framework;
using PokeSharp.Core.Components;
using PokeSharp.Core.Systems;
using PokeSharp.Rendering.Components;

namespace PokeSharp.Rendering.Systems;

/// <summary>
/// System for instant camera following with map bounds clamping.
/// Camera follows player position directly with no smoothing or prediction.
/// </summary>
public class CameraFollowSystem : BaseSystem
{
    private QueryDescription _playerQuery;

    /// <inheritdoc/>
    public override int Priority => SystemPriority.Camera;

    /// <inheritdoc/>
    public override void Initialize(World world)
    {
        base.Initialize(world);

        // Query for player with camera (no GridMovement needed)
        _playerQuery = new QueryDescription()
            .WithAll<Player, Position, Camera>();
    }

    /// <inheritdoc/>
    public override void Update(World world, float deltaTime)
    {
        if (!Enabled)
            return;

        EnsureInitialized();

        // Process each camera-equipped player
        world.Query(in _playerQuery, (ref Position position, ref Camera camera) =>
        {
            // Calculate desired camera position (follows player)
            var desiredPosition = new Vector2(position.PixelX, position.PixelY);

            // Apply map bounds clamping if configured
            if (camera.MapBounds != Rectangle.Empty)
            {
                // Only move camera in directions where map edge is NOT visible
                camera.Position = ClampToMapBounds(desiredPosition, camera);
            }
            else
            {
                // No bounds, follow player directly
                camera.Position = desiredPosition;
            }
        });
    }

    /// <summary>
    /// Clamps the camera position to stop when map edges become visible.
    /// Camera stops moving in a direction when the edge is visible in that direction.
    /// </summary>
    /// <param name="position">The desired camera position.</param>
    /// <param name="camera">The camera component with bounds.</param>
    /// <returns>The clamped camera position.</returns>
    private static Vector2 ClampToMapBounds(Vector2 position, Camera camera)
    {
        // Calculate half viewport dimensions in world coordinates (accounting for zoom)
        var halfViewportWidth = camera.Viewport.Width / (2f * camera.Zoom);
        var halfViewportHeight = camera.Viewport.Height / (2f * camera.Zoom);

        // Calculate the bounds where the camera should stop
        // When camera.X = minX, the left edge of the map is at the left edge of the viewport
        // When camera.X = maxX, the right edge of the map is at the right edge of the viewport
        var minX = camera.MapBounds.Left + halfViewportWidth;
        var maxX = camera.MapBounds.Right - halfViewportWidth;
        var minY = camera.MapBounds.Top + halfViewportHeight;
        var maxY = camera.MapBounds.Bottom - halfViewportHeight;

        // Handle cases where viewport is larger than map (center the camera)
        if (maxX < minX)
        {
            minX = maxX = (camera.MapBounds.Left + camera.MapBounds.Right) / 2f;
        }
        if (maxY < minY)
        {
            minY = maxY = (camera.MapBounds.Top + camera.MapBounds.Bottom) / 2f;
        }

        // Clamp position - this stops the camera when edges become visible
        return new Vector2(
            MathHelper.Clamp(position.X, minX, maxX),
            MathHelper.Clamp(position.Y, minY, maxY)
        );
    }
}
