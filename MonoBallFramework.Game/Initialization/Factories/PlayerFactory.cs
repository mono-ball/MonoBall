using Arch.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using MonoBallFramework.Game.Ecs.Components;
using MonoBallFramework.Game.Ecs.Components.Common;
using MonoBallFramework.Game.Ecs.Components.Maps;
using MonoBallFramework.Game.Ecs.Components.Movement;
using MonoBallFramework.Game.Ecs.Components.Player;
using MonoBallFramework.Game.Ecs.Components.Rendering;
using MonoBallFramework.Game.Ecs.Components.Warps;
using MonoBallFramework.Game.Engine.Common.Logging;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.Engine.Input.Components;
using MonoBallFramework.Game.Engine.Rendering.Components;
using MonoBallFramework.Game.Engine.Rendering.Constants;
using MonoBallFramework.Game.Engine.Systems.Management;
using MonoBallFramework.Game.Infrastructure.Configuration;

namespace MonoBallFramework.Game.Initialization.Factories;

/// <summary>
///     Factory for creating player entities with all required components.
/// </summary>
public class PlayerFactory(
    ILogger<PlayerFactory> logger,
    World world
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
        GameMapId? currentGameMapId = null;
        QueryDescription mapInfoQuery = QueryCache.Get<MapInfo>();
        world.Query(
            in mapInfoQuery,
            (ref MapInfo mapInfo) =>
            {
                tileSize = mapInfo.TileSize;
                currentGameMapId = mapInfo.MapId;
            }
        );

        // Calculate GBA-scaled viewport to match what UpdateViewportForResize will set
        // This prevents the "slide in" effect by ensuring viewport is correct from the start
        int scaleX = Math.Max(1, viewportWidth / Camera.GbaNativeWidth);
        int scaleY = Math.Max(1, viewportHeight / Camera.GbaNativeHeight);
        int scale = Math.Min(scaleX, scaleY);
        int gbaViewportWidth = Camera.GbaNativeWidth * scale;
        int gbaViewportHeight = Camera.GbaNativeHeight * scale;

        // Create camera component with GBA-scaled viewport (matches UpdateViewportForResize)
        var viewport = new Rectangle(0, 0, gbaViewportWidth, gbaViewportHeight);

        // Initialize camera position to tile center (matches what CameraFollowSystem will set)
        // This ensures camera is correctly positioned from the start, preventing visual glitches
        float initialPixelX = x * tileSize;
        float initialPixelY = y * tileSize;

        var camera = new Camera(viewport)
        {
            // Set zoom to match GBA scale (same as UpdateViewportForResize does)
            Zoom = scale,
            TargetZoom = scale,
            ZoomTransitionSpeed = gameplayConfig.ZoomTransitionSpeed,
            Position = new Vector2(
                initialPixelX + CameraConstants.HalfTilePixels,
                initialPixelY + CameraConstants.HalfTilePixels
            ),
            // Set reference dimensions so UpdateViewportForResize knows it's already initialized
            ReferenceWidth = viewportWidth,
            ReferenceHeight = viewportHeight
        };

        // Set VirtualViewport to match what UpdateViewportForResize will calculate
        camera.VirtualViewport = new Rectangle(
            (viewportWidth - gbaViewportWidth) / 2,
            (viewportHeight - gbaViewportHeight) / 2,
            gbaViewportWidth,
            gbaViewportHeight
        );

        // Create player entity directly with all required components
        // GridMovement already has FacingDirection = Direction.South as default
        // Direction component is required by InputSystem for tracking input direction
        Entity playerEntity = world.Create(
            new Player(),
            new Name("PLAYER"),
            new Wallet(3000),
            new Position(x, y, currentGameMapId, tileSize),
            new Sprite(new GameSpriteId("base:sprite:players/may/normal")),
            new Elevation(3),
            new GridMovement(3.75f),
            Direction.South, // Direction component for InputSystem
            new Animation("face_south"),
            new InputState(),
            new Collision(true),
            new Visible(),
            camera
        );

        // Add MainCamera tag to mark this as the primary camera
        world.Add<MainCamera>(playerEntity);

        // Add MapStreaming component for seamless map transitions
        if (currentGameMapId != null)
        {
            var mapStreaming = new MapStreaming(currentGameMapId);
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
            ("Sprite", "may/normal"),
            ("GridMovement", "3.75"),
            ("Direction", "south"),
            ("Animation", "face_south"),
            ("Camera", "attached")
        );
        logger.LogControlsHint("Use WASD or Arrow Keys to move!");
        logger.LogControlsHint("Zoom: +/- to zoom in/out");

        return playerEntity;
    }
}
