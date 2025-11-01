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
            // Check if object has "solid" property
            if (obj.Properties.TryGetValue("solid", out var solidValue) && solidValue is bool isSolid && isSolid)
            {
                // Mark tiles covered by this object as solid
                MarkTilesAsSolid(collider, obj, tmxDoc.TileWidth, tmxDoc.TileHeight);
            }
        }

        return collider;
    }

    private static void MarkTilesAsSolid(TileCollider collider, TmxObject obj, int tileWidth, int tileHeight)
    {
        // Convert pixel coordinates to tile coordinates
        int startX = (int)(obj.X / tileWidth);
        int startY = (int)(obj.Y / tileHeight);
        int endX = (int)((obj.X + obj.Width) / tileWidth);
        int endY = (int)((obj.Y + obj.Height) / tileHeight);

        // Mark all tiles in the rectangle as solid
        for (int y = startY; y <= endY; y++)
        {
            for (int x = startX; x <= endX; x++)
            {
                collider.SetSolid(x, y, true);
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
