# Flyweight Pattern Implementation for NPC Behaviors

## Critical Bug Fixed

**Problem**: ALL NPCs were sharing ONE script instance, causing state corruption.

**Example of the Bug**:
```csharp
// BEFORE (BROKEN):
// PatrolBehaviorScript has instance fields
class PatrolBehaviorScript {
    int currentWaypoint = 0;  // SHARED BY ALL NPCs!
    float waitTimer = 0f;      // SHARED BY ALL NPCs!
}

// Result: NPC1 moves -> currentWaypoint = 1
//         NPC2 also thinks it's at waypoint 1!
//         All NPCs share the same state = CORRUPTION
```

## Solution: Flyweight Pattern

The flyweight pattern separates:
- **Intrinsic state** (shared logic) - `IBehaviorLogic`
- **Extrinsic state** (per-entity data) - State components

### Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    TypeRegistry                              │
│  ┌────────────────────────────────────────────────────┐     │
│  │  "patrol" -> PatrolBehaviorLogic (SINGLETON)       │     │
│  │  "guard"  -> GuardBehaviorLogic  (SINGLETON)       │     │
│  │  "chase"  -> ChaseBehaviorLogic  (SINGLETON)       │     │
│  └────────────────────────────────────────────────────┘     │
└─────────────────────────────────────────────────────────────┘
                            │
                            │ GetBehaviorLogic()
                            ▼
┌─────────────────────────────────────────────────────────────┐
│              NpcBehaviorSystem.Update()                      │
│                                                              │
│  For each NPC entity:                                        │
│    1. Get SHARED logic: PatrolBehaviorLogic                 │
│    2. Pass entity to logic.OnTick(world, entity, dt)        │
│    3. Logic reads/writes PER-ENTITY state components        │
└─────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────┐
│                     ECS World                                │
│                                                              │
│  Entity: NPC1                    Entity: NPC2               │
│  ├─ PatrolState (UNIQUE)         ├─ PatrolState (UNIQUE)   │
│  │  └─ currentWaypoint: 0        │  └─ currentWaypoint: 2  │
│  │  └─ waitTimer: 1.0f           │  └─ waitTimer: 0.3f     │
│  ├─ Position (10, 5)             ├─ Position (20, 15)      │
│  └─ PathComponent                └─ PathComponent          │
│                                                              │
│  Entity: NPC3                                               │
│  ├─ PatrolState (UNIQUE)                                    │
│  │  └─ currentWaypoint: 1                                   │
│  │  └─ waitTimer: 0.5f                                      │
│  ├─ Position (5, 5)                                         │
│  └─ PathComponent                                           │
└─────────────────────────────────────────────────────────────┘
```

### Key Components

#### 1. IBehaviorLogic Interface
```csharp
// PokeSharp.Core/Behaviors/IBehaviorLogic.cs
public interface IBehaviorLogic
{
    void OnTick(World world, Entity entity, float deltaTime);
    void OnActivated(World world, Entity entity);
    void OnDeactivated(World world, Entity entity);
}
```

**Rules**:
- ✅ NO instance fields (completely stateless)
- ✅ Singleton per behavior type
- ✅ Shared across ALL entities of that type

#### 2. State Components
```csharp
// PokeSharp.Core/Components/BehaviorStates.cs
public struct PatrolState  // One instance PER ENTITY
{
    public int CurrentWaypoint;
    public float WaitTimer;
    public float WaitDuration;
    public float Speed;
    public bool IsWaiting;
}
```

**Rules**:
- ✅ Struct (value type) for performance
- ✅ One instance per entity
- ✅ Contains ALL mutable state
- ✅ Zero shared state between entities

#### 3. PatrolBehaviorLogic Implementation
```csharp
// PokeSharp.Scripting/BehaviorLogic/PatrolBehaviorLogic.cs
public class PatrolBehaviorLogic : IBehaviorLogic
{
    // NO INSTANCE FIELDS! Completely stateless!

    public void OnTick(World world, Entity entity, float deltaTime)
    {
        // Get per-entity state
        ref var state = ref world.Get<PatrolState>(entity);
        ref var position = ref world.Get<Position>(entity);
        ref var path = ref world.Get<PathComponent>(entity);

        // Update state
        state.WaitTimer -= deltaTime;
        if (state.WaitTimer <= 0)
        {
            state.CurrentWaypoint = (state.CurrentWaypoint + 1) % path.Waypoints.Count;
            state.WaitTimer = state.WaitDuration;
        }

        // Move entity
        var target = path.Waypoints[state.CurrentWaypoint];
        MoveToward(ref position, target);
    }

    public void OnActivated(World world, Entity entity)
    {
        // Initialize per-entity state
        var state = new PatrolState
        {
            CurrentWaypoint = 0,
            WaitTimer = 1.0f,
            WaitDuration = 1.0f,
            Speed = 4.0f,
            IsWaiting = false
        };
        world.Add(entity, state);
    }

    public void OnDeactivated(World world, Entity entity)
    {
        // Cleanup per-entity state
        if (world.Has<PatrolState>(entity))
        {
            world.Remove<PatrolState>(entity);
        }
    }
}
```

### 4. TypeRegistry Updates
```csharp
// Register flyweight logic (PREFERRED)
registry.RegisterBehaviorLogic("patrol", new PatrolBehaviorLogic());

// Get flyweight logic
var logic = registry.GetBehaviorLogic("patrol");

// Legacy script support (backward compatibility)
#pragma warning disable CS0618
var legacyScript = registry.GetBehavior("old_behavior");
#pragma warning restore CS0618
```

### 5. NpcBehaviorSystem Updates
```csharp
// Check for flyweight logic first (PREFERRED)
var behaviorLogic = _behaviorRegistry.GetBehaviorLogic(behavior.BehaviorTypeId);
if (behaviorLogic != null)
{
    // Use flyweight pattern
    if (!behavior.IsInitialized)
    {
        behaviorLogic.OnActivated(world, entity);
        behavior.IsInitialized = true;
    }

    behaviorLogic.OnTick(world, entity, deltaTime);
    return;
}

// Fall back to legacy scripts for backward compatibility
// ...
```

## Migration Guide

### Converting Existing Behaviors

**BEFORE (Legacy Script)**:
```csharp
public class PatrolScript : TypeScriptBase
{
    private int currentWaypoint = 0;  // ❌ Instance field
    private float waitTimer = 0f;     // ❌ Instance field

    public override void OnTick(float deltaTime)
    {
        // Access entity via this.Entity
        waitTimer -= deltaTime;
        // ...
    }
}
```

**AFTER (Flyweight Logic)**:
```csharp
public class PatrolBehaviorLogic : IBehaviorLogic
{
    // ✅ NO instance fields!

    public void OnTick(World world, Entity entity, float deltaTime)
    {
        // Get per-entity state
        ref var state = ref world.Get<PatrolState>(entity);

        // Update per-entity state
        state.WaitTimer -= deltaTime;
        // ...
    }

    public void OnActivated(World world, Entity entity)
    {
        // Create per-entity state component
        world.Add(entity, new PatrolState { /* ... */ });
    }

    public void OnDeactivated(World world, Entity entity)
    {
        world.Remove<PatrolState>(entity);
    }
}
```

**AFTER (State Component)**:
```csharp
public struct PatrolState
{
    public int CurrentWaypoint;
    public float WaitTimer;
    // ... all mutable state
}
```

### Step-by-Step Migration

1. **Identify mutable state** in your script class
2. **Create a state component** struct with those fields
3. **Implement IBehaviorLogic** with zero instance fields
4. **Move state initialization** to `OnActivated`
5. **Access state** via `world.Get<TState>(entity)` in `OnTick`
6. **Register** with `RegisterBehaviorLogic` instead of `RegisterBehaviorScript`
7. **Test** with multiple NPCs to verify independence

## Testing

### Critical Test: Multiple NPCs with Same Behavior

```csharp
[Fact]
public void MultipleNpcs_WithPatrolBehavior_HaveIndependentState()
{
    // Create 3 NPCs with patrol behavior
    var npc1 = world.Create(/*...*/);
    var npc2 = world.Create(/*...*/);
    var npc3 = world.Create(/*...*/);

    // Update system
    system.Update(world, deltaTime);

    // Assert: Each NPC has independent state
    var state1 = world.Get<PatrolState>(npc1);
    var state2 = world.Get<PatrolState>(npc2);
    var state3 = world.Get<PatrolState>(npc3);

    // Verify no state interference
    Assert.NotEqual(state1.CurrentWaypoint, state2.CurrentWaypoint);
}
```

## Performance Benefits

1. **Memory**: Logic instance shared (1 instance for 1000 NPCs)
2. **Cache**: State components are contiguous in memory (cache-friendly)
3. **Scalability**: O(1) logic lookup, linear state access
4. **Safety**: Zero risk of state corruption between entities

## Backward Compatibility

- Legacy `TypeScriptBase` scripts still work
- Warning logged when using legacy path
- Gradual migration supported
- No breaking changes to existing code

## Best Practices

1. ✅ **ALWAYS use IBehaviorLogic for new behaviors**
2. ✅ **NEVER add instance fields to IBehaviorLogic implementations**
3. ✅ **Store ALL mutable state in state components**
4. ✅ **Use struct for state components (value type)**
5. ✅ **Test with multiple entities of same type**
6. ✅ **Cleanup state in OnDeactivated**

## Related Files

- `/PokeSharp/PokeSharp.Core/Behaviors/IBehaviorLogic.cs`
- `/PokeSharp/PokeSharp.Core/Components/BehaviorStates.cs`
- `/PokeSharp/PokeSharp.Core/Types/TypeRegistry.cs`
- `/PokeSharp/PokeSharp.Game/Systems/NpcBehaviorSystem.cs`
- `/PokeSharp/PokeSharp.Scripting/BehaviorLogic/PatrolBehaviorLogic.cs`
- `/PokeSharp/tests/BehaviorFlyweightPatternTest.cs`

## References

- [Flyweight Pattern (GoF)](https://refactoring.guru/design-patterns/flyweight)
- [ECS Architecture](https://github.com/SanderMertens/ecs-faq)
- [Arch ECS Documentation](https://github.com/genaray/Arch)
