namespace PokeSharp.Rendering.Loaders;

/// <summary>
/// Represents a parsed Tiled map document (TMX format).
/// Supports Tiled 1.11.2 format.
/// </summary>
public class TmxDocument
{
    /// <summary>
    /// Gets or sets the TMX format version.
    /// </summary>
    public string Version { get; set; } = "1.0";

    /// <summary>
    /// Gets or sets the Tiled editor version that created the map.
    /// </summary>
    public string? TiledVersion { get; set; }

    /// <summary>
    /// Gets or sets the map width in tiles.
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Gets or sets the map height in tiles.
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// Gets or sets the tile width in pixels.
    /// </summary>
    public int TileWidth { get; set; }

    /// <summary>
    /// Gets or sets the tile height in pixels.
    /// </summary>
    public int TileHeight { get; set; }

    /// <summary>
    /// Gets or sets the tilesets used in this map.
    /// </summary>
    public List<TmxTileset> Tilesets { get; set; } = new();

    /// <summary>
    /// Gets or sets the tile layers in this map.
    /// </summary>
    public List<TmxLayer> Layers { get; set; } = new();

    /// <summary>
    /// Gets or sets the object groups (collision, triggers, etc.).
    /// </summary>
    public List<TmxObjectGroup> ObjectGroups { get; set; } = new();
}

/// <summary>
/// Represents a tileset in a Tiled map.
/// </summary>
public class TmxTileset
{
    /// <summary>
    /// Gets or sets the first global tile ID in this tileset.
    /// </summary>
    public int FirstGid { get; set; }

    /// <summary>
    /// Gets or sets the tileset name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the external TSX file path (if external tileset).
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// Gets or sets the tile width in pixels.
    /// </summary>
    public int TileWidth { get; set; }

    /// <summary>
    /// Gets or sets the tile height in pixels.
    /// </summary>
    public int TileHeight { get; set; }

    /// <summary>
    /// Gets or sets the total number of tiles in this tileset.
    /// </summary>
    public int TileCount { get; set; }

    /// <summary>
    /// Gets or sets the tileset image (if embedded tileset).
    /// </summary>
    public TmxImage? Image { get; set; }
}

/// <summary>
/// Represents an image used by a tileset.
/// </summary>
public class TmxImage
{
    /// <summary>
    /// Gets or sets the image source path.
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the image width in pixels.
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Gets or sets the image height in pixels.
    /// </summary>
    public int Height { get; set; }
}

/// <summary>
/// Represents a tile layer in a Tiled map.
/// </summary>
public class TmxLayer
{
    /// <summary>
    /// Gets or sets the layer ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the layer name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the layer width in tiles.
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Gets or sets the layer height in tiles.
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// Gets or sets whether the layer is visible.
    /// </summary>
    public bool Visible { get; set; } = true;

    /// <summary>
    /// Gets or sets the layer opacity (0.0 to 1.0).
    /// </summary>
    public float Opacity { get; set; } = 1.0f;

    /// <summary>
    /// Gets or sets the tile data as a 2D array [y, x].
    /// </summary>
    public int[,] Data { get; set; } = new int[0, 0];
}

/// <summary>
/// Represents an object group (collision, triggers, etc.).
/// </summary>
public class TmxObjectGroup
{
    /// <summary>
    /// Gets or sets the object group ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the object group name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the objects in this group.
    /// </summary>
    public List<TmxObject> Objects { get; set; } = new();
}

/// <summary>
/// Represents an object in an object group.
/// </summary>
public class TmxObject
{
    /// <summary>
    /// Gets or sets the object ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the object X position in pixels.
    /// </summary>
    public float X { get; set; }

    /// <summary>
    /// Gets or sets the object Y position in pixels.
    /// </summary>
    public float Y { get; set; }

    /// <summary>
    /// Gets or sets the object width in pixels.
    /// </summary>
    public float Width { get; set; }

    /// <summary>
    /// Gets or sets the object height in pixels.
    /// </summary>
    public float Height { get; set; }

    /// <summary>
    /// Gets or sets the object type.
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Gets or sets the object name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets custom properties for this object.
    /// </summary>
    public Dictionary<string, object> Properties { get; set; } = new();
}
