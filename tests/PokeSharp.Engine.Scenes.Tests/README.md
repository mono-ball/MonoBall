# PokeSharp.Engine.Scenes.Tests

Unit tests for the `PokeSharp.Engine.Scenes` project.

## Test Coverage

### LoadingProgress

- **LoadingProgressTests** - Tests for thread-safe progress tracking, value clamping, and concurrent access.

### SceneManager

- **SceneManagerTests** - Tests for scene transitions, scene stack operations, and two-step transition pattern.

## Running Tests

```bash
# Run all tests in this project
dotnet test tests/PokeSharp.Engine.Scenes.Tests

# Run with verbose output
dotnet test tests/PokeSharp.Engine.Scenes.Tests --logger "console;verbosity=detailed"

# Run with code coverage
dotnet test tests/PokeSharp.Engine.Scenes.Tests /p:CollectCoverage=true
```

## Test Structure

```
PokeSharp.Engine.Scenes.Tests/
├── LoadingProgressTests.cs    # Tests for LoadingProgress thread safety
└── SceneManagerTests.cs        # Tests for SceneManager operations
```

## Dependencies

- xUnit - Test framework
- FluentAssertions - Assertion library
- Moq - Mocking framework

## Note

Some SceneManager tests may require a real GraphicsDevice instance and are better suited for integration testing. Unit
tests focus on logic and thread safety.

