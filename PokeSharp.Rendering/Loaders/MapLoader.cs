using PokeSharp.Core.Components;
using PokeSharp.Rendering.Assets;

namespace PokeSharp.Rendering.Loaders;

/// <summary>
/// Loads Tiled maps and converts them to ECS components.
/// </summary>
public class MapLoader
{
    private readonly AssetManager _assetManager;

    /// <summary>
    /// Initializes a new instance of the MapLoader class.
    /// </summary>
    /// <param name="assetManager">Asset manager for texture loading.</param>
    public MapLoader(AssetManager assetManager)
    {
        _assetManager = assetManager ?? throw new ArgumentNullException(nameof(assetManager));
    }

    /// <summary>
    /// Loads a Tiled map from JSON file and converts to TileMap component.
    /// </summary>
    /// <param name="mapPath">Path to the .json map file.</param>
    /// <returns>TileMap component ready for ECS.</returns>
    public TileMap LoadMap(string mapPath)
    {
        // Parse JSON map
        var tmxDoc = TiledMapLoader.Load(mapPath);

        // Extract layers by name
        var groundLayer = tmxDoc.Layers.FirstOrDefault(l => l.Name.Equals("Ground", StringComparison.OrdinalIgnoreCase));
        var objectLayer = tmxDoc.Layers.FirstOrDefault(l => l.Name.Equals("Objects", StringComparison.OrdinalIgnoreCase));
        var overheadLayer = tmxDoc.Layers.FirstOrDefault(l => l.Name.Equals("Overhead", StringComparison.OrdinalIgnoreCase));

        // Get tileset info and extract texture ID
        var tileset = tmxDoc.Tilesets.FirstOrDefault()
            ?? throw new InvalidOperationException($"Map '{mapPath}' has no tilesets");

        var tilesetId = ExtractTilesetId(tileset, mapPath);

        // Ensure tileset texture is loaded
        if (!_assetManager.HasTexture(tilesetId))
        {
            LoadTilesetTexture(tileset, mapPath, tilesetId);
        }

        // Create TileMap component
        return new TileMap
        {
            MapId = Path.GetFileNameWithoutExtension(mapPath),
            Width = tmxDoc.Width,
            Height = tmxDoc.Height,
            TilesetId = tilesetId,
            GroundLayer = groundLayer?.Data ?? new int[tmxDoc.Height, tmxDoc.Width],
            ObjectLayer = objectLayer?.Data ?? new int[tmxDoc.Height, tmxDoc.Width],
            OverheadLayer = overheadLayer?.Data ?? new int[tmxDoc.Height, tmxDoc.Width]
        };
    }

    /// <summary>
    /// Loads collision data from a Tiled map.
    /// Supports both standard solid collision and directional ledges.
    /// </summary>
    /// <param name="mapPath">Path to the .json map file.</param>
    /// <returns>TileCollider component with collision data.</returns>
    public TileCollider LoadCollision(string mapPath)
    {
        var tmxDoc = TiledMapLoader.Load(mapPath);
        var collider = new TileCollider(tmxDoc.Width, tmxDoc.Height);

        // Find collision object group
        var collisionGroup = tmxDoc.ObjectGroups.FirstOrDefault(g =>
            g.Name.Equals("Collision", StringComparison.OrdinalIgnoreCase));

        if (collisionGroup == null)
        {
            return collider; // No collision data
        }

        // Process collision objects
        foreach (var obj in collisionGroup.Objects)
        {
            // Check for ledge objects (type="ledge")
            if (obj.Type != null && obj.Type.Equals("ledge", StringComparison.OrdinalIgnoreCase))
            {
                // Pokemon ledge: block upward movement (Direction.Up)
                MarkTilesAsLedge(collider, obj, tmxDoc.TileWidth, tmxDoc.TileHeight);
            }
            // Check if object has "solid" property for standard collision
            else if (obj.Properties.TryGetValue("solid", out var solidValue) && solidValue is bool isSolid && isSolid)
            {
                // Mark tiles covered by this object as solid
                MarkTilesAsSolid(collider, obj, tmxDoc.TileWidth, tmxDoc.TileHeight);
            }
        }

        return collider;
    }

    /// <summary>
    /// Loads animated tile data from a Tiled map.
    /// </summary>
    /// <param name="mapPath">Path to the .json map file.</param>
    /// <returns>Array of AnimatedTile components for tiles with animations.</returns>
    public AnimatedTile[] LoadAnimatedTiles(string mapPath)
    {
        var tmxDoc = TiledMapLoader.Load(mapPath);
        var tileset = tmxDoc.Tilesets.FirstOrDefault();

        if (tileset == null || tileset.Animations.Count == 0)
        {
            return Array.Empty<AnimatedTile>();
        }

        var animatedTiles = new List<AnimatedTile>();

        foreach (var kvp in tileset.Animations)
        {
            int localTileId = kvp.Key;
            var animation = kvp.Value;

            // Convert local tile ID to global tile ID
            int globalTileId = tileset.FirstGid + localTileId;

            // Convert frame local IDs to global IDs
            var globalFrameIds = animation.FrameTileIds.Select(id => tileset.FirstGid + id).ToArray();

            animatedTiles.Add(new AnimatedTile(
                globalTileId,
                globalFrameIds,
                animation.FrameDurations
            ));
        }

        return animatedTiles.ToArray();
    }

    private static void MarkTilesAsSolid(TileCollider collider, TmxObject obj, int tileWidth, int tileHeight)
    {
        // Convert pixel coordinates to tile coordinates
        int startX = (int)(obj.X / tileWidth);
        int startY = (int)(obj.Y / tileHeight);
        int endX = (int)((obj.X + obj.Width) / tileWidth);
        int endY = (int)((obj.Y + obj.Height) / tileHeight);

        // Mark all tiles in the rectangle as solid (exclusive upper bounds)
        for (int y = startY; y < endY; y++)
        {
            for (int x = startX; x < endX; x++)
            {
                collider.SetSolid(x, y, true);
            }
        }
    }

    /// <summary>
    /// Marks tiles as Pokemon-style ledges with directional blocking.
    /// Ledges block upward movement (Direction.Up) but allow jumping down (Direction.Down).
    /// </summary>
    /// <param name="collider">The TileCollider to update.</param>
    /// <param name="obj">The ledge object from Tiled.</param>
    /// <param name="tileWidth">Tile width in pixels.</param>
    /// <param name="tileHeight">Tile height in pixels.</param>
    private static void MarkTilesAsLedge(TileCollider collider, TmxObject obj, int tileWidth, int tileHeight)
    {
        // Convert pixel coordinates to tile coordinates
        int startX = (int)(obj.X / tileWidth);
        int startY = (int)(obj.Y / tileHeight);
        int endX = (int)((obj.X + obj.Width) / tileWidth);
        int endY = (int)((obj.Y + obj.Height) / tileHeight);

        // Parse direction property (default to "down" for Pokemon ledges)
        string ledgeDirection = "down";
        if (obj.Properties.TryGetValue("direction", out var dirValue) && dirValue is string dirStr)
        {
            ledgeDirection = dirStr.ToLowerInvariant();
        }

        // Pokemon ledge: blocks upward movement from below
        // When you jump DOWN onto a ledge, you cannot climb back UP
        Direction[] blockedDirections = ledgeDirection switch
        {
            "down" => new[] { Direction.Up },    // Jump down, block climbing up
            "up" => new[] { Direction.Down },     // Reverse ledge (rare)
            "left" => new[] { Direction.Right },  // Side ledge
            "right" => new[] { Direction.Left },  // Side ledge
            _ => new[] { Direction.Up }           // Default: standard Pokemon ledge
        };

        // Mark all tiles in the rectangle with directional blocking (exclusive upper bounds)
        for (int y = startY; y < endY; y++)
        {
            for (int x = startX; x < endX; x++)
            {
                collider.SetDirectionalBlock(x, y, blockedDirections);
            }
        }
    }

    private static string ExtractTilesetId(TmxTileset tileset, string mapPath)
    {
        // If tileset has an image, use the image filename as ID
        if (tileset.Image != null && !string.IsNullOrEmpty(tileset.Image.Source))
        {
            return Path.GetFileNameWithoutExtension(tileset.Image.Source);
        }

        // Fallback to tileset name
        return tileset.Name ?? "default-tileset";
    }

    private void LoadTilesetTexture(TmxTileset tileset, string mapPath, string tilesetId)
    {
        if (tileset.Image == null || string.IsNullOrEmpty(tileset.Image.Source))
        {
            throw new InvalidOperationException($"Tileset has no image source");
        }

        // Resolve relative path from map directory
        var mapDirectory = Path.GetDirectoryName(mapPath) ?? string.Empty;
        var tilesetPath = Path.Combine(mapDirectory, tileset.Image.Source);

        // Make path relative to Assets root for AssetManager
        var assetsRoot = "Assets";
        var relativePath = Path.GetRelativePath(assetsRoot, tilesetPath);

        _assetManager.LoadTexture(tilesetId, relativePath);
    }
}
