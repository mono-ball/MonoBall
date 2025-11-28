namespace PokeSharp.Engine.Rendering.Animation;

/// <summary>
///     Defines the available animation event types for common scenarios.
/// </summary>
public static class AnimationEventTypes
{
    /// <summary>
    ///     Event triggered when an attack/action makes contact.
    /// </summary>
    public const string Impact = "impact";

    /// <summary>
    ///     Event triggered when an animation completes.
    /// </summary>
    public const string Complete = "complete";

    /// <summary>
    ///     Event triggered at the start of an animation.
    /// </summary>
    public const string Start = "start";

    /// <summary>
    ///     Event triggered when a footstep should play.
    /// </summary>
    public const string Footstep = "footstep";

    /// <summary>
    ///     Event triggered when a jump starts.
    /// </summary>
    public const string JumpStart = "jump_start";

    /// <summary>
    ///     Event triggered when landing from a jump.
    /// </summary>
    public const string JumpLand = "jump_land";

    /// <summary>
    ///     Event triggered for custom gameplay logic.
    /// </summary>
    public const string Custom = "custom";
}
