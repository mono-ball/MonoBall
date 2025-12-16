namespace MonoBallFramework.Game.Engine.Audio.Core;

/// <summary>
/// Represents the format of audio data, including sample rate, channels, and bit depth.
/// Cross-platform audio format definition for PortAudio-based playback.
/// </summary>
public sealed class AudioFormat
{
    /// <summary>
    /// Gets the sample rate in Hz (e.g., 44100, 48000).
    /// </summary>
    public int SampleRate { get; }

    /// <summary>
    /// Gets the number of audio channels (1 = mono, 2 = stereo).
    /// </summary>
    public int Channels { get; }

    /// <summary>
    /// Gets the bits per sample (typically 16 or 32 for float).
    /// </summary>
    public int BitsPerSample { get; }

    /// <summary>
    /// Gets whether the audio data is in floating-point format.
    /// </summary>
    public bool IsFloat { get; }

    /// <summary>
    /// Gets the number of bytes per sample (BitsPerSample / 8).
    /// </summary>
    public int BytesPerSample => BitsPerSample / 8;

    /// <summary>
    /// Gets the block alignment (channels * bytes per sample).
    /// </summary>
    public int BlockAlign => Channels * BytesPerSample;

    /// <summary>
    /// Gets the average bytes per second.
    /// </summary>
    public int AverageBytesPerSecond => SampleRate * BlockAlign;

    /// <summary>
    /// Creates a new audio format.
    /// </summary>
    /// <param name="sampleRate">Sample rate in Hz.</param>
    /// <param name="channels">Number of channels (1 or 2).</param>
    /// <param name="bitsPerSample">Bits per sample (16 or 32).</param>
    /// <param name="isFloat">Whether the format uses floating-point samples.</param>
    public AudioFormat(int sampleRate, int channels, int bitsPerSample = 32, bool isFloat = true)
    {
        if (sampleRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(sampleRate), "Sample rate must be positive.");
        if (channels < 1 || channels > 2)
            throw new ArgumentOutOfRangeException(nameof(channels), "Channels must be 1 or 2.");
        if (bitsPerSample != 8 && bitsPerSample != 16 && bitsPerSample != 32)
            throw new ArgumentOutOfRangeException(nameof(bitsPerSample), "Bits per sample must be 8, 16, or 32.");

        SampleRate = sampleRate;
        Channels = channels;
        BitsPerSample = bitsPerSample;
        IsFloat = isFloat;
    }

    /// <summary>
    /// Creates a standard IEEE float format (32-bit float, typically used for processing).
    /// </summary>
    public static AudioFormat CreateFloat(int sampleRate, int channels) =>
        new(sampleRate, channels, 32, true);

    /// <summary>
    /// Creates a standard CD-quality format (16-bit PCM, 44.1kHz, stereo).
    /// </summary>
    public static AudioFormat CreateCdQuality() =>
        new(44100, 2, 16, false);

    /// <summary>
    /// Converts a sample count (interleaved) to time duration.
    /// </summary>
    /// <param name="sampleCount">Number of samples (interleaved).</param>
    /// <returns>Duration as TimeSpan.</returns>
    public TimeSpan SamplesToTime(long sampleCount)
    {
        double seconds = (double)sampleCount / Channels / SampleRate;
        return TimeSpan.FromSeconds(seconds);
    }

    /// <summary>
    /// Converts a time duration to sample count (interleaved).
    /// </summary>
    /// <param name="time">Duration to convert.</param>
    /// <returns>Number of samples (interleaved).</returns>
    public long TimeToSamples(TimeSpan time)
    {
        return (long)(time.TotalSeconds * SampleRate * Channels);
    }

    public override string ToString() =>
        $"{SampleRate}Hz, {Channels}ch, {BitsPerSample}bit{(IsFloat ? " float" : "")}";
}

