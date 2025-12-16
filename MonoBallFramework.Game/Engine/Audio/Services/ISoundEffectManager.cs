namespace MonoBallFramework.Game.Engine.Audio.Services;

/// <summary>
///     Interface for sound effect management.
///     Provides OGG file playback with advanced control over volume, pitch, and pan.
///     Supports concurrent playback with automatic cleanup and resource management.
/// </summary>
public interface ISoundEffectManager : IDisposable
{
    /// <summary>
    ///     Gets or sets the master volume for all sound effects (0.0 to 1.0).
    ///     Value is automatically clamped to valid range.
    /// </summary>
    float MasterVolume { get; set; }

    /// <summary>
    ///     Gets the maximum number of concurrent sound effects allowed.
    ///     When limit is reached, oldest non-looping sounds are stopped automatically.
    /// </summary>
    int MaxConcurrentSounds { get; }

    /// <summary>
    ///     Gets the number of currently active sound instances.
    /// </summary>
    int ActiveSoundCount { get; }

    /// <summary>
    ///     Plays a one-shot sound effect by track ID from AudioRegistry.
    /// </summary>
    /// <param name="trackId">The track ID from AudioRegistry (e.g., "se_battle_hit").</param>
    /// <param name="volume">Volume multiplier (0.0 to 1.0, default: 1.0).</param>
    /// <param name="pitch">Pitch adjustment in semitones (-1.0 to 1.0, default: 0.0). Note: Pitch control requires resampling and may not be fully implemented.</param>
    /// <param name="pan">Stereo pan adjustment (-1.0 left to 1.0 right, default: 0.0 center).</param>
    /// <param name="priority">Sound priority level for eviction control (default: Normal).</param>
    /// <returns>True if the sound was played successfully; false if track not found or max concurrent limit reached.</returns>
    bool Play(string trackId, float volume = 1.0f, float pitch = 0.0f, float pan = 0.0f, SoundPriority priority = SoundPriority.Normal);

    /// <summary>
    ///     Plays a sound effect directly from a file path.
    /// </summary>
    /// <param name="filePath">Absolute or relative path to the OGG audio file.</param>
    /// <param name="volume">Volume multiplier (0.0 to 1.0, default: 1.0).</param>
    /// <param name="pitch">Pitch adjustment in semitones (-1.0 to 1.0, default: 0.0).</param>
    /// <param name="pan">Stereo pan adjustment (-1.0 left to 1.0 right, default: 0.0 center).</param>
    /// <param name="priority">Sound priority level for eviction control (default: Normal).</param>
    /// <returns>True if the sound was played successfully; false if file not found or playback failed.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the audio file does not exist.</exception>
    bool PlayFromFile(string filePath, float volume = 1.0f, float pitch = 0.0f, float pan = 0.0f, SoundPriority priority = SoundPriority.Normal);

    /// <summary>
    ///     Plays a looping sound effect by track ID and returns a control handle.
    /// </summary>
    /// <param name="trackId">The track ID from AudioRegistry.</param>
    /// <param name="volume">Volume multiplier (0.0 to 1.0, default: 1.0).</param>
    /// <param name="pitch">Pitch adjustment in semitones (-1.0 to 1.0, default: 0.0).</param>
    /// <param name="pan">Stereo pan adjustment (-1.0 left to 1.0 right, default: 0.0 center).</param>
    /// <param name="priority">Sound priority level for eviction control (default: Normal).</param>
    /// <returns>A handle to control the looping sound, or null if playback failed or max concurrent limit reached.</returns>
    ILoopingSoundHandle? PlayLooping(string trackId, float volume = 1.0f, float pitch = 0.0f, float pan = 0.0f, SoundPriority priority = SoundPriority.Normal);

    /// <summary>
    ///     Plays a looping sound effect from a file path and returns a control handle.
    /// </summary>
    /// <param name="filePath">Absolute or relative path to the OGG audio file.</param>
    /// <param name="volume">Volume multiplier (0.0 to 1.0, default: 1.0).</param>
    /// <param name="pitch">Pitch adjustment in semitones (-1.0 to 1.0, default: 0.0).</param>
    /// <param name="pan">Stereo pan adjustment (-1.0 left to 1.0 right, default: 0.0 center).</param>
    /// <param name="priority">Sound priority level for eviction control (default: Normal).</param>
    /// <returns>A handle to control the looping sound, or null if playback failed.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the audio file does not exist.</exception>
    ILoopingSoundHandle? PlayLoopingFromFile(string filePath, float volume = 1.0f, float pitch = 0.0f, float pan = 0.0f, SoundPriority priority = SoundPriority.Normal);

    /// <summary>
    ///     Updates the manager, cleaning up stopped sound instances and freeing resources.
    ///     Must be called once per frame from the game loop.
    /// </summary>
    void Update();

    /// <summary>
    ///     Stops all currently playing sound effects immediately.
    ///     Includes both one-shot and looping sounds.
    /// </summary>
    void StopAll();

    /// <summary>
    ///     Preloads audio files into cache for faster playback.
    ///     Note: Streaming implementations may not benefit from preloading.
    /// </summary>
    /// <param name="trackIds">Track IDs to preload from AudioRegistry.</param>
    void Preload(params string[] trackIds);

    /// <summary>
    ///     Gets statistics about the manager's current state.
    /// </summary>
    /// <returns>A tuple containing (active sound count, max concurrent limit).</returns>
    (int active, int max) GetStatistics();
}

/// <summary>
///     Handle for controlling a looping sound effect instance.
///     Provides real-time volume and pan control, as well as pause/resume functionality.
/// </summary>
public interface ILoopingSoundHandle : IDisposable
{
    /// <summary>
    ///     Gets whether the sound is currently playing (not stopped or disposed).
    /// </summary>
    bool IsPlaying { get; }

    /// <summary>
    ///     Gets or sets the volume of this specific sound instance (0.0 to 1.0).
    ///     Changes take effect immediately.
    /// </summary>
    float Volume { get; set; }

    /// <summary>
    ///     Gets or sets the stereo pan of this sound (-1.0 left to 1.0 right).
    ///     Changes take effect immediately.
    /// </summary>
    float Pan { get; set; }

    /// <summary>
    ///     Stops the looping sound and releases resources.
    ///     The handle cannot be reused after stopping.
    /// </summary>
    void Stop();

    /// <summary>
    ///     Pauses playback without releasing resources.
    ///     Can be resumed with Resume().
    /// </summary>
    void Pause();

    /// <summary>
    ///     Resumes paused playback from the current position.
    /// </summary>
    void Resume();
}

