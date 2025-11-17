using Arch.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using PokeSharp.Engine.Common.Logging;
using PokeSharp.Engine.Rendering.Components;
using PokeSharp.Engine.Systems.Factories;
using PokeSharp.Engine.Systems.Management;
using PokeSharp.Game.Components.Maps;
using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Configuration;
using PokeSharp.Game.Systems;

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
        // Capture tile size from MapInfo (default from config if not found)
        var gameplayConfig = Configuration.GameplayConfig.CreateDefault();
        var tileSize = gameplayConfig.DefaultTileSize;
        var mapInfoQuery = QueryCache.Get<MapInfo>();
        world.Query(
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
            Zoom = gameplayConfig.DefaultZoom,
            TargetZoom = gameplayConfig.DefaultZoom,
            ZoomTransitionSpeed = gameplayConfig.ZoomTransitionSpeed,
            Position = new Vector2(x * tileSize, y * tileSize), // Start at player's position (grid to pixels)
        };

        // Set map bounds on camera from MapInfo
        world.Query(
            in mapInfoQuery,
            (ref MapInfo mapInfo) =>
            {
                camera.MapBounds = new Rectangle(0, 0, mapInfo.PixelWidth, mapInfo.PixelHeight);
            }
        );

        // Spawn player entity from template with position override
        var playerEntity = entityFactory.SpawnFromTemplate(
            "player",
            world,
            builder =>
            {
                builder.OverrideComponent(new Position(x, y, mapId: 0, tileSize));
            }
        );

        // Add Camera component (not in template as it's created per-instance)
        world.Add(playerEntity, camera);

        logger.LogEntityCreated(
            "Player",
            playerEntity.Id,
            ("Position", $"{x},{y}"),
            ("Sprite", "player"),
            ("GridMovement", "enabled"),
            ("Direction", "down"),
            ("Animation", "idle"),
            ("Camera", "attached")
        );
        logger.LogControlsHint("Use WASD or Arrow Keys to move!");
        logger.LogControlsHint("Zoom: +/- to zoom in/out, 1=GBA, 2=NDS, 3=Default");

        return playerEntity;
    }
}
