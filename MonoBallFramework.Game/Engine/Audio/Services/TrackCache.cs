using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Engine.Audio.Core;
using MonoBallFramework.Game.Engine.Content;
using MonoBallFramework.Game.GameData.Entities;

namespace MonoBallFramework.Game.Engine.Audio.Services;

/// <summary>
///     Represents a cached audio track with metadata and audio data
/// </summary>
public class CachedTrack : IDisposable
{
    /// <summary>Unique identifier for the track</summary>
    public required string TrackId { get; init; }

    /// <summary>Cached audio data (samples)</summary>
    public required CachedAudioData AudioData { get; init; }

    /// <summary>Audio format of the track</summary>
    public required AudioFormat AudioFormat { get; init; }

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
            if (AudioData?.Samples == null || AudioFormat == null)
            {
                return TimeSpan.Zero;
            }

            int totalSamples = AudioData.Samples.Length;
            int channels = AudioFormat.Channels;
            int sampleRate = AudioFormat.SampleRate;

            if (channels == 0 || sampleRate == 0)
            {
                return TimeSpan.Zero;
            }

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
///     Cached audio data container
/// </summary>
public class CachedAudioData
{
    public required AudioFormat AudioFormat { get; init; }
    public required float[] Samples { get; init; }
}

/// <summary>
///     Interface for track caching operations
/// </summary>
public interface ITrackCache : IDisposable
{
    /// <summary>Current number of cached tracks</summary>
    int Count { get; }

    /// <summary>Total memory usage in bytes</summary>
    long MemoryUsage { get; }

    /// <summary>Maximum cache size in tracks</summary>
    int MaxCacheSize { get; }

    /// <summary>Attempts to retrieve a cached track</summary>
    bool TryGetTrack(string trackId, out CachedTrack? track);

    /// <summary>Loads a track from disk and caches it</summary>
    CachedTrack? LoadAndCache(string trackId, AudioEntity definition);

    /// <summary>Evicts a specific track from the cache</summary>
    void Evict(string trackId);

    /// <summary>Clears all cached tracks</summary>
    void Clear();
}

/// <summary>
///     Thread-safe track cache with LRU eviction policy
/// </summary>
public class TrackCache : ITrackCache
{
    private readonly ConcurrentDictionary<string, CachedTrack> _cache = new();
    private readonly IContentProvider _contentProvider;
    private readonly object _evictionLock = new();
    private readonly ILogger<TrackCache>? _logger;
    private bool _disposed;

    /// <summary>
    ///     Creates a new track cache with the specified maximum size
    /// </summary>
    /// <param name="contentProvider">Content provider for resolving audio paths</param>
    /// <param name="maxCacheSize">Maximum number of tracks to cache (0 = unlimited)</param>
    /// <param name="logger">Optional logger for diagnostics</param>
    public TrackCache(IContentProvider contentProvider, int maxCacheSize = 50, ILogger<TrackCache>? logger = null)
    {
        _contentProvider = contentProvider ?? throw new ArgumentNullException(nameof(contentProvider));

        if (maxCacheSize < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCacheSize), "Cache size must be non-negative");
        }

        MaxCacheSize = maxCacheSize;
        _logger = logger;
    }

    public int MaxCacheSize { get; }

    public int Count => _cache.Count;

    public long MemoryUsage
    {
        get
        {
            long total = 0;
            foreach (CachedTrack track in _cache.Values)
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

    public CachedTrack? LoadAndCache(string trackId, AudioEntity definition)
    {
        if (_disposed || string.IsNullOrEmpty(trackId) || definition == null)
        {
            return null;
        }

        // ConcurrentDictionary.GetOrAdd is atomic and thread-safe
        // File I/O happens outside any lock for performance
        return _cache.GetOrAdd(trackId, _ =>
        {
            try
            {
                // Check if we need to evict before loading
                if (MaxCacheSize > 0 && _cache.Count >= MaxCacheSize)
                {
                    EvictLRU();
                }

                // Resolve full path to audio file using content provider
                // AudioPath includes content type prefix (e.g., "Audio/Music/..."), so use "Root"
                string? fullPath = _contentProvider.ResolveContentPath("Root", definition.AudioPath);

                if (fullPath == null)
                {
                    _logger?.LogError("Audio file not found: {Path}", definition.AudioPath);
                    throw new FileNotFoundException($"Audio file not found: {definition.AudioPath}");
                }

                // Load entire OGG file into memory for efficient playback
                CachedAudioData? audioData = LoadAudioFile(fullPath);
                if (audioData == null)
                {
                    _logger?.LogError("Failed to load audio data from: {Path}", fullPath);
                    return null!;
                }

                var track = new CachedTrack
                {
                    TrackId = trackId,
                    AudioData = audioData,
                    AudioFormat = audioData.AudioFormat,
                    LoopStartSamples = definition.LoopStartSamples,
                    LoopLengthSamples = definition.LoopLengthSamples,
                    LastAccessed = DateTime.UtcNow
                };

                long memoryMB = track.MemoryUsage / (1024 * 1024);

                if (definition.HasLoopPoints)
                {
                    _logger?.LogDebug(
                        "Loaded track with loop points: {TrackId} (start: {Start}, length: {Length}, size: {Size}MB, duration: {Duration})",
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
        {
            return;
        }

        if (_cache.TryRemove(trackId, out CachedTrack? track))
        {
            track.Dispose();
            _logger?.LogDebug("Evicted track from cache: {TrackId}", trackId);
        }
    }

    public void Clear()
    {
        if (_disposed)
        {
            return;
        }

        lock (_evictionLock)
        {
            CachedTrack[] tracks = _cache.Values.ToArray();
            _cache.Clear();

            foreach (CachedTrack track in tracks)
            {
                track.Dispose();
            }

            _logger?.LogDebug("Cleared all tracks from cache ({Count} tracks)", tracks.Length);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Clear();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Evicts the least recently used track from the cache
    /// </summary>
    private void EvictLRU()
    {
        // Use lock to ensure only one thread performs eviction at a time
        lock (_evictionLock)
        {
            // Re-check count inside lock
            if (_cache.Count < MaxCacheSize)
            {
                return;
            }

            // Find the least recently used track
            CachedTrack? oldestTrack = null;
            DateTime oldestTime = DateTime.MaxValue;
            string? oldestKey = null;

            foreach (KeyValuePair<string, CachedTrack> kvp in _cache)
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
                if (_cache.TryRemove(oldestKey, out CachedTrack? removed))
                {
                    removed.Dispose();
                    _logger?.LogDebug("LRU evicted track: {TrackId} (last accessed: {Time}, freed: {Size}MB)",
                        oldestKey, oldestTime, removed.MemoryUsage / (1024 * 1024));
                }
            }
        }
    }

    /// <summary>
    ///     Loads an audio file from disk into memory using NVorbis
    /// </summary>
    private CachedAudioData? LoadAudioFile(string filePath)
    {
        try
        {
            using var reader = new VorbisReader(filePath);

            AudioFormat format = reader.Format;

            // Calculate total samples to pre-allocate array
            long totalSamples = reader.TotalSamples;

            // Pre-allocate exact size needed - eliminates 3x memory allocations
            float[] samples = new float[totalSamples];
            float[] buffer = new float[format.SampleRate * format.Channels];
            int offset = 0;
            int samplesRead;

            while ((samplesRead = reader.Read(buffer, 0, buffer.Length)) > 0)
            {
                // Direct array copy instead of individual Add() calls
                Buffer.BlockCopy(buffer, 0, samples, offset * sizeof(float), samplesRead * sizeof(float));
                offset += samplesRead;
            }

            return new CachedAudioData { AudioFormat = format, Samples = samples };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error reading audio file: {Path}", filePath);
            return null;
        }
    }
}
