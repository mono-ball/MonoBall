using MonoBallFramework.Game.Engine.Audio.Core;

namespace MonoBallFramework.Game.Engine.Audio.Services.Streaming;

/// <summary>
///     Wraps a StreamingMusicProvider to provide infinite looping with support for custom loop points.
///     Behavior:
///     - Plays from start (sample 0) to loop end
///     - When loop end is reached, seeks back to loop start (not beginning)
///     - Supports seamless looping for music tracks with intro sections
///     Thread-safe for concurrent Read() calls from the audio thread.
/// </summary>
public class StreamingLoopProvider : ISampleProvider, IDisposable
{
    private readonly bool _enableLooping;
    private readonly object _loopLock = new();
    private readonly StreamingMusicProvider _source;
    private bool _disposed;

    /// <summary>
    ///     Creates a looping provider with optional custom loop points.
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
            LoopStartSample = loopStartSamples.Value * channels;
        }
        else
        {
            LoopStartSample = 0; // Default: loop from beginning
        }

        if (loopStartSamples.HasValue && loopLengthSamples.HasValue)
        {
            // Custom loop end = loop start + loop length
            LoopEndSample = (loopStartSamples.Value + loopLengthSamples.Value) * channels;
        }
        else
        {
            // Default: loop to end of file
            LoopEndSample = source.TotalSamples;
        }

        // Validate loop points
        if (LoopStartSample < 0 || LoopStartSample >= source.TotalSamples)
        {
            throw new ArgumentException(
                $"Invalid loop start: {LoopStartSample} (total samples: {source.TotalSamples})");
        }

        if (LoopEndSample <= LoopStartSample || LoopEndSample > source.TotalSamples)
        {
            throw new ArgumentException(
                $"Invalid loop end: {LoopEndSample} (must be > {LoopStartSample} and <= {source.TotalSamples})");
        }
    }

    /// <summary>
    ///     Gets the current position in the source stream.
    /// </summary>
    public long Position => _source.Position;

    /// <summary>
    ///     Gets the loop start position in samples (interleaved).
    /// </summary>
    public long LoopStartSample { get; }

    /// <summary>
    ///     Gets the loop end position in samples (interleaved).
    /// </summary>
    public long LoopEndSample { get; }

    /// <summary>
    ///     Disposes the underlying streaming provider.
    /// </summary>
    public void Dispose()
    {
        lock (_loopLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _source?.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    public AudioFormat Format => _source.Format;

    /// <summary>
    ///     Reads samples with automatic looping. Thread-safe.
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
            {
                return 0;
            }

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
            int consecutiveZeroReads = 0;
            const int MaxZeroReads = 3;

            while (totalRead < count)
            {
                long currentPosition = _source.Position;

                // Calculate how many samples we can read before hitting loop end
                long samplesUntilLoopEnd = LoopEndSample - currentPosition;

                if (samplesUntilLoopEnd <= 0)
                {
                    // At or past loop end - seek to loop start
                    _source.SeekToSample(LoopStartSample);
                    consecutiveZeroReads = 0; // Reset counter on successful seek
                    continue;
                }

                // Read up to loop end or remaining buffer space
                int toRead = (int)Math.Min(count - totalRead, samplesUntilLoopEnd);
                int read = _source.Read(buffer, offset + totalRead, toRead);

                if (read == 0)
                {
                    consecutiveZeroReads++;

                    if (consecutiveZeroReads >= MaxZeroReads)
                    {
                        // Too many consecutive zero reads - fill remaining with silence to prevent infinite loop
                        Array.Clear(buffer, offset + totalRead, count - totalRead);
                        return count;
                    }

                    // Unexpected EOF (shouldn't happen if loop end is set correctly)
                    // Seek to loop start and try again
                    _source.SeekToSample(LoopStartSample);

                    // Try reading again
                    read = _source.Read(buffer, offset + totalRead, count - totalRead);

                    if (read == 0)
                    {
                        // Empty source or corrupted file - fill with silence
                        Array.Clear(buffer, offset + totalRead, count - totalRead);
                        return count;
                    }
                }
                else
                {
                    consecutiveZeroReads = 0; // Reset counter on successful read
                }

                totalRead += read;

                // Check if we've reached loop end after this read
                if (_source.Position >= LoopEndSample)
                {
                    // Loop back to start
                    _source.SeekToSample(LoopStartSample);
                    consecutiveZeroReads = 0; // Reset counter on successful seek
                }
            }

            return totalRead;
        }
    }

    /// <summary>
    ///     Seeks to a specific sample position. Thread-safe.
    ///     Useful for synchronizing crossfades.
    /// </summary>
    /// <param name="samplePosition">Target sample position (interleaved samples).</param>
    public void SeekToSample(long samplePosition)
    {
        lock (_loopLock)
        {
            if (_disposed)
            {
                return;
            }

            _source.SeekToSample(samplePosition);
        }
    }

    /// <summary>
    ///     Resets the stream to the beginning (sample 0, not loop start).
    /// </summary>
    public void Reset()
    {
        SeekToSample(0);
    }
}
