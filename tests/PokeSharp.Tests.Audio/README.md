# PokeSharp Audio System Tests

Comprehensive test suite for the PokeSharp audio system, covering unit tests, integration tests, performance benchmarks, and functional gameplay scenarios.

## Project Structure

```
PokeSharp.Tests.Audio/
├── Unit/
│   └── Audio/
│       ├── VolumeControlTests.cs
│       └── AudioStateTransitionTests.cs
├── Integration/
│   └── Audio/
├── Performance/
│   └── Audio/
├── Functional/
│   └── Audio/
├── Utilities/
│   ├── Interfaces/
│   │   └── IAudioService.cs
│   ├── Mocks/
│   │   ├── MockSoundEffect.cs
│   │   ├── MockMusicPlayer.cs
│   │   └── MockContentManager.cs
│   ├── AudioEventRecorder.cs
│   ├── AudioPerformanceProfiler.cs
│   └── AudioTestFactory.cs
└── README.md
```

## Running Tests

### Run All Tests
```bash
dotnet test
```

### Run Tests by Category
```bash
# Unit tests only
dotnet test --filter "Category=Unit"

# Integration tests
dotnet test --filter "Category=Integration"

# Performance tests
dotnet test --filter "Category=Performance"

# Functional tests
dotnet test --filter "Category=Functional"
```

### Run Tests by Subsystem
```bash
# Audio subsystem tests
dotnet test --filter "Subsystem=Audio"
```

### Run with Code Coverage
```bash
dotnet test --collect:"XPlat Code Coverage"
```

## Test Categories

### Unit Tests
Tests individual audio components in isolation using mocks:
- Sound loading and unloading
- Volume control calculations
- Audio state transitions
- Sound prioritization logic

### Integration Tests
Tests interaction between multiple audio components:
- Multiple concurrent sounds
- BGM with SFX overlay
- Audio transitions and fades
- Resource loading pipelines

### Performance Tests
Validates performance characteristics and resource usage:
- Memory usage under load
- CPU usage during audio mixing
- Loading time benchmarks
- Concurrent sound limits

### Functional Tests
End-to-end tests for Pokemon gameplay scenarios:
- Battle audio sequences
- Pokemon cry playback
- Music looping correctness
- Pause/resume behavior

## Test Utilities

### MockSoundEffect
Mock implementation of sound effects for isolated testing.

```csharp
var sound = new MockSoundEffect("test.wav", TimeSpan.FromMilliseconds(500));
var instance = sound.CreateInstance();
instance.Play();
```

### MockMusicPlayer
Mock music player with fade and state management support.

```csharp
var musicPlayer = AudioTestFactory.CreateMockMusicPlayer();
musicPlayer.Play("bgm/route.ogg", loop: true);
musicPlayer.CrossFade("bgm/battle.ogg", TimeSpan.FromSeconds(2));
```

### MockContentManager
Content manager that provides mock audio assets.

```csharp
var contentManager = AudioTestFactory.CreateMockContentManagerWithDefaults();
var sound = contentManager.Load<ISoundEffect>("sfx/test.wav");
```

### AudioEventRecorder
Records audio events for verification in tests.

```csharp
var recorder = AudioTestFactory.CreateEventRecorder();
recorder.RecordSoundPlayed("sfx/test.wav", SoundPriority.Normal);
Assert.True(recorder.HasEvent(AudioEventType.SoundPlayed, "sfx/test.wav"));
```

### AudioPerformanceProfiler
Profiles memory and CPU usage during audio operations.

```csharp
var profiler = AudioTestFactory.CreatePerformanceProfiler();
profiler.StartProfiling();
// ... perform audio operations ...
profiler.TakeSnapshot("After loading", audioService);
var report = profiler.GenerateReport();
report.PrintReport();
```

## Test Naming Convention

Tests follow the pattern: `MethodName_Scenario_ExpectedBehavior`

Examples:
- `LoadSound_ValidPath_ReturnsNonNullSoundEffect`
- `PlaySound_WithHigherPriority_ReplacesLowestPriority`
- `CrossFade_BetweenTracks_SmoothTransition`

## Coverage Goals

- **Unit Tests**: Minimum 90% code coverage
- **Integration Tests**: All major interaction scenarios
- **Performance Tests**: Baseline metrics for critical paths
- **Functional Tests**: Complete gameplay audio sequences

## Adding New Tests

1. Determine the appropriate category (Unit/Integration/Performance/Functional)
2. Create test file in the correct directory
3. Add appropriate traits:
   ```csharp
   [Trait("Category", "Unit")]
   [Trait("Subsystem", "Audio")]
   ```
4. Use test utilities from `AudioTestFactory`
5. Follow naming conventions
6. Document test purpose in XML comments

## CI/CD Integration

Tests are automatically run in CI/CD pipeline:
- On every push to main branch
- On pull requests
- Coverage reports uploaded to Codecov

## Dependencies

- xUnit 2.6.2 - Test framework
- Moq 4.20.69 - Mocking framework
- FluentAssertions 6.12.0 - Assertion library
- BenchmarkDotNet 0.13.10 - Performance benchmarking
- coverlet.collector 6.0.0 - Code coverage

## Documentation

See `/docs/AudioSystemTestStrategy.md` for comprehensive testing strategy, test templates, and best practices.
