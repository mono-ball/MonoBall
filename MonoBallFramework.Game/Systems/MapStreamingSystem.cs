using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using MonoBallFramework.Game.Ecs.Components;
using MonoBallFramework.Game.Ecs.Components.Maps;
using MonoBallFramework.Game.Ecs.Components.Movement;
using MonoBallFramework.Game.Ecs.Components.Player;
using MonoBallFramework.Game.Engine.Core.Events;
using MonoBallFramework.Game.Engine.Core.Events.Map;
using MonoBallFramework.Game.Engine.Core.Systems;
using MonoBallFramework.Game.Engine.Core.Systems.Base;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.Engine.Systems.Management;
using MonoBallFramework.Game.GameData.Entities;
using MonoBallFramework.Game.GameData.MapLoading.Tiled.Core;
using MonoBallFramework.Game.GameData.Services;
using MonoBallFramework.Game.GameSystems.Movement;

namespace MonoBallFramework.Game.Systems;

/// <summary>
///     System that handles dynamic map loading/unloading for seamless Pokemon-style map streaming.
///     Detects when player approaches map boundaries and preloads adjacent maps.
///     Unloads maps that are far away to conserve memory.
/// </summary>
/// <remarks>
///     <para>
///         <strong>Algorithm Overview:</strong>
///         1. Calculate distance from player to each edge of current map
///         2. If within streaming radius (80 pixels / 5 tiles), check for connections
///         3. Load adjacent map if connection exists and not already loaded
///         4. Calculate correct world offset based on connection direction
///         5. Unload maps beyond 2x streaming radius (unload distance)
///     </para>
///     <para>
///         <strong>Priority:</strong> 100 (same as Movement) - executes early in update loop
///         to ensure maps are loaded before player needs them.
///     </para>
///     <para>
///         <strong>Async Preloading Architecture:</strong>
///         When maps are loaded, we asynchronously preload their TMX documents and tilesets
///         into the cache for future transitions. This reduces stutter when the player crosses
///         map boundaries.
///     </para>
///     <para>
///         1. <strong>Synchronous Phase:</strong> LoadAdjacentMap() creates ECS entities immediately
///         2. <strong>Async Phase:</strong> PreloadAdjacentMapsAsync() warms caches in background
///         - PreloadTmxDocumentAsync(): Loads/parses JSON on background thread
///         - PreloadMapTexturesAsync(): Queues textures for GPU upload
///         3. <strong>Next Transition:</strong> When player crosses boundary, assets are cached
///     </para>
///     <para>
///         The preloading uses Task.WhenAll for parallel execution and tracks completed
///         preloads to avoid redundant work. All errors are caught and logged without
///         blocking the main thread.
///     </para>
/// </remarks>
public class MapStreamingSystem : SystemBase, IUpdateSystem
{
    // Maximum number of maps to keep loaded simultaneously
    // Current + 4 directions + buffer for recently visited maps
    // Pokemon-style: keeps maps loaded to prevent excessive load/unload churn
    private const int MaxLoadedMaps = 8;
    private readonly IEventBus? _eventBus;
    private readonly ILogger<MapStreamingSystem>? _logger;
    private readonly MapEntityService _mapDefinitionService;

    // Map info cache to avoid nested queries (O(N×M) -> O(N))
    private readonly Dictionary<
        string,
        (MapInfo Info, MapWorldPosition WorldPos, MapEntity? Definition)
    > _mapInfoCache = new(10);

    private readonly MapLoader _mapLoader;

    // Track which maps have been preloaded to avoid redundant preloading
    private readonly HashSet<string> _preloadedMaps = new();

    // Invalidation batching flag - set when maps are loaded/unloaded, processed once at end of frame
    private bool _invalidationNeeded;

    // Optional lifecycle manager for proper entity cleanup (set after initialization)
    private MapLifecycleManager? _lifecycleManager;

    // Dirty flag to avoid rebuilding cache every frame - only rebuild when maps change
    private bool _mapCacheDirty = true;
    private QueryDescription _mapInfoQuery;

    // Optional movement system for cache invalidation during map transitions
    private MovementSystem? _movementSystem;

    // Cached queries for performance
    private QueryDescription _playerQuery;

    /// <summary>
    ///     Creates a new MapStreamingSystem with required services.
    /// </summary>
    /// <param name="mapLoader">MapLoader service for loading/unloading maps.</param>
    /// <param name="mapDefinitionService">Service for accessing map definitions and connections.</param>
    /// <param name="eventBus">Optional event bus for publishing map transition events.</param>
    /// <param name="logger">Optional logger for debugging streaming operations.</param>
    public MapStreamingSystem(
        MapLoader mapLoader,
        MapEntityService mapDefinitionService,
        IEventBus? eventBus = null,
        ILogger<MapStreamingSystem>? logger = null
    )
    {
        _mapLoader = mapLoader ?? throw new ArgumentNullException(nameof(mapLoader));
        _mapDefinitionService =
            mapDefinitionService ?? throw new ArgumentNullException(nameof(mapDefinitionService));
        _eventBus = eventBus;
        _logger = logger;
    }

    /// <summary>
    ///     Gets the update priority. Lower values execute first.
    ///     Streaming executes at priority 100, same as movement system.
    /// </summary>
    public override int Priority => SystemPriority.Movement;

    /// <inheritdoc />
    public override void Initialize(World world)
    {
        base.Initialize(world);

        // Query for player with streaming component
        _playerQuery = new QueryDescription().WithAll<Player, Position, MapStreaming>();

        // Query for map info entities with world position
        _mapInfoQuery = new QueryDescription().WithAll<MapInfo, MapWorldPosition>();
    }

    /// <inheritdoc />
    public override void Update(World world, float deltaTime)
    {
        if (!Enabled)
        {
            return;
        }

        EnsureInitialized();

        // Update map info cache once per frame to avoid nested queries
        UpdateMapInfoCache(world);

        // Process each player with map streaming
        world.Query(
            in _playerQuery,
            (Entity playerEntity, ref Position position, ref MapStreaming streaming) =>
            {
                ProcessMapStreaming(world, playerEntity, ref position, ref streaming);
            }
        );
    }

    /// <summary>
    ///     Sets the MapLifecycleManager for proper entity cleanup during map unloading.
    ///     Must be called after MapLifecycleManager is created (delayed initialization).
    /// </summary>
    /// <param name="lifecycleManager">The lifecycle manager instance.</param>
    public void SetLifecycleManager(MapLifecycleManager lifecycleManager)
    {
        _lifecycleManager =
            lifecycleManager ?? throw new ArgumentNullException(nameof(lifecycleManager));
        _logger?.LogDebug("MapStreamingSystem: MapLifecycleManager set for entity cleanup");
    }

    /// <summary>
    ///     Sets the MovementSystem for cache invalidation during map transitions.
    ///     Must be called after MovementSystem is created (delayed initialization).
    /// </summary>
    /// <param name="movementSystem">The movement system instance.</param>
    public void SetMovementSystem(MovementSystem movementSystem)
    {
        _movementSystem = movementSystem ?? throw new ArgumentNullException(nameof(movementSystem));
        _logger?.LogDebug("MapStreamingSystem: MovementSystem set for cache invalidation");
    }

    /// <summary>
    ///     Invalidates the map info cache, forcing a rebuild on next update.
    ///     Call this when maps are loaded or unloaded.
    /// </summary>
    public void InvalidateMapCache()
    {
        _mapCacheDirty = true;
    }

    /// <summary>
    ///     Updates the map info cache with current map data.
    ///     Only rebuilds when cache is dirty (maps loaded/unloaded) to avoid per-frame overhead.
    /// </summary>
    private void UpdateMapInfoCache(World world)
    {
        if (!_mapCacheDirty)
        {
            return;
        }

        _mapInfoCache.Clear();
        world.Query(
            in _mapInfoQuery,
            (ref MapInfo info, ref MapWorldPosition pos) =>
            {
                _mapInfoCache[info.MapName] = (info, pos, null);
            }
        );
        _mapCacheDirty = false;
    }

    /// <summary>
    ///     Processes map streaming for a single player.
    ///     Checks boundaries and loads/unloads maps as needed.
    /// </summary>
    private void ProcessMapStreaming(
        World world,
        Entity playerEntity,
        ref Position position,
        ref MapStreaming streaming
    )
    {
        // Skip streaming if no maps are loaded (we're in a warp transition)
        if (_mapInfoCache.Count == 0)
        {
            _logger?.LogDebug(
                "Skipping map streaming - no maps loaded (likely mid-warp transition)"
            );
            return;
        }

        // Create local copy to avoid ref struct capture in lambda
        MapStreaming streamingCopy = streaming;

        // Try to build the map context from cached data
        MapLoadContext? context = TryGetMapContext(streamingCopy.CurrentMapId);
        if (!context.HasValue)
        {
            // This happens for indoor maps (Pokemon Centers, buildings, caves) which don't have
            // map streaming connections - they're loaded via warps, not boundary streaming.
            // Also happens during warp transitions when the old map is already unloaded.
            // Use Trace level to avoid log spam since this check runs every frame.
            _logger?.LogTrace(
                "Map not in streaming cache (indoor/warp map): {MapId}",
                streamingCopy.CurrentMapId.Value
            );
            return;
        }

        // Load connected maps directly
        LoadConnectedMaps(world, ref streamingCopy, context.Value);

        // Update current map if player has crossed boundary
        UpdateCurrentMap(
            world,
            ref position,
            ref streamingCopy,
            context.Value.WorldPosition,
            context.Value.Info
        );

        // Unload distant maps
        UnloadDistantMaps(world, ref position, ref streamingCopy);

        // Batch all cache invalidations at the end of the frame
        // This prevents multiple spatial hash rebuilds during multi-map transitions
        if (_invalidationNeeded)
        {
            InvalidateMapCache();
            _movementSystem?.InvalidateMapWorldOffset();
            _invalidationNeeded = false;
        }

        // Apply changes back to the ref parameter
        streaming = streamingCopy;
    }

    /// <summary>
    ///     Tries to build a MapLoadContext from cached data for a given map.
    /// </summary>
    private MapLoadContext? TryGetMapContext(GameMapId mapId)
    {
        if (
            !_mapInfoCache.TryGetValue(
                mapId.Value,
                out (MapInfo Info, MapWorldPosition WorldPos, MapEntity? Definition) mapData
            )
        )
        {
            _logger?.LogDebug("TryGetMapContext: Map {MapId} not in cache", mapId.Value);
            return null;
        }

        // Find the map entity by its map name (full ID)
        Entity? foundEntity = null;
        QueryDescription query = QueryCache.Get<MapInfo>();
        World.Query(
            in query,
            (Entity entity, ref MapInfo info) =>
            {
                if (info.MapName == mapId.Value)
                {
                    foundEntity = entity;
                }
            }
        );

        if (!foundEntity.HasValue)
        {
            _logger?.LogWarning("TryGetMapContext: Entity not found for map {MapId}", mapId.Value);
            return null;
        }

        return new MapLoadContext(foundEntity.Value, mapData.Info, mapData.WorldPos);
    }

    /// <summary>
    ///     Loads all connected maps that aren't already loaded.
    ///     Uses deferred loading when prepared data is available.
    /// </summary>
    private void LoadConnectedMaps(World world, ref MapStreaming streaming, MapLoadContext context)
    {
        var connectionsToLoad = new List<ConnectionInfo>();
        var allConnections = context.GetAllConnections().ToList();

        foreach (ConnectionInfo connection in allConnections)
        {
            // Skip if already loaded
            if (streaming.IsMapLoaded(connection.MapId))
            {
                continue;
            }

            connectionsToLoad.Add(connection);

            // Start preparing the map in background (fire-and-forget)
            // This way, if we have multiple maps to load, later ones might be ready
            PrepareMapForLoading(context, connection);
        }

        // Only log when we're actually loading new maps
        if (connectionsToLoad.Count > 0)
        {
            _logger?.LogInformation(
                "LoadConnectedMaps: Map {MapId} loading {Count} new connections (total connections={TotalConn}, loaded={LoadedCount})",
                context.Info.MapName,
                connectionsToLoad.Count,
                allConnections.Count,
                streaming.LoadedMaps.Count
            );

            foreach (ConnectionInfo connection in connectionsToLoad)
            {
                _logger?.LogInformation(
                    "LoadConnectedMaps: Loading {Direction} -> {TargetMapId}",
                    connection.Direction,
                    connection.MapId.Value
                );
            }
        }

        // Now load all the maps (they might be prepared by now)
        foreach (ConnectionInfo connection in connectionsToLoad)
        {
            LoadAdjacentMap(world, ref streaming, context, connection);
        }
    }

    /// <summary>
    ///     Starts background preparation of a connected map.
    ///     The map will be ready for instant loading when actually needed.
    /// </summary>
    private void PrepareMapForLoading(MapLoadContext sourceContext, ConnectionInfo connection)
    {
        // Skip if already prepared
        if (_mapLoader.IsMapPrepared(connection.MapId))
        {
            return;
        }

        // Get dimensions to calculate offset
        (int Width, int Height)? dimensions = GetAdjacentMapDimensions(connection.MapId, sourceContext.Info);
        if (!dimensions.HasValue)
        {
            return;
        }

        // Calculate world offset
        Vector2 adjacentOffset = CalculateMapOffset(
            sourceContext.WorldPosition,
            sourceContext.Info.Width,
            sourceContext.Info.Height,
            dimensions.Value.Width,
            dimensions.Value.Height,
            sourceContext.Info.TileSize,
            connection.Direction,
            connection.Offset
        );

        // Start background preparation
        _mapLoader.PrepareMapInBackground(connection.MapId, adjacentOffset);
        _logger?.LogDebug(
            "Started background preparation for map: {MapId} at offset ({X}, {Y})",
            connection.MapId.Value,
            adjacentOffset.X,
            adjacentOffset.Y
        );
    }

    /// <summary>
    ///     Loads an adjacent map based on a connection.
    /// </summary>
    private void LoadAdjacentMap(
        World world,
        ref MapStreaming streaming,
        MapLoadContext sourceContext,
        ConnectionInfo connection
    )
    {
        _logger?.LogDebug(
            "Loading connected map in {Direction} direction: {MapId} (offset: {Offset} tiles)",
            connection.Direction,
            connection.MapId.Value,
            connection.Offset
        );

        // Get adjacent map dimensions (needed for correct offset calculation)
        (int Width, int Height)? adjacentDimensions = GetAdjacentMapDimensions(
            connection.MapId,
            sourceContext.Info
        );
        if (!adjacentDimensions.HasValue)
        {
            return;
        }

        (int adjacentWidth, int adjacentHeight) = adjacentDimensions.Value;

        // Calculate world offset for the adjacent map
        Vector2 adjacentOffset = CalculateMapOffset(
            sourceContext.WorldPosition,
            sourceContext.Info.Width,
            sourceContext.Info.Height,
            adjacentWidth,
            adjacentHeight,
            sourceContext.Info.TileSize,
            connection.Direction,
            connection.Offset
        );

        _logger?.LogInformation(
            "Loading adjacent map: {MapId} at offset ({X}, {Y})",
            connection.MapId.Value,
            adjacentOffset.X,
            adjacentOffset.Y
        );

        try
        {
            // Load the map using deferred support (uses prepared data if available, else sync)
            Entity mapInfoEntity = _mapLoader.LoadMapWithDeferredSupport(
                world,
                connection.MapId,
                adjacentOffset
            );
            streaming.AddLoadedMap(connection.MapId, adjacentOffset);

            // Register with MapLifecycleManager for proper entity cleanup during unloading
            if (_lifecycleManager != null && mapInfoEntity.Has<MapInfo>())
            {
                MapInfo mapInfo = mapInfoEntity.Get<MapInfo>();
                HashSet<string> tilesetTextureIds = _mapLoader.GetLoadedTextureIds(
                    mapInfo.MapId
                );
                // Note: Sprites are loaded lazily, so pass empty set for streaming-loaded maps
                _lifecycleManager.RegisterMap(
                    mapInfo.MapId,
                    connection.MapId.Value,
                    tilesetTextureIds,
                    new HashSet<string>()
                );
                _logger?.LogDebug(
                    "Registered streaming map with lifecycle manager: {MapId} (RuntimeId: {RuntimeId})",
                    connection.MapId.Value,
                    mapInfo.MapId.Value
                );
            }

            // Mark invalidation as needed - will be batched at end of ProcessMapStreaming
            _invalidationNeeded = true;

            _logger?.LogDebug("Loaded adjacent map: {MapId}", connection.MapId.Value);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load adjacent map: {MapId}", connection.MapId.Value);
        }
    }

    /// <summary>
    ///     Gets dimensions for an adjacent map, with fallback to source dimensions.
    /// </summary>
    private (int Width, int Height)? GetAdjacentMapDimensions(
        GameMapId mapId,
        MapInfo sourceInfo
    )
    {
        // Get adjacent map definition
        MapEntity? adjacentMapDef = _mapDefinitionService.GetMap(mapId);
        if (adjacentMapDef == null)
        {
            _logger?.LogWarning("Adjacent map definition not found: {MapId}", mapId.Value);
            return null;
        }

        try
        {
            (int Width, int Height, int TileSize) dimensions = _mapLoader.GetMapDimensions(mapId);
            _logger?.LogDebug(
                "Adjacent map {MapId} dimensions: {Width}x{Height} tiles",
                mapId.Value,
                dimensions.Width,
                dimensions.Height
            );
            return (dimensions.Width, dimensions.Height);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(
                ex,
                "Failed to get dimensions for adjacent map: {MapId}, using source dimensions as fallback",
                mapId.Value
            );
            return (sourceInfo.Width, sourceInfo.Height);
        }
    }

    /// <summary>
    ///     Calculates the world offset for an adjacent map based on direction and connection offset.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <strong>Offset Calculations:</strong>
    ///     </para>
    ///     <list type="bullet">
    ///         <item>North: Y = sourceOrigin.Y - adjacentHeight, X shifted by connectionOffset</item>
    ///         <item>South: Y = sourceOrigin.Y + sourceHeight, X shifted by connectionOffset</item>
    ///         <item>East: X = sourceOrigin.X + sourceWidth, Y shifted by connectionOffset</item>
    ///         <item>West: X = sourceOrigin.X - adjacentWidth, Y shifted by connectionOffset</item>
    ///     </list>
    ///     <para>
    ///         Connection offset is in tiles. For N/S connections it shifts the adjacent map
    ///         horizontally (positive = right). For E/W it shifts vertically (positive = down).
    ///     </para>
    ///     <para>
    ///         CRITICAL: North and West use ADJACENT map dimensions because we need to place
    ///         the adjacent map's bottom/right edge against the source map's top/left edge.
    ///         South and East use SOURCE map dimensions because we place at the source's edge.
    ///     </para>
    /// </remarks>
    private Vector2 CalculateMapOffset(
        MapWorldPosition sourceMapWorldPos,
        int sourceWidthInTiles,
        int sourceHeightInTiles,
        int adjacentWidthInTiles,
        int adjacentHeightInTiles,
        int tileSize,
        Direction direction,
        int connectionOffset
    )
    {
        Vector2 sourceOrigin = sourceMapWorldPos.WorldOrigin;
        int sourceWidth = sourceWidthInTiles * tileSize;
        int sourceHeight = sourceHeightInTiles * tileSize;
        int adjacentWidth = adjacentWidthInTiles * tileSize;
        int adjacentHeight = adjacentHeightInTiles * tileSize;
        int offsetPixels = connectionOffset * tileSize;

        return direction switch
        {
            // North: Place adjacent map ABOVE source. Adjacent's BOTTOM edge touches source's TOP edge.
            // Y = sourceOrigin.Y - adjacentHeight (use adjacent height!)
            Direction.North => new Vector2(
                sourceOrigin.X + offsetPixels,
                sourceOrigin.Y - adjacentHeight
            ),
            // South: Place adjacent map BELOW source. Adjacent's TOP edge touches source's BOTTOM edge.
            // Y = sourceOrigin.Y + sourceHeight (use source height)
            Direction.South => new Vector2(
                sourceOrigin.X + offsetPixels,
                sourceOrigin.Y + sourceHeight
            ),
            // East: Place adjacent map RIGHT of source. Adjacent's LEFT edge touches source's RIGHT edge.
            // X = sourceOrigin.X + sourceWidth (use source width)
            Direction.East => new Vector2(
                sourceOrigin.X + sourceWidth,
                sourceOrigin.Y + offsetPixels
            ),
            // West: Place adjacent map LEFT of source. Adjacent's RIGHT edge touches source's LEFT edge.
            // X = sourceOrigin.X - adjacentWidth (use adjacent width!)
            Direction.West => new Vector2(
                sourceOrigin.X - adjacentWidth,
                sourceOrigin.Y + offsetPixels
            ),
            _ => sourceOrigin
        };
    }

    /// <summary>
    ///     Calculates the shortest distance from a point to the nearest point on a rectangle's boundary.
    ///     Returns 0 if the point is inside the rectangle.
    /// </summary>
    /// <param name="point">The point to measure from (player position).</param>
    /// <param name="rectOrigin">The top-left corner of the rectangle (map world origin).</param>
    /// <param name="rectWidth">Width of the rectangle in pixels.</param>
    /// <param name="rectHeight">Height of the rectangle in pixels.</param>
    /// <returns>Distance in pixels to the nearest boundary point, or 0 if inside.</returns>
    private float CalculateDistanceToMapBoundary(
        Vector2 point,
        Vector2 rectOrigin,
        int rectWidth,
        int rectHeight
    )
    {
        // Find the closest point on the rectangle to the given point
        float closestX = MathHelper.Clamp(point.X, rectOrigin.X, rectOrigin.X + rectWidth);
        float closestY = MathHelper.Clamp(point.Y, rectOrigin.Y, rectOrigin.Y + rectHeight);
        var closestPoint = new Vector2(closestX, closestY);

        // Return distance to that closest point
        return Vector2.Distance(point, closestPoint);
    }

    /// <summary>
    ///     Updates the current map if player has crossed into a different map.
    /// </summary>
    private void UpdateCurrentMap(
        World world,
        ref Position position,
        ref MapStreaming streaming,
        MapWorldPosition currentMapWorldPos,
        MapInfo currentMapInfo
    )
    {
        var playerPos = new Vector2(position.PixelX, position.PixelY);

        // Still in current map
        if (currentMapWorldPos.Contains(playerPos))
        {
            return;
        }

        // Find new map
        GameMapId? newMapId = TryFindContainingMap(
            playerPos,
            streaming,
            streaming.CurrentMapId
        );
        if (newMapId == null)
        {
            return;
        }

        Vector2? offset = streaming.GetMapOffset(newMapId);
        if (!offset.HasValue)
        {
            return;
        }

        UpdatePlayerMapPosition(
            ref position,
            ref streaming,
            newMapId,
            offset.Value,
            currentMapInfo.TileSize
        );
    }

    /// <summary>
    ///     Finds which loaded map contains the given position.
    /// </summary>
    private GameMapId? TryFindContainingMap(
        Vector2 playerPos,
        MapStreaming streaming,
        GameMapId currentMapId
    )
    {
        foreach (GameMapId loadedMapId in streaming.LoadedMaps)
        {
            if (loadedMapId.Value == currentMapId.Value)
            {
                continue;
            }

            Vector2? offset = streaming.GetMapOffset(loadedMapId);
            if (
                !offset.HasValue
                || !_mapInfoCache.TryGetValue(
                    loadedMapId.Value,
                    out (MapInfo Info, MapWorldPosition WorldPos, MapEntity? Definition) mapData
                )
            )
            {
                continue;
            }

            var bounds = new Rectangle(
                (int)offset.Value.X,
                (int)offset.Value.Y,
                mapData.Info.Width * mapData.Info.TileSize,
                mapData.Info.Height * mapData.Info.TileSize
            );

            if (bounds.Contains(playerPos))
            {
                return loadedMapId;
            }
        }

        return null;
    }

    /// <summary>
    ///     Updates player position when crossing into a new map.
    /// </summary>
    private void UpdatePlayerMapPosition(
        ref Position position,
        ref MapStreaming streaming,
        GameMapId newMapId,
        Vector2 newMapOffset,
        int tileSize
    )
    {
        GameMapId? oldMapId = position.MapId;
        int oldGridX = position.X;
        int oldGridY = position.Y;

        // Get old map name before updating
        string? oldMapName = null;
        if (_mapInfoCache.TryGetValue(streaming.CurrentMapId.Value,
                out (MapInfo Info, MapWorldPosition WorldPos, MapEntity? Definition) oldMapData))
        {
            oldMapName = oldMapData.Info.MapName;
        }

        // Update map reference
        if (
            _mapInfoCache.TryGetValue(
                newMapId.Value,
                out (MapInfo Info, MapWorldPosition WorldPos, MapEntity? Definition) newMapData
            )
        )
        {
            position.MapId = newMapData.Info.MapId;
        }

        streaming.CurrentMapId = newMapId;

        // Recalculate grid coordinates
        position.X = (int)((position.PixelX - newMapOffset.X) / tileSize);
        position.Y = (int)((position.PixelY - newMapOffset.Y) / tileSize);

        _logger?.LogInformation(
            "Player crossed map boundary: {OldMapId} -> {NewMapId} | Grid: ({OldX},{OldY}) -> ({NewX},{NewY})",
            oldMapId,
            position.MapId,
            oldGridX,
            oldGridY,
            position.X,
            position.Y
        );

        // Publish map transition event for subscribers (e.g., map popup display)
        PublishMapTransitionEvent(oldMapId, oldMapName, newMapData);
    }

    /// <summary>
    ///     Publishes a MapTransitionEvent when the player crosses a map boundary.
    /// </summary>
    private void PublishMapTransitionEvent(
        GameMapId? oldMapId,
        string? oldMapName,
        (MapInfo Info, MapWorldPosition WorldPos, MapEntity? Definition) newMapData
    )
    {
        if (_eventBus == null)
        {
            return;
        }

        // Get display name and region section from the new map
        string? displayName = null;
        string? regionSection = null;

        // Try to get components from the map entity
        if (World != null)
        {
            QueryDescription mapInfoQuery = QueryCache.Get<MapInfo>();
            World.Query(
                in mapInfoQuery,
                (Entity entity, ref MapInfo info) =>
                {
                    if (info.MapId == newMapData.Info.MapId)
                    {
                        // Get DisplayName if available
                        if (entity.Has<DisplayName>())
                        {
                            displayName = entity.Get<DisplayName>().Value;
                        }

                        // Get RegionSection if available
                        if (entity.Has<RegionSection>())
                        {
                            regionSection = entity.Get<RegionSection>().Value;
                        }
                    }
                }
            );
        }

        _eventBus.PublishPooled<MapTransitionEvent>(evt =>
        {
            evt.FromMapId = oldMapId;
            evt.FromMapName = oldMapName;
            evt.ToMapId = newMapData.Info.MapId;
            evt.ToMapName = displayName ?? newMapData.Info.MapName;
            evt.RegionName = regionSection;
        });

        _logger?.LogDebug(
            "Published MapTransitionEvent for boundary crossing: {OldMap} -> {NewMap} (Region: {Region})",
            oldMapName ?? "Unknown",
            displayName ?? newMapData.Info.MapName,
            regionSection ?? "None"
        );
    }

    /// <summary>
    ///     Unloads maps that are not connected to the current map.
    ///     Pokemon-style: only keep current map and its direct connections.
    ///     Uses capacity-based unloading to prevent excessive load/unload churn.
    /// </summary>
    private void UnloadDistantMaps(World world, ref Position position, ref MapStreaming streaming)
    {
        // Don't unload if we're under the capacity limit
        // This provides a buffer so recently-visited maps stay loaded
        // Prevents load/unload/load cycles when walking back and forth
        if (streaming.LoadedMaps.Count() <= MaxLoadedMaps)
        {
            // Use Trace level to avoid log spam - this check runs every frame
            _logger?.LogTrace(
                "Skipping unload - under capacity limit ({Count}/{Max} maps loaded)",
                streaming.LoadedMaps.Count(),
                MaxLoadedMaps
            );
            return;
        }

        // Create local copy to avoid ref struct capture
        GameMapId currentMapId = streaming.CurrentMapId;

        // Find the current map entity using ECS query
        Entity? currentMapEntity = null;
        QueryDescription query = QueryCache.Get<MapInfo>();
        world.Query(
            in query,
            (Entity entity, ref MapInfo mapInfo) =>
            {
                if (mapInfo.MapName == currentMapId.Value)
                {
                    currentMapEntity = entity;
                }
            }
        );

        if (!currentMapEntity.HasValue)
        {
            return;
        }

        Entity mapEntity = currentMapEntity.Value;

        // Build set of maps that should stay loaded:
        // 1. Current map
        // 2. All maps directly connected to current map (using connection components)
        var mapsToKeep = new HashSet<string> { currentMapId.Value };

        if (mapEntity.Has<NorthConnection>())
        {
            mapsToKeep.Add(mapEntity.Get<NorthConnection>().MapId.Value);
        }

        if (mapEntity.Has<SouthConnection>())
        {
            mapsToKeep.Add(mapEntity.Get<SouthConnection>().MapId.Value);
        }

        if (mapEntity.Has<EastConnection>())
        {
            mapsToKeep.Add(mapEntity.Get<EastConnection>().MapId.Value);
        }

        if (mapEntity.Has<WestConnection>())
        {
            mapsToKeep.Add(mapEntity.Get<WestConnection>().MapId.Value);
        }

        var mapsToUnload = new List<GameMapId>();
        var loadedMaps = new HashSet<GameMapId>(streaming.LoadedMaps);

        foreach (GameMapId loadedMapId in loadedMaps)
        {
            // Unload if not in the "keep" set
            if (!mapsToKeep.Contains(loadedMapId.Value))
            {
                mapsToUnload.Add(loadedMapId);
            }
        }

        // Unload maps not connected to current map
        foreach (GameMapId mapId in mapsToUnload)
        {
            _logger?.LogInformation(
                "Unloading map (not connected to current map): {MapId}",
                mapId.Value
            );

            try
            {
                // Get the MapRuntimeId from our cache to cleanup entities
                // NOTE: Cache is keyed by full ID (e.g., "base:map/hoenn/littleroot_town")
                if (
                    _mapInfoCache.TryGetValue(
                        mapId.Value,
                        out (
                        MapInfo Info,
                        MapWorldPosition WorldPos,
                        MapEntity? Definition
                        ) mapData
                    )
                )
                {
                    GameMapId gameMapId = mapData.Info.MapId;

                    // Try to use MapLifecycleManager for proper cleanup (registered maps)
                    if (_lifecycleManager != null)
                    {
                        _lifecycleManager.UnloadMap(gameMapId);
                        _logger?.LogInformation(
                            "Unloaded map via lifecycle manager: {MapId} (GameMapId: {GameMapId})",
                            mapId.Value,
                            gameMapId.Value
                        );
                    }
                    else
                    {
                        // Fallback: destroy entities directly if no lifecycle manager
                        int destroyedCount = DestroyMapEntities(world, gameMapId);
                        _logger?.LogInformation(
                            "Destroyed {Count} entities directly for map: {MapId} (GameMapId: {GameMapId})",
                            destroyedCount,
                            mapId.Value,
                            gameMapId.Value
                        );
                    }
                }
                else
                {
                    _logger?.LogWarning(
                        "Map not found in cache - cannot cleanup entities: {MapId}",
                        mapId.Value
                    );
                }

                // Mark invalidation as needed - will be batched at end of ProcessMapStreaming
                _invalidationNeeded = true;

                // Remove from streaming tracking
                streaming.RemoveLoadedMap(mapId);

                _logger?.LogDebug("Successfully unloaded map: {MapId}", mapId.Value);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to unload map: {MapId}", mapId.Value);
            }
        }
    }

    /// <summary>
    ///     Destroys all entities belonging to a specific map using the tile cache.
    ///     Used for cleaning up streaming-loaded maps which aren't registered with MapLifecycleManager.
    ///     Note: This is a fallback when MapLifecycleManager is not available. Prefer using
    ///     MapLifecycleManager.UnloadMap() for proper cleanup with tile cache.
    /// </summary>
    private int DestroyMapEntities(World world, GameMapId mapId)
    {
        int destroyedCount = 0;

        // Try to get tiles from the lifecycle manager's cache (if available)
        if (_lifecycleManager != null)
        {
            IReadOnlyList<Entity>? tiles = _lifecycleManager.GetMapTiles(mapId);
            if (tiles != null)
            {
                foreach (Entity tile in tiles)
                {
                    if (world.IsAlive(tile))
                    {
                        world.Destroy(tile);
                        destroyedCount++;
                    }
                }

                _lifecycleManager.ClearMapTileCache(mapId);
            }
        }

        // Find and destroy the MapInfo entity
        Entity? mapInfoEntity = null;
        QueryDescription mapInfoQuery = QueryCache.Get<MapInfo>();
        world.Query(
            in mapInfoQuery,
            (Entity entity, ref MapInfo info) =>
            {
                if (info.MapId == mapId)
                {
                    mapInfoEntity = entity;
                }
            }
        );

        if (mapInfoEntity.HasValue && world.IsAlive(mapInfoEntity.Value))
        {
            world.Destroy(mapInfoEntity.Value);
            destroyedCount++;
        }

        _logger?.LogDebug(
            "Destroyed {Count} entities for map {MapId}",
            destroyedCount,
            mapId.Value
        );

        return destroyedCount;
    }
}
