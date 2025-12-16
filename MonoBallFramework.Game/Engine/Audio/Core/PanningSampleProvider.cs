namespace MonoBallFramework.Game.Engine.Audio.Core;

/// <summary>
/// Sample provider that applies stereo panning to audio samples.
/// Only works with stereo (2-channel) audio.
/// </summary>
public class PanningSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private volatile float _pan;

    /// <summary>
    /// Creates a new panning sample provider.
    /// </summary>
    /// <param name="source">The source sample provider (must be stereo).</param>
    public PanningSampleProvider(ISampleProvider source)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));

        if (source.Format.Channels != 2)
            throw new ArgumentException("Panning only works with stereo audio.", nameof(source));
    }

    /// <summary>
    /// Gets the audio format (same as source).
    /// </summary>
    public AudioFormat Format => _source.Format;

    /// <summary>
    /// Gets or sets the pan position (-1.0 = full left, 0.0 = center, 1.0 = full right).
    /// Thread-safe; changes take effect immediately.
    /// </summary>
    public float Pan
    {
        get => _pan;
        set => _pan = Math.Clamp(value, -1f, 1f);
    }

    /// <summary>
    /// Reads samples from the source and applies panning.
    /// Uses constant-power panning for smooth transitions.
    /// </summary>
    public int Read(float[] buffer, int offset, int count)
    {
        int samplesRead = _source.Read(buffer, offset, count);

        float pan = _pan;
        if (Math.Abs(pan) < 0.0001f) // Skip if centered
            return samplesRead;

        // Constant-power panning
        // When pan is at center (0), both channels are at full volume
        // When pan is at one extreme, that channel is at full and the other is at 0
        float leftGain, rightGain;

        if (pan < 0)
        {
            // Panning left: left channel stays full, right channel reduces
            leftGain = 1.0f;
            rightGain = 1.0f + pan; // pan is negative, so this reduces right
        }
        else
        {
            // Panning right: right channel stays full, left channel reduces
            leftGain = 1.0f - pan;
            rightGain = 1.0f;
        }

        // Process stereo pairs
        for (int i = 0; i < samplesRead; i += 2)
        {
            buffer[offset + i] *= leftGain;
            buffer[offset + i + 1] *= rightGain;
        }

        return samplesRead;
    }
}

