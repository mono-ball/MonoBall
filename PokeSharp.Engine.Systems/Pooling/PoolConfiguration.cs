namespace PokeSharp.Engine.Systems.Pooling;

/// <summary>
///     Configuration for entity pools, defining size limits and behavior.
///     Used to initialize pools with specific characteristics for different entity types.
/// </summary>
public class PoolConfiguration
{
    /// <summary>
    ///     Unique name for this pool configuration.
    /// </summary>
    public string Name { get; set; } = "default";

    /// <summary>
    ///     Number of entities to pre-allocate when pool is created.
    ///     Higher values reduce initial allocation spikes but use more memory.
    /// </summary>
    public int InitialSize { get; set; } = 100;

    /// <summary>
    ///     Maximum number of entities this pool can hold.
    ///     Pool will create new entities if empty until this limit is reached.
    /// </summary>
    public int MaxSize { get; set; } = 1000;

    /// <summary>
    ///     Whether to pre-create entities up to InitialSize on pool creation.
    ///     Set false to defer allocation until entities are actually needed.
    /// </summary>
    public bool Warmup { get; set; } = true;

    /// <summary>
    ///     Whether to track detailed statistics for this pool (slight overhead).
    ///     Disable for maximum performance in production.
    /// </summary>
    public bool TrackStatistics { get; set; } = true;

    // Common pool configurations for typical game entities

    /// <summary>
    ///     Default pool configuration for general-purpose entities.
    /// </summary>
    public static PoolConfiguration Default =>
        new()
        {
            Name = "default",
            InitialSize = 100,
            MaxSize = 1000,
        };

    /// <summary>
    ///     Pool configuration optimized for enemy entities (medium size, moderate max).
    /// </summary>
    public static PoolConfiguration Enemies =>
        new()
        {
            Name = "enemies",
            InitialSize = 50,
            MaxSize = 500,
        };

    /// <summary>
    ///     Pool configuration optimized for projectiles (large size, high throughput).
    /// </summary>
    public static PoolConfiguration Projectiles =>
        new()
        {
            Name = "projectiles",
            InitialSize = 200,
            MaxSize = 2000,
        };

    /// <summary>
    ///     Pool configuration optimized for visual effects (medium-large, temporary entities).
    /// </summary>
    public static PoolConfiguration Effects =>
        new()
        {
            Name = "effects",
            InitialSize = 100,
            MaxSize = 1000,
        };

    /// <summary>
    ///     Pool configuration optimized for UI elements (small, stable count).
    /// </summary>
    public static PoolConfiguration UI =>
        new()
        {
            Name = "ui",
            InitialSize = 50,
            MaxSize = 200,
        };

    /// <summary>
    ///     Pool configuration optimized for particles (very large, high churn).
    /// </summary>
    public static PoolConfiguration Particles =>
        new()
        {
            Name = "particles",
            InitialSize = 500,
            MaxSize = 5000,
        };
}
