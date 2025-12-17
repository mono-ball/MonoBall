using MonoBallFramework.Game.Engine.Audio.Core;

namespace MonoBallFramework.Game.Engine.Audio.Services.Streaming;

/// <summary>
///     Holds streaming track data and metadata for a single music track.
///     Manages the lifecycle of streaming providers and ensures proper disposal.
/// </summary>
public class StreamingTrackData : IDisposable
{
    private bool _disposed;

    /// <summary>
    ///     Gets the track name/identifier.
    /// </summary>
    public required string TrackName { get; init; }

    /// <summary>
    ///     Gets the file path to the audio file.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    ///     Gets the audio format.
    /// </summary>
    public required AudioFormat AudioFormat { get; init; }

    /// <summary>
    ///     Gets the loop start position in samples (per channel). Null = loop from beginning.
    /// </summary>
    public int? LoopStartSamples { get; init; }

    /// <summary>
    ///     Gets the loop length in samples (per channel). Null = loop to end.
    /// </summary>
    public int? LoopLengthSamples { get; init; }

    /// <summary>
    ///     Gets whether this track has custom loop points defined.
    /// </summary>
    public bool HasLoopPoints => LoopStartSamples.HasValue && LoopLengthSamples.HasValue;

    /// <summary>
    ///     Disposes resources. Note: This does NOT dispose active streaming providers
    ///     created by CreateStreamingProvider() - those must be disposed separately.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Creates a new streaming music provider for this track.
    ///     Each call creates a new independent stream that can be played simultaneously.
    ///     Caller is responsible for disposing the returned provider.
    /// </summary>
    /// <returns>A new StreamingMusicProvider instance.</returns>
    public StreamingMusicProvider CreateStreamingProvider()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return new StreamingMusicProvider(FilePath);
    }

    /// <summary>
    ///     Creates a new looping provider for this track.
    ///     Automatically applies loop points if they are defined.
    ///     Caller is responsible for disposing the returned provider.
    /// </summary>
    /// <param name="enableLooping">Whether to enable looping.</param>
    /// <returns>A new StreamingLoopProvider instance.</returns>
    public StreamingLoopProvider CreateLoopingProvider(bool enableLooping = true)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        StreamingMusicProvider streamingProvider = CreateStreamingProvider();

        try
        {
            return new StreamingLoopProvider(
                streamingProvider,
                enableLooping,
                LoopStartSamples,
                LoopLengthSamples);
        }
        catch
        {
            // If loop provider creation fails, dispose the streaming provider
            streamingProvider.Dispose();
            throw;
        }
    }
}
