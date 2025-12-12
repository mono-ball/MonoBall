using Microsoft.Xna.Framework.Audio;

namespace MonoBallFramework.Game.Ecs.Components.Audio;

/// <summary>
///     ECS component for attaching sound effects to entities.
///     Used for entity-specific sounds like footsteps, attacks, or interactions.
/// </summary>
public struct SoundEffectComponent
{
    /// <summary>
    ///     Gets or sets the name/path of the sound effect.
    /// </summary>
    public string SoundName { get; set; }

    /// <summary>
    ///     Gets or sets the volume (0.0 to 1.0).
    /// </summary>
    public float Volume { get; set; }

    /// <summary>
    ///     Gets or sets the pitch adjustment (-1.0 to 1.0).
    /// </summary>
    public float Pitch { get; set; }

    /// <summary>
    ///     Gets or sets whether the sound should loop.
    /// </summary>
    public bool IsLooping { get; set; }

    /// <summary>
    ///     Gets or sets whether the sound should play automatically on entity spawn.
    /// </summary>
    public bool PlayOnSpawn { get; set; }

    /// <summary>
    ///     Gets or sets whether the sound is currently playing.
    /// </summary>
    public bool IsPlaying { get; set; }

    /// <summary>
    ///     Gets or sets the currently active sound instance (for looping sounds).
    /// </summary>
    public SoundEffectInstance? ActiveInstance { get; set; }

    /// <summary>
    ///     Initializes a new sound effect component.
    /// </summary>
    /// <param name="soundName">The name/path of the sound.</param>
    /// <param name="volume">The volume (0.0 to 1.0).</param>
    /// <param name="isLooping">Whether the sound should loop.</param>
    public SoundEffectComponent(string soundName, float volume = 1.0f, bool isLooping = false)
    {
        SoundName = soundName;
        Volume = volume;
        Pitch = 0f;
        IsLooping = isLooping;
        PlayOnSpawn = false;
        IsPlaying = false;
        ActiveInstance = null;
    }
}
