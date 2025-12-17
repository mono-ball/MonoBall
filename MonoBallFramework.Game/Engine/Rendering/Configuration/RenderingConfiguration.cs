namespace MonoBallFramework.Game.Engine.Rendering.Configuration;

/// <summary>
///     Configuration for rendering system settings.
///     Provides sensible defaults for sprite batching, layers, and performance tuning.
///     Based on the AudioConfiguration pattern.
/// </summary>
public class RenderingConfiguration
{
    /// <summary>
    ///     Maximum number of sprites that can be batched in a single draw call.
    ///     Default: 2048.
    /// </summary>
    public int MaxSpriteBatchSize { get; set; } = RenderingConstants.DefaultMaxSpriteBatchSize;

    /// <summary>
    ///     Default number of rendering layers.
    ///     Default: 8.
    /// </summary>
    public int DefaultLayerCount { get; set; } = RenderingConstants.DefaultLayerCount;

    /// <summary>
    ///     Maximum number of concurrent render targets.
    ///     Default: 4.
    /// </summary>
    public int MaxRenderTargets { get; set; } = RenderingConstants.DefaultMaxRenderTargets;

    /// <summary>
    ///     Whether to enable sprite batching optimization.
    ///     Default: true.
    /// </summary>
    public bool EnableSpriteBatching { get; set; } = true;

    /// <summary>
    ///     Whether to enable texture atlasing.
    ///     Default: true.
    /// </summary>
    public bool EnableTextureAtlasing { get; set; } = true;

    /// <summary>
    ///     Target frame rate for rendering.
    ///     Default: 60.
    /// </summary>
    public int TargetFrameRate { get; set; } = 60;

    /// <summary>
    ///     Whether to enable VSync.
    ///     Default: true.
    /// </summary>
    public bool EnableVSync { get; set; } = true;

    /// <summary>
    ///     Pixel scale for rendering (for pixel-perfect games).
    ///     Default: 1.
    /// </summary>
    public int PixelScale { get; set; } = 1;

    /// <summary>
    ///     Default configuration with balanced rendering settings.
    /// </summary>
    public static RenderingConfiguration Default => new();

    /// <summary>
    ///     Configuration optimized for production with performance focus.
    /// </summary>
    public static RenderingConfiguration Production =>
        new()
        {
            MaxSpriteBatchSize = 4096,
            DefaultLayerCount = 8,
            MaxRenderTargets = 4,
            EnableSpriteBatching = true,
            EnableTextureAtlasing = true,
            TargetFrameRate = 60,
            EnableVSync = true,
            PixelScale = 1
        };

    /// <summary>
    ///     Configuration for high-performance mode (more batching, less quality).
    /// </summary>
    public static RenderingConfiguration HighPerformance =>
        new()
        {
            MaxSpriteBatchSize = 8192,
            DefaultLayerCount = 4,
            MaxRenderTargets = 2,
            EnableSpriteBatching = true,
            EnableTextureAtlasing = true,
            TargetFrameRate = 60,
            EnableVSync = false,
            PixelScale = 1
        };

    /// <summary>
    ///     Factory method to create a default configuration instance.
    /// </summary>
    public static RenderingConfiguration CreateDefault()
    {
        return new RenderingConfiguration();
    }
}
