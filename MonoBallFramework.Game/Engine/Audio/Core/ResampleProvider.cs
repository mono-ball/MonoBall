namespace MonoBallFramework.Game.Engine.Audio.Core;

/// <summary>
///     Resamples audio from source sample rate to target sample rate using linear interpolation.
///     Thread-safe for concurrent Read() calls.
/// </summary>
public class ResampleProvider : ISampleProvider
{
    // Interpolation state
    private readonly float[] _lastSamples; // Last sample per channel for interpolation
    private readonly object _lock = new();
    private readonly double _resampleRatio;
    private readonly ISampleProvider _source;
    private double _samplePosition; // Fractional position in source
    private float[]? _sourceBuffer; // Pre-allocated source read buffer

    /// <summary>
    ///     Creates a new resampler that converts audio from source sample rate to target sample rate.
    /// </summary>
    /// <param name="source">Source audio provider</param>
    /// <param name="targetSampleRate">Target sample rate in Hz</param>
    /// <exception cref="ArgumentNullException">Thrown when source is null</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when target sample rate is invalid</exception>
    public ResampleProvider(ISampleProvider source, int targetSampleRate)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (targetSampleRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetSampleRate), "Target sample rate must be positive");
        }

        _source = source;

        // Calculate resample ratio (how much to advance source position per output sample)
        _resampleRatio = (double)source.Format.SampleRate / targetSampleRate;

        // Create output format with same channel count but different sample rate
        Format = new AudioFormat(targetSampleRate, source.Format.Channels);

        // Initialize interpolation state
        _lastSamples = new float[source.Format.Channels];
        _samplePosition = 0.0;

        // Pre-allocate source buffer for reading (4096 samples per channel)
        int sourceBufferSize = 4096 * source.Format.Channels;
        _sourceBuffer = new float[sourceBufferSize];
    }

    /// <summary>
    ///     Gets the output audio format after resampling.
    /// </summary>
    public AudioFormat Format { get; }

    /// <summary>
    ///     Reads resampled audio samples into the buffer.
    /// </summary>
    /// <param name="buffer">Destination buffer for resampled samples</param>
    /// <param name="offset">Offset in buffer to start writing</param>
    /// <param name="count">Number of samples to read (must be multiple of channel count)</param>
    /// <returns>Number of samples actually read</returns>
    public int Read(float[] buffer, int offset, int count)
    {
        if (buffer == null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }

        if (offset < 0 || offset >= buffer.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        if (count < 0 || offset + count > buffer.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        if (count % Format.Channels != 0)
        {
            throw new ArgumentException("Count must be a multiple of channel count", nameof(count));
        }

        lock (_lock)
        {
            int samplesWritten = 0;
            int channels = Format.Channels;

            // Ensure source buffer is allocated
            if (_sourceBuffer == null)
            {
                _sourceBuffer = new float[4096 * channels];
            }

            // Process output samples
            while (samplesWritten < count)
            {
                // Calculate how many source samples we need
                int sourceFrameIndex = (int)_samplePosition;
                double fractionalPart = _samplePosition - sourceFrameIndex;

                // Read source samples if needed
                int sourceSamplesNeeded = (sourceFrameIndex + 1) * channels;
                if (_sourceBuffer.Length < sourceSamplesNeeded)
                {
                    // Need to read more source samples
                    int samplesToRead = Math.Min(_sourceBuffer.Length, (count - samplesWritten) * 2);
                    samplesToRead = samplesToRead / channels * channels; // Align to frame boundary

                    int samplesRead = _source.Read(_sourceBuffer, 0, samplesToRead);

                    if (samplesRead == 0)
                    {
                        // End of source stream
                        break;
                    }

                    // Process the samples we just read
                    int framesRead = samplesRead / channels;

                    for (int frame = 0; frame < framesRead && samplesWritten < count; frame++)
                    {
                        // Get fractional position
                        double localPosition = _samplePosition - sourceFrameIndex;

                        // Interpolate each channel
                        for (int ch = 0; ch < channels; ch++)
                        {
                            int sourceIndex = (frame * channels) + ch;

                            float sample0 = frame == 0 && sourceFrameIndex > 0
                                ? _lastSamples[ch]
                                : _sourceBuffer[sourceIndex];

                            float sample1 = ((frame + 1) * channels) + ch < samplesRead
                                ? _sourceBuffer[((frame + 1) * channels) + ch]
                                : sample0; // Use same sample if at end

                            // Linear interpolation
                            float interpolated = sample0 + ((float)localPosition * (sample1 - sample0));

                            buffer[offset + samplesWritten] = interpolated;
                            samplesWritten++;
                        }

                        // Advance source position
                        _samplePosition += _resampleRatio;
                        sourceFrameIndex = (int)_samplePosition;

                        // Check if we've consumed this frame
                        if (sourceFrameIndex > frame)
                        {
                            break;
                        }
                    }

                    // Store last samples for next iteration
                    if (samplesRead >= channels)
                    {
                        for (int ch = 0; ch < channels; ch++)
                        {
                            _lastSamples[ch] = _sourceBuffer[samplesRead - channels + ch];
                        }
                    }

                    // Reset position for next read
                    int framesConsumed = (int)_samplePosition;
                    _samplePosition -= framesConsumed;
                }
                else
                {
                    // We have enough source samples in buffer
                    // Interpolate between samples at sourceFrameIndex and sourceFrameIndex + 1
                    for (int ch = 0; ch < channels; ch++)
                    {
                        int index0 = (sourceFrameIndex * channels) + ch;
                        int index1 = ((sourceFrameIndex + 1) * channels) + ch;

                        float sample0 = index0 < _sourceBuffer.Length
                            ? _sourceBuffer[index0]
                            : _lastSamples[ch];

                        float sample1 = index1 < _sourceBuffer.Length
                            ? _sourceBuffer[index1]
                            : sample0;

                        // Linear interpolation
                        float interpolated = sample0 + ((float)fractionalPart * (sample1 - sample0));

                        buffer[offset + samplesWritten] = interpolated;
                        samplesWritten++;
                    }

                    // Advance source position
                    _samplePosition += _resampleRatio;
                }
            }

            return samplesWritten;
        }
    }

    /// <summary>
    ///     Creates a resampler if the source sample rate doesn't match the target sample rate.
    ///     Returns the original provider if resampling is not needed.
    /// </summary>
    /// <param name="source">Source audio provider</param>
    /// <param name="targetSampleRate">Target sample rate in Hz</param>
    /// <returns>Resampled provider if needed, or original provider if sample rates match</returns>
    public static ISampleProvider CreateIfNeeded(ISampleProvider source, int targetSampleRate)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (targetSampleRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetSampleRate), "Target sample rate must be positive");
        }

        // Check if resampling is needed
        if (source.Format.SampleRate == targetSampleRate)
        {
            // No resampling needed
            return source;
        }

        // Create resampler
        return new ResampleProvider(source, targetSampleRate);
    }
}
