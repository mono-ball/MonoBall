namespace PokeSharp.Game.Data.Configuration;

/// <summary>
///     Configuration settings for the MapLoader.
///     Replaces hardcoded values with configurable options.
/// </summary>
public class MapLoaderConfig
{
    /// <summary>
    ///     Root directory for asset files (relative to executable).
    /// </summary>
    public string AssetRoot { get; set; } = "Assets";

    /// <summary>
    ///     Default image size for tilesets when dimensions are not specified.
    /// </summary>
    public int DefaultImageSize { get; set; } = 256;

    /// <summary>
    ///     Maximum render distance for culling (in tiles).
    /// </summary>
    public int MaxRenderDistance { get; set; } = 10000;

    /// <summary>
    ///     Whether to validate maps before loading.
    /// </summary>
    public bool ValidateMaps { get; set; } = true;

    /// <summary>
    ///     Whether to throw exceptions on validation errors.
    ///     If false, validation errors are logged as warnings.
    /// </summary>
    public bool ThrowOnValidationError { get; set; }

    /// <summary>
    ///     Whether to cache loaded tileset textures.
    /// </summary>
    public bool CacheTilesetTextures { get; set; } = true;

    /// <summary>
    ///     Default tile layer for tiles without explicit layer names.
    /// </summary>
    public string DefaultTileLayer { get; set; } = "Ground";

    /// <summary>
    ///     Maximum number of animated tiles to create per map.
    ///     Prevents performance issues with excessive animations.
    /// </summary>
    public int MaxAnimatedTiles { get; set; } = 1000;

    /// <summary>
    ///     Creates a default configuration with standard settings.
    /// </summary>
    public static MapLoaderConfig CreateDefault()
    {
        return new MapLoaderConfig();
    }

    /// <summary>
    ///     Creates a development configuration with validation enabled.
    /// </summary>
    public static MapLoaderConfig CreateDevelopment()
    {
        return new MapLoaderConfig
        {
            ValidateMaps = true,
            ThrowOnValidationError = true,
            CacheTilesetTextures = true,
        };
    }

    /// <summary>
    ///     Creates a production configuration optimized for performance.
    /// </summary>
    public static MapLoaderConfig CreateProduction()
    {
        return new MapLoaderConfig
        {
            ValidateMaps = false, // Skip validation in production for performance
            ThrowOnValidationError = false,
            CacheTilesetTextures = true,
            MaxRenderDistance = 5000, // Smaller render distance for better performance
        };
    }
}
