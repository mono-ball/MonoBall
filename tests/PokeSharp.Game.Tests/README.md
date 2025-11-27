# PokeSharp.Game.Tests

Comprehensive test suite for PokeSharp Game systems, including map streaming and connection handling.

## Test Files

### MapStreamingSystemTests.cs
Tests for the map streaming system that handles dynamic loading/unloading of adjacent maps.

**Test Coverage Areas:**
- **Boundary Detection** (6 tests)
  - North, South, East, West edge detection
  - Corner case handling
  - Center position validation

- **Map Loading** (3 tests)
  - Track loaded maps
  - Unload distant maps
  - Multiple adjacent maps loaded simultaneously

- **World Offset Calculation** (5 tests)
  - North/South connection offsets (negative/positive Y)
  - East/West connection offsets (positive/negative X)
  - Connection offset handling

- **Edge Cases** (7 tests)
  - Initial state validation
  - Boundary validation
  - Local tile to world conversion
  - World to local tile conversion
  - Map transition handling

**Total Tests: 21**

## Running Tests

```bash
# Run all Game tests
dotnet test tests/PokeSharp.Game.Tests/PokeSharp.Game.Tests.csproj

# Run specific test class
dotnet test --filter "FullyQualifiedName~MapStreamingSystemTests"

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

## Test Structure

Tests follow xUnit patterns with:
- FluentAssertions for readable assertions
- Moq for mocking dependencies
- Arch.Core for ECS entity testing
- Test fixtures and helper methods

## Coverage Goals

- Target: >80% code coverage
- All public methods tested
- Edge cases and error conditions covered
- Integration scenarios validated
