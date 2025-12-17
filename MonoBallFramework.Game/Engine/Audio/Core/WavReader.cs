namespace MonoBallFramework.Game.Engine.Audio.Core;

/// <summary>
///     Simple WAV file reader that provides audio samples in float format.
///     Supports 8-bit, 16-bit, 24-bit, and 32-bit PCM as well as 32-bit float WAV files.
///     Thread-safe for concurrent Read() calls.
/// </summary>
public class WavReader : ISeekableSampleProvider, IDisposable
{
    private readonly int _bytesPerSample;
    private readonly long _dataLength;
    private readonly long _dataStartPosition;
    private readonly BinaryReader _reader;
    private readonly Lock _readLock = new();
    private readonly Stream _stream;
    private bool _disposed;

    /// <summary>
    ///     Creates a new WAV reader for the specified file.
    /// </summary>
    /// <param name="filePath">Path to the WAV file.</param>
    public WavReader(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            throw new ArgumentNullException(nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Audio file not found: {filePath}", filePath);
        }

        _stream = File.OpenRead(filePath);
        _reader = new BinaryReader(_stream);

        try
        {
            ParseWavHeader(out int sampleRate, out int channels, out int bitsPerSample, out bool isFloat,
                out _dataStartPosition, out _dataLength);
            Format = new AudioFormat(sampleRate, channels, bitsPerSample, isFloat);
            _bytesPerSample = bitsPerSample / 8;
        }
        catch
        {
            _reader.Dispose();
            _stream.Dispose();
            throw;
        }
    }

    /// <summary>
    ///     Gets the total duration of the audio.
    /// </summary>
    public TimeSpan TotalTime => Format.SamplesToTime(TotalSamples);

    /// <summary>
    ///     Disposes the reader and releases resources.
    /// </summary>
    public void Dispose()
    {
        lock (_readLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _reader?.Dispose();
            _stream?.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Gets the audio format.
    /// </summary>
    public AudioFormat Format { get; }

    /// <summary>
    ///     Gets the total number of samples (interleaved).
    /// </summary>
    public long TotalSamples => _dataLength / _bytesPerSample;

    /// <summary>
    ///     Gets the current position in samples (interleaved).
    /// </summary>
    public long Position
    {
        get
        {
            lock (_readLock)
            {
                if (_disposed)
                {
                    return 0;
                }

                return (_stream.Position - _dataStartPosition) / _bytesPerSample;
            }
        }
    }

    /// <summary>
    ///     Reads samples from the WAV stream and converts to float format.
    ///     Thread-safe.
    /// </summary>
    public int Read(float[] buffer, int offset, int count)
    {
        lock (_readLock)
        {
            if (_disposed)
            {
                return 0;
            }

            int samplesRead = 0;
            long remainingBytes = _dataStartPosition + _dataLength - _stream.Position;
            int maxSamples = (int)Math.Min(count, remainingBytes / _bytesPerSample);

            try
            {
                for (int i = 0; i < maxSamples; i++)
                {
                    float sample = ReadSample();
                    buffer[offset + i] = sample;
                    samplesRead++;
                }
            }
            catch (EndOfStreamException)
            {
                // Reached end of data
            }

            return samplesRead;
        }
    }

    /// <summary>
    ///     Seeks to a specific sample position.
    ///     Thread-safe.
    /// </summary>
    public void SeekToSample(long samplePosition)
    {
        lock (_readLock)
        {
            if (_disposed)
            {
                return;
            }

            long bytePosition = samplePosition * _bytesPerSample;
            bytePosition = Math.Max(0, Math.Min(bytePosition, _dataLength));
            _stream.Position = _dataStartPosition + bytePosition;
        }
    }

    /// <summary>
    ///     Seeks to a specific time position.
    /// </summary>
    public void SeekToTime(TimeSpan time)
    {
        long samplePosition = Format.TimeToSamples(time);
        SeekToSample(samplePosition);
    }

    /// <summary>
    ///     Resets the reader to the beginning.
    /// </summary>
    public void Reset()
    {
        SeekToSample(0);
    }

    private void ParseWavHeader(out int sampleRate, out int channels, out int bitsPerSample, out bool isFloat,
        out long dataStart, out long dataLength)
    {
        // Read RIFF header
        string riff = new(_reader.ReadChars(4));
        if (riff != "RIFF")
        {
            throw new InvalidDataException("Not a valid WAV file - missing RIFF header");
        }

        _reader.ReadInt32(); // File size minus 8

        string wave = new(_reader.ReadChars(4));
        if (wave != "WAVE")
        {
            throw new InvalidDataException("Not a valid WAV file - missing WAVE format");
        }

        // Initialize output values
        sampleRate = 0;
        channels = 0;
        bitsPerSample = 0;
        isFloat = false;
        dataStart = 0;
        dataLength = 0;

        // Read chunks until we find fmt and data
        bool foundFmt = false;
        bool foundData = false;

        while (!foundFmt || !foundData)
        {
            if (_stream.Position >= _stream.Length - 8)
            {
                throw new InvalidDataException("WAV file is missing required chunks");
            }

            string chunkId = new(_reader.ReadChars(4));
            int chunkSize = _reader.ReadInt32();

            if (chunkId == "fmt ")
            {
                int audioFormat = _reader.ReadInt16();
                channels = _reader.ReadInt16();
                sampleRate = _reader.ReadInt32();
                _reader.ReadInt32(); // Byte rate
                _reader.ReadInt16(); // Block align
                bitsPerSample = _reader.ReadInt16();

                // audioFormat: 1 = PCM, 3 = IEEE float
                isFloat = audioFormat == 3;

                if (audioFormat != 1 && audioFormat != 3)
                {
                    throw new NotSupportedException(
                        $"Unsupported WAV format: {audioFormat}. Only PCM (1) and IEEE float (3) are supported.");
                }

                // Skip any extra format bytes
                int extraBytes = chunkSize - 16;
                if (extraBytes > 0)
                {
                    _reader.ReadBytes(extraBytes);
                }

                foundFmt = true;
            }
            else if (chunkId == "data")
            {
                dataStart = _stream.Position;
                dataLength = chunkSize;
                foundData = true;

                // Don't skip data chunk - we'll read from it
            }
            else
            {
                // Skip unknown chunk
                _reader.ReadBytes(chunkSize);
            }
        }

        // Position stream at start of data
        _stream.Position = dataStart;
    }

    private float ReadSample()
    {
        if (Format.IsFloat && Format.BitsPerSample == 32)
        {
            return _reader.ReadSingle();
        }

        return Format.BitsPerSample switch
        {
            8 => (_reader.ReadByte() - 128) / 128f,
            16 => _reader.ReadInt16() / 32768f,
            24 => Read24BitSample() / 8388608f,
            32 => _reader.ReadInt32() / 2147483648f,
            _ => throw new NotSupportedException($"Unsupported bit depth: {Format.BitsPerSample}")
        };
    }

    private int Read24BitSample()
    {
        byte b1 = _reader.ReadByte();
        byte b2 = _reader.ReadByte();
        byte b3 = _reader.ReadByte();

        // Sign extend if negative
        int value = (b3 << 16) | (b2 << 8) | b1;
        if ((b3 & 0x80) != 0)
        {
            value |= unchecked((int)0xFF000000); // Sign extend
        }

        return value;
    }
}
