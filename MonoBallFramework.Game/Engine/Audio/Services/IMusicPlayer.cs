using Microsoft.Xna.Framework.Media;

namespace MonoBallFramework.Game.Engine.Audio.Services;

/// <summary>
///     Background music player with support for streaming, crossfading, and playlists.
///     Manages music playback with advanced fade effects and seamless transitions.
/// </summary>
public interface IMusicPlayer : IDisposable
{
    /// <summary>
    ///     Gets or sets the music volume (0.0 to 1.0).
    ///     Values outside this range will be clamped.
    /// </summary>
    float Volume { get; set; }

    /// <summary>
    ///     Gets whether music is currently playing.
    /// </summary>
    bool IsPlaying { get; }

    /// <summary>
    ///     Gets whether music is currently paused.
    /// </summary>
    bool IsPaused { get; }

    /// <summary>
    ///     Gets the name of the currently playing track, or null if no track is playing.
    /// </summary>
    string? CurrentTrack { get; }

    /// <summary>
    ///     Gets whether the player is currently crossfading between tracks.
    /// </summary>
    bool IsCrossfading { get; }

    /// <summary>
    ///     Plays a music track with optional looping and fade-in.
    /// </summary>
    /// <param name="trackName">The track identifier from the audio registry.</param>
    /// <param name="loop">Whether to loop the track after completion (default: true).</param>
    /// <param name="fadeInDuration">Duration of fade-in effect in seconds (0 for instant playback).</param>
    /// <exception cref="ArgumentException">Thrown when trackName is not found in the registry.</exception>
    void Play(string trackName, bool loop = true, float fadeInDuration = 0f);

    /// <summary>
    ///     Stops the currently playing music with optional fade-out.
    /// </summary>
    /// <param name="fadeOutDuration">Duration of fade-out effect in seconds (0 for instant stop).</param>
    void Stop(float fadeOutDuration = 0f);

    /// <summary>
    ///     Pauses the currently playing music without unloading resources.
    /// </summary>
    void Pause();

    /// <summary>
    ///     Resumes paused music playback from the current position.
    /// </summary>
    void Resume();

    /// <summary>
    ///     Crossfades from the current track to a new track with simultaneous fade-out and fade-in.
    ///     Provides smooth transition between two tracks without silence.
    /// </summary>
    /// <param name="newTrackName">The track identifier for the new track.</param>
    /// <param name="crossfadeDuration">Duration of the crossfade effect in seconds (default: 1.0s).</param>
    /// <param name="loop">Whether the new track should loop (default: true).</param>
    void Crossfade(string newTrackName, float crossfadeDuration = 1.0f, bool loop = true);

    /// <summary>
    ///     Fades out current track completely, then plays new track immediately without fade-in.
    ///     Implements pokeemerald-style sequential transition (state 6).
    ///     Uses the audio definition's fadeOut duration for the current track.
    /// </summary>
    /// <param name="newTrackName">The track identifier for the new track.</param>
    /// <param name="loop">Whether the new track should loop (default: true).</param>
    void FadeOutAndPlay(string newTrackName, bool loop = true);

    /// <summary>
    ///     Fades out current track, then fades in new track sequentially.
    ///     Implements pokeemerald-style transition for bike music and special events (state 7).
    ///     Uses the current track's fadeOut duration and new track's fadeIn duration from audio definitions.
    /// </summary>
    /// <param name="newTrackName">The track identifier for the new track.</param>
    /// <param name="loop">Whether the new track should loop (default: true).</param>
    void FadeOutAndFadeIn(string newTrackName, bool loop = true);

    /// <summary>
    ///     Updates the music player state, processing fade effects and crossfading.
    ///     Must be called once per frame from the game loop.
    /// </summary>
    /// <param name="deltaTime">Time elapsed since last update in seconds.</param>
    void Update(float deltaTime);

    /// <summary>
    ///     Preloads a music track into memory or cache for faster playback.
    ///     Useful for reducing lag when starting music during gameplay.
    /// </summary>
    /// <param name="trackName">The track identifier to preload.</param>
    void PreloadTrack(string trackName);

    /// <summary>
    ///     Unloads a music track from memory to free resources.
    ///     Cannot unload currently playing tracks.
    /// </summary>
    /// <param name="trackName">The track identifier to unload.</param>
    void UnloadTrack(string trackName);
}
