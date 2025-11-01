using System.Text.Json.Serialization;

namespace PokeSharp.Rendering.Loaders;

/// <summary>
/// JSON structure for Tiled map format (Tiled 1.11.2).
/// See: https://doc.mapeditor.org/en/stable/reference/json-map-format/
/// </summary>
public class TiledJsonMap
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";

    [JsonPropertyName("tiledversion")]
    public string? TiledVersion { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = "map";

    [JsonPropertyName("orientation")]
    public string Orientation { get; set; } = "orthogonal";

    [JsonPropertyName("renderorder")]
    public string RenderOrder { get; set; } = "right-down";

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }

    [JsonPropertyName("tilewidth")]
    public int TileWidth { get; set; }

    [JsonPropertyName("tileheight")]
    public int TileHeight { get; set; }

    [JsonPropertyName("infinite")]
    public bool Infinite { get; set; }

    [JsonPropertyName("layers")]
    public List<TiledJsonLayer>? Layers { get; set; }

    [JsonPropertyName("tilesets")]
    public List<TiledJsonTileset>? Tilesets { get; set; }

    [JsonPropertyName("properties")]
    public List<TiledJsonProperty>? Properties { get; set; }
}

/// <summary>
/// Represents a layer in a Tiled JSON map.
/// </summary>
public class TiledJsonLayer
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "tilelayer";

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }

    [JsonPropertyName("visible")]
    public bool Visible { get; set; } = true;

    [JsonPropertyName("opacity")]
    public float Opacity { get; set; } = 1.0f;

    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }

    /// <summary>
    /// Tile data as flat array (for tilelayer).
    /// </summary>
    [JsonPropertyName("data")]
    public int[]? Data { get; set; }

    /// <summary>
    /// Objects in this layer (for objectgroup).
    /// </summary>
    [JsonPropertyName("objects")]
    public List<TiledJsonObject>? Objects { get; set; }

    [JsonPropertyName("properties")]
    public List<TiledJsonProperty>? Properties { get; set; }
}

/// <summary>
/// Represents a tileset reference in a Tiled JSON map.
/// </summary>
public class TiledJsonTileset
{
    [JsonPropertyName("firstgid")]
    public int FirstGid { get; set; }

    /// <summary>
    /// External tileset file path (if external).
    /// </summary>
    [JsonPropertyName("source")]
    public string? Source { get; set; }

    // Embedded tileset properties (if not external)
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("tilewidth")]
    public int? TileWidth { get; set; }

    [JsonPropertyName("tileheight")]
    public int? TileHeight { get; set; }

    [JsonPropertyName("tilecount")]
    public int? TileCount { get; set; }

    [JsonPropertyName("columns")]
    public int? Columns { get; set; }

    [JsonPropertyName("image")]
    public string? Image { get; set; }

    [JsonPropertyName("imagewidth")]
    public int? ImageWidth { get; set; }

    [JsonPropertyName("imageheight")]
    public int? ImageHeight { get; set; }

    /// <summary>
    /// Tile definitions with animations and properties.
    /// </summary>
    [JsonPropertyName("tiles")]
    public List<TiledJsonTileDefinition>? Tiles { get; set; }
}

/// <summary>
/// Represents a tile definition with animation data.
/// </summary>
public class TiledJsonTileDefinition
{
    /// <summary>
    /// Local tile ID within the tileset.
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>
    /// Animation frames for this tile.
    /// </summary>
    [JsonPropertyName("animation")]
    public List<TiledJsonAnimationFrame>? Animation { get; set; }
}

/// <summary>
/// Represents a single animation frame in a tile animation.
/// </summary>
public class TiledJsonAnimationFrame
{
    /// <summary>
    /// Local tile ID to display for this frame.
    /// </summary>
    [JsonPropertyName("tileid")]
    public int TileId { get; set; }

    /// <summary>
    /// Duration of this frame in milliseconds.
    /// </summary>
    [JsonPropertyName("duration")]
    public int Duration { get; set; }
}

/// <summary>
/// Represents an object in an object layer.
/// </summary>
public class TiledJsonObject
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("x")]
    public float X { get; set; }

    [JsonPropertyName("y")]
    public float Y { get; set; }

    [JsonPropertyName("width")]
    public float Width { get; set; }

    [JsonPropertyName("height")]
    public float Height { get; set; }

    [JsonPropertyName("visible")]
    public bool Visible { get; set; } = true;

    [JsonPropertyName("properties")]
    public List<TiledJsonProperty>? Properties { get; set; }
}

/// <summary>
/// Represents a custom property in Tiled.
/// </summary>
public class TiledJsonProperty
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "string";

    [JsonPropertyName("value")]
    public object? Value { get; set; }
}
