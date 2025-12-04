namespace MonoBallFramework.Game.Infrastructure.Configuration;

/// <summary>
///     Configuration for gameplay settings (camera, input, pools).
/// </summary>
public class GameplayConfig
{
    /// <summary>
    ///     Default camera zoom level.
    ///     4.0 = GBA style (240x160 native resolution scaled to 960x640 window).
    /// </summary>
    public float DefaultZoom { get; set; } = 4.0f;

    /// <summary>
    ///     Camera zoom transition speed (0.0 to 1.0).
    /// </summary>
    public float ZoomTransitionSpeed { get; set; } = 0.1f;

    /// <summary>
    ///     Default tile size in pixels (used when map info is unavailable).
    /// </summary>
    public int DefaultTileSize { get; set; } = 16;

    /// <summary>
    ///     Input buffering configuration.
    /// </summary>
    public InputBufferConfig InputBuffer { get; set; } = new();

    /// <summary>
    ///     Entity pool configurations.
    /// </summary>
    public PoolConfigs Pools { get; set; } = new();

    /// <summary>
    ///     Creates a default gameplay configuration.
    /// </summary>
    public static GameplayConfig CreateDefault()
    {
        return new GameplayConfig();
    }

    /// <summary>
    ///     Creates a development configuration with larger pools for testing.
    /// </summary>
    public static GameplayConfig CreateDevelopment()
    {
        return new GameplayConfig
        {
            Pools = new PoolConfigs
            {
                Player = new PoolConfig { InitialSize = 1, MaxSize = 10 },
                Npc = new PoolConfig { InitialSize = 50, MaxSize = 200 },
                Tile = new PoolConfig { InitialSize = 5000, MaxSize = 10000 },
            },
        };
    }

    /// <summary>
    ///     Creates a production configuration optimized for performance.
    /// </summary>
    public static GameplayConfig CreateProduction()
    {
        return new GameplayConfig();
    }
}

/// <summary>
///     Configuration for input buffering.
/// </summary>
public class InputBufferConfig
{
    /// <summary>
    ///     Maximum number of buffered inputs.
    /// </summary>
    public int MaxBufferedInputs { get; set; } = 5;

    /// <summary>
    ///     Input timeout in seconds before buffer is cleared.
    /// </summary>
    public float TimeoutSeconds { get; set; } = 0.2f;
}

/// <summary>
///     Configuration for entity pools.
/// </summary>
public class PoolConfigs
{
    /// <summary>
    ///     Player entity pool configuration.
    /// </summary>
    public PoolConfig Player { get; set; } = new() { InitialSize = 1, MaxSize = 10 };

    /// <summary>
    ///     NPC entity pool configuration.
    /// </summary>
    public PoolConfig Npc { get; set; } =
        new()
        {
            InitialSize = 100,
            MaxSize = 200,
            AutoResize = true,
        };

    /// <summary>
    ///     Tile entity pool configuration for multi-map streaming.
    ///     A typical map has ~60x40 tiles × 3 layers = 7200 tiles.
    ///     With 5 maps loaded simultaneously (current + 4 adjacent), we need ~36000 tiles.
    ///     AbsoluteMaxSize is set high to accommodate large maps and streaming.
    /// </summary>
    public PoolConfig Tile { get; set; } =
        new()
        {
            InitialSize = 5000,
            MaxSize = 20000,
            AutoResize = true,
            GrowthFactor = 2.0f,
            AbsoluteMaxSize = 100000,
        };
}

/// <summary>
///     Configuration for a single entity pool.
/// </summary>
public class PoolConfig
{
    /// <summary>
    ///     Initial pool size.
    /// </summary>
    public int InitialSize { get; set; }

    /// <summary>
    ///     Maximum pool size.
    /// </summary>
    public int MaxSize { get; set; }

    /// <summary>
    ///     Whether to warmup the pool on creation.
    /// </summary>
    public bool Warmup { get; set; } = true;

    /// <summary>
    ///     Whether the pool should automatically resize when exhausted.
    ///     When enabled, pool will grow by <see cref="GrowthFactor" /> when full.
    /// </summary>
    public bool AutoResize { get; set; } = true;

    /// <summary>
    ///     Growth factor when auto-resizing (e.g., 2.0 = double the size).
    ///     Only used when <see cref="AutoResize" /> is enabled.
    /// </summary>
    public float GrowthFactor { get; set; } = 1.5f;

    /// <summary>
    ///     Absolute maximum size the pool can grow to, even with auto-resize.
    ///     Set to 0 for unlimited growth (not recommended).
    /// </summary>
    public int AbsoluteMaxSize { get; set; } = 10000;
}
