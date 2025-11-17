namespace PokeSharp.Game.Configuration;

/// <summary>
///     Configuration for gameplay settings (camera, input, pools).
/// </summary>
public class GameplayConfig
{
    /// <summary>
    ///     Default camera zoom level.
    /// </summary>
    public float DefaultZoom { get; set; } = 3.0f;

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
                Tile = new PoolConfig { InitialSize = 5000, MaxSize = 10000 }
            }
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
    public PoolConfig Npc { get; set; } = new() { InitialSize = 20, MaxSize = 100 };

    /// <summary>
    ///     Tile entity pool configuration.
    /// </summary>
    public PoolConfig Tile { get; set; } = new() { InitialSize = 2000, MaxSize = 5000 };
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
}

