using Arch.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using PokeSharp.Engine.Common.Logging;
using PokeSharp.Engine.Systems.BulkOperations;
using PokeSharp.Game.Components.Common;
using PokeSharp.Game.Components.Maps;
using PokeSharp.Game.Components.Rendering;
using PokeSharp.Game.Components.Tiles;
using PokeSharp.Game.Systems;
using PokeSharp.Game.Data.MapLoading.Tiled.Tmx;
using PokeSharp.Game.Data.PropertyMapping;

namespace PokeSharp.Game.Data.MapLoading.Tiled;

/// <summary>
///     Handles processing of map layers and creation of tile entities.
///     Responsible for parsing layer data, determining elevation, and creating tile entities with bulk operations.
/// </summary>
public class LayerProcessor
{
    // Tiled flip flags (stored in upper 3 bits of GID)
    private const uint FLIPPED_HORIZONTALLY_FLAG = 0x80000000;
    private const uint FLIPPED_VERTICALLY_FLAG = 0x40000000;
    private const uint FLIPPED_DIAGONALLY_FLAG = 0x20000000;
    private const uint TILE_ID_MASK = 0x1FFFFFFF;

    private readonly PropertyMapperRegistry? _propertyMapperRegistry;
    private readonly ILogger<LayerProcessor>? _logger;

    public LayerProcessor(PropertyMapperRegistry? propertyMapperRegistry = null, ILogger<LayerProcessor>? logger = null)
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
                (layer.OffsetX != 0 || layer.OffsetY != 0)
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
            var tileGid = (int)(rawGid & TILE_ID_MASK);
            var flipH = (rawGid & FLIPPED_HORIZONTALLY_FLAG) != 0;
            var flipV = (rawGid & FLIPPED_VERTICALLY_FLAG) != 0;
            var flipD = (rawGid & FLIPPED_DIAGONALLY_FLAG) != 0;

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
            {
                tileElevation = Convert.ToByte(elevProp);
            }
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
    ///     - Standard layer (1) = elevation 3 (most tiles)
    ///     - Overhead layer (2+) = elevation 9 (tall structures)
    /// </summary>
    /// <remarks>
    ///     Layers can override this by setting a custom "elevation" property in Tiled.
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
        }

        // Fallback to index-based elevation
        return DetermineElevationFromIndex(layerIndex);
    }

    /// <summary>
    ///     Determines elevation from layer index.
    ///     Index 0 = Ground (0), Index 1 = Standard (3), Index 2+ = Overhead (9).
    /// </summary>
    private static byte DetermineElevationFromIndex(int layerIndex)
    {
        return layerIndex switch
        {
            0 => Elevation.Ground, // 0
            1 => Elevation.Default, // 3
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
            CalculateSourceRect(tileGid, tileset),
            flipH,
            flipV,
            flipD
        );
    }

    /// <summary>
    ///     Calculates the source rectangle for a tile in a tileset texture.
    /// </summary>
    private Rectangle CalculateSourceRect(int tileGid, TmxTileset tileset)
    {
        // Convert global ID to local ID
        var localTileId = tileGid - tileset.FirstGid;

        // Get tileset dimensions
        var tileWidth = tileset.TileWidth;
        var tileHeight = tileset.TileHeight;

        // Validate tile dimensions to prevent division by zero
        if (tileWidth <= 0 || tileHeight <= 0)
        {
            _logger?.LogError("Invalid tile dimensions: {Width}x{Height}", tileWidth, tileHeight);
            throw new InvalidOperationException(
                $"Invalid tile dimensions: {tileWidth}x{tileHeight}"
            );
        }

        if (tileset.Image == null || tileset.Image.Width <= 0 || tileset.Image.Height <= 0)
            throw new InvalidOperationException(
                $"Tileset '{tileset.Name ?? "unnamed"}' is missing valid image dimensions."
            );

        // Calculate tiles per row
        var tilesPerRow = CalculateTilesPerRow(tileset);

        // Calculate position in tileset
        var tileX = localTileId % tilesPerRow;
        var tileY = localTileId / tilesPerRow;

        // Calculate source rectangle with spacing and margin
        var spacing = Math.Max(0, tileset.Spacing);
        var margin = Math.Max(0, tileset.Margin);

        var sourceX = margin + tileX * (tileWidth + spacing);
        var sourceY = margin + tileY * (tileHeight + spacing);

        return new Rectangle(sourceX, sourceY, tileWidth, tileHeight);
    }

    /// <summary>
    ///     Calculates the number of tiles per row in a tileset.
    /// </summary>
    private static int CalculateTilesPerRow(TmxTileset tileset)
    {
        if (tileset.TileWidth <= 0)
            throw new InvalidOperationException(
                $"Tileset '{tileset.Name ?? "unnamed"}' has invalid tile width {tileset.TileWidth}."
            );

        if (tileset.Image == null || tileset.Image.Width <= 0)
            throw new InvalidOperationException(
                $"Tileset '{tileset.Name ?? "unnamed"}' is missing a valid image width."
            );

        var spacing = tileset.Spacing;
        var margin = tileset.Margin;

        if (spacing < 0)
            throw new InvalidOperationException(
                $"Tileset '{tileset.Name ?? "unnamed"}' has negative spacing value {spacing}."
            );
        if (margin < 0)
            throw new InvalidOperationException(
                $"Tileset '{tileset.Name ?? "unnamed"}' has negative margin value {margin}."
            );

        var usableWidth = tileset.Image.Width - margin * 2;
        if (usableWidth <= 0)
            throw new InvalidOperationException(
                $"Tileset '{tileset.Name ?? "unnamed"}' has unusable image width after margins."
            );

        var step = tileset.TileWidth + spacing;
        if (step <= 0)
            throw new InvalidOperationException(
                $"Tileset '{tileset.Name ?? "unnamed"}' has invalid step size {step}."
            );

        var tilesPerRow = (usableWidth + spacing) / step;
        if (tilesPerRow <= 0)
            throw new InvalidOperationException(
                $"Tileset '{tileset.Name ?? "unnamed"}' produced non-positive tiles-per-row."
            );

        return tilesPerRow;
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
        {
            if (tileGid >= tilesets[i].Tileset.FirstGid)
                return i;
        }

        return -1;
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

