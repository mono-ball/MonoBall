namespace PokeSharp.Engine.Rendering;

/// <summary>
///     Nintendo DS hardware constants for accurate emulation of NDS resolution and display characteristics.
/// </summary>
/// <remarks>
///     The Nintendo DS has dual screens, each with specific resolution characteristics.
///     These constants are used to calculate proper zoom levels and viewport scaling.
/// </remarks>
public static class NdsConstants
{
    /// <summary>
    ///     Native width of Nintendo DS screen in pixels.
    /// </summary>
    /// <remarks>
    ///     The NDS screen resolution is 256x192 pixels (4:3 aspect ratio).
    ///     This is used to calculate zoom levels that match authentic NDS scaling.
    /// </remarks>
    public const int ScreenWidth = 256;

    /// <summary>
    ///     Native height of Nintendo DS screen in pixels.
    /// </summary>
    /// <seealso cref="ScreenWidth" />
    public const int ScreenHeight = 192;

    /// <summary>
    ///     Aspect ratio of NDS screen (width / height).
    /// </summary>
    public const float AspectRatio = (float)ScreenWidth / ScreenHeight;
}
