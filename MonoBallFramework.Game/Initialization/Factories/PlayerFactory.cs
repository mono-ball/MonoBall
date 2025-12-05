using Arch.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using MonoBallFramework.Game.Engine.Common.Logging;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.Engine.Rendering.Components;
using MonoBallFramework.Game.Engine.Systems.Factories;
using MonoBallFramework.Game.Engine.Systems.Management;
using MonoBallFramework.Game.Components;
using MonoBallFramework.Game.Ecs.Components;
using MonoBallFramework.Game.Ecs.Components.Maps;
using MonoBallFramework.Game.Ecs.Components.Movement;
using MonoBallFramework.Game.Ecs.Components.Warps;
using MonoBallFramework.Game.Infrastructure.Configuration;

namespace MonoBallFramework.Game.Initialization.Factories;

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
        // Capture tile size and current map info from MapInfo
        var gameplayConfig = GameplayConfig.CreateDefault();
        int tileSize = gameplayConfig.DefaultTileSize;
        string? currentMapName = null;
        QueryDescription mapInfoQuery = QueryCache.Get<MapInfo>();
        world.Query(
            in mapInfoQuery,
            (ref MapInfo mapInfo) =>
            {
                tileSize = mapInfo.TileSize;
                currentMapName = mapInfo.MapName;
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

        // Map bounds removed - camera moves freely without restrictions (Pokemon Emerald style)
        // camera.MapBounds remains Rectangle.Empty (default) to allow free camera movement

        // Spawn player entity from template with position override
        Entity playerEntity = entityFactory.SpawnFromTemplate(
            "player",
            world,
            builder =>
            {
                builder.OverrideComponent(new Position(x, y, 0, tileSize));
            }
        );

        // Add Camera component (not in template as it's created per-instance)
        world.Add(playerEntity, camera);

        // Add MainCamera tag to mark this as the primary camera
        world.Add<MainCamera>(playerEntity);

        // Add MapStreaming component for seamless map transitions
        if (currentMapName != null)
        {
            var mapStreaming = new MapStreaming(new MapIdentifier(currentMapName));
            world.Add(playerEntity, mapStreaming);
            logger.LogInformation("MapStreaming component added to player");
        }
        else
        {
            logger.LogWarning("No map found - MapStreaming component not added");
        }

        // Add WarpState component for warp transitions
        world.Add(playerEntity, WarpState.Default);
        logger.LogDebug("WarpState component added to player");

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
        logger.LogControlsHint("Zoom: +/- to zoom in/out");

        return playerEntity;
    }
}
