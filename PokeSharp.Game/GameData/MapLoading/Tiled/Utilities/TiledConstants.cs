namespace PokeSharp.Game.Data.MapLoading.Tiled.Utilities;

/// <summary>
///     Constants used in Tiled map loading.
///     Centralizes magic numbers that were duplicated across multiple files.
/// </summary>
public static class TiledConstants
{
    /// <summary>
    ///     Tiled flip flags stored in upper 3 bits of GID.
    /// </summary>
    public static class FlipFlags
    {
        /// <summary>Bit flag for horizontal flip (0x80000000).</summary>
        public const uint HorizontalFlip = 0x80000000;

        /// <summary>Bit flag for vertical flip (0x40000000).</summary>
        public const uint VerticalFlip = 0x40000000;

        /// <summary>Bit flag for diagonal flip (0x20000000).</summary>
        public const uint DiagonalFlip = 0x20000000;

        /// <summary>Mask to extract the actual tile ID (lower 29 bits).</summary>
        public const uint TileIdMask = 0x1FFFFFFF;

        /// <summary>Combined mask for all flip flags.</summary>
        public const uint AllFlipFlags = HorizontalFlip | VerticalFlip | DiagonalFlip;
    }

    /// <summary>
    ///     Default tile dimensions used in Pokemon games.
    /// </summary>
    public static class Defaults
    {
        /// <summary>Default tile size in pixels (16x16 for GBA Pokemon games).</summary>
        public const int TileSize = 16;

        /// <summary>Default metatile size (2x2 tiles = 32x32 pixels).</summary>
        public const int MetatileSize = 32;

        /// <summary>Tiles per metatile in each dimension.</summary>
        public const int TilesPerMetatile = 2;
    }

    /// <summary>
    ///     Tiled layer type strings.
    /// </summary>
    public static class LayerTypes
    {
        public const string TileLayer = "tilelayer";
        public const string ObjectGroup = "objectgroup";
        public const string ImageLayer = "imagelayer";
        public const string Group = "group";
    }

    /// <summary>
    ///     Common property names in Tiled files.
    /// </summary>
    public static class PropertyNames
    {
        public const string Elevation = "elevation";
        public const string Collision = "collision";
        public const string ZOrder = "z_order";
        public const string AnimationFrames = "animation_frames";
        public const string FrameDuration = "frame_duration";
    }
}
