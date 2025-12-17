namespace MonoBallFramework.Game.Engine.Audio.Core;

/// <summary>
///     Pitch shifts audio by resampling. Uses linear interpolation for quality.
///     Pitch range: -1.0 (octave down) to +1.0 (octave up).
///     Thread-safe for concurrent Read() calls.
/// </summary>
public class PitchShiftProvider : ISampleProvider
{
    // Interpolation state
    private readonly float[] _lastSamples;
    private readonly object _lock = new();
    private readonly double _pitchRatio; // 0.5 to 2.0 (2^pitch)
    private readonly ISampleProvider _source;
    private float[]? _sourceBuffer;
    private double _sourcePosition;

    /// <summary>
    ///     Creates a pitch shift provider.
    /// </summary>
    /// <param name="source">Source audio provider</param>
    /// <param name="pitch">Pitch shift in range -1.0 (octave down) to +1.0 (octave up)</param>
    public PitchShiftProvider(ISampleProvider source, float pitch)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        Format = source.Format;

        // Clamp pitch to safe range
        pitch = Math.Clamp(pitch, -1.0f, 1.0f);

        // Convert pitch to ratio: pitch -1 to +1 maps to ratio 0.5 to 2.0
        // ratio = 2^pitch
        _pitchRatio = Math.Pow(2.0, pitch);

        // Initialize interpolation state
        _lastSamples = new float[Format.Channels];
        _sourcePosition = 0.0;

        // Pre-allocate source buffer for reading ahead
        // Buffer size based on worst case (pitch down requires more samples)
        int maxSourceSamples = (int)Math.Ceiling(4096 * 2.0) + Format.Channels;
        _sourceBuffer = new float[maxSourceSamples];
    }

    /// <summary>
    ///     Gets the audio format (same as source).
    /// </summary>
    public AudioFormat Format { get; }

    /// <summary>
    ///     Reads pitch-shifted audio samples.
    /// </summary>
    /// <param name="buffer">Destination buffer</param>
    /// <param name="offset">Offset in destination buffer</param>
    /// <param name="count">Number of samples to read</param>
    /// <returns>Number of samples actually read</returns>
    public int Read(float[] buffer, int offset, int count)
    {
        lock (_lock)
        {
            int samplesWritten = 0;
            int channels = Format.Channels;

            // Ensure source buffer is large enough
            int maxSourceSamples = (int)Math.Ceiling(count * _pitchRatio) + channels;
            if (_sourceBuffer == null || _sourceBuffer.Length < maxSourceSamples)
            {
                _sourceBuffer = new float[maxSourceSamples];
            }

            // Read source samples we'll need
            int sourceSamplesNeeded = (int)Math.Ceiling(count * _pitchRatio);
            int sourceSamplesRead = _source.Read(_sourceBuffer, 0, sourceSamplesNeeded);

            if (sourceSamplesRead == 0)
            {
                return 0; // End of stream
            }

            // Process each output sample using linear interpolation
            for (int i = 0; i < count; i += channels)
            {
                // Calculate source position
                int sourceIndex = (int)_sourcePosition;
                double fraction = _sourcePosition - sourceIndex;

                // Check if we have enough source samples
                if (sourceIndex + channels >= sourceSamplesRead)
                {
                    break; // Not enough source data
                }

                // Interpolate each channel
                for (int ch = 0; ch < channels; ch++)
                {
                    int idx = sourceIndex + ch;

                    // Linear interpolation between current and next sample
                    float sample1 = idx >= 0 && idx < sourceSamplesRead
                        ? _sourceBuffer[idx]
                        : _lastSamples[ch];

                    float sample2 = idx + channels < sourceSamplesRead
                        ? _sourceBuffer[idx + channels]
                        : sample1;

                    float interpolated = (float)(sample1 + ((sample2 - sample1) * fraction));

                    buffer[offset + i + ch] = interpolated;

                    // Store last sample for next iteration
                    if (idx < sourceSamplesRead)
                    {
                        _lastSamples[ch] = _sourceBuffer[idx];
                    }
                }

                samplesWritten += channels;

                // Advance source position by pitch ratio
                _sourcePosition += _pitchRatio * channels;
            }

            // Reset source position for next read
            _sourcePosition -= sourceSamplesRead;
            if (_sourcePosition < 0)
            {
                _sourcePosition = 0;
            }

            return samplesWritten;
        }
    }

    /// <summary>
    ///     Creates a PitchShiftProvider only if pitch shift is needed.
    ///     Returns the original provider if pitch is near zero.
    /// </summary>
    /// <param name="source">Source audio provider</param>
    /// <param name="pitch">Pitch shift value</param>
    /// <returns>PitchShiftProvider if needed, otherwise original source</returns>
    public static ISampleProvider CreateIfNeeded(ISampleProvider source, float pitch)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        // No pitch change needed if very close to zero
        if (Math.Abs(pitch) < 0.001f)
        {
            return source;
        }

        return new PitchShiftProvider(source, pitch);
    }
}
