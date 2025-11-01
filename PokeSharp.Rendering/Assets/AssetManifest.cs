namespace PokeSharp.Rendering.Assets;

/// <summary>
/// Defines the structure of the asset manifest JSON file.
/// </summary>
public class AssetManifest
{
    /// <summary>
    /// Gets or sets the list of tileset assets.
    /// </summary>
    public List<TilesetAssetEntry>? Tilesets { get; set; }

    /// <summary>
    /// Gets or sets the list of sprite assets.
    /// </summary>
    public List<SpriteAssetEntry>? Sprites { get; set; }

    /// <summary>
    /// Gets or sets the list of map assets.
    /// </summary>
    public List<MapAssetEntry>? Maps { get; set; }
}

/// <summary>
/// Represents a tileset entry in the asset manifest.
/// </summary>
public class TilesetAssetEntry
{
    /// <summary>
    /// Gets or sets the unique identifier for the tileset.
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Gets or sets the relative path to the tileset PNG file.
    /// </summary>
    public required string Path { get; set; }

    /// <summary>
    /// Gets or sets the tile width in pixels (default: 16).
    /// </summary>
    public int TileWidth { get; set; } = 16;

    /// <summary>
    /// Gets or sets the tile height in pixels (default: 16).
    /// </summary>
    public int TileHeight { get; set; } = 16;
}

/// <summary>
/// Represents a sprite entry in the asset manifest.
/// </summary>
public class SpriteAssetEntry
{
    /// <summary>
    /// Gets or sets the unique identifier for the sprite.
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Gets or sets the relative path to the sprite PNG file.
    /// </summary>
    public required string Path { get; set; }
}

/// <summary>
/// Represents a map entry in the asset manifest.
/// </summary>
public class MapAssetEntry
{
    /// <summary>
    /// Gets or sets the unique identifier for the map.
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Gets or sets the relative path to the TMX map file.
    /// </summary>
    public required string Path { get; set; }
}
