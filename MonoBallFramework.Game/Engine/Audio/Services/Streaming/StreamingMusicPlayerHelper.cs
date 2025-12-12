using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.GameData.Entities;
using NAudio.Vorbis;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace MonoBallFramework.Game.Engine.Audio.Services.Streaming;

/// <summary>
/// Helper class for integrating streaming audio into NAudioMusicPlayer.
/// Provides methods for creating streaming providers and managing track metadata.
/// Thread-safe for concurrent access.
/// </summary>
public class StreamingMusicPlayerHelper
{
    private readonly ConcurrentDictionary<string, StreamingTrackData> _trackMetadataCache = new();
    private readonly ILogger? _logger;

    public StreamingMusicPlayerHelper(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets or creates streaming track metadata for a given audio definition.
    /// Metadata is cached but does NOT keep the audio file open.
    /// </summary>
    /// <param name="trackName">The track identifier.</param>
    /// <param name="definition">The audio definition with file path and loop points.</param>
    /// <param name="baseDirectory">Base directory for resolving relative paths (typically AppContext.BaseDirectory).</param>
    /// <returns>Streaming track data, or null if the file cannot be accessed.</returns>
    public StreamingTrackData? GetOrCreateTrackData(
        string trackName,
        AudioDefinition definition,
        string baseDirectory)
    {
        // Check cache first
        if (_trackMetadataCache.TryGetValue(trackName, out var cached))
            return cached;

        // Create track data (may return null on failure)
        var data = CreateTrackDataInternal(trackName, definition, baseDirectory);

        // Only cache successful results (don't cache null!)
        if (data != null)
            _trackMetadataCache.TryAdd(trackName, data);

        return data;
    }

    /// <summary>
    /// Internal method to create track data. Returns null on failure instead of null!
    /// </summary>
    private StreamingTrackData? CreateTrackDataInternal(
        string trackName,
        AudioDefinition definition,
        string baseDirectory)
    {
        try
        {
            // Build full path to audio file
            string fullPath = Path.Combine(baseDirectory, "Assets", definition.AudioPath);

            if (!File.Exists(fullPath))
            {
                _logger?.LogError("Audio file not found: {Path}", fullPath);
                return null;
            }

            // Briefly open the file to read metadata (wave format)
            WaveFormat waveFormat;
            using (var reader = new VorbisWaveReader(fullPath))
            {
                waveFormat = reader.WaveFormat;
            }

            var trackData = new StreamingTrackData
            {
                TrackName = trackName,
                FilePath = fullPath,
                WaveFormat = waveFormat,
                LoopStartSamples = definition.LoopStartSamples,
                LoopLengthSamples = definition.LoopLengthSamples
            };

            if (definition.HasLoopPoints)
            {
                _logger?.LogDebug("Cached streaming track metadata with loop points: {TrackName} (start: {Start}, length: {Length})",
                    trackName, definition.LoopStartSamples, definition.LoopLengthSamples);
            }
            else
            {
                _logger?.LogDebug("Cached streaming track metadata: {TrackName}", trackName);
            }

            return trackData;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error reading track metadata: {TrackName} from {Path}",
                trackName, definition.AudioPath);
            return null;
        }
    }

    /// <summary>
    /// Creates a new streaming playback state with all necessary providers.
    /// Returns a ready-to-play state with volume control configured.
    /// </summary>
    /// <param name="trackData">The streaming track data.</param>
    /// <param name="loop">Whether the track should loop.</param>
    /// <param name="targetVolume">Target volume (0.0 - 1.0).</param>
    /// <param name="fadeInDuration">Fade-in duration in seconds (0 for instant).</param>
    /// <param name="definitionFadeOut">Fade-out duration from audio definition.</param>
    /// <returns>A configured StreamingPlaybackState ready for playback.</returns>
    public StreamingPlaybackState CreatePlaybackState(
        StreamingTrackData trackData,
        bool loop,
        float targetVolume,
        float fadeInDuration,
        float definitionFadeOut)
    {
        // Create the streaming provider chain
        var loopingProvider = trackData.CreateLoopingProvider(loop);

        try
        {
            // Wrap with volume control
            var volumeProvider = new VolumeSampleProvider(loopingProvider)
            {
                Volume = fadeInDuration > 0f ? 0f : targetVolume
            };

            var playbackState = new StreamingPlaybackState
            {
                TrackName = trackData.TrackName,
                Loop = loop,
                FadeState = fadeInDuration > 0f ? FadeState.FadingIn : FadeState.None,
                FadeDuration = fadeInDuration,
                FadeTimer = 0f,
                CurrentVolume = fadeInDuration > 0f ? 0f : targetVolume,
                TargetVolume = targetVolume,
                DefinitionFadeOut = definitionFadeOut,
                StreamingProvider = loopingProvider,
                VolumeProvider = volumeProvider
            };

            return playbackState;
        }
        catch
        {
            // If playback state creation fails, dispose the provider
            loopingProvider.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Synchronizes two streaming providers to the same playback position.
    /// Useful for seamless crossfades.
    /// </summary>
    /// <param name="sourceProvider">The source provider to read position from.</param>
    /// <param name="targetProvider">The target provider to seek.</param>
    public void SynchronizeProviders(StreamingLoopProvider sourceProvider, StreamingLoopProvider targetProvider)
    {
        if (sourceProvider == null || targetProvider == null)
            return;

        try
        {
            long sourcePosition = sourceProvider.Position;
            targetProvider.SeekToSample(sourcePosition);

            _logger?.LogDebug("Synchronized crossfade providers to position: {Position} samples", sourcePosition);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to synchronize streaming providers for crossfade");
        }
    }

    /// <summary>
    /// Removes track metadata from the cache.
    /// Does NOT affect currently playing tracks.
    /// </summary>
    /// <param name="trackName">The track name to remove.</param>
    public void UnloadTrackMetadata(string trackName)
    {
        if (_trackMetadataCache.Remove(trackName, out var trackData))
        {
            trackData.Dispose();
            _logger?.LogDebug("Unloaded track metadata: {TrackName}", trackName);
        }
    }

    /// <summary>
    /// Clears all cached track metadata.
    /// </summary>
    public void ClearCache()
    {
        foreach (var trackData in _trackMetadataCache.Values)
        {
            trackData.Dispose();
        }

        _trackMetadataCache.Clear();
        _logger?.LogDebug("Cleared all streaming track metadata cache");
    }

    /// <summary>
    /// Gets the number of cached track metadata entries.
    /// </summary>
    public int CachedTrackCount => _trackMetadataCache.Count;
}
