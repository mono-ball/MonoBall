using Arch.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using PokeSharp.Engine.Common.Logging;
using PokeSharp.Engine.Core.Types;
using PokeSharp.Engine.Systems.BulkOperations;
using PokeSharp.Game.Components.Rendering;
using PokeSharp.Game.Components.Tiles;
using PokeSharp.Game.Data.MapLoading.Tiled.Services;
using PokeSharp.Game.Data.MapLoading.Tiled.Tmx;
using PokeSharp.Game.Data.MapLoading.Tiled.Utilities;
using PokeSharp.Game.Data.PropertyMapping;
using System.Text.Json;

namespace PokeSharp.Game.Data.MapLoading.Tiled.Processors;

/// <summary>
///     Handles processing of map layers and creation of tile entities.
///     Responsible for parsing layer data, determining elevation, and creating tile entities with bulk operations.
/// </summary>
public class LayerProcessor : ILayerProcessor
{
    private readonly ILogger<LayerProcessor>? _logger;

    private readonly PropertyMapperRegistry? _propertyMapperRegistry;

    public LayerProcessor(
        PropertyMapperRegistry? propertyMapperRegistry = null,
        ILogger<LayerProcessor>? logger = null
    )
    {
        _propertyMapperRegistry = propertyMapperRegistry;
        _logger = logger;
    }

    /// <summary>
    ///     Processes all tile layers and creates tile entities.
    /// </summary>
    /// <returns>Total number of tiles created</returns>
    public int ProcessLayers(
        World world,
        TmxDocument tmxDoc,
        int mapId,
        IReadOnlyList<LoadedTileset> tilesets
    )
    {
        var tilesCreated = 0;

        for (var layerIndex = 0; layerIndex < tmxDoc.Layers.Count; layerIndex++)
        {
            var layer = tmxDoc.Layers[layerIndex];
            if (layer?.Data == null)
                continue;

            // Determine elevation from layer name, custom properties, or index
            var elevation = DetermineElevation(layer, layerIndex);

            // Get layer offset for parallax scrolling (default to 0,0 if not set)
            var layerOffset =
                layer.OffsetX != 0 || layer.OffsetY != 0
                    ? new LayerOffset(layer.OffsetX, layer.OffsetY)
                    : (LayerOffset?)null;

            tilesCreated += CreateTileEntities(
                world,
                tmxDoc,
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
    ///     Creates tile entities for a single layer using bulk operations for performance.
    /// </summary>
    private int CreateTileEntities(
        World world,
        TmxDocument tmxDoc,
        int mapId,
        IReadOnlyList<LoadedTileset> tilesets,
        TmxLayer layer,
        byte elevation,
        LayerOffset? layerOffset
    )
    {
        // Collect tile data for bulk creation
        var tileDataList = new List<TileData>();

        for (var y = 0; y < tmxDoc.Height; y++)
        for (var x = 0; x < tmxDoc.Width; x++)
        {
            // Extract flip flags from GID (flat array: row-major order)
            var index = y * layer.Width + x;
            var rawGid = layer.Data![index];
            var tileGid = (int)(rawGid & TiledConstants.FlipFlags.TileIdMask);
            var flipH = (rawGid & TiledConstants.FlipFlags.HorizontalFlip) != 0;
            var flipV = (rawGid & TiledConstants.FlipFlags.VerticalFlip) != 0;
            var flipD = (rawGid & TiledConstants.FlipFlags.DiagonalFlip) != 0;

            if (tileGid == 0)
                continue; // Skip empty tiles

            var tilesetIndex = FindTilesetIndexForGid(tileGid, tilesets);
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
            return 0;

        // Use bulk operations for creating tiles
        var bulkOps = new BulkEntityOperations(world);

        // Create all tile entities with TilePosition and TileSprite components
        var tileEntities = bulkOps.CreateEntities(
            tileDataList.Count,
            i =>
            {
                var data = tileDataList[i];
                return new TilePosition(data.X, data.Y, mapId);
            },
            i =>
            {
                var data = tileDataList[i];
                var tileset = tilesets[data.TilesetIndex];
                return CreateTileSprite(data.TileGid, tileset, data.FlipH, data.FlipV, data.FlipD);
            }
        );

        // Process additional tile properties and components
        for (var i = 0; i < tileEntities.Length; i++)
        {
            var entity = tileEntities[i];
            var data = tileDataList[i];
            var tileset = tilesets[data.TilesetIndex].Tileset;

            // Get tile properties from tileset
            var localTileId = data.TileGid - tileset.FirstGid;
            Dictionary<string, object>? props = null;
            if (localTileId >= 0)
                tileset.TileProperties.TryGetValue(localTileId, out props);

            // Add Elevation component (Pokemon Emerald-style elevation system)
            // Check if tile has custom elevation property, otherwise use layer elevation
            var tileElevation = elevation;
            if (props != null && props.TryGetValue("elevation", out var elevProp))
                tileElevation = Convert.ToByte(elevProp);
            world.Add(entity, new Elevation(tileElevation));

            // Add LayerOffset if needed
            if (layerOffset.HasValue)
                world.Add(entity, layerOffset.Value);

            // Process additional tile properties (collision, ledges, encounters, etc.)
            ProcessTileProperties(world, entity, props);
        }

        return tileDataList.Count;
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
            var normalized = layer.Name.ToLowerInvariant();
            if (normalized.Contains("ground") || normalized.Contains("water"))
                return Elevation.Ground; // 0
            if (normalized.Contains("overhead") || normalized.Contains("roof"))
                return Elevation.Overhead; // 9
            if (normalized.Contains("bridge"))
                return Elevation.Bridge; // 6
            if (normalized.Contains("objects"))
                return 2; // Objects layer - render behind player
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
        var tileset = loadedTileset.Tileset;
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
            return;

        // Use PropertyMapperRegistry if available (new extensible approach)
        if (_propertyMapperRegistry != null)
        {
            var componentsAdded = _propertyMapperRegistry.MapAndAddAll(world, entity, props);
            if (componentsAdded > 0)
                _logger?.LogTrace(
                    "Applied {ComponentCount} components via property mappers to entity {EntityId}",
                    componentsAdded,
                    entity.Id
                );
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
            props.TryGetValue("terrain_type", out var terrainValue)
            && terrainValue is string terrainType
        )
        {
            var footstepSound = props.TryGetValue("footstep_sound", out var soundValue)
                ? soundValue.ToString() ?? ""
                : "";

            world.Add(entity, new TerrainType(terrainType, footstepSound));
        }

        // Add TileScript component if script path exists
        if (props.TryGetValue("script", out var scriptValue) && scriptValue is string scriptPath)
            world.Add(entity, new TileScript(scriptPath));
    }

    /// <summary>
    ///     Finds the tileset index for a given global tile ID.
    /// </summary>
    private static int FindTilesetIndexForGid(int tileGid, IReadOnlyList<LoadedTileset> tilesets)
    {
        for (var i = tilesets.Count - 1; i >= 0; i--)
            if (tileGid >= tilesets[i].Tileset.FirstGid)
                return i;

        return -1;
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
    ///     Parses a single connection property from Tiled.
    ///     Extracts direction, target map, and offset from the property value.
    /// </summary>
    /// <param name="propertyValue">The JSON property value containing connection data.</param>
    /// <returns>A MapConnection if parsing succeeds; otherwise, null.</returns>
    /// <remarks>
    ///     Expected property structure:
    ///     {
    ///         "direction": "North",
    ///         "map": "route101",
    ///         "offset": 0
    ///     }
    /// </remarks>
    private MapConnection? ParseConnectionProperty(object? propertyValue)
    {
        if (propertyValue == null)
            return null;

        try
        {
            // Handle both JsonElement and Dictionary<string, object> cases
            Dictionary<string, object>? connectionData = null;

            if (propertyValue is JsonElement jsonElement)
            {
                connectionData = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonElement.GetRawText());
            }
            else if (propertyValue is Dictionary<string, object> dict)
            {
                connectionData = dict;
            }

            if (connectionData == null)
                return null;

            // Extract direction
            if (!connectionData.TryGetValue("direction", out var directionObj))
                return null;

            var direction = ConnectionDirectionExtensions.Parse(directionObj?.ToString());
            if (!direction.HasValue)
            {
                _logger?.LogWarning("Invalid connection direction: {Direction}", directionObj);
                return null;
            }

            // Extract target map
            if (!connectionData.TryGetValue("map", out var mapObj) || mapObj == null)
                return null;

            var mapId = MapIdentifier.TryCreate(mapObj.ToString());
            if (mapId == null)
            {
                _logger?.LogWarning("Invalid connection map identifier: {Map}", mapObj);
                return null;
            }

            // Extract offset (optional, defaults to 0)
            var offset = 0;
            if (connectionData.TryGetValue("offset", out var offsetObj))
            {
                if (offsetObj is int intOffset)
                    offset = intOffset;
                else if (offsetObj is JsonElement offsetElement && offsetElement.ValueKind == JsonValueKind.Number)
                    offset = offsetElement.GetInt32();
                else if (int.TryParse(offsetObj?.ToString(), out var parsedOffset))
                    offset = parsedOffset;
            }

            return new MapConnection(direction.Value, mapId.Value, offset);
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
