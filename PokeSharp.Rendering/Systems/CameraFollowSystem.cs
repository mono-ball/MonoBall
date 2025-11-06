using Arch.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using PokeSharp.Core.Components;
using PokeSharp.Core.Systems;
using PokeSharp.Rendering.Components;

namespace PokeSharp.Rendering.Systems;

/// <summary>
///     System for camera following with smooth transitions and map bounds clamping.
///     Sets the camera's follow target and calls camera.Update() to handle all logic.
/// </summary>
public class CameraFollowSystem : BaseSystem
{
    private readonly ILogger<CameraFollowSystem>? _logger;
    private QueryDescription _playerQuery;

    /// <summary>
    ///     Initializes a new instance of the CameraFollowSystem class.
    /// </summary>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    public CameraFollowSystem(ILogger<CameraFollowSystem>? logger = null)
    {
        _logger = logger;
        _logger?.LogDebug("CameraFollowSystem initialized");
    }

    /// <inheritdoc />
    public override int Priority => SystemPriority.CameraFollow;

    /// <inheritdoc />
    public override void Initialize(World world)
    {
        base.Initialize(world);

        // Query for player with camera
        _playerQuery = new QueryDescription().WithAll<Player, Position, Camera>();
    }

    /// <inheritdoc />
    public override void Update(World world, float deltaTime)
    {
        if (!Enabled)
            return;

        EnsureInitialized();

        // Process each camera-equipped player
        world.Query(
            in _playerQuery,
            (ref Position position, ref Camera camera) =>
            {
                // Set follow target and let Camera.Update() handle the rest
                camera.FollowTarget = new Vector2(position.PixelX, position.PixelY);
                camera.Update(deltaTime);
            }
        );
    }
}
