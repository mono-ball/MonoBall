namespace MonoBallFramework.Game.Engine.Audio.Configuration;

/// <summary>
///     Configuration for audio system settings including volumes, paths, and behavior.
///     Provides sensible defaults for music, sound effects, and Pokémon cries.
/// </summary>
public class AudioConfiguration
{
    /// <summary>
    ///     Master volume level (0.0 to 1.0).
    ///     Default: 1.0 (100%).
    /// </summary>
    public float DefaultMasterVolume { get; set; } = AudioConstants.DefaultMasterVolume;

    /// <summary>
    ///     Default music volume level (0.0 to 1.0).
    ///     Default: 0.7 (70%).
    /// </summary>
    public float DefaultMusicVolume { get; set; } = AudioConstants.DefaultMusicVolume;

    /// <summary>
    ///     Default sound effects volume level (0.0 to 1.0).
    ///     Default: 0.9 (90%).
    /// </summary>
    public float DefaultSfxVolume { get; set; } = AudioConstants.DefaultSoundEffectVolume;

    /// <summary>
    ///     Default Pokémon cry volume level (0.0 to 1.0).
    ///     Default: 1.0 (100%).
    /// </summary>
    public float DefaultCryVolume { get; set; } = AudioConstants.DefaultCryVolume;

    /// <summary>
    ///     Maximum number of sounds that can play simultaneously.
    ///     Default: 32.
    /// </summary>
    public int MaxConcurrentSounds { get; set; } = AudioConstants.MaxConcurrentSounds;

    /// <summary>
    ///     Path to music files relative to Content directory.
    ///     Default: "Audio/Music".
    /// </summary>
    public string MusicPath { get; set; } = "Audio/Music";

    /// <summary>
    ///     Path to sound effect files relative to Content directory.
    ///     Default: "Audio/SFX".
    /// </summary>
    public string SfxPath { get; set; } = "Audio/SFX";

    /// <summary>
    ///     Path to Pokémon cry files relative to Content directory.
    ///     Default: "Audio/Cries".
    /// </summary>
    public string CryPath { get; set; } = "Audio/Cries";

    /// <summary>
    ///     Volume level to duck background music to when playing a Pokémon cry (0.0 to 1.0).
    ///     Default: 0.33 (33%, approximately 1/3 volume).
    /// </summary>
    public float CryDuckVolume { get; set; } = AudioConstants.CryDuckVolume;

    /// <summary>
    ///     Duration in seconds for the duck transition when playing a Pokémon cry.
    ///     Default: 0.1 seconds (100ms).
    /// </summary>
    public float CryDuckDurationSeconds { get; set; } = AudioConstants.CryDuckDuration;

    /// <summary>
    ///     Default duration in seconds for audio fade transitions.
    ///     Default: 1.0 second.
    /// </summary>
    public float DefaultFadeDurationSeconds { get; set; } = AudioConstants.DefaultCrossfadeDuration;

    /// <summary>
    ///     Audio buffer size in frames. Lower values reduce latency but may cause audio glitches.
    ///     Higher values increase latency but provide more stable playback.
    ///     Default: 1024 frames (~23ms latency at 44.1kHz).
    /// </summary>
    public int BufferSizeFrames { get; set; } = AudioConstants.DefaultBufferSizeFrames;

    /// <summary>
    ///     Default configuration with balanced audio settings.
    ///     Equivalent to Production configuration.
    /// </summary>
    public static AudioConfiguration Default => new();

    /// <summary>
    ///     Configuration optimized for production with balanced user-friendly defaults.
    /// </summary>
    public static AudioConfiguration Production =>
        new()
        {
            DefaultMasterVolume = AudioConstants.DefaultMasterVolume,
            DefaultMusicVolume = AudioConstants.DefaultMusicVolume,
            DefaultSfxVolume = AudioConstants.DefaultSoundEffectVolume,
            DefaultCryVolume = AudioConstants.DefaultCryVolume,
            MaxConcurrentSounds = AudioConstants.MaxConcurrentSounds,
            CryDuckVolume = AudioConstants.CryDuckVolume,
            CryDuckDurationSeconds = AudioConstants.CryDuckDuration,
            DefaultFadeDurationSeconds = AudioConstants.DefaultCrossfadeDuration
        };

    /// <summary>
    ///     Configuration optimized for development with full volumes for testing.
    ///     DEPRECATED: Use Production configuration instead. This preset will be removed in a future version.
    /// </summary>
    [Obsolete("Development preset is deprecated. Use Production or create a custom configuration.")]
    public static AudioConfiguration Development =>
        new()
        {
            DefaultMasterVolume = 1.0f,
            DefaultMusicVolume = 1.0f,
            DefaultSfxVolume = 1.0f,
            DefaultCryVolume = 1.0f,
            MaxConcurrentSounds = 64, // More concurrent sounds for testing
            CryDuckVolume = 0.25f, // More aggressive ducking for clarity
            CryDuckDurationSeconds = 0.05f, // Faster ducking for testing
            DefaultFadeDurationSeconds = 0.5f // Faster fades for iteration
        };

    /// <summary>
    ///     Factory method to create a default configuration instance.
    /// </summary>
    public static AudioConfiguration CreateDefault()
    {
        return new AudioConfiguration();
    }
}
