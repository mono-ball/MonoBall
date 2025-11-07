using Arch.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using PokeSharp.Core.Components.Maps;
using PokeSharp.Core.Logging;
using PokeSharp.Core.Systems;
using PokeSharp.Rendering.Loaders;
using PokeSharp.Rendering.Systems;

namespace PokeSharp.Game.Initialization;

/// <summary>
///     Handles map loading and initialization.
/// </summary>
public class MapInitializer(
    ILogger<MapInitializer> logger,
    World world,
    MapLoader mapLoader,
    SpatialHashSystem spatialHashSystem,
    ZOrderRenderSystem renderSystem)
{
    private readonly ILogger<MapInitializer> _logger = logger;
    private readonly World _world = world;
    private readonly MapLoader _mapLoader = mapLoader;
    private readonly SpatialHashSystem _spatialHashSystem = spatialHashSystem;
    private readonly ZOrderRenderSystem _renderSystem = renderSystem;

    /// <summary>
    ///     Loads the test map using the entity-based tile system.
    ///     Creates individual entities for each tile with appropriate components.
    /// </summary>
    /// <param name="mapPath">Path to the map file.</param>
    /// <returns>The MapInfo entity containing map metadata.</returns>
    public Entity? LoadMap(string mapPath)
    {
        try
        {
            // Load map as tile entities (ECS-based approach)
            var mapInfoEntity = _mapLoader.LoadMapEntities(_world, mapPath);

            // Invalidate spatial hash to reindex static tiles
            _spatialHashSystem.InvalidateStaticTiles();

            // Preload all textures used by the map to avoid loading spikes during gameplay
            _renderSystem.PreloadMapAssets(_world);

            // Set camera bounds from MapInfo
            var mapInfoQuery = new QueryDescription().WithAll<MapInfo>();
            _world.Query(
                in mapInfoQuery,
                (ref MapInfo mapInfo) =>
                {
                    _logger.LogInformation(
                        "Camera bounds set to {Width}x{Height} pixels",
                        mapInfo.PixelWidth,
                        mapInfo.PixelHeight
                    );
                }
            );

            return mapInfoEntity;
        }
        catch (Exception ex)
        {
            _logger.LogExceptionWithContext(ex, "Failed to load map: {MapPath}. Game will continue without map", mapPath);
            return null;
        }
    }

    /// <summary>
    ///     Sets the camera map bounds based on tilemap dimensions.
    /// </summary>
    /// <param name="mapWidthInTiles">Map width in tiles.</param>
    /// <param name="mapHeightInTiles">Map height in tiles.</param>
    public Rectangle GetMapBounds(int mapWidthInTiles, int mapHeightInTiles)
    {
        const int tileSize = 16;
        return new Rectangle(
            0,
            0,
            mapWidthInTiles * tileSize,
            mapHeightInTiles * tileSize
        );
    }
}

