# ECS Testing Infrastructure

Comprehensive testing utilities for PokeSharp's Entity Component System (ECS).

## 📁 Structure

```
tests/ECS/
├── EcsTestBase.cs              # Base class for all ECS tests
├── TestUtilities/
│   ├── EcsTestHelpers.cs       # Helper methods for entity/component testing
│   ├── ComponentFixtures.cs    # Pre-built component configurations
│   └── TestWorldFactory.cs     # Factory for creating test worlds
├── Systems/
│   └── SystemTestBase.cs       # Base class for system-specific tests
└── Components/
    └── MovementSystemTests.cs  # Example tests demonstrating usage
```

## 🚀 Quick Start

### Basic Entity Testing

```csharp
public class MyComponentTests : EcsTestBase
{
    [Fact]
    public void Entity_Should_Have_Position_Component()
    {
        // Arrange
        var entity = EcsTestHelpers.CreateEntityWithPosition(World, 10, 20);

        // Assert
        AssertHasComponent<Position>(entity);
        EcsTestHelpers.AssertPosition(World, entity, 10, 20);
    }
}
```

### System Testing

```csharp
public class MySystemTests : SystemTestBase<MovementSystem>
{
    protected override MovementSystem CreateSystem()
    {
        return new MovementSystem();
    }

    [Fact]
    public void System_Should_Process_Entities()
    {
        // Arrange
        InitializeSystem();
        var entity = EcsTestHelpers.CreateMovableEntity(World, 0, 0);

        // Act
        UpdateSystem(0.016f);

        // Assert
        AssertSystemHasProcessedEntities();
    }
}
```

## 📚 Available Utilities

### EcsTestBase

**Purpose**: Base class providing World and SystemManager setup/teardown.

**Key Features**:
- Automatic World creation and disposal
- SystemManager instance for coordinated testing
- Helper methods for running systems and checking entity counts

**Common Methods**:
- `RunSystems(deltaTime)` - Update all registered systems once
- `RunSystemsForFrames(count)` - Simulate multiple frames
- `CreateEntity()` - Create empty entity
- `GetEntityCount()` - Get total entity count
- `AssertHasComponent<T>(entity)` - Assert component exists
- `AssertDoesNotHaveComponent<T>(entity)` - Assert component missing

### EcsTestHelpers

**Purpose**: Static helper methods for common test operations.

**Entity Creation**:
- `CreateEntityWithPosition(world, x, y)` - Entity with Position
- `CreateMovableEntity(world, x, y, speed)` - Entity with Position + GridMovement
- `CreateAnimatedMovableEntity(world, x, y, animation)` - Fully featured moving entity
- `CreateTaggedEntity(world, tag)` - Entity with Tag

**Component Manipulation**:
- `AddMovementRequest(world, entity, direction)` - Request movement
- `SetPosition(world, entity, x, y)` - Update entity position
- `GetPosition(world, entity)` - Retrieve position component
- `GetGridMovement(world, entity)` - Retrieve movement component

**Query Helpers**:
- `GetEntitiesWithComponent<T>(world)` - Find all entities with component
- `GetEntitiesWithComponents<T1, T2>(world)` - Find entities with multiple components
- `CountEntitiesWithComponent<T>(world)` - Count entities with component

**Assertions**:
- `AssertPosition(world, entity, x, y)` - Assert grid position
- `AssertPixelPosition(world, entity, pixelX, pixelY)` - Assert interpolated position
- `AssertIsMoving(world, entity)` - Assert entity is moving
- `AssertIsNotMoving(world, entity)` - Assert entity is stationary
- `AssertFacingDirection(world, entity, direction)` - Assert facing direction
- `AssertAnimation(world, entity, animationName)` - Assert current animation
- `AssertTag(world, entity, tagValue)` - Assert tag value

**Performance**:
- `MeasureExecutionTime(action)` - Measure action execution time
- `CreateBulkEntities(world, count)` - Create many entities for benchmarking

**Math**:
- `ApproximatelyEqual(vector1, vector2, tolerance)` - Float comparison
- `GetDistance(world, entity1, entity2)` - Calculate distance between entities

### ComponentFixtures

**Purpose**: Pre-built component configurations for testing.

**Position Fixtures**:
- `CreatePositionAtOrigin()` - Position at (0, 0)
- `CreatePositionAt10x10()` - Position at (10, 10)
- `CreatePositionAt(x, y, mapId)` - Custom position
- `CreatePositionWithPixels(gridX, gridY, pixelX, pixelY)` - Position with pixel coords

**Movement Fixtures**:
- `CreateDefaultMovement()` - Speed 4.0
- `CreateFastMovement()` - Speed 8.0
- `CreateSlowMovement()` - Speed 2.0
- `CreateMovementInProgress(direction, progress)` - Mid-movement state
- `CreateLockedMovement()` - Movement disabled

**Request Fixtures**:
- `CreateMoveUpRequest()` - Move up request
- `CreateMoveDownRequest()` - Move down request
- `CreateMoveLeftRequest()` - Move left request
- `CreateMoveRightRequest()` - Move right request

**Animation Fixtures**:
- `CreateIdleDownAnimation()` - Idle facing down
- `CreateWalkUpAnimation()` - Walking up
- `CreateCustomAnimation(name, frame, time)` - Custom animation state

**Tag Fixtures**:
- `CreatePlayerTag()` - "Player" tag
- `CreateNpcTag()` - "NPC" tag
- `CreateEnemyTag()` - "Enemy" tag
- `CreateCustomTag(value)` - Custom tag

**NPC Fixtures**:
- `CreateWanderNpc()` - NPC that wanders randomly
- `CreatePatrolNpc(path)` - NPC that patrols waypoints
- `CreateStationaryNpc()` - Stationary NPC

**Rendering Fixtures**:
- `CreateSpriteRenderer(textureName)` - Basic sprite renderer
- `CreatePlayerSpriteRenderer()` - Player sprite configuration
- `CreateNpcSpriteRenderer()` - NPC sprite configuration

**Collision Fixtures**:
- `CreateDefaultCollider()` - 16x16 solid collider
- `CreateCustomCollider(width, height, isSolid)` - Custom collider
- `CreateTriggerCollider()` - Non-solid trigger collider

**Data Generators**:
- `GenerateRandomPositions(count, maxX, maxY, seed)` - Random positions
- `GenerateGridPositions(width, height)` - Grid of positions

### TestWorldFactory

**Purpose**: Factory for creating pre-configured test worlds.

**Basic Worlds**:
- `CreateEmptyWorld()` - Empty world
- `CreateMinimalWorld()` - Minimal world for unit tests
- `CreateWorldWithMap(mapId, width, height, tileSize)` - World with MapInfo

**System-Configured Worlds**:
- `CreateWorldWithMovementSystem()` - World + MovementSystem + SpatialHashSystem
- `CreateWorldWithCollisionSystem()` - World + CollisionSystem + SpatialHashSystem
- `CreateWorldWithAllCoreSystems()` - World with all core systems registered

**Populated Worlds**:
- `CreatePopulatedWorld(entityCount)` - World with test entities
- `CreatePerformanceTestWorld(entityCount)` - World for benchmarking
- `CreateWorldWithMultipleMaps(mapCount)` - World with multiple maps

**Custom Worlds**:
- `CreateCustomWorld(configure)` - Custom world configuration
- `CreateCustomWorldWithSystems(configure)` - Custom world + systems

**Cleanup**:
- `DisposeWorld(world)` - Safely dispose single world
- `DisposeWorlds(worlds...)` - Dispose multiple worlds

### SystemTestBase<TSystem>

**Purpose**: Base class for testing specific systems.

**Setup**:
- `CreateSystem()` - Abstract method to instantiate system (override required)
- `ConfigureAdditionalSystems(manager)` - Override to add dependent systems
- `InitializeSystem()` - Initialize system directly
- `InitializeSystemViaManager()` - Initialize through SystemManager

**Update Methods**:
- `UpdateSystem(deltaTime)` - Update system once
- `UpdateSystemForFrames(frameCount, deltaTime)` - Update multiple times
- `UpdateAllSystems(deltaTime)` - Update all systems via manager
- `UpdateAllSystemsForFrames(frameCount, deltaTime)` - Update all systems multiple times

**Control**:
- `EnableSystem()` - Enable the system
- `DisableSystem()` - Disable the system
- `AssertSystemEnabled()` - Assert system is enabled
- `AssertSystemDisabled()` - Assert system is disabled

**Metrics**:
- `GetSystemMetrics()` - Get performance metrics
- `AssertSystemHasProcessedEntities()` - Assert system ran
- `AssertSystemPerformance(maxMs)` - Assert performance within limits

**Utilities**:
- `CreateTestEntity()` - Create entity in test world
- `GetEntityCount()` - Get entity count

## 📝 Common Testing Patterns

### Testing Component Addition

```csharp
[Fact]
public void Should_Add_Component_To_Entity()
{
    // Arrange
    var entity = CreateEntity();

    // Act
    World.Add(entity, ComponentFixtures.CreatePositionAtOrigin());

    // Assert
    AssertHasComponent<Position>(entity);
}
```

### Testing Movement

```csharp
[Fact]
public void Should_Move_Entity_Right()
{
    // Arrange
    var entity = EcsTestHelpers.CreateMovableEntity(World, 0, 0);
    EcsTestHelpers.AddMovementRequest(World, entity, Direction.Right);

    // Act
    RunSystemsForFrames(20);

    // Assert
    EcsTestHelpers.AssertPosition(World, entity, 1, 0);
}
```

### Testing System Dependencies

```csharp
public class MySystemTests : SystemTestBase<MySystem>
{
    protected override void ConfigureAdditionalSystems(SystemManager manager)
    {
        manager.RegisterSystem(new DependencySystem());
    }

    [Fact]
    public void Should_Work_With_Dependencies()
    {
        InitializeSystemViaManager();
        // Test code here
    }
}
```

### Performance Testing

```csharp
[Fact]
public void Should_Handle_Many_Entities_Efficiently()
{
    // Arrange
    var (world, manager, entities) = TestWorldFactory.CreatePerformanceTestWorld(10000);

    // Act
    var executionTime = EcsTestHelpers.MeasureExecutionTime(() =>
    {
        manager.Update(world, 0.016f);
    });

    // Assert
    Assert.True(executionTime < 16.0, "Should complete within frame budget");

    // Cleanup
    TestWorldFactory.DisposeWorld(world);
}
```

### Testing Animation State

```csharp
[Fact]
public void Should_Change_Animation_While_Moving()
{
    // Arrange
    var entity = EcsTestHelpers.CreateAnimatedMovableEntity(World, 0, 0, "idle_down");
    EcsTestHelpers.AddMovementRequest(World, entity, Direction.Up);

    // Act
    RunSystems(0.016f);

    // Assert
    EcsTestHelpers.AssertAnimation(World, entity, "walk_up");
    EcsTestHelpers.AssertIsMoving(World, entity);
}
```

## 🎯 Best Practices

1. **Use TestWorldFactory**: Create pre-configured worlds instead of manual setup
2. **Leverage Fixtures**: Use ComponentFixtures for consistent test data
3. **Test in Isolation**: Each test should create its own entities
4. **Clean Up**: EcsTestBase handles disposal automatically
5. **Use Assertions**: Prefer EcsTestHelpers assertions over manual checks
6. **Test Edge Cases**: Use fixtures to generate edge case data
7. **Performance Test**: Use MeasureExecutionTime for benchmarks
8. **Document Tests**: Add clear comments explaining test intent

## 🔧 Extending the Infrastructure

### Adding New Helpers

Add to `EcsTestHelpers.cs`:

```csharp
public static Entity CreateCustomEntity(World world, /* params */)
{
    // Your implementation
}
```

### Adding New Fixtures

Add to `ComponentFixtures.cs`:

```csharp
public static MyComponent CreateMyComponentFixture()
{
    return new MyComponent { /* ... */ };
}
```

### Adding New World Configurations

Add to `TestWorldFactory.cs`:

```csharp
public static World CreateWorldForMyScenario()
{
    var world = World.Create();
    // Configure world
    return world;
}
```

## 📊 Running Tests

```bash
# Run all ECS tests
dotnet test --filter "FullyQualifiedName~PokeSharp.Tests.ECS"

# Run specific test class
dotnet test --filter "FullyQualifiedName~MovementSystemTests"

# Run with detailed output
dotnet test --verbosity detailed
```

## 🐛 Troubleshooting

**Problem**: Tests fail with "World not initialized"
**Solution**: Call `InitializeSystem()` or use `RunSystems()` which auto-initializes

**Problem**: System dependencies not found
**Solution**: Override `ConfigureAdditionalSystems()` in SystemTestBase

**Problem**: Position assertions failing due to floating point
**Solution**: Use `AssertPixelPosition()` with tolerance parameter

**Problem**: Memory leaks in tests
**Solution**: Ensure you inherit from `EcsTestBase` or call `Dispose()` on World

## 📖 Additional Resources

- See `MovementSystemTests.cs` for complete examples
- Check inline XML documentation for detailed method descriptions
- Refer to Arch ECS documentation: https://github.com/genaray/Arch
