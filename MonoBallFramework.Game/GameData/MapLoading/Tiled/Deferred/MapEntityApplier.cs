using System.Diagnostics;
using Arch.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using MonoBallFramework.Game.Ecs.Components;
using MonoBallFramework.Game.Ecs.Components.Maps;
using MonoBallFramework.Game.Ecs.Components.Rendering;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.Ecs.Components.Tiles;
using MonoBallFramework.Game.Engine.Content;
using MonoBallFramework.Game.Engine.Rendering.Assets;
using MonoBallFramework.Game.Engine.Systems.BulkOperations;
using MonoBallFramework.Game.Engine.Systems.Management;
using MonoBallFramework.Game.GameData.MapLoading.Tiled.Services;
using MonoBallFramework.Game.GameData.MapLoading.Tiled.Tmx;
using MonoBallFramework.Game.GameData.MapLoading.Tiled.Utilities;
using MonoBallFramework.Game.GameSystems.Spatial;
using MonoBallFramework.Game.Systems;

namespace MonoBallFramework.Game.GameData.MapLoading.Tiled.Deferred;

/// <summary>
///     Applies prepared map data to create ECS entities on the main thread.
///     This is the fast part - all heavy computation already done on background thread.
/// </summary>
public class MapEntityApplier
{
    private readonly IAssetProvider? _assetProvider;
    private readonly IContentProvider? _contentProvider;
    private readonly Lazy<MapLifecycleManager>? _lifecycleManager;
    private readonly ILogger<MapEntityApplier>? _logger;
    private readonly SystemManager? _systemManager;
    private readonly TilesetLoader? _tilesetLoader;

    public MapEntityApplier(
        SystemManager? systemManager = null,
        Func<MapLifecycleManager>? lifecycleManagerFactory = null,
        IAssetProvider? assetProvider = null,
        IContentProvider? contentProvider = null,
        TilesetLoader? tilesetLoader = null,
        ILogger<MapEntityApplier>? logger = null)
    {
        _systemManager = systemManager;
        _lifecycleManager = lifecycleManagerFactory != null
            ? new Lazy<MapLifecycleManager>(lifecycleManagerFactory)
            : null;
        _assetProvider = assetProvider;
        _contentProvider = contentProvider;
        _tilesetLoader = tilesetLoader;
        _logger = logger;
    }

    /// <summary>
    ///     Applies prepared map data to create all entities.
    ///     MUST be called on the main thread.
    /// </summary>
    /// <returns>The map info entity.</returns>
    public Entity ApplyPreparedMap(World world, PreparedMapData data)
    {
        var sw = Stopwatch.StartNew();

        _logger?.LogDebug(
            "ApplyPreparedMap: Applying map {MapId}, Tiles={TileCount}",
            data.MapId.Value,
            data.Tiles.Count
        );

        // 0. Ensure all tileset textures are loaded (CRITICAL for rendering!)
        EnsureTexturesLoaded(data);

        // 1. Create map info entity with MapWorldPosition (critical for streaming)
        Entity mapInfoEntity = CreateMapInfoEntity(world, data);

        // 2. Create TilesetInfo entities (CRITICAL - missing this breaks rendering!)
        CreateTilesetInfoEntities(world, data);

        // 3. Bulk create tile entities (the fast part!)
        List<Entity> tileEntities = CreateTileEntities(world, data);

        // 3. Register with lifecycle manager for cleanup
        _lifecycleManager?.Value.RegisterMapTiles(data.MapId, tileEntities);

        // 4. Collect tileset texture IDs for lifecycle manager
        var tilesetTextureIds = new HashSet<string>();
        foreach (LoadedTileset tileset in data.Tilesets)
        {
            tilesetTextureIds.Add(tileset.TilesetId);
        }

        _lifecycleManager?.Value.RegisterMap(
            data.MapId,
            data.MapName,
            tilesetTextureIds,
            new HashSet<string>() // Sprite textures handled separately
        );

        // 5. Add tiles to spatial hash for rendering (avoids full rebuild)
        if (_systemManager != null && tileEntities.Count > 0)
        {
            SpatialHashSystem? spatialHash = _systemManager.GetSystem<SpatialHashSystem>();
            if (spatialHash != null)
            {
                spatialHash.AddMapTiles(data.MapId, tileEntities);
                _logger?.LogDebug(
                    "Added {TileCount} tiles to spatial hash for map {MapId}",
                    tileEntities.Count,
                    data.MapId.Value
                );
            }
        }

        // 6. CRITICAL: Process animated tiles!
        // This was missing and caused tile animations to stop on streamed maps
        if (data.AnimatedTiles != null && data.AnimatedTiles.Count > 0)
        {
            int animatedCount = ProcessAnimatedTiles(world, data, tileEntities);
            _logger?.LogDebug(
                "Added AnimatedTile components to {Count} tiles in map {MapId}",
                animatedCount,
                data.MapId.Value
            );
        }

        sw.Stop();
        _logger?.LogDebug(
            "Applied prepared map {MapId}: {TileCount} tiles in {ElapsedMs:F2}ms",
            data.MapId.Value,
            tileEntities.Count,
            sw.Elapsed.TotalMilliseconds);

        return mapInfoEntity;
    }

    private Entity CreateMapInfoEntity(World world, PreparedMapData data)
    {
        // MapInfo constructor: (mapId, mapName, width, height, tileSize)
        // TileWidth and TileHeight should be the same for now, use TileWidth
        // Also add MapWarps spatial index for warp lookups (same as MapMetadataFactory)
        Entity entity = world.Create(
            new MapInfo(
                data.MapId,
                data.MapName,
                data.MapWidth,
                data.MapHeight,
                data.TileWidth
            ),
            MapWarps.Create()
        );

        // CRITICAL: Add MapWorldPosition for streaming system to find and track this map
        var mapWorldPos = new MapWorldPosition(
            data.WorldOffset,
            data.MapWidth,
            data.MapHeight,
            data.TileWidth
        );
        world.Add(entity, mapWorldPos);

        // Add optional components
        if (!string.IsNullOrEmpty(data.Name))
        {
            world.Add(entity, new DisplayName(data.Name));
        }

        if (!string.IsNullOrEmpty(data.RegionSection))
        {
            world.Add(entity, new RegionSection(data.RegionSection));
        }

        // Add Music component for map music
        if (!string.IsNullOrEmpty(data.MusicTrack))
        {
            GameAudioId? musicId = GameAudioId.TryCreate(data.MusicTrack);
            if (musicId != null)
            {
                world.Add(entity, new Music(musicId));
            }
        }

        // CRITICAL: Add connection components for map streaming
        if (data.Connections != null && data.Connections.Count > 0)
        {
            foreach (MapConnection conn in data.Connections)
            {
                switch (conn.Direction.ToLowerInvariant())
                {
                    case "north":
                        world.Add(entity, new NorthConnection(conn.TargetMapId, conn.Offset));
                        break;
                    case "south":
                        world.Add(entity, new SouthConnection(conn.TargetMapId, conn.Offset));
                        break;
                    case "east":
                        world.Add(entity, new EastConnection(conn.TargetMapId, conn.Offset));
                        break;
                    case "west":
                        world.Add(entity, new WestConnection(conn.TargetMapId, conn.Offset));
                        break;
                }
            }

            _logger?.LogDebug(
                "Added {ConnectionCount} connections to map {MapId}",
                data.Connections.Count,
                data.MapId.Value
            );
        }

        // CRITICAL: Add ShowMapNameOnEntry for popup display!
        // This was missing and caused popups not to show on streamed maps
        if (data.ShowMapName)
        {
            world.Add(entity, new ShowMapNameOnEntry());
            _logger?.LogDebug("Added ShowMapNameOnEntry component to map {MapId}", data.MapId.Value);
        }

        // CRITICAL: Add MapBorder for edge rendering!
        // This was missing and caused borders to stop rendering on streamed maps
        if (data.BorderData != null)
        {
            AddMapBorder(world, entity, data);
        }

        return entity;
    }

    /// <summary>
    ///     Adds MapBorder component with pre-calculated source rectangles.
    /// </summary>
    private void AddMapBorder(World world, Entity entity, PreparedMapData data)
    {
        PreparedBorderData borderData = data.BorderData!;

        // Find the primary tileset for source rect calculation
        LoadedTileset? primaryTileset = data.Tilesets.FirstOrDefault(t => t.TilesetId == borderData.TilesetId);
        if (primaryTileset == null && data.Tilesets.Count > 0)
        {
            primaryTileset = data.Tilesets[0];
        }

        if (primaryTileset == null)
        {
            _logger?.LogWarning("Cannot add MapBorder - no tileset available for map {MapId}", data.MapId.Value);
            return;
        }

        var mapBorder = new MapBorder(borderData.BottomLayerGids, borderData.TopLayerGids, borderData.TilesetId);

        // Pre-calculate source rectangles for bottom layer
        mapBorder.BottomSourceRects = new Rectangle[4];
        for (int i = 0; i < 4; i++)
        {
            int tileGid = borderData.BottomLayerGids[i];
            if (tileGid > 0)
            {
                mapBorder.BottomSourceRects[i] = TilesetUtilities.CalculateSourceRect(tileGid, primaryTileset.Tileset);
            }
        }

        // Pre-calculate source rectangles for top layer
        mapBorder.TopSourceRects = new Rectangle[4];
        for (int i = 0; i < 4; i++)
        {
            int tileGid = borderData.TopLayerGids[i];
            if (tileGid > 0)
            {
                mapBorder.TopSourceRects[i] = TilesetUtilities.CalculateSourceRect(tileGid, primaryTileset.Tileset);
            }
        }

        world.Add(entity, mapBorder);
        _logger?.LogDebug(
            "Added MapBorder to map {MapId}: Bottom=[{B0},{B1},{B2},{B3}], Top=[{T0},{T1},{T2},{T3}]",
            data.MapId.Value,
            borderData.BottomLayerGids[0], borderData.BottomLayerGids[1],
            borderData.BottomLayerGids[2], borderData.BottomLayerGids[3],
            borderData.TopLayerGids[0], borderData.TopLayerGids[1],
            borderData.TopLayerGids[2], borderData.TopLayerGids[3]
        );
    }

    /// <summary>
    ///     Processes animated tiles by adding AnimatedTile components to matching tile entities.
    ///     This is critical for tile animations to work on streamed maps.
    /// </summary>
    private int ProcessAnimatedTiles(World world, PreparedMapData data, List<Entity> tileEntities)
    {
        if (data.AnimatedTiles == null || data.AnimatedTiles.Count == 0)
        {
            return 0;
        }

        // Build a lookup of animated tile data by GID for fast matching
        var animationsByGid = new Dictionary<int, PreparedAnimatedTile>(data.AnimatedTiles.Count);
        foreach (PreparedAnimatedTile animTile in data.AnimatedTiles)
        {
            animationsByGid[animTile.TileGid] = animTile;
        }

        // Find tileset info for each animated tile
        var tilesetInfoByGid = new Dictionary<int, LoadedTileset>();
        foreach (PreparedAnimatedTile animTile in data.AnimatedTiles)
        {
            LoadedTileset? tileset = data.Tilesets.FirstOrDefault(t => t.TilesetId == animTile.TilesetId);
            if (tileset != null)
            {
                tilesetInfoByGid[animTile.TileGid] = tileset;
            }
        }

        int animatedCount = 0;

        // Iterate through tile entities and add AnimatedTile component where needed
        for (int i = 0; i < tileEntities.Count && i < data.Tiles.Count; i++)
        {
            PreparedTile tileData = data.Tiles[i];
            int tileGid = tileData.TileGid;

            if (!animationsByGid.TryGetValue(tileGid, out PreparedAnimatedTile animData))
            {
                continue;
            }

            if (!tilesetInfoByGid.TryGetValue(tileGid, out LoadedTileset? tileset))
            {
                continue;
            }

            Entity entity = tileEntities[i];
            TmxTileset tmxTileset = tileset.Tileset;

            // Convert millisecond durations to seconds
            var frameDurations = new float[animData.FrameDurations.Length];
            var frameTileIds = new int[animData.FrameDurations.Length];
            for (int f = 0; f < animData.FrameDurations.Length; f++)
            {
                frameDurations[f] = animData.FrameDurations[f] / 1000f;
                // Frame tile IDs are relative to tileset FirstGid
                frameTileIds[f] = tmxTileset.FirstGid + f; // Approximation - will use source rects
            }

            // Calculate tiles per row
            int tilesPerRow = tmxTileset.Image != null && tmxTileset.TileWidth > 0
                ? tmxTileset.Image.Width / tmxTileset.TileWidth
                : 1;

            var animatedTile = new AnimatedTile(
                baseTileId: tileGid - tmxTileset.FirstGid,
                frameTileIds: frameTileIds,
                frameDurations: frameDurations,
                frameSourceRects: animData.FrameSourceRects,
                tilesetFirstGid: tmxTileset.FirstGid,
                tilesPerRow: tilesPerRow,
                tileWidth: tmxTileset.TileWidth,
                tileHeight: tmxTileset.TileHeight,
                tileSpacing: tmxTileset.Spacing,
                tileMargin: tmxTileset.Margin
            );

            world.Add(entity, animatedTile);
            animatedCount++;
        }

        return animatedCount;
    }

    /// <summary>
    ///     Creates TilesetInfo entities for each tileset used by the map.
    ///     Required for tile rendering to find tileset metadata.
    /// </summary>
    private void CreateTilesetInfoEntities(World world, PreparedMapData data)
    {
        foreach (LoadedTileset loadedTileset in data.Tilesets)
        {
            TmxTileset tileset = loadedTileset.Tileset;

            // Validate tileset data
            if (tileset.FirstGid <= 0)
            {
                _logger?.LogWarning(
                    "Skipping tileset '{TilesetId}' with invalid FirstGid {FirstGid}",
                    loadedTileset.TilesetId,
                    tileset.FirstGid
                );
                continue;
            }

            if (tileset.TileWidth <= 0 || tileset.TileHeight <= 0)
            {
                _logger?.LogWarning(
                    "Skipping tileset '{TilesetId}' with invalid tile size {Width}x{Height}",
                    loadedTileset.TilesetId,
                    tileset.TileWidth,
                    tileset.TileHeight
                );
                continue;
            }

            if (tileset.Image == null || tileset.Image.Width <= 0 || tileset.Image.Height <= 0)
            {
                _logger?.LogWarning(
                    "Skipping tileset '{TilesetId}' with invalid image dimensions",
                    loadedTileset.TilesetId
                );
                continue;
            }

            var tilesetInfo = new TilesetInfo(
                loadedTileset.TilesetId,
                tileset.FirstGid,
                tileset.TileWidth,
                tileset.TileHeight,
                tileset.Image.Width,
                tileset.Image.Height
            );
            world.Create(tilesetInfo);
        }

        _logger?.LogDebug(
            "Created {Count} TilesetInfo entities for map {MapId}",
            data.Tilesets.Count,
            data.MapId.Value
        );
    }

    private List<Entity> CreateTileEntities(World world, PreparedMapData data)
    {
        if (data.Tiles.Count == 0)
        {
            return new List<Entity>();
        }

        var bulkOps = new BulkEntityOperations(world);

        // Bulk create with TilePosition and TileSprite (the two required components)
        Entity[] entities = bulkOps.CreateEntities(
            data.Tiles.Count,
            i => new TilePosition(data.Tiles[i].X, data.Tiles[i].Y, data.Tiles[i].MapId),
            i =>
            {
                PreparedTile tile = data.Tiles[i];
                return new TileSprite(
                    tile.TilesetId,
                    tile.TileGid,
                    tile.SourceRect,
                    tile.FlipH,
                    tile.FlipV,
                    tile.FlipD
                );
            }
        );

        // Add additional components (Elevation, LayerOffset, properties)
        for (int i = 0; i < entities.Length; i++)
        {
            Entity entity = entities[i];
            PreparedTile tile = data.Tiles[i];

            // Always add elevation
            world.Add(entity, new Elevation(tile.Elevation));

            // Add layer offset if needed
            if (tile.LayerOffsetX != 0 || tile.LayerOffsetY != 0)
            {
                world.Add(entity, new LayerOffset((int)tile.LayerOffsetX, (int)tile.LayerOffsetY));
            }

            // Process tile properties (terrain, script, etc.)
            if (tile.Properties != null)
            {
                ProcessTileProperties(world, entity, tile.Properties);
            }
        }

        return entities.ToList();
    }

    private void ProcessTileProperties(World world, Entity entity, Dictionary<string, object> props)
    {
        // Add TerrainType if present
        if (props.TryGetValue("terrain_type", out object? terrainValue) && terrainValue is string terrainType)
        {
            string footstepSound = props.TryGetValue("footstep_sound", out object? soundValue)
                ? soundValue?.ToString() ?? ""
                : "";
            world.Add(entity, new TerrainType(terrainType, footstepSound));
        }

        // Add TileScript if present
        if (props.TryGetValue("script", out object? scriptValue) && scriptValue is string scriptPath)
        {
            world.Add(entity, new TileScript(scriptPath));
        }
    }

    /// <summary>
    ///     Ensures all tileset textures are loaded before creating entities.
    ///     The async preload may have queued textures, but they need to be on GPU.
    ///     Falls back to sync loading if textures aren't ready.
    /// </summary>
    private void EnsureTexturesLoaded(PreparedMapData data)
    {
        if (_assetProvider == null)
        {
            _logger?.LogWarning("AssetProvider not available - textures may not render");
            return;
        }

        // First, process any pending async textures
        if (_assetProvider is AssetManager assetManager)
        {
            // Process pending texture uploads to GPU
            int uploaded = assetManager.ProcessTextureQueue();
            if (uploaded > 0)
            {
                _logger?.LogDebug("Processed {Count} pending texture uploads for map {MapId}",
                    uploaded, data.MapId.Value);
            }
        }

        // Check each tileset and load synchronously if needed
        int loadedCount = 0;
        foreach (LoadedTileset loadedTileset in data.Tilesets)
        {
            string tilesetId = loadedTileset.TilesetId;

            if (!_assetProvider.HasTexture(tilesetId))
            {
                // Texture not loaded - need to load it synchronously
                TmxTileset tileset = loadedTileset.Tileset;
                if (tileset.Image != null && !string.IsNullOrEmpty(tileset.Image.Source))
                {
                    string mapDirectory = Path.GetDirectoryName(data.MapPath) ?? string.Empty;
                    string imagePath = Path.Combine(mapDirectory, tileset.Image.Source);

                    // Resolve path through content provider
                    if (_contentProvider != null)
                    {
                        string? resolvedPath =
                            _contentProvider.ResolveContentPath("Graphics", Path.GetFileName(imagePath));
                        if (resolvedPath == null)
                        {
                            _logger?.LogError("Failed to resolve tileset texture path: {TilesetId} from {Path}",
                                tilesetId, imagePath);
                            continue;
                        }

                        imagePath = resolvedPath;
                    }

                    try
                    {
                        _assetProvider.LoadTexture(tilesetId, imagePath);
                        loadedCount++;
                        _logger?.LogDebug("Loaded tileset texture synchronously: {TilesetId}", tilesetId);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Failed to load tileset texture: {TilesetId} from {Path}",
                            tilesetId, imagePath);
                    }
                }
            }
        }

        if (loadedCount > 0)
        {
            _logger?.LogInformation("Loaded {Count} tileset textures synchronously for map {MapId}",
                loadedCount, data.MapId.Value);
        }
    }
}
