# System Coupling Analysis - PokeSharp

**Analysis Date**: 2025-12-02
**Analyst**: System-Analyst Agent
**Hive Mind Swarm**: swarm-1764694320645-cswhxppkf

---

## Executive Summary

This analysis examines the current system coupling in PokeSharp's ECS architecture, identifying tight coupling points and opportunities for event-driven decoupling to improve modularity, testability, and extensibility.

### Key Findings

1. **Tight Coupling Detected**: MovementSystem → CollisionService → TileBehaviorSystem
2. **Service-Based Architecture**: Current design uses direct service injection
3. **Manual Coordination**: Systems manually call each other through interfaces
4. **Limited Extensibility**: Mod injection points require modifying core systems
5. **Performance Optimizations Present**: Already optimized for single-pass queries

### Recommendation

Implement a hybrid event-driven architecture that maintains performance while enabling:
- Decoupled system communication
- Dynamic mod/script injection points
- Better testability and maintainability
- Preserved zero-allocation optimizations

---

## Current System Architecture

### System Dependency Graph

```
Input Systems (Priority 0-20)
    ↓
SpatialHashSystem (Priority 25) ← ISpatialQuery
    ↓
TileBehaviorSystem (Priority 50) ← ITileBehaviorSystem
    ↓
MovementSystem (Priority 90) ← uses ICollisionService
    ↓
CollisionService (Service) ← uses ISpatialQuery, ITileBehaviorSystem
    ↓
WarpSystem (Priority 110)
    ↓
MapStreamingSystem (Priority 100+)
    ↓
SpriteAnimationSystem (Priority 875)
    ↓
RenderingSystem (Priority 1000)
```

### Priority Ordering

| System                 | Priority | Type     | Dependencies                              |
|------------------------|----------|----------|-------------------------------------------|
| Input Systems          | 0-20     | System   | None                                      |
| SpatialHashSystem      | 25       | System   | World queries                             |
| TileBehaviorSystem     | 50       | System   | IScriptingApiProvider                     |
| MovementSystem         | 90       | System   | ICollisionService, ISpatialQuery          |
| WarpSystem             | 110      | System   | Spatial queries via components            |
| MapStreamingSystem     | 100+     | System   | Position components                       |
| SpriteAnimationSystem  | 875      | System   | SpriteLoader                              |
| RenderingSystem        | 1000     | System   | Spatial queries                           |

---

## Tight Coupling Analysis

### 1. MovementSystem → CollisionService (HIGH COUPLING)

**Current Implementation**:
```csharp
public class MovementSystem : SystemBase, IUpdateSystem
{
    private readonly ICollisionService _collisionService;
    private readonly ISpatialQuery? _spatialQuery;
    private ITileBehaviorSystem? _tileBehaviorSystem;

    public MovementSystem(
        ICollisionService collisionService,
        ISpatialQuery? spatialQuery = null,
        ILogger<MovementSystem>? logger = null)
    {
        _collisionService = collisionService
            ?? throw new ArgumentNullException(nameof(collisionService));
        _spatialQuery = spatialQuery;
        _logger = logger;
    }

    private void TryStartMovement(...)
    {
        // Direct service call - tight coupling
        (bool isJumpTile, Direction allowedJumpDir, bool isTargetWalkable) =
            _collisionService.GetTileCollisionInfo(
                position.MapId,
                targetX,
                targetY,
                entityElevation,
                direction
            );
    }
}
```

**Coupling Issues**:
- MovementSystem has direct dependency on ICollisionService
- Must be initialized with collision service instance
- Changes to collision logic require MovementSystem recompilation
- Cannot intercept or modify collision checks without modifying service
- Difficult to add mod injection points for custom collision logic

**Impact**:
- **Modularity**: Medium - Interface provides some abstraction
- **Testability**: Good - Can mock ICollisionService
- **Extensibility**: Poor - Mods cannot intercept collision checks
- **Performance**: Excellent - Direct method calls, zero allocation

---

### 2. CollisionService → TileBehaviorSystem (HIGH COUPLING)

**Current Implementation**:
```csharp
public class CollisionService : ICollisionService
{
    private readonly ISpatialQuery _spatialQuery;
    private ITileBehaviorSystem? _tileBehaviorSystem;
    private World? _world;

    public void SetTileBehaviorSystem(ITileBehaviorSystem tileBehaviorSystem)
    {
        _tileBehaviorSystem = tileBehaviorSystem;
    }

    public (bool, Direction, bool) GetTileCollisionInfo(...)
    {
        // Direct system call within service
        if (_tileBehaviorSystem != null && _world != null && entity.Has<TileBehavior>())
        {
            Direction jumpDir = _tileBehaviorSystem.GetJumpDirection(
                _world,
                entity,
                tileFromDirection
            );

            if (_tileBehaviorSystem.IsMovementBlocked(...))
            {
                isWalkable = false;
            }
        }
    }
}
```

**Coupling Issues**:
- Service depends on system reference (architectural anti-pattern)
- Requires manual setter injection after initialization
- Service must be aware of TileBehaviorSystem existence
- Cannot add additional behavior systems without modifying CollisionService
- World reference stored in service (violates separation of concerns)

**Impact**:
- **Modularity**: Poor - Service depends on specific system
- **Testability**: Fair - Requires complex test setup
- **Extensibility**: Poor - Cannot add new behavior systems dynamically
- **Performance**: Excellent - Direct method calls

---

### 3. MovementSystem → TileBehaviorSystem (MEDIUM COUPLING)

**Current Implementation**:
```csharp
public class MovementSystem : SystemBase, IUpdateSystem
{
    private ITileBehaviorSystem? _tileBehaviorSystem;

    public void SetTileBehaviorSystem(ITileBehaviorSystem tileBehaviorSystem)
    {
        _tileBehaviorSystem = tileBehaviorSystem;
    }

    private void TryStartMovement(...)
    {
        // Check for forced movement from current tile
        if (_tileBehaviorSystem != null && _spatialQuery != null)
        {
            IReadOnlyList<Entity> currentTileEntities = _spatialQuery.GetEntitiesAt(
                position.MapId,
                position.X,
                position.Y
            );

            foreach (Entity tileEntity in currentTileEntities)
            {
                if (tileEntity.Has<TileBehavior>())
                {
                    Direction forcedDir = _tileBehaviorSystem.GetForcedMovement(
                        world,
                        tileEntity,
                        direction
                    );
                }
            }
        }
    }
}
```

**Coupling Issues**:
- MovementSystem queries spatial hash directly
- Manual iteration over tile entities
- Direct calls to behavior system
- Cannot intercept or modify forced movement logic
- Mods cannot add custom movement modifiers

**Impact**:
- **Modularity**: Fair - Optional dependency (nullable)
- **Testability**: Good - Can test with/without behavior system
- **Extensibility**: Poor - Hard-coded behavior checks
- **Performance**: Good - Efficient spatial queries

---

### 4. Systems → ISpatialQuery (LOW COUPLING)

**Current Implementation**:
```csharp
public interface ISpatialQuery
{
    IReadOnlyList<Entity> GetEntitiesAt(int mapId, int x, int y);
    IReadOnlyList<Entity> GetEntitiesInBounds(int mapId, Rectangle bounds);
}

// Used by: MovementSystem, CollisionService, WarpSystem, others
```

**Coupling Assessment**:
- Clean abstraction through interface
- No tight coupling to implementation
- Easy to mock for testing
- Performance optimized (pooled buffers)

**Impact**:
- **Modularity**: Excellent - Clean interface abstraction
- **Testability**: Excellent - Easy to mock
- **Extensibility**: Good - Can swap implementations
- **Performance**: Excellent - Optimized implementation

---

## Data Flow Analysis

### Movement Request Flow

```
1. Input System (Priority 0)
   └─> Creates MovementRequest component on player entity

2. SpatialHashSystem (Priority 25)
   └─> Updates spatial hash with current positions

3. TileBehaviorSystem (Priority 50)
   └─> Executes tile behaviors (OnTick)

4. MovementSystem (Priority 90)
   ├─> Queries MovementRequest components
   ├─> Calls CollisionService.GetTileCollisionInfo()
   │   ├─> CollisionService queries ISpatialQuery
   │   ├─> CollisionService calls ITileBehaviorSystem methods
   │   └─> Returns (isJumpTile, allowedJumpDir, isWalkable)
   ├─> Updates Position and GridMovement components
   └─> Marks MovementRequest.Active = false

5. WarpSystem (Priority 110)
   └─> Checks if player is on warp tile

6. SpriteAnimationSystem (Priority 875)
   └─> Updates sprite frames based on movement state

7. RenderingSystem (Priority 1000)
   └─> Renders entities at updated positions
```

### Key Observations

1. **Synchronous Flow**: All operations happen in single frame
2. **Direct Dependencies**: Systems call services/interfaces directly
3. **Manual Coordination**: Developer must ensure correct priority ordering
4. **Limited Interception**: No hooks for mods to intercept operations
5. **Component Pooling**: Optimized to avoid ECS archetype changes

---

## Performance Characteristics

### Current Optimizations

1. **Component Pooling**:
   - MovementRequest marked inactive instead of removed
   - Eliminates expensive ECS archetype transitions
   - Reduced 186ms spikes to negligible overhead

2. **Single-Pass Queries**:
   - GetTileCollisionInfo() performs ONE spatial query
   - Previously: 2-3 separate queries (6.25ms)
   - Now: Single query (~1.5ms)
   - 75% reduction in collision overhead

3. **Cached Results**:
   - Map world offset cache in MovementSystem
   - Tile size cache per map
   - Animation lookup dictionaries
   - Sprite manifest cache

4. **Zero-Allocation Patterns**:
   - Cached direction name strings
   - Pooled query result buffers
   - Cached sprite manifest keys
   - Reused entity lists

### Performance Metrics

| Operation                | Before      | After       | Improvement |
|--------------------------|-------------|-------------|-------------|
| Movement archetype change| 186ms spike | <1ms        | 99.5%       |
| Collision queries        | 6.25ms      | 1.5ms       | 76%         |
| Sprite animation         | 192KB/sec   | 0KB/sec     | 100%        |
| Direction name lookup    | 64B/call    | 0B/call     | 100%        |

---

## Extensibility Analysis

### Current Extensibility Points

1. **TileBehaviorScriptBase**:
   - Scripts can implement custom tile behaviors
   - Limited to predefined methods (IsBlockedFrom, GetForcedMovement, etc.)
   - Cannot add new behavior types without modifying base class

2. **ITileBehaviorSystem Interface**:
   - Systems can query behavior properties
   - Cannot intercept or modify behavior execution
   - Fixed set of query methods

3. **Component Data**:
   - Mods can add custom components
   - Cannot intercept component updates
   - Limited to ECS query patterns

### Extensibility Limitations

1. **No Event Hooks**:
   - Cannot intercept movement before/after execution
   - Cannot modify collision results dynamically
   - Cannot add custom validation logic

2. **Hard-Coded System Order**:
   - Priority values hard-coded
   - Cannot insert new systems between existing ones easily
   - Manual coordination required

3. **Service-Based Architecture**:
   - Services are singletons
   - Cannot chain or wrap service implementations
   - Difficult to add middleware logic

4. **Limited Script API**:
   - Scripts have fixed method signatures
   - Cannot subscribe to arbitrary events
   - Cannot communicate between scripts easily

---

## Testability Analysis

### Current Testability

**Strengths**:
- Interface-based dependencies (ICollisionService, ISpatialQuery)
- Can mock services for unit tests
- Component-based data allows isolated testing
- SystemBase provides clean initialization pattern

**Weaknesses**:
- Manual setter injection (SetTileBehaviorSystem) complicates setup
- Services depend on systems (architectural anti-pattern)
- Complex initialization order required for integration tests
- World reference stored in services (tight coupling)

### Test Complexity Matrix

| Component           | Unit Test | Integration Test | End-to-End Test |
|---------------------|-----------|------------------|-----------------|
| MovementSystem      | Medium    | High             | High            |
| CollisionService    | High      | High             | Medium          |
| TileBehaviorSystem  | Medium    | High             | High            |
| SpatialHashSystem   | Low       | Low              | Low             |

---

## Mod System Requirements

### Desired Mod Capabilities

1. **Movement Interception**:
   - Modify movement speed dynamically
   - Block movement based on custom conditions
   - Add custom movement modes (surf, fly, dive)
   - Override collision detection

2. **Tile Behavior Extension**:
   - Add new tile behavior types
   - Chain multiple behaviors per tile
   - Override built-in behaviors
   - React to movement events

3. **Collision Customization**:
   - Add custom collision layers
   - Implement one-way platforms
   - Create dynamic collision (moving platforms)
   - Elevation-based collision overrides

4. **Animation Control**:
   - Custom animation triggers
   - Override animation selection
   - Add animation events
   - Blend multiple animations

### Current Limitations for Mods

| Requirement                  | Supported | Implementation Difficulty |
|------------------------------|-----------|---------------------------|
| Custom movement modes        | Partial   | High - requires system mod|
| New tile behaviors           | Yes       | Medium - script system    |
| Collision interception       | No        | Very High - no hooks      |
| Animation event triggers     | No        | High - no event system    |
| Dynamic system injection     | No        | Very High - hard-coded    |
| Movement event listeners     | No        | High - no event system    |

---

## Architectural Anti-Patterns Detected

### 1. Service Depends on System

**Location**: CollisionService → ITileBehaviorSystem

```csharp
public class CollisionService : ICollisionService
{
    private ITileBehaviorSystem? _tileBehaviorSystem;

    public void SetTileBehaviorSystem(ITileBehaviorSystem tileBehaviorSystem)
    {
        _tileBehaviorSystem = tileBehaviorSystem;
    }
}
```

**Issue**: Services should not depend on systems. This creates circular dependencies and makes testing complex.

**Impact**: High - Violates clean architecture principles

---

### 2. Manual Setter Injection

**Location**: Multiple systems

```csharp
movementSystem.SetTileBehaviorSystem(tileBehaviorSystem);
collisionService.SetTileBehaviorSystem(tileBehaviorSystem);
collisionService.SetWorld(world);
```

**Issue**: Requires manual coordination and error-prone initialization order. Easy to forget setter calls.

**Impact**: Medium - Increases complexity and risk of initialization bugs

---

### 3. World Reference in Service

**Location**: CollisionService

```csharp
public class CollisionService : ICollisionService
{
    private World? _world;

    public void SetWorld(World world)
    {
        _world = world;
    }
}
```

**Issue**: Services should be stateless or have minimal state. Storing World reference couples service to ECS lifetime.

**Impact**: Medium - Limits service reusability and testability

---

### 4. Hard-Coded Priority Values

**Location**: All systems

```csharp
public override int Priority => 90; // Magic number
```

**Issue**: Priority values scattered across systems. No centralized priority management. Easy to create conflicts.

**Impact**: Low - Works but makes system ordering opaque

---

## Conclusion

The current architecture is **well-optimized for performance** but **tightly coupled for extensibility**. The service-based approach provides some abstraction but limits mod capabilities and creates architectural anti-patterns (services depending on systems).

### Critical Coupling Points

1. **MovementSystem → CollisionService** (High)
2. **CollisionService → TileBehaviorSystem** (High)
3. **Manual setter injection** (Medium)
4. **No event interception points** (High)

### Next Steps

1. Design event-driven architecture (see 02-event-driven-proposal.md)
2. Create dependency graphs (see 03-dependency-graphs.md)
3. Risk assessment for refactoring (see 04-refactoring-risks.md)
4. Migration strategy (see 05-migration-strategy.md)

---

**Analysis Status**: ✅ Complete
**Next Document**: 02-event-driven-proposal.md
