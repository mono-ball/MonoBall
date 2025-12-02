# System Dependencies & Coupling Analysis

**Research Date**: 2025-12-02
**Researcher**: ECS-Researcher (Hive Mind)

## Dependency Graph

### Visual Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                         ECS World (Arch)                         │
│                    (Core entity/component store)                 │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                      SystemManager                               │
│            (Coordinates system execution order)                  │
└─────────────────────────────────────────────────────────────────┘
          │                   │                   │
          ▼                   ▼                   ▼
┌──────────────────┐  ┌──────────────────┐  ┌──────────────────┐
│ MovementSystem   │  │ CollisionService │  │TileBehaviorSystem│
│  (Priority: 90)  │  │   (Service)      │  │   (System)       │
└──────────────────┘  └──────────────────┘  └──────────────────┘
          │                   │                   │
          │◄──────────────────┤                   │
          │      requires      │                   │
          │                    │                   │
          │◄───────────────────────────────────────┤
          │           optional requires            │
          │                                        │
          ▼                                        ▼
┌──────────────────┐                    ┌──────────────────┐
│  ISpatialQuery   │                    │  ScriptContext   │
│ (SpatialHash)    │                    │   (Scripting)    │
└──────────────────┘                    └──────────────────┘
          │                                        │
          │                                        ▼
          │                              ┌──────────────────┐
          │                              │   EventBus       │
          │                              │ (IEventBus)      │
          │                              └──────────────────┘
          │                                        │
          │                                        ▼
          │                              ┌──────────────────┐
          │                              │  UI/Rendering    │
          │                              │    Systems       │
          │                              └──────────────────┘
          │
          ▼
┌──────────────────────────────────────────────────────────────────┐
│                    Component Storage                              │
│  Position, GridMovement, Collision, TileBehavior, etc.           │
└──────────────────────────────────────────────────────────────────┘
```

## Detailed Dependency Analysis

### 1. MovementSystem

**Location**: `/Users/ntomsic/Documents/PokeSharp/PokeSharp.Game.Systems/Movement/MovementSystem.cs`

**Priority**: 90 (executes before MapStreaming at 100)

#### Direct Dependencies
```csharp
public MovementSystem(
    ICollisionService collisionService,      // REQUIRED
    ISpatialQuery? spatialQuery = null,      // OPTIONAL
    ILogger<MovementSystem>? logger = null   // OPTIONAL
)
```

#### Runtime Dependencies (via Setters)
```csharp
public void SetTileBehaviorSystem(ITileBehaviorSystem tileBehaviorSystem)
```

#### Component Dependencies
- **Reads**: `Position`, `GridMovement`, `Animation`, `MovementRequest`, `Elevation`, `MapInfo`, `MapWorldPosition`
- **Writes**: `Position`, `GridMovement`, `Animation`

#### Service Call Flow
```
MovementSystem.TryStartMovement()
    ├─> collisionService.GetTileCollisionInfo()    [ALWAYS]
    │       └─> spatialQuery.GetEntitiesAt()       [ALWAYS]
    │              └─> checks TileBehavior components
    │              └─> checks Collision components
    │              └─> checks Elevation components
    │
    └─> tileBehaviorSystem.GetForcedMovement()     [OPTIONAL]
    └─> tileBehaviorSystem.IsMovementBlocked()     [OPTIONAL]
    └─> tileBehaviorSystem.GetJumpDirection()      [OPTIONAL]
```

#### Coupling Score: **8/10 (HIGH)** ⚠️

**Reasons**:
1. Must know about `ICollisionService` interface
2. Must know about `ITileBehaviorSystem` interface
3. Must call specific methods on these services
4. Tightly coupled to collision logic flow
5. No event-based decoupling available
6. Difficult to add new movement modifiers

**Impact of Changes**:
- Adding new collision types → Must modify MovementSystem
- Adding new tile behaviors → Must modify MovementSystem
- Adding movement events → Must modify MovementSystem
- Adding custom movement logic → Must modify MovementSystem

### 2. CollisionService

**Location**: `/Users/ntomsic/Documents/PokeSharp/PokeSharp.Game.Systems/Movement/CollisionSystem.cs`

**Type**: Service (not a system - no per-frame execution)

#### Direct Dependencies
```csharp
public CollisionService(
    ISpatialQuery spatialQuery,                  // REQUIRED
    ILogger<CollisionService>? logger = null     // OPTIONAL
)
```

#### Runtime Dependencies (via Setters)
```csharp
public void SetWorld(World world)
public void SetTileBehaviorSystem(ITileBehaviorSystem tileBehaviorSystem)
```

#### Component Dependencies
- **Reads**: `Collision`, `Elevation`, `TileBehavior`
- **Writes**: None (read-only service)

#### Service Call Flow
```
CollisionService.GetTileCollisionInfo()
    └─> spatialQuery.GetEntitiesAt()              [ALWAYS]
            ├─> Check Elevation components
            ├─> Check TileBehavior components
            │       └─> tileBehaviorSystem.GetJumpDirection()
            │       └─> tileBehaviorSystem.IsMovementBlocked()
            └─> Check Collision components
```

#### Coupling Score: **6/10 (MEDIUM-HIGH)**

**Reasons**:
1. Requires ISpatialQuery for entity lookup
2. Optional coupling to ITileBehaviorSystem
3. Must understand tile behavior logic
4. No events for collision detection
5. Direct component access pattern

**Impact of Changes**:
- Adding new collision types → Modify CollisionService
- Adding elevation-based rules → Modify CollisionService
- Adding behavior-based collision → Modify CollisionService

### 3. TileBehaviorSystem

**Location**: Via `ITileBehaviorSystem` interface

**Component**: `/Users/ntomsic/Documents/PokeSharp/PokeSharp.Game.Components/Interfaces/ITileBehaviorSystem.cs`

#### Interface Definition
```csharp
public interface ITileBehaviorSystem
{
    bool IsMovementBlocked(World world, Entity tile, Direction from, Direction to);
    Direction GetForcedMovement(World world, Entity tile, Direction current);
    Direction GetJumpDirection(World world, Entity tile, Direction from);
    string? GetRequiredMovementMode(World world, Entity tile);
    bool AllowsRunning(World world, Entity tile);
    void OnStep(World world, Entity tile, Entity entity);
}
```

#### Component Dependencies
- **Reads**: `TileBehavior`, custom behavior state components
- **Writes**: Custom behavior state components

#### Script Integration
```
TileBehaviorSystem
    └─> Loads TileBehaviorScriptBase scripts
            └─> Executes script methods:
                    ├─> IsBlockedFrom()
                    ├─> IsBlockedTo()
                    ├─> GetForcedMovement()
                    ├─> GetJumpDirection()
                    ├─> GetRequiredMovementMode()
                    ├─> AllowsRunning()
                    └─> OnStep()
```

#### Coupling Score: **4/10 (MEDIUM-LOW)**

**Reasons**:
1. Interface provides good abstraction
2. Scripts provide decoupling for behavior logic
3. Well-defined method contracts
4. Component-based state management

**Strength**: This system demonstrates **good decoupling** through:
- Interface-based design
- Script-based extensibility
- Minimal direct dependencies

### 4. ScriptContext

**Location**: `/Users/ntomsic/Documents/PokeSharp/PokeSharp.Game.Scripting/Runtime/ScriptContext.cs`

#### Dependencies
```csharp
public ScriptContext(
    World world,                          // REQUIRED
    Entity? entity,                       // OPTIONAL (null for global scripts)
    ILogger logger,                       // REQUIRED
    IScriptingApiProvider apis            // REQUIRED
)
```

#### API Provider Facade
```csharp
public interface IScriptingApiProvider
{
    PlayerApiService Player { get; }      // Player operations
    NpcApiService Npc { get; }           // NPC control
    MapApiService Map { get; }           // Map queries
    GameStateApiService GameState { get; } // Flags/variables
    DialogueApiService Dialogue { get; }  // Dialogue display
    EffectApiService Effects { get; }     // Visual effects
}
```

#### Event Integration
```
ScriptContext.Dialogue.ShowMessage()
    └─> DialogueApiService
            └─> EventBasedDialogueSystem
                    └─> eventBus.Publish<DialogueRequestedEvent>()
                            └─> UI systems (subscribers)

ScriptContext.Effects.SpawnEffect()
    └─> EffectApiService
            └─> eventBus.Publish<EffectRequestedEvent>()
                    └─> Rendering systems (subscribers)
```

#### Coupling Score: **5/10 (MEDIUM)**

**Reasons**:
1. Facade pattern reduces coupling
2. API services provide abstraction
3. Event-based UI communication
4. Still requires World reference
5. Direct component access via GetState<T>()

**Strength**: Good use of facade pattern to reduce parameter count and coupling.

## Cross-System Communication Patterns

### Pattern 1: Direct Service Calls (Current)

```
MovementSystem ────calls────> CollisionService ────queries────> SpatialHash
                                                                      │
                                                                      └─> Components
```

**Pros**:
- Simple and direct
- Easy to trace execution
- Low overhead

**Cons**:
- Tight coupling
- Hard to extend
- No auditing/logging of calls
- Difficult to add cross-cutting concerns

### Pattern 2: Event-Based Communication (Proposed)

```
MovementSystem ────publishes────> MovementStartedEvent
                                          │
                                          ├──subscribes──> AudioSystem (play footstep)
                                          ├──subscribes──> ParticleSystem (dust effect)
                                          ├──subscribes──> AnalyticsSystem (track movement)
                                          └──subscribes──> ModSystem (custom logic)
```

**Pros**:
- Loose coupling
- Easy to extend
- Supports modding
- Cross-cutting concerns are simple

**Cons**:
- Harder to trace execution
- Potential performance overhead
- Need event debugging tools

### Pattern 3: Hybrid Approach (Recommended)

```
MovementSystem
    │
    ├─> (Direct) CollisionService.CheckCollision()   [Performance-critical]
    │
    └─> (Event) Publish(MovementStartedEvent)        [Extension points]
```

**When to use direct calls**:
- Performance-critical paths (collision detection)
- Core system dependencies (spatial queries)
- When order of execution matters

**When to use events**:
- Extension points for mods
- Cross-cutting concerns (logging, analytics)
- UI/feedback systems
- Optional system interactions

## Coupling Reduction Opportunities

### Opportunity 1: Extract Movement Validation Interface

**Current Problem**: MovementSystem directly calls multiple collision/behavior methods

**Proposed Solution**:
```csharp
public interface IMovementValidator
{
    ValidationResult ValidateMovement(
        int mapId,
        Point from,
        Point to,
        Direction direction,
        byte elevation
    );
}

// CollisionService and TileBehaviorSystem both implement aspects
// Compose validators via chain of responsibility
```

**Benefits**:
- Single validation call from MovementSystem
- Easy to add new validators
- Better testability
- Clear separation of concerns

### Opportunity 2: Add Movement Events

**Current Problem**: No way to observe or react to movement without modifying MovementSystem

**Proposed Events**:
```csharp
public record MovementStartedEvent
{
    public Entity Entity { get; init; }
    public Point From { get; init; }
    public Point To { get; init; }
    public Direction Direction { get; init; }
    public float Timestamp { get; init; }
}

public record MovementCompletedEvent
{
    public Entity Entity { get; init; }
    public Point Position { get; init; }
    public float Duration { get; init; }
    public float Timestamp { get; init; }
}

public record MovementBlockedEvent
{
    public Entity Entity { get; init; }
    public Point AttemptedPosition { get; init; }
    public BlockReason Reason { get; init; }
    public float Timestamp { get; init; }
}
```

**Benefits**:
- Mods can react to movement
- Easy to add footstep sounds, particles, analytics
- Debug systems can track movement history
- No changes to MovementSystem core logic

### Opportunity 3: Scriptable Collision Rules

**Current Problem**: Adding new collision rules requires code changes

**Proposed Solution**:
```csharp
// Allow scripts to register collision rules
public interface ICollisionRule
{
    bool IsBlocked(CollisionContext ctx);
    int Priority { get; }
}

// Scripts can provide custom rules
public class WaterSurfRule : ICollisionRule
{
    public bool IsBlocked(CollisionContext ctx)
    {
        if (ctx.Tile.Has<WaterTile>())
            return !ctx.Entity.Has<SurfAbility>();
        return false;
    }
}
```

**Benefits**:
- Collision rules in scripts/mods
- No code changes for new rules
- Easy to enable/disable rules
- Better testability

## Recommended Architecture Changes

### Phase 1: Add Events (Low Risk)

1. Add movement events (MovementStarted, MovementCompleted, MovementBlocked)
2. Add collision events (CollisionDetected, TileSteppedOn)
3. Keep existing direct calls for core logic
4. Allow systems to subscribe to events for extensions

**Impact**: Low risk, adds functionality without breaking existing code

### Phase 2: Extract Interfaces (Medium Risk)

1. Create IMovementValidator interface
2. Create ICollisionRule interface
3. Refactor CollisionService to use rule chain
4. Refactor MovementSystem to use validator interface

**Impact**: Medium risk, requires testing but improves maintainability

### Phase 3: Migrate to Arch.Event (High Risk)

1. Research Arch.Event performance characteristics
2. Create migration plan for EventBus → Arch.Event
3. Benchmark high-frequency events
4. Migrate low-frequency events first
5. Monitor performance and adjust

**Impact**: High risk, requires careful testing and performance validation

## Coupling Metrics Summary

| System | Coupling Score | Risk Level | Extensibility |
|--------|---------------|------------|---------------|
| MovementSystem | 8/10 | HIGH ⚠️ | LOW ❌ |
| CollisionService | 6/10 | MEDIUM | MEDIUM |
| TileBehaviorSystem | 4/10 | LOW ✅ | HIGH ✅ |
| ScriptContext | 5/10 | MEDIUM | HIGH ✅ |
| EventBus | 3/10 | LOW ✅ | HIGH ✅ |

**Priority for Improvement**: MovementSystem (highest coupling, lowest extensibility)

## Conclusion

The current architecture has **high coupling in movement and collision systems**, making it difficult to:
- Add new movement types (flying, swimming, teleporting)
- Implement gameplay modifiers (speed boosts, slippery terrain)
- Create mods that react to movement/collision
- Add debugging and analytics tools

**Key Recommendations**:
1. **Immediate**: Add movement events for extensibility
2. **Short-term**: Extract validation interfaces
3. **Long-term**: Migrate to Arch.Event for performance

The TileBehaviorSystem demonstrates **best practices** with its interface-based design and script extensibility - this pattern should be applied to movement and collision systems.

---

**Next Document**: ECS Event Best Practices Research
