namespace PokeSharp.Core.Components.Tiles;

/// <summary>
///     Component for storing animated tile data.
///     Supports Pokemon-style tile animations (water ripples, grass swaying, flowers).
/// </summary>
public struct AnimatedTile
{
    /// <summary>
    ///     Gets or sets the array of tile IDs that make up the animation frames.
    /// </summary>
    public int[] FrameTileIds { get; set; }

    /// <summary>
    ///     Gets or sets the duration of each frame in seconds.
    /// </summary>
    public float[] FrameDurations { get; set; }

    /// <summary>
    ///     Gets or sets the current frame index being displayed.
    /// </summary>
    public int CurrentFrameIndex { get; set; }

    /// <summary>
    ///     Gets or sets the frame timer in seconds.
    ///     When this exceeds the current frame duration, advance to next frame.
    /// </summary>
    public float FrameTimer { get; set; }

    /// <summary>
    ///     Gets or sets the base tile ID (the original tile ID in the tileset).
    /// </summary>
    public int BaseTileId { get; set; }

    /// <summary>
    ///     Initializes a new instance of the AnimatedTile struct.
    /// </summary>
    /// <param name="baseTileId">The base tile ID.</param>
    /// <param name="frameTileIds">Array of animation frame tile IDs.</param>
    /// <param name="frameDurations">Array of frame durations in seconds.</param>
    public AnimatedTile(int baseTileId, int[] frameTileIds, float[] frameDurations)
    {
        BaseTileId = baseTileId;
        FrameTileIds = frameTileIds;
        FrameDurations = frameDurations;
        CurrentFrameIndex = 0;
        FrameTimer = 0f;
    }
}