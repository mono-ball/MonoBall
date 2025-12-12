namespace MonoBallFramework.Game.Engine.Audio;

/// <summary>
///     Categorizes sounds for volume mixing and organization.
///     Each category can have independent volume control.
/// </summary>
public enum SoundCategory
{
    /// <summary>
    ///     User interface sounds (button clicks, menu navigation, notifications).
    /// </summary>
    UI,

    /// <summary>
    ///     NPC interactions and dialogue sounds.
    /// </summary>
    NPC,

    /// <summary>
    ///     Environmental ambient sounds (wind, water, background atmosphere).
    /// </summary>
    Environment,

    /// <summary>
    ///     Player and NPC footstep sounds.
    /// </summary>
    Footsteps,

    /// <summary>
    ///     Item-related sounds (pickup, use, drop).
    /// </summary>
    Items,

    /// <summary>
    ///     Battle-specific sounds (attacks, impacts, status effects).
    /// </summary>
    Battle,

    /// <summary>
    ///     Pokemon cries and Pokemon-specific sounds.
    /// </summary>
    Pokemon,

    /// <summary>
    ///     System-level sounds (save, load, error notifications).
    /// </summary>
    System
}
