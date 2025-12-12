namespace PokeSharp.Tests.Audio.Utilities.Interfaces;

/// <summary>
/// Main interface for audio service operations
/// </summary>
public interface IAudioService : IDisposable
{
    // Sound Effects Management
    ISoundEffect LoadSound(string path);
    void UnloadSound(string path);
    void UnloadAllSounds();
    bool IsCached(string path);
    int GetCachedSoundCount();

    // Sound Playback
    ISoundInstance? PlaySound(string path, SoundPriority priority = SoundPriority.Normal, bool loop = false);
    ISoundInstance? PlayPokemonCry(string species, float pitch = 1.0f);
    void StopAllSounds();
    int GetActiveSoundCount();
    List<ISoundInstance> GetActiveSoundsByPriority();

    // Music Management
    IMusicPlayer GetMusicPlayer();
    IMusic LoadMusic(string path, bool streaming = false);

    // Volume Control
    void SetMasterVolume(float volume);
    void SetMusicVolume(float volume);
    void SetSfxVolume(float volume);
    float GetMasterVolume();
    float GetMusicVolume();
    float GetSfxVolume();

    // Audio Ducking
    void DuckMusic(float targetVolume, TimeSpan duration);
    void UnduckMusic(TimeSpan duration);

    // Pause/Resume
    void PauseAll();
    void ResumeAll();

    // Update
    void Update(TimeSpan deltaTime);

    // Events
    event Action<string, SoundPriority>? OnSoundPlayed;
    event Action<string>? OnSoundStopped;
    event Action<string>? OnMusicStarted;
    event Action<string>? OnMusicStopped;
    event Action<VolumeChannel, float>? OnVolumeChanged;

    // State
    bool IsDisposed { get; }
}

/// <summary>
/// Represents a loaded sound effect
/// </summary>
public interface ISoundEffect : IDisposable
{
    string Name { get; }
    TimeSpan Duration { get; }
    ISoundInstance CreateInstance();
}

/// <summary>
/// Represents a playing sound instance
/// </summary>
public interface ISoundInstance : IDisposable
{
    string AudioPath { get; }
    bool IsPlaying { get; }
    bool IsPaused { get; }
    float Volume { get; set; }
    float Pitch { get; set; }
    float Pan { get; set; }
    bool IsLooped { get; set; }
    SoundPriority Priority { get; set; }

    void Play();
    void Pause();
    void Resume();
    void Stop();
}

/// <summary>
/// Music player interface
/// </summary>
public interface IMusicPlayer
{
    MusicState State { get; }
    string? CurrentTrack { get; }
    bool IsPlaying { get; }
    float Volume { get; }
    TimeSpan Position { get; }
    TimeSpan Duration { get; }

    void Play(string musicPath, bool loop = true, TimeSpan? fadeIn = null, LoopPoints? loopPoints = null);
    void Stop();
    void Pause();
    void Resume();
    void SetVolume(float volume);
    void SetLooping(bool loop);

    void FadeOut(TimeSpan duration);
    void FadeIn(TimeSpan duration);
    void CrossFade(string newMusicPath, TimeSpan duration);
    void CancelFade();

    event Action? OnLoop;
    event Action? OnTrackEnd;
}

/// <summary>
/// Loaded music track
/// </summary>
public interface IMusic : IDisposable
{
    string Name { get; }
    TimeSpan Duration { get; }
    bool IsStreaming { get; }
}

/// <summary>
/// Content manager for loading assets
/// </summary>
public interface IContentManager
{
    T Load<T>(string assetName) where T : class;
    void Unload();
}

/// <summary>
/// Sound priority levels
/// </summary>
public enum SoundPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Critical = 3
}

/// <summary>
/// Music player states
/// </summary>
public enum MusicState
{
    Stopped,
    Playing,
    Paused,
    FadingIn,
    FadingOut
}

/// <summary>
/// Volume channels
/// </summary>
public enum VolumeChannel
{
    Master,
    Music,
    SFX
}

/// <summary>
/// Loop point configuration
/// </summary>
public class LoopPoints
{
    public TimeSpan LoopStart { get; set; }
    public TimeSpan LoopEnd { get; set; }
}
