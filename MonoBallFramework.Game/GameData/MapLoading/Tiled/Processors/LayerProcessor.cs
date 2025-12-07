using System.Text.Json;
using Arch.Core;
using Arch.Core.Extensions;
using Arch.Relationships;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Ecs.Components.Relationships;
using MonoBallFramework.Game.Ecs.Components.Rendering;
using MonoBallFramework.Game.Ecs.Components.Tiles;
using MonoBallFramework.Game.Engine.Common.Logging;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.Engine.Systems.BulkOperations;
using MonoBallFramework.Game.Engine.Systems.Pooling;
using MonoBallFramework.Game.GameData.MapLoading.Tiled.Services;
using MonoBallFramework.Game.GameData.MapLoading.Tiled.Tmx;
using MonoBallFramework.Game.GameData.MapLoading.Tiled.Utilities;
using MonoBallFramework.Game.GameData.PropertyMapping;

namespace MonoBallFramework.Game.GameData.MapLoading.Tiled.Processors;

/// <summary>
///     Handles processing of map layers and creation of tile entities.
///     Responsible for parsing layer data, determining elevation, and creating tile entities.
///     Supports entity pooling for improved performance when EntityPoolManager is provided.
/// </summary>
public class LayerProcessor : ILayerProcessor
{
    private const string TilePoolName = "tile";
    private readonly ILogger<LayerProcessor>? _logger;
    private readonly EntityPoolManager? _poolManager;
    private readonly PropertyMapperRegistry? _propertyMapperRegistry;

    public LayerProcessor(
        PropertyMapperRegistry? propertyMapperRegistry = null,
        EntityPoolManager? poolManager = null,
        ILogger<LayerProcessor>? logger = null
    )
    {
        _propertyMapperRegistry = propertyMapperRegistry;
        _poolManager = poolManager;
        _logger = logger;
    }

    /// <summary>
    ///     Processes all tile layers and creates tile entities.
    /// </summary>
    /// <returns>Total number of tiles created</returns>
    public int ProcessLayers(
        World world,
        TmxDocument tmxDoc,
        Entity mapInfoEntity,
        GameMapId mapId,
        IReadOnlyList<LoadedTileset> tilesets
    )
    {
        int tilesCreated = 0;

        for (int layerIndex = 0; layerIndex < tmxDoc.Layers.Count; layerIndex++)
        {
            TmxLayer? layer = tmxDoc.Layers[layerIndex];
            if (layer?.Data == null)
            {
                continue;
            }

            // Determine elevation from layer name, custom properties, or index
            byte elevation = DetermineElevation(layer, layerIndex);

            // Get layer offset for parallax scrolling (default to 0,0 if not set)
            LayerOffset? layerOffset =
                layer.OffsetX != 0 || layer.OffsetY != 0
                    ? new LayerOffset(layer.OffsetX, layer.OffsetY)
                    : null;

            tilesCreated += CreateTileEntities(
                world,
                tmxDoc,
                mapInfoEntity,
                mapId,
                tilesets,
                layer,
                elevation,
                layerOffset
            );
        }

        return tilesCreated;
    }

    /// <summary>
    ///     Parses map connection properties from Tiled custom properties.
    ///     Extracts connection_* properties that define how maps connect to each other.
    /// </summary>
    /// <param name="tmxDoc">The Tiled document to parse connections from.</param>
    /// <returns>A list of map connections parsed from the document.</returns>
    /// <remarks>
    ///     Connections are stored as custom properties with names like "connection_north", "connection_south", etc.
    ///     Each connection contains:
    ///     - direction: The direction of the connection (North, South, East, West)
    ///     - map: The identifier of the connected map
    ///     - offset: Optional alignment offset in tiles (default: 0)
    /// </remarks>
    public List<MapConnection> ParseMapConnections(TmxDocument tmxDoc)
    {
        var connections = new List<MapConnection>();

        // Tiled stores custom properties at the map level
        // We need to check if the document has custom properties exposed
        // For now, we'll return an empty list and implement this when the TmxDocument
        // structure is extended to include custom properties

        // TODO: Once TmxDocument.Properties is added, implement parsing like this:
        // foreach (var property in tmxDoc.Properties ?? Enumerable.Empty<TmxProperty>())
        // {
        //     if (property.Name?.StartsWith("connection_", StringComparison.OrdinalIgnoreCase) == true)
        //     {
        //         var connection = ParseConnectionProperty(property);
        //         if (connection.HasValue)
        //             connections.Add(connection.Value);
        //     }
        // }

        return connections;
    }

    /// <summary>
    ///     Creates tile entities for a single layer using bulk operations for performance.
    /// </summary>
    private int CreateTileEntities(
        World world,
        TmxDocument tmxDoc,
        Entity mapInfoEntity,
        GameMapId mapId,
        IReadOnlyList<LoadedTileset> tilesets,
        TmxLayer layer,
        byte elevation,
        LayerOffset? layerOffset
    )
    {
        // Collect tile data for bulk creation
        var tileDataList = new List<TileData>();

        for (int y = 0; y < tmxDoc.Height; y++)
        for (int x = 0; x < tmxDoc.Width; x++)
        {
            // Extract flip flags from GID (flat array: row-major order)
            int index = (y * layer.Width) + x;
            uint rawGid = layer.Data![index];
            int tileGid = (int)(rawGid & TiledConstants.FlipFlags.TileIdMask);
            bool flipH = (rawGid & TiledConstants.FlipFlags.HorizontalFlip) != 0;
            bool flipV = (rawGid & TiledConstants.FlipFlags.VerticalFlip) != 0;
            bool flipD = (rawGid & TiledConstants.FlipFlags.DiagonalFlip) != 0;

            if (tileGid == 0)
            {
                continue; // Skip empty tiles
            }

            int tilesetIndex = FindTilesetIndexForGid(tileGid, tilesets);
            if (tilesetIndex < 0)
            {
                _logger?.LogResourceNotFound(
                    "Tileset",
                    $"gid {tileGid} for layer '{layer.Name ?? "unnamed"}' in map {mapId}"
                );
                continue;
            }

            tileDataList.Add(
                new TileData
                {
                    X = x,
                    Y = y,
                    TileGid = tileGid,
                    FlipH = flipH,
                    FlipV = flipV,
                    FlipD = flipD,
                    TilesetIndex = tilesetIndex,
                }
            );
        }

        if (tileDataList.Count == 0)
        {
            return 0;
        }

        // Try to use entity pool for better performance (reuses entities instead of allocating)
        EntityPool? tilePool = null;
        bool usePooling = _poolManager != null && _poolManager.HasPool(TilePoolName);
        if (usePooling)
        {
            tilePool = _poolManager!.GetPool(TilePoolName);
        }

        Entity[] tileEntities;

        if (usePooling && tilePool != null)
        {
            // Acquire entities from pool
            tileEntities = AcquireEntitiesFromPool(world, tilePool, tileDataList, tilesets, mapId);
            _logger?.LogDebug(
                "Acquired {Count} tile entities from pool for map {MapId}",
                tileEntities.Length,
                mapId
            );
        }
        else
        {
            // Fallback: Use bulk operations for creating tiles (no pooling)
            var bulkOps = new BulkEntityOperations(world);
            tileEntities = bulkOps.CreateEntities(
                tileDataList.Count,
                i =>
                {
                    TileData data = tileDataList[i];
                    return new TilePosition(data.X, data.Y, mapId);
                },
                i =>
                {
                    TileData data = tileDataList[i];
                    LoadedTileset tileset = tilesets[data.TilesetIndex];
                    return CreateTileSprite(
                        data.TileGid,
                        tileset,
                        data.FlipH,
                        data.FlipV,
                        data.FlipD
                    );
                }
            );
        }

        // Process additional tile properties and components
        // Create relationship data once for all tiles
        var parentOfData = new ParentOf();

        for (int i = 0; i < tileEntities.Length; i++)
        {
            Entity entity = tileEntities[i];
            TileData data = tileDataList[i];
            TmxTileset tileset = tilesets[data.TilesetIndex].Tileset;

            // Add ParentOf relationship - map is parent of all tiles
            mapInfoEntity.AddRelationship(entity, parentOfData);

            // Get tile properties from tileset
            int localTileId = data.TileGid - tileset.FirstGid;
            Dictionary<string, object>? props = null;
            if (localTileId >= 0)
            {
                tileset.TileProperties.TryGetValue(localTileId, out props);
            }

            // Add/Set Elevation component (Pokemon Emerald-style elevation system)
            // Check if tile has custom elevation property, otherwise use layer elevation
            byte tileElevation = elevation;
            if (props != null && props.TryGetValue("elevation", out object? elevProp))
            {
                tileElevation = Convert.ToByte(elevProp);
            }

            // Use Set if entity already has component (pooled), Add if not
            if (entity.Has<Elevation>())
            {
                entity.Set(new Elevation(tileElevation));
            }
            else
            {
                world.Add(entity, new Elevation(tileElevation));
            }

            // Add LayerOffset if needed
            if (layerOffset.HasValue)
            {
                if (entity.Has<LayerOffset>())
                {
                    entity.Set(layerOffset.Value);
                }
                else
                {
                    world.Add(entity, layerOffset.Value);
                }
            }

            // Process additional tile properties (collision, ledges, encounters, etc.)
            ProcessTileProperties(world, entity, props);
        }

        return tileDataList.Count;
    }

    /// <summary>
    ///     Acquires entities from the tile pool and sets their components.
    /// </summary>
    private Entity[] AcquireEntitiesFromPool(
        World world,
        EntityPool pool,
        List<TileData> tileDataList,
        IReadOnlyList<LoadedTileset> tilesets,
        GameMapId mapId
    )
    {
        var entities = new Entity[tileDataList.Count];

        for (int i = 0; i < tileDataList.Count; i++)
        {
            TileData data = tileDataList[i];
            Entity entity = pool.Acquire();

            // Set TilePosition component
            var tilePos = new TilePosition(data.X, data.Y, mapId);
            if (entity.Has<TilePosition>())
            {
                entity.Set(tilePos);
            }
            else
            {
                world.Add(entity, tilePos);
            }

            // Set TileSprite component
            LoadedTileset tileset = tilesets[data.TilesetIndex];
            TileSprite tileSprite = CreateTileSprite(
                data.TileGid,
                tileset,
                data.FlipH,
                data.FlipV,
                data.FlipD
            );
            if (entity.Has<TileSprite>())
            {
                entity.Set(tileSprite);
            }
            else
            {
                world.Add(entity, tileSprite);
            }

            entities[i] = entity;
        }

        return entities;
    }

    /// <summary>
    ///     Determines elevation from layer name, custom properties, or index.
    ///     Follows Pokemon Emerald's elevation model:
    ///     - Ground layer (0) = elevation 0 (water, pits)
    ///     - Objects layer (1) = elevation 2 (renders behind player at elevation 3)
    ///     - Overhead layer (2+) = elevation 9 (tall structures, renders in front)
    /// </summary>
    /// <remarks>
    ///     Layers can override this by setting a custom "elevation" property in Tiled.
    ///     Objects layer uses elevation 2 so the player (elevation 3) renders in front.
    /// </remarks>
    private byte DetermineElevation(TmxLayer layer, int layerIndex)
    {
        // Try to determine from layer name (case-insensitive)
        if (!string.IsNullOrEmpty(layer.Name))
        {
            string normalized = layer.Name.ToLowerInvariant();
            if (normalized.Contains("ground") || normalized.Contains("water"))
            {
                return Elevation.Ground; // 0
            }

            if (normalized.Contains("overhead") || normalized.Contains("roof"))
            {
                return Elevation.Overhead; // 9
            }

            if (normalized.Contains("bridge"))
            {
                return Elevation.Bridge; // 6
            }

            if (normalized.Contains("objects"))
            {
                return 2; // Objects layer - render behind player
            }
        }

        // Fallback to index-based elevation
        return DetermineElevationFromIndex(layerIndex);
    }

    /// <summary>
    ///     Determines elevation from layer index.
    ///     Index 0 = Ground (0), Index 1 = Objects (2, renders behind player), Index 2+ = Overhead (9).
    /// </summary>
    /// <remarks>
    ///     Objects layer uses elevation 2 (instead of 3) so it renders behind the player (elevation 3).
    ///     This allows the player to walk in front of objects when moving up.
    /// </remarks>
    private static byte DetermineElevationFromIndex(int layerIndex)
    {
        return layerIndex switch
        {
            0 => Elevation.Ground, // 0
            1 => 2, // Objects layer - render behind player (elevation 3)
            _ => Elevation.Overhead, // 9 (2+)
        };
    }

    /// <summary>
    ///     Creates a TileSprite component with flip flags applied.
    /// </summary>
    private TileSprite CreateTileSprite(
        int tileGid,
        LoadedTileset loadedTileset,
        bool flipH,
        bool flipV,
        bool flipD
    )
    {
        TmxTileset tileset = loadedTileset.Tileset;
        return new TileSprite(
            loadedTileset.TilesetId,
            tileGid,
            TilesetUtilities.CalculateSourceRect(tileGid, tileset),
            flipH,
            flipV,
            flipD
        );
    }

    /// <summary>
    ///     Processes tile properties and adds appropriate components to the entity.
    ///     Uses PropertyMapperRegistry if available, otherwise falls back to legacy mapping.
    /// </summary>
    private void ProcessTileProperties(
        World world,
        Entity entity,
        Dictionary<string, object>? props
    )
    {
        if (props == null)
        {
            return;
        }

        // Use PropertyMapperRegistry if available (new extensible approach)
        if (_propertyMapperRegistry != null)
        {
            int componentsAdded = _propertyMapperRegistry.MapAndAddAll(world, entity, props);
            if (componentsAdded > 0)
            {
                _logger?.LogTrace(
                    "Applied {ComponentCount} components via property mappers to entity {EntityId}",
                    componentsAdded,
                    entity.Id
                );
            }
        }
        else
        {
            // Legacy fallback: hardcoded property mapping for backward compatibility
            ProcessTilePropertiesLegacy(world, entity, props);
        }
    }

    /// <summary>
    ///     Legacy hardcoded property mapping for backward compatibility.
    ///     Used when PropertyMapperRegistry is not provided.
    ///     Adds TerrainType and TileScript components if specified in properties.
    /// </summary>
    private void ProcessTilePropertiesLegacy(
        World world,
        Entity entity,
        Dictionary<string, object> props
    )
    {
        // Add TerrainType component if terrain type exists
        if (
            props.TryGetValue("terrain_type", out object? terrainValue)
            && terrainValue is string terrainType
        )
        {
            string footstepSound = props.TryGetValue("footstep_sound", out object? soundValue)
                ? soundValue.ToString() ?? ""
                : "";

            world.Add(entity, new TerrainType(terrainType, footstepSound));
        }

        // Add TileScript component if script path exists
        if (
            props.TryGetValue("script", out object? scriptValue) && scriptValue is string scriptPath
        )
        {
            world.Add(entity, new TileScript(scriptPath));
        }
    }

    /// <summary>
    ///     Finds the tileset index for a given global tile ID.
    /// </summary>
    private static int FindTilesetIndexForGid(int tileGid, IReadOnlyList<LoadedTileset> tilesets)
    {
        for (int i = tilesets.Count - 1; i >= 0; i--)
        {
            if (tileGid >= tilesets[i].Tileset.FirstGid)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    ///     Parses a single connection property from Tiled.
    ///     Extracts direction, target map, and offset from the property value.
    /// </summary>
    /// <param name="propertyValue">The JSON property value containing connection data.</param>
    /// <returns>A MapConnection if parsing succeeds; otherwise, null.</returns>
    /// <remarks>
    ///     Expected property structure:
    ///     {
    ///     "direction": "North",
    ///     "map": "route101",
    ///     "offset": 0
    ///     }
    /// </remarks>
    private MapConnection? ParseConnectionProperty(object? propertyValue)
    {
        if (propertyValue == null)
        {
            return null;
        }

        try
        {
            // Handle both JsonElement and Dictionary<string, object> cases
            Dictionary<string, object>? connectionData = null;

            if (propertyValue is JsonElement jsonElement)
            {
                connectionData = JsonSerializer.Deserialize<Dictionary<string, object>>(
                    jsonElement.GetRawText()
                );
            }
            else if (propertyValue is Dictionary<string, object> dict)
            {
                connectionData = dict;
            }

            if (connectionData == null)
            {
                return null;
            }

            // Extract direction
            if (!connectionData.TryGetValue("direction", out object? directionObj))
            {
                return null;
            }

            ConnectionDirection? direction = ConnectionDirectionExtensions.Parse(
                directionObj?.ToString()
            );
            if (!direction.HasValue)
            {
                _logger?.LogWarning("Invalid connection direction: {Direction}", directionObj);
                return null;
            }

            // Extract target map
            if (!connectionData.TryGetValue("map", out object? mapObj) || mapObj == null)
            {
                return null;
            }

            string mapString = mapObj.ToString() ?? string.Empty;
            if (string.IsNullOrEmpty(mapString))
            {
                _logger?.LogWarning("Invalid connection map identifier: {Map}", mapObj);
                return null;
            }

            // TryCreate handles both full format (base:map:hoenn/route_101) and legacy short names
            GameMapId? mapId = GameMapId.TryCreate(mapString);
            if (mapId == null)
            {
                _logger?.LogWarning("Invalid connection map ID format: {Map}", mapString);
                return null;
            }

            // Extract offset (optional, defaults to 0)
            int offset = 0;
            if (connectionData.TryGetValue("offset", out object? offsetObj))
            {
                if (offsetObj is int intOffset)
                {
                    offset = intOffset;
                }
                else if (
                    offsetObj is JsonElement offsetElement
                    && offsetElement.ValueKind == JsonValueKind.Number
                )
                {
                    offset = offsetElement.GetInt32();
                }
                else if (int.TryParse(offsetObj?.ToString(), out int parsedOffset))
                {
                    offset = parsedOffset;
                }
            }

            return new MapConnection(direction.Value, mapId, offset);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to parse connection property: {Value}", propertyValue);
            return null;
        }
    }

    /// <summary>
    ///     Temporary structure to hold tile data before bulk creation.
    /// </summary>
    private struct TileData
    {
        public int X;
        public int Y;
        public int TileGid;
        public bool FlipH;
        public bool FlipV;
        public bool FlipD;
        public int TilesetIndex;
    }
}
