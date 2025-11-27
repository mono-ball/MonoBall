using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using PokeSharp.Engine.Common.Logging;
using PokeSharp.Engine.Core.Systems;
using PokeSharp.Engine.Core.Types;
using PokeSharp.Game.Components;
using PokeSharp.Game.Components.Maps;
using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Components.Player;
using PokeSharp.Game.Data.Entities;
using PokeSharp.Game.Data.MapLoading.Tiled.Core;
using PokeSharp.Game.Data.Services;

namespace PokeSharp.Game.Systems;

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
/// </remarks>
public class MapStreamingSystem : SystemBase, IUpdateSystem
{
    private readonly ILogger<MapStreamingSystem>? _logger;
    private readonly MapLoader _mapLoader;
    private readonly MapDefinitionService _mapDefinitionService;

    // Cached queries for performance
    private QueryDescription _playerQuery;
    private QueryDescription _mapInfoQuery;

    // Map info cache to avoid nested queries (O(N×M) -> O(N))
    private readonly Dictionary<string, (MapInfo Info, MapWorldPosition WorldPos, MapDefinition? Definition)> _mapInfoCache = new(10);

    /// <summary>
    ///     Creates a new MapStreamingSystem with required services.
    /// </summary>
    /// <param name="mapLoader">MapLoader service for loading/unloading maps.</param>
    /// <param name="mapDefinitionService">Service for accessing map definitions and connections.</param>
    /// <param name="logger">Optional logger for debugging streaming operations.</param>
    public MapStreamingSystem(
        MapLoader mapLoader,
        MapDefinitionService mapDefinitionService,
        ILogger<MapStreamingSystem>? logger = null)
    {
        _mapLoader = mapLoader ?? throw new ArgumentNullException(nameof(mapLoader));
        _mapDefinitionService = mapDefinitionService ?? throw new ArgumentNullException(nameof(mapDefinitionService));
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
        _playerQuery = new QueryDescription()
            .WithAll<Player, Position, MapStreaming>();

        // Query for map info entities with world position
        _mapInfoQuery = new QueryDescription()
            .WithAll<MapInfo, MapWorldPosition>();
    }

    /// <inheritdoc />
    public override void Update(World world, float deltaTime)
    {
        if (!Enabled)
            return;

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
    ///     Updates the map info cache with current map data.
    ///     Called once per frame to avoid nested queries during streaming checks.
    /// </summary>
    private void UpdateMapInfoCache(World world)
    {
        _mapInfoCache.Clear();
        world.Query(in _mapInfoQuery, (ref MapInfo info, ref MapWorldPosition pos) =>
        {
            _mapInfoCache[info.MapName] = (info, pos, null);
        });
    }

    /// <summary>
    ///     Processes map streaming for a single player.
    ///     Checks boundaries and loads/unloads maps as needed.
    /// </summary>
    private void ProcessMapStreaming(
        World world,
        Entity playerEntity,
        ref Position position,
        ref MapStreaming streaming)
    {
        // Create local copy to avoid ref struct capture in lambda
        var streamingCopy = streaming;

        // Try to build the map context from cached data
        var context = TryGetMapContext(streamingCopy.CurrentMapId);
        if (!context.HasValue)
        {
            _logger?.LogError("Current map not found for streaming: {MapId}", streamingCopy.CurrentMapId.Value);
            return;
        }

        // Load ALL connected maps immediately (Pokemon-style)
        LoadAllConnections(world, ref streamingCopy, context.Value);

        // Update current map if player has crossed boundary
        UpdateCurrentMap(world, ref position, ref streamingCopy, context.Value.WorldPosition, context.Value.Info);

        // Unload distant maps
        UnloadDistantMaps(world, ref position, ref streamingCopy);

        // Apply changes back to the ref parameter
        streaming = streamingCopy;
    }

    /// <summary>
    ///     Tries to build a MapLoadContext from cached data for a given map.
    /// </summary>
    private MapLoadContext? TryGetMapContext(MapIdentifier mapId)
    {
        if (!_mapInfoCache.TryGetValue(mapId.Value, out var mapData))
            return null;

        var mapDef = _mapDefinitionService.GetMap(mapId);
        if (mapDef == null)
            return null;

        return new MapLoadContext(mapDef, mapData.Info, mapData.WorldPos);
    }

    /// <summary>
    ///     Loads all connected maps for the given context.
    /// </summary>
    private void LoadAllConnections(
        World world,
        ref MapStreaming streaming,
        MapLoadContext context)
    {
        foreach (var connection in context.GetAllConnections())
        {
            LoadAdjacentMapIfNeeded(world, ref streaming, context, connection);
        }
    }

    /// <summary>
    ///     Loads an adjacent map if it's connected and not already loaded.
    ///     Pokemon-style: all connections are preloaded immediately.
    /// </summary>
    /// <param name="sourceContext">Context for the source map (dimensions, world position).</param>
    /// <param name="connection">Connection info (map ID, offset, direction).</param>
    private void LoadAdjacentMapIfNeeded(
        World world,
        ref MapStreaming streaming,
        MapLoadContext sourceContext,
        ConnectionInfo connection)
    {
        // Already loaded
        if (streaming.IsMapLoaded(connection.MapId))
            return;

        _logger?.LogDebug(
            "Loading connected map in {Direction} direction: {MapId} (offset: {Offset} tiles)",
            connection.Direction,
            connection.MapId.Value,
            connection.Offset
        );

        // Get adjacent map dimensions (needed for correct offset calculation)
        var adjacentDimensions = GetAdjacentMapDimensions(connection.MapId, sourceContext.Info);
        if (!adjacentDimensions.HasValue)
            return;

        var (adjacentWidth, adjacentHeight) = adjacentDimensions.Value;

        // Calculate world offset for the adjacent map
        var adjacentOffset = CalculateMapOffset(
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
            // Load the map at the calculated world offset
            _mapLoader.LoadMapAtOffset(world, connection.MapId, adjacentOffset);
            streaming.AddLoadedMap(connection.MapId, adjacentOffset);

            _logger?.LogInformation("Successfully loaded adjacent map: {MapId}", connection.MapId.Value);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load adjacent map: {MapId}", connection.MapId.Value);
        }
    }

    /// <summary>
    ///     Gets dimensions for an adjacent map, with fallback to source dimensions.
    /// </summary>
    private (int Width, int Height)? GetAdjacentMapDimensions(MapIdentifier mapId, MapInfo sourceInfo)
    {
        // Get adjacent map definition
        var adjacentMapDef = _mapDefinitionService.GetMap(mapId);
        if (adjacentMapDef == null)
        {
            _logger?.LogWarning("Adjacent map definition not found: {MapId}", mapId.Value);
            return null;
        }

        try
        {
            var dimensions = _mapLoader.GetMapDimensions(mapId);
            _logger?.LogDebug(
                "Adjacent map {MapId} dimensions: {Width}x{Height} tiles",
                mapId.Value, dimensions.Width, dimensions.Height);
            return (dimensions.Width, dimensions.Height);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex,
                "Failed to get dimensions for adjacent map: {MapId}, using source dimensions as fallback",
                mapId.Value);
            return (sourceInfo.Width, sourceInfo.Height);
        }
    }

    /// <summary>
    ///     Calculates the world offset for an adjacent map based on direction and connection offset.
    /// </summary>
    /// <remarks>
    ///     <para><strong>Offset Calculations:</strong></para>
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
        int connectionOffset)
    {
        var sourceOrigin = sourceMapWorldPos.WorldOrigin;
        var sourceWidth = sourceWidthInTiles * tileSize;
        var sourceHeight = sourceHeightInTiles * tileSize;
        var adjacentWidth = adjacentWidthInTiles * tileSize;
        var adjacentHeight = adjacentHeightInTiles * tileSize;
        var offsetPixels = connectionOffset * tileSize;

        return direction switch
        {
            // North: Place adjacent map ABOVE source. Adjacent's BOTTOM edge touches source's TOP edge.
            // Y = sourceOrigin.Y - adjacentHeight (use adjacent height!)
            Direction.North => new Vector2(sourceOrigin.X + offsetPixels, sourceOrigin.Y - adjacentHeight),
            // South: Place adjacent map BELOW source. Adjacent's TOP edge touches source's BOTTOM edge.
            // Y = sourceOrigin.Y + sourceHeight (use source height)
            Direction.South => new Vector2(sourceOrigin.X + offsetPixels, sourceOrigin.Y + sourceHeight),
            // East: Place adjacent map RIGHT of source. Adjacent's LEFT edge touches source's RIGHT edge.
            // X = sourceOrigin.X + sourceWidth (use source width)
            Direction.East => new Vector2(sourceOrigin.X + sourceWidth, sourceOrigin.Y + offsetPixels),
            // West: Place adjacent map LEFT of source. Adjacent's RIGHT edge touches source's LEFT edge.
            // X = sourceOrigin.X - adjacentWidth (use adjacent width!)
            Direction.West => new Vector2(sourceOrigin.X - adjacentWidth, sourceOrigin.Y + offsetPixels),
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
        int rectHeight)
    {
        // Find the closest point on the rectangle to the given point
        var closestX = MathHelper.Clamp(point.X, rectOrigin.X, rectOrigin.X + rectWidth);
        var closestY = MathHelper.Clamp(point.Y, rectOrigin.Y, rectOrigin.Y + rectHeight);
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
        MapInfo currentMapInfo)
    {
        var playerPos = new Vector2(position.PixelX, position.PixelY);

        // Still in current map
        if (currentMapWorldPos.Contains(playerPos))
            return;

        // Find new map
        var newMapId = TryFindContainingMap(playerPos, streaming, streaming.CurrentMapId);
        if (!newMapId.HasValue)
            return;

        var offset = streaming.GetMapOffset(newMapId.Value);
        if (!offset.HasValue)
            return;

        UpdatePlayerMapPosition(ref position, ref streaming, newMapId.Value, offset.Value, currentMapInfo.TileSize);
    }

    /// <summary>
    ///     Finds which loaded map contains the given position.
    /// </summary>
    private MapIdentifier? TryFindContainingMap(
        Vector2 playerPos,
        MapStreaming streaming,
        MapIdentifier currentMapId)
    {
        foreach (var loadedMapId in streaming.LoadedMaps)
        {
            if (loadedMapId.Value == currentMapId.Value)
                continue;

            var offset = streaming.GetMapOffset(loadedMapId);
            if (!offset.HasValue || !_mapInfoCache.TryGetValue(loadedMapId.Value, out var mapData))
                continue;

            var bounds = new Rectangle(
                (int)offset.Value.X,
                (int)offset.Value.Y,
                mapData.Info.Width * mapData.Info.TileSize,
                mapData.Info.Height * mapData.Info.TileSize);

            if (bounds.Contains(playerPos))
                return loadedMapId;
        }
        return null;
    }

    /// <summary>
    ///     Updates player position when crossing into a new map.
    /// </summary>
    private void UpdatePlayerMapPosition(
        ref Position position,
        ref MapStreaming streaming,
        MapIdentifier newMapId,
        Vector2 newMapOffset,
        int tileSize)
    {
        var oldMapId = position.MapId;
        var oldGridX = position.X;
        var oldGridY = position.Y;

        // Update map reference
        if (_mapInfoCache.TryGetValue(newMapId.Value, out var newMapData))
        {
            position.MapId = newMapData.Info.MapId;
        }
        streaming.CurrentMapId = newMapId;

        // Recalculate grid coordinates
        position.X = (int)((position.PixelX - newMapOffset.X) / tileSize);
        position.Y = (int)((position.PixelY - newMapOffset.Y) / tileSize);

        _logger?.LogInformation(
            "Player crossed map boundary: {OldMapId} -> {NewMapId} | Grid: ({OldX},{OldY}) -> ({NewX},{NewY})",
            oldMapId, position.MapId, oldGridX, oldGridY, position.X, position.Y);
    }

    /// <summary>
    ///     Unloads maps that are not connected to the current map.
    ///     Pokemon-style: only keep current map and its direct connections.
    /// </summary>
    private void UnloadDistantMaps(
        World world,
        ref Position position,
        ref MapStreaming streaming)
    {
        // Create local copy to avoid ref struct capture
        var currentMapId = streaming.CurrentMapId;

        // Get current map definition to check connections
        var currentMapDef = _mapDefinitionService.GetMap(currentMapId);
        if (currentMapDef == null)
            return;

        // Build set of maps that should stay loaded:
        // 1. Current map
        // 2. All maps directly connected to current map
        var mapsToKeep = new HashSet<string> { currentMapId.Value };

        if (currentMapDef.NorthMapId != null)
            mapsToKeep.Add(currentMapDef.NorthMapId.Value.Value);
        if (currentMapDef.SouthMapId != null)
            mapsToKeep.Add(currentMapDef.SouthMapId.Value.Value);
        if (currentMapDef.EastMapId != null)
            mapsToKeep.Add(currentMapDef.EastMapId.Value.Value);
        if (currentMapDef.WestMapId != null)
            mapsToKeep.Add(currentMapDef.WestMapId.Value.Value);

        var mapsToUnload = new List<MapIdentifier>();
        var loadedMaps = new HashSet<MapIdentifier>(streaming.LoadedMaps);

        foreach (var loadedMapId in loadedMaps)
        {
            // Unload if not in the "keep" set
            if (!mapsToKeep.Contains(loadedMapId.Value))
            {
                mapsToUnload.Add(loadedMapId);
            }
        }

        // Unload maps not connected to current map
        foreach (var mapId in mapsToUnload)
        {
            _logger?.LogInformation(
                "Unloading map (not connected to current map): {MapId}",
                mapId.Value
            );

            try
            {
                // TODO: Implement map unloading in MapLoader
                // For now, just remove from tracking
                streaming.RemoveLoadedMap(mapId);

                _logger?.LogDebug("Successfully unloaded map: {MapId}", mapId.Value);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to unload map: {MapId}", mapId.Value);
            }
        }
    }
}
