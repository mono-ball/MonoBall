using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using MonoBallFramework.Audio;
using Xunit;
using Xunit.Abstractions;

namespace PokeSharp.Tests.Audio;

/// <summary>
/// Comprehensive test suite for NAudioSoundEffectManager streaming functionality.
/// Tests verify streaming behavior for sound effects, concurrent playback, and resource cleanup.
/// </summary>
public class NAudioSoundEffectManagerStreamingTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly NAudioSoundEffectManager _manager;
    private readonly string _testSfxPath;
    private readonly string _testLongSfxPath;

    private long _initialMemory;
    private const long MaxStreamMemoryOverheadBytes = 512 * 1024; // 512KB per SFX stream

    public NAudioSoundEffectManagerStreamingTests(ITestOutputHelper output)
    {
        _output = output;
        _manager = new NAudioSoundEffectManager();

        // Setup test sound effect files
        _testSfxPath = Path.Combine("TestData", "Audio", "SFX", "test_sfx.ogg");
        _testLongSfxPath = Path.Combine("TestData", "Audio", "SFX", "test_long_sfx.ogg");

        // Capture memory baseline
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        _initialMemory = GC.GetTotalMemory(false);
    }

    public void Dispose()
    {
        _manager?.Dispose();
        GC.Collect();
    }

    #region Helper Methods

    private long GetMemoryOverhead()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        return GC.GetTotalMemory(false) - _initialMemory;
    }

    private bool VerifyFileHandleClosed(string filePath)
    {
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    #endregion

    #region 1. Basic Sound Effect Streaming

    [Fact]
    public async Task PlaySoundEffect_StreamsFromDisk_CompletesSuccessfully()
    {
        // Arrange
        var sfxDefinition = new SoundEffectDefinition
        {
            FilePath = _testSfxPath,
            Volume = 1.0f
        };

        // Act
        var instanceId = _manager.PlaySoundEffect(sfxDefinition);

        // Assert
        Assert.True(instanceId > 0, "Should return valid instance ID");
        Assert.True(_manager.IsPlaying(instanceId), "Sound effect should be playing");

        await Task.Delay(200);
        _output.WriteLine($"Sound effect instance {instanceId} playing successfully");
    }

    [Fact]
    public async Task PlaySoundEffect_MemoryUsageStaysLow_UnderHalfMBPerStream()
    {
        // Arrange
        var sfxDefinition = new SoundEffectDefinition
        {
            FilePath = _testSfxPath,
            Volume = 1.0f
        };

        var baselineMemory = GetMemoryOverhead();

        // Act
        var instanceId = _manager.PlaySoundEffect(sfxDefinition);
        await Task.Delay(200);

        var streamingMemory = GetMemoryOverhead();

        // Assert
        var memoryIncrease = streamingMemory - baselineMemory;
        Assert.True(memoryIncrease < MaxStreamMemoryOverheadBytes,
            $"Memory overhead ({memoryIncrease / 1024}KB) should be under 512KB for SFX streaming");

        _output.WriteLine($"SFX memory overhead: {memoryIncrease / 1024}KB");
    }

    [Fact]
    public async Task PlaySoundEffect_CompletesAndDisposesAutomatically_NoLeaks()
    {
        // Arrange
        var sfxDefinition = new SoundEffectDefinition
        {
            FilePath = _testSfxPath,
            Volume = 1.0f
        };

        // Act
        var instanceId = _manager.PlaySoundEffect(sfxDefinition);

        // Wait for sound to complete (assuming 1-second test file)
        await Task.Delay(1500);

        // Assert
        Assert.False(_manager.IsPlaying(instanceId), "Sound effect should have stopped after completion");

        // Verify file handle is released
        await Task.Delay(200);
        Assert.True(VerifyFileHandleClosed(_testSfxPath),
            "File handle should be released after sound effect completes");

        _output.WriteLine($"Sound effect {instanceId} completed and disposed");
    }

    #endregion

    #region 2. Concurrent Sound Effect Playback

    [Fact]
    public async Task PlayMultipleSoundEffects_Simultaneously_AllPlayConcurrently()
    {
        // Arrange
        var sfxDefinition = new SoundEffectDefinition
        {
            FilePath = _testSfxPath,
            Volume = 1.0f
        };

        // Act
        var instanceIds = new int[5];
        for (int i = 0; i < 5; i++)
        {
            instanceIds[i] = _manager.PlaySoundEffect(sfxDefinition);
            await Task.Delay(50); // Stagger slightly
        }

        // Assert
        foreach (var id in instanceIds)
        {
            Assert.True(_manager.IsPlaying(id), $"Instance {id} should be playing");
        }

        _output.WriteLine($"Successfully playing {instanceIds.Length} concurrent sound effects");
    }

    [Fact]
    public async Task PlayManySoundEffects_MemoryScalesLinearly_NoExponentialGrowth()
    {
        // Arrange
        var sfxDefinition = new SoundEffectDefinition
        {
            FilePath = _testSfxPath,
            Volume = 1.0f
        };

        var baselineMemory = GetMemoryOverhead();

        // Act - Play 10 concurrent sound effects
        var instanceIds = new int[10];
        for (int i = 0; i < 10; i++)
        {
            instanceIds[i] = _manager.PlaySoundEffect(sfxDefinition);
            await Task.Delay(20);
        }

        var concurrentMemory = GetMemoryOverhead();

        // Assert
        var memoryIncrease = concurrentMemory - baselineMemory;
        var expectedMaxMemory = MaxStreamMemoryOverheadBytes * 10 * 1.2; // 20% tolerance

        Assert.True(memoryIncrease < expectedMaxMemory,
            $"Memory for 10 streams ({memoryIncrease / 1024}KB) should scale linearly, not exponentially");

        _output.WriteLine($"10 concurrent SFX memory: {memoryIncrease / 1024}KB (Max expected: {expectedMaxMemory / 1024}KB)");
    }

    [Fact]
    public async Task PlaySameSoundEffectMultipleTimes_IndependentInstances_NoInterference()
    {
        // Arrange
        var sfxDefinition = new SoundEffectDefinition
        {
            FilePath = _testSfxPath,
            Volume = 1.0f
        };

        // Act
        var instance1 = _manager.PlaySoundEffect(sfxDefinition);
        await Task.Delay(100);
        var instance2 = _manager.PlaySoundEffect(sfxDefinition);
        await Task.Delay(100);
        var instance3 = _manager.PlaySoundEffect(sfxDefinition);

        // Assert
        Assert.True(_manager.IsPlaying(instance1), "Instance 1 should still be playing");
        Assert.True(_manager.IsPlaying(instance2), "Instance 2 should be playing");
        Assert.True(_manager.IsPlaying(instance3), "Instance 3 should be playing");

        // Stop one instance
        _manager.StopSoundEffect(instance2);
        await Task.Delay(50);

        Assert.True(_manager.IsPlaying(instance1), "Instance 1 should still be playing after stopping instance 2");
        Assert.False(_manager.IsPlaying(instance2), "Instance 2 should be stopped");
        Assert.True(_manager.IsPlaying(instance3), "Instance 3 should still be playing");

        _output.WriteLine("Multiple instances of same SFX play independently");
    }

    #endregion

    #region 3. Volume and Pan Controls

    [Fact]
    public async Task SetVolume_DuringPlayback_UpdatesImmediately()
    {
        // Arrange
        var sfxDefinition = new SoundEffectDefinition
        {
            FilePath = _testLongSfxPath,
            Volume = 1.0f
        };

        var instanceId = _manager.PlaySoundEffect(sfxDefinition);
        await Task.Delay(100);

        // Act
        _manager.SetVolume(instanceId, 0.5f);
        await Task.Delay(100);

        // Assert
        var currentVolume = _manager.GetVolume(instanceId);
        Assert.Equal(0.5f, currentVolume, 0.01f);

        _output.WriteLine($"Volume updated to {currentVolume} during playback");
    }

    [Fact]
    public async Task SetPan_DuringPlayback_UpdatesStereoPosition()
    {
        // Arrange
        var sfxDefinition = new SoundEffectDefinition
        {
            FilePath = _testLongSfxPath,
            Volume = 1.0f,
            Pan = 0.0f // Center
        };

        var instanceId = _manager.PlaySoundEffect(sfxDefinition);
        await Task.Delay(100);

        // Act
        _manager.SetPan(instanceId, -1.0f); // Full left
        await Task.Delay(100);
        _manager.SetPan(instanceId, 1.0f);  // Full right
        await Task.Delay(100);

        // Assert
        var currentPan = _manager.GetPan(instanceId);
        Assert.Equal(1.0f, currentPan, 0.01f);

        _output.WriteLine($"Pan updated to {currentPan} during playback");
    }

    #endregion

    #region 4. Resource Management

    [Fact]
    public async Task StopSoundEffect_DisposesStreamImmediately_ReleasesFileHandle()
    {
        // Arrange
        var sfxDefinition = new SoundEffectDefinition
        {
            FilePath = _testLongSfxPath,
            Volume = 1.0f
        };

        var instanceId = _manager.PlaySoundEffect(sfxDefinition);
        await Task.Delay(200);

        // Act
        _manager.StopSoundEffect(instanceId);
        await Task.Delay(200);

        // Assert
        Assert.False(_manager.IsPlaying(instanceId), "Sound effect should be stopped");
        Assert.True(VerifyFileHandleClosed(_testLongSfxPath),
            "File handle should be released after stop");

        _output.WriteLine($"Instance {instanceId} stopped and file handle released");
    }

    [Fact]
    public async Task StopAllSoundEffects_DisposesAllStreams_NoLeaks()
    {
        // Arrange
        var sfxDefinition = new SoundEffectDefinition
        {
            FilePath = _testSfxPath,
            Volume = 1.0f
        };

        var instanceIds = new int[5];
        for (int i = 0; i < 5; i++)
        {
            instanceIds[i] = _manager.PlaySoundEffect(sfxDefinition);
        }

        await Task.Delay(200);

        // Act
        _manager.StopAllSoundEffects();
        await Task.Delay(200);

        // Assert
        foreach (var id in instanceIds)
        {
            Assert.False(_manager.IsPlaying(id), $"Instance {id} should be stopped");
        }

        Assert.True(VerifyFileHandleClosed(_testSfxPath),
            "All file handles should be released");

        _output.WriteLine("All sound effects stopped and resources released");
    }

    [Fact]
    public async Task PlayStopLoop_1000Times_NoMemoryLeak()
    {
        // Arrange
        var sfxDefinition = new SoundEffectDefinition
        {
            FilePath = _testSfxPath,
            Volume = 1.0f
        };

        var baselineMemory = GetMemoryOverhead();

        // Act
        for (int i = 0; i < 1000; i++)
        {
            var instanceId = _manager.PlaySoundEffect(sfxDefinition);
            await Task.Delay(5);
            _manager.StopSoundEffect(instanceId);

            if (i % 100 == 0)
            {
                GC.Collect();
                _output.WriteLine($"Iteration {i}, Memory: {GetMemoryOverhead() / 1024}KB");
            }
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var finalMemory = GetMemoryOverhead();

        // Assert
        var memoryIncrease = finalMemory - baselineMemory;
        Assert.True(memoryIncrease < 1024 * 1024, // 1MB tolerance
            $"Memory should not leak after 1000 play/stop cycles. Increase: {memoryIncrease / 1024}KB");

        _output.WriteLine($"Memory after 1000 cycles: {memoryIncrease / 1024}KB increase from baseline");
    }

    #endregion

    #region 5. Error Handling

    [Fact]
    public void ErrorHandling_MissingFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var invalidSfx = new SoundEffectDefinition
        {
            FilePath = "NonExistent/sfx.ogg",
            Volume = 1.0f
        };

        // Act & Assert
        Assert.Throws<FileNotFoundException>(() => _manager.PlaySoundEffect(invalidSfx));

        _output.WriteLine("Correctly threw FileNotFoundException for missing SFX file");
    }

    [Fact]
    public void ErrorHandling_InvalidInstanceId_HandlesGracefully()
    {
        // Act & Assert
        Assert.False(_manager.IsPlaying(99999), "Invalid instance ID should return false");

        // These should not throw
        _manager.StopSoundEffect(99999);
        _manager.SetVolume(99999, 0.5f);
        _manager.SetPan(99999, 0.0f);

        _output.WriteLine("Invalid instance ID handled gracefully");
    }

    [Fact]
    public void ErrorHandling_InvalidVolume_ClampsToValidRange()
    {
        // Arrange
        var sfxDefinition = new SoundEffectDefinition
        {
            FilePath = _testSfxPath,
            Volume = 1.0f
        };

        var instanceId = _manager.PlaySoundEffect(sfxDefinition);

        // Act
        _manager.SetVolume(instanceId, 5.0f); // Invalid: > 1.0
        var volumeHigh = _manager.GetVolume(instanceId);

        _manager.SetVolume(instanceId, -2.0f); // Invalid: < 0.0
        var volumeLow = _manager.GetVolume(instanceId);

        // Assert
        Assert.InRange(volumeHigh, 0.0f, 1.0f);
        Assert.InRange(volumeLow, 0.0f, 1.0f);

        _output.WriteLine($"Invalid volumes clamped: High={volumeHigh}, Low={volumeLow}");
    }

    [Fact]
    public void ErrorHandling_InvalidPan_ClampsToValidRange()
    {
        // Arrange
        var sfxDefinition = new SoundEffectDefinition
        {
            FilePath = _testSfxPath,
            Volume = 1.0f
        };

        var instanceId = _manager.PlaySoundEffect(sfxDefinition);

        // Act
        _manager.SetPan(instanceId, 5.0f); // Invalid: > 1.0
        var panHigh = _manager.GetPan(instanceId);

        _manager.SetPan(instanceId, -5.0f); // Invalid: < -1.0
        var panLow = _manager.GetPan(instanceId);

        // Assert
        Assert.InRange(panHigh, -1.0f, 1.0f);
        Assert.InRange(panLow, -1.0f, 1.0f);

        _output.WriteLine($"Invalid pan values clamped: High={panHigh}, Low={panLow}");
    }

    #endregion

    #region 6. Performance Tests

    [Fact]
    public async Task Performance_StreamingVsCaching_MemoryComparison()
    {
        // Arrange
        var sfxDefinition = new SoundEffectDefinition
        {
            FilePath = _testSfxPath,
            Volume = 1.0f
        };

        var fileInfo = new FileInfo(_testSfxPath);
        var fileSizeKB = fileInfo.Length / 1024;

        var baselineMemory = GetMemoryOverhead();

        // Act - Stream 5 instances
        var instances = new int[5];
        for (int i = 0; i < 5; i++)
        {
            instances[i] = _manager.PlaySoundEffect(sfxDefinition);
            await Task.Delay(50);
        }

        var streamingMemory = GetMemoryOverhead();

        // Assert
        var memoryPerInstance = (streamingMemory - baselineMemory) / 5 / 1024;

        _output.WriteLine($"File size: {fileSizeKB}KB");
        _output.WriteLine($"Memory per stream: {memoryPerInstance}KB");
        _output.WriteLine($"Memory savings: {(double)fileSizeKB / memoryPerInstance:F2}x");

        // Streaming should use significantly less than full file size per instance
        Assert.True(memoryPerInstance < fileSizeKB / 2,
            "Streaming should use < 50% of file size per instance");
    }

    [Fact]
    public async Task Performance_MaxConcurrentStreams_IdentifyLimit()
    {
        // Arrange
        var sfxDefinition = new SoundEffectDefinition
        {
            FilePath = _testSfxPath,
            Volume = 0.1f // Low volume to avoid audio artifacts
        };

        var instances = new System.Collections.Generic.List<int>();
        var maxConcurrent = 0;

        // Act
        try
        {
            for (int i = 0; i < 100; i++)
            {
                var instanceId = _manager.PlaySoundEffect(sfxDefinition);
                instances.Add(instanceId);

                if (_manager.IsPlaying(instanceId))
                    maxConcurrent++;

                await Task.Delay(10);
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Max concurrent streams reached at: {maxConcurrent}");
            _output.WriteLine($"Exception: {ex.Message}");
        }

        // Assert
        _output.WriteLine($"Successfully created {instances.Count} sound effect instances");
        _output.WriteLine($"Max concurrent playing: {maxConcurrent}");

        Assert.True(maxConcurrent >= 10, "Should support at least 10 concurrent streams");
    }

    #endregion

    #region 7. Stress Tests

    [Fact]
    public async Task StressTest_RapidFireSoundEffects_NoExceptions()
    {
        // Arrange
        var sfxDefinition = new SoundEffectDefinition
        {
            FilePath = _testSfxPath,
            Volume = 1.0f
        };

        var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();

        // Act
        var tasks = new Task[100];
        for (int i = 0; i < 100; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                try
                {
                    _manager.PlaySoundEffect(sfxDefinition);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            });
        }

        await Task.WhenAll(tasks);

        // Assert
        Assert.Empty(exceptions);
        _output.WriteLine("Completed 100 rapid-fire PlaySoundEffect calls with no exceptions");
    }

    [Fact]
    public async Task StressTest_ConcurrentVolumeChanges_ThreadSafe()
    {
        // Arrange
        var sfxDefinition = new SoundEffectDefinition
        {
            FilePath = _testLongSfxPath,
            Volume = 1.0f
        };

        var instanceId = _manager.PlaySoundEffect(sfxDefinition);

        // Act
        var tasks = new Task[50];
        for (int i = 0; i < 50; i++)
        {
            int index = i;
            tasks[i] = Task.Run(() =>
            {
                _manager.SetVolume(instanceId, (index % 10) / 10.0f);
            });
        }

        await Task.WhenAll(tasks);

        // Assert
        Assert.True(_manager.IsPlaying(instanceId), "Instance should still be playing");
        _output.WriteLine($"Survived 50 concurrent volume changes");
    }

    #endregion
}
