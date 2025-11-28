using Microsoft.Xna.Framework;

namespace PokeSharp.Engine.Core.Types.Events;

/// <summary>
///     Event raised when a script requests a visual effect to be spawned.
///     This is caught by the rendering system (PokeSharp.Rendering/PokeSharp.Game)
///     which is responsible for rendering the particle effects and animations.
/// </summary>
public sealed record EffectRequestedEvent : TypeEventBase
{
    /// <summary>
    ///     The effect identifier or type (e.g., "explosion", "sparkle", "smoke").
    /// </summary>
    public required string EffectId { get; init; }

    /// <summary>
    ///     World position in grid coordinates where the effect should appear.
    /// </summary>
    public required Point Position { get; init; }

    /// <summary>
    ///     Duration in seconds. 0 means one-shot effect (play once and destroy).
    /// </summary>
    public float Duration { get; init; } = 0.0f;

    /// <summary>
    ///     Scale multiplier for the effect (1.0 = normal size).
    /// </summary>
    public float Scale { get; init; } = 1.0f;

    /// <summary>
    ///     Optional color tint applied to the effect.
    /// </summary>
    public Color? Tint { get; init; }
}
