using Microsoft.Xna.Framework;

namespace MonoBallFramework.Game.Ecs.Components.Audio;

/// <summary>
///     ECS component for environmental ambient sounds attached to zones or areas.
///     Used for location-based audio like cave ambience, water sounds, or weather effects.
/// </summary>
public struct AmbientSoundComponent
{
    /// <summary>
    ///     Gets or sets the name/path of the ambient sound.
    /// </summary>
    public string SoundName { get; set; }

    /// <summary>
    ///     Gets or sets the base volume (0.0 to 1.0).
    /// </summary>
    public float BaseVolume { get; set; }

    /// <summary>
    ///     Gets or sets the current volume (affected by distance/conditions).
    /// </summary>
    public float CurrentVolume { get; set; }

    /// <summary>
    ///     Gets or sets the maximum distance at which the sound can be heard (in tiles).
    ///     0 means infinite range (global ambient).
    /// </summary>
    public float MaxDistance { get; set; }

    /// <summary>
    ///     Gets or sets the zone bounds for this ambient sound.
    ///     If set, sound only plays when player is within these bounds.
    /// </summary>
    public Rectangle? ZoneBounds { get; set; }

    /// <summary>
    ///     Gets or sets whether this ambient sound is currently active.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    ///     Gets or sets whether the sound should fade in/out based on zone entry/exit.
    /// </summary>
    public bool UseFading { get; set; }

    /// <summary>
    ///     Gets or sets the fade duration in seconds.
    /// </summary>
    public float FadeDuration { get; set; }

    /// <summary>
    ///     Initializes a new ambient sound component.
    /// </summary>
    /// <param name="soundName">The name/path of the ambient sound.</param>
    /// <param name="volume">The base volume (0.0 to 1.0).</param>
    /// <param name="maxDistance">Maximum audible distance in tiles (0 for infinite).</param>
    public AmbientSoundComponent(string soundName, float volume = 1.0f, float maxDistance = 0f)
    {
        SoundName = soundName;
        BaseVolume = volume;
        CurrentVolume = volume;
        MaxDistance = maxDistance;
        ZoneBounds = null;
        IsActive = true;
        UseFading = true;
        FadeDuration = 1.0f;
    }
}
