# Audio System Test Strategy for PokeSharp

## Overview

This document outlines a comprehensive testing strategy for implementing and validating the audio system in PokeSharp, a MonoGame-based Pokemon game with ECS architecture. The strategy covers unit tests, integration tests, performance tests, and functional test scenarios.

---

## 1. Unit Test Categories

### 1.1 Sound Loading and Unloading

**Purpose**: Verify that audio resources are correctly loaded, cached, and disposed.

**Test Cases**:

```csharp
public class SoundLoadingTests
{
    [Fact]
    public void LoadSound_ValidPath_ReturnsNonNullSoundEffect()
    {
        // Arrange
        var audioService = CreateAudioService();

        // Act
        var sound = audioService.LoadSound("sfx/battle_hit.wav");

        // Assert
        Assert.NotNull(sound);
    }

    [Fact]
    public void LoadSound_InvalidPath_ThrowsFileNotFoundException()
    {
        // Arrange
        var audioService = CreateAudioService();

        // Act & Assert
        Assert.Throws<FileNotFoundException>(() =>
            audioService.LoadSound("invalid/path.wav"));
    }

    [Fact]
    public void LoadSound_AlreadyCached_ReturnsCachedInstance()
    {
        // Arrange
        var audioService = CreateAudioService();

        // Act
        var sound1 = audioService.LoadSound("sfx/jump.wav");
        var sound2 = audioService.LoadSound("sfx/jump.wav");

        // Assert
        Assert.Same(sound1, sound2);
    }

    [Fact]
    public void UnloadSound_LoadedSound_RemovesFromCache()
    {
        // Arrange
        var audioService = CreateAudioService();
        var sound = audioService.LoadSound("sfx/menu_select.wav");

        // Act
        audioService.UnloadSound("sfx/menu_select.wav");

        // Assert
        Assert.False(audioService.IsCached("sfx/menu_select.wav"));
    }

    [Fact]
    public void UnloadAllSounds_MultipleLoaded_ClearsCache()
    {
        // Arrange
        var audioService = CreateAudioService();
        audioService.LoadSound("sfx/1.wav");
        audioService.LoadSound("sfx/2.wav");
        audioService.LoadSound("sfx/3.wav");

        // Act
        audioService.UnloadAllSounds();

        // Assert
        Assert.Equal(0, audioService.GetCachedSoundCount());
    }

    [Fact]
    public void Dispose_LoadedSounds_DisposesAllResources()
    {
        // Arrange
        var audioService = CreateAudioService();
        audioService.LoadSound("sfx/test.wav");

        // Act
        audioService.Dispose();

        // Assert
        Assert.True(audioService.IsDisposed);
    }
}
```

### 1.2 Volume Control Calculations

**Purpose**: Ensure volume calculations are accurate across master, music, and SFX channels.

**Test Cases**:

```csharp
public class VolumeControlTests
{
    [Theory]
    [InlineData(1.0f, 0.5f, 0.5f)]  // Master 100%, SFX 50% = 50%
    [InlineData(0.5f, 1.0f, 0.5f)]  // Master 50%, SFX 100% = 50%
    [InlineData(0.5f, 0.5f, 0.25f)] // Master 50%, SFX 50% = 25%
    [InlineData(0.0f, 1.0f, 0.0f)]  // Muted master
    public void CalculateEffectiveVolume_VariousLevels_ReturnsCorrectProduct(
        float master, float sfx, float expected)
    {
        // Arrange
        var volumeController = new VolumeController();
        volumeController.SetMasterVolume(master);
        volumeController.SetSfxVolume(sfx);

        // Act
        var result = volumeController.CalculateEffectiveVolume(VolumeChannel.SFX);

        // Assert
        Assert.Equal(expected, result, precision: 3);
    }

    [Theory]
    [InlineData(-0.1f)]
    [InlineData(1.1f)]
    public void SetMasterVolume_OutOfRange_ThrowsArgumentOutOfRangeException(float volume)
    {
        // Arrange
        var volumeController = new VolumeController();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            volumeController.SetMasterVolume(volume));
    }

    [Fact]
    public void SetVolume_UpdatesActiveInstances_AppliesVolumeImmediately()
    {
        // Arrange
        var audioService = CreateAudioService();
        var instance = audioService.PlaySound("sfx/test.wav");

        // Act
        audioService.SetSfxVolume(0.3f);

        // Assert
        Assert.Equal(0.3f, instance.Volume, precision: 3);
    }

    [Fact]
    public void MuteAll_WhenCalled_SetsAllVolumesToZero()
    {
        // Arrange
        var volumeController = new VolumeController();
        volumeController.SetMasterVolume(0.8f);
        volumeController.SetMusicVolume(0.6f);
        volumeController.SetSfxVolume(0.7f);

        // Act
        volumeController.MuteAll();

        // Assert
        Assert.Equal(0f, volumeController.MasterVolume);
        Assert.Equal(0f, volumeController.MusicVolume);
        Assert.Equal(0f, volumeController.SfxVolume);
    }
}
```

### 1.3 Audio State Transitions

**Purpose**: Validate state machine transitions for music and sound effects.

**Test Cases**:

```csharp
public class AudioStateTransitionTests
{
    [Fact]
    public void PlayMusic_FromStopped_TransitionsToPlaying()
    {
        // Arrange
        var musicPlayer = CreateMusicPlayer();
        Assert.Equal(MusicState.Stopped, musicPlayer.State);

        // Act
        musicPlayer.Play("bgm/route_1.ogg");

        // Assert
        Assert.Equal(MusicState.Playing, musicPlayer.State);
    }

    [Fact]
    public void PauseMusic_WhilePlaying_TransitionsToPaused()
    {
        // Arrange
        var musicPlayer = CreateMusicPlayer();
        musicPlayer.Play("bgm/battle.ogg");

        // Act
        musicPlayer.Pause();

        // Assert
        Assert.Equal(MusicState.Paused, musicPlayer.State);
    }

    [Fact]
    public void ResumeMusic_WhilePaused_TransitionsToPlaying()
    {
        // Arrange
        var musicPlayer = CreateMusicPlayer();
        musicPlayer.Play("bgm/title.ogg");
        musicPlayer.Pause();

        // Act
        musicPlayer.Resume();

        // Assert
        Assert.Equal(MusicState.Playing, musicPlayer.State);
    }

    [Fact]
    public void CrossFade_DuringPlayback_TransitionsToFadingOut()
    {
        // Arrange
        var musicPlayer = CreateMusicPlayer();
        musicPlayer.Play("bgm/town.ogg");

        // Act
        musicPlayer.CrossFade("bgm/route.ogg", TimeSpan.FromSeconds(2));

        // Assert
        Assert.Equal(MusicState.FadingOut, musicPlayer.State);
    }

    [Fact]
    public void StopMusic_FromAnyState_TransitionsToStopped()
    {
        // Arrange
        var musicPlayer = CreateMusicPlayer();
        musicPlayer.Play("bgm/test.ogg");
        musicPlayer.Pause();

        // Act
        musicPlayer.Stop();

        // Assert
        Assert.Equal(MusicState.Stopped, musicPlayer.State);
    }

    [Theory]
    [InlineData(MusicState.Stopped)]
    [InlineData(MusicState.FadingOut)]
    public void Pause_FromInvalidState_ThrowsInvalidOperationException(MusicState state)
    {
        // Arrange
        var musicPlayer = CreateMusicPlayerInState(state);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => musicPlayer.Pause());
    }
}
```

### 1.4 Sound Prioritization Logic

**Purpose**: Verify that sound priority system works correctly when channels are limited.

**Test Cases**:

```csharp
public class SoundPrioritizationTests
{
    [Fact]
    public void PlaySound_WithLowerPriority_DoesNotReplaceHigherPriority()
    {
        // Arrange
        var audioService = CreateAudioService(maxChannels: 2);
        var highPriorityInstance = audioService.PlaySound("sfx/important.wav",
            priority: SoundPriority.High);
        audioService.PlaySound("sfx/normal.wav", priority: SoundPriority.Normal);

        // Act - Try to play low priority when channels full
        var result = audioService.PlaySound("sfx/low.wav",
            priority: SoundPriority.Low);

        // Assert
        Assert.Null(result);
        Assert.True(highPriorityInstance.IsPlaying);
    }

    [Fact]
    public void PlaySound_WithHigherPriority_ReplacesLowestPriority()
    {
        // Arrange
        var audioService = CreateAudioService(maxChannels: 2);
        var lowPriorityInstance = audioService.PlaySound("sfx/low.wav",
            priority: SoundPriority.Low);
        audioService.PlaySound("sfx/normal.wav", priority: SoundPriority.Normal);

        // Act
        var highPriorityInstance = audioService.PlaySound("sfx/high.wav",
            priority: SoundPriority.High);

        // Assert
        Assert.NotNull(highPriorityInstance);
        Assert.False(lowPriorityInstance.IsPlaying);
    }

    [Fact]
    public void PlaySound_CriticalPriority_AlwaysPlays()
    {
        // Arrange
        var audioService = CreateAudioService(maxChannels: 2);
        audioService.PlaySound("sfx/1.wav", priority: SoundPriority.High);
        audioService.PlaySound("sfx/2.wav", priority: SoundPriority.High);

        // Act
        var criticalInstance = audioService.PlaySound("sfx/critical.wav",
            priority: SoundPriority.Critical);

        // Assert
        Assert.NotNull(criticalInstance);
        Assert.True(criticalInstance.IsPlaying);
    }

    [Fact]
    public void GetActiveSoundsByPriority_ReturnsOrderedList()
    {
        // Arrange
        var audioService = CreateAudioService();
        audioService.PlaySound("sfx/low.wav", priority: SoundPriority.Low);
        audioService.PlaySound("sfx/high.wav", priority: SoundPriority.High);
        audioService.PlaySound("sfx/normal.wav", priority: SoundPriority.Normal);

        // Act
        var sounds = audioService.GetActiveSoundsByPriority();

        // Assert
        Assert.Equal(SoundPriority.High, sounds[0].Priority);
        Assert.Equal(SoundPriority.Normal, sounds[1].Priority);
        Assert.Equal(SoundPriority.Low, sounds[2].Priority);
    }
}
```

---

## 2. Integration Test Categories

### 2.1 Multiple Concurrent Sounds

**Purpose**: Validate that multiple sound effects can play simultaneously without interference.

**Test Cases**:

```csharp
public class ConcurrentSoundsIntegrationTests
{
    [Fact]
    public async Task PlayMultipleSounds_Simultaneously_AllPlayCorrectly()
    {
        // Arrange
        var audioService = CreateIntegratedAudioService();

        // Act
        var instances = new[]
        {
            audioService.PlaySound("sfx/hit1.wav"),
            audioService.PlaySound("sfx/hit2.wav"),
            audioService.PlaySound("sfx/jump.wav"),
            audioService.PlaySound("sfx/land.wav")
        };

        await Task.Delay(100); // Let sounds start

        // Assert
        Assert.All(instances, instance => Assert.True(instance.IsPlaying));
    }

    [Fact]
    public void PlaySound_BeyondMaxChannels_HandlesGracefully()
    {
        // Arrange
        var audioService = CreateIntegratedAudioService(maxChannels: 8);

        // Act
        var instances = new List<ISoundInstance>();
        for (int i = 0; i < 12; i++)
        {
            instances.Add(audioService.PlaySound($"sfx/sound{i}.wav"));
        }

        // Assert
        var playingCount = instances.Count(i => i?.IsPlaying == true);
        Assert.True(playingCount <= 8);
    }

    [Fact]
    public void StopAllSounds_WithMultiplePlaying_StopsAll()
    {
        // Arrange
        var audioService = CreateIntegratedAudioService();
        var instances = Enumerable.Range(0, 5)
            .Select(i => audioService.PlaySound($"sfx/test{i}.wav"))
            .ToList();

        // Act
        audioService.StopAllSounds();

        // Assert
        Assert.All(instances, instance => Assert.False(instance.IsPlaying));
    }
}
```

### 2.2 BGM with SFX Overlay

**Purpose**: Ensure background music and sound effects can play together harmoniously.

**Test Cases**:

```csharp
public class BgmSfxIntegrationTests
{
    [Fact]
    public void PlaySfx_WhileMusicPlaying_BothAudible()
    {
        // Arrange
        var audioService = CreateIntegratedAudioService();
        var musicPlayer = audioService.GetMusicPlayer();
        musicPlayer.Play("bgm/battle.ogg");

        // Act
        var sfxInstance = audioService.PlaySound("sfx/hit.wav");

        // Assert
        Assert.True(musicPlayer.IsPlaying);
        Assert.True(sfxInstance.IsPlaying);
    }

    [Fact]
    public void SetSfxVolume_WhileMusicPlaying_DoesNotAffectMusic()
    {
        // Arrange
        var audioService = CreateIntegratedAudioService();
        var musicPlayer = audioService.GetMusicPlayer();
        musicPlayer.Play("bgm/town.ogg");
        float initialMusicVolume = musicPlayer.Volume;

        // Act
        audioService.SetSfxVolume(0.2f);

        // Assert
        Assert.Equal(initialMusicVolume, musicPlayer.Volume);
    }

    [Fact]
    public void SetMusicVolume_WhileSfxPlaying_DoesNotAffectSfx()
    {
        // Arrange
        var audioService = CreateIntegratedAudioService();
        audioService.SetSfxVolume(0.8f);
        var sfxInstance = audioService.PlaySound("sfx/test.wav");

        // Act
        audioService.SetMusicVolume(0.3f);

        // Assert
        Assert.Equal(0.8f, audioService.GetSfxVolume(), precision: 3);
    }

    [Fact]
    public async Task DuckMusic_WhenDialoguePlays_ReducesMusicVolume()
    {
        // Arrange
        var audioService = CreateIntegratedAudioService();
        var musicPlayer = audioService.GetMusicPlayer();
        musicPlayer.Play("bgm/route.ogg");
        musicPlayer.SetVolume(1.0f);

        // Act
        audioService.DuckMusic(0.3f, TimeSpan.FromMilliseconds(500));
        await Task.Delay(600);

        // Assert
        Assert.Equal(0.3f, musicPlayer.Volume, precision: 2);
    }

    [Fact]
    public async Task UnduckMusic_AfterDialogue_RestoresVolume()
    {
        // Arrange
        var audioService = CreateIntegratedAudioService();
        var musicPlayer = audioService.GetMusicPlayer();
        musicPlayer.Play("bgm/route.ogg");
        musicPlayer.SetVolume(1.0f);
        audioService.DuckMusic(0.3f, TimeSpan.FromMilliseconds(200));
        await Task.Delay(300);

        // Act
        audioService.UnduckMusic(TimeSpan.FromMilliseconds(200));
        await Task.Delay(300);

        // Assert
        Assert.Equal(1.0f, musicPlayer.Volume, precision: 2);
    }
}
```

### 2.3 Audio Transitions and Fades

**Purpose**: Verify smooth transitions between music tracks and fade effects.

**Test Cases**:

```csharp
public class AudioTransitionIntegrationTests
{
    [Fact]
    public async Task CrossFade_BetweenTracks_SmoothTransition()
    {
        // Arrange
        var audioService = CreateIntegratedAudioService();
        var musicPlayer = audioService.GetMusicPlayer();
        musicPlayer.Play("bgm/track1.ogg");

        // Act
        musicPlayer.CrossFade("bgm/track2.ogg", TimeSpan.FromSeconds(2));
        await Task.Delay(2500);

        // Assert
        Assert.Equal("bgm/track2.ogg", musicPlayer.CurrentTrack);
        Assert.True(musicPlayer.IsPlaying);
    }

    [Fact]
    public async Task FadeOut_Music_GraduallReducesVolume()
    {
        // Arrange
        var audioService = CreateIntegratedAudioService();
        var musicPlayer = audioService.GetMusicPlayer();
        musicPlayer.Play("bgm/test.ogg");
        musicPlayer.SetVolume(1.0f);

        // Act
        musicPlayer.FadeOut(TimeSpan.FromSeconds(1));
        await Task.Delay(500);

        var midVolume = musicPlayer.Volume;
        await Task.Delay(600);

        // Assert
        Assert.True(midVolume > 0.1f && midVolume < 0.9f); // Check mid-fade
        Assert.Equal(0f, musicPlayer.Volume, precision: 2);
    }

    [Fact]
    public async Task FadeIn_Music_GraduallyIncreasesVolume()
    {
        // Arrange
        var audioService = CreateIntegratedAudioService();
        var musicPlayer = audioService.GetMusicPlayer();

        // Act
        musicPlayer.Play("bgm/test.ogg", fadeIn: TimeSpan.FromSeconds(1));
        await Task.Delay(500);

        var midVolume = musicPlayer.Volume;
        await Task.Delay(600);

        // Assert
        Assert.True(midVolume > 0.1f && midVolume < 0.9f); // Check mid-fade
        Assert.Equal(1f, musicPlayer.Volume, precision: 2);
    }

    [Fact]
    public async Task CancelFade_MidTransition_StopsAtCurrentVolume()
    {
        // Arrange
        var audioService = CreateIntegratedAudioService();
        var musicPlayer = audioService.GetMusicPlayer();
        musicPlayer.Play("bgm/test.ogg");
        musicPlayer.FadeOut(TimeSpan.FromSeconds(2));
        await Task.Delay(1000);

        // Act
        var volumeAtCancel = musicPlayer.Volume;
        musicPlayer.CancelFade();
        await Task.Delay(200);

        // Assert
        Assert.Equal(volumeAtCancel, musicPlayer.Volume, precision: 2);
    }
}
```

### 2.4 Resource Loading Pipelines

**Purpose**: Test the complete audio asset loading pipeline with content manager integration.

**Test Cases**:

```csharp
public class ResourceLoadingPipelineTests
{
    [Fact]
    public void LoadAudioAsset_ThroughContentManager_LoadsSuccessfully()
    {
        // Arrange
        var contentManager = CreateTestContentManager();
        var audioService = new AudioService(contentManager);

        // Act
        var sound = audioService.LoadSound("Content/sfx/menu_select");

        // Assert
        Assert.NotNull(sound);
        Assert.Equal("menu_select", sound.Name);
    }

    [Fact]
    public async Task PreloadAudioAssets_AsyncBatch_LoadsAllAssets()
    {
        // Arrange
        var audioService = CreateIntegratedAudioService();
        var assetPaths = new[]
        {
            "sfx/battle/hit.wav",
            "sfx/battle/miss.wav",
            "sfx/battle/critical.wav",
            "bgm/battle_wild.ogg"
        };

        // Act
        await audioService.PreloadAsync(assetPaths);

        // Assert
        foreach (var path in assetPaths)
        {
            Assert.True(audioService.IsCached(path));
        }
    }

    [Fact]
    public void LoadSound_WithCompressedFormat_DecodesCorrectly()
    {
        // Arrange
        var audioService = CreateIntegratedAudioService();

        // Act
        var sound = audioService.LoadSound("sfx/compressed.ogg");

        // Assert
        Assert.NotNull(sound);
        Assert.True(sound.Duration > TimeSpan.Zero);
    }

    [Fact]
    public void LoadSound_StreamingMusic_LoadsWithoutFullBuffer()
    {
        // Arrange
        var audioService = CreateIntegratedAudioService();

        // Act
        var music = audioService.LoadMusic("bgm/long_track.ogg", streaming: true);

        // Assert
        Assert.NotNull(music);
        Assert.True(music.IsStreaming);
    }
}
```

---

## 3. Performance Test Categories

### 3.1 Memory Usage Under Load

**Purpose**: Ensure audio system doesn't leak memory or consume excessive resources.

**Test Cases**:

```csharp
public class MemoryPerformanceTests
{
    [Fact]
    public void LoadUnloadCycle_1000Iterations_NoMemoryLeak()
    {
        // Arrange
        var audioService = CreateIntegratedAudioService();
        long initialMemory = GC.GetTotalMemory(true);

        // Act
        for (int i = 0; i < 1000; i++)
        {
            var sound = audioService.LoadSound("sfx/test.wav");
            audioService.UnloadSound("sfx/test.wav");
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long finalMemory = GC.GetTotalMemory(true);
        long memoryDelta = finalMemory - initialMemory;

        // Assert - Allow 1MB variance for test overhead
        Assert.True(memoryDelta < 1024 * 1024,
            $"Memory leak detected: {memoryDelta} bytes");
    }

    [Fact]
    public void CachedSounds_100Assets_ReasonableMemoryFootprint()
    {
        // Arrange
        var audioService = CreateIntegratedAudioService();
        long initialMemory = GC.GetTotalMemory(true);

        // Act
        for (int i = 0; i < 100; i++)
        {
            audioService.LoadSound($"sfx/test_{i}.wav");
        }

        long finalMemory = GC.GetTotalMemory(true);
        long memoryUsed = finalMemory - initialMemory;

        // Assert - 100 small sounds should be under 50MB
        Assert.True(memoryUsed < 50 * 1024 * 1024,
            $"Excessive memory usage: {memoryUsed / (1024 * 1024)}MB");
    }

    [Fact]
    public void StreamingMusic_PlayFor10Seconds_ConstantMemory()
    {
        // Arrange
        var audioService = CreateIntegratedAudioService();
        var musicPlayer = audioService.GetMusicPlayer();

        long[] memorySnapshots = new long[10];

        // Act
        musicPlayer.Play("bgm/long_track.ogg", streaming: true);

        for (int i = 0; i < 10; i++)
        {
            Thread.Sleep(1000);
            GC.Collect();
            memorySnapshots[i] = GC.GetTotalMemory(false);
        }

        musicPlayer.Stop();

        // Assert - Memory should not grow significantly
        long maxDelta = memorySnapshots.Max() - memorySnapshots.Min();
        Assert.True(maxDelta < 10 * 1024 * 1024,
            $"Memory growth during streaming: {maxDelta / (1024 * 1024)}MB");
    }
}
```

### 3.2 CPU Usage During Audio Mixing

**Purpose**: Verify audio mixing doesn't cause performance issues.

**Test Cases**:

```csharp
public class CpuPerformanceTests
{
    [Fact]
    public async Task Update_8ConcurrentSounds_LowCpuUsage()
    {
        // Arrange
        var audioService = CreateIntegratedAudioService();
        var stopwatch = Stopwatch.StartNew();

        // Act
        for (int i = 0; i < 8; i++)
        {
            audioService.PlaySound($"sfx/test_{i}.wav");
        }

        // Update audio for 1000 frames
        for (int frame = 0; frame < 1000; frame++)
        {
            audioService.Update(TimeSpan.FromMilliseconds(16.67)); // 60 FPS
        }

        stopwatch.Stop();

        // Assert - Should take less than 200ms total (0.2ms per frame)
        Assert.True(stopwatch.ElapsedMilliseconds < 200,
            $"Audio update too slow: {stopwatch.ElapsedMilliseconds}ms for 1000 frames");
    }

    [Fact]
    public void CrossFade_Update_MinimalOverhead()
    {
        // Arrange
        var audioService = CreateIntegratedAudioService();
        var musicPlayer = audioService.GetMusicPlayer();
        musicPlayer.Play("bgm/track1.ogg");

        var stopwatch = Stopwatch.StartNew();

        // Act
        musicPlayer.CrossFade("bgm/track2.ogg", TimeSpan.FromSeconds(2));

        // Update for fade duration at 60 FPS
        for (int frame = 0; frame < 120; frame++)
        {
            audioService.Update(TimeSpan.FromMilliseconds(16.67));
        }

        stopwatch.Stop();

        // Assert - Should take less than 50ms total
        Assert.True(stopwatch.ElapsedMilliseconds < 50,
            $"CrossFade update too slow: {stopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public void VolumeCalculation_10000Calls_OptimizedPerformance()
    {
        // Arrange
        var volumeController = new VolumeController();
        volumeController.SetMasterVolume(0.8f);
        volumeController.SetSfxVolume(0.6f);

        var stopwatch = Stopwatch.StartNew();

        // Act
        for (int i = 0; i < 10000; i++)
        {
            volumeController.CalculateEffectiveVolume(VolumeChannel.SFX);
        }

        stopwatch.Stop();

        // Assert - Should take less than 10ms
        Assert.True(stopwatch.ElapsedMilliseconds < 10,
            $"Volume calculation too slow: {stopwatch.ElapsedMilliseconds}ms");
    }
}
```

### 3.3 Loading Time Benchmarks

**Purpose**: Ensure audio assets load quickly enough for gameplay.

**Test Cases**:

```csharp
public class LoadingPerformanceTests
{
    [Fact]
    public void LoadSound_SmallFile_LoadsQuickly()
    {
        // Arrange
        var audioService = CreateIntegratedAudioService();
        var stopwatch = Stopwatch.StartNew();

        // Act
        var sound = audioService.LoadSound("sfx/menu_select.wav");
        stopwatch.Stop();

        // Assert - Small sounds should load in under 50ms
        Assert.True(stopwatch.ElapsedMilliseconds < 50,
            $"Small sound loading too slow: {stopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task PreloadBattleAudio_TypicalSet_LoadsInUnder500ms()
    {
        // Arrange
        var audioService = CreateIntegratedAudioService();
        var battleSounds = new[]
        {
            "sfx/battle/hit.wav",
            "sfx/battle/miss.wav",
            "sfx/battle/critical.wav",
            "sfx/battle/faint.wav",
            "bgm/battle_wild.ogg"
        };

        var stopwatch = Stopwatch.StartNew();

        // Act
        await audioService.PreloadAsync(battleSounds);
        stopwatch.Stop();

        // Assert
        Assert.True(stopwatch.ElapsedMilliseconds < 500,
            $"Battle audio loading too slow: {stopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public void LoadMusic_StreamingMode_LoadsInstantly()
    {
        // Arrange
        var audioService = CreateIntegratedAudioService();
        var stopwatch = Stopwatch.StartNew();

        // Act
        var music = audioService.LoadMusic("bgm/long_track.ogg", streaming: true);
        stopwatch.Stop();

        // Assert - Streaming should only load headers (< 100ms)
        Assert.True(stopwatch.ElapsedMilliseconds < 100,
            $"Streaming music initialization too slow: {stopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task ParallelLoad_10Sounds_FasterThanSequential()
    {
        // Arrange
        var audioService = CreateIntegratedAudioService();
        var soundPaths = Enumerable.Range(0, 10)
            .Select(i => $"sfx/test_{i}.wav")
            .ToArray();

        // Sequential load
        var sequentialWatch = Stopwatch.StartNew();
        foreach (var path in soundPaths)
        {
            audioService.LoadSound(path);
        }
        sequentialWatch.Stop();

        audioService.UnloadAllSounds();

        // Parallel load
        var parallelWatch = Stopwatch.StartNew();
        await audioService.PreloadAsync(soundPaths);
        parallelWatch.Stop();

        // Assert - Parallel should be at least 2x faster
        Assert.True(parallelWatch.ElapsedMilliseconds < sequentialWatch.ElapsedMilliseconds / 2,
            $"Parallel loading not optimized: {parallelWatch.ElapsedMilliseconds}ms vs " +
            $"{sequentialWatch.ElapsedMilliseconds}ms");
    }
}
```

### 3.4 Concurrent Sound Limits

**Purpose**: Determine system limits for simultaneous audio playback.

**Test Cases**:

```csharp
public class ConcurrencyLimitsTests
{
    [Theory]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(32)]
    public void PlayConcurrentSounds_VariousLimits_HandlesGracefully(int maxChannels)
    {
        // Arrange
        var audioService = CreateIntegratedAudioService(maxChannels);

        // Act
        var instances = new List<ISoundInstance>();
        for (int i = 0; i < maxChannels + 5; i++)
        {
            instances.Add(audioService.PlaySound($"sfx/test_{i}.wav"));
        }

        var playingCount = instances.Count(i => i?.IsPlaying == true);

        // Assert
        Assert.True(playingCount <= maxChannels);
        Assert.True(playingCount >= maxChannels - 2); // Allow small variance
    }

    [Fact]
    public void StressTest_100SoundsRapidFire_SystemStable()
    {
        // Arrange
        var audioService = CreateIntegratedAudioService();
        var exceptions = new List<Exception>();

        // Act
        for (int i = 0; i < 100; i++)
        {
            try
            {
                audioService.PlaySound($"sfx/test_{i % 10}.wav");
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        }

        // Assert
        Assert.Empty(exceptions);
    }

    [Fact]
    public void ChannelStarvation_HighPrioritySounds_AlwaysPlayable()
    {
        // Arrange
        var audioService = CreateIntegratedAudioService(maxChannels: 8);

        // Fill all channels with low priority
        for (int i = 0; i < 8; i++)
        {
            audioService.PlaySound($"sfx/low_{i}.wav", priority: SoundPriority.Low);
        }

        // Act - Try to play high priority sounds
        var highPrioritySounds = new List<ISoundInstance>();
        for (int i = 0; i < 5; i++)
        {
            highPrioritySounds.Add(
                audioService.PlaySound($"sfx/high_{i}.wav", priority: SoundPriority.High)
            );
        }

        // Assert - All high priority should play
        Assert.All(highPrioritySounds, instance => Assert.NotNull(instance));
        Assert.All(highPrioritySounds, instance => Assert.True(instance.IsPlaying));
    }
}
```

---

## 4. Functional Test Scenarios

### 4.1 Battle Audio Sequences

**Purpose**: Test complete audio sequences during Pokemon battles.

**Test Cases**:

```csharp
public class BattleAudioFunctionalTests
{
    [Fact]
    public async Task WildBattleSequence_Complete_AllSoundsPlayInOrder()
    {
        // Arrange
        var audioService = CreateIntegratedAudioService();
        var musicPlayer = audioService.GetMusicPlayer();
        var eventLog = new List<string>();

        // Act - Simulate battle sequence
        // 1. Battle transition
        musicPlayer.FadeOut(TimeSpan.FromMilliseconds(500));
        await Task.Delay(500);
        eventLog.Add("route_music_faded");

        // 2. Battle intro
        audioService.PlaySound("sfx/battle_intro.wav");
        await Task.Delay(200);
        eventLog.Add("intro_sound_played");

        // 3. Battle music starts
        musicPlayer.Play("bgm/battle_wild.ogg", fadeIn: TimeSpan.FromMilliseconds(500));
        await Task.Delay(500);
        eventLog.Add("battle_music_started");

        // 4. Battle sounds
        audioService.PlaySound("sfx/battle/pokemon_appear.wav");
        await Task.Delay(300);
        audioService.PlaySound("sfx/battle/hit.wav");
        await Task.Delay(100);
        eventLog.Add("battle_sounds_played");

        // Assert
        Assert.Equal("route_music_faded", eventLog[0]);
        Assert.Equal("intro_sound_played", eventLog[1]);
        Assert.Equal("battle_music_started", eventLog[2]);
        Assert.Equal("battle_sounds_played", eventLog[3]);
        Assert.True(musicPlayer.IsPlaying);
        Assert.Equal("bgm/battle_wild.ogg", musicPlayer.CurrentTrack);
    }

    [Fact]
    public async Task BattleVictory_MusicTransition_SmoothSequence()
    {
        // Arrange
        var audioService = CreateIntegratedAudioService();
        var musicPlayer = audioService.GetMusicPlayer();
        musicPlayer.Play("bgm/battle_wild.ogg");

        // Act
        // 1. Stop battle music
        musicPlayer.Stop();

        // 2. Play victory jingle
        audioService.PlaySound("sfx/battle/victory.wav");
        await Task.Delay(100);

        // 3. Start victory music
        musicPlayer.Play("bgm/victory.ogg");
        await Task.Delay(200);

        // 4. Fade back to route music
        musicPlayer.CrossFade("bgm/route_1.ogg", TimeSpan.FromSeconds(2));
        await Task.Delay(2100);

        // Assert
        Assert.Equal("bgm/route_1.ogg", musicPlayer.CurrentTrack);
        Assert.True(musicPlayer.IsPlaying);
    }

    [Fact]
    public void BattleEffectiveness_CriticalHit_PlaysCriticalSound()
    {
        // Arrange
        var audioService = CreateIntegratedAudioService();

        // Act
        var criticalHit = audioService.PlaySound("sfx/battle/critical.wav",
            priority: SoundPriority.High);

        // Assert
        Assert.NotNull(criticalHit);
        Assert.True(criticalHit.IsPlaying);
    }

    [Fact]
    public async Task PokemonFaint_Sequence_SoundAndMusicChange()
    {
        // Arrange
        var audioService = CreateIntegratedAudioService();
        var musicPlayer = audioService.GetMusicPlayer();
        musicPlayer.Play("bgm/battle_trainer.ogg");

        // Act
        audioService.PlaySound("sfx/battle/faint.wav");
        await Task.Delay(100);

        // Music ducks during faint sound
        audioService.DuckMusic(0.5f, TimeSpan.FromMilliseconds(200));
        await Task.Delay(1000);

        audioService.UnduckMusic(TimeSpan.FromMilliseconds(200));

        // Assert
        Assert.True(musicPlayer.IsPlaying);
        Assert.Equal(1.0f, musicPlayer.Volume, precision: 2);
    }
}
```

### 4.2 Pokemon Cry Playback

**Purpose**: Verify Pokemon cry audio works correctly in various contexts.

**Test Cases**:

```csharp
public class PokemonCryFunctionalTests
{
    [Theory]
    [InlineData("pikachu", "cries/025.wav")]
    [InlineData("charizard", "cries/006.wav")]
    [InlineData("mewtwo", "cries/150.wav")]
    public void PlayPokemonCry_ValidSpecies_PlaysCorrectCry(string species, string expectedPath)
    {
        // Arrange
        var audioService = CreateIntegratedAudioService();

        // Act
        var cry = audioService.PlayPokemonCry(species);

        // Assert
        Assert.NotNull(cry);
        Assert.Contains(expectedPath, cry.AudioPath);
    }

    [Fact]
    public void PlayPokemonCry_DuringBattle_HighPriority()
    {
        // Arrange
        var audioService = CreateIntegratedAudioService(maxChannels: 4);

        // Fill channels with normal priority sounds
        for (int i = 0; i < 4; i++)
        {
            audioService.PlaySound($"sfx/ambient_{i}.wav", priority: SoundPriority.Normal);
        }

        // Act
        var cry = audioService.PlayPokemonCry("pikachu");

        // Assert
        Assert.NotNull(cry);
        Assert.True(cry.IsPlaying);
    }

    [Fact]
    public void PlayPokemonCry_InterruptPrevious_NewCryPlays()
    {
        // Arrange
        var audioService = CreateIntegratedAudioService();

        // Act
        var cry1 = audioService.PlayPokemonCry("bulbasaur");
        var cry2 = audioService.PlayPokemonCry("charmander");

        // Assert
        Assert.False(cry1.IsPlaying); // First cry stopped
        Assert.True(cry2.IsPlaying);  // Second cry playing
    }

    [Fact]
    public async Task PlayPokemonCry_WithPitch_AlteredPlayback()
    {
        // Arrange
        var audioService = CreateIntegratedAudioService();

        // Act
        var normalCry = audioService.PlayPokemonCry("pikachu", pitch: 1.0f);
        await Task.Delay(500);
        var highCry = audioService.PlayPokemonCry("pikachu", pitch: 1.5f);

        // Assert
        Assert.NotEqual(normalCry.Pitch, highCry.Pitch);
        Assert.Equal(1.5f, highCry.Pitch, precision: 2);
    }
}
```

### 4.3 Music Looping Correctness

**Purpose**: Ensure music tracks loop seamlessly without gaps or clicks.

**Test Cases**:

```csharp
public class MusicLoopingFunctionalTests
{
    [Fact]
    public async Task PlayMusic_Looping_SeamlessRepeat()
    {
        // Arrange
        var audioService = CreateIntegratedAudioService();
        var musicPlayer = audioService.GetMusicPlayer();
        int loopCount = 0;

        musicPlayer.OnLoop += () => loopCount++;

        // Act
        musicPlayer.Play("bgm/short_loop.ogg", loop: true);
        await Task.Delay(5000); // Let it loop several times

        // Assert
        Assert.True(loopCount >= 2, $"Music should have looped, count: {loopCount}");
        Assert.True(musicPlayer.IsPlaying);
    }

    [Fact]
    public async Task PlayMusic_WithLoopPoints_RespectsCustomLoop()
    {
        // Arrange
        var audioService = CreateIntegratedAudioService();
        var musicPlayer = audioService.GetMusicPlayer();

        var loopPoints = new LoopPoints
        {
            LoopStart = TimeSpan.FromSeconds(5),
            LoopEnd = TimeSpan.FromSeconds(35)
        };

        // Act
        musicPlayer.Play("bgm/intro_loop.ogg", loopPoints: loopPoints);
        await Task.Delay(40000); // Should play intro once, then loop

        // Assert
        var position = musicPlayer.Position;
        Assert.True(position >= loopPoints.LoopStart && position <= loopPoints.LoopEnd);
    }

    [Fact]
    public void PlayMusic_NonLooping_StopsAtEnd()
    {
        // Arrange
        var audioService = CreateIntegratedAudioService();
        var musicPlayer = audioService.GetMusicPlayer();
        bool trackEnded = false;

        musicPlayer.OnTrackEnd += () => trackEnded = true;

        // Act
        musicPlayer.Play("bgm/short_jingle.ogg", loop: false);
        Thread.Sleep((int)musicPlayer.Duration.TotalMilliseconds + 100);

        // Assert
        Assert.True(trackEnded);
        Assert.False(musicPlayer.IsPlaying);
    }

    [Fact]
    public async Task ChangeLoop_MidPlayback_UpdatesLoopBehavior()
    {
        // Arrange
        var audioService = CreateIntegratedAudioService();
        var musicPlayer = audioService.GetMusicPlayer();

        // Act
        musicPlayer.Play("bgm/test.ogg", loop: true);
        await Task.Delay(1000);

        musicPlayer.SetLooping(false);
        await Task.Delay((int)musicPlayer.Duration.TotalMilliseconds);

        // Assert
        Assert.False(musicPlayer.IsPlaying);
    }
}
```

### 4.4 Pause/Resume Behavior

**Purpose**: Test audio system behavior during game pause.

**Test Cases**:

```csharp
public class PauseResumeFunctionalTests
{
    [Fact]
    public void PauseGame_AllAudio_PausesCorrectly()
    {
        // Arrange
        var audioService = CreateIntegratedAudioService();
        var musicPlayer = audioService.GetMusicPlayer();

        musicPlayer.Play("bgm/route.ogg");
        var sfx1 = audioService.PlaySound("sfx/walking.wav", loop: true);
        var sfx2 = audioService.PlaySound("sfx/ambient.wav", loop: true);

        // Act
        audioService.PauseAll();

        // Assert
        Assert.Equal(MusicState.Paused, musicPlayer.State);
        Assert.True(sfx1.IsPaused);
        Assert.True(sfx2.IsPaused);
    }

    [Fact]
    public void ResumeGame_AllAudio_ResumesCorrectly()
    {
        // Arrange
        var audioService = CreateIntegratedAudioService();
        var musicPlayer = audioService.GetMusicPlayer();

        musicPlayer.Play("bgm/route.ogg");
        var sfx = audioService.PlaySound("sfx/walking.wav", loop: true);

        audioService.PauseAll();

        // Act
        audioService.ResumeAll();

        // Assert
        Assert.Equal(MusicState.Playing, musicPlayer.State);
        Assert.True(sfx.IsPlaying);
        Assert.False(sfx.IsPaused);
    }

    [Fact]
    public void PauseMusic_SfxContinue_SelectivePause()
    {
        // Arrange
        var audioService = CreateIntegratedAudioService();
        var musicPlayer = audioService.GetMusicPlayer();

        musicPlayer.Play("bgm/route.ogg");
        var sfx = audioService.PlaySound("sfx/menu_move.wav");

        // Act
        musicPlayer.Pause();

        // Assert
        Assert.Equal(MusicState.Paused, musicPlayer.State);
        Assert.True(sfx.IsPlaying); // SFX not affected
    }

    [Fact]
    public async Task PauseResume_PreservesPosition_ContinuesFromSamePoint()
    {
        // Arrange
        var audioService = CreateIntegratedAudioService();
        var musicPlayer = audioService.GetMusicPlayer();

        musicPlayer.Play("bgm/route.ogg");
        await Task.Delay(2000);

        var positionBeforePause = musicPlayer.Position;

        // Act
        musicPlayer.Pause();
        await Task.Delay(1000); // Paused for 1 second
        musicPlayer.Resume();

        var positionAfterResume = musicPlayer.Position;

        // Assert
        Assert.Equal(positionBeforePause.TotalMilliseconds,
                     positionAfterResume.TotalMilliseconds,
                     precision: 0);
    }

    [Fact]
    public void MenuPause_DucksMusicPlaysMenuSfx_CorrectBehavior()
    {
        // Arrange
        var audioService = CreateIntegratedAudioService();
        var musicPlayer = audioService.GetMusicPlayer();
        musicPlayer.Play("bgm/route.ogg");

        // Act
        audioService.DuckMusic(0.4f, TimeSpan.FromMilliseconds(200));
        var menuSound = audioService.PlaySound("sfx/menu_open.wav");

        // Assert
        Assert.True(musicPlayer.IsPlaying);
        Assert.Equal(0.4f, musicPlayer.Volume, precision: 2);
        Assert.True(menuSound.IsPlaying);
    }
}
```

---

## 5. Test Utilities and Mocking Infrastructure

### 5.1 Mock Audio Provider

```csharp
/// <summary>
/// Mock implementation of MonoGame's SoundEffect for testing
/// </summary>
public class MockSoundEffect : IDisposable
{
    public string Name { get; set; }
    public TimeSpan Duration { get; set; }
    public bool IsDisposed { get; private set; }

    public MockSoundEffect(string name, TimeSpan duration)
    {
        Name = name;
        Duration = duration;
    }

    public MockSoundEffectInstance CreateInstance()
    {
        return new MockSoundEffectInstance(this);
    }

    public void Dispose()
    {
        IsDisposed = true;
    }
}

/// <summary>
/// Mock sound instance for testing playback
/// </summary>
public class MockSoundEffectInstance : ISoundInstance
{
    public MockSoundEffect Sound { get; }
    public bool IsPlaying { get; private set; }
    public bool IsPaused { get; private set; }
    public float Volume { get; set; }
    public float Pitch { get; set; }
    public float Pan { get; set; }
    public bool IsLooped { get; set; }
    public SoundPriority Priority { get; set; }

    public MockSoundEffectInstance(MockSoundEffect sound)
    {
        Sound = sound;
        Volume = 1.0f;
        Pitch = 0.0f;
        Pan = 0.0f;
    }

    public void Play()
    {
        IsPlaying = true;
        IsPaused = false;
    }

    public void Pause()
    {
        IsPaused = true;
    }

    public void Resume()
    {
        IsPlaying = true;
        IsPaused = false;
    }

    public void Stop()
    {
        IsPlaying = false;
        IsPaused = false;
    }

    public void Dispose()
    {
        Stop();
    }
}

/// <summary>
/// Mock content manager for audio loading
/// </summary>
public class MockAudioContentManager : IContentManager
{
    private readonly Dictionary<string, MockSoundEffect> _sounds = new();
    private readonly Dictionary<string, MockMusic> _music = new();

    public void RegisterSound(string path, TimeSpan duration)
    {
        _sounds[path] = new MockSoundEffect(path, duration);
    }

    public void RegisterMusic(string path, TimeSpan duration, bool streaming = false)
    {
        _music[path] = new MockMusic(path, duration, streaming);
    }

    public T Load<T>(string assetName) where T : class
    {
        if (typeof(T) == typeof(MockSoundEffect))
        {
            if (_sounds.TryGetValue(assetName, out var sound))
                return sound as T;
        }
        else if (typeof(T) == typeof(MockMusic))
        {
            if (_music.TryGetValue(assetName, out var music))
                return music as T;
        }

        throw new FileNotFoundException($"Asset not found: {assetName}");
    }

    public void Unload() { }
}
```

### 5.2 Audio Event Recorder

```csharp
/// <summary>
/// Records audio events for verification in tests
/// </summary>
public class AudioEventRecorder
{
    public List<AudioEvent> Events { get; } = new();

    public void RecordSoundPlayed(string soundPath, SoundPriority priority)
    {
        Events.Add(new AudioEvent
        {
            Type = AudioEventType.SoundPlayed,
            Timestamp = DateTime.UtcNow,
            SoundPath = soundPath,
            Priority = priority
        });
    }

    public void RecordSoundStopped(string soundPath)
    {
        Events.Add(new AudioEvent
        {
            Type = AudioEventType.SoundStopped,
            Timestamp = DateTime.UtcNow,
            SoundPath = soundPath
        });
    }

    public void RecordMusicStarted(string musicPath)
    {
        Events.Add(new AudioEvent
        {
            Type = AudioEventType.MusicStarted,
            Timestamp = DateTime.UtcNow,
            SoundPath = musicPath
        });
    }

    public void RecordMusicStopped(string musicPath)
    {
        Events.Add(new AudioEvent
        {
            Type = AudioEventType.MusicStopped,
            Timestamp = DateTime.UtcNow,
            SoundPath = musicPath
        });
    }

    public void RecordVolumChanged(VolumeChannel channel, float newVolume)
    {
        Events.Add(new AudioEvent
        {
            Type = AudioEventType.VolumeChanged,
            Timestamp = DateTime.UtcNow,
            Channel = channel,
            Volume = newVolume
        });
    }

    public void Clear() => Events.Clear();

    public IEnumerable<AudioEvent> GetEvents(AudioEventType type)
    {
        return Events.Where(e => e.Type == type);
    }

    public AudioEvent GetLastEvent(AudioEventType type)
    {
        return Events.LastOrDefault(e => e.Type == type);
    }
}

public class AudioEvent
{
    public AudioEventType Type { get; set; }
    public DateTime Timestamp { get; set; }
    public string SoundPath { get; set; }
    public SoundPriority Priority { get; set; }
    public VolumeChannel Channel { get; set; }
    public float Volume { get; set; }
}

public enum AudioEventType
{
    SoundPlayed,
    SoundStopped,
    MusicStarted,
    MusicStopped,
    MusicPaused,
    MusicResumed,
    VolumeChanged,
    FadeStarted,
    FadeCompleted
}
```

### 5.3 Performance Profiler

```csharp
/// <summary>
/// Profiles audio system performance metrics
/// </summary>
public class AudioPerformanceProfiler
{
    private readonly Stopwatch _stopwatch = new();
    private readonly List<PerformanceSnapshot> _snapshots = new();

    public void StartProfiling()
    {
        _stopwatch.Restart();
        _snapshots.Clear();
    }

    public void TakeSnapshot(string label, IAudioService audioService)
    {
        var snapshot = new PerformanceSnapshot
        {
            Label = label,
            ElapsedMs = _stopwatch.ElapsedMilliseconds,
            MemoryUsedBytes = GC.GetTotalMemory(false),
            ActiveSoundCount = audioService.GetActiveSoundCount(),
            CachedSoundCount = audioService.GetCachedSoundCount()
        };

        _snapshots.Add(snapshot);
    }

    public PerformanceReport GenerateReport()
    {
        return new PerformanceReport
        {
            Snapshots = _snapshots.ToList(),
            TotalDurationMs = _stopwatch.ElapsedMilliseconds,
            PeakMemoryBytes = _snapshots.Max(s => s.MemoryUsedBytes),
            AverageActiveSounds = _snapshots.Average(s => s.ActiveSoundCount)
        };
    }

    public void StopProfiling()
    {
        _stopwatch.Stop();
    }
}

public class PerformanceSnapshot
{
    public string Label { get; set; }
    public long ElapsedMs { get; set; }
    public long MemoryUsedBytes { get; set; }
    public int ActiveSoundCount { get; set; }
    public int CachedSoundCount { get; set; }
}

public class PerformanceReport
{
    public List<PerformanceSnapshot> Snapshots { get; set; }
    public long TotalDurationMs { get; set; }
    public long PeakMemoryBytes { get; set; }
    public double AverageActiveSounds { get; set; }

    public void PrintReport()
    {
        Console.WriteLine("=== Audio Performance Report ===");
        Console.WriteLine($"Total Duration: {TotalDurationMs}ms");
        Console.WriteLine($"Peak Memory: {PeakMemoryBytes / (1024 * 1024)}MB");
        Console.WriteLine($"Average Active Sounds: {AverageActiveSounds:F2}");
        Console.WriteLine("\nSnapshots:");

        foreach (var snapshot in Snapshots)
        {
            Console.WriteLine($"  [{snapshot.ElapsedMs}ms] {snapshot.Label}");
            Console.WriteLine($"    Memory: {snapshot.MemoryUsedBytes / (1024 * 1024)}MB");
            Console.WriteLine($"    Active: {snapshot.ActiveSoundCount}, " +
                            $"Cached: {snapshot.CachedSoundCount}");
        }
    }
}
```

### 5.4 Test Helper Factory

```csharp
/// <summary>
/// Factory for creating configured test instances
/// </summary>
public static class AudioTestFactory
{
    public static IAudioService CreateAudioService(int maxChannels = 16)
    {
        var contentManager = CreateMockContentManager();
        var audioService = new AudioService(contentManager, maxChannels);
        return audioService;
    }

    public static IAudioService CreateIntegratedAudioService(int maxChannels = 16)
    {
        var contentManager = CreateMockContentManager();
        RegisterDefaultAssets(contentManager);

        var audioService = new AudioService(contentManager, maxChannels);
        return audioService;
    }

    public static MockAudioContentManager CreateMockContentManager()
    {
        return new MockAudioContentManager();
    }

    public static void RegisterDefaultAssets(MockAudioContentManager contentManager)
    {
        // Register common test sounds
        contentManager.RegisterSound("sfx/test.wav", TimeSpan.FromMilliseconds(500));
        contentManager.RegisterSound("sfx/menu_select.wav", TimeSpan.FromMilliseconds(100));
        contentManager.RegisterSound("sfx/jump.wav", TimeSpan.FromMilliseconds(300));

        // Register battle sounds
        contentManager.RegisterSound("sfx/battle/hit.wav", TimeSpan.FromMilliseconds(200));
        contentManager.RegisterSound("sfx/battle/miss.wav", TimeSpan.FromMilliseconds(150));
        contentManager.RegisterSound("sfx/battle/critical.wav", TimeSpan.FromMilliseconds(400));

        // Register music tracks
        contentManager.RegisterMusic("bgm/route_1.ogg", TimeSpan.FromSeconds(120));
        contentManager.RegisterMusic("bgm/battle_wild.ogg", TimeSpan.FromSeconds(90));
        contentManager.RegisterMusic("bgm/town.ogg", TimeSpan.FromSeconds(150));
    }

    public static AudioEventRecorder CreateEventRecorder(IAudioService audioService)
    {
        var recorder = new AudioEventRecorder();

        // Wire up event recording
        audioService.OnSoundPlayed += (path, priority) =>
            recorder.RecordSoundPlayed(path, priority);
        audioService.OnSoundStopped += (path) =>
            recorder.RecordSoundStopped(path);
        audioService.OnMusicStarted += (path) =>
            recorder.RecordMusicStarted(path);
        audioService.OnMusicStopped += (path) =>
            recorder.RecordMusicStopped(path);
        audioService.OnVolumeChanged += (channel, volume) =>
            recorder.RecordVolumChanged(channel, volume);

        return recorder;
    }
}
```

---

## 6. Testing Infrastructure Recommendations

### 6.1 Test Project Structure

```
PokeSharp.Tests/
├── Unit/
│   ├── Audio/
│   │   ├── SoundLoadingTests.cs
│   │   ├── VolumeControlTests.cs
│   │   ├── AudioStateTransitionTests.cs
│   │   └── SoundPrioritizationTests.cs
├── Integration/
│   ├── Audio/
│   │   ├── ConcurrentSoundsTests.cs
│   │   ├── BgmSfxIntegrationTests.cs
│   │   ├── AudioTransitionTests.cs
│   │   └── ResourceLoadingTests.cs
├── Performance/
│   ├── Audio/
│   │   ├── MemoryPerformanceTests.cs
│   │   ├── CpuPerformanceTests.cs
│   │   ├── LoadingPerformanceTests.cs
│   │   └── ConcurrencyLimitsTests.cs
├── Functional/
│   ├── Audio/
│   │   ├── BattleAudioTests.cs
│   │   ├── PokemonCryTests.cs
│   │   ├── MusicLoopingTests.cs
│   │   └── PauseResumeTests.cs
├── Utilities/
│   ├── Mocks/
│   │   ├── MockSoundEffect.cs
│   │   ├── MockMusicPlayer.cs
│   │   └── MockContentManager.cs
│   ├── AudioEventRecorder.cs
│   ├── AudioPerformanceProfiler.cs
│   └── AudioTestFactory.cs
└── PokeSharp.Tests.csproj
```

### 6.2 Test Project Configuration (csproj)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="xunit" Version="2.6.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.4" />
    <PackageReference Include="Moq" Version="4.20.69" />
    <PackageReference Include="FluentAssertions" Version="6.12.0" />
    <PackageReference Include="BenchmarkDotNet" Version="0.13.10" />
    <PackageReference Include="coverlet.collector" Version="6.0.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\MonoBallFramework.Game\MonoBallFramework.Game.csproj" />
  </ItemGroup>

  <ItemGroup>
    <!-- Test audio assets -->
    <None Include="TestAssets\Audio\**\*.*">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>
</Project>
```

### 6.3 CI/CD Integration

```yaml
# .github/workflows/audio-tests.yml
name: Audio System Tests

on:
  push:
    branches: [ main ]
  pull_request:
    branches: [ main ]

jobs:
  test:
    runs-on: ubuntu-latest

    steps:
    - uses: actions/checkout@v3

    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: 9.0.x

    - name: Restore dependencies
      run: dotnet restore

    - name: Build
      run: dotnet build --no-restore

    - name: Run Unit Tests
      run: dotnet test --no-build --filter "Category=Unit" --verbosity normal

    - name: Run Integration Tests
      run: dotnet test --no-build --filter "Category=Integration" --verbosity normal

    - name: Run Performance Tests
      run: dotnet test --no-build --filter "Category=Performance" --verbosity normal

    - name: Generate Coverage Report
      run: |
        dotnet test --no-build --collect:"XPlat Code Coverage"
        dotnet tool install -g dotnet-reportgenerator-globaltool
        reportgenerator -reports:**/coverage.cobertura.xml -targetdir:coverage -reporttypes:Html

    - name: Upload Coverage
      uses: codecov/codecov-action@v3
      with:
        files: ./coverage/coverage.cobertura.xml
```

### 6.4 Test Data Organization

```
TestAssets/
└── Audio/
    ├── sfx/
    │   ├── test_short.wav (0.1s, mono, 22050Hz)
    │   ├── test_medium.wav (0.5s, mono, 44100Hz)
    │   ├── test_long.wav (2.0s, stereo, 44100Hz)
    │   └── battle/
    │       ├── hit.wav
    │       ├── miss.wav
    │       └── critical.wav
    ├── bgm/
    │   ├── test_loop.ogg (10s looping)
    │   ├── test_intro.ogg (30s with intro section)
    │   └── test_streaming.ogg (3min, for streaming tests)
    └── cries/
        ├── 025.wav (Pikachu)
        ├── 006.wav (Charizard)
        └── 150.wav (Mewtwo)
```

---

## 7. Testing Best Practices

### 7.1 Test Naming Convention

```csharp
// Pattern: MethodName_Scenario_ExpectedBehavior
[Fact]
public void LoadSound_InvalidPath_ThrowsFileNotFoundException() { }

[Fact]
public void PlaySound_WithHigherPriority_ReplacesLowestPriority() { }

[Fact]
public async Task CrossFade_BetweenTracks_SmoothTransition() { }
```

### 7.2 Test Categories

```csharp
[Trait("Category", "Unit")]
[Trait("Subsystem", "Audio")]
public class SoundLoadingTests { }

[Trait("Category", "Integration")]
[Trait("Subsystem", "Audio")]
public class BgmSfxIntegrationTests { }

[Trait("Category", "Performance")]
[Trait("Priority", "High")]
public class MemoryPerformanceTests { }

[Trait("Category", "Functional")]
[Trait("GameSystem", "Battle")]
public class BattleAudioFunctionalTests { }
```

### 7.3 Test Documentation

```csharp
/// <summary>
/// Verifies that sound effects are loaded correctly from disk and cached
/// for subsequent use, reducing file I/O operations.
/// </summary>
/// <remarks>
/// This test ensures:
/// - Valid audio files load without errors
/// - Loaded sounds are cached in memory
/// - Cache returns same instance on repeated loads
/// - Invalid paths throw appropriate exceptions
/// </remarks>
[Fact]
public void LoadSound_ValidPath_ReturnsNonNullSoundEffect()
{
    // Test implementation
}
```

### 7.4 Coverage Goals

- **Unit Tests**: Minimum 90% code coverage for audio service classes
- **Integration Tests**: All major audio interaction scenarios covered
- **Performance Tests**: Baseline metrics established for all critical paths
- **Functional Tests**: Complete Pokemon gameplay audio sequences validated

### 7.5 Continuous Monitoring

```csharp
[Fact]
[Trait("Performance", "Benchmark")]
public void PerformanceRegression_AudioUpdate_MaintainsBaseline()
{
    // Baseline: Audio update should take < 0.5ms per frame
    const long BASELINE_MICROSECONDS = 500;

    var audioService = CreateIntegratedAudioService();
    var stopwatch = Stopwatch.StartNew();

    for (int i = 0; i < 1000; i++)
    {
        audioService.Update(TimeSpan.FromMilliseconds(16.67));
    }

    stopwatch.Stop();
    long avgMicroseconds = stopwatch.ElapsedTicks / 1000 * 1000000 / Stopwatch.Frequency;

    Assert.True(avgMicroseconds < BASELINE_MICROSECONDS,
        $"Performance regression detected: {avgMicroseconds}μs vs {BASELINE_MICROSECONDS}μs baseline");
}
```

---

## Summary

This comprehensive testing strategy provides:

1. **Complete Coverage**: Unit, integration, performance, and functional tests
2. **Pokemon-Specific**: Battle sequences, cry playback, and gameplay scenarios
3. **Performance Validation**: Memory, CPU, and loading time benchmarks
4. **Robust Infrastructure**: Mocking utilities, event recording, and profiling tools
5. **Maintainability**: Clear organization, naming conventions, and documentation
6. **CI/CD Ready**: Automated testing and coverage reporting

The test suite ensures the PokeSharp audio system is reliable, performant, and delivers an authentic Pokemon gameplay experience.
