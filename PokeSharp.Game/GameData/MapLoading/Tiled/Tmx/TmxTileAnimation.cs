namespace PokeSharp.Game.Data.MapLoading.Tiled.Tmx;

/// <summary>
///     Represents tile animation data.
/// </summary>
public class TmxTileAnimation
{
    /// <summary>
    ///     Gets or sets the array of tile IDs for each animation frame.
    /// </summary>
    public int[] FrameTileIds { get; set; } = [];

    /// <summary>
    ///     Gets or sets the array of frame durations in seconds.
    /// </summary>
    public float[] FrameDurations { get; set; } = [];
}
