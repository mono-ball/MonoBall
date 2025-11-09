# Dependency Injection Migration Guide

## Overview

The PokeSharp ECS architecture now supports **automatic dependency injection** for systems, eliminating manual wiring and making code more maintainable and testable.

## Table of Contents

- [Key Concepts](#key-concepts)
- [Quick Start](#quick-start)
- [Migration Patterns](#migration-patterns)
- [Best Practices](#best-practices)
- [Advanced Scenarios](#advanced-scenarios)
- [Troubleshooting](#troubleshooting)

---

## Key Concepts

### Service Container

The `ServiceContainer` manages all registered services with two lifetime modes:

- **Singleton**: One instance shared across all systems (default for systems)
- **Transient**: New instance created for each injection

### SystemFactory

The `SystemFactory` automatically creates systems by:

1. Analyzing constructor parameters
2. Resolving dependencies from the container
3. Instantiating the system with all dependencies

### Constructor Injection

Systems declare their dependencies as constructor parameters:

```csharp
public class MySystem : SystemBase
{
    private readonly SpatialHashSystem _spatialHash;
    private readonly ILogger<MySystem> _logger;

    public MySystem(World world, SpatialHashSystem spatialHash, ILogger<MySystem> logger)
        : base(world)
    {
        _spatialHash = spatialHash;
        _logger = logger;
    }
}
```

---

## Quick Start

### 1. Register Services (One-Time Setup)

```csharp
var systemManager = new SystemManager(logger);

// Register shared services first
systemManager.RegisterService(spatialHashSystem);
systemManager.RegisterService(collisionSystem);
systemManager.RegisterService(loggerFactory);

// Register systems with automatic DI
systemManager.RegisterSystem<MovementSystem>();
systemManager.RegisterSystem<PathfindingSystem>();
systemManager.RegisterSystem<NpcBehaviorSystem>();

// Initialize with world
systemManager.Initialize(world);
```

### 2. Convert Existing System

**BEFORE (Manual Wiring):**

```csharp
public class MovementSystem : BaseSystem
{
    private SpatialHashSystem? _spatialHashSystem;

    public void SetSpatialHashSystem(SpatialHashSystem system)
    {
        _spatialHashSystem = system;
    }

    public override void Update(World world, float deltaTime)
    {
        if (_spatialHashSystem == null)
            throw new InvalidOperationException("SpatialHashSystem not set");

        // Movement logic...
    }
}

// Manual registration
var movementSystem = new MovementSystem();
movementSystem.SetSpatialHashSystem(spatialHashSystem);
systemManager.RegisterSystem(movementSystem);
```

**AFTER (Constructor Injection):**

```csharp
public class MovementSystem : SystemBase
{
    private readonly SpatialHashSystem _spatialHash;
    private readonly ILogger<MovementSystem> _logger;

    public MovementSystem(World world, SpatialHashSystem spatialHash, ILogger<MovementSystem>? logger = null)
        : base(world)
    {
        _spatialHash = spatialHash ?? throw new ArgumentNullException(nameof(spatialHash));
        _logger = logger;
    }

    public override void Update(World world, float deltaTime)
    {
        EnsureInitialized();

        // Movement logic using _spatialHash directly
        // No null checks needed!
    }
}

// Automatic registration
systemManager.RegisterService(spatialHashSystem);
systemManager.RegisterSystem<MovementSystem>();
```

---

## Migration Patterns

### Pattern 1: Basic System Dependencies

**Before:**
```csharp
public class CollisionSystem : BaseSystem
{
    private SpatialHashSystem? _spatialHash;

    public void SetSpatialHashSystem(SpatialHashSystem system)
        => _spatialHashSystem = system;
}
```

**After:**
```csharp
public class CollisionSystem : SystemBase
{
    private readonly SpatialHashSystem _spatialHash;

    public CollisionSystem(World world, SpatialHashSystem spatialHash)
        : base(world)
    {
        _spatialHash = spatialHash ?? throw new ArgumentNullException(nameof(spatialHash));
    }
}
```

### Pattern 2: Optional Dependencies

```csharp
public class RenderSystem : SystemBase
{
    private readonly ILogger<RenderSystem>? _logger;
    private readonly DebugRenderer? _debugRenderer;

    // Use default parameter values for optional dependencies
    public RenderSystem(
        World world,
        ILogger<RenderSystem>? logger = null,
        DebugRenderer? debugRenderer = null)
        : base(world)
    {
        _logger = logger;
        _debugRenderer = debugRenderer;
    }

    public override void Update(World world, float deltaTime)
    {
        EnsureInitialized();

        // Safe to use optional dependencies
        _logger?.LogDebug("Rendering frame");
        _debugRenderer?.DrawGrid();
    }
}
```

### Pattern 3: Multiple System Dependencies

```csharp
public class PathfindingSystem : SystemBase
{
    private readonly SpatialHashSystem _spatialHash;
    private readonly CollisionSystem _collision;
    private readonly ILogger<PathfindingSystem>? _logger;

    public PathfindingSystem(
        World world,
        SpatialHashSystem spatialHash,
        CollisionSystem collision,
        ILogger<PathfindingSystem>? logger = null)
        : base(world)
    {
        _spatialHash = spatialHash ?? throw new ArgumentNullException(nameof(spatialHash));
        _collision = collision ?? throw new ArgumentNullException(nameof(collision));
        _logger = logger;
    }
}
```

### Pattern 4: Service Factory Registration

For services that need initialization logic:

```csharp
systemManager.RegisterService<IPathfinder>(container =>
{
    var spatialHash = container.Resolve<SpatialHashSystem>();
    var collision = container.Resolve<CollisionSystem>();
    return new AStarPathfinder(spatialHash, collision);
});
```

---

## Best Practices

### ✅ DO

1. **Use `readonly` fields for injected dependencies**
   ```csharp
   private readonly SpatialHashSystem _spatialHash;
   ```

2. **Validate required dependencies in constructor**
   ```csharp
   _spatialHash = spatialHash ?? throw new ArgumentNullException(nameof(spatialHash));
   ```

3. **Inherit from `SystemBase` for new systems**
   ```csharp
   public class MySystem : SystemBase
   ```

4. **Use nullable types for optional dependencies**
   ```csharp
   private readonly ILogger<MySystem>? _logger;
   ```

5. **Register services before systems that depend on them**
   ```csharp
   systemManager.RegisterService(spatialHashSystem);
   systemManager.RegisterSystem<MovementSystem>(); // Depends on spatialHashSystem
   ```

6. **Use descriptive parameter names matching the field**
   ```csharp
   public MySystem(World world, SpatialHashSystem spatialHash)
       : base(world)
   {
       _spatialHash = spatialHash; // Clear mapping
   }
   ```

### ❌ DON'T

1. **Don't use setter injection (old pattern)**
   ```csharp
   // ❌ Avoid this
   public void SetDependency(ISomeService service) { ... }
   ```

2. **Don't store nullable dependencies as non-nullable**
   ```csharp
   // ❌ Bad
   private readonly ILogger<MySystem> _logger; // Should be nullable if optional

   // ✅ Good
   private readonly ILogger<MySystem>? _logger;
   ```

3. **Don't perform complex logic in constructors**
   ```csharp
   // ❌ Bad
   public MySystem(World world, ...) : base(world)
   {
       // Don't do complex initialization here
       var data = LoadComplexData();
   }

   // ✅ Good - Use OnInitialized() or Initialize() override
   protected override void OnInitialized()
   {
       var data = LoadComplexData();
   }
   ```

4. **Don't resolve services manually**
   ```csharp
   // ❌ Bad
   var service = systemManager.ServiceContainer.Resolve<SomeService>();

   // ✅ Good - Use constructor injection
   public MySystem(World world, SomeService service) : base(world) { ... }
   ```

---

## Advanced Scenarios

### Scenario 1: Backward Compatibility

Support both DI and manual registration:

```csharp
public class LegacySystem : BaseSystem
{
    private readonly SpatialHashSystem? _spatialHashFromDI;
    private SpatialHashSystem? _spatialHashManual;

    // DI constructor
    public LegacySystem(World world, SpatialHashSystem spatialHash)
        : base(world)
    {
        _spatialHashFromDI = spatialHash;
    }

    // Legacy parameterless constructor
    public LegacySystem() { }

    // Legacy setter (still works)
    public void SetSpatialHashSystem(SpatialHashSystem system)
    {
        _spatialHashManual = system;
    }

    private SpatialHashSystem SpatialHash =>
        _spatialHashFromDI ?? _spatialHashManual
        ?? throw new InvalidOperationException("SpatialHashSystem not set");
}
```

### Scenario 2: Validating Dependencies Before Registration

```csharp
// Check if all dependencies can be resolved
var (canResolve, missingDeps) =
    systemManager.ValidateSystemDependencies<PathfindingSystem>();

if (!canResolve)
{
    Console.WriteLine($"Cannot register PathfindingSystem. Missing: {string.Join(", ", missingDeps)}");

    // Register missing dependencies
    foreach (var dep in missingDeps)
    {
        // Handle missing dependencies
    }
}

systemManager.RegisterSystem<PathfindingSystem>();
```

### Scenario 3: Custom System Factory Usage

```csharp
var container = systemManager.ServiceContainer;
container.RegisterSingleton(spatialHashSystem);
container.RegisterSingleton(logger);

var factory = new SystemFactory(container);

// Create system manually with DI
var customSystem = factory.CreateSystem<MyCustomSystem>();

// Register the created instance
systemManager.RegisterSystem(customSystem);
```

### Scenario 4: Transient Services

For services that should be recreated each time:

```csharp
// Register transient service (new instance per injection)
systemManager.RegisterTransientService<IPathfindingRequest>(container =>
{
    return new PathfindingRequest();
});
```

---

## Troubleshooting

### Error: "Service of type 'X' is not registered"

**Cause:** Attempting to create a system before its dependencies are registered.

**Solution:** Register services before systems:

```csharp
// ✅ Correct order
systemManager.RegisterService(spatialHashSystem);
systemManager.RegisterService(collisionSystem);
systemManager.RegisterSystem<MovementSystem>(); // Now dependencies are available
```

### Error: "Cannot resolve parameter 'world' of type 'World'"

**Cause:** Trying to inject `World` from the container.

**Solution:** `World` is injected automatically during `Initialize()`. Use the base constructor pattern:

```csharp
public MySystem(World world, SpatialHashSystem spatialHash)
    : base(world) // World is set by base class
{
    _spatialHash = spatialHash;
}
```

### Error: "No public constructors found"

**Cause:** System class has no public constructors.

**Solution:** Add a public constructor:

```csharp
public class MySystem : SystemBase
{
    // ✅ Add public constructor
    public MySystem(World world) : base(world) { }
}
```

### Error: System created but dependencies are null

**Cause:** Using the old manual registration pattern with DI-enabled systems.

**Solution:** Either use automatic registration OR manually wire dependencies:

```csharp
// ✅ Option 1: Use automatic registration
systemManager.RegisterService(spatialHashSystem);
systemManager.RegisterSystem<MovementSystem>();

// ✅ Option 2: Manual with full wiring
var movementSystem = new MovementSystem(null!, spatialHashSystem, logger);
systemManager.RegisterSystem(movementSystem);
```

### Circular Dependency Warning

**Cause:** System A depends on System B, and System B depends on System A.

**Solution:** Extract shared logic into a service:

```csharp
// ❌ Circular dependency
public class SystemA : SystemBase
{
    public SystemA(World world, SystemB systemB) : base(world) { }
}

public class SystemB : SystemBase
{
    public SystemB(World world, SystemA systemA) : base(world) { }
}

// ✅ Extract shared service
public interface ISharedService { }

public class SystemA : SystemBase
{
    public SystemA(World world, ISharedService shared) : base(world) { }
}

public class SystemB : SystemBase
{
    public SystemB(World world, ISharedService shared) : base(world) { }
}
```

---

## Testing with DI

### Unit Testing Example

```csharp
[Test]
public void MovementSystem_ShouldMoveEntity_WhenValidRequest()
{
    // Arrange
    var world = World.Create();
    var spatialHash = new SpatialHashSystem();
    var logger = new Mock<ILogger<MovementSystem>>();

    // Create system with test dependencies
    var movementSystem = new MovementSystem(world, spatialHash, logger.Object);
    movementSystem.Initialize(world);

    // Act
    movementSystem.Update(world, 0.016f);

    // Assert
    // ... verify behavior
}
```

### Integration Testing

```csharp
[Test]
public void SystemManager_ShouldResolveAllDependencies()
{
    // Arrange
    var systemManager = new SystemManager();

    // Register services
    systemManager.RegisterService(new SpatialHashSystem());
    systemManager.RegisterService(new CollisionSystem());

    // Validate dependencies
    var (canResolve, missing) = systemManager.ValidateSystemDependencies<MovementSystem>();

    // Assert
    Assert.IsTrue(canResolve, $"Missing dependencies: {string.Join(", ", missing)}");

    // Register and verify
    systemManager.RegisterSystem<MovementSystem>();
    Assert.AreEqual(1, systemManager.SystemCount);
}
```

---

## Summary

The dependency injection system provides:

- ✅ **Automatic wiring** - No more manual `Set*()` methods
- ✅ **Type safety** - Dependencies validated at registration time
- ✅ **Testability** - Easy to inject mocks for unit testing
- ✅ **Maintainability** - Clear dependency graphs in constructor signatures
- ✅ **Backward compatibility** - Old manual registration still works

**Next Steps:**
1. Identify systems with manual dependency wiring
2. Convert to constructor injection pattern
3. Register services before systems
4. Test and validate

For questions or issues, refer to the architecture documentation or create an issue.
