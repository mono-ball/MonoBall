using Arch.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using PokeSharp.Core.Components.Maps;
using PokeSharp.Core.Components.Movement;
using PokeSharp.Core.Factories;
using PokeSharp.Core.Logging;
using PokeSharp.Rendering.Components;

namespace PokeSharp.Game.Initialization;

/// <summary>
///     Factory for creating player entities with all required components.
/// </summary>
public class PlayerFactory(
    ILogger<PlayerFactory> logger,
    World world,
    IEntityFactoryService entityFactory
)
{
    private readonly IEntityFactoryService _entityFactory = entityFactory;
    private readonly ILogger<PlayerFactory> _logger = logger;
    private readonly World _world = world;

    /// <summary>
    ///     Creates a player entity at the specified position with a camera.
    /// </summary>
    /// <param name="x">Starting X position in tiles.</param>
    /// <param name="y">Starting Y position in tiles.</param>
    /// <param name="viewportWidth">Viewport width in pixels.</param>
    /// <param name="viewportHeight">Viewport height in pixels.</param>
    /// <returns>The created player entity.</returns>
    public Entity CreatePlayer(int x, int y, int viewportWidth, int viewportHeight)
    {
        // Capture tile size from MapInfo (default to 16 if not found)
        var tileSize = 16;
        var mapInfoQuery = new QueryDescription().WithAll<MapInfo>();
        _world.Query(
            in mapInfoQuery,
            (ref MapInfo mapInfo) =>
            {
                tileSize = mapInfo.TileSize;
            }
        );

        // Create camera component with viewport and initial settings
        var viewport = new Rectangle(0, 0, viewportWidth, viewportHeight);
        var camera = new Camera(viewport)
        {
            Zoom = 3.0f,
            TargetZoom = 3.0f,
            ZoomTransitionSpeed = 0.1f,
            Position = new Vector2(x * tileSize, y * tileSize), // Start at player's position (grid to pixels)
        };

        // Set map bounds on camera from MapInfo
        _world.Query(
            in mapInfoQuery,
            (ref MapInfo mapInfo) =>
            {
                camera.MapBounds = new Rectangle(0, 0, mapInfo.PixelWidth, mapInfo.PixelHeight);
            }
        );

        // Spawn player entity from template with position override
        var playerEntity = _entityFactory.SpawnFromTemplate(
            "player",
            _world,
            builder =>
            {
                builder.OverrideComponent(new Position(x, y, mapId: 0, tileSize));
            }
        );

        // Add Camera component (not in template as it's created per-instance)
        _world.Add(playerEntity, camera);

        _logger.LogEntityCreated(
            "Player",
            playerEntity.Id,
            ("Position", $"{x},{y}"),
            ("Sprite", "player"),
            ("GridMovement", "enabled"),
            ("Direction", "down"),
            ("Animation", "idle"),
            ("Camera", "attached")
        );
        _logger.LogControlsHint("Use WASD or Arrow Keys to move!");
        _logger.LogControlsHint("Zoom: +/- to zoom in/out, 1=GBA, 2=NDS, 3=Default");

        return playerEntity;
    }
}
