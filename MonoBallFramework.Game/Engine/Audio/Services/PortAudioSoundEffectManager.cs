using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Engine.Audio.Configuration;
using MonoBallFramework.Game.Engine.Audio.Core;
using MonoBallFramework.Game.GameData.Entities;

namespace MonoBallFramework.Game.Engine.Audio.Services;

/// <summary>
///     PortAudio-based sound effect manager for playing OGG sound effects.
///     Supports concurrent playback, looping, and advanced audio control.
///     Cross-platform implementation using PortAudioSharp2 and NVorbis.
/// </summary>
public class PortAudioSoundEffectManager : ISoundEffectManager
{
    private readonly AudioRegistry _audioRegistry;
    private readonly ILogger<PortAudioSoundEffectManager>? _logger;
    private readonly int _maxConcurrentSounds;
    private readonly ConcurrentDictionary<Guid, SoundInstance> _activeSounds;
    private readonly object _lock = new();
    private float _masterVolume = AudioConstants.DefaultMasterVolume;
    private bool _disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PortAudioSoundEffectManager"/> class.
    /// </summary>
    /// <param name="audioRegistry">The audio registry for looking up sound definitions.</param>
    /// <param name="maxConcurrentSounds">Maximum number of concurrent sounds (uses AudioConstants.MaxConcurrentSounds if not specified).</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public PortAudioSoundEffectManager(
        AudioRegistry audioRegistry,
        int maxConcurrentSounds = AudioConstants.MaxConcurrentSounds,
        ILogger<PortAudioSoundEffectManager>? logger = null)
    {
        _audioRegistry = audioRegistry ?? throw new ArgumentNullException(nameof(audioRegistry));
        _logger = logger;

        if (maxConcurrentSounds <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxConcurrentSounds));

        _maxConcurrentSounds = maxConcurrentSounds;
        _activeSounds = new ConcurrentDictionary<Guid, SoundInstance>();
    }

    public float MasterVolume
    {
        get => _masterVolume;
        set => _masterVolume = Math.Clamp(value, AudioConstants.MinVolume, AudioConstants.MaxVolume);
    }

    public int MaxConcurrentSounds => _maxConcurrentSounds;

    public int ActiveSoundCount => _activeSounds.Count;

    public bool Play(string trackId, float volume = 1.0f, float pitch = 0.0f, float pan = 0.0f, SoundPriority priority = SoundPriority.Normal)
    {
        if (_disposed || string.IsNullOrEmpty(trackId))
            return false;

        // Look up audio definition by track ID
        var definition = _audioRegistry.GetByTrackId(trackId);
        if (definition == null)
        {
            _logger?.LogWarning("Audio definition not found for track ID: {TrackId}", trackId);
            return false;
        }

        // Construct full path
        string fullPath = Path.Combine(AppContext.BaseDirectory, definition.AudioPath);
        return PlayFromFile(fullPath, volume, pitch, pan, priority);
    }

    public bool PlayFromFile(string filePath, float volume = 1.0f, float pitch = 0.0f, float pan = 0.0f, SoundPriority priority = SoundPriority.Normal)
    {
        if (_disposed || string.IsNullOrEmpty(filePath))
            return false;

        if (!File.Exists(filePath))
        {
            _logger?.LogWarning("Audio file not found: {FilePath}", filePath);
            return false;
        }

        // Check if we've hit the concurrent sound limit
        if (_activeSounds.Count >= _maxConcurrentSounds)
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
            var soundInstance = new SoundInstance(
                filePath,
                false,
                volume * _masterVolume,
                pitch,
                pan,
                priority,
                _logger,
                this);

            _activeSounds.TryAdd(soundInstance.Id, soundInstance);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to play sound from file: {FilePath}", filePath);
            return false;
        }
    }

    public ILoopingSoundHandle? PlayLooping(string trackId, float volume = 1.0f, float pitch = 0.0f, float pan = 0.0f, SoundPriority priority = SoundPriority.Normal)
    {
        if (_disposed || string.IsNullOrEmpty(trackId))
            return null;

        var definition = _audioRegistry.GetByTrackId(trackId);
        if (definition == null)
        {
            _logger?.LogWarning("Audio definition not found for track ID: {TrackId}", trackId);
            return null;
        }

        string fullPath = Path.Combine(AppContext.BaseDirectory, definition.AudioPath);
        return PlayLoopingFromFile(fullPath, volume, pitch, pan, priority);
    }

    public ILoopingSoundHandle? PlayLoopingFromFile(string filePath, float volume = 1.0f, float pitch = 0.0f, float pan = 0.0f, SoundPriority priority = SoundPriority.Normal)
    {
        if (_disposed || string.IsNullOrEmpty(filePath))
            return null;

        if (!File.Exists(filePath))
        {
            _logger?.LogWarning("Audio file not found: {FilePath}", filePath);
            return null;
        }

        if (_activeSounds.Count >= _maxConcurrentSounds)
        {
            if (!TryEvictLowerPrioritySound(priority))
            {
                _logger?.LogWarning("Max concurrent sounds reached, cannot evict for priority {Priority}", priority);
                return null;
            }
        }

        try
        {
            var soundInstance = new SoundInstance(
                filePath,
                true,
                volume * _masterVolume,
                pitch,
                pan,
                priority,
                _logger,
                this);

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
            return;

        // Clean up stopped sounds - no LINQ, direct iteration
        // Pre-allocate list for stopped sound IDs (most frames have 0-2 stopped sounds)
        var stoppedSounds = new List<Guid>(4);

        foreach (var kvp in _activeSounds)
        {
            if (!kvp.Value.IsPlaying)
            {
                stoppedSounds.Add(kvp.Key);
            }
        }

        // Remove stopped sounds
        for (int i = 0; i < stoppedSounds.Count; i++)
        {
            if (_activeSounds.TryRemove(stoppedSounds[i], out var sound))
            {
                sound.Dispose();
            }
        }
    }

    public void StopAll()
    {
        if (_disposed)
            return;

        lock (_lock)
        {
            foreach (var sound in _activeSounds.Values)
            {
                sound.Stop();
            }
        }
    }

    public void Preload(params string[] trackIds)
    {
        if (_disposed || trackIds == null)
            return;

        // PortAudio doesn't require preloading as files are streamed
        // This method is provided for interface compatibility
        foreach (var trackId in trackIds)
        {
            var definition = _audioRegistry.GetByTrackId(trackId);
            if (definition != null)
            {
                string fullPath = Path.Combine(AppContext.BaseDirectory, definition.AudioPath);
                if (!File.Exists(fullPath))
                {
                    _logger?.LogWarning("Audio file not found for preload: {TrackId} -> {FilePath}", trackId, fullPath);
                }
            }
        }
    }

    public (int active, int max) GetStatistics()
    {
        return (_activeSounds.Count, _maxConcurrentSounds);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        lock (_lock)
        {
            foreach (var sound in _activeSounds.Values)
            {
                sound.Dispose();
            }
            _activeSounds.Clear();
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    internal void RemoveSound(Guid id)
    {
        if (_activeSounds.TryRemove(id, out var sound))
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
            return false;

        // Find lowest priority, oldest sound that is below new sound's priority
        Guid candidateId = Guid.Empty;
        SoundPriority lowestPriority = newSoundPriority; // Must be strictly lower
        DateTime oldestTime = DateTime.MaxValue;
        bool found = false;

        foreach (var kvp in _activeSounds)
        {
            var sound = kvp.Value;

            // Skip protected priorities (Critical and UI are never evicted)
            if (sound.Priority >= SoundPriority.Critical)
                continue;

            // Skip sounds at or above the new sound's priority
            if (sound.Priority >= newSoundPriority)
                continue;

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

        if (found && _activeSounds.TryRemove(candidateId, out var evictedSound))
        {
            _logger?.LogDebug("Evicted sound with priority {Priority} to make room for priority {NewPriority}",
                lowestPriority, newSoundPriority);
            evictedSound.Dispose();
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Represents a single playing sound instance using PortAudio.
    /// </summary>
    private class SoundInstance : IDisposable
    {
        private readonly PortAudioOutput _outputDevice;
        private readonly VorbisReader _reader;
        private readonly VolumeSampleProvider _volumeProvider;
        private readonly PanningSampleProvider? _panningProvider;
        private readonly ILogger? _logger;
        private readonly PortAudioSoundEffectManager _manager;
        private bool _disposed;

        public Guid Id { get; } = Guid.NewGuid();
        public DateTime CreatedAt { get; } = DateTime.UtcNow;
        public bool IsLooping { get; }
        public SoundPriority Priority { get; }

        public bool IsPlaying
        {
            get
            {
                if (_disposed)
                    return false;

                try
                {
                    return _outputDevice.PlaybackState == PlaybackState.Playing;
                }
                catch
                {
                    return false;
                }
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
                    _panningProvider.Pan = Math.Clamp(value, AudioConstants.MinPan, AudioConstants.MaxPan);
            }
        }

        public SoundInstance(
            string filePath,
            bool isLooping,
            float volume,
            float pitch,
            float pan,
            SoundPriority priority,
            ILogger? logger,
            PortAudioSoundEffectManager manager)
        {
            _logger = logger;
            _manager = manager;
            IsLooping = isLooping;
            Priority = priority;

            try
            {
                // Create the audio reader for OGG files
                _reader = new VorbisReader(filePath);

                // Build sample provider chain
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

                // Apply pitch if needed (requires resampling - not implemented)
                if (Math.Abs(pitch) > 0.001f)
                {
                    // Note: Pitch shifting is complex and requires additional libraries
                    _logger?.LogWarning("Pitch adjustment not fully implemented in PortAudio version");
                }

                // Apply volume control
                _volumeProvider = new VolumeSampleProvider(sampleProvider)
                {
                    Volume = Math.Clamp(volume, AudioConstants.MinVolume, AudioConstants.MaxVolume)
                };

                // Apply panning if stereo
                ISampleProvider finalProvider;
                if (_reader.Format.Channels == 2)
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

                // Create output device and start playback
                _outputDevice = new PortAudioOutput(finalProvider);
                _outputDevice.PlaybackStopped += OnPlaybackStopped;
                _outputDevice.Play();
            }
            catch (Exception ex)
            {
                _reader?.Dispose();
                _logger?.LogError(ex, "Failed to initialize sound instance for: {FilePath}", filePath);
                throw;
            }
        }

        private void OnPlaybackStopped(object? sender, PlaybackStoppedEventArgs e)
        {
            if (e.Exception != null)
            {
                _logger?.LogWarning(e.Exception, "Sound playback stopped with error");
            }
        }

        public void Stop()
        {
            if (_disposed)
                return;

            try
            {
                _outputDevice.Stop();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error stopping sound instance");
            }
        }

        public void Pause()
        {
            if (_disposed)
                return;

            try
            {
                _outputDevice.Pause();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error pausing sound instance");
            }
        }

        public void Resume()
        {
            if (_disposed)
                return;

            try
            {
                _outputDevice.Play();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error resuming sound instance");
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            try
            {
                _outputDevice.PlaybackStopped -= OnPlaybackStopped;
                _outputDevice.Stop();
                _outputDevice.Dispose();
                _reader.Dispose();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error disposing sound instance");
            }

            _disposed = true;
        }
    }

    /// <summary>
    ///     Sample provider that loops audio continuously.
    /// </summary>
    private class LoopingSampleProvider : ISampleProvider
    {
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
                    // End of stream, reset to beginning
                    _source.Reset();
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
                    _instance.Volume = value;
            }
        }

        public float Pan
        {
            get => _disposed ? 0f : _instance.Pan;
            set
            {
                if (!_disposed)
                    _instance.Pan = value;
            }
        }

        public void Stop()
        {
            if (_disposed)
                return;

            _instance.Stop();
            _manager.RemoveSound(_instance.Id);
        }

        public void Pause()
        {
            if (!_disposed)
                _instance.Pause();
        }

        public void Resume()
        {
            if (!_disposed)
                _instance.Resume();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            Stop();
            _disposed = true;
        }
    }
}

