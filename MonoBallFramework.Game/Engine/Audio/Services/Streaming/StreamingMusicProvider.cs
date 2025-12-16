using MonoBallFramework.Game.Engine.Audio.Core;

namespace MonoBallFramework.Game.Engine.Audio.Services.Streaming;

/// <summary>
/// Sample provider that streams audio data on-demand from a Vorbis file.
/// This avoids loading entire audio files into memory, suitable for large music tracks.
/// Thread-safe for concurrent Read() calls from the audio thread.
/// </summary>
public class StreamingMusicProvider : ISeekableSampleProvider, IDisposable
{
    private readonly VorbisReader _reader;
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

        _reader = new VorbisReader(filePath);
    }

    /// <summary>
    /// Creates a new streaming music provider from an existing VorbisReader.
    /// Takes ownership of the reader and will dispose it.
    /// </summary>
    /// <param name="reader">The VorbisReader to stream from.</param>
    internal StreamingMusicProvider(VorbisReader reader)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
    }

    public AudioFormat Format => _reader.Format;

    /// <summary>
    /// Gets the total length in samples (interleaved).
    /// </summary>
    public long TotalSamples => _reader.TotalSamples;

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
                return _reader.Position;
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
                return _reader.Read(buffer, offset, count);
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

            _reader.SeekToSample(samplePosition);
        }
    }

    /// <summary>
    /// Resets the stream to the beginning. Thread-safe.
    /// </summary>
    public void Reset()
    {
        SeekToSample(0);
    }

    /// <summary>
    /// Disposes the underlying VorbisReader and releases resources.
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
