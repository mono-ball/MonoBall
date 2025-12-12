# Audio Streaming Tests - Test Plan Documentation

## Overview

This test suite comprehensively validates the audio streaming refactoring for `NAudioMusicPlayer` and `NAudioSoundEffectManager`. The refactoring changes from loading entire OGG files into memory to streaming from disk, which significantly reduces memory usage while maintaining audio quality.

## Test Files

- **NAudioMusicPlayerStreamingTests.cs** - 40+ test cases for music streaming
- **NAudioSoundEffectManagerStreamingTests.cs** - 35+ test cases for sound effect streaming
- **TestAudioFixtures.cs** - Helper utilities and test data generators

## Test Setup Requirements

### 1. Test Audio Files

Create the following test audio files in `tests/PokeSharp.Tests.Audio/TestData/Audio/`:

```
TestData/
└── Audio/
    ├── test_music.ogg          (3-second music file, ~100KB)
    ├── test_loop.ogg           (5-second file with loop points)
    └── SFX/
        ├── test_sfx.ogg        (1-second sound effect)
        └── test_long_sfx.ogg   (3-second sound effect)
```

**How to create test files:**

```bash
# Using ffmpeg to create test OGG files with sine wave tones
ffmpeg -f lavfi -i "sine=frequency=440:duration=3" -acodec libvorbis test_music.ogg
ffmpeg -f lavfi -i "sine=frequency=880:duration=5" -acodec libvorbis test_loop.ogg
ffmpeg -f lavfi -i "sine=frequency=1320:duration=1" -acodec libvorbis test_sfx.ogg
ffmpeg -f lavfi -i "sine=frequency=660:duration=3" -acodec libvorbis test_long_sfx.ogg
```

**Adding loop points to OGG files:**

For `test_loop.ogg`, add loop metadata using `vorbiscomment`:

```bash
# Create loop point tags
echo "LOOPSTART=44100" > loop_tags.txt
echo "LOOPLENGTH=88200" >> loop_tags.txt

# Apply tags to OGG file
vorbiscomment -w -c loop_tags.txt test_loop.ogg
```

### 2. Required NuGet Packages

Ensure these packages are installed in `PokeSharp.Tests.Audio.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="xunit" Version="2.6.1" />
  <PackageReference Include="xunit.runner.visualstudio" Version="2.5.3" />
  <PackageReference Include="NAudio.Vorbis" Version="1.5.0" />
  <PackageReference Include="NAudio" Version="2.2.1" />
</ItemGroup>
```

### 3. Test Environment Configuration

- **Memory Baseline**: Tests capture initial memory before each test to measure overhead
- **File Handle Verification**: Tests verify file handles are properly released
- **Timeout Values**: Adjust timeout values in tests based on CI/CD environment performance

## Test Categories

### 1. Basic Streaming Tests (NAudioMusicPlayer)

**Purpose**: Verify core streaming functionality works correctly.

| Test | Description | Success Criteria |
|------|-------------|------------------|
| `StreamMusic_PlayFromStartToFinish_CompletesSuccessfully` | Play a track from start to end | Track plays completely, position advances correctly |
| `StreamMusic_MemoryUsageStaysLow_UnderOneMemoryPerStream` | Verify memory efficiency | Memory overhead < 1MB (should not load entire file) |
| `StreamMusic_ChecksForAudioGlitches_NoDropouts` | Manual verification test | No audio dropouts or glitches (requires manual listening) |

**Expected Behavior**:
- Music streams progressively from disk
- Memory usage stays constant regardless of file size
- No audio quality degradation

### 2. Loop Point Tests (NAudioMusicPlayer)

**Purpose**: Verify seamless looping with custom loop points.

| Test | Description | Success Criteria |
|------|-------------|------------------|
| `StreamWithLoopPoints_SeamlessLoopTransition_NoGaps` | Loop transition has no gaps | No audio discontinuity at loop boundary |
| `StreamWithLoopPoints_PlaysCorrectSection_NotFullTrack` | Only loops specified section | Position resets to loop start, not file start |
| `StreamWithLoopPoints_VerifyLoopBoundaries_CorrectSampleRange` | Loop boundaries are exact | Loop starts/ends at exact sample positions |

**Expected Behavior**:
- Loop points work correctly with streaming (not just memory buffering)
- Sample-accurate loop transitions
- No audio artifacts at loop boundaries

### 3. Crossfade Tests (NAudioMusicPlayer)

**Purpose**: Verify smooth crossfading between streaming tracks.

| Test | Description | Success Criteria |
|------|-------------|------------------|
| `Crossfade_BothStreamsPlaySimultaneously_DuringFade` | Both tracks play during fade | Two VorbisWaveReaders active simultaneously |
| `Crossfade_FadeTimingIsCorrect_MatchesSpecifiedDuration` | Fade duration is accurate | Actual duration matches requested duration (±100ms) |
| `Crossfade_OldStreamDisposesAfterFade_NoMemoryLeak` | Old stream cleans up | File handle released, memory doesn't accumulate |

**Expected Behavior**:
- Smooth volume transitions during crossfade
- Old stream properly disposed after fade completes
- No memory leaks from overlapping streams

### 4. FadeOutAndPlay Tests (NAudioMusicPlayer)

**Purpose**: Verify sequential fade-out then play behavior.

| Test | Description | Success Criteria |
|------|-------------|------------------|
| `FadeOutAndPlay_FadeOutCompletesBeforeNewTrack_SequentialPlayback` | Sequential, not simultaneous | Old track fades out completely before new track starts |
| `FadeOutAndPlay_NewTrackStartsImmediately_AfterFade` | No gap between tracks | New track starts within 100ms of fade completion |

**Expected Behavior**:
- Clean transition between tracks
- No simultaneous playback (unlike crossfade)
- Precise timing control

### 5. Resource Management Tests (Both Players)

**Purpose**: Verify no memory leaks or file handle leaks.

| Test | Description | Success Criteria |
|------|-------------|------------------|
| `ResourceManagement_VorbisReaderDisposedOnTrackChange_NoLeaks` | File handles released on change | Can open file exclusively after track change |
| `ResourceManagement_NoFileHandleLeaks_After100TrackChanges` | Stress test file handles | No leaked handles after 100 rapid changes |
| `ResourceManagement_MemoryReturnsToBaseline_AfterStopAndGC` | Memory cleanup after stop | Memory returns to within 100KB of baseline |
| `ResourceManagement_DisposeReleasesAllResources_CleanShutdown` | Dispose is thorough | All resources released on Dispose() |

**Expected Behavior**:
- VorbisWaveReader properly disposed when tracks change
- File handles never leak, even under stress
- Memory doesn't accumulate over time

### 6. Concurrent Access Tests (Both Players)

**Purpose**: Verify thread-safety and race condition handling.

| Test | Description | Success Criteria |
|------|-------------|------------------|
| `ConcurrentAccess_RapidPlayStop_NoExceptions` | 100 rapid Play/Stop calls | No exceptions thrown |
| `ConcurrentAccess_ChangeTracksDuringFade_NoRaceConditions` | Change track during crossfade | No crashes or corrupted state |
| `ConcurrentAccess_VolumeChangesDuringPlayback_ThreadSafe` | 50 concurrent volume changes | Playback continues smoothly |

**Expected Behavior**:
- Thread-safe operations throughout
- No race conditions in crossfade logic
- Graceful handling of rapid state changes

### 7. Error Handling Tests (Both Players)

**Purpose**: Verify graceful error handling and validation.

| Test | Description | Success Criteria |
|------|-------------|------------------|
| `ErrorHandling_MissingAudioFile_ThrowsFileNotFoundException` | Missing file throws exception | FileNotFoundException thrown |
| `ErrorHandling_CorruptedOggFile_ThrowsInvalidDataException` | Corrupted file detected | InvalidDataException thrown |
| `ErrorHandling_SeekBeyondFileEnd_HandlesGracefully` | Invalid seek doesn't crash | Returns false, no exception |
| `ErrorHandling_InvalidLoopPoints_ThrowsArgumentException` | Invalid loop points rejected | ArgumentException thrown |

**Expected Behavior**:
- Clear, specific exceptions for error conditions
- No crashes or undefined behavior
- Validates inputs before processing

### 8. Sound Effect Specific Tests (NAudioSoundEffectManager)

**Purpose**: Verify sound effect streaming and concurrent playback.

| Test | Description | Success Criteria |
|------|-------------|------------------|
| `PlayMultipleSoundEffects_Simultaneously_AllPlayConcurrently` | Multiple instances of same SFX | All instances play independently |
| `PlayManySoundEffects_MemoryScalesLinearly_NoExponentialGrowth` | 10 concurrent SFX | Memory scales linearly, not exponentially |
| `PlaySameSoundEffectMultipleTimes_IndependentInstances_NoInterference` | Same SFX played 3 times | Each instance is independent |
| `StopSoundEffect_DisposesStreamImmediately_ReleasesFileHandle` | Stop releases resources | File handle released within 200ms |

**Expected Behavior**:
- Multiple instances of same sound effect can play simultaneously
- Each instance streams independently
- Memory usage is proportional to active stream count

### 9. Performance Benchmarks (Both Players)

**Purpose**: Measure and compare performance metrics.

| Test | Description | Success Criteria |
|------|-------------|------------------|
| `Performance_StreamingVsMemoryLoading_CompareMemoryFootprint` | Memory comparison | Streaming uses < 10% of file size |
| `Performance_MaxConcurrentStreams_IdentifyLimit` | Find maximum concurrent streams | At least 10 concurrent streams supported |

**Expected Behavior**:
- Streaming uses dramatically less memory than full buffering
- Performance scales well with multiple concurrent streams

## Test Execution Guide

### Running All Tests

```bash
# Run all audio streaming tests
dotnet test tests/PokeSharp.Tests.Audio/ --filter "FullyQualifiedName~Streaming" --verbosity detailed

# Run specific test class
dotnet test --filter "FullyQualifiedName~NAudioMusicPlayerStreamingTests"

# Run specific test category
dotnet test --filter "FullyQualifiedName~ResourceManagement"
```

### Debugging Failed Tests

1. **Memory Tests Failing**:
   - Ensure GC is collecting properly (may need longer delays)
   - Check for background processes affecting memory measurements
   - Verify test audio files are correct sizes

2. **File Handle Tests Failing**:
   - Increase delay after Stop() calls (file system latency)
   - Check that anti-virus isn't locking files
   - Verify Dispose() is being called

3. **Timing Tests Failing**:
   - Increase tolerance values for slower CI/CD environments
   - Check system audio latency settings
   - Verify test audio files have correct durations

### CI/CD Integration

**GitHub Actions Example**:

```yaml
- name: Run Audio Streaming Tests
  run: |
    dotnet test tests/PokeSharp.Tests.Audio/ \
      --filter "FullyQualifiedName~Streaming" \
      --logger "trx;LogFileName=audio-streaming-tests.trx" \
      --collect:"XPlat Code Coverage"

- name: Upload Test Results
  uses: actions/upload-artifact@v3
  if: always()
  with:
    name: audio-streaming-test-results
    path: '**/audio-streaming-tests.trx'
```

## Test Coverage Goals

| Component | Target Coverage | Critical Paths |
|-----------|----------------|----------------|
| NAudioMusicPlayer | > 85% | Play(), CrossFade(), FadeOutAndPlay(), Dispose() |
| NAudioSoundEffectManager | > 85% | PlaySoundEffect(), StopSoundEffect(), StopAllSoundEffects() |
| Loop Point Logic | 100% | Loop boundary calculations, sample positioning |
| Resource Cleanup | 100% | VorbisWaveReader disposal, file handle management |

## Known Limitations

1. **Audio Quality Tests**: Require manual listening or specialized audio analysis tools
2. **Exact Timing**: Tests use tolerance values due to system timing variability
3. **Platform Differences**: Audio latency varies by OS (Windows/Linux/macOS)
4. **Loop Point Metadata**: Requires specific OGG encoding with loop tags

## Future Test Enhancements

- [ ] Automated audio quality analysis (FFT-based glitch detection)
- [ ] Cross-platform compatibility tests (Windows/Linux/macOS)
- [ ] Performance regression tests (baseline comparison)
- [ ] Integration tests with actual game audio files
- [ ] Memory profiler integration for detailed leak detection
- [ ] Audio waveform comparison tests (pixel-perfect validation)

## Troubleshooting

### Common Issues

**Issue**: Tests fail with "File not found"
**Solution**: Run `setup-test-files.sh` to generate test audio files using ffmpeg

**Issue**: Memory tests are flaky
**Solution**: Increase GC delay times or disable parallel test execution

**Issue**: File handle tests fail on Windows
**Solution**: Ensure no processes (IDE, media players) are locking test files

**Issue**: Timing tests fail in CI/CD
**Solution**: Increase timeout tolerance values by 50-100ms

## Test Maintenance

- **Add new tests** when bugs are discovered in streaming logic
- **Update timeout values** if CI/CD environment changes
- **Regenerate test audio files** if format specifications change
- **Review memory baselines** quarterly as codebase grows

## Contact

For questions about these tests, see:
- **Implementation**: MonoBallFramework.Audio namespace
- **Test Design**: This document
- **CI/CD Issues**: .github/workflows/

---

**Test Suite Version**: 1.0.0
**Last Updated**: 2025-12-11
**Maintainer**: Audio Team
