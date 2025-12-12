using Microsoft.Xna.Framework.Audio;

namespace MonoBallFramework.Game.Engine.Audio.Services;

/// <summary>
///     Pooled sound effect instance manager to reduce allocations and improve performance.
///     Manages a pool of reusable SoundEffectInstance objects.
/// </summary>
public interface ISoundEffectPool : IDisposable
{
    /// <summary>
    ///     Gets the maximum number of concurrent sound effect instances allowed.
    /// </summary>
    int MaxConcurrentInstances { get; }

    /// <summary>
    ///     Gets the number of currently active sound instances.
    /// </summary>
    int ActiveInstanceCount { get; }

    /// <summary>
    ///     Gets the number of instances currently in the pool (not playing).
    /// </summary>
    int PooledInstanceCount { get; }

    /// <summary>
    ///     Plays a sound effect from the pool.
    /// </summary>
    /// <param name="soundEffect">The sound effect to play.</param>
    /// <param name="volume">Volume (0.0 to 1.0).</param>
    /// <param name="pitch">Pitch adjustment (-1.0 to 1.0).</param>
    /// <param name="pan">Pan adjustment (-1.0 left to 1.0 right).</param>
    /// <returns>The instance that was used, or null if pool is full.</returns>
    SoundEffectInstance? PlayPooled(SoundEffect soundEffect, float volume = 1.0f, float pitch = 0f, float pan = 0f);

    /// <summary>
    ///     Rents a looping sound instance from the pool.
    ///     Must be manually returned to the pool when done.
    /// </summary>
    /// <param name="soundEffect">The sound effect to create an instance for.</param>
    /// <param name="volume">Volume (0.0 to 1.0).</param>
    /// <returns>A looping sound instance, or null if pool is full.</returns>
    SoundEffectInstance? RentLoopingInstance(SoundEffect soundEffect, float volume = 1.0f);

    /// <summary>
    ///     Returns a rented instance back to the pool.
    /// </summary>
    /// <param name="instance">The instance to return.</param>
    void ReturnInstance(SoundEffectInstance instance);

    /// <summary>
    ///     Updates the pool, removing stopped instances and returning them to the available pool.
    /// </summary>
    void Update();

    /// <summary>
    ///     Stops all currently playing sound instances.
    /// </summary>
    void StopAll();

    /// <summary>
    ///     Clears the pool and disposes all instances.
    /// </summary>
    void Clear();

    /// <summary>
    ///     Gets statistics about the pool's current state.
    /// </summary>
    /// <returns>A tuple containing (active, pooled, total) instance counts.</returns>
    (int active, int pooled, int total) GetPoolStatistics();
}
