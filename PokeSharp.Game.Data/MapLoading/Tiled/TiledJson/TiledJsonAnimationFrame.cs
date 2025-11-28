using System.Text.Json.Serialization;

namespace PokeSharp.Game.Data.MapLoading.Tiled.TiledJson;

/// <summary>
///     Represents a single animation frame in a tile animation.
/// </summary>
public class TiledJsonAnimationFrame
{
    /// <summary>
    ///     Local tile ID to display for this frame.
    /// </summary>
    [JsonPropertyName("tileid")]
    public int TileId { get; set; }

    /// <summary>
    ///     Duration of this frame in milliseconds.
    /// </summary>
    [JsonPropertyName("duration")]
    public int Duration { get; set; }
}
