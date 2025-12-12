using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Media;

namespace MonoBallFramework.Game.Engine.Audio.Services;

/// <summary>
///     Main audio service interface for managing sound effects and music playback.
///     Provides high-level control over the game's audio system with event-driven architecture.
///     Coordinates between music playback, sound effects, and audio events.
/// </summary>
public interface IAudioService : IDisposable
{
    /// <summary>
    ///     Gets or sets the master volume for all audio (0.0 to 1.0).
    ///     Affects both music and sound effects. Values are automatically clamped.
    /// </summary>
    float MasterVolume { get; set; }

    /// <summary>
    ///     Gets or sets the volume for sound effects (0.0 to 1.0).
    ///     Multiplied with master volume for final effect volume. Values are automatically clamped.
    /// </summary>
    float SoundEffectVolume { get; set; }

    /// <summary>
    ///     Gets or sets the volume for background music (0.0 to 1.0).
    ///     Multiplied with master volume for final music volume. Values are automatically clamped.
    /// </summary>
    float MusicVolume { get; set; }

    /// <summary>
    ///     Gets or sets whether all audio is muted.
    ///     When true, sets effective volume to zero without changing volume settings.
    /// </summary>
    bool IsMuted { get; set; }

    /// <summary>
    ///     Gets whether the audio system is initialized and ready for playback.
    /// </summary>
    bool IsInitialized { get; }

    /// <summary>
    ///     Initializes the audio system and subscribes to audio events.
    ///     Must be called during game initialization before any audio playback.
    /// </summary>
    void Initialize();

    /// <summary>
    ///     Updates the audio system state, processing fade effects and cleaning up stopped sounds.
    ///     Must be called once per frame from the game loop.
    /// </summary>
    /// <param name="deltaTime">Time elapsed since last update in seconds.</param>
    void Update(float deltaTime);

    /// <summary>
    ///     Plays a one-shot sound effect by name from the audio registry.
    /// </summary>
    /// <param name="soundName">The track identifier from the audio registry.</param>
    /// <param name="volume">Volume override (0.0 to 1.0), or null to use default from audio definition.</param>
    /// <param name="pitch">Pitch adjustment in semitones (-1.0 to 1.0), or null for no adjustment.</param>
    /// <param name="pan">Stereo pan adjustment (-1.0 left to 1.0 right), or null for center.</param>
    /// <returns>True if the sound was played successfully; false if not found or playback failed.</returns>
    bool PlaySound(string soundName, float? volume = null, float? pitch = null, float? pan = null);

    /// <summary>
    ///     Plays a looping sound effect by name and returns a control handle.
    /// </summary>
    /// <param name="soundName">The track identifier from the audio registry.</param>
    /// <param name="volume">Volume override (0.0 to 1.0), or null to use default from audio definition.</param>
    /// <returns>A sound instance handle for controlling playback, or null if playback failed.</returns>
    ILoopingSoundHandle? PlayLoopingSound(string soundName, float? volume = null);

    /// <summary>
    ///     Stops and disposes a looping sound effect instance.
    /// </summary>
    /// <param name="handle">The sound instance handle to stop. Null handles are safely ignored.</param>
    void StopLoopingSound(ILoopingSoundHandle handle);

    /// <summary>
    ///     Stops all currently playing sound effects immediately.
    ///     Includes both one-shot and looping sounds.
    /// </summary>
    void StopAllSounds();

    /// <summary>
    ///     Plays background music by name with automatic fade handling.
    ///     If music is already playing and fadeDuration > 0, performs sequential fade-out-then-play.
    /// </summary>
    /// <param name="musicName">The track identifier from the audio registry.</param>
    /// <param name="loop">Whether the music should loop continuously (default: true).</param>
    /// <param name="fadeDuration">Duration of fade-in effect in seconds (0 for instant playback).</param>
    void PlayMusic(string musicName, bool loop = true, float fadeDuration = 0f);

    /// <summary>
    ///     Stops the currently playing music with optional fade-out.
    /// </summary>
    /// <param name="fadeDuration">Duration of fade-out effect in seconds (0 for instant stop).</param>
    void StopMusic(float fadeDuration = 0f);

    /// <summary>
    ///     Pauses the currently playing music without unloading resources.
    ///     Can be resumed with ResumeMusic().
    /// </summary>
    void PauseMusic();

    /// <summary>
    ///     Resumes paused music playback from the current position.
    /// </summary>
    void ResumeMusic();

    /// <summary>
    ///     Gets whether music is currently playing (not paused or stopped).
    /// </summary>
    bool IsMusicPlaying { get; }

    /// <summary>
    ///     Gets the name of the currently playing music track, or null if no music is playing.
    /// </summary>
    string? CurrentMusicName { get; }

    /// <summary>
    ///     Preloads audio assets into memory for faster playback during gameplay.
    ///     Useful for loading frequently-used sounds or music before they are needed.
    /// </summary>
    /// <param name="assetNames">Array of track identifiers to preload from the audio registry.</param>
    void PreloadAssets(params string[] assetNames);

    /// <summary>
    ///     Unloads audio assets from memory to free resources.
    ///     Cannot unload currently playing tracks.
    /// </summary>
    /// <param name="assetNames">Array of track identifiers to unload.</param>
    void UnloadAssets(params string[] assetNames);

    /// <summary>
    ///     Clears all cached audio assets from memory.
    ///     Note: NAudio implementation uses streaming, so this may have minimal effect.
    /// </summary>
    void ClearCache();
}
