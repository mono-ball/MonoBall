using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.GameData.Entities;
using NAudio.Vorbis;
using NAudio.Wave;

namespace MonoBallFramework.Game.Engine.Audio.Services;

/// <summary>
/// Represents a cached audio track with metadata and audio data
/// </summary>
public class CachedTrack : IDisposable
{
    /// <summary>Unique identifier for the track</summary>
    public required string TrackId { get; init; }

    /// <summary>Cached audio data (samples)</summary>
    public required CachedAudioData AudioData { get; init; }

    /// <summary>Wave format of the audio</summary>
    public required WaveFormat WaveFormat { get; init; }

    /// <summary>Loop start position in samples (per channel). Null = loop from beginning.</summary>
    public int? LoopStartSamples { get; init; }

    /// <summary>Loop length in samples (per channel). Null = loop to end.</summary>
    public int? LoopLengthSamples { get; init; }

    /// <summary>Last time this track was accessed (for LRU eviction)</summary>
    public DateTime LastAccessed { get; set; } = DateTime.UtcNow;

    /// <summary>Duration of the track</summary>
    public TimeSpan Duration
    {
        get
        {
            if (AudioData?.Samples == null || WaveFormat == null)
                return TimeSpan.Zero;

            int totalSamples = AudioData.Samples.Length;
            int channels = WaveFormat.Channels;
            int sampleRate = WaveFormat.SampleRate;

            if (channels == 0 || sampleRate == 0)
                return TimeSpan.Zero;

            double durationSeconds = (double)totalSamples / channels / sampleRate;
            return TimeSpan.FromSeconds(durationSeconds);
        }
    }

    /// <summary>Memory usage in bytes</summary>
    public long MemoryUsage => AudioData?.Samples?.Length * sizeof(float) ?? 0;

    public void Dispose()
    {
        // Audio data will be garbage collected
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Cached audio data container
/// </summary>
public class CachedAudioData
{
    public required WaveFormat WaveFormat { get; init; }
    public required float[] Samples { get; init; }
}

/// <summary>
/// Interface for track caching operations
/// </summary>
public interface ITrackCache : IDisposable
{
    /// <summary>Attempts to retrieve a cached track</summary>
    bool TryGetTrack(string trackId, out CachedTrack? track);

    /// <summary>Loads a track from disk and caches it</summary>
    CachedTrack? LoadAndCache(string trackId, AudioDefinition definition);

    /// <summary>Evicts a specific track from the cache</summary>
    void Evict(string trackId);

    /// <summary>Clears all cached tracks</summary>
    void Clear();

    /// <summary>Current number of cached tracks</summary>
    int Count { get; }

    /// <summary>Total memory usage in bytes</summary>
    long MemoryUsage { get; }

    /// <summary>Maximum cache size in tracks</summary>
    int MaxCacheSize { get; }
}

/// <summary>
/// Thread-safe track cache with LRU eviction policy
/// </summary>
public class TrackCache : ITrackCache
{
    private readonly ConcurrentDictionary<string, CachedTrack> _cache = new();
    private readonly int _maxCacheSize;
    private readonly ILogger<TrackCache>? _logger;
    private readonly object _evictionLock = new();
    private bool _disposed;

    /// <summary>
    /// Creates a new track cache with the specified maximum size
    /// </summary>
    /// <param name="maxCacheSize">Maximum number of tracks to cache (0 = unlimited)</param>
    /// <param name="logger">Optional logger for diagnostics</param>
    public TrackCache(int maxCacheSize = 50, ILogger<TrackCache>? logger = null)
    {
        if (maxCacheSize < 0)
            throw new ArgumentOutOfRangeException(nameof(maxCacheSize), "Cache size must be non-negative");

        _maxCacheSize = maxCacheSize;
        _logger = logger;
    }

    public int MaxCacheSize => _maxCacheSize;

    public int Count => _cache.Count;

    public long MemoryUsage
    {
        get
        {
            long total = 0;
            foreach (var track in _cache.Values)
            {
                total += track.MemoryUsage;
            }
            return total;
        }
    }

    public bool TryGetTrack(string trackId, out CachedTrack? track)
    {
        if (_disposed || string.IsNullOrEmpty(trackId))
        {
            track = null;
            return false;
        }

        if (_cache.TryGetValue(trackId, out track))
        {
            // Update last accessed time for LRU
            track.LastAccessed = DateTime.UtcNow;
            return true;
        }

        track = null;
        return false;
    }

    public CachedTrack? LoadAndCache(string trackId, AudioDefinition definition)
    {
        if (_disposed || string.IsNullOrEmpty(trackId) || definition == null)
            return null;

        // ConcurrentDictionary.GetOrAdd is atomic and thread-safe
        // File I/O happens outside any lock for performance
        return _cache.GetOrAdd(trackId, _ =>
        {
            try
            {
                // Check if we need to evict before loading
                if (_maxCacheSize > 0 && _cache.Count >= _maxCacheSize)
                {
                    EvictLRU();
                }

                // Build full path to audio file
                string fullPath = Path.Combine(AppContext.BaseDirectory, "Assets", definition.AudioPath);

                if (!File.Exists(fullPath))
                {
                    _logger?.LogError("Audio file not found: {Path}", fullPath);
                    return null!;
                }

                // Load entire OGG file into memory for efficient playback
                var audioData = LoadAudioFile(fullPath);
                if (audioData == null)
                {
                    _logger?.LogError("Failed to load audio data from: {Path}", fullPath);
                    return null!;
                }

                var track = new CachedTrack
                {
                    TrackId = trackId,
                    AudioData = audioData,
                    WaveFormat = audioData.WaveFormat,
                    LoopStartSamples = definition.LoopStartSamples,
                    LoopLengthSamples = definition.LoopLengthSamples,
                    LastAccessed = DateTime.UtcNow
                };

                long memoryMB = track.MemoryUsage / (1024 * 1024);

                if (definition.HasLoopPoints)
                {
                    _logger?.LogDebug("Loaded track with loop points: {TrackId} (start: {Start}, length: {Length}, size: {Size}MB, duration: {Duration})",
                        trackId, definition.LoopStartSamples, definition.LoopLengthSamples, memoryMB, track.Duration);
                }
                else
                {
                    _logger?.LogDebug("Loaded and cached track: {TrackId} (size: {Size}MB, duration: {Duration})",
                        trackId, memoryMB, track.Duration);
                }

                return track;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading track: {TrackId} from {Path}", trackId, definition.AudioPath);
                return null!;
            }
        });
    }

    public void Evict(string trackId)
    {
        if (_disposed || string.IsNullOrEmpty(trackId))
            return;

        if (_cache.TryRemove(trackId, out var track))
        {
            track.Dispose();
            _logger?.LogDebug("Evicted track from cache: {TrackId}", trackId);
        }
    }

    public void Clear()
    {
        if (_disposed)
            return;

        lock (_evictionLock)
        {
            var tracks = _cache.Values.ToArray();
            _cache.Clear();

            foreach (var track in tracks)
            {
                track.Dispose();
            }

            _logger?.LogDebug("Cleared all tracks from cache ({Count} tracks)", tracks.Length);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Clear();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Evicts the least recently used track from the cache
    /// </summary>
    private void EvictLRU()
    {
        // Use lock to ensure only one thread performs eviction at a time
        lock (_evictionLock)
        {
            // Re-check count inside lock
            if (_cache.Count < _maxCacheSize)
                return;

            // Find the least recently used track
            CachedTrack? oldestTrack = null;
            DateTime oldestTime = DateTime.MaxValue;
            string? oldestKey = null;

            foreach (var kvp in _cache)
            {
                if (kvp.Value.LastAccessed < oldestTime)
                {
                    oldestTime = kvp.Value.LastAccessed;
                    oldestTrack = kvp.Value;
                    oldestKey = kvp.Key;
                }
            }

            // Evict the oldest track
            if (oldestKey != null && oldestTrack != null)
            {
                if (_cache.TryRemove(oldestKey, out var removed))
                {
                    removed.Dispose();
                    _logger?.LogDebug("LRU evicted track: {TrackId} (last accessed: {Time}, freed: {Size}MB)",
                        oldestKey, oldestTime, removed.MemoryUsage / (1024 * 1024));
                }
            }
        }
    }

    /// <summary>
    /// Loads an audio file from disk into memory
    /// </summary>
    private CachedAudioData? LoadAudioFile(string filePath)
    {
        try
        {
            using var reader = new VorbisWaveReader(filePath);

            var format = reader.WaveFormat;

            // Calculate total samples first to pre-allocate array
            var sampleProvider = reader.ToSampleProvider();
            long totalBytes = reader.Length;
            int bytesPerSample = reader.WaveFormat.BitsPerSample / 8;
            long totalSamples = totalBytes / bytesPerSample;

            // Pre-allocate exact size needed - eliminates 3x memory allocations
            var samples = new float[totalSamples];
            var buffer = new float[reader.WaveFormat.SampleRate * reader.WaveFormat.Channels];
            int offset = 0;
            int samplesRead;

            while ((samplesRead = sampleProvider.Read(buffer, 0, buffer.Length)) > 0)
            {
                // Direct array copy instead of individual Add() calls
                Buffer.BlockCopy(buffer, 0, samples, offset * sizeof(float), samplesRead * sizeof(float));
                offset += samplesRead;
            }

            return new CachedAudioData
            {
                WaveFormat = format,
                Samples = samples
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error reading audio file: {Path}", filePath);
            return null;
        }
    }
}
