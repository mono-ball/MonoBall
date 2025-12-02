# Migration Comparison: Phase 2 → Phase 3

This document shows side-by-side comparisons of scripts migrating from Phase 2 (specialized base classes) to Phase 3 (unified ScriptBase).

## Overview of Changes

| Aspect | Phase 2 | Phase 3 |
|--------|---------|---------|
| **Base Classes** | `TileBehaviorScriptBase`, `TypeScriptBase` | Single `ScriptBase` |
| **Event Handlers** | `OnTileSteppedOn()`, `OnMovementCompleted()` | `On<TileSteppedOnEvent>()`, `On<MovementCompletedEvent>()` |
| **Priority** | Not supported | `public override int Priority` |
| **Composition** | Limited | Multiple scripts per tile/entity |
| **Custom Events** | Not supported | Define and publish custom events |
| **Logging** | Console.WriteLine | ctx.Logger with levels |

---

## Example 1: Ice Tile

### Phase 2 Version

```csharp
// ice_tile.csx (Phase 2)
public class IceTile : TileBehaviorScriptBase {
    public override void RegisterEventHandlers(ScriptContext ctx) {
        // Specialized method from TileBehaviorScriptBase
        OnMovementCompleted(evt => {
            if (!ctx.Player.IsPlayerEntity(evt.Entity)) {
                return;
            }

            if (IsOnIceTile(evt.NewPosition)) {
                ContinueSliding(evt.Entity, evt.Direction);
            } else {
                RestoreNormalSpeed(evt.Entity);
            }
        });

        // Another specialized method
        OnTileSteppedOn(evt => {
            if (ctx.Player.IsPlayerEntity(evt.Entity)) {
                ctx.Effects.PlaySound("ice_slide");
            }
        });
    }

    // No logging support
    private void ContinueSliding(Entity entity, Direction direction) {
        // Logic...
    }
}
```

### Phase 3 Version

```csharp
// ice_tile_unified.csx (Phase 3)
public class IceTileScript : ScriptBase {
    public override void RegisterEventHandlers(ScriptContext ctx) {
        // Generic event subscription
        On<MovementCompletedEvent>(evt => {
            if (!ctx.Player.IsPlayerEntity(evt.Entity)) {
                return;
            }

            ctx.Logger.Info($"Ice tile: Player completed movement to {evt.NewPosition}");

            if (IsOnIceTile(evt.NewPosition)) {
                ContinueSliding(evt.Entity, evt.Direction);
            } else {
                RestoreNormalSpeed(evt.Entity);
                ctx.Logger.Info("Ice tile: Player left ice, normal speed restored");
            }
        });

        // Same pattern for all events
        On<TileSteppedOnEvent>(evt => {
            if (ctx.Player.IsPlayerEntity(evt.Entity)) {
                ctx.Effects.PlaySound("ice_slide");
                ctx.Logger.Info($"Ice tile activated at ({evt.TileX}, {evt.TileY})");
            }
        });
    }

    // Built-in logging
    private void ContinueSliding(Entity entity, Direction direction) {
        ctx.Logger.Info("Ice tile: Hit obstacle, stopped sliding");
        // Logic...
    }
}
```

### Key Differences

1. **Base Class**: `TileBehaviorScriptBase` → `ScriptBase`
2. **Event Methods**: `OnMovementCompleted()` → `On<MovementCompletedEvent>()`
3. **Logging**: None → `ctx.Logger.Info()`
4. **Naming**: `IceTile` → `IceTileScript` (convention)

---

## Example 2: NPC Patrol

### Phase 2 Version

```csharp
// npc_patrol.csx (Phase 2)
public class NPCPatrol : TypeScriptBase {
    private int currentIndex = 0;
    private Entity npcEntity;

    public override void OnInitialize(ScriptContext ctx) {
        base.OnInitialize(ctx);
        npcEntity = GetAttachedEntity();
        MoveToNextPoint();
        // No initialization logging
    }

    public override void RegisterEventHandlers(ScriptContext ctx) {
        // Specialized method from TypeScriptBase
        OnMovementCompleted(evt => {
            if (evt.Entity != npcEntity) return;
            WaitAtPoint();
        });

        OnMovementBlocked(evt => {
            if (evt.Entity != npcEntity) return;
            currentIndex = (currentIndex + 1) % patrolPoints.Count;
            MoveToNextPoint();
        });
    }

    public override void OnTick(ScriptContext ctx, float deltaTime) {
        // Polling-based wait
        if (isWaiting) {
            waitTimer -= deltaTime;
            if (waitTimer <= 0) {
                isWaiting = false;
                MoveToNextPoint();
            }
        }
    }
}
```

### Phase 3 Version

```csharp
// npc_patrol_unified.csx (Phase 3)
public class NPCPatrolScript : ScriptBase {
    private int currentIndex = 0;
    private Entity npcEntity;

    public override void OnInitialize(ScriptContext ctx) {
        base.OnInitialize(ctx);
        npcEntity = GetAttachedEntity();

        ctx.Logger.Info($"NPC Patrol: Initialized with {patrolPoints.Count} waypoints");

        MoveToNextPoint();
    }

    public override void RegisterEventHandlers(ScriptContext ctx) {
        // Generic event subscription
        On<MovementCompletedEvent>(evt => {
            if (evt.Entity != npcEntity) return;

            ctx.Logger.Info($"NPC Patrol: Reached waypoint {currentIndex} at {evt.NewPosition}");

            WaitAtPoint();
        });

        On<MovementBlockedEvent>(evt => {
            if (evt.Entity != npcEntity) return;

            ctx.Logger.Info("NPC Patrol: Movement blocked, skipping to next waypoint");

            currentIndex = (currentIndex + 1) % patrolPoints.Count;
            MoveToNextPoint();
        });
    }

    public override void OnTick(ScriptContext ctx, float deltaTime) {
        // Same polling-based wait (unchanged)
        if (isWaiting) {
            waitTimer -= deltaTime;
            if (waitTimer <= 0) {
                isWaiting = false;
                ctx.Logger.Info("NPC Patrol: Wait complete, moving to next waypoint");
                MoveToNextPoint();
            }
        }
    }
}
```

### Key Differences

1. **Base Class**: `TypeScriptBase` → `ScriptBase`
2. **Event Methods**: Same pattern as ice tile
3. **Logging**: Added structured logging at key points
4. **OnTick**: Unchanged (same signature)

---

## Example 3: Custom Events (New in Phase 3)

### Phase 2: No Custom Event Support

Phase 2 had no mechanism for custom events. Scripts could only react to built-in system events.

### Phase 3: Custom Event Pattern

```csharp
// Step 1: Define custom event
public class LedgeJumpedEvent : IGameEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public Entity Entity { get; init; }
    public Direction JumpDirection { get; init; }
    public Vector2 StartPosition { get; init; }
    public Vector2 LandingPosition { get; init; }
}

// Step 2: Publish event in publisher script
public class LedgeJumpScript : ScriptBase
{
    private void PerformJump(Entity entity, Vector2 startPos, Vector2 landingPos)
    {
        // ... jump logic ...

        // Publish custom event
        Publish(new LedgeJumpedEvent {
            Entity = entity,
            JumpDirection = ledgeDirection,
            StartPosition = startPos,
            LandingPosition = landingPos
        });

        ctx.Logger.Info("Ledge: Published LedgeJumpedEvent");
    }
}

// Step 3: Subscribe in listener script
public class LedgeJumpListenerScript : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<LedgeJumpedEvent>(evt => {
            ctx.Logger.Info($"Jump detected! Direction: {evt.JumpDirection}");
            // React to jump...
        });
    }
}
```

**Benefits**:
- Decoupled scripts
- Multiple listeners per event
- Type-safe event data
- Extensible without modifying publishers

---

## Example 4: Script Composition (New in Phase 3)

### Phase 2: No Composition Support

Phase 2 allowed only one script per tile/entity. You couldn't combine behaviors.

### Phase 3: Multiple Scripts with Priority

```csharp
// First script: Ice behavior (Priority 100)
public class CompositeIceScript : ScriptBase
{
    public override int Priority => 100; // Executes first

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<TileSteppedOnEvent>(evt => {
            ctx.Logger.Info("[Ice] Processing ice behavior");
            // Ice logic...
        });
    }
}

// Second script: Grass behavior (Priority 50)
public class CompositeGrassScript : ScriptBase
{
    public override int Priority => 50; // Executes second

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<TileSteppedOnEvent>(evt => {
            ctx.Logger.Info("[Grass] Processing grass behavior");
            // Grass logic...
        });
    }
}

// Return both scripts
return new ScriptBase[] {
    new CompositeIceScript(),
    new CompositeGrassScript()
};
```

**Execution Order**:
1. CompositeIceScript (Priority 100) handles TileSteppedOnEvent
2. CompositeGrassScript (Priority 50) handles TileSteppedOnEvent
3. Both behaviors work independently on same tile!

---

## Event Handler Comparison

### Phase 2 Event Methods

| Method | Description |
|--------|-------------|
| `OnTileSteppedOn(Action<TileSteppedOnEvent>)` | Tile was stepped on |
| `OnMovementStarted(Action<MovementStartedEvent>)` | Movement started |
| `OnMovementCompleted(Action<MovementCompletedEvent>)` | Movement completed |
| `OnMovementBlocked(Action<MovementBlockedEvent>)` | Movement blocked |

**Limitations**:
- Specialized methods for specific bases
- No custom events
- No priority control
- Limited to predefined event types

### Phase 3 Event Subscription

| Pattern | Description |
|---------|-------------|
| `On<TileSteppedOnEvent>(evt => { })` | Subscribe to tile stepped on |
| `On<MovementStartedEvent>(evt => { })` | Subscribe to movement started |
| `On<MovementCompletedEvent>(evt => { })` | Subscribe to movement completed |
| `On<MovementBlockedEvent>(evt => { })` | Subscribe to movement blocked |
| `On<CustomEvent>(evt => { })` | Subscribe to ANY event type |

**Benefits**:
- Unified subscription pattern
- Custom events supported
- Priority-based execution
- Type-safe event data

---

## Logging Comparison

### Phase 2: Console.WriteLine

```csharp
// Phase 2
Console.WriteLine($"Wild {pokemonName} (Lv.{level}) appeared at {position}!");
```

**Problems**:
- No log levels
- Hard to filter
- No context
- No structured data

### Phase 3: Structured Logging

```csharp
// Phase 3
ctx.Logger.Info($"Wild {pokemonName} (Lv.{level}) appeared at {position}!");
ctx.Logger.Debug("Detailed debug info");
ctx.Logger.Warn("Warning message");
ctx.Logger.Error("Error occurred");
```

**Benefits**:
- Multiple log levels
- Easy filtering
- Script context included
- Structured output

---

## Migration Checklist

Use this checklist when migrating a Phase 2 script to Phase 3:

### Step 1: Update Base Class
- [ ] Change `TileBehaviorScriptBase` to `ScriptBase`
- [ ] Change `TypeScriptBase` to `ScriptBase`
- [ ] Update class name to end with `Script` (convention)

### Step 2: Update Event Handlers
- [ ] Replace `OnTileSteppedOn(...)` with `On<TileSteppedOnEvent>(...)`
- [ ] Replace `OnMovementCompleted(...)` with `On<MovementCompletedEvent>(...)`
- [ ] Replace `OnMovementStarted(...)` with `On<MovementStartedEvent>(...)`
- [ ] Replace `OnMovementBlocked(...)` with `On<MovementBlockedEvent>(...)`

### Step 3: Add Logging
- [ ] Replace `Console.WriteLine()` with `ctx.Logger.Info()`
- [ ] Add debug logging at key points
- [ ] Add error logging for failure cases
- [ ] Add info logging for state changes

### Step 4: Optional Enhancements
- [ ] Add `Priority` property if using composition
- [ ] Define custom events if needed
- [ ] Add event publishing with `Publish()`
- [ ] Consider splitting into multiple scripts

### Step 5: Test
- [ ] Verify basic functionality works
- [ ] Test hot-reload
- [ ] Check logging output
- [ ] Verify composition if using multiple scripts

---

## Common Migration Patterns

### Pattern 1: Simple Event Handler

**Before (Phase 2)**:
```csharp
OnTileSteppedOn(evt => {
    // Handle event
});
```

**After (Phase 3)**:
```csharp
On<TileSteppedOnEvent>(evt => {
    ctx.Logger.Info("Tile stepped on");
    // Handle event
});
```

### Pattern 2: Entity Filtering

**Before (Phase 2)**:
```csharp
OnMovementCompleted(evt => {
    if (evt.Entity != npcEntity) return;
    // Handle NPC movement
});
```

**After (Phase 3)**:
```csharp
On<MovementCompletedEvent>(evt => {
    if (evt.Entity != npcEntity) return;

    ctx.Logger.Debug($"NPC moved to {evt.NewPosition}");
    // Handle NPC movement
});
```

### Pattern 3: Complex State Management

**Before (Phase 2)**:
```csharp
private void TriggerBattle(Entity player) {
    isWaiting = true;
    FaceEntity(npcEntity, player);
    // ... more logic ...
}
```

**After (Phase 3)**:
```csharp
private void TriggerBattle(Entity player) {
    isWaiting = true;

    ctx.Logger.Info("Triggering trainer battle");

    FaceEntity(npcEntity, player);
    // ... more logic ...

    ctx.Logger.Info("Battle started");
}
```

---

## Performance Impact

### Phase 2 → Phase 3 Performance

| Aspect | Phase 2 | Phase 3 | Impact |
|--------|---------|---------|--------|
| Event Dispatch | Direct method calls | Generic event routing | Minimal overhead |
| Memory | One script per tile | Multiple scripts possible | Slightly higher if using composition |
| Logging | Console.WriteLine | Structured logger | Minimal overhead |
| Compilation | Simple | Same | No change |

**Conclusion**: Phase 3 has minimal performance impact while providing significantly more features.

---

## Troubleshooting

### Issue: Script won't compile

**Cause**: Incorrect base class or event handler syntax

**Solution**:
```csharp
// ✅ Correct
public class MyScript : ScriptBase {
    On<TileSteppedOnEvent>(evt => { });
}

// ❌ Incorrect
public class MyScript : TileBehaviorScriptBase {
    OnTileSteppedOn(evt => { });
}
```

### Issue: Events not firing

**Cause**: Forgot to call base.OnInitialize()

**Solution**:
```csharp
public override void OnInitialize(ScriptContext ctx) {
    base.OnInitialize(ctx); // Required!
    // Your initialization...
}
```

### Issue: Logging not appearing

**Cause**: Log level too high

**Solution**: Check log configuration, use appropriate level:
```csharp
ctx.Logger.Debug("Only in debug builds");
ctx.Logger.Info("Always logged");
```

---

## Summary

Phase 3's unified scripting system provides:

✅ **Simplified API**: One base class for everything
✅ **Better Composition**: Multiple scripts per tile/entity
✅ **Custom Events**: Publish and subscribe to domain events
✅ **Structured Logging**: Better debugging and monitoring
✅ **Priority System**: Control execution order
✅ **Type Safety**: Compile-time event checking

The migration is straightforward and provides significant benefits for maintainability and extensibility.

---

**See Also**:
- [README.md](./README.md) - Complete usage guide
- [Phase 2 Examples](../csx-event-driven/) - Original implementations
- [Implementation Roadmap](../../docs/IMPLEMENTATION-ROADMAP.md) - Full plan
