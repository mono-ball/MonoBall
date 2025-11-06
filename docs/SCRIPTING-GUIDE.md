# PokeSharp Scripting Guide

**For Modders and Content Creators**

## Introduction

PokeSharp uses Roslyn C# scripting (.csx files) for moddable behaviors. Scripts are compiled at runtime and support hot-reload for rapid iteration.

## Quick Start

### 1. Create a Behavior Definition

**File:** `Data/types/behaviors/my_behavior.json`
```json
{
  "typeId": "my_behavior",
  "displayName": "My Custom Behavior",
  "description": "What this behavior does",
  "behaviorScript": "behaviors/my_behavior.csx",
  "defaultSpeed": 4.0,
  "pauseAtWaypoint": 1.0
}
```

### 2. Create the Behavior Script

**File:** `Scripts/behaviors/my_behavior.csx`
```csharp
using Arch.Core;
using Microsoft.Xna.Framework;
using PokeSharp.Core.Components;
using PokeSharp.Scripting;

public class MyBehavior : TypeScriptBase
{
    protected override void OnInitialize(ScriptContext ctx)
    {
        // Called once when script loads
        // Use ctx.Logger for logging
        ctx.Logger.LogInformation("MyBehavior initialized");
    }

    public override void OnActivated(ScriptContext ctx)
    {
        // Called when behavior activates on an NPC
        // Initialize per-entity state here
        ctx.World.Add(ctx.Entity.Value, new MyState
        {
            Timer = 0f,
            Counter = 0
        });
    }

    public override void OnTick(ScriptContext ctx, float deltaTime)
    {
        // Called every frame while active
        // Access entity state (each NPC has its own!)
        ref var state = ref ctx.GetState<MyState>();
        ref var position = ref ctx.Position;

        state.Timer += deltaTime;
        if (state.Timer >= 1.0f)
        {
            state.Counter++;
            state.Timer = 0f;
            ctx.Logger.LogDebug("Counter: {Count} at position ({X},{Y})",
                state.Counter, position.X, position.Y);
        }
    }

    public override void OnDeactivated(ScriptContext ctx)
    {
        // Called when behavior deactivates
        // Cleanup entity state
        ctx.RemoveState<MyState>();
    }
}

// Define state component for this behavior
public struct MyState
{
    public float Timer;
    public int Counter;
}

return new MyBehavior();
```

### 3. Use in Game

Spawn an NPC with your behavior:
```csharp
var npc = entityFactory.SpawnFromTemplate("npc/generic", world,
    new EntitySpawnContext
    {
        Overrides = new Dictionary<string, object>
        {
            ["BehaviorComponent"] = new BehaviorComponent("my_behavior")
        }
    }
);
```

## Available APIs

### ScriptContext - Your Primary Interface

The `ScriptContext` object (`ctx`) is passed to all lifecycle methods and provides:

```csharp
public override void OnTick(ScriptContext ctx, float deltaTime)
{
    // ✅ Core Properties
    World world = ctx.World;          // ECS world for queries
    Entity? entity = ctx.Entity;      // Current entity (null for global scripts)
    ILogger logger = ctx.Logger;      // Logging instance

    // ✅ Context Type Checks
    bool isEntityScript = ctx.IsEntityScript;   // true for NPC behaviors
    bool isGlobalScript = ctx.IsGlobalScript;   // true for weather, events

    // ✅ Type-Safe Component Access
    ref var health = ref ctx.GetState<Health>();          // Get (throws if missing)
    bool found = ctx.TryGetState<Health>(out var hp);     // Safe get
    ref var timer = ref ctx.GetOrAddState<Timer>();       // Get or create
    bool has = ctx.HasState<Health>();                    // Check existence
    bool removed = ctx.RemoveState<StatusEffect>();       // Remove component

    // ✅ Convenience Properties
    ref var pos = ref ctx.Position;       // Shortcut for GetState<Position>()
    bool hasPos = ctx.HasPosition;        // Shortcut for HasState<Position>()
}
```

### Per-Entity State Management

**CRITICAL:** Scripts are stateless! Multiple NPCs share one script instance. Use components for per-entity state:

```csharp
public override void OnActivated(ScriptContext ctx)
{
    // ✅ CORRECT - Each NPC gets its own component
    ctx.World.Add(ctx.Entity.Value, new PatrolState
    {
        CurrentWaypoint = 0,
        WaitTimer = 0f
    });
}

public override void OnTick(ScriptContext ctx, float deltaTime)
{
    // ✅ CORRECT - Access per-entity state via GetState
    ref var state = ref ctx.GetState<PatrolState>();
    state.WaitTimer -= deltaTime;
}

// ❌ WRONG - Instance fields corrupt between entities!
private int _currentWaypoint;  // DON'T DO THIS!
```

### Working with Components

```csharp
public override void OnTick(ScriptContext ctx, float deltaTime)
{
    // ✅ Safe component access
    if (ctx.TryGetState<Health>(out var health))
    {
        ctx.Logger.LogInformation("HP: {Current}/{Max}", health.Current, health.Max);
    }

    // ✅ Modify components by reference (zero-allocation)
    ref var position = ref ctx.Position;
    position.X += 1;
    position.Y += 1;

    // ✅ Add movement request
    ctx.World.Add(ctx.Entity.Value, new MovementRequest(Direction.North));

    // ✅ Remove temporary component
    ctx.RemoveState<Stunned>();
}
```

### Helper Methods (from TypeScriptBase)

Static helper methods available in all scripts:

```csharp
// Calculate direction to target
var dir = TypeScriptBase.GetDirectionTo(currentPos, targetPos);

// Show message to player
TypeScriptBase.ShowMessage(ctx, "Hello, trainer!");

// Play sound effect
TypeScriptBase.PlaySound(ctx, "npc_greeting");

// Spawn visual effect
TypeScriptBase.SpawnEffect(ctx, "exclamation", new Point(10, 5));

// Random values
float rand = TypeScriptBase.Random();           // 0.0 to 1.0
int dice = TypeScriptBase.RandomRange(1, 7);    // 1 to 6
```

### World Queries (For Global Scripts)

```csharp
public override void OnTick(ScriptContext ctx, float deltaTime)
{
    if (ctx.IsGlobalScript)
    {
        // Query all NPCs
        var query = ctx.World.Query<Position, NpcComponent>();
        query.ForEach((ref Position pos, ref NpcComponent npc) =>
        {
            ctx.Logger.LogDebug("NPC at ({X}, {Y})", pos.X, pos.Y);
        });
    }
}
```

See [SCRIPT-CONTEXT-GUIDE.md](SCRIPT-CONTEXT-GUIDE.md) for complete ScriptContext reference.

## Common Patterns

### Patrol Behavior

```csharp
public class PatrolBehavior : TypeScriptBase
{
    public override void OnActivated(ScriptContext ctx)
    {
        // Initialize per-entity patrol state
        ctx.World.Add(ctx.Entity.Value, new PatrolState
        {
            CurrentWaypoint = 0,
            WaitTimer = 0f
        });
    }

    public override void OnTick(ScriptContext ctx, float deltaTime)
    {
        ref var state = ref ctx.GetState<PatrolState>();
        ref var path = ref ctx.GetState<PathComponent>();
        ref var position = ref ctx.Position;

        // Wait at waypoint
        if (state.WaitTimer > 0)
        {
            state.WaitTimer -= deltaTime;
            return;
        }

        var target = path.Waypoints[state.CurrentWaypoint];

        // Reached waypoint?
        if (position.X == target.X && position.Y == target.Y)
        {
            state.CurrentWaypoint = (state.CurrentWaypoint + 1) % path.Waypoints.Length;
            state.WaitTimer = path.WaypointWaitTime;
            return;
        }

        // Move toward waypoint
        var direction = TypeScriptBase.GetDirectionTo(
            new Point(position.X, position.Y), target);
        ctx.World.Add(ctx.Entity.Value, new MovementRequest(direction));
    }
}

public struct PatrolState
{
    public int CurrentWaypoint;
    public float WaitTimer;
}
```

### Guard Behavior (Stationary with Rotation)

```csharp
public class GuardBehavior : TypeScriptBase
{
    public override void OnActivated(ScriptContext ctx)
    {
        ref var position = ref ctx.Position;

        ctx.World.Add(ctx.Entity.Value, new GuardState
        {
            GuardPosition = new Point(position.X, position.Y),
            FacingDirection = Direction.South,
            ScanTimer = 2.0f
        });
    }

    public override void OnTick(ScriptContext ctx, float deltaTime)
    {
        ref var state = ref ctx.GetState<GuardState>();
        ref var position = ref ctx.Position;

        // Return to guard post if moved
        if (position.X != state.GuardPosition.X || position.Y != state.GuardPosition.Y)
        {
            var dir = TypeScriptBase.GetDirectionTo(
                new Point(position.X, position.Y),
                state.GuardPosition
            );
            ctx.World.Add(ctx.Entity.Value, new MovementRequest(dir));
            return;
        }

        // Rotate periodically to scan
        state.ScanTimer -= deltaTime;
        if (state.ScanTimer <= 0)
        {
            state.FacingDirection = RotateClockwise(state.FacingDirection);
            state.ScanTimer = 2.0f;
        }
    }

    private static Direction RotateClockwise(Direction current) =>
        current switch
        {
            Direction.North => Direction.East,
            Direction.East => Direction.South,
            Direction.South => Direction.West,
            Direction.West => Direction.North,
            _ => Direction.North
        };
}

public struct GuardState
{
    public Point GuardPosition;
    public Direction FacingDirection;
    public float ScanTimer;
}
```

### Random Wander

```csharp
public class WanderBehavior : TypeScriptBase
{
    public override void OnActivated(ScriptContext ctx)
    {
        ctx.World.Add(ctx.Entity.Value, new WanderState
        {
            MoveTimer = TypeScriptBase.Random() * 3.0f
        });
    }

    public override void OnTick(ScriptContext ctx, float deltaTime)
    {
        ref var state = ref ctx.GetState<WanderState>();
        state.MoveTimer -= deltaTime;

        if (state.MoveTimer <= 0)
        {
            // Pick random direction
            var directions = new[] { Direction.North, Direction.South, Direction.East, Direction.West };
            var randomDir = directions[TypeScriptBase.RandomRange(0, directions.Length)];

            ctx.World.Add(ctx.Entity.Value, new MovementRequest(randomDir));

            // Next movement in 1-4 seconds
            state.MoveTimer = TypeScriptBase.Random() * 3.0f + 1.0f;
        }
    }
}

public struct WanderState
{
    public float MoveTimer;
}
```

## Hot-Reload

### How to Hot-Reload

1. Keep game running
2. Edit your .csx file
3. Save the file
4. Game automatically reloads script

**Note:** With the ScriptContext pattern, state is stored in ECS components, so hot-reload is safe by default!

### Reload-Safe Pattern (Automatic!)

```csharp
public class ReloadSafeBehavior : TypeScriptBase
{
    // ✅ NO INSTANCE FIELDS NEEDED!
    // State is stored in ECS components, which survive hot-reload

    public override void OnActivated(ScriptContext ctx)
    {
        // State survives reload
        ctx.World.Add(ctx.Entity.Value, new MyState
        {
            Counter = 0,
            Timer = 0f
        });
    }

    public override void OnTick(ScriptContext ctx, float deltaTime)
    {
        // State automatically persists through hot-reload!
        ref var state = ref ctx.GetState<MyState>();
        state.Counter++;
        state.Timer += deltaTime;

        // Even if this script reloads, state.Counter and state.Timer
        // remain unchanged because they're in ECS components
    }
}

public struct MyState
{
    public int Counter;
    public float Timer;
}
```

### What Survives Hot-Reload?

✅ **SURVIVES** (Stored in ECS):
- Component data accessed via `ctx.GetState<T>()`
- Entity positions, health, inventory
- World state and queries
- All gameplay data

❌ **LOST** (Script instance data):
- Private fields in script class
- Local variables
- Cached references

**Best Practice:** Always use components for state. The ScriptContext pattern enforces this automatically!

## Debugging

### Logging

```csharp
// Use Console.WriteLine (appears in game console)
Console.WriteLine($"NPC at position: {position.X}, {position.Y}");
```

### Common Errors

**Script won't compile:**
- Check for syntax errors
- Ensure all `using` statements are present
- Verify class inherits from `TypeScriptBase`

**Script loads but doesn't run:**
- Check `IsActive` flag on BehaviorComponent
- Ensure NPC has required components (Position, etc.)
- Verify behavior type ID matches JSON file

**Script crashes:**
- Always check `Entity.HasValue` before using `Entity.Value`
- Use `World.Has<T>()` before `World.Get<T>()`
- Wrap risky code in try-catch

## Best Practices

### 1. **NEVER Use Instance Fields**
```csharp
// ❌ WRONG - Corrupts state between entities
private int _counter;

// ✅ CORRECT - Use components
public struct MyState { public int Counter; }
ctx.World.Add(ctx.Entity.Value, new MyState { Counter = 0 });
```

### 2. **Always Use ScriptContext**
```csharp
// ❌ WRONG - Old pattern (don't use!)
public override void OnTick(float deltaTime)
{
    var pos = World.Get<Position>(Entity.Value);
}

// ✅ CORRECT - New pattern with context
public override void OnTick(ScriptContext ctx, float deltaTime)
{
    ref var pos = ref ctx.Position;
}
```

### 3. **Use References for Zero-Allocation Modification**
```csharp
// ❌ Copies struct (slower)
var health = ctx.GetState<Health>();
health.Current -= 10;  // Modifies copy, not original!

// ✅ Modifies directly (faster)
ref var health = ref ctx.GetState<Health>();
health.Current -= 10;  // Modifies original
```

### 4. **Safe Component Access**
```csharp
// ❌ Throws if component missing
ref var health = ref ctx.GetState<Health>();

// ✅ Safe check first
if (ctx.TryGetState<Health>(out var health))
{
    // Use health safely
}
```

### 5. **Keep OnTick Fast**
```csharp
// ❌ Expensive operation every frame
public override void OnTick(ScriptContext ctx, float deltaTime)
{
    var nearbyEntities = FindAllNearbyEntities(); // Slow!
}

// ✅ Use timers for expensive checks
public override void OnTick(ScriptContext ctx, float deltaTime)
{
    ref var state = ref ctx.GetState<MyState>();
    state.CheckTimer -= deltaTime;

    if (state.CheckTimer <= 0)
    {
        var nearbyEntities = FindAllNearbyEntities();
        state.CheckTimer = 1.0f; // Check every second
    }
}
```

### 6. **Cleanup State on Deactivate**
```csharp
public override void OnDeactivated(ScriptContext ctx)
{
    // Remove behavior state components
    ctx.RemoveState<MyState>();
    ctx.Logger.LogInformation("Cleaned up state");
}
```

### 7. **Use Logger for Debugging**
```csharp
// Different log levels for different purposes
ctx.Logger.LogTrace("Fine-grained detail");      // Development only
ctx.Logger.LogDebug("Diagnostic information");   // Troubleshooting
ctx.Logger.LogInformation("General events");     // Normal operation
ctx.Logger.LogWarning("Unexpected situation");   // Potential issues
ctx.Logger.LogError("Failure occurred");         // Errors
```

## Performance Tips

```csharp
// ❌ BAD: Creates allocations every frame
public override void OnTick(ScriptContext ctx, float deltaTime)
{
    var nearby = GetNearbyEntities(); // Allocates array
    foreach (var entity in nearby) { ... }
}

// ✅ GOOD: Use ECS queries (zero allocation)
public override void OnTick(ScriptContext ctx, float deltaTime)
{
    var query = ctx.World.Query<Position, NpcComponent>();
    query.ForEach((Entity e, ref Position pos, ref NpcComponent npc) =>
    {
        // Process entities
    });
}

// ✅ BEST: Use ref for component access (no copying)
public override void OnTick(ScriptContext ctx, float deltaTime)
{
    ref var state = ref ctx.GetState<MyState>();  // Reference, not copy
    ref var pos = ref ctx.Position;                // Reference, not copy

    // Modifies original components directly
    state.Timer += deltaTime;
    pos.X += 1;
}
```

## Advanced: Custom Components

You can define custom components directly in your .csx scripts:

```csharp
using Arch.Core;
using Microsoft.Xna.Framework;
using PokeSharp.Core.Components;
using PokeSharp.Scripting;

// ✅ Define custom state component in script
public struct QuestGiverState
{
    public int QuestPhase;
    public float CooldownTimer;
    public bool QuestCompleted;
    public int TimesInteracted;
}

public class QuestGiverBehavior : TypeScriptBase
{
    public override void OnActivated(ScriptContext ctx)
    {
        // Initialize custom state
        ctx.World.Add(ctx.Entity.Value, new QuestGiverState
        {
            QuestPhase = 0,
            CooldownTimer = 0f,
            QuestCompleted = false,
            TimesInteracted = 0
        });
    }

    public override void OnTick(ScriptContext ctx, float deltaTime)
    {
        ref var state = ref ctx.GetState<QuestGiverState>();

        // Complex multi-phase behavior
        switch (state.QuestPhase)
        {
            case 0: // Initial greeting
                // ...
                break;
            case 1: // Quest in progress
                // ...
                break;
            case 2: // Quest completed
                state.QuestCompleted = true;
                break;
        }
    }
}

return new QuestGiverBehavior();
```

## Examples Repository

See `Assets/Scripts/Behaviors/` for complete working examples:
- `patrol_behavior.csx` - Waypoint patrol with per-entity state
- `guard_behavior.csx` - Stationary guard with rotation
- `wander_behavior.csx` - Random wandering NPC
- `weather_rain.csx` - Global script example (weather system)

All examples use the ScriptContext pattern for proper state management!

## Getting Help

- Check console for compilation errors
- Review example scripts
- Join community Discord
- Report bugs on GitHub

Happy modding!


