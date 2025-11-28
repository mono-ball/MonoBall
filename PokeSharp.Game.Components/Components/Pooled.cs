namespace PokeSharp.Game.Components;

/// <summary>
///     Component that marks an entity as pooled, preventing accidental destruction.
///     Contains metadata about which pool owns this entity for proper lifecycle management.
/// </summary>
public struct Pooled
{
    /// <summary>
    ///     Name of the pool that owns this entity.
    ///     Used to return entity to correct pool on release.
    /// </summary>
    public string PoolName;

    /// <summary>
    ///     Timestamp when entity was acquired from pool (for debugging/metrics).
    /// </summary>
    public long AcquiredAt;

    /// <summary>
    ///     Number of times this entity has been reused from the pool.
    /// </summary>
    public int ReuseCount;
}
