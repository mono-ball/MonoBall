# PokeSharp Dependency Injection System

Lightweight dependency injection (DI) framework for the PokeSharp ECS architecture.

## Quick Start

```csharp
// Create system manager
var systemManager = new SystemManager(logger);

// Register shared services
systemManager.RegisterService(spatialHashSystem);
systemManager.RegisterService(collisionSystem);

// Register systems with automatic DI
systemManager.RegisterSystem<MovementSystem>();
systemManager.RegisterSystem<PathfindingSystem>();

// Initialize
systemManager.Initialize(world);
```

## Components

### ServiceContainer
Thread-safe container for managing service registrations and lifetimes.

```csharp
var container = new ServiceContainer();

// Singleton (one instance shared)
container.RegisterSingleton<IPathfinder>(pathfinder);

// Transient (new instance each time)
container.RegisterTransient<IRequest>(c => new Request());

// Resolve
var service = container.Resolve<IPathfinder>();
```

### SystemFactory
Creates system instances with automatic constructor injection.

```csharp
var factory = new SystemFactory(container);

// Create with dependency resolution
var system = factory.CreateSystem<MovementSystem>();

// Validate before creation
var (canResolve, missing) = factory.ValidateDependencies<MovementSystem>();
```

### SystemManager Integration
Enhanced `SystemManager` with built-in DI support.

```csharp
// Register services
systemManager.RegisterService(spatialHashSystem);
systemManager.RegisterService<ILogger>(c => loggerFactory.CreateLogger());

// Register systems (automatic DI)
systemManager.RegisterSystem<MovementSystem>();

// Validate dependencies
var (ok, missing) = systemManager.ValidateSystemDependencies<MySystem>();
```

## Creating DI-Enabled Systems

Inherit from `SystemBase` and declare dependencies in constructor:

```csharp
public class MySystem : SystemBase
{
    private readonly SpatialHashSystem _spatialHash;
    private readonly ILogger<MySystem>? _logger;

    public MySystem(
        World world,
        SpatialHashSystem spatialHash,
        ILogger<MySystem>? logger = null)
        : base(world)
    {
        _spatialHash = spatialHash
            ?? throw new ArgumentNullException(nameof(spatialHash));
        _logger = logger;
    }

    public override int Priority => SystemPriority.Custom;

    public override void Update(World world, float deltaTime)
    {
        EnsureInitialized();
        // Use dependencies...
    }
}
```

## Features

- ✅ **Constructor Injection**: Automatic dependency resolution
- ✅ **Type Safety**: Compile-time dependency validation
- ✅ **Thread-Safe**: Concurrent service registration and resolution
- ✅ **Backward Compatible**: Old manual registration still works
- ✅ **Zero Overhead**: No runtime reflection or allocation
- ✅ **Clear Errors**: Descriptive exception messages
- ✅ **Testable**: Easy to inject mocks

## Documentation

- [Migration Guide](/docs/DI_MIGRATION_GUIDE.md) - Complete migration instructions
- [Examples](/docs/EXAMPLES_DI_MIGRATION.md) - Code examples and patterns
- [Architecture Decision Record](/docs/ARCHITECTURE_DECISION_DI.md) - Design rationale

## API Reference

### ServiceContainer

| Method | Description |
|--------|-------------|
| `RegisterSingleton<T>(instance)` | Register singleton instance |
| `RegisterSingleton<T>(factory)` | Register singleton with factory |
| `RegisterTransient<T>(factory)` | Register transient service |
| `Resolve<T>()` | Resolve service |
| `TryResolve<T>(out service)` | Try resolve without exception |
| `IsRegistered<T>()` | Check if service is registered |

### SystemFactory

| Method | Description |
|--------|-------------|
| `CreateSystem<T>()` | Create system with DI |
| `CreateSystem(type)` | Create system by type |
| `ValidateDependencies<T>()` | Check if dependencies can be resolved |

### SystemManager (New Methods)

| Method | Description |
|--------|-------------|
| `RegisterSystem<T>()` | Register system with automatic DI |
| `RegisterService<T>(instance)` | Register singleton service |
| `RegisterService<T>(factory)` | Register singleton with factory |
| `RegisterTransientService<T>(factory)` | Register transient service |
| `ValidateSystemDependencies<T>()` | Validate system dependencies |
| `ServiceContainer` | Access underlying container |

## Best Practices

1. **Use readonly fields for injected dependencies**
   ```csharp
   private readonly SpatialHashSystem _spatialHash;
   ```

2. **Validate required dependencies in constructor**
   ```csharp
   _service = service ?? throw new ArgumentNullException(nameof(service));
   ```

3. **Use nullable types for optional dependencies**
   ```csharp
   private readonly ILogger<MySystem>? _logger;
   ```

4. **Register services before dependent systems**
   ```csharp
   systemManager.RegisterService(spatialHash);
   systemManager.RegisterSystem<MovementSystem>(); // Uses spatialHash
   ```

5. **Inherit from SystemBase for new systems**
   ```csharp
   public class MySystem : SystemBase { ... }
   ```

## Performance

- **Registration**: O(1) - Hash table lookup
- **Resolution**: O(1) for singletons, O(n) for transients (n = constructor params)
- **Memory**: Minimal overhead (dictionary storage only)
- **Runtime**: Zero allocation after initialization
- **Thread-Safety**: Lock-free reads, synchronized writes

## License

Part of the PokeSharp project. See main LICENSE file.
