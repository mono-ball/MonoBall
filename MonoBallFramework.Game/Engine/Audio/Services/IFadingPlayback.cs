using MonoBallFramework.Game.Engine.Audio.Services.Streaming;
using NAudio.Wave.SampleProviders;

namespace MonoBallFramework.Game.Engine.Audio.Services;

/// <summary>
/// Interface for playback states that support fade operations.
/// Implemented by both cached and streaming music playback states.
/// </summary>
public interface IFadingPlayback
{
    /// <summary>
    /// Gets the track name being played.
    /// </summary>
    string TrackName { get; }

    /// <summary>
    /// Gets or sets the current fade state.
    /// </summary>
    FadeState FadeState { get; set; }

    /// <summary>
    /// Gets or sets the fade duration in seconds.
    /// </summary>
    float FadeDuration { get; set; }

    /// <summary>
    /// Gets or sets the fade timer (elapsed time in current fade).
    /// </summary>
    float FadeTimer { get; set; }

    /// <summary>
    /// Gets or sets the current volume (0.0 - 1.0).
    /// </summary>
    float CurrentVolume { get; set; }

    /// <summary>
    /// Gets the target volume (0.0 - 1.0).
    /// </summary>
    float TargetVolume { get; }

    /// <summary>
    /// Gets the volume at crossfade start (for crossfading).
    /// </summary>
    float CrossfadeStartVolume { get; }

    /// <summary>
    /// Gets the volume sample provider (for volume control).
    /// </summary>
    VolumeSampleProvider? VolumeProvider { get; }
}
