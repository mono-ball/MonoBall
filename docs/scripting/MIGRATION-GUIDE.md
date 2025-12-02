# Script Migration Guide: Legacy Bases → ScriptBase

**Version**: 1.0
**Target**: PokeSharp Phase 3.4
**Audience**: Developers and Modders

---

## 📋 Table of Contents

1. [Executive Summary](#executive-summary)
2. [Quick Start (5-Minute Migration)](#quick-start-5-minute-migration)
3. [Pattern Reference](#pattern-reference)
4. [Step-by-Step Guide](#step-by-step-guide)
5. [Before/After Examples](#beforeafter-examples)
6. [Troubleshooting](#troubleshooting)
7. [Migration Checklist](#migration-checklist)
8. [Advanced Topics](#advanced-topics)

---

## Executive Summary

### Why Migrate?

**Current Architecture** (TileBehaviorScriptBase/TypeScriptBase):
- ❌ **No Composition**: Only 1 script per tile/entity
- ❌ **Fixed API**: Can't add custom events without engine changes
- ❌ **Multiple Base Classes**: Different base class for each script type
- ❌ **Polling**: OnTick() called every frame (inefficient)
- ❌ **Instance State**: Unsafe with hot-reload and composition

**Unified Architecture** (ScriptBase):
- ✅ **Full Composition**: Multiple scripts per tile/entity
- ✅ **Extensible**: Define custom events without engine changes
- ✅ **Single Base Class**: Learn once, use everywhere
- ✅ **Event-Driven**: React only when needed (efficient)
- ✅ **Context State**: Safe with hot-reload and composition

### Benefits

| Benefit | Description | Impact |
|---------|-------------|--------|
| **Modding** | Multiple mods can affect same tile | Ecosystem growth |
| **Simplicity** | One base class to learn | Faster onboarding |
| **Performance** | Event-driven (no polling) | Better CPU usage |
| **Testability** | Mock events easily | Better code quality |
| **Extensibility** | Custom events without engine changes | Future-proof |

### Migration Effort

| Script Type | Count | Effort per Script | Total Effort |
|-------------|-------|-------------------|--------------|
| **Tile Behaviors** | ~47 | 10-15 minutes | 8-12 hours |
| **NPC Behaviors** | ~30 | 15-20 minutes | 8-10 hours |
| **Custom Scripts** | Varies | 5-20 minutes | Varies |

**Total Estimated Effort**: 16-22 hours for core scripts

---

## Quick Start (5-Minute Migration)

### Simple Tile Behavior Example

**BEFORE** (TileBehaviorScriptBase):
```csharp
// jump_south.csx
public class JumpSouthBehavior : TileBehaviorScriptBase
{
    public override bool IsBlockedFrom(ScriptContext ctx, Direction from, Direction to)
    {
        return from == Direction.South;
    }

    public override Direction GetJumpDirection(ScriptContext ctx, Direction from)
    {
        if (from == Direction.South)
            return Direction.North;
        return Direction.None;
    }
}

return new JumpSouthBehavior();
```

**AFTER** (ScriptBase):
```csharp
// jump_south.csx
public class JumpSouthBehavior : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // Block movement from south
        On<MovementStartedEvent>(evt => {
            if (evt.FromDirection == Direction.South)
                evt.PreventDefault("Cannot climb up ledge");
        });

        // Allow jumping north when coming from south
        On<MovementCompletedEvent>(evt => {
            if (evt.Direction == Direction.North)
                PerformJump(evt.Entity, Direction.North);
        });
    }

    private void PerformJump(Entity entity, Direction direction)
    {
        Context.Effects.PlayAnimation(entity, "jump");
        Context.Effects.PlaySound("jump");
    }
}

return new JumpSouthBehavior();
```

### 3-Step Process

1. **Change base class**: `TileBehaviorScriptBase` → `ScriptBase`
2. **Add RegisterEventHandlers**: Subscribe to events instead of overriding methods
3. **Convert logic**: Virtual methods → Event handlers

**Done!** Test with hot-reload.

---

## Pattern Reference

### Pattern 1: Virtual Method → Event Subscription

#### Blocking Movement

**BEFORE** (TileBehaviorScriptBase):
```csharp
public override bool IsBlockedFrom(ScriptContext ctx, Direction from, Direction to)
{
    return from == Direction.South; // Block upward movement
}
```

**AFTER** (ScriptBase):
```csharp
public override void RegisterEventHandlers(ScriptContext ctx)
{
    On<MovementStartedEvent>(evt => {
        if (evt.FromDirection == Direction.South)
            evt.PreventDefault("Cannot move up from ledge");
    });
}
```

**Key Changes**:
- `IsBlockedFrom` → `On<MovementStartedEvent>`
- Return bool → Call `evt.PreventDefault()`
- Reactive instead of query-based

---

#### Jump Direction

**BEFORE** (TileBehaviorScriptBase):
```csharp
public override Direction GetJumpDirection(ScriptContext ctx, Direction from)
{
    if (from == Direction.North)
        return Direction.South;
    return Direction.None;
}
```

**AFTER** (ScriptBase):
```csharp
On<JumpCheckEvent>(evt => {
    if (evt.FromDirection == Direction.North)
    {
        evt.JumpDirection = Direction.South;
        evt.PerformJump = true;
    }
});
```

**Key Changes**:
- `GetJumpDirection` → `On<JumpCheckEvent>`
- Return value → Set event properties
- More explicit intent

---

### Pattern 2: OnTick → Event-Driven

#### Polling (INEFFICIENT)

**BEFORE** (TypeScriptBase):
```csharp
private float timer = 0;

public override void OnTick(ScriptContext ctx, float deltaTime)
{
    // Called EVERY FRAME (60 FPS = 60 calls/second!)
    timer += deltaTime;

    if (timer >= 5.0f)
    {
        DoSomething();
        timer = 0;
    }

    // Check player proximity every frame
    if (IsPlayerNearby())
    {
        React();
    }
}
```

**Problems**:
- ❌ Runs every frame even when nothing happens
- ❌ Wastes CPU cycles
- ❌ Hard to coordinate with other scripts

---

#### Event-Driven (EFFICIENT)

**AFTER** (ScriptBase):
```csharp
public override void RegisterEventHandlers(ScriptContext ctx)
{
    // Use timer event instead of polling
    OnTimer("myTimer", TimeSpan.FromSeconds(5), () => {
        DoSomething();
    });

    // React to player movement instead of polling
    OnEntity<MovementCompletedEvent>(ctx.Player.Entity, evt => {
        if (IsNearby(evt.NewPosition))
            React();
    });
}
```

**Benefits**:
- ✅ Only runs when event fires
- ✅ Better CPU usage
- ✅ Easy to coordinate with other scripts

---

### Pattern 3: State Management

#### Instance State (UNSAFE)

**BEFORE** (TypeScriptBase):
```csharp
// Instance variables DON'T survive hot-reload!
private int counter = 0;
private bool isActive = false;
private List<Vector2> visitedPositions = new List<Vector2>();

public override void OnTick(ScriptContext ctx, float deltaTime)
{
    counter++; // LOST on hot-reload!

    if (counter > 100)
    {
        isActive = true; // LOST on hot-reload!
    }
}
```

**Problems**:
- ❌ State lost on hot-reload
- ❌ Can't share state with other scripts
- ❌ No composition (multiple scripts = multiple copies)

---

#### Context State (SAFE)

**AFTER** (ScriptBase):
```csharp
// Use context state - survives hot-reload!
public override void RegisterEventHandlers(ScriptContext ctx)
{
    OnTimer("increment", TimeSpan.FromSeconds(1), () => {
        // Get with default value
        var counter = Get<int>("counter", 0);
        counter++;
        Set("counter", counter); // SURVIVES hot-reload!

        if (counter > 100)
        {
            Set("isActive", true); // SURVIVES hot-reload!
        }
    });

    // Share state with other scripts!
    On<SomeEvent>(evt => {
        var sharedData = Get<List<Vector2>>("shared:visitedPositions", new());
        // Other scripts can access "shared:visitedPositions" too!
    });
}
```

**Benefits**:
- ✅ Survives hot-reload
- ✅ Can share between scripts (use "shared:" prefix)
- ✅ Composition-friendly

---

### Pattern 4: Configuration

#### Hardcoded Values (INFLEXIBLE)

**BEFORE** (TypeScriptBase):
```csharp
public override void OnInitialize(ScriptContext ctx)
{
    // Hardcoded values
    float speed = 1.5f;
    int maxHealth = 100;
    string npcName = "Oak";
}
```

---

#### Configurable Values (FLEXIBLE)

**AFTER** (ScriptBase):
```csharp
// Public properties (can be set from editor/JSON)
public float speed = 1.5f;
public int maxHealth = 100;
public string npcName = "Oak";

public override void Initialize(ScriptContext ctx)
{
    base.Initialize(ctx);

    // Use configured values
    var stats = new Stats { Health = maxHealth, Speed = speed };
    Set("stats", stats);
}
```

**Benefits**:
- ✅ Configurable from external sources
- ✅ No code changes needed for tweaking
- ✅ Better for modders

---

### Pattern 5: Event Publishing (Custom Events)

#### No Inter-Script Communication (ISOLATED)

**BEFORE**:
```csharp
// Scripts can't communicate!
public override void OnTick(ScriptContext ctx, float deltaTime)
{
    if (SomethingHappened())
    {
        // Other scripts don't know about this
        DoLocalThing();
    }
}
```

---

#### Custom Event Publishing (CONNECTED)

**AFTER** (ScriptBase):
```csharp
// Define custom event (can be in same file or shared file)
public class WeatherChangedEvent : IGameEvent
{
    public WeatherType NewWeather { get; init; }
    public float Intensity { get; init; }
}

public override void RegisterEventHandlers(ScriptContext ctx)
{
    OnTimer("weatherCheck", TimeSpan.FromMinutes(5), () => {
        var newWeather = DetermineWeather();

        // PUBLISH custom event - other scripts can react!
        Publish(new WeatherChangedEvent {
            NewWeather = newWeather,
            Intensity = 0.7f
        });
    });
}
```

**Other scripts can react**:
```csharp
// plant_growth.csx
public override void RegisterEventHandlers(ScriptContext ctx)
{
    // React to weather changes!
    On<WeatherChangedEvent>(evt => {
        if (evt.NewWeather == WeatherType.Rain)
            GrowFaster(evt.Intensity);
    });
}
```

**Benefits**:
- ✅ Scripts can communicate
- ✅ Loose coupling (scripts don't need to know about each other)
- ✅ Enables mod ecosystems

---

## Step-by-Step Guide

### Step 1: Identify Script Type

Determine which legacy base class your script uses:

| Legacy Base | Use Case | Common Methods |
|-------------|----------|----------------|
| **TileBehaviorScriptBase** | Tile interactions | `IsBlockedFrom`, `IsBlockedTo`, `GetJumpDirection` |
| **TypeScriptBase** | General scripts, NPCs | `OnTick`, `OnInitialize` |

---

### Step 2: Change Base Class

```diff
- public class MyScript : TileBehaviorScriptBase
+ public class MyScript : ScriptBase
```

```diff
- public class MyScript : TypeScriptBase
+ public class MyScript : ScriptBase
```

---

### Step 3: Add RegisterEventHandlers

```csharp
public override void RegisterEventHandlers(ScriptContext ctx)
{
    // Event subscriptions go here
}
```

**IMPORTANT**: This method is called ONCE when script loads. All event subscriptions must happen here.

---

### Step 4: Convert Virtual Methods to Events

Use this mapping:

| Legacy Method | Event Type | Pattern |
|---------------|------------|---------|
| `IsBlockedFrom` | `MovementStartedEvent` | Call `evt.PreventDefault()` to block |
| `IsBlockedTo` | `MovementStartedEvent` | Call `evt.PreventDefault()` to block |
| `GetJumpDirection` | `JumpCheckEvent` | Set `evt.JumpDirection` and `evt.PerformJump` |
| `OnInitialize` | `Initialize()` (override) | One-time setup |
| `OnTick` | `On<TickEvent>` or better: specific events | Prefer events over polling |

**Example Conversion**:

**BEFORE**:
```csharp
public override bool IsBlockedFrom(ScriptContext ctx, Direction from, Direction to)
{
    if (from == Direction.North)
        return true;
    return false;
}
```

**AFTER**:
```csharp
public override void RegisterEventHandlers(ScriptContext ctx)
{
    On<MovementStartedEvent>(evt => {
        if (evt.FromDirection == Direction.North)
            evt.PreventDefault("Can't climb ledge");
    });
}
```

---

### Step 5: Convert State Management

**Move instance variables to context state**:

**BEFORE**:
```csharp
private int counter = 0;

public override void OnTick(ScriptContext ctx, float deltaTime)
{
    counter++;
}
```

**AFTER**:
```csharp
public override void RegisterEventHandlers(ScriptContext ctx)
{
    On<TickEvent>(evt => {
        var counter = Get<int>("counter", 0);
        Set("counter", counter + 1);
    });
}
```

---

### Step 6: Remove OnTick Polling (If Possible)

**Try to replace with events**:

**BEFORE** (Polling):
```csharp
public override void OnTick(ScriptContext ctx, float deltaTime)
{
    // Check every frame (inefficient!)
    if (IsPlayerNearby())
    {
        TriggerBattle();
    }
}
```

**AFTER** (Event-Driven):
```csharp
public override void RegisterEventHandlers(ScriptContext ctx)
{
    // React to player movement (efficient!)
    OnEntity<MovementCompletedEvent>(Context.Player.Entity, evt => {
        if (IsNearby(evt.NewPosition))
            TriggerBattle();
    });
}
```

**If you MUST use polling** (timers, physics, etc.):
```csharp
On<TickEvent>(evt => {
    // Still works, but use sparingly
});
```

---

### Step 7: Test

1. **Hot-reload**: Save file and verify script reloads
2. **Functionality**: Test all behaviors work as before
3. **State**: Test state survives hot-reload
4. **Composition**: Test multiple scripts work together (if applicable)

```bash
# Run game and test
dotnet run

# Make changes, save, hot-reload happens
# Verify behavior is unchanged
```

---

## Before/After Examples

### Example 1: Jump Ledge (Tile Behavior)

#### BEFORE (TileBehaviorScriptBase)

```csharp
// Assets/Scripts/TileBehaviors/jump_south.csx
using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Scripting.Runtime;

/// <summary>
///     Jump south behavior.
///     Allows jumping south but blocks north movement.
/// </summary>
public class JumpSouthBehavior : TileBehaviorScriptBase
{
    public override bool IsBlockedFrom(
        ScriptContext ctx,
        Direction fromDirection,
        Direction toDirection
    )
    {
        // Block movement from north (can't climb up)
        if (fromDirection == Direction.North)
            return true;

        return false;
    }

    public override bool IsBlockedTo(ScriptContext ctx, Direction toDirection)
    {
        // Block movement to north (can't enter from south)
        if (toDirection == Direction.North)
            return true;

        return false;
    }

    public override Direction GetJumpDirection(ScriptContext ctx, Direction fromDirection)
    {
        // Allow jumping south when coming from north
        if (fromDirection == Direction.North)
            return Direction.South;

        return Direction.None;
    }
}

return new JumpSouthBehavior();
```

**Lines**: 42
**Limitations**: Only 1 script per tile, can't compose with other behaviors

---

#### AFTER (ScriptBase)

```csharp
// Assets/Scripts/Unified/jump_south.csx
using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Scripting.Runtime;

/// <summary>
///     Jump south behavior.
///     Allows jumping south but blocks north movement.
///     Now supports composition with other tile scripts!
/// </summary>
public class JumpSouthBehavior : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // Block movement from/to north (can't climb up)
        On<MovementStartedEvent>(evt => {
            if (evt.FromDirection == Direction.North || evt.ToDirection == Direction.North)
            {
                evt.PreventDefault("Can't climb up ledge");
                Context.Effects.PlaySound("bump");
            }
        });

        // Allow jumping south when coming from north
        On<MovementCompletedEvent>(evt => {
            if (evt.Direction == Direction.South && evt.FromDirection == Direction.North)
            {
                PerformJump(evt.Entity, Direction.South);

                // BONUS: Publish custom event for other scripts!
                Publish(new LedgeJumpedEvent {
                    Entity = evt.Entity,
                    TilePosition = evt.NewPosition,
                    Direction = Direction.South
                });
            }
        });
    }

    private void PerformJump(Entity entity, Direction direction)
    {
        Context.Effects.PlayAnimation(entity, "jump");
        Context.Effects.PlaySound("jump");
        Context.Effects.PlayEffect("land_dust", entity.Get<Position>().Value);
    }
}

// Custom event - other scripts can react to jumps!
public class LedgeJumpedEvent : IGameEvent, ITileEvent, IEntityEvent
{
    public Entity Entity { get; init; }
    public Vector2 TilePosition { get; init; }
    public Direction Direction { get; init; }
}

return new JumpSouthBehavior();
```

**Lines**: 54 (12 more, but with bonus features!)
**Benefits**:
- ✅ Supports composition (multiple scripts per tile)
- ✅ Publishes custom event (other mods can react)
- ✅ Better audio/visual feedback
- ✅ More maintainable

---

### Example 2: NPC Patrol (Entity Behavior)

#### BEFORE (TypeScriptBase)

```csharp
// Assets/Scripts/NPCs/patrol_basic.csx
using PokeSharp.Game.Scripting.Runtime;

public class NPCPatrol : TypeScriptBase
{
    // Instance state (LOST on hot-reload!)
    private int currentIndex = 0;
    private float waitTimer = 0;
    private bool isWaiting = false;

    private List<Vector2> patrolPoints = new List<Vector2> {
        new Vector2(5, 5),
        new Vector2(10, 5),
        new Vector2(10, 10)
    };

    public override void OnInitialize(ScriptContext ctx)
    {
        MoveToNextPoint();
    }

    public override void OnTick(ScriptContext ctx, float deltaTime)
    {
        // Poll wait timer EVERY FRAME (inefficient!)
        if (isWaiting)
        {
            waitTimer -= deltaTime;
            if (waitTimer <= 0)
            {
                isWaiting = false;
                MoveToNextPoint();
            }
        }
    }

    private void MoveToNextPoint()
    {
        currentIndex = (currentIndex + 1) % patrolPoints.Count;
        var target = patrolPoints[currentIndex];

        var npc = GetAttachedEntity();
        var movement = npc.Get<MovementComponent>();
        movement.StartMove(target);
    }

    private Entity GetAttachedEntity()
    {
        return Context.Npc.GetCurrentNPC();
    }
}

return new NPCPatrol();
```

**Problems**:
- ❌ State lost on hot-reload
- ❌ Polling every frame (inefficient)
- ❌ No event-driven logic
- ❌ Hard to extend

---

#### AFTER (ScriptBase)

```csharp
// Assets/Scripts/Unified/patrol_basic.csx
using PokeSharp.Game.Scripting.Runtime;

public class NPCPatrol : ScriptBase
{
    // Configuration (can be set externally!)
    public List<Vector2> patrolPoints = new List<Vector2> {
        new Vector2(5, 5),
        new Vector2(10, 5),
        new Vector2(10, 10)
    };

    public float waitTimeAtPoint = 2.0f;

    private Entity npcEntity;

    public override void Initialize(ScriptContext ctx)
    {
        base.Initialize(ctx);

        npcEntity = GetAttachedEntity();

        // Initialize state (survives hot-reload!)
        if (Get<int>("currentIndex", -1) == -1)
        {
            Set("currentIndex", 0);
            MoveToNextPoint();
        }
    }

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // React to movement completion (event-driven!)
        OnEntity<MovementCompletedEvent>(npcEntity, evt => {
            WaitAtPoint();
        });

        // Handle blocked movement
        OnEntity<MovementBlockedEvent>(npcEntity, evt => {
            // Skip to next point if blocked
            var currentIndex = Get<int>("currentIndex", 0);
            Set("currentIndex", (currentIndex + 1) % patrolPoints.Count);
            MoveToNextPoint();
        });
    }

    private void WaitAtPoint()
    {
        // Use timer instead of polling!
        OnTimer("waitTimer", TimeSpan.FromSeconds(waitTimeAtPoint), () => {
            MoveToNextPoint();
        }, once: true);
    }

    private void MoveToNextPoint()
    {
        // Get state (survives hot-reload!)
        var currentIndex = Get<int>("currentIndex", 0);
        currentIndex = (currentIndex + 1) % patrolPoints.Count;
        Set("currentIndex", currentIndex);

        var target = patrolPoints[currentIndex];
        var movement = npcEntity.Get<MovementComponent>();
        movement.StartMove(target);
    }

    private Entity GetAttachedEntity()
    {
        return Context.Npc.GetCurrentNPC();
    }
}

return new NPCPatrol();
```

**Benefits**:
- ✅ State survives hot-reload (`Get`/`Set`)
- ✅ Event-driven (no polling!)
- ✅ Configurable patrol points
- ✅ More maintainable
- ✅ Easy to extend (add player detection, etc.)

---

### Example 3: Ice Tile with Composition

#### BEFORE (No Composition Possible)

```csharp
// Can only have ONE script per tile
// ice_slide.csx - CANNOT coexist with other behaviors
public class IceSlide : TileBehaviorScriptBase
{
    public override void OnTick(ScriptContext ctx, float deltaTime)
    {
        // Check if player is on ice every frame
        if (IsPlayerOnIce())
        {
            ContinueSliding();
        }
    }
}
```

**Problem**: Can't add grass encounters, cracking effect, or any other behavior!

---

#### AFTER (Full Composition)

```csharp
// Script 1: Ice sliding (ice_slide.csx)
public class IceSlide : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<MovementCompletedEvent>(evt => {
            if (Context.Map.GetTileAt(evt.NewPosition)?.HasTag("ice") ?? false)
            {
                Context.Player.ContinueMovement(evt.Direction);
                Context.Effects.PlaySound("ice_slide");
            }
        });
    }
}

// Script 2: Grass encounters (tall_grass.csx) - COEXISTS with ice_slide!
public class TallGrass : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<TileSteppedOnEvent>(evt => {
            if (Context.Map.GetTileAt(evt.TilePosition)?.HasTag("grass") ?? false)
            {
                if (Random.NextDouble() < 0.1)
                    TriggerWildEncounter(evt.Entity);
            }
        });
    }
}

// Script 3: Ice crack effect (ice_crack.csx) - ALSO COEXISTS!
public class IceCrackEffect : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<TileSteppedOnEvent>(evt => {
            if (Context.Map.GetTileAt(evt.TilePosition)?.HasTag("ice") ?? false)
            {
                Context.Effects.PlayEffect("ice_crack", evt.TilePosition);

                // Custom event - other mods can react!
                Publish(new IceCrackedEvent { TilePosition = evt.TilePosition });
            }
        });
    }
}
```

**Result**: All 3 scripts work together on the same tile!
- Player slides on ice
- Grass can trigger encounters
- Ice shows crack effect
- Other mods can react to `IceCrackedEvent`

**This is the power of composition!**

---

## Troubleshooting

### Problem 1: State Lost on Hot-Reload

**Symptom**: Variables reset to 0 when script reloads.

**Cause**: Using instance variables instead of context state.

**Solution**:
```diff
- private int counter = 0;
+ // Remove instance variable

  public override void RegisterEventHandlers(ScriptContext ctx)
  {
      On<SomeEvent>(evt => {
-         counter++;
+         var counter = Get<int>("counter", 0);
+         Set("counter", counter + 1);
      });
  }
```

---

### Problem 2: Event Not Firing

**Symptom**: Handler never gets called.

**Causes**:
1. **Wrong event type**: Check you're subscribing to the correct event
2. **Not registered**: Forgot to add `On<>` in `RegisterEventHandlers`
3. **Filtering too strict**: Entity/tile filter excludes your case

**Solution**:
```csharp
// Debug: Log when event fires
On<MovementStartedEvent>(evt => {
    Console.WriteLine($"MovementStarted: {evt.Entity}, {evt.Direction}");
    // Your logic here
});
```

---

### Problem 3: Multiple Handlers Conflict

**Symptom**: Two handlers modify same event, unexpected behavior.

**Cause**: Event priority not set, handlers run in undefined order.

**Solution**: Use priority (lower = earlier):
```csharp
// High priority (runs first)
On<MovementStartedEvent>(evt => {
    // Check something first
}, priority: 100);

// Normal priority
On<MovementStartedEvent>(evt => {
    // Default handling
}, priority: 500); // Default

// Low priority (runs last)
On<MovementStartedEvent>(evt => {
    // Cleanup or logging
}, priority: 900);
```

---

### Problem 4: Can't Access Context

**Symptom**: `Context` is null in helper methods.

**Cause**: Helper methods called before `Initialize`.

**Solution**: Use `Context` property (set by base class):
```csharp
private void HelperMethod()
{
    // ✅ Use Context property
    Context.Effects.PlaySound("sound");

    // ❌ DON'T store ctx parameter
    // ctx is only valid during RegisterEventHandlers
}
```

---

### Problem 5: Performance Issues

**Symptom**: Game slows down after migration.

**Causes**:
1. **Still using OnTick**: Convert to events
2. **Too many allocations**: Reuse objects
3. **Heavy event handlers**: Optimize logic

**Solution**:
```csharp
// ❌ BAD: Allocates list every event
On<SomeEvent>(evt => {
    var list = new List<Vector2>(); // Allocates!
    // Use list...
});

// ✅ GOOD: Reuse cached list
private List<Vector2> cachedList = new();

On<SomeEvent>(evt => {
    cachedList.Clear();
    // Use cachedList...
});
```

---

### Problem 6: Script Doesn't Load

**Symptom**: Script not found, no errors.

**Causes**:
1. **Wrong file location**: Check scripts folder
2. **Compilation error**: Check console for errors
3. **Missing return statement**: Must return instance

**Solution**:
```csharp
public class MyScript : ScriptBase
{
    // ... your code ...
}

// ✅ REQUIRED: Return instance at end of file
return new MyScript();
```

---

## Migration Checklist

Use this checklist for each script you migrate:

### Pre-Migration

- [ ] Identify script type (TileBehaviorScriptBase, TypeScriptBase)
- [ ] List all virtual methods overridden
- [ ] Note any instance state variables
- [ ] Document current behavior (for testing)
- [ ] Create backup of original script

### During Migration

- [ ] Change base class to `ScriptBase`
- [ ] Add `RegisterEventHandlers(ScriptContext ctx)` method
- [ ] Convert virtual methods to event subscriptions
- [ ] Convert instance state to context state (`Get`/`Set`)
- [ ] Replace `OnTick` polling with events (if possible)
- [ ] Add error handling where appropriate
- [ ] Add custom events (if other scripts should react)

### Post-Migration

- [ ] Script compiles without errors
- [ ] Hot-reload works (save file, script reloads)
- [ ] All behaviors work as before
- [ ] State survives hot-reload
- [ ] Performance is same or better
- [ ] No console errors or warnings
- [ ] Documentation updated (if public API changed)

### Testing

- [ ] **Basic Functionality**: Core behavior works
- [ ] **Edge Cases**: Test boundary conditions
- [ ] **Hot-Reload**: State survives script reload
- [ ] **Composition**: Works with other scripts (if applicable)
- [ ] **Performance**: No slowdowns
- [ ] **Custom Events**: Other scripts can react (if published)

### Documentation

- [ ] Update script header comments
- [ ] Document configurable properties
- [ ] Document custom events (if any)
- [ ] Add usage examples (if complex)

---

## Advanced Topics

### Custom Event Definitions

Create your own events for mod interaction:

```csharp
// Define event interface
public interface IWeatherEvent : IGameEvent
{
    WeatherType Weather { get; }
}

// Implement specific event
public class RainStartedEvent : IWeatherEvent
{
    public WeatherType Weather => WeatherType.Rain;
    public float Intensity { get; init; }
}

// Publish in your script
Publish(new RainStartedEvent { Intensity = 0.8f });

// Other scripts react
On<RainStartedEvent>(evt => {
    if (evt.Intensity > 0.5f)
        SpawnRainPokemon();
});
```

---

### Shared State Between Scripts

Use namespaced keys for collaboration:

```csharp
// Script 1: Weather system
Set("weather:current", WeatherType.Rain);
Set("weather:intensity", 0.7f);

// Script 2: Plant growth (different file!)
var currentWeather = Get<WeatherType>("weather:current", WeatherType.Sunny);
var intensity = Get<float>("weather:intensity", 0f);

if (currentWeather == WeatherType.Rain)
    GrowFaster(intensity);
```

**Convention**: Use `:` to namespace shared state.

---

### Priority-Based Event Handling

Control execution order:

```csharp
// Security check (runs FIRST)
On<ItemUseEvent>(evt => {
    if (!HasPermission(evt.Entity))
        evt.PreventDefault("No permission");
}, priority: 100);

// Normal logic (runs if not cancelled)
On<ItemUseEvent>(evt => {
    UseItem(evt.Item);
}, priority: 500); // Default priority

// Logging (runs LAST)
On<ItemUseEvent>(evt => {
    LogItemUsage(evt.Item, evt.WasCancelled);
}, priority: 900);
```

**Priority ranges**:
- `0-200`: Security, validation
- `200-400`: Pre-processing
- `400-600`: Main logic (500 = default)
- `600-800`: Post-processing
- `800-1000`: Logging, cleanup

---

### Conditional Subscriptions

Subscribe only when conditions met:

```csharp
public override void RegisterEventHandlers(ScriptContext ctx)
{
    // Only subscribe if feature enabled
    if (Get<bool>("feature:playerDetection", false))
    {
        OnEntity<MovementCompletedEvent>(Context.Player.Entity, evt => {
            CheckPlayerDetection(evt.NewPosition);
        });
    }

    // Subscribe to different events based on config
    var mode = Get<string>("mode", "patrol");

    if (mode == "patrol")
        SubscribePatrolEvents();
    else if (mode == "guard")
        SubscribeGuardEvents();
}
```

---

### Unsubscribing (Advanced)

Rarely needed (cleanup automatic), but possible:

```csharp
private IDisposable? subscription;

public override void RegisterEventHandlers(ScriptContext ctx)
{
    // Save subscription for manual unsubscribe
    subscription = On<SomeEvent>(evt => {
        // Handle once, then unsubscribe
        DoSomething();
        subscription?.Dispose();
    });
}
```

**Note**: Usually not needed! Base class auto-cleanup on unload.

---

### Testing Scripts

Mock events for unit testing:

```csharp
[Test]
public void TestJumpBehavior()
{
    // Arrange
    var script = new JumpSouthBehavior();
    var ctx = new MockScriptContext();
    script.Initialize(ctx);
    script.RegisterEventHandlers(ctx);

    // Act
    var evt = new MovementStartedEvent {
        Entity = playerEntity,
        FromDirection = Direction.South
    };
    ctx.Events.Publish(evt);

    // Assert
    Assert.IsTrue(evt.IsCancelled);
    Assert.AreEqual("Cannot climb up ledge", evt.CancellationReason);
}
```

---

## Migration Tools

### Automated Migration Script (Optional)

We provide a CLI tool to automate common migrations:

```bash
# Install tool
npm install -g pokesharp-migration-tool

# Migrate single file
pokesharp-migrate --input jump_south.csx --output jump_south_migrated.csx

# Migrate entire directory
pokesharp-migrate --input Assets/Scripts/TileBehaviors --output Assets/Scripts/Unified

# Dry run (preview changes)
pokesharp-migrate --input jump_south.csx --dry-run
```

**What it automates**:
- ✅ Base class change
- ✅ Common virtual method → event conversions
- ✅ Instance state → context state (simple cases)
- ⚠️ Complex logic (manual review needed)

**What it doesn't handle**:
- ❌ Complex OnTick logic (needs human judgment)
- ❌ Custom event creation
- ❌ Performance optimizations

**Always review automated changes before committing!**

---

### Manual Migration Template

Use this as a starting point:

```csharp
using PokeSharp.Game.Scripting.Runtime;
using PokeSharp.Game.Components.Movement;

/// <summary>
/// TODO: Describe your script
/// </summary>
public class MyScript : ScriptBase
{
    // Configuration (public properties)
    public float someValue = 1.0f;

    public override void Initialize(ScriptContext ctx)
    {
        base.Initialize(ctx);

        // One-time initialization
        // Set default state if needed
        if (Get<int>("initialized", 0) == 0)
        {
            Set("initialized", 1);
            // Initial setup...
        }
    }

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // Subscribe to events
        On<SomeEvent>(evt => {
            // Your logic here
        });
    }

    // Helper methods
    private void HelperMethod()
    {
        // Use Context property, not ctx parameter
        Context.Effects.PlaySound("sound");
    }
}

return new MyScript();
```

---

## Getting Help

### Resources

- **Documentation**: `/docs/scripting/modding-platform-architecture.md`
- **Examples**: `/examples/csx-event-driven/`
- **Event Reference**: `/docs/scripting/EVENT-REFERENCE.md`
- **API Docs**: `/docs/api/ScriptBase.md`

### Common Questions

**Q: Do I have to migrate all scripts at once?**
A: No! Old and new systems coexist. Migrate incrementally.

**Q: Will my existing scripts break?**
A: No. TileBehaviorScriptBase and TypeScriptBase still work during transition.

**Q: Can I mix old and new scripts?**
A: Yes. Some tiles can use old base, others new base.

**Q: What if I need help with complex migration?**
A: Create an issue on GitHub with your script, we'll help!

---

## Summary

### Key Takeaways

1. **Change base class**: `TileBehaviorScriptBase`/`TypeScriptBase` → `ScriptBase`
2. **Add RegisterEventHandlers**: Subscribe to events instead of overriding methods
3. **Use context state**: `Get`/`Set` instead of instance variables
4. **Replace polling**: Use events instead of `OnTick` where possible
5. **Test thoroughly**: Verify behavior unchanged, state survives hot-reload

### Benefits

- ✅ **Composition**: Multiple scripts per tile/entity
- ✅ **Extensibility**: Custom events without engine changes
- ✅ **Simplicity**: One base class to learn
- ✅ **Performance**: Event-driven instead of polling
- ✅ **Testability**: Easy to mock and test

### Next Steps

1. Read `/docs/scripting/jump-script-migration-example.md` for detailed example
2. Try migrating a simple script (jump tile, warp tile)
3. Experiment with composition (2+ scripts on same tile)
4. Create custom events for mod interaction
5. Share your migrated scripts with the community!

---

**Happy Scripting!** 🎮

---

## Appendix: Event Type Reference

Quick reference for common events:

| Event | Use Case | Cancellable | Properties |
|-------|----------|-------------|------------|
| **MovementStartedEvent** | Block movement before it happens | ✅ Yes | `Entity`, `Direction`, `FromDirection`, `ToDirection` |
| **MovementCompletedEvent** | React after movement | ❌ No | `Entity`, `Direction`, `OldPosition`, `NewPosition` |
| **MovementBlockedEvent** | React to blocked movement | ❌ No | `Entity`, `Direction`, `BlockReason` |
| **TileSteppedOnEvent** | React to entity entering tile | ✅ Yes | `Entity`, `TilePosition` |
| **JumpCheckEvent** | Determine jump behavior | ❌ No | `Entity`, `FromDirection`, `JumpDirection`, `PerformJump` |
| **CollisionCheckEvent** | Check collision before movement | ✅ Yes | `Entity`, `TilePosition`, `FromDirection`, `ToDirection`, `IsBlocked` |
| **TickEvent** | Every frame update | ❌ No | `DeltaTime` |
| **InteractionEvent** | Player presses action button | ✅ Yes | `Entity`, `TargetEntity` |

See `/docs/scripting/EVENT-REFERENCE.md` for complete list.
