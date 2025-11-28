using Arch.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using PokeSharp.Engine.Core.Systems;
using PokeSharp.Engine.Rendering.Components;
using PokeSharp.Engine.Systems.Management;
using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Components.Player;

namespace PokeSharp.Engine.Rendering.Systems;

/// <summary>
///     System for camera following with smooth transitions.
///     Sets the camera's follow target and calls camera.Update() to handle all logic.
///     Camera moves freely without bounds restrictions (Pokemon Emerald style).
/// </summary>
public class CameraFollowSystem(ILogger<CameraFollowSystem>? logger = null)
    : SystemBase,
        IUpdateSystem
{
    private readonly ILogger<CameraFollowSystem>? _logger = logger;
    private QueryDescription _playerQuery;

    /// <summary>
    ///     Gets the update priority. Lower values execute first.
    ///     Camera follow executes at priority 825, after animation (800) and before tile animation (850).
    /// </summary>
    public int UpdatePriority => SystemPriority.CameraFollow;

    /// <inheritdoc />
    public override int Priority => SystemPriority.CameraFollow;

    /// <inheritdoc />
    public override void Initialize(World world)
    {
        base.Initialize(world);

        // Query for player with camera
        _playerQuery = QueryCache.Get<Player, Position, Camera>();
    }

    /// <inheritdoc />
    public override void Update(World world, float deltaTime)
    {
        if (!Enabled)
        {
            return;
        }

        EnsureInitialized();

        // Process each camera-equipped player
        world.Query(
            in _playerQuery,
            (ref Position position, ref Camera camera) =>
            {
                // Set follow target with offset to center on player sprite
                // Player position is tile top-left, so add half tile (8 pixels) for centering
                const float halfTile = 8f;
                camera.FollowTarget = new Vector2(
                    position.PixelX + halfTile,
                    position.PixelY + halfTile
                );
                camera.Update(deltaTime);
            }
        );
    }
}
