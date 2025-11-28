using Microsoft.Xna.Framework;

namespace PokeSharp.Game.Scripting.Api;

/// <summary>
///     Visual effects and particle system API for scripts.
///     Provides access to spawning particle effects, sprite-based effects, and shader effects.
/// </summary>
public interface IEffectApi
{
    /// <summary>
    ///     Spawn a visual effect at the specified position.
    /// </summary>
    /// <param name="effectId">The effect identifier or type.</param>
    /// <param name="position">World position in grid coordinates.</param>
    /// <param name="duration">Duration in seconds (0 for one-shot).</param>
    /// <param name="scale">Scale multiplier for the effect.</param>
    /// <param name="tint">Optional color tint.</param>
    void SpawnEffect(
        string effectId,
        Point position,
        float duration = 0.0f,
        float scale = 1.0f,
        Color? tint = null
    );

    /// <summary>
    ///     Clear all active effects.
    /// </summary>
    void ClearEffects();

    /// <summary>
    ///     Check if an effect type is registered.
    /// </summary>
    /// <param name="effectId">The effect identifier to check.</param>
    /// <returns>True if the effect exists, false otherwise.</returns>
    bool HasEffect(string effectId);
}
