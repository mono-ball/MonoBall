# Mod Developer Testing Guide

**Version:** 1.0
**Date:** 2025-12-02
**Audience:** Mod Developers

---

## Introduction

This guide helps mod developers test their mods thoroughly before release. Following these testing practices ensures your mod is stable, safe, and compatible with PokeSharp's event-driven architecture.

## Table of Contents

1. [Quick Start](#quick-start)
2. [Setting Up Your Test Environment](#setting-up-your-test-environment)
3. [Writing Mod Tests](#writing-mod-tests)
4. [Testing Event Handlers](#testing-event-handlers)
5. [Testing Scripts](#testing-scripts)
6. [Performance Testing](#performance-testing)
7. [Security Testing](#security-testing)
8. [Integration Testing](#integration-testing)
9. [Common Pitfalls](#common-pitfalls)
10. [Best Practices](#best-practices)

---

## Quick Start

### Basic Mod Test Template

```csharp
using NUnit.Framework;
using FluentAssertions;

[TestFixture]
public class MyModTests
{
    private MyMod _mod;
    private EventBus _eventBus;

    [SetUp]
    public void Setup()
    {
        _eventBus = new EventBus();
        _mod = new MyMod();
        _mod.Initialize(_eventBus);
    }

    [TearDown]
    public void TearDown()
    {
        _eventBus?.Dispose();
    }

    [Test]
    public void MyMod_HandlesPlayerMoveEvent_Correctly()
    {
        // Arrange
        var eventReceived = false;
        _mod.OnPlayerMove += () => eventReceived = true;

        // Act
        _eventBus.Publish(new PlayerMoveEvent());

        // Assert
        eventReceived.Should().BeTrue();
    }
}
```

---

## Setting Up Your Test Environment

### 1. Create Test Project

```bash
dotnet new nunit -n MyMod.Tests
cd MyMod.Tests
dotnet add package FluentAssertions
dotnet add package NSubstitute
dotnet add reference ../MyMod/MyMod.csproj
```

### 2. Project Structure

```
MyMod/
├── src/
│   └── MyMod.cs
├── tests/
│   ├── MyModTests.cs
│   ├── EventTests.cs
│   ├── ScriptTests.cs
│   └── test-data/
└── MyMod.csproj
```

### 3. Test Configuration

```xml
<!-- MyMod.Tests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="NUnit" Version="3.14.0" />
    <PackageReference Include="NUnit3TestAdapter" Version="4.5.0" />
    <PackageReference Include="FluentAssertions" Version="6.12.0" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
  </ItemGroup>
</Project>
```

---

## Writing Mod Tests

### Test Your Mod Initialization

```csharp
[Test]
public void Mod_Initialize_RegistersEventHandlers()
{
    // Arrange
    var eventBus = new EventBus();
    var mod = new MyMod();

    // Act
    mod.Initialize(eventBus);

    // Assert
    mod.IsInitialized.Should().BeTrue();
    mod.RegisteredHandlerCount.Should().BeGreaterThan(0);
}

[Test]
public void Mod_Initialize_CalledTwice_DoesNotThrow()
{
    // Arrange
    var mod = new MyMod();

    // Act & Assert
    Action act = () =>
    {
        mod.Initialize(new EventBus());
        mod.Initialize(new EventBus()); // Second call
    };

    act.Should().NotThrow("double initialization should be safe");
}
```

### Test Mod Configuration

```csharp
[Test]
public void Mod_LoadConfiguration_ValidConfig_LoadsSuccessfully()
{
    // Arrange
    var config = new ModConfig
    {
        EnableFeatureX = true,
        MaxSpawnCount = 10
    };

    // Act
    _mod.LoadConfiguration(config);

    // Assert
    _mod.Configuration.EnableFeatureX.Should().BeTrue();
    _mod.Configuration.MaxSpawnCount.Should().Be(10);
}

[Test]
public void Mod_LoadConfiguration_InvalidConfig_UsesDefaults()
{
    // Arrange
    var invalidConfig = new ModConfig
    {
        MaxSpawnCount = -5 // Invalid
    };

    // Act
    _mod.LoadConfiguration(invalidConfig);

    // Assert
    _mod.Configuration.MaxSpawnCount.Should().Be(1, "should use default");
}
```

---

## Testing Event Handlers

### Test Event Subscriptions

```csharp
[Test]
public void Mod_SubscribesToPlayerMoveEvent_ReceivesEvent()
{
    // Arrange
    var eventReceived = false;
    _eventBus.Subscribe<PlayerMoveEvent>(evt => eventReceived = true);

    // Act
    _eventBus.Publish(new PlayerMoveEvent
    {
        NewPosition = new Vector2(10, 5)
    });

    // Assert
    eventReceived.Should().BeTrue();
}

[Test]
public void Mod_HandlesMultipleEvents_ProcessesAll()
{
    // Arrange
    var eventsHandled = 0;
    _mod.OnEventHandled += () => eventsHandled++;

    // Act
    _eventBus.Publish(new PlayerMoveEvent());
    _eventBus.Publish(new PlayerAttackEvent());
    _eventBus.Publish(new ItemPickedUpEvent());

    // Assert
    eventsHandled.Should().Be(3);
}
```

### Test Event Publishing

```csharp
[Test]
public void Mod_PublishesCustomEvent_OtherSubscribersReceive()
{
    // Arrange
    var eventReceived = false;
    var eventData = string.Empty;

    _eventBus.Subscribe<MyModCustomEvent>(evt =>
    {
        eventReceived = true;
        eventData = evt.CustomData;
    });

    // Act
    _mod.TriggerCustomAction("test-data");

    // Assert
    eventReceived.Should().BeTrue();
    eventData.Should().Be("test-data");
}

[Test]
public void Mod_PublishesEvent_WithCorrectEventData()
{
    // Arrange
    MyModCustomEvent? receivedEvent = null;
    _eventBus.Subscribe<MyModCustomEvent>(evt => receivedEvent = evt);

    // Act
    _mod.TriggerCustomAction("expected-data");

    // Assert
    receivedEvent.Should().NotBeNull();
    receivedEvent!.CustomData.Should().Be("expected-data");
    receivedEvent.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
}
```

### Test Event Priorities

```csharp
[Test]
public void Mod_EventHandlers_ExecuteInPriorityOrder()
{
    // Arrange
    var executionOrder = new List<string>();

    _eventBus.Subscribe<TestEvent>(
        evt => executionOrder.Add("low"),
        priority: 1
    );
    _eventBus.Subscribe<TestEvent>(
        evt => executionOrder.Add("high"),
        priority: 10
    );
    _eventBus.Subscribe<TestEvent>(
        evt => executionOrder.Add("medium"),
        priority: 5
    );

    // Act
    _eventBus.Publish(new TestEvent());

    // Assert
    executionOrder.Should().ContainInOrder("high", "medium", "low");
}
```

---

## Testing Scripts

### Test Script Compilation

```csharp
[Test]
public void Script_ValidSyntax_CompilesSuccessfully()
{
    // Arrange
    var scriptCode = @"
public class MyBehavior : TileBehaviorScriptBase
{
    public override bool IsBlockedFrom(ScriptContext ctx, Direction from, Direction to)
    {
        return true;
    }
}
return new MyBehavior();
";

    // Act
    var result = _mod.CompileScript(scriptCode);

    // Assert
    result.Success.Should().BeTrue();
    result.CompiledScript.Should().NotBeNull();
}

[Test]
public void Script_InvalidSyntax_FailsCompilation()
{
    // Arrange
    var invalidScript = "public class { invalid }";

    // Act
    var result = _mod.CompileScript(invalidScript);

    // Assert
    result.Success.Should().BeFalse();
    result.Errors.Should().NotBeEmpty();
}
```

### Test Script Execution

```csharp
[Test]
public async Task Script_Execute_ReturnsExpectedBehavior()
{
    // Arrange
    var script = _mod.LoadScript("test-behavior.csx");
    var context = new ScriptContext
    {
        Entity = CreateTestEntity(),
        World = CreateTestWorld()
    };

    // Act
    var result = await script.IsBlockedFrom(context, Direction.Up, Direction.Up);

    // Assert
    result.Should().BeTrue();
}

[Test]
public void Script_StateManagement_WorksCorrectly()
{
    // Arrange
    var script = _mod.LoadScript("stateful-behavior.csx");
    var context = new ScriptContext();

    // Act
    context.SetState("counter", 0);
    script.Execute(context);
    var counter = context.GetState<int>("counter");

    // Assert
    counter.Should().Be(1, "script should increment counter");
}
```

---

## Performance Testing

### Test Event Handler Performance

```csharp
[Test]
public void Mod_EventHandler_ExecutesWithinBudget()
{
    // Arrange
    const double maxExecutionMs = 1.0; // 1ms budget
    var sw = Stopwatch.StartNew();

    // Act
    for (int i = 0; i < 100; i++)
    {
        _eventBus.Publish(new TestEvent());
    }

    sw.Stop();
    var avgMs = sw.Elapsed.TotalMilliseconds / 100.0;

    // Assert
    avgMs.Should().BeLessThan(maxExecutionMs,
        $"handler should execute in under {maxExecutionMs}ms");
}

[Test]
public void Mod_DoesNotAllocateExcessiveMemory()
{
    // Arrange
    var gen0Before = GC.CollectionCount(0);

    // Act - Run mod for 1000 frames
    for (int i = 0; i < 1000; i++)
    {
        _mod.Update(deltaTime: 0.016f);
    }

    var gen0After = GC.CollectionCount(0);
    var collections = gen0After - gen0Before;

    // Assert
    collections.Should().BeLessThan(10,
        "mod should not cause excessive garbage collections");
}
```

---

## Security Testing

### Test Sandboxing

```csharp
[Test]
public void Mod_CannotAccessFileSystem()
{
    // Arrange
    var script = @"
System.IO.File.ReadAllText(""C:/secrets.txt"");
";

    // Act & Assert
    Action act = () => _mod.ExecuteScript(script);
    act.Should().Throw<SecurityException>();
}

[Test]
public void Mod_CannotAccessRestrictedAPIs()
{
    // Act & Assert
    Action act = () => _mod.ShutdownGame();
    act.Should().Throw<SecurityException>("shutdown is restricted");
}
```

---

## Integration Testing

### Test With Other Mods

```csharp
[Test]
public void Mod_WorksWithOtherMods_NoConflicts()
{
    // Arrange
    var mod1 = new MyMod();
    var mod2 = new OtherMod();
    var eventBus = new EventBus();

    // Act
    mod1.Initialize(eventBus);
    mod2.Initialize(eventBus);

    // Assert
    Action act = () =>
    {
        mod1.Update(0.016f);
        mod2.Update(0.016f);
    };

    act.Should().NotThrow("mods should not conflict");
}
```

---

## Common Pitfalls

### 1. **Stateful Scripts**

**❌ Wrong:**
```csharp
public class BadBehavior : TileBehaviorScriptBase
{
    private int counter = 0; // DON'T: Instance state

    public override bool IsBlockedFrom(...)
    {
        counter++; // Will cause issues
        return counter > 5;
    }
}
```

**✅ Correct:**
```csharp
public class GoodBehavior : TileBehaviorScriptBase
{
    public override bool IsBlockedFrom(ScriptContext ctx, ...)
    {
        var counter = ctx.GetState<int>("counter");
        counter++;
        ctx.SetState("counter", counter);
        return counter > 5;
    }
}
```

### 2. **Not Handling Null Events**

**❌ Wrong:**
```csharp
_eventBus.Subscribe<PlayerMoveEvent>(evt =>
{
    var position = evt.NewPosition; // May be null
    DoSomethingWith(position.X);
});
```

**✅ Correct:**
```csharp
_eventBus.Subscribe<PlayerMoveEvent>(evt =>
{
    if (evt?.NewPosition == null) return;
    DoSomethingWith(evt.NewPosition.X);
});
```

### 3. **Blocking Event Handlers**

**❌ Wrong:**
```csharp
_eventBus.Subscribe<SaveEvent>(evt =>
{
    Thread.Sleep(5000); // Blocks entire game!
});
```

**✅ Correct:**
```csharp
_eventBus.Subscribe<SaveEvent>(evt =>
{
    Task.Run(async () =>
    {
        await SaveDataAsync();
    });
});
```

---

## Best Practices

### 1. Test Coverage

Aim for:
- **80%+ code coverage**
- **100% coverage for critical paths**
- Test all public APIs
- Test all event handlers

### 2. Test Organization

```
tests/
├── unit/           # Individual component tests
├── integration/    # Mod interaction tests
├── performance/    # Performance benchmarks
└── scripts/        # Script tests
```

### 3. Naming Conventions

```csharp
// Pattern: [MethodName]_[Scenario]_[ExpectedBehavior]
HandlePlayerMove_WhenPlayerEntersWater_TriggersSwimAnimation()
LoadConfig_InvalidPath_UsesDefaultConfig()
PublishEvent_NoSubscribers_DoesNotThrow()
```

### 4. Test Independence

Each test should:
- Set up its own state
- Not depend on other tests
- Clean up after itself
- Be able to run in any order

### 5. Use Test Helpers

```csharp
public static class TestHelpers
{
    public static Entity CreateTestPlayer() =>
        new Entity { Position = Vector2.Zero };

    public static World CreateTestWorld() =>
        World.Create();

    public static void AssertEventFired<T>(EventBus bus, Action action)
        where T : IEvent
    {
        var fired = false;
        bus.Subscribe<T>(evt => fired = true);
        action();
        fired.Should().BeTrue($"{typeof(T).Name} should have been published");
    }
}
```

---

## Running Your Tests

### Command Line

```bash
# Run all tests
dotnet test

# Run specific test
dotnet test --filter "FullyQualifiedName~MyModTests.HandlePlayerMove"

# Run with coverage
dotnet test /p:CollectCoverage=true
```

### Visual Studio

1. Open Test Explorer (Test → Test Explorer)
2. Click "Run All" or right-click specific tests
3. View results and coverage

### CI/CD

```yaml
# .github/workflows/mod-tests.yml
name: Mod Tests

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v2
      - name: Setup .NET
        uses: actions/setup-dotnet@v1
      - name: Run Tests
        run: dotnet test --logger "console;verbosity=detailed"
```

---

## Example: Complete Mod Test Suite

```csharp
[TestFixture]
public class CompleteModTestSuite
{
    private MyMod _mod;
    private EventBus _eventBus;
    private ModContext _context;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        // Setup that runs once for all tests
    }

    [SetUp]
    public void Setup()
    {
        _eventBus = new EventBus();
        _mod = new MyMod();
        _context = new ModContext();
        _mod.Initialize(_eventBus);
    }

    [TearDown]
    public void TearDown()
    {
        _mod?.Dispose();
        _eventBus?.Dispose();
    }

    [Test]
    [Category("Initialization")]
    public void Initialization_Tests() { }

    [Test]
    [Category("Events")]
    public void Event_Tests() { }

    [Test]
    [Category("Performance")]
    [Explicit] // Only run when explicitly requested
    public void Performance_Tests() { }
}
```

---

## Resources

- [NUnit Documentation](https://docs.nunit.org/)
- [FluentAssertions Documentation](https://fluentassertions.com/)
- [PokeSharp Event System Guide](./event-system-guide.md)
- [PokeSharp Scripting API](./scripting-api.md)

---

## Getting Help

- **Discord:** #mod-development channel
- **GitHub:** Open an issue with `[mod-testing]` tag
- **Forums:** PokeSharp Modding Community

---

**Happy Testing! 🧪**

Remember: Good tests = Stable mods = Happy players!
