using Arch.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using PokeSharp.Core.Components.Common;
using PokeSharp.Core.Components.Maps;
using PokeSharp.Core.Components.Movement;
using PokeSharp.Core.Components.NPCs;
using PokeSharp.Core.Components.Rendering;
using PokeSharp.Core.Components.Tiles;
using PokeSharp.Core.Factories;
using PokeSharp.Core.Logging;
using PokeSharp.Rendering.Assets;
using PokeSharp.Rendering.Loaders.Tmx;

namespace PokeSharp.Rendering.Loaders;

/// <summary>
///     Loads Tiled maps and converts them to ECS components.
///     Supports template-based tile creation when EntityFactoryService is provided.
/// </summary>
public class MapLoader(
    IAssetProvider assetManager,
    IEntityFactoryService? entityFactory = null,
    ILogger<MapLoader>? logger = null
)
{
    // Tiled flip flags (stored in upper 3 bits of GID)
    private const uint FLIPPED_HORIZONTALLY_FLAG = 0x80000000;
    private const uint FLIPPED_VERTICALLY_FLAG = 0x40000000;
    private const uint FLIPPED_DIAGONALLY_FLAG = 0x20000000;
    private const uint TILE_ID_MASK = 0x1FFFFFFF;

    private readonly IAssetProvider _assetManager =
        assetManager ?? throw new ArgumentNullException(nameof(assetManager));

    private readonly IEntityFactoryService? _entityFactory = entityFactory;
    private readonly ILogger<MapLoader>? _logger = logger;
    private readonly Dictionary<string, int> _mapNameToId = new();
    private int _nextMapId;

    /// <summary>
    ///     Loads a complete map by creating tile entities for each non-empty tile.
    ///     This is the new ECS-based approach where every tile is an entity with components.
    ///     Also creates a MapInfo entity to store map metadata.
    /// </summary>
    /// <param name="world">The ECS world to create entities in.</param>
    /// <param name="mapPath">Path to the Tiled JSON map file.</param>
    /// <returns>The MapInfo entity containing map metadata.</returns>
    public Entity LoadMapEntities(World world, string mapPath)
    {
        // Use scoped logging to group all map loading operations
        using (_logger?.BeginScope($"Loading:{Path.GetFileNameWithoutExtension(mapPath)}"))
        {
            return LoadMapEntitiesInternal(world, mapPath);
        }
    }

    private Entity LoadMapEntitiesInternal(World world, string mapPath)
    {
        var tmxDoc = TiledMapLoader.Load(mapPath);
        var mapId = GetMapId(mapPath);
        var tileset =
            tmxDoc.Tilesets.FirstOrDefault()
            ?? throw new InvalidOperationException($"Map '{mapPath}' has no tilesets");

        var tilesetId = ExtractTilesetId(tileset, mapPath);

        // Ensure tileset texture is loaded
        if (!_assetManager.HasTexture(tilesetId))
            LoadTilesetTexture(tileset, mapPath, tilesetId);

        var tilesCreated = 0;
        var totalLayerCount = tmxDoc.Layers.Count + tmxDoc.ImageLayers.Count;

        // Create entity for each non-empty tile across all layers
        for (var layerIndex = 0; layerIndex < tmxDoc.Layers.Count; layerIndex++)
        {
            var layer = tmxDoc.Layers[layerIndex];
            if (layer?.Data == null)
                continue;

            // Determine TileLayer from layer name or index
            var tileLayer = DetermineTileLayer(layer.Name, layerIndex);

            // Get layer offset for parallax scrolling (default to 0,0 if not set)
            var layerOffset = (layer.OffsetX != 0 || layer.OffsetY != 0)
                ? new LayerOffset(layer.OffsetX, layer.OffsetY)
                : (LayerOffset?)null;

            for (var y = 0; y < tmxDoc.Height; y++)
            for (var x = 0; x < tmxDoc.Width; x++)
            {
                // Extract flip flags from GID
                var rawGid = (uint)layer.Data[y, x];
                var tileGid = (int)(rawGid & TILE_ID_MASK);
                var flipH = (rawGid & FLIPPED_HORIZONTALLY_FLAG) != 0;
                var flipV = (rawGid & FLIPPED_VERTICALLY_FLAG) != 0;
                var flipD = (rawGid & FLIPPED_DIAGONALLY_FLAG) != 0;

                if (tileGid == 0)
                    continue; // Skip empty tiles

                CreateTileEntity(
                    world,
                    x,
                    y,
                    mapId,
                    tileGid,
                    tileset,
                    tileLayer,
                    layerOffset,
                    flipH,
                    flipV,
                    flipD
                );
                tilesCreated++;
            }
        }

        // Create MapInfo entity for map metadata
        var mapName = Path.GetFileNameWithoutExtension(mapPath);
        var mapInfo = new MapInfo(mapId, mapName, tmxDoc.Width, tmxDoc.Height, tmxDoc.TileWidth);
        var mapInfoEntity = world.Create(mapInfo);

        // Create TilesetInfo entity for tileset metadata
        var tilesetInfo = new TilesetInfo(
            tilesetId,
            tileset.FirstGid,
            tileset.TileWidth,
            tileset.TileHeight,
            tileset.Image?.Width ?? 256,
            tileset.Image?.Height ?? 256
        );
        var tilesetEntity = world.Create(tilesetInfo);

        // Create animated tile entities
        var animatedTilesCreated = CreateAnimatedTileEntities(world, tmxDoc, tileset);

        // Create image layer entities
        var imageLayersCreated = CreateImageLayerEntities(world, tmxDoc, mapPath, totalLayerCount);

        // Spawn entities from map objects (NPCs, items, etc.)
        var objectsCreated = SpawnMapObjects(world, tmxDoc, mapId, tmxDoc.TileHeight);

        _logger?.LogMapLoaded(mapName, tmxDoc.Width, tmxDoc.Height, tilesCreated, objectsCreated);
        if (imageLayersCreated > 0)
        {
            _logger?.LogDebug(
                "[dim]Image Layers:[/] [magenta]{ImageLayerCount}[/]",
                imageLayersCreated
            );
        }
        _logger?.LogDebug(
            "[dim]MapId:[/] [grey]{MapId}[/] [dim]|[/] [dim]Animated:[/] [yellow]{AnimatedCount}[/] [dim]|[/] [dim]Tileset:[/] [cyan]{TilesetId}[/]",
            mapId,
            animatedTilesCreated,
            tilesetId
        );

        return mapInfoEntity;
    }

    private int CreateAnimatedTileEntities(World world, TmxDocument tmxDoc, TmxTileset tileset)
    {
        if (tileset.Animations.Count == 0)
            return 0;

        var created = 0;

        foreach (var kvp in tileset.Animations)
        {
            var localTileId = kvp.Key;
            var animation = kvp.Value;

            // Convert local tile ID to global tile ID
            var globalTileId = tileset.FirstGid + localTileId;

            // Convert frame local IDs to global IDs
            var globalFrameIds = animation
                .FrameTileIds.Select(id => tileset.FirstGid + id)
                .ToArray();

            // Create AnimatedTile component
            var animatedTile = new AnimatedTile(
                globalTileId,
                globalFrameIds,
                animation.FrameDurations
            );

            // Find all tile entities with this tile ID and add AnimatedTile component
            var tileQuery = new QueryDescription().WithAll<TileSprite>();
            world.Query(
                in tileQuery,
                (Entity entity, ref TileSprite sprite) =>
                {
                    if (sprite.TileGid == globalTileId)
                    {
                        world.Add(entity, animatedTile);
                        created++;
                    }
                }
            );
        }

        return created;
    }

    // Obsolete methods removed - they referenced TileMap and TileProperties components which no longer exist
    // Use LoadMapEntities() instead

    private static string ExtractTilesetId(TmxTileset tileset, string mapPath)
    {
        // If tileset has an image, use the image filename as ID
        if (tileset.Image != null && !string.IsNullOrEmpty(tileset.Image.Source))
            return Path.GetFileNameWithoutExtension(tileset.Image.Source);

        // Fallback to tileset name
        return tileset.Name ?? "default-tileset";
    }

    private void LoadTilesetTexture(TmxTileset tileset, string mapPath, string tilesetId)
    {
        if (tileset.Image == null || string.IsNullOrEmpty(tileset.Image.Source))
            throw new InvalidOperationException("Tileset has no image source");

        // Resolve relative path from map directory
        var mapDirectory = Path.GetDirectoryName(mapPath) ?? string.Empty;
        var tilesetPath = Path.Combine(mapDirectory, tileset.Image.Source);

        // Make path relative to Assets root for AssetManager
        var assetsRoot = "Assets";
        var relativePath = Path.GetRelativePath(assetsRoot, tilesetPath);

        _assetManager.LoadTexture(tilesetId, relativePath);
    }

    private int GetMapId(string mapPath)
    {
        var mapName = Path.GetFileNameWithoutExtension(mapPath);

        // Get or create unique map ID
        if (_mapNameToId.TryGetValue(mapName, out var existingId))
            return existingId;

        var newId = _nextMapId++;
        _mapNameToId[mapName] = newId;
        return newId;
    }

    /// <summary>
    ///     Gets the map ID for a map name without loading it.
    /// </summary>
    /// <param name="mapName">The map name (without extension).</param>
    /// <returns>Map ID if the map has been loaded, -1 otherwise.</returns>
    public int GetMapIdByName(string mapName)
    {
        return _mapNameToId.TryGetValue(mapName, out var id) ? id : -1;
    }

    /// <summary>
    ///     Determines the TileLayer enum value from a layer name.
    ///     Supports backward compatibility with standard "Ground", "Objects", "Overhead" names,
    ///     while also working with any layer names by falling back to layer index.
    /// </summary>
    /// <param name="layerName">The name of the layer from Tiled.</param>
    /// <param name="layerIndex">The index of the layer (0-based).</param>
    /// <returns>The appropriate TileLayer enum value.</returns>
    private TileLayer DetermineTileLayer(string layerName, int layerIndex)
    {
        // Try to determine from layer name (case-insensitive)
        if (!string.IsNullOrEmpty(layerName))
        {
            return layerName.ToLowerInvariant() switch
            {
                "ground" => TileLayer.Ground,
                "objects" => TileLayer.Object,
                "overhead" => TileLayer.Overhead,
                // For any other names, use index-based fallback
                _ => DetermineFromIndex(layerIndex)
            };
        }

        // Fallback to index
        return DetermineFromIndex(layerIndex);
    }

    /// <summary>
    ///     Determines TileLayer from index with wraparound for 4+ layers.
    ///     Treats index 0 as Ground, 1 as Objects, 2+ as Overhead.
    /// </summary>
    private static TileLayer DetermineFromIndex(int layerIndex)
    {
        return layerIndex switch
        {
            0 => TileLayer.Ground,
            1 => TileLayer.Object,
            _ => TileLayer.Overhead // 2+ all map to Overhead
        };
    }

    /// <summary>
    ///     Determines which tile template to use based on tile properties.
    ///     Returns null if no suitable template is found (falls back to manual creation).
    /// </summary>
    /// <param name="props">Tile properties from Tiled.</param>
    /// <returns>Template ID or null</returns>
    private static string? DetermineTileTemplate(Dictionary<string, object> props)
    {
        // Check for ledge - highest priority (specific behavior)
        if (props.TryGetValue("ledge_direction", out var ledgeValue))
            return ledgeValue switch
            {
                string s when !string.IsNullOrEmpty(s) => s.ToLower() switch
                {
                    "down" => "tile/ledge/down",
                    "up" => "tile/ledge/up",
                    "left" => "tile/ledge/left",
                    "right" => "tile/ledge/right",
                    _ => null,
                },
                not null when !string.IsNullOrEmpty(ledgeValue.ToString()) => ledgeValue
                    .ToString()!
                    .ToLower() switch
                {
                    "down" => "tile/ledge/down",
                    "up" => "tile/ledge/up",
                    "left" => "tile/ledge/left",
                    "right" => "tile/ledge/right",
                    _ => null,
                },
                _ => null,
            };

        // Check for solid wall (but not ledge)
        if (props.TryGetValue("solid", out var solidValue))
        {
            var isSolid = solidValue switch
            {
                bool b => b,
                string s => bool.TryParse(s, out var result) && result,
                _ => false,
            };

            if (isSolid)
                return "tile/wall";
        }

        // Check for encounter zone (grass)
        if (props.TryGetValue("encounter_rate", out var encounterValue))
        {
            var encounterRate = encounterValue switch
            {
                int i => i,
                string s when int.TryParse(s, out var result) => result,
                _ => 0,
            };

            if (encounterRate > 0)
                return "tile/grass";
        }

        // Default ground tile
        return "tile/ground";
    }

    private void CreateTileEntity(
        World world,
        int x,
        int y,
        int mapId,
        int tileGid,
        TmxTileset tileset,
        TileLayer layer,
        LayerOffset? layerOffset,
        bool flipH = false,
        bool flipV = false,
        bool flipD = false
    )
    {
        // Get tile properties from tileset (convert global ID to local ID)
        var localTileId = tileGid - tileset.FirstGid;
        Dictionary<string, object>? props = null;
        if (localTileId >= 0)
            tileset.TileProperties.TryGetValue(localTileId, out props);

        // Determine which template to use (if entity factory is available)
        string? templateId = null;
        if (_entityFactory != null && props != null)
            templateId = DetermineTileTemplate(props);

        // Create the entity - use template if available, otherwise manual creation
        Entity entity;
        if (_entityFactory != null && templateId != null && _entityFactory.HasTemplate(templateId))
        {
            // Template-based creation
            var position = new TilePosition(x, y, mapId);
            var sprite = new TileSprite(
                tileset.Name ?? "default",
                tileGid,
                layer,
                CalculateSourceRect(tileGid, tileset),
                flipH,
                flipV,
                flipD
            );

            entity = _entityFactory.SpawnFromTemplate(
                templateId,
                world,
                builder =>
                {
                    builder.OverrideComponent(position);
                    builder.OverrideComponent(sprite);
                }
            );
        }
        else
        {
            // Fallback: Manual creation (backward compatible)
            var position = new TilePosition(x, y, mapId);
            var sprite = new TileSprite(
                tileset.Name ?? "default",
                tileGid,
                layer,
                CalculateSourceRect(tileGid, tileset),
                flipH,
                flipV,
                flipD
            );

            entity = world.Create(position, sprite);

            // Add components based on properties (old behavior)
            if (props != null)
            {
                // Check if this is a ledge tile (needs special handling)
                var isLedge = props.ContainsKey("ledge_direction");

                // Add Collision component if tile is solid OR is a ledge
                if (props.TryGetValue("solid", out var solidValue) || isLedge)
                {
                    var isSolid = false;

                    if (solidValue != null)
                        isSolid = solidValue switch
                        {
                            bool b => b,
                            string s => bool.TryParse(s, out var result) && result,
                            _ => false,
                        };
                    else if (isLedge)
                        isSolid = true;

                    if (isSolid)
                        world.Add(entity, new Collision(true));
                }

                // Add TileLedge component for ledges
                if (props.TryGetValue("ledge_direction", out var ledgeValue))
                {
                    var ledgeDir = ledgeValue switch
                    {
                        string s => s,
                        _ => ledgeValue?.ToString(),
                    };

                    if (!string.IsNullOrEmpty(ledgeDir))
                    {
                        var jumpDirection = ledgeDir.ToLower() switch
                        {
                            "down" => Direction.Down,
                            "up" => Direction.Up,
                            "left" => Direction.Left,
                            "right" => Direction.Right,
                            _ => Direction.None,
                        };

                        if (jumpDirection != Direction.None)
                            world.Add(entity, new TileLedge(jumpDirection));
                    }
                }

                // Add EncounterZone component if encounter rate exists
                if (
                    props.TryGetValue("encounter_rate", out var encounterRateValue)
                    && encounterRateValue is int encounterRate
                    && encounterRate > 0
                )
                {
                    var encounterTableId = props.TryGetValue("encounter_table", out var tableValue)
                        ? tableValue.ToString() ?? ""
                        : "";

                    world.Add(entity, new EncounterZone(encounterTableId, encounterRate));
                }
            }
        }

        // Add additional components that aren't in templates (both paths)
        if (props != null)
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
            if (
                props.TryGetValue("script", out var scriptValue) && scriptValue is string scriptPath
            )
                world.Add(entity, new TileScript(scriptPath));
        }

        // Add LayerOffset component if layer has offset (for parallax scrolling)
        if (layerOffset.HasValue)
            world.Add(entity, layerOffset.Value);
    }

    /// <summary>
    ///     Spawns entities from map objects (NPCs, items, triggers, etc.).
    ///     Objects must have a "type" property indicating entity template (e.g., "npc/generic").
    /// </summary>
    /// <param name="world">The ECS world.</param>
    /// <param name="tmxDoc">The Tiled map document.</param>
    /// <param name="mapId">The map identifier.</param>
    /// <param name="tileHeight">Tile height for coordinate conversion.</param>
    /// <returns>Number of entities created from objects.</returns>
    private int SpawnMapObjects(World world, TmxDocument tmxDoc, int mapId, int tileHeight)
    {
        if (_entityFactory == null)
            // No entity factory - can't spawn from templates
            return 0;

        var created = 0;

        foreach (var objectGroup in tmxDoc.ObjectGroups)
        foreach (var obj in objectGroup.Objects)
        {
            // Get template ID from object type or properties
            var templateId = obj.Type;
            if (
                string.IsNullOrEmpty(templateId)
                && obj.Properties.TryGetValue("template", out var templateProp)
            )
                templateId = templateProp.ToString();

            if (string.IsNullOrEmpty(templateId))
            {
                _logger?.LogOperationSkipped($"Object '{obj.Name}'", "no type/template");
                continue;
            }

            // Check if template exists
            if (!_entityFactory.HasTemplate(templateId))
            {
                _logger?.LogResourceNotFound("Template", $"{templateId} for '{obj.Name}'");
                continue;
            }

            // Convert pixel coordinates to tile coordinates
            // Tiled Y coordinate is from top of object, use top-left corner for positioning
            var tileX = (int)Math.Floor(obj.X / tileHeight);
            var tileY = (int)Math.Floor(obj.Y / tileHeight);

            try
            {
                // Spawn entity from template
                var entity = _entityFactory.SpawnFromTemplate(
                    templateId,
                    world,
                    builder =>
                    {
                        // Override position with map coordinates
                        builder.OverrideComponent(new Position(tileX, tileY, mapId, tileHeight));

                        // Apply any custom properties from the object
                        if (obj.Properties.TryGetValue("direction", out var dirProp))
                        {
                            var dirStr = dirProp.ToString()?.ToLower();
                            var direction = dirStr switch
                            {
                                "up" => Direction.Up,
                                "down" => Direction.Down,
                                "left" => Direction.Left,
                                "right" => Direction.Right,
                                _ => Direction.Down,
                            };
                            builder.OverrideComponent(direction);
                        }

                        // Handle NPC-specific properties
                        if (templateId.StartsWith("npc/"))
                        {
                            // NPC properties
                            var hasNpcId = obj.Properties.TryGetValue("npcId", out var npcIdProp);
                            var hasDisplayName = obj.Properties.TryGetValue(
                                "displayName",
                                out var displayNameProp
                            );

                            if (hasNpcId || hasDisplayName)
                            {
                                var npcId = npcIdProp?.ToString();
                                if (string.IsNullOrWhiteSpace(npcId))
                                    npcId = obj.Name ?? string.Empty;

                                var displayName = displayNameProp?.ToString();
                                if (string.IsNullOrWhiteSpace(displayName))
                                    displayName = obj.Name ?? string.Empty;

                                builder.OverrideComponent(new Npc(npcId));

                                if (!string.IsNullOrWhiteSpace(displayName))
                                    builder.OverrideComponent(new Name(displayName));
                            }

                            // Movement route properties (waypoints for patrol NPCs)
                            if (obj.Properties.TryGetValue("waypoints", out var waypointsProp))
                            {
                                var waypointsStr = waypointsProp.ToString();
                                if (!string.IsNullOrEmpty(waypointsStr))
                                {
                                    // Parse waypoints: "x1,y1;x2,y2;x3,y3"
                                    var points = new List<Point>();
                                    var pairs = waypointsStr.Split(';');
                                    foreach (var pair in pairs)
                                    {
                                        var coords = pair.Split(',');
                                        if (
                                            coords.Length == 2
                                            && int.TryParse(coords[0].Trim(), out var x)
                                            && int.TryParse(coords[1].Trim(), out var y)
                                        )
                                            points.Add(new Point(x, y));
                                    }

                                    if (points.Count > 0)
                                    {
                                        var waypointWaitTime = 1.0f;
                                        if (
                                            obj.Properties.TryGetValue(
                                                "waypointWaitTime",
                                                out var waitProp
                                            )
                                            && float.TryParse(waitProp.ToString(), out var waitTime)
                                        )
                                            waypointWaitTime = waitTime;

                                        builder.OverrideComponent(
                                            new MovementRoute(
                                                points.ToArray(),
                                                true,
                                                waypointWaitTime
                                            )
                                        );
                                    }
                                }
                            }
                        }
                    }
                );

                _logger?.LogDebug(
                    "Spawned '{ObjectName}' ({TemplateId}) at ({X}, {Y})",
                    obj.Name,
                    templateId,
                    tileX,
                    tileY
                );
                created++;
            }
            catch (Exception ex)
            {
                _logger?.LogExceptionWithContext(
                    ex,
                    "Failed to spawn '{ObjectName}' from template '{TemplateId}'",
                    obj.Name,
                    templateId
                );
            }
        }

        return created;
    }

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

        var spacing = tileset.Spacing; // Space between tiles
        var margin = tileset.Margin; // Border around tileset

        var imageWidth = tileset.Image?.Width ?? 256;
        var tilesPerRow = (imageWidth - margin) / (tileWidth + spacing);

        // Calculate tile position in the grid
        var tileX = localTileId % tilesPerRow;
        var tileY = localTileId / tilesPerRow;

        // Calculate source rect with spacing and margin
        var sourceX = margin + tileX * (tileWidth + spacing);
        var sourceY = margin + tileY * (tileHeight + spacing);

        return new Rectangle(sourceX, sourceY, tileWidth, tileHeight);
    }

    /// <summary>
    ///     Creates entities for image layers in the map.
    ///     Image layers are rendered as full images at specific positions in the layer order.
    /// </summary>
    /// <param name="world">The ECS world.</param>
    /// <param name="tmxDoc">The Tiled map document.</param>
    /// <param name="mapPath">Path to the map file (for relative image path resolution).</param>
    /// <param name="totalLayerCount">Total number of layers (for Z-order calculation).</param>
    /// <returns>Number of image layer entities created.</returns>
    private int CreateImageLayerEntities(
        World world,
        TmxDocument tmxDoc,
        string mapPath,
        int totalLayerCount
    )
    {
        if (tmxDoc.ImageLayers.Count == 0)
            return 0;

        var created = 0;
        var mapDirectory = Path.GetDirectoryName(mapPath) ?? string.Empty;

        // Process image layers in order
        for (var i = 0; i < tmxDoc.ImageLayers.Count; i++)
        {
            var imageLayer = tmxDoc.ImageLayers[i];

            // Skip invisible layers
            if (!imageLayer.Visible || imageLayer.Image == null)
                continue;

            // Get image path
            var imagePath = imageLayer.Image.Source;
            if (string.IsNullOrEmpty(imagePath))
            {
                _logger?.LogWarning(
                    "Image layer '{LayerName}' has no image source - skipping",
                    imageLayer.Name
                );
                continue;
            }

            // Create texture ID from image filename
            var textureId = Path.GetFileNameWithoutExtension(imagePath);

            // Load texture if not already loaded
            if (!_assetManager.HasTexture(textureId))
            {
                try
                {
                    // Resolve relative path from map directory
                    var fullImagePath = Path.Combine(mapDirectory, imagePath);

                    // Make path relative to Assets root for AssetManager
                    var assetsRoot = "Assets";
                    var relativePath = Path.GetRelativePath(assetsRoot, fullImagePath);

                    _assetManager.LoadTexture(textureId, relativePath);
                }
                catch (Exception ex)
                {
                    _logger?.LogExceptionWithContext(
                        ex,
                        "Failed to load image layer texture '{TextureId}' from '{ImagePath}'",
                        textureId,
                        imagePath
                    );
                    continue;
                }
            }

            // Calculate layer depth based on position in layer stack
            // Image layers should interleave with tile layers
            // We need to determine where this image layer falls in the overall layer order
            var layerDepth = CalculateImageLayerDepth(imageLayer.Id, totalLayerCount);

            // Create ImageLayer component
            var imageLayerComponent = new ImageLayer(
                textureId,
                imageLayer.X,
                imageLayer.Y,
                imageLayer.Opacity,
                layerDepth,
                imageLayer.Id
            );

            // Create entity with ImageLayer component
            var entity = world.Create(imageLayerComponent);

            _logger?.LogDebug(
                "Created image layer '{LayerName}' with texture '{TextureId}' at ({X}, {Y}) depth {Depth:F2}",
                imageLayer.Name,
                textureId,
                imageLayer.X,
                imageLayer.Y,
                layerDepth
            );

            created++;
        }

        return created;
    }

    /// <summary>
    ///     Calculates the layer depth for an image layer based on its ID.
    ///     Lower IDs render behind, higher IDs render in front.
    /// </summary>
    /// <param name="layerId">The layer ID from Tiled.</param>
    /// <param name="totalLayerCount">Total number of layers in the map.</param>
    /// <returns>Layer depth value (0.0 to 1.0, where lower is in front).</returns>
    private static float CalculateImageLayerDepth(int layerId, int totalLayerCount)
    {
        // Map layer IDs to depth range 0.0 (front) to 1.0 (back)
        // Lower layer IDs (created first in Tiled) should render behind (higher depth)
        // Higher layer IDs (created later in Tiled) should render in front (lower depth)
        if (totalLayerCount <= 1)
            return 0.5f;

        var normalized = (float)layerId / totalLayerCount;
        return 1.0f - normalized; // Invert so lower IDs = higher depth (back)
    }
}
