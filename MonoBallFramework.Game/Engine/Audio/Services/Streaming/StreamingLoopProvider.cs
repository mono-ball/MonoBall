using MonoBallFramework.Game.Engine.Audio.Core;

namespace MonoBallFramework.Game.Engine.Audio.Services.Streaming;

/// <summary>
/// Wraps a StreamingMusicProvider to provide infinite looping with support for custom loop points.
///
/// Behavior:
/// - Plays from start (sample 0) to loop end
/// - When loop end is reached, seeks back to loop start (not beginning)
/// - Supports seamless looping for music tracks with intro sections
///
/// Thread-safe for concurrent Read() calls from the audio thread.
/// </summary>
public class StreamingLoopProvider : ISampleProvider, IDisposable
{
    private readonly StreamingMusicProvider _source;
    private readonly long _loopStartSample;  // In total samples (interleaved)
    private readonly long _loopEndSample;    // In total samples (interleaved)
    private readonly bool _enableLooping;
    private readonly object _loopLock = new();
    private bool _disposed;

    /// <summary>
    /// Creates a looping provider with optional custom loop points.
    /// </summary>
    /// <param name="source">The source streaming provider.</param>
    /// <param name="enableLooping">Whether to enable looping. If false, plays once and returns silence.</param>
    /// <param name="loopStartSamples">Loop start in per-channel samples. Null = loop from beginning (sample 0).</param>
    /// <param name="loopLengthSamples">Loop length in per-channel samples. Null = loop to end of file.</param>
    public StreamingLoopProvider(
        StreamingMusicProvider source,
        bool enableLooping = true,
        int? loopStartSamples = null,
        int? loopLengthSamples = null)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _enableLooping = enableLooping;

        int channels = source.Format.Channels;

        // Convert per-channel samples to total samples (interleaved format)
        if (loopStartSamples.HasValue)
        {
            _loopStartSample = loopStartSamples.Value * channels;
        }
        else
        {
            _loopStartSample = 0; // Default: loop from beginning
        }

        if (loopStartSamples.HasValue && loopLengthSamples.HasValue)
        {
            // Custom loop end = loop start + loop length
            _loopEndSample = (loopStartSamples.Value + loopLengthSamples.Value) * channels;
        }
        else
        {
            // Default: loop to end of file
            _loopEndSample = source.TotalSamples;
        }

        // Validate loop points
        if (_loopStartSample < 0 || _loopStartSample >= source.TotalSamples)
        {
            throw new ArgumentException($"Invalid loop start: {_loopStartSample} (total samples: {source.TotalSamples})");
        }

        if (_loopEndSample <= _loopStartSample || _loopEndSample > source.TotalSamples)
        {
            throw new ArgumentException($"Invalid loop end: {_loopEndSample} (must be > {_loopStartSample} and <= {source.TotalSamples})");
        }
    }

    public AudioFormat Format => _source.Format;

    /// <summary>
    /// Gets the current position in the source stream.
    /// </summary>
    public long Position => _source.Position;

    /// <summary>
    /// Gets the loop start position in samples (interleaved).
    /// </summary>
    public long LoopStartSample => _loopStartSample;

    /// <summary>
    /// Gets the loop end position in samples (interleaved).
    /// </summary>
    public long LoopEndSample => _loopEndSample;

    /// <summary>
    /// Reads samples with automatic looping. Thread-safe.
    /// </summary>
    /// <param name="buffer">Destination buffer for samples.</param>
    /// <param name="offset">Offset in the buffer to start writing.</param>
    /// <param name="count">Number of samples to read.</param>
    /// <returns>Number of samples actually read (always returns count if looping is enabled).</returns>
    public int Read(float[] buffer, int offset, int count)
    {
        lock (_loopLock)
        {
            if (_disposed)
                return 0;

            if (!_enableLooping)
            {
                // Non-looping: read once and return silence at EOF
                int read = _source.Read(buffer, offset, count);
                if (read < count)
                {
                    // Fill remaining buffer with silence
                    Array.Clear(buffer, offset + read, count - read);
                }
                return count;
            }

            int totalRead = 0;

            while (totalRead < count)
            {
                long currentPosition = _source.Position;

                // Calculate how many samples we can read before hitting loop end
                long samplesUntilLoopEnd = _loopEndSample - currentPosition;

                if (samplesUntilLoopEnd <= 0)
                {
                    // At or past loop end - seek to loop start
                    _source.SeekToSample(_loopStartSample);
                    continue;
                }

                // Read up to loop end or remaining buffer space
                int toRead = (int)Math.Min(count - totalRead, samplesUntilLoopEnd);
                int read = _source.Read(buffer, offset + totalRead, toRead);

                if (read == 0)
                {
                    // Unexpected EOF (shouldn't happen if loop end is set correctly)
                    // Seek to loop start and try again
                    _source.SeekToSample(_loopStartSample);

                    // Try reading again
                    read = _source.Read(buffer, offset + totalRead, count - totalRead);

                    if (read == 0)
                    {
                        // Empty source or corrupted file - fill with silence
                        Array.Clear(buffer, offset + totalRead, count - totalRead);
                        return count;
                    }
                }

                totalRead += read;

                // Check if we've reached loop end after this read
                if (_source.Position >= _loopEndSample)
                {
                    // Loop back to start
                    _source.SeekToSample(_loopStartSample);
                }
            }

            return totalRead;
        }
    }

    /// <summary>
    /// Seeks to a specific sample position. Thread-safe.
    /// Useful for synchronizing crossfades.
    /// </summary>
    /// <param name="samplePosition">Target sample position (interleaved samples).</param>
    public void SeekToSample(long samplePosition)
    {
        lock (_loopLock)
        {
            if (_disposed)
                return;

            _source.SeekToSample(samplePosition);
        }
    }

    /// <summary>
    /// Resets the stream to the beginning (sample 0, not loop start).
    /// </summary>
    public void Reset()
    {
        SeekToSample(0);
    }

    /// <summary>
    /// Disposes the underlying streaming provider.
    /// </summary>
    public void Dispose()
    {
        lock (_loopLock)
        {
            if (_disposed)
                return;

            _disposed = true;
            _source?.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}
