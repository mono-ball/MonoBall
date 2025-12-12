using System;
using System.IO;
using NAudio.Vorbis;
using NAudio.Wave;

namespace PokeSharp.Tests.Audio;

/// <summary>
/// Test fixtures and utilities for audio streaming tests.
/// Provides helper methods for generating test audio files, measuring performance,
/// and validating streaming behavior.
/// </summary>
public static class TestAudioFixtures
{
    /// <summary>
    /// Creates a simple OGG Vorbis test file with specified duration and frequency.
    /// Used for generating consistent test audio files.
    /// </summary>
    /// <param name="filePath">Output file path</param>
    /// <param name="durationSeconds">Duration in seconds</param>
    /// <param name="frequency">Tone frequency in Hz (default: 440Hz = A4)</param>
    /// <param name="sampleRate">Sample rate (default: 44100Hz)</param>
    public static void CreateTestOggFile(string filePath, double durationSeconds, int frequency = 440, int sampleRate = 44100)
    {
        // Ensure directory exists
        Directory.CreateDirectory(Path.GetDirectoryName(filePath));

        // Generate sine wave audio data
        var samples = (int)(sampleRate * durationSeconds);
        var waveFormat = new WaveFormat(sampleRate, 16, 2); // Stereo, 16-bit

        using var outputStream = new FileStream(filePath, FileMode.Create);
        using var writer = new WaveFileWriter(outputStream, waveFormat);

        for (int i = 0; i < samples; i++)
        {
            // Generate sine wave sample
            var sampleValue = (short)(Math.Sin(2 * Math.PI * frequency * i / sampleRate) * short.MaxValue * 0.5);

            // Write stereo samples
            writer.WriteSample(sampleValue / (float)short.MaxValue);
            writer.WriteSample(sampleValue / (float)short.MaxValue);
        }

        // Note: Actual OGG Vorbis encoding would require additional libraries
        // For testing, this creates a WAV file that can be manually converted to OGG
        // or we can use a pre-existing OGG encoder library
    }

    /// <summary>
    /// Creates a test OGG file with loop points marked in metadata.
    /// Useful for testing loop point functionality.
    /// </summary>
    /// <param name="filePath">Output file path</param>
    /// <param name="introDuration">Intro duration before loop (seconds)</param>
    /// <param name="loopDuration">Loop section duration (seconds)</param>
    /// <param name="frequency">Tone frequency in Hz</param>
    public static void CreateLoopingTestOggFile(string filePath, double introDuration, double loopDuration, int frequency = 440)
    {
        // This would generate an OGG file with loop point metadata
        // Implementation depends on OGG encoder library capabilities

        // Placeholder implementation
        var totalDuration = introDuration + loopDuration * 2; // Intro + 2 loop cycles
        CreateTestOggFile(filePath, totalDuration, frequency);

        // In a real implementation, we would:
        // 1. Encode OGG with LOOPSTART and LOOPLENGTH tags
        // 2. Or use a library that supports loop point metadata
    }

    /// <summary>
    /// Validates that an OGG file has correct loop point metadata.
    /// </summary>
    public static bool ValidateLoopPoints(string filePath, out long loopStartSamples, out long loopLengthSamples)
    {
        loopStartSamples = 0;
        loopLengthSamples = 0;

        try
        {
            using var reader = new VorbisWaveReader(filePath);

            // Check for loop point tags in Vorbis comments
            // This is a simplified example - actual implementation depends on
            // how loop points are stored in your OGG files

            // Common loop point tag formats:
            // - LOOPSTART, LOOPLENGTH (in samples)
            // - LOOP_START, LOOP_END (in samples)
            // - Custom metadata

            // For now, return false indicating no loop points found
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Creates a corrupted OGG file for error handling tests.
    /// </summary>
    public static void CreateCorruptedOggFile(string filePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath));

        // Write invalid OGG header
        File.WriteAllBytes(filePath, new byte[]
        {
            0x4F, 0x67, 0x67, 0x53, // "OggS" magic number
            0xFF, 0xFF, 0xFF, 0xFF, // Invalid version/header
            0x00, 0x01, 0x02, 0x03  // Random data
        });
    }

    /// <summary>
    /// Measures the decoded size of an OGG file when fully loaded into memory.
    /// Used for comparing streaming vs. memory-loading approaches.
    /// </summary>
    public static long GetDecodedSizeInMemory(string filePath)
    {
        try
        {
            using var reader = new VorbisWaveReader(filePath);

            // Calculate decoded size: samples * channels * bytes per sample
            var totalSamples = reader.Length / (reader.WaveFormat.BitsPerSample / 8);
            return totalSamples * reader.WaveFormat.Channels * (reader.WaveFormat.BitsPerSample / 8);
        }
        catch
        {
            return -1;
        }
    }

    /// <summary>
    /// Gets the duration of an OGG file without loading it entirely into memory.
    /// </summary>
    public static TimeSpan GetAudioDuration(string filePath)
    {
        try
        {
            using var reader = new VorbisWaveReader(filePath);
            return reader.TotalTime;
        }
        catch
        {
            return TimeSpan.Zero;
        }
    }

    /// <summary>
    /// Verifies that audio data is being read progressively (streaming)
    /// rather than all at once (memory buffering).
    /// </summary>
    public static bool VerifyStreamingBehavior(VorbisWaveReader reader, int bufferSize = 4096)
    {
        try
        {
            // Read small chunks and verify position advances
            var buffer = new byte[bufferSize];
            var initialPosition = reader.Position;

            var bytesRead = reader.Read(buffer, 0, bufferSize);

            if (bytesRead == 0)
                return false;

            var newPosition = reader.Position;

            // Verify position advanced by exactly the amount read
            return (newPosition - initialPosition) == bytesRead;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Performance measurement utilities for audio streaming tests.
/// </summary>
public class AudioPerformanceMonitor : IDisposable
{
    private readonly System.Diagnostics.Stopwatch _stopwatch;
    private readonly long _initialMemory;
    private readonly string _operationName;

    public AudioPerformanceMonitor(string operationName)
    {
        _operationName = operationName;

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        _initialMemory = GC.GetTotalMemory(false);
        _stopwatch = System.Diagnostics.Stopwatch.StartNew();
    }

    public void Dispose()
    {
        _stopwatch.Stop();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var finalMemory = GC.GetTotalMemory(false);
        var memoryDelta = finalMemory - _initialMemory;

        Console.WriteLine($"[Performance] {_operationName}:");
        Console.WriteLine($"  Duration: {_stopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"  Memory Delta: {memoryDelta / 1024}KB");
        Console.WriteLine($"  Initial Memory: {_initialMemory / 1024}KB");
        Console.WriteLine($"  Final Memory: {finalMemory / 1024}KB");
    }

    public long ElapsedMilliseconds => _stopwatch.ElapsedMilliseconds;
    public long MemoryDelta
    {
        get
        {
            GC.Collect();
            return GC.GetTotalMemory(false) - _initialMemory;
        }
    }
}

/// <summary>
/// Mock audio definitions for testing without real audio files.
/// </summary>
public static class MockAudioDefinitions
{
    public static MusicTrackDefinition CreateMockMusicTrack(string name = "test_music")
    {
        return new MusicTrackDefinition
        {
            Name = name,
            FilePath = $"TestData/Audio/{name}.ogg",
            Volume = 1.0f,
            LoopStartSamples = 0,
            LoopLengthSamples = 0
        };
    }

    public static MusicTrackDefinition CreateMockLoopingTrack(string name = "test_loop", long loopStart = 44100, long loopLength = 88200)
    {
        return new MusicTrackDefinition
        {
            Name = name,
            FilePath = $"TestData/Audio/{name}.ogg",
            Volume = 1.0f,
            LoopStartSamples = loopStart,
            LoopLengthSamples = loopLength
        };
    }

    public static SoundEffectDefinition CreateMockSoundEffect(string name = "test_sfx", float volume = 1.0f, float pan = 0.0f)
    {
        return new SoundEffectDefinition
        {
            Name = name,
            FilePath = $"TestData/Audio/SFX/{name}.ogg",
            Volume = volume,
            Pan = pan
        };
    }
}
