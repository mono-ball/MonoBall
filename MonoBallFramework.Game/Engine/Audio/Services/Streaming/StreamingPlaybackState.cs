using MonoBallFramework.Game.Engine.Audio.Core;

namespace MonoBallFramework.Game.Engine.Audio.Services.Streaming;

/// <summary>
/// Represents the playback state for a streaming music track.
/// Manages the streaming provider lifecycle and playback metadata.
/// </summary>
public class StreamingPlaybackState : IDisposable, IFadingPlayback
{
    private bool _disposed;

    /// <summary>
    /// Gets the track name being played.
    /// </summary>
    public required string TrackName { get; init; }

    /// <summary>
    /// Gets whether the track should loop.
    /// </summary>
    public bool Loop { get; init; }

    /// <summary>
    /// Gets or sets the current fade state.
    /// </summary>
    public FadeState FadeState { get; set; }

    /// <summary>
    /// Gets or sets the fade duration in seconds.
    /// </summary>
    public float FadeDuration { get; set; }

    /// <summary>
    /// Gets or sets the fade timer (elapsed time in current fade).
    /// </summary>
    public float FadeTimer { get; set; }

    /// <summary>
    /// Gets or sets the current volume (0.0 - 1.0).
    /// </summary>
    public float CurrentVolume { get; set; }

    /// <summary>
    /// Gets or sets the target volume (0.0 - 1.0).
    /// </summary>
    public float TargetVolume { get; set; }

    /// <summary>
    /// Gets or sets the volume at crossfade start (for crossfading).
    /// </summary>
    public float CrossfadeStartVolume { get; set; }

    /// <summary>
    /// Gets or sets the volume sample provider (for volume control).
    /// </summary>
    public VolumeSampleProvider? VolumeProvider { get; set; }

    /// <summary>
    /// Gets the fade-out duration from the audio definition (for crossfades).
    /// </summary>
    public float DefinitionFadeOut { get; init; }

    /// <summary>
    /// Gets or sets the streaming loop provider (owns the underlying streaming provider).
    /// IMPORTANT: This must be disposed when playback stops.
    /// </summary>
    public StreamingLoopProvider? StreamingProvider { get; set; }

    /// <summary>
    /// Disposes the streaming provider and releases resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Dispose the streaming provider (which also disposes the underlying VorbisReader)
        StreamingProvider?.Dispose();
        StreamingProvider = null;

        // VolumeProvider doesn't need disposal (it's a wrapper)
        VolumeProvider = null;

        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Enumeration of fade states for music playback.
/// </summary>
public enum FadeState
{
    None,
    FadingIn,
    FadingOut,
    /// <summary>Fading out then will play new track immediately (pokeemerald state 6)</summary>
    FadingOutThenPlay,
    /// <summary>Fading out then will fade in new track (pokeemerald state 7)</summary>
    FadingOutThenFadeIn,
    Crossfading
}
