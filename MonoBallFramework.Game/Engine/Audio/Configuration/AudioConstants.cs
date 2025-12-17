namespace MonoBallFramework.Game.Engine.Audio.Configuration;

/// <summary>
///     Audio system constants and default values.
///     These values define the standard audio settings used throughout the game.
/// </summary>
public static class AudioConstants
{
    /// <summary>
    ///     Default master volume (0.0 - 1.0).
    ///     This is the baseline volume before any category-specific adjustments.
    /// </summary>
    public const float DefaultMasterVolume = 1.0f;

    /// <summary>
    ///     Default sound effect volume - slightly below max to leave headroom for mixing.
    ///     Sound effects are kept loud enough to be prominent but allow music to be heard.
    /// </summary>
    public const float DefaultSoundEffectVolume = 0.9f;

    /// <summary>
    ///     Default music volume - lower than SFX so effects are audible over background music.
    ///     This creates a balanced audio mix where music provides atmosphere without overwhelming gameplay sounds.
    /// </summary>
    public const float DefaultMusicVolume = 0.7f;

    /// <summary>
    ///     Default Pokemon cry volume.
    ///     Cries are important for gameplay feedback and are kept at full volume.
    /// </summary>
    public const float DefaultCryVolume = 1.0f;

    /// <summary>
    ///     Maximum concurrent sound effects to prevent audio overload and performance issues.
    ///     This limit ensures the audio system doesn't consume excessive resources while still
    ///     allowing enough simultaneous sounds for rich audio environments.
    /// </summary>
    public const int MaxConcurrentSounds = 32;

    /// <summary>
    ///     Default crossfade duration in seconds.
    ///     This is used when transitioning between music tracks to create smooth audio changes.
    /// </summary>
    public const float DefaultCrossfadeDuration = 1.0f;

    /// <summary>
    ///     Default fallback fade duration in seconds.
    ///     Used when a track definition doesn't specify a fade duration.
    /// </summary>
    public const float DefaultFallbackFadeDuration = 0.5f;

    /// <summary>
    ///     Volume level to duck background music to when playing a Pokemon cry (0.0 to 1.0).
    ///     This is approximately 1/3 volume to ensure the cry is clearly audible.
    /// </summary>
    public const float CryDuckVolume = 0.33f;

    /// <summary>
    ///     Duration in seconds for the duck transition when playing a Pokemon cry.
    ///     A quick transition ensures responsive audio feedback.
    /// </summary>
    public const float CryDuckDuration = 0.1f;

    /// <summary>
    ///     Minimum volume clamp value.
    ///     Volume values cannot go below this to prevent completely silent audio.
    /// </summary>
    public const float MinVolume = 0.0f;

    /// <summary>
    ///     Maximum volume clamp value.
    ///     Volume values cannot exceed this to prevent audio distortion.
    /// </summary>
    public const float MaxVolume = 1.0f;

    /// <summary>
    ///     Minimum pitch adjustment value.
    ///     Pitch adjustments range from -1.0 (one octave down) to +1.0 (one octave up).
    /// </summary>
    public const float MinPitch = -1.0f;

    /// <summary>
    ///     Maximum pitch adjustment value.
    /// </summary>
    public const float MaxPitch = 1.0f;

    /// <summary>
    ///     Minimum pan value (full left).
    ///     Pan values range from -1.0 (left) to +1.0 (right).
    /// </summary>
    public const float MinPan = -1.0f;

    /// <summary>
    ///     Maximum pan value (full right).
    /// </summary>
    public const float MaxPan = 1.0f;

    /// <summary>
    ///     Default audio buffer size in frames. Lower = less latency, higher = more stable.
    ///     1024 frames at 44.1kHz = ~23ms latency.
    /// </summary>
    public const int DefaultBufferSizeFrames = 1024;

    /// <summary>
    ///     Minimum buffer size (very low latency, may cause glitches).
    /// </summary>
    public const int MinBufferSizeFrames = 256;

    /// <summary>
    ///     Maximum buffer size (high latency, very stable).
    /// </summary>
    public const int MaxBufferSizeFrames = 4096;

    /// <summary>
    ///     Clamps a volume value to the valid range [MinVolume, MaxVolume].
    /// </summary>
    /// <param name="value">The volume value to clamp.</param>
    /// <returns>The clamped volume value.</returns>
    public static float ClampVolume(float value)
    {
        return Math.Clamp(value, MinVolume, MaxVolume);
    }

    /// <summary>
    ///     Clamps a pan value to the valid range [MinPan, MaxPan].
    /// </summary>
    /// <param name="value">The pan value to clamp.</param>
    /// <returns>The clamped pan value.</returns>
    public static float ClampPan(float value)
    {
        return Math.Clamp(value, MinPan, MaxPan);
    }
}
