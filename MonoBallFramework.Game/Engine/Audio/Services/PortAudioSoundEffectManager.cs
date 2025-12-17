using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Engine.Audio.Configuration;
using MonoBallFramework.Game.Engine.Audio.Core;
using MonoBallFramework.Game.Engine.Content;
using MonoBallFramework.Game.GameData.Entities;

namespace MonoBallFramework.Game.Engine.Audio.Services;

/// <summary>
///     PortAudio-based sound effect manager for playing OGG sound effects.
///     Supports concurrent playback, looping, and advanced audio control.
///     Cross-platform implementation using PortAudioSharp2 and NVorbis.
/// </summary>
public class PortAudioSoundEffectManager : ISoundEffectManager
{
    private readonly ConcurrentDictionary<Guid, SoundInstance> _activeSounds;
    private readonly AudioRegistry _audioRegistry;
    private readonly IContentProvider _contentProvider;
    private readonly object _lock = new();
    private readonly ILogger<PortAudioSoundEffectManager>? _logger;
    private readonly AudioFormat _mixerFormat;
    private bool _disposed;
    private float _masterVolume = AudioConstants.DefaultMasterVolume;
    private AudioMixer? _mixer;
    private PortAudioOutput? _outputDevice;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PortAudioSoundEffectManager" /> class.
    /// </summary>
    /// <param name="audioRegistry">The audio registry for looking up sound definitions.</param>
    /// <param name="contentProvider">The content provider for resolving asset paths with mod support.</param>
    /// <param name="maxConcurrentSounds">
    ///     Maximum number of concurrent sounds (uses AudioConstants.MaxConcurrentSounds if not
    ///     specified).
    /// </param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public PortAudioSoundEffectManager(
        AudioRegistry audioRegistry,
        IContentProvider contentProvider,
        int maxConcurrentSounds = AudioConstants.MaxConcurrentSounds,
        ILogger<PortAudioSoundEffectManager>? logger = null)
    {
        _audioRegistry = audioRegistry ?? throw new ArgumentNullException(nameof(audioRegistry));
        _contentProvider = contentProvider ?? throw new ArgumentNullException(nameof(contentProvider));
        _logger = logger;

        if (maxConcurrentSounds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxConcurrentSounds));
        }

        MaxConcurrentSounds = maxConcurrentSounds;
        _activeSounds = new ConcurrentDictionary<Guid, SoundInstance>();

        // Initialize mixer format (44100Hz stereo - standard for audio playback)
        _mixerFormat = new AudioFormat(44100, 2);
    }

    public float MasterVolume
    {
        get => _masterVolume;
        set => _masterVolume = Math.Clamp(value, AudioConstants.MinVolume, AudioConstants.MaxVolume);
    }

    public int MaxConcurrentSounds { get; }

    public int ActiveSoundCount => _activeSounds.Count;

    public bool Play(string trackId, float volume = 1.0f, float pitch = 0.0f, float pan = 0.0f,
        SoundPriority priority = SoundPriority.Normal)
    {
        if (_disposed || string.IsNullOrEmpty(trackId))
        {
            return false;
        }

        // Look up audio definition by track ID
        AudioEntity? definition = _audioRegistry.GetByTrackId(trackId);
        if (definition == null)
        {
            _logger?.LogWarning("Audio definition not found for track ID: {TrackId}", trackId);
            return false;
        }

        // Resolve full path using content provider (supports mods)
        // AudioPath includes content type prefix (e.g., "Audio/SFX/..."), so use "Root"
        string? fullPath = _contentProvider.ResolveContentPath("Root", definition.AudioPath);
        if (fullPath == null)
        {
            _logger?.LogWarning("Audio file not found: {AudioPath}", definition.AudioPath);
            return false;
        }

        return PlayFromFile(fullPath, volume, pitch, pan, priority);
    }

    public bool PlayFromFile(string filePath, float volume = 1.0f, float pitch = 0.0f, float pan = 0.0f,
        SoundPriority priority = SoundPriority.Normal)
    {
        if (_disposed || string.IsNullOrEmpty(filePath))
        {
            return false;
        }

        if (!File.Exists(filePath))
        {
            _logger?.LogWarning("Audio file not found: {FilePath}", filePath);
            return false;
        }

        // Check if we've hit the concurrent sound limit
        if (_activeSounds.Count >= MaxConcurrentSounds)
        {
            // Try to evict a lower priority sound
            if (!TryEvictLowerPrioritySound(priority))
            {
                _logger?.LogWarning("Max concurrent sounds reached, cannot evict for priority {Priority}", priority);
                return false;
            }
        }

        try
        {
            // Ensure mixer is initialized before creating sound instances
            EnsureMixerInitialized();

            var soundInstance = new SoundInstance(
                filePath,
                false,
                volume * _masterVolume,
                pitch,
                pan,
                priority,
                _logger,
                this,
                _mixer!,
                _mixerFormat);

            _activeSounds.TryAdd(soundInstance.Id, soundInstance);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to play sound from file: {FilePath}", filePath);
            return false;
        }
    }

    public ILoopingSoundHandle? PlayLooping(string trackId, float volume = 1.0f, float pitch = 0.0f, float pan = 0.0f,
        SoundPriority priority = SoundPriority.Normal)
    {
        if (_disposed || string.IsNullOrEmpty(trackId))
        {
            return null;
        }

        AudioEntity? definition = _audioRegistry.GetByTrackId(trackId);
        if (definition == null)
        {
            _logger?.LogWarning("Audio definition not found for track ID: {TrackId}", trackId);
            return null;
        }

        // Resolve full path using content provider (supports mods)
        string? fullPath = _contentProvider.ResolveContentPath("Root", definition.AudioPath);
        if (fullPath == null)
        {
            _logger?.LogWarning("Audio file not found: {AudioPath}", definition.AudioPath);
            return null;
        }

        return PlayLoopingFromFile(fullPath, volume, pitch, pan, priority);
    }

    public ILoopingSoundHandle? PlayLoopingFromFile(string filePath, float volume = 1.0f, float pitch = 0.0f,
        float pan = 0.0f, SoundPriority priority = SoundPriority.Normal)
    {
        if (_disposed || string.IsNullOrEmpty(filePath))
        {
            return null;
        }

        if (!File.Exists(filePath))
        {
            _logger?.LogWarning("Audio file not found: {FilePath}", filePath);
            return null;
        }

        if (_activeSounds.Count >= MaxConcurrentSounds)
        {
            if (!TryEvictLowerPrioritySound(priority))
            {
                _logger?.LogWarning("Max concurrent sounds reached, cannot evict for priority {Priority}", priority);
                return null;
            }
        }

        try
        {
            // Ensure mixer is initialized before creating sound instances
            EnsureMixerInitialized();

            var soundInstance = new SoundInstance(
                filePath,
                true,
                volume * _masterVolume,
                pitch,
                pan,
                priority,
                _logger,
                this,
                _mixer!,
                _mixerFormat);

            _activeSounds.TryAdd(soundInstance.Id, soundInstance);
            return new LoopingSoundHandle(soundInstance, this);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to play looping sound from file: {FilePath}", filePath);
            return null;
        }
    }

    public void Update()
    {
        if (_disposed)
        {
            return;
        }

        // Clean up stopped sounds - no LINQ, direct iteration
        // Pre-allocate list for stopped sound IDs (most frames have 0-2 stopped sounds)
        var stoppedSounds = new List<Guid>(4);

        foreach (KeyValuePair<Guid, SoundInstance> kvp in _activeSounds)
        {
            if (!kvp.Value.IsPlaying)
            {
                stoppedSounds.Add(kvp.Key);
            }
        }

        // Remove stopped sounds
        for (int i = 0; i < stoppedSounds.Count; i++)
        {
            if (_activeSounds.TryRemove(stoppedSounds[i], out SoundInstance? sound))
            {
                sound.Dispose();
            }
        }
    }

    public void StopAll()
    {
        if (_disposed)
        {
            return;
        }

        lock (_lock)
        {
            foreach (SoundInstance sound in _activeSounds.Values)
            {
                sound.Stop();
            }
        }
    }

    public void Preload(params string[] trackIds)
    {
        if (_disposed || trackIds == null)
        {
            return;
        }

        // PortAudio doesn't require preloading as files are streamed
        // This method is provided for interface compatibility and path validation
        foreach (string trackId in trackIds)
        {
            AudioEntity? definition = _audioRegistry.GetByTrackId(trackId);
            if (definition != null)
            {
                // Resolve path using content provider (supports mods)
                string? fullPath = _contentProvider.ResolveContentPath("Root", definition.AudioPath);
                if (fullPath == null)
                {
                    _logger?.LogWarning("Audio file not found for preload: {TrackId} -> {AudioPath}", trackId,
                        definition.AudioPath);
                }
            }
        }
    }

    public (int active, int max) GetStatistics()
    {
        return (_activeSounds.Count, MaxConcurrentSounds);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        lock (_lock)
        {
            foreach (SoundInstance sound in _activeSounds.Values)
            {
                sound.Dispose();
            }

            _activeSounds.Clear();

            // Dispose shared output device and mixer
            try
            {
                _outputDevice?.Stop();
                _outputDevice?.Dispose();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error disposing output device");
            }

            _mixer?.Dispose();
            _mixer = null;
            _outputDevice = null;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Ensures the shared mixer and output device are initialized.
    ///     Called lazily on first sound playback.
    /// </summary>
    private void EnsureMixerInitialized()
    {
        if (_mixer != null && _outputDevice != null)
        {
            return;
        }

        lock (_lock)
        {
            // Double-check after acquiring lock
            if (_mixer != null && _outputDevice != null)
            {
                return;
            }

            try
            {
                _mixer = new AudioMixer(_mixerFormat);
                _outputDevice = new PortAudioOutput(_mixer);
                _outputDevice.Play();
                _logger?.LogInformation("Initialized shared audio mixer with format: {Format}", _mixerFormat);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to initialize audio mixer");
                _mixer?.Dispose();
                _outputDevice?.Dispose();
                _mixer = null;
                _outputDevice = null;
                throw;
            }
        }
    }

    internal void RemoveSound(Guid id)
    {
        if (_activeSounds.TryRemove(id, out SoundInstance? sound))
        {
            sound.Dispose();
        }
    }

    /// <summary>
    ///     Attempts to evict a lower priority sound to make room for a new sound.
    ///     Critical and UI priority sounds are never evicted.
    /// </summary>
    /// <param name="newSoundPriority">The priority of the new sound being added.</param>
    /// <returns>True if a sound was evicted; false if no suitable sound to evict.</returns>
    private bool TryEvictLowerPrioritySound(SoundPriority newSoundPriority)
    {
        // Never evict sounds to make room for Background priority
        // Critical and UI sounds are protected from eviction
        if (newSoundPriority <= SoundPriority.Background)
        {
            return false;
        }

        // Find lowest priority, oldest sound that is below new sound's priority
        Guid candidateId = Guid.Empty;
        SoundPriority lowestPriority = newSoundPriority; // Must be strictly lower
        DateTime oldestTime = DateTime.MaxValue;
        bool found = false;

        foreach (KeyValuePair<Guid, SoundInstance> kvp in _activeSounds)
        {
            SoundInstance sound = kvp.Value;

            // Skip protected priorities (Critical and UI are never evicted)
            if (sound.Priority >= SoundPriority.Critical)
            {
                continue;
            }

            // Skip sounds at or above the new sound's priority
            if (sound.Priority >= newSoundPriority)
            {
                continue;
            }

            // Find the lowest priority, oldest sound
            if (sound.Priority < lowestPriority ||
                (sound.Priority == lowestPriority && sound.CreatedAt < oldestTime))
            {
                candidateId = kvp.Key;
                lowestPriority = sound.Priority;
                oldestTime = sound.CreatedAt;
                found = true;
            }
        }

        if (found && _activeSounds.TryRemove(candidateId, out SoundInstance? evictedSound))
        {
            _logger?.LogDebug("Evicted sound with priority {Priority} to make room for priority {NewPriority}",
                lowestPriority, newSoundPriority);
            evictedSound.Dispose();
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Represents a single playing sound instance using the shared mixer.
    /// </summary>
    private class SoundInstance : IDisposable
    {
        private readonly ILogger? _logger;
        private readonly PortAudioSoundEffectManager _manager;
        private readonly AudioMixer _mixer;
        private readonly PanningSampleProvider? _panningProvider;
        private readonly VorbisReader _reader;
        private readonly VolumeSampleProvider _volumeProvider;
        private bool _disposed;
        private AudioMixer.MixerInput? _mixerInput;

        public SoundInstance(
            string filePath,
            bool isLooping,
            float volume,
            float pitch,
            float pan,
            SoundPriority priority,
            ILogger? logger,
            PortAudioSoundEffectManager manager,
            AudioMixer mixer,
            AudioFormat mixerFormat)
        {
            _logger = logger;
            _manager = manager;
            _mixer = mixer;
            IsLooping = isLooping;
            Priority = priority;

            try
            {
                // Create the audio reader for OGG files
                _reader = new VorbisReader(filePath);

                // Build sample provider chain with proper order:
                // 1. Start with base provider (looping or direct reader)
                ISampleProvider sampleProvider;

                if (isLooping)
                {
                    // For looping, we need a custom looping provider
                    sampleProvider = new LoopingSampleProvider(_reader);
                }
                else
                {
                    sampleProvider = _reader;
                }

                // 2. Apply pitch shifting if needed (before resampling)
                sampleProvider = PitchShiftProvider.CreateIfNeeded(sampleProvider, pitch);

                // 3. Resample to mixer format if sample rates differ
                sampleProvider = ResampleProvider.CreateIfNeeded(sampleProvider, mixerFormat.SampleRate);

                // 4. Convert mono to stereo if needed (before panning)
                sampleProvider = MonoToStereoProvider.CreateIfNeeded(sampleProvider);

                // 5. Apply volume control
                _volumeProvider = new VolumeSampleProvider(sampleProvider)
                {
                    Volume = Math.Clamp(volume, AudioConstants.MinVolume, AudioConstants.MaxVolume)
                };

                // 6. Apply panning (now safe - audio is guaranteed stereo)
                ISampleProvider finalProvider;
                if (sampleProvider.Format.Channels == 2)
                {
                    _panningProvider = new PanningSampleProvider(_volumeProvider)
                    {
                        Pan = Math.Clamp(pan, AudioConstants.MinPan, AudioConstants.MaxPan)
                    };
                    finalProvider = _panningProvider;
                }
                else
                {
                    finalProvider = _volumeProvider;
                }

                // Add to mixer and start playback
                _mixerInput = _mixer.AddSource(finalProvider);
            }
            catch (Exception ex)
            {
                _reader?.Dispose();
                _logger?.LogError(ex, "Failed to initialize sound instance for: {FilePath}", filePath);
                throw;
            }
        }

        public Guid Id { get; } = Guid.NewGuid();
        public DateTime CreatedAt { get; } = DateTime.UtcNow;
        public bool IsLooping { get; }
        public SoundPriority Priority { get; }

        public bool IsPlaying
        {
            get
            {
                if (_disposed || _mixerInput == null)
                {
                    return false;
                }

                // Sound is playing if it's still in the mixer
                // The mixer automatically removes sources that have finished
                return true;
            }
        }

        public float Volume
        {
            get => _volumeProvider.Volume;
            set => _volumeProvider.Volume = Math.Clamp(value, AudioConstants.MinVolume, AudioConstants.MaxVolume);
        }

        public float Pan
        {
            get => _panningProvider?.Pan ?? 0f;
            set
            {
                if (_panningProvider != null)
                {
                    _panningProvider.Pan = Math.Clamp(value, AudioConstants.MinPan, AudioConstants.MaxPan);
                }
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                // Remove from mixer
                if (_mixerInput != null)
                {
                    _mixer.RemoveSource(_mixerInput);
                    _mixerInput = null;
                }

                _reader.Dispose();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error disposing sound instance");
            }

            _disposed = true;
        }

        public void Stop()
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                // Remove from mixer
                if (_mixerInput != null)
                {
                    _mixer.RemoveSource(_mixerInput);
                    _mixerInput = null;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error stopping sound instance");
            }
        }

        public void Pause()
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                // Pause by setting volume to 0 (mixer doesn't support pause)
                if (_mixerInput != null)
                {
                    _mixerInput.Volume = 0f;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error pausing sound instance");
            }
        }

        public void Resume()
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                // Resume by restoring volume (mixer doesn't support pause)
                if (_mixerInput != null)
                {
                    _mixerInput.Volume = 1.0f;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error resuming sound instance");
            }
        }
    }

    /// <summary>
    ///     Sample provider that loops audio continuously.
    /// </summary>
    private class LoopingSampleProvider : ISampleProvider
    {
        private const int MaxResetRetries = 3;
        private readonly VorbisReader _source;

        public LoopingSampleProvider(VorbisReader source)
        {
            _source = source;
        }

        public AudioFormat Format => _source.Format;

        public int Read(float[] buffer, int offset, int count)
        {
            int totalRead = 0;

            while (totalRead < count)
            {
                int read = _source.Read(buffer, offset + totalRead, count - totalRead);

                if (read == 0)
                {
                    // End of stream, attempt to reset
                    int retryCount = 0;
                    bool resetSuccessful = false;

                    while (retryCount < MaxResetRetries && !resetSuccessful)
                    {
                        _source.Reset();
                        retryCount++;

                        // Verify reset worked by attempting a small read
                        int testRead = _source.Read(buffer, offset + totalRead, Math.Min(count - totalRead, 1));
                        if (testRead > 0)
                        {
                            totalRead += testRead;
                            resetSuccessful = true;
                        }
                    }

                    if (!resetSuccessful)
                    {
                        // Reset failed after retries - fill remaining with silence to prevent infinite loop
                        Array.Clear(buffer, offset + totalRead, count - totalRead);
                        return totalRead;
                    }
                }
                else
                {
                    totalRead += read;
                }
            }

            return totalRead;
        }
    }

    /// <summary>
    ///     Handle implementation for controlling looping sounds.
    /// </summary>
    private class LoopingSoundHandle : ILoopingSoundHandle
    {
        private readonly SoundInstance _instance;
        private readonly PortAudioSoundEffectManager _manager;
        private bool _disposed;

        public LoopingSoundHandle(SoundInstance instance, PortAudioSoundEffectManager manager)
        {
            _instance = instance;
            _manager = manager;
        }

        public bool IsPlaying => !_disposed && _instance.IsPlaying;

        public float Volume
        {
            get => _disposed ? 0f : _instance.Volume;
            set
            {
                if (!_disposed)
                {
                    _instance.Volume = value;
                }
            }
        }

        public float Pan
        {
            get => _disposed ? 0f : _instance.Pan;
            set
            {
                if (!_disposed)
                {
                    _instance.Pan = value;
                }
            }
        }

        public void Stop()
        {
            if (_disposed)
            {
                return;
            }

            _instance.Stop();
            _manager.RemoveSound(_instance.Id);
        }

        public void Pause()
        {
            if (!_disposed)
            {
                _instance.Pause();
            }
        }

        public void Resume()
        {
            if (!_disposed)
            {
                _instance.Resume();
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Stop();
            _disposed = true;
        }
    }
}
