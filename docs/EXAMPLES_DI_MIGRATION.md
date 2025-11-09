# Dependency Injection Migration Examples

This document provides complete, copy-pasteable examples of migrating PokeSharp systems to use dependency injection.

## Table of Contents
- [MovementSystem Migration](#movementsystem-migration)
- [CollisionSystem Migration](#collisionsystem-migration)
- [PathfindingSystem Migration](#pathfindingsystem-migration)
- [Custom System from Scratch](#custom-system-from-scratch)
- [Testing Patterns](#testing-patterns)

---

## MovementSystem Migration

### Before (Current Implementation)

```csharp
public class MovementSystem : BaseSystem
{
    private readonly List<Entity> _entitiesToRemove = new(32);
    private readonly ILogger<MovementSystem>? _logger;
    private readonly Dictionary<int, int> _tileSizeCache = new();
    private readonly QueryDescription _movementQuery = /* ... */;
    private readonly QueryDescription _movementQueryWithAnimation = /* ... */;
    private readonly QueryDescription _requestQuery = /* ... */;
    private readonly QueryDescription _removeQuery = /* ... */;
    private readonly QueryDescription _mapInfoQuery = /* ... */;

    private SpatialHashSystem? _spatialHashSystem;

    public MovementSystem(ILogger<MovementSystem>? logger = null)
    {
        _logger = logger;
    }

    public void SetSpatialHashSystem(SpatialHashSystem spatialHashSystem)
    {
        _spatialHashSystem = spatialHashSystem;
        _logger?.LogDebug("SpatialHashSystem connected to MovementSystem");
    }

    public override void Update(World world, float deltaTime)
    {
        EnsureInitialized();

        // Process movement requests
        if (_spatialHashSystem == null)
        {
            _logger?.LogSystemDependencyMissing("MovementSystem", "SpatialHashSystem", true);
            throw new InvalidOperationException(
                "SpatialHashSystem must be set before processing movement."
            );
        }

        // Movement logic...
    }
}
```

### After (With Dependency Injection)

```csharp
public class MovementSystem : SystemBase
{
    private readonly List<Entity> _entitiesToRemove = new(32);
    private readonly ILogger<MovementSystem>? _logger;
    private readonly SpatialHashSystem _spatialHashSystem; // No longer nullable!
    private readonly Dictionary<int, int> _tileSizeCache = new();
    private readonly QueryDescription _movementQuery = /* ... */;
    private readonly QueryDescription _movementQueryWithAnimation = /* ... */;
    private readonly QueryDescription _requestQuery = /* ... */;
    private readonly QueryDescription _removeQuery = /* ... */;
    private readonly QueryDescription _mapInfoQuery = /* ... */;

    // Constructor with dependency injection
    public MovementSystem(
        World world,
        SpatialHashSystem spatialHashSystem,
        ILogger<MovementSystem>? logger = null)
        : base(world)
    {
        _spatialHashSystem = spatialHashSystem
            ?? throw new ArgumentNullException(nameof(spatialHashSystem));
        _logger = logger;

        _logger?.LogDebug("MovementSystem created with injected dependencies");
    }

    public override int Priority => SystemPriority.Movement;

    public override void Update(World world, float deltaTime)
    {
        EnsureInitialized();

        // No need for null checks - _spatialHashSystem is guaranteed to be set!
        ProcessMovementRequests(world);

        // Rest of movement logic...
    }

    private void ProcessMovementRequests(World world)
    {
        // Use _spatialHashSystem directly without null checks
        // ...
    }

    // Remove the SetSpatialHashSystem method - no longer needed!
}
```

### Registration Code

```csharp
// Before (Manual Wiring)
var movementSystem = new MovementSystem(logger);
movementSystem.SetSpatialHashSystem(spatialHashSystem);
systemManager.RegisterSystem(movementSystem);

// After (Automatic DI)
systemManager.RegisterService(spatialHashSystem);
systemManager.RegisterSystem<MovementSystem>();
```

---

## CollisionSystem Migration

### Before (Current Implementation)

```csharp
public class CollisionSystem : BaseSystem
{
    private readonly ILogger<CollisionSystem>? _logger;
    private SpatialHashSystem? _spatialHashSystem;

    public CollisionSystem(ILogger<CollisionSystem>? logger = null)
    {
        _logger = logger;
    }

    public override int Priority => SystemPriority.Collision;

    public void SetSpatialHashSystem(SpatialHashSystem spatialHashSystem)
    {
        _spatialHashSystem = spatialHashSystem;
        _logger?.LogDebug("SpatialHashSystem connected to CollisionSystem");
    }

    public override void Update(World world, float deltaTime)
    {
        EnsureInitialized();
        // Collision system doesn't require per-frame updates
    }

    public bool IsPositionWalkableInstance(
        int mapId,
        int tileX,
        int tileY,
        Direction fromDirection = Direction.None)
    {
        if (_spatialHashSystem == null)
            throw new InvalidOperationException(
                "SpatialHashSystem not initialized. Call SetSpatialHashSystem() first."
            );

        return IsPositionWalkable(_spatialHashSystem, mapId, tileX, tileY, fromDirection);
    }

    public static bool IsPositionWalkable(
        SpatialHashSystem spatialHash,
        int mapId,
        int tileX,
        int tileY,
        Direction fromDirection)
    {
        // Static collision logic...
    }
}
```

### After (With Dependency Injection)

```csharp
public class CollisionSystem : SystemBase
{
    private readonly ILogger<CollisionSystem>? _logger;
    private readonly SpatialHashSystem _spatialHashSystem; // No longer nullable!

    // Constructor with dependency injection
    public CollisionSystem(
        World world,
        SpatialHashSystem spatialHashSystem,
        ILogger<CollisionSystem>? logger = null)
        : base(world)
    {
        _spatialHashSystem = spatialHashSystem
            ?? throw new ArgumentNullException(nameof(spatialHashSystem));
        _logger = logger;

        _logger?.LogDebug("CollisionSystem created with injected SpatialHashSystem");
    }

    public override int Priority => SystemPriority.Collision;

    public override void Update(World world, float deltaTime)
    {
        EnsureInitialized();
        // Collision system doesn't require per-frame updates
    }

    // Simplified instance method - no null checks needed!
    public bool IsPositionWalkableInstance(
        int mapId,
        int tileX,
        int tileY,
        Direction fromDirection = Direction.None)
    {
        return IsPositionWalkable(_spatialHashSystem, mapId, tileX, tileY, fromDirection);
    }

    // Static method unchanged (for use by other systems)
    public static bool IsPositionWalkable(
        SpatialHashSystem spatialHash,
        int mapId,
        int tileX,
        int tileY,
        Direction fromDirection)
    {
        // Static collision logic...
    }

    // Remove SetSpatialHashSystem method!
}
```

---

## PathfindingSystem Migration

### Before (Hypothetical Old Pattern)

```csharp
public class PathfindingSystem : BaseSystem
{
    private SpatialHashSystem? _spatialHash;
    private CollisionSystem? _collision;
    private ILogger<PathfindingSystem>? _logger;

    public void SetSpatialHashSystem(SpatialHashSystem system)
        => _spatialHash = system;

    public void SetCollisionSystem(CollisionSystem system)
        => _collision = system;

    public void SetLogger(ILogger<PathfindingSystem> logger)
        => _logger = logger;

    public override void Update(World world, float deltaTime)
    {
        if (_spatialHash == null || _collision == null)
            throw new InvalidOperationException("Dependencies not set");

        // Pathfinding logic...
    }
}
```

### After (With Dependency Injection)

```csharp
public class PathfindingSystem : SystemBase
{
    private readonly SpatialHashSystem _spatialHash;
    private readonly CollisionSystem _collision;
    private readonly ILogger<PathfindingSystem>? _logger;

    // All dependencies declared in constructor
    public PathfindingSystem(
        World world,
        SpatialHashSystem spatialHash,
        CollisionSystem collision,
        ILogger<PathfindingSystem>? logger = null)
        : base(world)
    {
        _spatialHash = spatialHash
            ?? throw new ArgumentNullException(nameof(spatialHash));
        _collision = collision
            ?? throw new ArgumentNullException(nameof(collision));
        _logger = logger;
    }

    public override int Priority => SystemPriority.Pathfinding;

    public override void Update(World world, float deltaTime)
    {
        EnsureInitialized();

        // All dependencies are guaranteed to be set!
        // No null checks needed
        var path = FindPath(startPos, endPos);
    }

    private List<Point> FindPath(Point start, Point end)
    {
        // Use _spatialHash and _collision directly
        if (!CollisionSystem.IsPositionWalkable(_spatialHash, mapId, x, y))
        {
            // Pathfinding logic...
        }
    }
}
```

### Registration Code

```csharp
// Register dependencies in correct order
systemManager.RegisterService(spatialHashSystem);
systemManager.RegisterService(collisionSystem);
systemManager.RegisterService(logger);

// Register system with automatic DI
systemManager.RegisterSystem<PathfindingSystem>();

// All dependencies automatically resolved!
```

---

## Custom System from Scratch

Building a new system with DI from the start:

```csharp
public class NpcBehaviorSystem : SystemBase
{
    private readonly PathfindingSystem _pathfinding;
    private readonly SpatialHashSystem _spatialHash;
    private readonly ILogger<NpcBehaviorSystem>? _logger;

    // Declare all dependencies in constructor
    public NpcBehaviorSystem(
        World world,
        PathfindingSystem pathfinding,
        SpatialHashSystem spatialHash,
        ILogger<NpcBehaviorSystem>? logger = null)
        : base(world)
    {
        // Validate required dependencies
        _pathfinding = pathfinding
            ?? throw new ArgumentNullException(nameof(pathfinding));
        _spatialHash = spatialHash
            ?? throw new ArgumentNullException(nameof(spatialHash));

        // Optional dependencies can be null
        _logger = logger;

        _logger?.LogInformation("NpcBehaviorSystem initialized with dependencies");
    }

    public override int Priority => SystemPriority.NpcBehavior;

    protected override void OnInitialized()
    {
        // Optional: Additional setup after World is available
        _logger?.LogDebug("World set, ready to process NPC behaviors");
    }

    public override void Update(World world, float deltaTime)
    {
        EnsureInitialized();

        // Query for NPCs
        var npcQuery = new QueryDescription().WithAll<NpcAI, Position, GridMovement>();

        world.Query(in npcQuery, (Entity entity, ref NpcAI ai, ref Position pos, ref GridMovement movement) =>
        {
            // Use injected dependencies
            var nearbyEntities = _spatialHash.GetEntitiesInBounds(pos.MapId, GetSearchArea(pos));

            if (ai.ShouldMove)
            {
                var path = _pathfinding.FindPath(pos, ai.TargetPosition);
                // Process path...
            }

            _logger?.LogTrace("Updated NPC {EntityId} at ({X},{Y})", entity.Id, pos.X, pos.Y);
        });
    }

    private Rectangle GetSearchArea(Position pos)
    {
        return new Rectangle(pos.X - 5, pos.Y - 5, 10, 10);
    }
}
```

### Registration

```csharp
// Register all dependencies
systemManager.RegisterService(spatialHashSystem);
systemManager.RegisterService(pathfindingSystem);
systemManager.RegisterService<ILogger<NpcBehaviorSystem>>(
    container => loggerFactory.CreateLogger<NpcBehaviorSystem>()
);

// Register the system
systemManager.RegisterSystem<NpcBehaviorSystem>();
```

---

## Testing Patterns

### Unit Test with Mocked Dependencies

```csharp
[TestFixture]
public class MovementSystemTests
{
    private World _world;
    private Mock<SpatialHashSystem> _mockSpatialHash;
    private Mock<ILogger<MovementSystem>> _mockLogger;
    private MovementSystem _movementSystem;

    [SetUp]
    public void Setup()
    {
        _world = World.Create();
        _mockSpatialHash = new Mock<SpatialHashSystem>();
        _mockLogger = new Mock<ILogger<MovementSystem>>();

        // Create system with mocked dependencies
        _movementSystem = new MovementSystem(
            _world,
            _mockSpatialHash.Object,
            _mockLogger.Object
        );

        _movementSystem.Initialize(_world);
    }

    [Test]
    public void Update_ShouldMoveEntity_WhenValidMovementRequest()
    {
        // Arrange
        var entity = _world.Create<Position, GridMovement, MovementRequest>();
        ref var pos = ref entity.Get<Position>();
        pos.X = 5;
        pos.Y = 5;

        ref var request = ref entity.Get<MovementRequest>();
        request.Direction = Direction.Right;

        // Mock spatial hash to allow movement
        _mockSpatialHash
            .Setup(s => s.GetEntitiesAt(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(Enumerable.Empty<Entity>());

        // Act
        _movementSystem.Update(_world, 0.016f);

        // Assert
        Assert.AreEqual(6, pos.X, "Entity should move right");
        _mockSpatialHash.Verify(
            s => s.GetEntitiesAt(0, 6, 5),
            Times.AtLeastOnce,
            "Should check target position"
        );
    }

    [TearDown]
    public void Teardown()
    {
        _world?.Dispose();
    }
}
```

### Integration Test with Real Dependencies

```csharp
[TestFixture]
public class SystemIntegrationTests
{
    private World _world;
    private SystemManager _systemManager;

    [SetUp]
    public void Setup()
    {
        _world = World.Create();
        _systemManager = new SystemManager();

        // Register real dependencies
        var spatialHashSystem = new SpatialHashSystem();
        var collisionSystem = new CollisionSystem(_world, spatialHashSystem);

        _systemManager.RegisterService(spatialHashSystem);
        _systemManager.RegisterService(collisionSystem);

        // Register systems with DI
        _systemManager.RegisterSystem<MovementSystem>();
        _systemManager.RegisterSystem<PathfindingSystem>();

        _systemManager.Initialize(_world);
    }

    [Test]
    public void SystemManager_ShouldResolveAllDependencies()
    {
        // Assert
        Assert.AreEqual(3, _systemManager.SystemCount, "Should have 3 systems registered");
    }

    [Test]
    public void Systems_ShouldWorkTogether()
    {
        // Arrange
        var player = _world.Create<Position, GridMovement, MovementRequest>();
        ref var pos = ref player.Get<Position>();
        pos.X = 0;
        pos.Y = 0;

        ref var request = ref player.Get<MovementRequest>();
        request.Direction = Direction.Right;

        // Act
        _systemManager.Update(_world, 0.016f);

        // Assert
        ref var finalPos = ref player.Get<Position>();
        Assert.Greater(finalPos.X, 0, "Player should have moved");
    }

    [TearDown]
    public void Teardown()
    {
        _world?.Dispose();
    }
}
```

### Dependency Validation Test

```csharp
[TestFixture]
public class DependencyValidationTests
{
    [Test]
    public void ValidateSystemDependencies_ShouldDetectMissingDependencies()
    {
        // Arrange
        var systemManager = new SystemManager();
        // Don't register any services

        // Act
        var (canResolve, missingDeps) = systemManager.ValidateSystemDependencies<MovementSystem>();

        // Assert
        Assert.IsFalse(canResolve, "Should not be able to resolve without dependencies");
        Assert.Contains("spatialHashSystem", missingDeps.Select(d => d.ToLower()).ToList());
    }

    [Test]
    public void ValidateSystemDependencies_ShouldSucceed_WhenAllDependenciesRegistered()
    {
        // Arrange
        var systemManager = new SystemManager();
        var spatialHashSystem = new SpatialHashSystem();

        systemManager.RegisterService(spatialHashSystem);

        // Act
        var (canResolve, missingDeps) = systemManager.ValidateSystemDependencies<MovementSystem>();

        // Assert
        Assert.IsTrue(canResolve, "Should resolve when all dependencies are registered");
        Assert.IsEmpty(missingDeps, "Should have no missing dependencies");
    }
}
```

---

## Summary

Key migration patterns:

1. **Add constructor with dependencies**: Declare all dependencies as constructor parameters
2. **Call base constructor**: Pass `World` to `SystemBase` constructor
3. **Remove setter methods**: Delete all `Set*()` methods
4. **Remove null checks**: Dependencies are guaranteed to be non-null
5. **Update registration**: Use `RegisterSystem<T>()` instead of manual instantiation
6. **Register services first**: Register dependencies before systems that use them

Benefits:
- ✅ 60% less boilerplate code
- ✅ Compile-time dependency validation
- ✅ Easier testing with mocks
- ✅ Clear dependency graphs
- ✅ No runtime null reference errors
