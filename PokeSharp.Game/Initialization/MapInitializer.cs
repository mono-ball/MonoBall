using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using PokeSharp.Engine.Common.Logging;
using PokeSharp.Engine.Core.Types;
using PokeSharp.Engine.Rendering.Systems;
using PokeSharp.Game.Components.Maps;
using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Data.MapLoading.Tiled;
using PokeSharp.Game.Systems;
using EcsQueries = PokeSharp.Engine.Systems.Queries.Queries;

namespace PokeSharp.Game.Initialization;

/// <summary>
///     Handles map loading and initialization.
/// </summary>
public class MapInitializer(
    ILogger<MapInitializer> logger,
    World world,
    MapLoader mapLoader,
    SpatialHashSystem spatialHashSystem,
    ElevationRenderSystem renderSystem,
    MapLifecycleManager mapLifecycleManager,
    SpriteTextureLoader? spriteTextureLoader = null
)
{
    private SpriteTextureLoader _spriteTextureLoader = spriteTextureLoader!;

    /// <summary>
    ///     Sets the sprite texture loader after construction (for delayed initialization).
    /// </summary>
    public void SetSpriteTextureLoader(SpriteTextureLoader loader)
    {
        _spriteTextureLoader = loader ?? throw new ArgumentNullException(nameof(loader));
    }
    /// <summary>
    ///     Loads a map from EF Core definition (NEW: Definition-based loading).
    ///     Creates individual entities for each tile with appropriate components.
    /// </summary>
    /// <param name="mapId">The map identifier (e.g., "test-map", "littleroot_town").</param>
    /// <returns>The MapInfo entity containing map metadata.</returns>
    public async Task<Entity?> LoadMap(MapIdentifier mapId)
    {
        try
        {
            logger.LogWorkflowStatus("Loading map from definition", ("mapId", mapId.Value));

            // Load map from EF Core definition (NEW: Definition-based)
            var mapInfoEntity = mapLoader.LoadMap(world, mapId);
            logger.LogWorkflowStatus("Map entities created", ("entity", mapInfoEntity.Id));

            // Get MapInfo to extract map ID and name for lifecycle tracking
            var mapInfo = mapInfoEntity.Get<MapInfo>();
            var mapName = mapInfo.MapName ?? mapId.Value;

            // Complete post-loading steps (shared logic)
            await CompleteMapLoadingAsync(mapInfoEntity, mapInfo, mapName);

            logger.LogWorkflowStatus("Map load complete", ("mapId", mapId.Value));
            return mapInfoEntity;
        }
        catch (Exception ex)
        {
            logger.LogExceptionWithContext(
                ex,
                "Failed to load map: {MapId}. Game will continue without map",
                mapId.Value
            );
            return null;
        }
    }

    /// <summary>
    ///     Loads a map from file path (LEGACY: Backward compatibility).
    ///     Use LoadMap(mapId) for definition-based loading instead.
    /// </summary>
    /// <param name="mapPath">Path to the map file.</param>
    /// <returns>The MapInfo entity containing map metadata.</returns>
    public async Task<Entity?> LoadMapFromFile(string mapPath)
    {
        try
        {
            logger.LogWorkflowStatus("Loading map from file (LEGACY)", ("path", mapPath));

            // Load map as tile entities (file-based approach for backward compatibility)
            var mapInfoEntity = mapLoader.LoadMapEntities(world, mapPath);
            logger.LogWorkflowStatus("Map entities created", ("entity", mapInfoEntity.Id));

            // Get MapInfo to extract map ID and name for lifecycle tracking
            var mapInfo = mapInfoEntity.Get<MapInfo>();
            var mapName = mapInfo.MapName ?? mapPath;

            // Complete post-loading steps (shared logic)
            await CompleteMapLoadingAsync(mapInfoEntity, mapInfo, mapName);

            logger.LogWorkflowStatus("Map load complete (LEGACY)", ("path", mapPath));
            return mapInfoEntity;
        }
        catch (Exception ex)
        {
            logger.LogExceptionWithContext(
                ex,
                "Failed to load map: {MapPath}. Game will continue without map",
                mapPath
            );
            return null;
        }
    }

    /// <summary>
    ///     Completes post-loading steps for a map: sprite loading, lifecycle registration,
    ///     spatial hash invalidation, and render system setup.
    /// </summary>
    private async Task CompleteMapLoadingAsync(Entity mapInfoEntity, MapInfo mapInfo, string mapName)
    {
        var tilesetTextureIds = mapLoader.GetLoadedTextureIds(mapInfo.MapId);

        // Load sprites for NPCs in this map
        var spriteTextureKeys = await LoadMapSpritesAsync(mapInfo.MapId);

        // Register map with lifecycle manager BEFORE transitioning
        var safeTilesetIds = tilesetTextureIds ?? new HashSet<string>();
        var safeSpriteKeys = spriteTextureKeys ?? new HashSet<string>();
        mapLifecycleManager.RegisterMap(mapInfo.MapId, mapName, safeTilesetIds, safeSpriteKeys);

        // Transition to new map (cleans up old maps)
        mapLifecycleManager.TransitionToMap(mapInfo.MapId);
        logger.LogWorkflowStatus("Map lifecycle transition complete", ("mapId", mapInfo.MapId));

        // Invalidate spatial hash to reindex static tiles
        spatialHashSystem.InvalidateStaticTiles();
        logger.LogWorkflowStatus("Spatial hash invalidated", ("cells", "static"));

        // Preload all textures used by the map to avoid loading spikes during gameplay
        renderSystem.PreloadMapAssets(world);
        logger.LogWorkflowStatus("Render assets preloaded");

        // Set camera bounds and tile size from MapInfo
        world.Query(
            in EcsQueries.MapInfo,
            (ref MapInfo info) =>
            {
                renderSystem.SetTileSize(info.TileSize);
                logger.LogWorkflowStatus(
                    "Camera bounds updated",
                    ("widthPx", info.PixelWidth),
                    ("heightPx", info.PixelHeight)
                );
            }
        );
    }

    /// <summary>
    ///     Loads sprites required for the map's NPCs.
    /// </summary>
    private async Task<HashSet<string>> LoadMapSpritesAsync(MapRuntimeId mapId)
    {
        if (_spriteTextureLoader == null)
        {
            logger.LogWarning("SpriteTextureLoader not set - skipping sprite loading for map {MapId}", mapId.Value);
            return new HashSet<string>();
        }

        var requiredSpriteIds = mapLoader.GetRequiredSpriteIds();
        try
        {
            var spriteTextureKeys = await _spriteTextureLoader.LoadSpritesForMapAsync(mapId, requiredSpriteIds);
            logger.LogAssetStatus(
                "Map sprites loaded",
                ("mapId", mapId.Value),
                ("spriteCount", spriteTextureKeys.Count));
            return spriteTextureKeys;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to load sprites for map {MapId}, using fallback textures",
                mapId.Value);
            return new HashSet<string>();
        }
    }
}
