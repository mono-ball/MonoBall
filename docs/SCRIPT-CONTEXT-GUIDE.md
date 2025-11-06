# ScriptContext Comprehensive Reference

**The Unified Pattern for Entity and Global Scripts**

## Table of Contents

- [Overview](#overview)
- [Core Concepts](#core-concepts)
- [ScriptContext API](#scriptcontext-api)
- [Entity vs Global Scripts](#entity-vs-global-scripts)
- [State Management](#state-management)
- [Common Patterns](#common-patterns)
- [Error Handling](#error-handling)
- [Performance Optimization](#performance-optimization)
- [Migration Guide](#migration-guide)

## Overview

`ScriptContext` is the unified interface for all PokeSharp scripts. It provides type-safe, per-entity state management and eliminates the state corruption bugs inherent in the old instance-field pattern.

### The Problem It Solves

```csharp
// ❌ OLD BROKEN PATTERN - State corrupts between entities!
public class BrokenScript : TypeScriptBase
{
    private int _counter = 0;  // SHARED across all NPCs!

    public override void OnTick(float deltaTime)
    {
        _counter++;  // All NPCs increment the SAME counter!
        // NPC #1 sees: 1, 2, 3, 4...
        // NPC #2 sees: 5, 6, 7, 8... (continues from NPC #1!)
        // NPC #3 sees: 9, 10, 11... (continues from NPC #2!)
    }
}
```

### The Solution

```csharp
// ✅ NEW CORRECT PATTERN - Each entity has its own state!
public class CorrectScript : TypeScriptBase
{
    public override void OnActivated(ScriptContext ctx)
    {
        // Each NPC gets its own component
        ctx.World.Add(ctx.Entity.Value, new CounterState { Counter = 0 });
    }

    public override void OnTick(ScriptContext ctx, float deltaTime)
    {
        ref var state = ref ctx.GetState<CounterState>();
        state.Counter++;
        // NPC #1: 1, 2, 3, 4... (independent)
        // NPC #2: 1, 2, 3, 4... (independent)
        // NPC #3: 1, 2, 3, 4... (independent)
    }
}

public struct CounterState
{
    public int Counter;
}
```

## Core Concepts

### 1. Scripts Are Stateless Flyweights

- One script instance is shared by ALL entities using that behavior
- Scripts MUST NOT have instance fields or properties
- All per-entity state MUST be stored in ECS components

### 2. ScriptContext Provides Safe Access

- Type-safe component access
- Entity/global script detection
- Automatic null checking
- Zero-allocation reference access

### 3. State Lives in the ECS

- Components are per-entity (each NPC has its own)
- Components survive hot-reload
- Components are automatically cleaned up when entities are destroyed

## ScriptContext API

### Core Properties

```csharp
public sealed class ScriptContext
{
    // ECS world for queries and bulk operations
    public World World { get; }

    // Current entity (null for global scripts)
    public Entity? Entity { get; }

    // Logger instance for this script execution
    public ILogger Logger { get; }

    // Type detection
    public bool IsEntityScript { get; }  // true if Entity.HasValue
    public bool IsGlobalScript { get; }  // true if !Entity.HasValue
}
```

### Type-Safe Component Access

#### GetState<T>()

Gets a reference to a component (throws if missing).

```csharp
public override void OnTick(ScriptContext ctx, float deltaTime)
{
    // ✅ Returns reference for direct modification
    ref var health = ref ctx.GetState<Health>();
    health.Current -= 10;  // Modifies original component

    // ❌ Throws if component doesn't exist
    // ❌ Throws if called on global script
}
```

**When to use:**
- When you're certain the component exists
- After OnActivated() has added the component
- For performance-critical code (no null check overhead)

**Throws:**
- `InvalidOperationException` if entity is null (global script)
- `InvalidOperationException` if component doesn't exist

#### TryGetState<T>()

Safely attempts to get a component.

```csharp
public override void OnTick(ScriptContext ctx, float deltaTime)
{
    // ✅ Safe - returns false if missing
    if (ctx.TryGetState<Health>(out var health))
    {
        ctx.Logger.LogInformation("HP: {Current}/{Max}", health.Current, health.Max);
    }

    // ✅ Works on both entity and global scripts
    // ✅ Never throws
}
```

**When to use:**
- When component existence is uncertain
- For optional components
- In defensive code
- When handling multiple entity types

**Returns:**
- `true` if component exists
- `false` if component missing or global script

**Note:** Returns a **copy** of the component, not a reference. Use `GetState<T>()` if you need to modify the component.

#### GetOrAddState<T>()

Gets component if it exists, or adds it with default values.

```csharp
public override void OnTick(ScriptContext ctx, float deltaTime)
{
    // ✅ Lazy initialization - adds component if missing
    ref var timer = ref ctx.GetOrAddState<ScriptTimer>();
    timer.ElapsedSeconds += deltaTime;

    // Component is created with default struct values
    // Logs debug message when component is added
}
```

**When to use:**
- Lazy initialization of optional components
- When you don't want to check existence first
- For utility components like timers

**Throws:**
- `InvalidOperationException` if called on global script

#### HasState<T>()

Checks if component exists.

```csharp
public override void OnTick(ScriptContext ctx, float deltaTime)
{
    // ✅ Check before accessing
    if (ctx.HasState<Inventory>())
    {
        ref var inventory = ref ctx.GetState<Inventory>();
        // Work with inventory
    }

    // ✅ Safe on global scripts (returns false)
}
```

**When to use:**
- Before calling `GetState<T>()`
- To conditionally execute logic
- For type checking in generic handlers

#### RemoveState<T>()

Removes component from entity.

```csharp
public override void OnDeactivated(ScriptContext ctx)
{
    // ✅ Cleanup behavior state
    if (ctx.RemoveState<PatrolState>())
    {
        ctx.Logger.LogInformation("Removed patrol state");
    }

    // ✅ Safe if component doesn't exist
    // ✅ Safe on global scripts (returns false)
}
```

**When to use:**
- In `OnDeactivated()` to clean up behavior state
- To remove temporary status effects
- To reset entity state

**Returns:**
- `true` if component was removed
- `false` if component didn't exist or global script

### Convenience Properties

#### Position

Shortcut for `GetState<Position>()`.

```csharp
public override void OnTick(ScriptContext ctx, float deltaTime)
{
    // ✅ Convenient access
    ref var pos = ref ctx.Position;
    pos.X += 1;

    // Equivalent to:
    // ref var pos = ref ctx.GetState<Position>();
}
```

**Throws:** Same as `GetState<Position>()`

#### HasPosition

Shortcut for `HasState<Position>()`.

```csharp
if (ctx.HasPosition)
{
    ref var pos = ref ctx.Position;
    // Work with position
}
```

## Entity vs Global Scripts

### Entity Scripts

Entity scripts run **per-entity** (e.g., NPC behaviors).

```csharp
public class NpcBehavior : TypeScriptBase
{
    public override void OnActivated(ScriptContext ctx)
    {
        // ✅ ctx.IsEntityScript == true
        // ✅ ctx.Entity.HasValue == true
        // ✅ Can use GetState<T>()

        ctx.World.Add(ctx.Entity.Value, new BehaviorState());
    }

    public override void OnTick(ScriptContext ctx, float deltaTime)
    {
        // Access this entity's components
        ref var state = ref ctx.GetState<BehaviorState>();
        ref var pos = ref ctx.Position;
    }
}
```

**Use for:**
- NPC behaviors (patrol, guard, wander)
- Entity-specific logic
- Component manipulation for single entities

### Global Scripts

Global scripts run **once per frame** for the entire world (e.g., weather, events).

```csharp
public class WeatherScript : TypeScriptBase
{
    public override void OnActivated(ScriptContext ctx)
    {
        // ✅ ctx.IsGlobalScript == true
        // ✅ ctx.Entity == null
        // ❌ Cannot use GetState<T>() - no entity!

        if (ctx.IsGlobalScript)
        {
            ctx.Logger.LogInformation("Weather system activated");
        }
    }

    public override void OnTick(ScriptContext ctx, float deltaTime)
    {
        // ✅ Use World queries to process all entities
        var query = ctx.World.Query<Position, Sprite>();
        query.ForEach((ref Position pos, ref Sprite sprite) =>
        {
            // Apply rain tint to all sprites
            sprite.Tint = Color.LightBlue;
        });
    }
}
```

**Use for:**
- Weather systems
- Day/night cycles
- Global events
- World-wide effects

**Important:**
- `ctx.Entity` is `null`
- `GetState<T>()` throws exception
- Use `TryGetState<T>()` or check `IsEntityScript` first
- Use World queries to process entities

## State Management

### Defining State Components

Define components as structs in your .csx file:

```csharp
// Simple state
public struct PatrolState
{
    public int CurrentWaypoint;
    public float WaitTimer;
}

// Complex state with nested data
public struct QuestState
{
    public int QuestId;
    public QuestPhase Phase;
    public float CooldownTimer;
    public bool IsCompleted;
}

public enum QuestPhase
{
    NotStarted,
    InProgress,
    ReadyToComplete,
    Completed
}
```

### Initializing State

Always initialize state in `OnActivated()`:

```csharp
public override void OnActivated(ScriptContext ctx)
{
    // ✅ Initialize with explicit values
    ctx.World.Add(ctx.Entity.Value, new PatrolState
    {
        CurrentWaypoint = 0,
        WaitTimer = 0f
    });

    // ✅ Or use GetOrAddState for lazy init
    ref var timer = ref ctx.GetOrAddState<ScriptTimer>();
    timer.Start = ctx.DeltaTime;
}
```

### Modifying State

Use `ref` to modify components without copying:

```csharp
public override void OnTick(ScriptContext ctx, float deltaTime)
{
    // ✅ CORRECT - Modifies original
    ref var state = ref ctx.GetState<PatrolState>();
    state.WaitTimer -= deltaTime;  // Changes persist!

    // ❌ WRONG - Modifies a copy (changes lost!)
    var stateCopy = ctx.GetState<PatrolState>();
    stateCopy.WaitTimer -= deltaTime;  // No effect!
}
```

### Cleaning Up State

Remove state components in `OnDeactivated()`:

```csharp
public override void OnDeactivated(ScriptContext ctx)
{
    // ✅ Clean up behavior state
    ctx.RemoveState<PatrolState>();
    ctx.RemoveState<ScriptTimer>();

    ctx.Logger.LogInformation("Behavior deactivated and cleaned up");
}
```

## Common Patterns

### Pattern 1: Timer-Based Behavior

```csharp
public struct TimerState
{
    public float Timer;
    public int EventCount;
}

public class TimedBehavior : TypeScriptBase
{
    public override void OnActivated(ScriptContext ctx)
    {
        ctx.World.Add(ctx.Entity.Value, new TimerState
        {
            Timer = 5.0f,
            EventCount = 0
        });
    }

    public override void OnTick(ScriptContext ctx, float deltaTime)
    {
        ref var state = ref ctx.GetState<TimerState>();
        state.Timer -= deltaTime;

        if (state.Timer <= 0)
        {
            // Trigger event
            state.EventCount++;
            state.Timer = 5.0f;  // Reset timer
            ctx.Logger.LogInformation("Event #{Count} triggered", state.EventCount);
        }
    }
}
```

### Pattern 2: State Machine

```csharp
public enum BehaviorState { Idle, Patrolling, Chasing, Attacking }

public struct StateMachineState
{
    public BehaviorState Current;
    public float StateTimer;
}

public class StateMachineBehavior : TypeScriptBase
{
    public override void OnActivated(ScriptContext ctx)
    {
        ctx.World.Add(ctx.Entity.Value, new StateMachineState
        {
            Current = BehaviorState.Idle,
            StateTimer = 0f
        });
    }

    public override void OnTick(ScriptContext ctx, float deltaTime)
    {
        ref var state = ref ctx.GetState<StateMachineState>();
        state.StateTimer += deltaTime;

        switch (state.Current)
        {
            case BehaviorState.Idle:
                if (state.StateTimer >= 2.0f)
                {
                    TransitionTo(ref state, BehaviorState.Patrolling);
                }
                break;

            case BehaviorState.Patrolling:
                // Patrol logic
                break;

            case BehaviorState.Chasing:
                // Chase logic
                break;

            case BehaviorState.Attacking:
                // Attack logic
                break;
        }
    }

    private static void TransitionTo(ref StateMachineState state, BehaviorState newState)
    {
        state.Current = newState;
        state.StateTimer = 0f;
    }
}
```

### Pattern 3: Conditional Initialization

```csharp
public override void OnTick(ScriptContext ctx, float deltaTime)
{
    // ✅ Initialize on first use (lazy initialization)
    ref var cache = ref ctx.GetOrAddState<CachedData>();

    if (!cache.IsInitialized)
    {
        cache.NearbyEntities = FindNearbyEntities(ctx);
        cache.IsInitialized = true;
    }

    // Use cached data
    foreach (var entity in cache.NearbyEntities)
    {
        // Process
    }
}
```

### Pattern 4: Multi-Component Coordination

```csharp
public override void OnTick(ScriptContext ctx, float deltaTime)
{
    // ✅ Access multiple components safely
    if (!ctx.TryGetState<Health>(out var health)) return;
    if (!ctx.TryGetState<Movement>(out var movement)) return;

    // Coordinate logic across components
    if (health.Current < health.Max * 0.25f)
    {
        // Flee when low health
        movement.Speed *= 1.5f;
    }
}
```

## Error Handling

### Defensive Component Access

```csharp
public override void OnTick(ScriptContext ctx, float deltaTime)
{
    // ✅ Always check for global scripts
    if (ctx.IsGlobalScript)
    {
        ctx.Logger.LogWarning("This behavior requires an entity!");
        return;
    }

    // ✅ Use TryGetState for optional components
    if (!ctx.TryGetState<Health>(out var health))
    {
        ctx.Logger.LogDebug("Entity has no health component");
        return;
    }

    // ✅ Use HasState before GetState
    if (ctx.HasState<Inventory>())
    {
        ref var inventory = ref ctx.GetState<Inventory>();
        // Work with inventory
    }
}
```

### Logging Best Practices

```csharp
public override void OnTick(ScriptContext ctx, float deltaTime)
{
    try
    {
        ref var state = ref ctx.GetState<BehaviorState>();
        // Behavior logic
    }
    catch (InvalidOperationException ex)
    {
        ctx.Logger.LogError(ex, "Failed to access behavior state for entity {EntityId}",
            ctx.Entity?.Id ?? -1);
    }
}
```

## Performance Optimization

### Use References, Not Copies

```csharp
// ❌ BAD - Copies struct twice
var state1 = ctx.GetState<BigState>();  // Copy 1
state1.Value = 10;
var state2 = ctx.GetState<BigState>();  // Copy 2 (doesn't see changes!)

// ✅ GOOD - Zero copies, direct modification
ref var state = ref ctx.GetState<BigState>();
state.Value = 10;  // Modifies original directly
```

### Minimize Component Lookups

```csharp
// ❌ BAD - Looks up same component 100 times
for (int i = 0; i < 100; i++)
{
    ref var state = ref ctx.GetState<State>();  // Lookup each iteration!
    state.Counter++;
}

// ✅ GOOD - One lookup, 100 modifications
ref var state = ref ctx.GetState<State>();  // One lookup
for (int i = 0; i < 100; i++)
{
    state.Counter++;  // Direct modification
}
```

### Use Timers for Expensive Operations

```csharp
public struct OptimizedState
{
    public float CheckTimer;
    public CachedResult CachedData;
}

public override void OnTick(ScriptContext ctx, float deltaTime)
{
    ref var state = ref ctx.GetOrAddState<OptimizedState>();
    state.CheckTimer -= deltaTime;

    if (state.CheckTimer <= 0)
    {
        // Expensive operation once per second
        state.CachedData = PerformExpensiveCalculation();
        state.CheckTimer = 1.0f;
    }

    // Use cached result every frame
    UseCachedData(state.CachedData);
}
```

## Migration Guide

### From Old Pattern (Instance Fields)

```csharp
// ❌ OLD BROKEN PATTERN
public class OldScript : TypeScriptBase
{
    private int _waypoint = 0;        // Shared across all entities!
    private float _timer = 0f;        // Shared across all entities!

    public override void OnTick(float deltaTime)
    {
        _timer -= deltaTime;
        if (_timer <= 0)
        {
            _waypoint = (_waypoint + 1) % 4;
            _timer = 2.0f;
        }
    }
}

// ✅ NEW CORRECT PATTERN
public struct PatrolState
{
    public int Waypoint;
    public float Timer;
}

public class NewScript : TypeScriptBase
{
    public override void OnActivated(ScriptContext ctx)
    {
        ctx.World.Add(ctx.Entity.Value, new PatrolState
        {
            Waypoint = 0,
            Timer = 0f
        });
    }

    public override void OnTick(ScriptContext ctx, float deltaTime)
    {
        ref var state = ref ctx.GetState<PatrolState>();
        state.Timer -= deltaTime;

        if (state.Timer <= 0)
        {
            state.Waypoint = (state.Waypoint + 1) % 4;
            state.Timer = 2.0f;
        }
    }

    public override void OnDeactivated(ScriptContext ctx)
    {
        ctx.RemoveState<PatrolState>();
    }
}
```

### Migration Steps

1. **Identify instance fields** - Find all `private` fields in your script
2. **Create state component** - Define a struct with those fields
3. **Add OnActivated** - Initialize the component
4. **Replace field access** - Use `ctx.GetState<T>()` instead
5. **Add OnDeactivated** - Clean up the component
6. **Test with multiple entities** - Verify state is independent

## See Also

- [SCRIPTING-GUIDE.md](SCRIPTING-GUIDE.md) - Quick start and examples
- [PATTERN-COMPARISON.md](PATTERN-COMPARISON.md) - Why we changed patterns
- [TypeScriptBase.cs](../PokeSharp.Scripting/TypeScriptBase.cs) - Base class source
- [ScriptContext.cs](../PokeSharp.Scripting/ScriptContext.cs) - Context class source
