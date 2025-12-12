namespace MonoBallFramework.Game.Engine.Audio.Services;

/// <summary>
///     Service interface for managing audio volume levels.
///     Provides focused control over master, music, and sound effect volumes.
/// </summary>
public interface IVolumeControl
{
    /// <summary>
    ///     Gets or sets the master volume for all audio (0.0 to 1.0).
    ///     Affects both music and sound effects. Values are automatically clamped.
    /// </summary>
    float MasterVolume { get; set; }

    /// <summary>
    ///     Gets or sets the volume for background music (0.0 to 1.0).
    ///     Multiplied with master volume for final music volume. Values are automatically clamped.
    /// </summary>
    float MusicVolume { get; set; }

    /// <summary>
    ///     Gets or sets the volume for sound effects (0.0 to 1.0).
    ///     Multiplied with master volume for final effect volume. Values are automatically clamped.
    /// </summary>
    float SfxVolume { get; set; }

    /// <summary>
    ///     Gets or sets whether all audio is muted.
    ///     When true, sets effective volume to zero without changing volume settings.
    /// </summary>
    bool IsMuted { get; set; }
}
