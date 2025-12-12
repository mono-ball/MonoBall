# Audio Testing Quick Reference

Quick reference for common testing scenarios in the PokeSharp audio system.

---

## Quick Start

### 1. Run Tests
```bash
# All tests
dotnet test

# Specific category
dotnet test --filter "Category=Unit"
dotnet test --filter "Category=Integration"
dotnet test --filter "Category=Performance"
dotnet test --filter "Category=Functional"
```

### 2. Basic Test Structure
```csharp
[Fact]
[Trait("Category", "Unit")]
[Trait("Subsystem", "Audio")]
public void MethodName_Scenario_ExpectedBehavior()
{
    // Arrange
    var component = CreateTestComponent();

    // Act
    var result = component.DoSomething();

    // Assert
    Assert.NotNull(result);
}
```

---

## Common Test Patterns

### Load and Play Sound
```csharp
var contentManager = AudioTestFactory.CreateMockContentManagerWithDefaults();
var sound = contentManager.Load<ISoundEffect>("sfx/test.wav");
var instance = sound.CreateInstance();
instance.Play();

Assert.True(instance.IsPlaying);
```

### Music Playback
```csharp
var musicPlayer = AudioTestFactory.CreateMockMusicPlayer();
musicPlayer.Play("bgm/route.ogg");

Assert.Equal(MusicState.Playing, musicPlayer.State);
Assert.Equal("bgm/route.ogg", musicPlayer.CurrentTrack);
```

### Volume Control
```csharp
var musicPlayer = AudioTestFactory.CreateMockMusicPlayer();
musicPlayer.Play("bgm/test.ogg");
musicPlayer.SetVolume(0.5f);

Assert.Equal(0.5f, musicPlayer.Volume, precision: 3);
```

### Cross-Fade
```csharp
var musicPlayer = AudioTestFactory.CreateMockMusicPlayer();
musicPlayer.Play("bgm/track1.ogg");
musicPlayer.CrossFade("bgm/track2.ogg", TimeSpan.FromSeconds(2));

Assert.Equal(MusicState.FadingOut, musicPlayer.State);
```

### Event Recording
```csharp
var recorder = AudioTestFactory.CreateEventRecorder();
recorder.RecordSoundPlayed("sfx/test.wav", SoundPriority.High);

Assert.True(recorder.HasEvent(AudioEventType.SoundPlayed, "sfx/test.wav"));
Assert.Equal(1, recorder.CountEvents(AudioEventType.SoundPlayed));
```

### Performance Profiling
```csharp
var profiler = AudioTestFactory.CreatePerformanceProfiler();
profiler.StartProfiling();

// Perform operations
LoadAndPlaySounds();

profiler.TakeSnapshot("After loading", audioService);
var report = profiler.GenerateReport();

Assert.True(report.TotalDurationMs < 1000);
```

---

## Test Data

### Available Sound Effects
```
sfx/test.wav                    # Generic test sound (500ms)
sfx/menu_select.wav             # Menu selection (100ms)
sfx/jump.wav                    # Jump sound (300ms)
sfx/battle/hit.wav              # Battle hit (200ms)
sfx/battle/critical.wav         # Critical hit (400ms)
cries/025.wav                   # Pikachu cry (600ms)
```

### Available Music Tracks
```
bgm/route_1.ogg                 # Route theme (120s)
bgm/battle_wild.ogg             # Wild battle (90s)
bgm/town.ogg                    # Town theme (150s)
bgm/short_loop.ogg              # Short loop (5s)
bgm/long_track.ogg              # Long track (3min, streaming)
```

---

## Assertion Patterns

### State Verification
```csharp
Assert.Equal(MusicState.Playing, musicPlayer.State);
Assert.True(instance.IsPlaying);
Assert.False(instance.IsPaused);
```

### Value Comparison
```csharp
Assert.Equal(0.5f, volume, precision: 3);
Assert.InRange(value, 0.0f, 1.0f);
```

### Exception Testing
```csharp
Assert.Throws<ArgumentOutOfRangeException>(() =>
    musicPlayer.SetVolume(1.5f));
```

### Collection Testing
```csharp
Assert.NotEmpty(activeSounds);
Assert.All(instances, i => Assert.True(i.IsPlaying));
```

### Timing
```csharp
var stopwatch = Stopwatch.StartNew();
PerformOperation();
stopwatch.Stop();

Assert.True(stopwatch.ElapsedMilliseconds < 100);
```

---

## Mock Behaviors

### Sound Instance States
```csharp
instance.Play();    // IsPlaying = true, IsPaused = false
instance.Pause();   // IsPlaying = true, IsPaused = true
instance.Resume();  // IsPlaying = true, IsPaused = false
instance.Stop();    // IsPlaying = false, IsPaused = false
```

### Music Player States
```csharp
musicPlayer.Play("track.ogg");              // Playing
musicPlayer.Pause();                        // Paused
musicPlayer.Resume();                       // Playing
musicPlayer.FadeOut(TimeSpan.FromSeconds(1)); // FadingOut
musicPlayer.Stop();                         // Stopped
```

### Volume Calculation
```csharp
float effective = masterVolume * channelVolume;
// Example: 0.8 * 0.5 = 0.4
```

---

## Performance Baselines

### Target Metrics
- **Small sound loading**: < 50ms
- **Battle audio preload**: < 500ms
- **Audio update per frame**: < 0.5ms
- **Volume calculation**: < 10ms per 10,000 calls
- **Memory per 100 cached sounds**: < 50MB
- **Load/unload cycles**: No memory leaks

### Measurement
```csharp
var stopwatch = Stopwatch.StartNew();
// Operation
stopwatch.Stop();

Assert.True(stopwatch.ElapsedMilliseconds < TARGET_MS);
```

---

## Test Categories

### Unit
- Isolated component testing
- No external dependencies
- Fast execution (< 100ms)
- Deterministic results

### Integration
- Multi-component interaction
- Simulated real scenarios
- Moderate duration (< 5s)
- May use async operations

### Performance
- Resource usage validation
- Timing measurements
- Baseline comparisons
- Stress testing

### Functional
- End-to-end scenarios
- Pokemon gameplay flows
- Complete sequences
- User experience validation

---

## Debugging Tests

### View Test Output
```bash
dotnet test --logger "console;verbosity=detailed"
```

### Run Single Test
```bash
dotnet test --filter "FullyQualifiedName~MethodName"
```

### Debug in IDE
1. Set breakpoint in test
2. Right-click test
3. Select "Debug Test"

### Inspect Mock State
```csharp
var sound = new MockSoundEffect("test.wav", TimeSpan.FromMilliseconds(500));
var instance = sound.CreateInstance();
instance.Play();

// Check internal state
Console.WriteLine($"IsPlaying: {instance.IsPlaying}");
Console.WriteLine($"Volume: {instance.Volume}");
```

---

## Common Issues

### Test Flakiness
**Problem**: Test passes sometimes, fails others
**Solution**:
- Avoid Thread.Sleep, use Task.Delay
- Don't rely on exact timing
- Use tolerance in float comparisons

### Memory Leaks
**Problem**: Memory grows during tests
**Solution**:
```csharp
// Always dispose resources
using var audioService = CreateAudioService();
// or
audioService.Dispose();
```

### Async Test Issues
**Problem**: Test completes before async operation
**Solution**:
```csharp
[Fact]
public async Task TestAsync()
{
    await someOperation;
    Assert.True(condition);
}
```

---

## Tips and Tricks

### Use Theory for Multiple Cases
```csharp
[Theory]
[InlineData(1.0f, 0.5f, 0.5f)]
[InlineData(0.5f, 1.0f, 0.5f)]
public void TestVolume(float master, float channel, float expected)
{
    var result = master * channel;
    Assert.Equal(expected, result, precision: 3);
}
```

### Group Related Tests
```csharp
public class VolumeControlTests
{
    public class SetVolume
    {
        [Fact]
        public void ValidValue_UpdatesVolume() { }

        [Fact]
        public void InvalidValue_ThrowsException() { }
    }
}
```

### Setup/Teardown
```csharp
public class MyTests : IDisposable
{
    private readonly IAudioService _audioService;

    public MyTests()
    {
        _audioService = CreateAudioService();
    }

    public void Dispose()
    {
        _audioService?.Dispose();
    }
}
```

---

## Resources

- **Full Strategy**: `/docs/AudioSystemTestStrategy.md`
- **Summary**: `/docs/AudioTestingSummary.md`
- **Project README**: `/tests/PokeSharp.Tests.Audio/README.md`
- **xUnit Docs**: https://xunit.net/
- **Moq Docs**: https://github.com/moq/moq4

---

## Quick Commands Cheat Sheet

```bash
# Build and test
dotnet build
dotnet test

# Test with coverage
dotnet test --collect:"XPlat Code Coverage"

# Test categories
dotnet test --filter "Category=Unit"
dotnet test --filter "Category=Integration"
dotnet test --filter "Category=Performance"

# Specific subsystem
dotnet test --filter "Subsystem=Audio"

# Verbose output
dotnet test --logger "console;verbosity=detailed"

# List all tests
dotnet test --list-tests

# Run tests in parallel
dotnet test --parallel
```

---

## Example: Complete Test

```csharp
using PokeSharp.Tests.Audio.Utilities;
using PokeSharp.Tests.Audio.Utilities.Interfaces;
using Xunit;

namespace PokeSharp.Tests.Audio.Unit.Audio;

[Trait("Category", "Unit")]
[Trait("Subsystem", "Audio")]
public class SoundPlaybackTests
{
    [Fact]
    public void PlaySound_ValidSound_PlaysSuccessfully()
    {
        // Arrange
        var contentManager = AudioTestFactory.CreateMockContentManagerWithDefaults();
        var sound = contentManager.Load<ISoundEffect>("sfx/test.wav");
        var instance = sound.CreateInstance();

        // Act
        instance.Play();

        // Assert
        Assert.True(instance.IsPlaying);
        Assert.False(instance.IsPaused);
    }

    [Fact]
    public void StopSound_WhilePlaying_StopsSuccessfully()
    {
        // Arrange
        var contentManager = AudioTestFactory.CreateMockContentManagerWithDefaults();
        var sound = contentManager.Load<ISoundEffect>("sfx/test.wav");
        var instance = sound.CreateInstance();
        instance.Play();

        // Act
        instance.Stop();

        // Assert
        Assert.False(instance.IsPlaying);
    }
}
```

---

This quick reference provides the most common patterns and commands needed for audio system testing in PokeSharp.
