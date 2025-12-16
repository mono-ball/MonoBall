using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Engine.Audio.Configuration;
using MonoBallFramework.Game.Engine.Audio.Events;
using MonoBallFramework.Game.Engine.Core.Events;
using MonoBallFramework.Game.GameData.Entities;

namespace MonoBallFramework.Game.Engine.Audio.Services;

/// <summary>
///     Cross-platform audio service implementation.
///     Manages sound effects and music playback using PortAudio for cross-platform support.
/// </summary>
public class AudioService : IAudioService
{
    private readonly AudioRegistry _audioRegistry;
    private readonly ISoundEffectManager _soundEffectManager;
    private readonly IMusicPlayer _musicPlayer;
    private readonly IEventBus _eventBus;
    private readonly ILogger<AudioService>? _logger;
    private readonly AudioConfiguration _config;

    private readonly List<IDisposable> _subscriptions;
    private readonly Dictionary<ILoopingSoundHandle, string> _loopingSounds;

    private float _masterVolume = AudioConstants.DefaultMasterVolume;
    private float _soundEffectVolume = AudioConstants.DefaultSoundEffectVolume;
    private float _musicVolume = AudioConstants.DefaultMusicVolume;
    private bool _isMuted;
    private bool _isInitialized;
    private bool _disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AudioService"/> class.
    /// </summary>
    /// <param name="audioRegistry">Audio registry for track/sound lookup.</param>
    /// <param name="soundEffectManager">Sound effect manager.</param>
    /// <param name="musicPlayer">Music player.</param>
    /// <param name="eventBus">Event bus for subscribing to audio events.</param>
    /// <param name="config">Audio configuration settings (optional, uses default if null).</param>
    /// <param name="logger">Logger for diagnostic output (optional).</param>
    public AudioService(
        AudioRegistry audioRegistry,
        ISoundEffectManager soundEffectManager,
        IMusicPlayer musicPlayer,
        IEventBus eventBus,
        AudioConfiguration? config = null,
        ILogger<AudioService>? logger = null)
    {
        _audioRegistry = audioRegistry ?? throw new ArgumentNullException(nameof(audioRegistry));
        _soundEffectManager = soundEffectManager ?? throw new ArgumentNullException(nameof(soundEffectManager));
        _musicPlayer = musicPlayer ?? throw new ArgumentNullException(nameof(musicPlayer));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _logger = logger;
        _config = config ?? AudioConfiguration.Default;

        _subscriptions = new List<IDisposable>();
        _loopingSounds = new Dictionary<ILoopingSoundHandle, string>();

        // Apply configuration defaults
        _masterVolume = _config.DefaultMasterVolume;
        _soundEffectVolume = _config.DefaultSfxVolume;
        _musicVolume = _config.DefaultMusicVolume;

        _logger?.LogInformation(
            "AudioService created with master volume: {MasterVolume}, SFX: {SfxVolume}, Music: {MusicVolume}",
            _masterVolume, _soundEffectVolume, _musicVolume);
    }

    /// <summary>
    ///     Gets or sets the master volume for all audio (0.0 to 1.0).
    /// </summary>
    public float MasterVolume
    {
        get => _masterVolume;
        set
        {
            _masterVolume = Math.Clamp(value, AudioConstants.MinVolume, AudioConstants.MaxVolume);
            UpdateVolumes();
            _logger?.LogDebug("Master volume set to {Volume}", _masterVolume);
        }
    }

    /// <summary>
    ///     Gets or sets the volume for sound effects (0.0 to 1.0).
    /// </summary>
    public float SoundEffectVolume
    {
        get => _soundEffectVolume;
        set
        {
            _soundEffectVolume = Math.Clamp(value, AudioConstants.MinVolume, AudioConstants.MaxVolume);
            UpdateVolumes();
            _logger?.LogDebug("Sound effect volume set to {Volume}", _soundEffectVolume);
        }
    }

    /// <summary>
    ///     Gets or sets the volume for background music (0.0 to 1.0).
    /// </summary>
    public float MusicVolume
    {
        get => _musicVolume;
        set
        {
            _musicVolume = Math.Clamp(value, AudioConstants.MinVolume, AudioConstants.MaxVolume);
            _musicPlayer.Volume = GetEffectiveMusicVolume();
            _logger?.LogDebug("Music volume set to {Volume}", _musicVolume);
        }
    }

    /// <summary>
    ///     Gets or sets whether all audio is muted.
    /// </summary>
    public bool IsMuted
    {
        get => _isMuted;
        set
        {
            _isMuted = value;
            UpdateVolumes();
            _logger?.LogInformation("Audio muted: {IsMuted}", _isMuted);
        }
    }

    /// <summary>
    ///     Gets whether the audio system is initialized and ready.
    /// </summary>
    public bool IsInitialized => _isInitialized;

    /// <summary>
    ///     Gets whether music is currently playing.
    /// </summary>
    public bool IsMusicPlaying => _musicPlayer.IsPlaying;

    /// <summary>
    ///     Gets the name of the currently playing music track.
    /// </summary>
    public string? CurrentMusicName => _musicPlayer.CurrentTrack;

    /// <summary>
    ///     Initializes the audio system.
    ///     Should be called during game initialization.
    /// </summary>
    public void Initialize()
    {
        if (_isInitialized)
        {
            _logger?.LogWarning("AudioService already initialized");
            return;
        }

        // Subscribe to audio events
        SubscribeToEvents();

        // Set initial volumes
        UpdateVolumes();

        _isInitialized = true;
        _logger?.LogInformation("AudioService initialized successfully");
    }

    /// <summary>
    ///     Updates the audio system state.
    ///     Should be called once per frame to handle crossfading, pooling, etc.
    /// </summary>
    /// <param name="deltaTime">Time elapsed since last update in seconds.</param>
    public void Update(float deltaTime)
    {
        if (!_isInitialized || _disposed)
            return;

        _soundEffectManager.Update();
        _musicPlayer.Update(deltaTime);

        // Clean up stopped looping sounds
        CleanupLoopingSounds();
    }

    /// <summary>
    ///     Plays a sound effect by name.
    /// </summary>
    /// <param name="soundName">The name/path of the sound effect to play.</param>
    /// <param name="volume">Volume override (0.0 to 1.0), or null to use default.</param>
    /// <param name="pitch">Pitch adjustment (-1.0 to 1.0), or null for no adjustment.</param>
    /// <param name="pan">Pan adjustment (-1.0 to 1.0), or null for center.</param>
    /// <returns>True if the sound was played successfully.</returns>
    public bool PlaySound(string soundName, float? volume = null, float? pitch = null, float? pan = null)
    {
        if (!_isInitialized || _disposed)
        {
            _logger?.LogWarning("Cannot play sound: AudioService not initialized or disposed");
            return false;
        }

        try
        {
            var effectiveVolume = GetEffectiveSoundVolume(volume ?? 1.0f);
            var success = _soundEffectManager.Play(
                soundName,
                effectiveVolume,
                pitch ?? 0f,
                pan ?? 0f
            );

            if (success)
            {
                _logger?.LogTrace("Played sound: {SoundName} at volume {Volume}", soundName, effectiveVolume);
            }
            else
            {
                _logger?.LogWarning("Failed to play sound: {SoundName}", soundName);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error playing sound: {SoundName}", soundName);
            return false;
        }
    }

    /// <summary>
    ///     Plays a looping sound effect by name.
    /// </summary>
    /// <param name="soundName">The name/path of the sound effect to loop.</param>
    /// <param name="volume">Volume override (0.0 to 1.0), or null to use default.</param>
    /// <returns>A sound instance handle that can be used to stop the looping sound.</returns>
    public ILoopingSoundHandle? PlayLoopingSound(string soundName, float? volume = null)
    {
        if (!_isInitialized || _disposed)
        {
            _logger?.LogWarning("Cannot play looping sound: AudioService not initialized or disposed");
            return null;
        }

        try
        {
            var effectiveVolume = GetEffectiveSoundVolume(volume ?? 1.0f);
            var handle = _soundEffectManager.PlayLooping(soundName, effectiveVolume);

            if (handle != null)
            {
                _loopingSounds[handle] = soundName;
                _logger?.LogDebug("Started looping sound: {SoundName}", soundName);
                return handle;
            }

            _logger?.LogWarning("Failed to play looping sound: {SoundName}", soundName);
            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error playing looping sound: {SoundName}", soundName);
            return null;
        }
    }

    /// <summary>
    ///     Stops a looping sound effect instance.
    /// </summary>
    /// <param name="handle">The sound instance handle to stop.</param>
    public void StopLoopingSound(ILoopingSoundHandle handle)
    {
        if (handle == null || _disposed)
            return;

        try
        {
            handle.Stop();
            _loopingSounds.Remove(handle);
            handle.Dispose();
            _logger?.LogTrace("Stopped looping sound");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error stopping looping sound");
        }
    }

    /// <summary>
    ///     Stops all currently playing sound effects.
    /// </summary>
    public void StopAllSounds()
    {
        if (_disposed)
            return;

        // Stop all looping sounds
        foreach (var handle in _loopingSounds.Keys.ToList())
        {
            try
            {
                handle.Stop();
                handle.Dispose();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error stopping looping sound handle");
            }
        }
        _loopingSounds.Clear();

        // Stop all one-shot sounds
        _soundEffectManager.StopAll();
        _logger?.LogDebug("Stopped all sounds");
    }

    /// <summary>
    ///     Plays background music by name.
    /// </summary>
    /// <param name="musicName">The name/path of the music to play.</param>
    /// <param name="loop">Whether the music should loop.</param>
    /// <param name="fadeDuration">Duration of fade-in effect in seconds (0 for instant).</param>
    public void PlayMusic(string musicName, bool loop = true, float fadeDuration = 0f)
    {
        if (!_isInitialized || _disposed)
        {
            _logger?.LogWarning("Cannot play music: AudioService not initialized or disposed");
            return;
        }

        // Use pokeemerald-style sequential fade when music is already playing
        // (fade out old track completely, then start new track)
        _logger?.LogDebug("PlayMusic called: {MusicName}, IsMusicPlaying={IsPlaying}, fadeDuration={FadeDuration}",
            musicName, IsMusicPlaying, fadeDuration);

        if (IsMusicPlaying && fadeDuration > 0f)
        {
            // FadeOutAndPlay: fade out current track, then play new track immediately
            // Run on background thread to avoid blocking main thread during file I/O
            _logger?.LogInformation("FadeOutAndPlay: {MusicName} (loop: {Loop})", musicName, loop);
            _ = Task.Run(() =>
            {
                try
                {
                    _musicPlayer.FadeOutAndPlay(musicName, loop);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Background music fade-out-and-play task failed for {MusicName}", musicName);
                }
            });
        }
        else
        {
            // Run Play on background thread to avoid blocking main thread
            _logger?.LogInformation("Playing music: {MusicName} (loop: {Loop}, fade: {FadeDuration}s)",
                musicName, loop, fadeDuration);
            _ = Task.Run(() =>
            {
                try
                {
                    _musicPlayer.Play(musicName, loop, fadeDuration);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Background music play task failed for {MusicName}", musicName);
                }
            });
        }
    }

    /// <summary>
    ///     Stops the currently playing music.
    /// </summary>
    /// <param name="fadeDuration">Duration of fade-out effect in seconds (0 for instant).</param>
    public void StopMusic(float fadeDuration = 0f)
    {
        if (_disposed)
            return;

        _musicPlayer.Stop(fadeDuration);
        _logger?.LogDebug("Stopped music with fade duration: {FadeDuration}s", fadeDuration);
    }

    /// <summary>
    ///     Pauses the currently playing music.
    /// </summary>
    public void PauseMusic()
    {
        if (_disposed)
            return;

        _musicPlayer.Pause();
        _logger?.LogDebug("Paused music");
    }

    /// <summary>
    ///     Resumes paused music.
    /// </summary>
    public void ResumeMusic()
    {
        if (_disposed)
            return;

        _musicPlayer.Resume();
        _logger?.LogDebug("Resumed music");
    }

    /// <summary>
    ///     Preloads audio assets into memory for faster playback.
    /// </summary>
    /// <param name="assetNames">Array of asset names to preload.</param>
    public void PreloadAssets(params string[] assetNames)
    {
        if (_disposed)
            return;

        // Preload music tracks
        foreach (var assetName in assetNames)
        {
            var definition = _audioRegistry.GetByTrackId(assetName)
                          ?? _audioRegistry.GetById(assetName);

            if (definition != null)
            {
                if (definition.IsMusic)
                {
                    _musicPlayer.PreloadTrack(assetName);
                }
                else if (definition.IsSoundEffect)
                {
                    _soundEffectManager.Preload(assetName);
                }
            }
        }

        _logger?.LogInformation("Preloaded {Count} audio assets", assetNames.Length);
    }

    /// <summary>
    ///     Unloads audio assets from memory.
    /// </summary>
    /// <param name="assetNames">Array of asset names to unload.</param>
    public void UnloadAssets(params string[] assetNames)
    {
        if (_disposed)
            return;

        foreach (var assetName in assetNames)
        {
            var definition = _audioRegistry.GetByTrackId(assetName)
                          ?? _audioRegistry.GetById(assetName);

            if (definition != null && definition.IsMusic)
            {
                _musicPlayer.UnloadTrack(assetName);
            }
        }

        _logger?.LogDebug("Unloaded {Count} audio assets", assetNames.Length);
    }

    /// <summary>
    ///     Clears all cached audio assets from memory.
    /// </summary>
    public void ClearCache()
    {
        if (_disposed)
            return;

        // PortAudio implementations use streaming
        // This is kept for interface compatibility
        _logger?.LogInformation("ClearCache called (streaming implementation)");
    }

    /// <summary>
    ///     Disposes of audio resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        // Unsubscribe from all events
        foreach (var subscription in _subscriptions)
        {
            subscription.Dispose();
        }
        _subscriptions.Clear();

        // Stop all sounds
        StopAllSounds();

        // Dispose of managers
        _soundEffectManager.Dispose();
        _musicPlayer.Dispose();

        _disposed = true;
        _logger?.LogInformation("AudioService disposed");

        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Subscribes to audio-related events on the event bus.
    /// </summary>
    private void SubscribeToEvents()
    {
        // Subscribe to PlaySoundEvent
        var playSoundSubscription = _eventBus.Subscribe<PlaySoundEvent>(OnPlaySoundEvent);
        _subscriptions.Add(playSoundSubscription);

        // Subscribe to PlayMusicEvent
        var playMusicSubscription = _eventBus.Subscribe<PlayMusicEvent>(OnPlayMusicEvent);
        _subscriptions.Add(playMusicSubscription);

        // Subscribe to StopMusicEvent
        var stopMusicSubscription = _eventBus.Subscribe<StopMusicEvent>(OnStopMusicEvent);
        _subscriptions.Add(stopMusicSubscription);

        // Subscribe to PauseMusicEvent
        var pauseMusicSubscription = _eventBus.Subscribe<PauseMusicEvent>(OnPauseMusicEvent);
        _subscriptions.Add(pauseMusicSubscription);

        // Subscribe to ResumeMusicEvent
        var resumeMusicSubscription = _eventBus.Subscribe<ResumeMusicEvent>(OnResumeMusicEvent);
        _subscriptions.Add(resumeMusicSubscription);

        // Subscribe to StopAllSoundsEvent
        var stopAllSoundsSubscription = _eventBus.Subscribe<StopAllSoundsEvent>(OnStopAllSoundsEvent);
        _subscriptions.Add(stopAllSoundsSubscription);

        _logger?.LogDebug("Subscribed to audio events");
    }

    private void OnPlaySoundEvent(PlaySoundEvent evt)
    {
        if (string.IsNullOrEmpty(evt.SoundName))
        {
            _logger?.LogWarning("PlaySoundEvent received with empty sound name");
            return;
        }

        PlaySound(evt.SoundName, evt.Volume, evt.Pitch, evt.Pan);
    }

    private void OnPlayMusicEvent(PlayMusicEvent evt)
    {
        if (string.IsNullOrEmpty(evt.MusicName))
        {
            _logger?.LogWarning("PlayMusicEvent received with empty music name");
            return;
        }

        PlayMusic(evt.MusicName, evt.Loop, evt.FadeDuration);
    }

    private void OnStopMusicEvent(StopMusicEvent evt)
    {
        StopMusic(evt.FadeDuration);
    }

    private void OnPauseMusicEvent(PauseMusicEvent evt)
    {
        PauseMusic();
    }

    private void OnResumeMusicEvent(ResumeMusicEvent evt)
    {
        ResumeMusic();
    }

    private void OnStopAllSoundsEvent(StopAllSoundsEvent evt)
    {
        StopAllSounds();
    }

    /// <summary>
    ///     Updates all volume levels based on master volume and mute state.
    /// </summary>
    private void UpdateVolumes()
    {
        _soundEffectManager.MasterVolume = GetEffectiveSoundVolume(1.0f);
        _musicPlayer.Volume = GetEffectiveMusicVolume();
    }

    /// <summary>
    ///     Calculates the effective sound volume based on master, category, and mute state.
    /// </summary>
    private float GetEffectiveSoundVolume(float baseVolume)
    {
        return _isMuted ? AudioConstants.MinVolume : _masterVolume * _soundEffectVolume * baseVolume;
    }

    /// <summary>
    ///     Calculates the effective music volume based on master, category, and mute state.
    /// </summary>
    private float GetEffectiveMusicVolume()
    {
        return _isMuted ? AudioConstants.MinVolume : _masterVolume * _musicVolume;
    }

    /// <summary>
    ///     Cleans up stopped looping sounds from the tracking dictionary.
    /// </summary>
    private void CleanupLoopingSounds()
    {
        var stoppedHandles = _loopingSounds.Keys
            .Where(handle => !handle.IsPlaying)
            .ToList();

        foreach (var handle in stoppedHandles)
        {
            _loopingSounds.Remove(handle);
            handle.Dispose();
        }
    }
}

