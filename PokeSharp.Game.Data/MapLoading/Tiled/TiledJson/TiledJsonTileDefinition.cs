using System.Text.Json.Serialization;

namespace PokeSharp.Game.Data.MapLoading.Tiled.TiledJson;

/// <summary>
///     Represents a tile definition with animation data and custom properties.
/// </summary>
public class TiledJsonTileDefinition
{
    /// <summary>
    ///     Local tile ID within the tileset.
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>
    ///     Animation frames for this tile.
    /// </summary>
    [JsonPropertyName("animation")]
    public List<TiledJsonAnimationFrame>? Animation { get; set; }

    /// <summary>
    ///     Custom properties for this tile (fully data-driven).
    /// </summary>
    [JsonPropertyName("properties")]
    public List<TiledJsonProperty>? Properties { get; set; }

    /// <summary>
    ///     Tile type/class (optional, from Tiled object types).
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }
}
