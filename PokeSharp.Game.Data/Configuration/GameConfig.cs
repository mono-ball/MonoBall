namespace PokeSharp.Game.Data.Configuration;

/// <summary>
///     Root configuration for the game.
///     Aggregates all sub-configurations for easy management.
/// </summary>
public class GameConfig
{
    /// <summary>
    ///     Map loading configuration.
    /// </summary>
    public MapLoaderConfig MapLoader { get; set; } = MapLoaderConfig.CreateDefault();

    /// <summary>
    ///     Asset management configuration.
    /// </summary>
    public AssetConfig Assets { get; set; } = new();

    /// <summary>
    ///     Performance and optimization settings.
    /// </summary>
    public PerformanceConfig Performance { get; set; } = new();

    /// <summary>
    ///     Creates a default game configuration.
    /// </summary>
    public static GameConfig CreateDefault()
    {
        return new GameConfig
        {
            MapLoader = MapLoaderConfig.CreateDefault(),
            Assets = new AssetConfig(),
            Performance = new PerformanceConfig(),
        };
    }

    /// <summary>
    ///     Creates a development configuration with debugging features.
    /// </summary>
    public static GameConfig CreateDevelopment()
    {
        return new GameConfig
        {
            MapLoader = MapLoaderConfig.CreateDevelopment(),
            Assets = AssetConfig.CreateDevelopment(),
            Performance = PerformanceConfig.CreateDevelopment(),
        };
    }

    /// <summary>
    ///     Creates a production configuration optimized for performance.
    /// </summary>
    public static GameConfig CreateProduction()
    {
        return new GameConfig
        {
            MapLoader = MapLoaderConfig.CreateProduction(),
            Assets = AssetConfig.CreateProduction(),
            Performance = PerformanceConfig.CreateProduction(),
        };
    }
}

/// <summary>
///     Configuration for asset loading and caching.
/// </summary>
public class AssetConfig
{
    public string AssetRoot { get; set; } = "Assets";
    public bool PreloadAssets { get; set; }
    public bool CompressTextures { get; set; }
    public int MaxTextureSize { get; set; } = 4096;

    public static AssetConfig CreateDevelopment()
    {
        return new AssetConfig { PreloadAssets = false };
    }

    public static AssetConfig CreateProduction()
    {
        return new AssetConfig { PreloadAssets = true, CompressTextures = true };
    }
}

/// <summary>
///     Configuration for performance optimization.
/// </summary>
public class PerformanceConfig
{
    public bool EnableEntityCulling { get; set; } = true;
    public int CullingDistance { get; set; } = 100;
    public bool EnableBatchRendering { get; set; } = true;
    public int TargetFrameRate { get; set; } = 60;

    public static PerformanceConfig CreateDevelopment()
    {
        return new PerformanceConfig { EnableEntityCulling = false };
    }

    public static PerformanceConfig CreateProduction()
    {
        return new PerformanceConfig { EnableEntityCulling = true };
    }
}
