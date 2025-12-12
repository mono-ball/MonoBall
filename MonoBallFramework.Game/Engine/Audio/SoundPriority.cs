namespace MonoBallFramework.Game.Engine.Audio;

/// <summary>
///     Defines priority levels for sound effects to control eviction when max concurrent sounds is reached.
///     Higher priority sounds are protected from being stopped when the system needs to make room for new sounds.
/// </summary>
public enum SoundPriority
{
    /// <summary>
    ///     Lowest priority - ambient sounds, background effects.
    ///     These sounds will be evicted first when capacity is reached.
    ///     Examples: distant birds, wind, water ambience.
    /// </summary>
    Background = 0,

    /// <summary>
    ///     Low priority - environmental effects and non-critical sounds.
    ///     Evicted after Background sounds.
    ///     Examples: footsteps, rustling grass, minor environmental feedback.
    /// </summary>
    Low = 1,

    /// <summary>
    ///     Normal priority - standard game sounds (default).
    ///     Most sound effects should use this priority.
    ///     Examples: door opening, item use, general interactions.
    /// </summary>
    Normal = 2,

    /// <summary>
    ///     High priority - important gameplay feedback sounds.
    ///     These sounds provide crucial feedback to the player.
    ///     Examples: item pickup, menu navigation, Pokemon capture success.
    /// </summary>
    High = 3,

    /// <summary>
    ///     Critical priority - essential gameplay sounds that should rarely be interrupted.
    ///     New sounds will be rejected rather than stopping Critical sounds.
    ///     Examples: battle move sounds, damage effects, Pokemon cries in battle.
    /// </summary>
    Critical = 4,

    /// <summary>
    ///     Maximum priority - UI sounds that must never be dropped.
    ///     New sounds will always be rejected rather than stopping UI sounds.
    ///     Examples: menu confirm/cancel, error beeps, system notifications.
    /// </summary>
    UI = 5
}
