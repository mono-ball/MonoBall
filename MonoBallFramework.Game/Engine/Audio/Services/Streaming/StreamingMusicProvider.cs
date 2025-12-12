using NAudio.Vorbis;
using NAudio.Wave;

namespace MonoBallFramework.Game.Engine.Audio.Services.Streaming;

/// <summary>
/// Sample provider that streams audio data on-demand from a VorbisWaveReader.
/// This avoids loading entire audio files into memory, suitable for large music tracks.
/// Thread-safe for concurrent Read() calls from the audio thread.
/// </summary>
public class StreamingMusicProvider : ISampleProvider, IDisposable
{
    private readonly VorbisWaveReader _reader;
    private readonly ISampleProvider _sampleProvider;
    private readonly object _readLock = new();
    private bool _disposed;

    /// <summary>
    /// Creates a new streaming music provider from a file path.
    /// </summary>
    /// <param name="filePath">Absolute path to the OGG Vorbis file.</param>
    public StreamingMusicProvider(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            throw new ArgumentNullException(nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Audio file not found: {filePath}", filePath);

        _reader = new VorbisWaveReader(filePath);
        _sampleProvider = _reader.ToSampleProvider();
    }

    /// <summary>
    /// Creates a new streaming music provider from an existing VorbisWaveReader.
    /// Takes ownership of the reader and will dispose it.
    /// </summary>
    /// <param name="reader">The VorbisWaveReader to stream from.</param>
    internal StreamingMusicProvider(VorbisWaveReader reader)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _sampleProvider = _reader.ToSampleProvider();
    }

    public WaveFormat WaveFormat => _sampleProvider.WaveFormat;

    /// <summary>
    /// Gets the total length in samples (interleaved).
    /// </summary>
    public long TotalSamples => _reader.Length / (_reader.WaveFormat.BitsPerSample / 8);

    /// <summary>
    /// Gets the current position in samples (interleaved).
    /// </summary>
    public long Position
    {
        get
        {
            lock (_readLock)
            {
                if (_disposed) return 0;
                return _reader.Position / (_reader.WaveFormat.BitsPerSample / 8);
            }
        }
    }

    /// <summary>
    /// Reads samples from the stream. Thread-safe.
    /// </summary>
    /// <param name="buffer">Destination buffer for samples.</param>
    /// <param name="offset">Offset in the buffer to start writing.</param>
    /// <param name="count">Number of samples to read.</param>
    /// <returns>Number of samples actually read (may be less than count at EOF).</returns>
    public int Read(float[] buffer, int offset, int count)
    {
        lock (_readLock)
        {
            if (_disposed)
                return 0;

            try
            {
                return _sampleProvider.Read(buffer, offset, count);
            }
            catch (ObjectDisposedException)
            {
                // Reader was disposed during read
                return 0;
            }
        }
    }

    /// <summary>
    /// Seeks to a specific sample position. Thread-safe.
    /// </summary>
    /// <param name="samplePosition">Target sample position (interleaved samples).</param>
    public void SeekToSample(long samplePosition)
    {
        lock (_readLock)
        {
            if (_disposed)
                return;

            // Convert sample position to byte position
            // VorbisWaveReader.Position is in bytes
            long bytePosition = samplePosition * (_reader.WaveFormat.BitsPerSample / 8);

            // Clamp to valid range
            bytePosition = Math.Max(0, Math.Min(bytePosition, _reader.Length));

            _reader.Position = bytePosition;
        }
    }

    /// <summary>
    /// Resets the stream to the beginning. Thread-safe.
    /// </summary>
    /// <remarks>
    /// This method is kept for API compatibility but is not currently used internally.
    /// Consider using SeekToSample(0) directly if you need to reset playback position.
    /// </remarks>
    public void Reset()
    {
        SeekToSample(0);
    }

    /// <summary>
    /// Disposes the underlying VorbisWaveReader and releases resources.
    /// </summary>
    public void Dispose()
    {
        lock (_readLock)
        {
            if (_disposed)
                return;

            _disposed = true;
            _reader?.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}
