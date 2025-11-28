namespace PokeSharp.Engine.Rendering;

/// <summary>
///     Centralized constants for rendering configuration.
///     Contains all hardcoded values used throughout the rendering pipeline.
/// </summary>
public static class RenderingConstants
{
    /// <summary>
    ///     Maximum Y coordinate for render distance normalization in z-ordering.
    ///     Used to calculate depth values for proper sprite layering.
    /// </summary>
    /// <remarks>
    ///     This value should be larger than any expected Y coordinate in your game world.
    ///     Sprites with Y coordinates beyond this will be clamped for rendering purposes.
    /// </remarks>
    public const float MaxRenderDistance = 10000f;

    /// <summary>
    ///     Layer index after which sprites should be rendered.
    ///     Sprites are rendered between object layers and overhead layers.
    /// </summary>
    /// <remarks>
    ///     Layer ordering:
    ///     - Layer 0: Ground
    ///     - Layer 1: Objects
    ///     - [Sprites rendered here]
    ///     - Layer 2+: Overhead (trees, roofs, etc.)
    /// </remarks>
    public const int SpriteRenderAfterLayer = 1;

    /// <summary>
    ///     Frame interval for performance logging (in frames).
    ///     Performance statistics are logged every N frames to avoid log spam.
    /// </summary>
    /// <remarks>
    ///     300 frames at 60fps = 5 seconds between log entries.
    ///     Adjust this value to log more or less frequently.
    /// </remarks>
    public const int PerformanceLogInterval = 300;

    /// <summary>
    ///     Default assets root directory name.
    ///     This is the base directory where game assets are stored.
    /// </summary>
    /// <remarks>
    ///     This default can be overridden in AssetManager constructor.
    ///     All asset paths are resolved relative to this root directory.
    /// </remarks>
    public const string DefaultAssetRoot = "Assets";
}
