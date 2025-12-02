# Jump Script Migration Example

**Comparing**: `jump_south.csx` - Current vs Unified Architecture

---

## Current Implementation (TileBehaviorScriptBase)

**File**: `PokeSharp.Game/Assets/Scripts/TileBehaviors/jump_south.csx`

```csharp
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
        // Allow jumping south when coming from north (player moving north onto this tile)
        if (fromDirection == Direction.North)
            return Direction.South;

        return Direction.None;
    }
}

return new JumpSouthBehavior();
```

**Current Architecture**:
- ✅ Inherits from `TileBehaviorScriptBase`
- ✅ Overrides 3 virtual methods
- ✅ Methods called by `TileBehaviorSystem` during collision checks
- ✅ Type-safe: Compiler knows this is a tile behavior
- ✅ Query-based: System asks "is this blocked?" and "what's jump direction?"

**Lines of Code**: 42 lines (with comments and formatting)

---

## Unified Implementation (Event-Driven ScriptBase)

**Option 1: Direct Event Migration** (Most Similar)

```csharp
using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Scripting.Runtime;

/// <summary>
///     Jump south behavior.
///     Allows jumping south but blocks north movement.
/// </summary>
public class JumpSouthBehavior : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // Subscribe to collision check events
        ctx.On<CollisionCheckEvent>(OnCollisionCheck);
        ctx.On<JumpCheckEvent>(OnJumpCheck);
    }

    private void OnCollisionCheck(CollisionCheckEvent evt)
    {
        // Block movement from north (can't climb up)
        if (evt.FromDirection == Direction.North)
        {
            evt.IsBlocked = true;
            evt.BlockReason = "Can't climb up ledge";
            return;
        }

        // Block movement to north (can't enter from south)
        if (evt.ToDirection == Direction.North)
        {
            evt.IsBlocked = true;
            evt.BlockReason = "Ledge blocks path";
            return;
        }
    }

    private void OnJumpCheck(JumpCheckEvent evt)
    {
        // Allow jumping south when coming from north
        if (evt.FromDirection == Direction.North)
        {
            evt.JumpDirection = Direction.South;
            evt.PerformJump = true;
        }
    }
}

return new JumpSouthBehavior();
```

**Lines of Code**: 47 lines (5 more lines due to event handlers)

---

**Option 2: Simplified Event Registration** (More Concise)

```csharp
using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Scripting.Runtime;

/// <summary>
///     Jump south behavior.
///     Allows jumping south but blocks north movement.
/// </summary>
public class JumpSouthBehavior : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // Block collision from north (can't climb up)
        ctx.OnCollisionCheck(evt => {
            if (evt.FromDirection == Direction.North || evt.ToDirection == Direction.North)
            {
                evt.IsBlocked = true;
                evt.BlockReason = "Can't climb ledge";
            }
        });

        // Allow jumping south when coming from north
        ctx.OnJumpCheck(evt => {
            if (evt.FromDirection == Direction.North)
            {
                evt.JumpDirection = Direction.South;
                evt.PerformJump = true;
            }
        });
    }
}

return new JumpSouthBehavior();
```

**Lines of Code**: 30 lines (12 lines fewer than current!)

---

**Option 3: Ultra-Concise Functional Style**

```csharp
using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Scripting.Runtime;

public class JumpSouthBehavior : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // Block northward movement (can't climb)
        ctx.OnCollisionCheck(evt =>
            evt.IsBlocked = evt.FromDirection == Direction.North || evt.ToDirection == Direction.North);

        // Jump south from north
        ctx.OnJumpCheck(evt => {
            if (evt.FromDirection == Direction.North) {
                evt.JumpDirection = Direction.South;
                evt.PerformJump = true;
            }
        });
    }
}

return new JumpSouthBehavior();
```

**Lines of Code**: 22 lines (20 lines fewer than current!)

---

## Side-by-Side Comparison

### What Changed?

| Aspect | Current (TileBehaviorScriptBase) | Unified (ScriptBase) |
|--------|----------------------------------|----------------------|
| Base Class | `TileBehaviorScriptBase` | `ScriptBase` |
| API Pattern | Override virtual methods | Subscribe to events |
| Execution Model | Query-based (system calls methods) | Event-driven (methods react to events) |
| Type Safety | Compile-time (enforced by base class) | Runtime (event types checked at runtime) |
| Code Size | 42 lines | 22-47 lines (depends on style) |
| Verbosity | Medium | Low (with lambdas) |
| Testability | Good (mock ScriptContext) | Excellent (mock events) |

### What Stayed the Same?

- ✅ Same logic (block north, allow south jump)
- ✅ Same behavior at runtime
- ✅ Same CSX Roslyn compilation
- ✅ Same hot-reload support
- ✅ Same ScriptContext API
- ✅ Still returns instance at end

---

## Detailed Migration Steps

### Step 1: Change Base Class

**Before**:
```csharp
public class JumpSouthBehavior : TileBehaviorScriptBase
```

**After**:
```csharp
public class JumpSouthBehavior : ScriptBase
```

---

### Step 2: Remove Virtual Method Overrides

**Before**:
```csharp
public override bool IsBlockedFrom(ScriptContext ctx, Direction from, Direction to) { }
public override bool IsBlockedTo(ScriptContext ctx, Direction to) { }
public override Direction GetJumpDirection(ScriptContext ctx, Direction from) { }
```

**After**: Delete these methods entirely.

---

### Step 3: Add Event Registration

**Add**:
```csharp
public override void RegisterEventHandlers(ScriptContext ctx)
{
    // Event subscriptions go here
}
```

---

### Step 4: Convert Logic to Events

**Collision Blocking** (IsBlockedFrom/IsBlockedTo → CollisionCheckEvent):
```csharp
// BEFORE: System queries methods
public override bool IsBlockedFrom(ScriptContext ctx, Direction from, Direction to)
{
    if (from == Direction.North) return true;
    return false;
}

// AFTER: Script reacts to event
ctx.OnCollisionCheck(evt => {
    if (evt.FromDirection == Direction.North || evt.ToDirection == Direction.North)
    {
        evt.IsBlocked = true;  // Set property instead of returning bool
        evt.BlockReason = "Can't climb ledge";
    }
});
```

**Jump Direction** (GetJumpDirection → JumpCheckEvent):
```csharp
// BEFORE: System queries method
public override Direction GetJumpDirection(ScriptContext ctx, Direction from)
{
    if (from == Direction.North) return Direction.South;
    return Direction.None;
}

// AFTER: Script reacts to event
ctx.OnJumpCheck(evt => {
    if (evt.FromDirection == Direction.North)
    {
        evt.JumpDirection = Direction.South;  // Set property instead of returning value
        evt.PerformJump = true;
    }
});
```

---

### Step 5: Test

```bash
# Hot-reload should work
# 1. Edit script file
# 2. Save
# 3. Game reloads script automatically
# 4. Step on jump tile to test
```

---

## Event Type Definitions Needed

For the unified approach to work, you'd need these event types:

### CollisionCheckEvent

```csharp
public class CollisionCheckEvent : IGameEvent, ICancellableEvent
{
    public Entity Entity { get; init; }
    public Vector2 TilePosition { get; init; }
    public Direction FromDirection { get; init; }
    public Direction ToDirection { get; init; }

    // Script sets these
    public bool IsBlocked { get; set; }
    public string BlockReason { get; set; }

    // ICancellableEvent
    public bool IsCancelled => IsBlocked;
    public string CancellationReason => BlockReason;

    public void Cancel(string reason)
    {
        IsBlocked = true;
        BlockReason = reason;
    }
}
```

### JumpCheckEvent

```csharp
public class JumpCheckEvent : IGameEvent
{
    public Entity Entity { get; init; }
    public Vector2 TilePosition { get; init; }
    public Direction FromDirection { get; init; }

    // Script sets these
    public Direction JumpDirection { get; set; } = Direction.None;
    public bool PerformJump { get; set; } = false;
}
```

---

## How Systems Would Change

### Current System Code

```csharp
// TileBehaviorSystem.cs (current)
public bool IsMovementBlocked(Vector2 tilePos, Direction from, Direction to)
{
    var behavior = GetBehaviorForTile(tilePos);
    if (behavior == null) return false;

    // Direct method call
    return behavior.IsBlockedFrom(scriptContext, from, to) ||
           behavior.IsBlockedTo(scriptContext, to);
}

public Direction GetJumpDirection(Vector2 tilePos, Direction from)
{
    var behavior = GetBehaviorForTile(tilePos);
    if (behavior == null) return Direction.None;

    // Direct method call
    return behavior.GetJumpDirection(scriptContext, from);
}
```

### Unified System Code

```csharp
// TileBehaviorSystem.cs (unified)
public bool IsMovementBlocked(Vector2 tilePos, Direction from, Direction to)
{
    var evt = new CollisionCheckEvent {
        Entity = playerEntity,
        TilePosition = tilePos,
        FromDirection = from,
        ToDirection = to,
        IsBlocked = false
    };

    // Publish event - all scripts at this tile position react
    eventBus.Publish(evt);

    return evt.IsBlocked;
}

public Direction GetJumpDirection(Vector2 tilePos, Direction from)
{
    var evt = new JumpCheckEvent {
        Entity = playerEntity,
        TilePosition = tilePos,
        FromDirection = from,
        JumpDirection = Direction.None,
        PerformJump = false
    };

    // Publish event
    eventBus.Publish(evt);

    return evt.PerformJump ? evt.JumpDirection : Direction.None;
}
```

---

## Performance Comparison

### Current Approach (Method Calls)

```
Per frame with 1 player moving:
- Check collision: 1 virtual method call (~10ns)
- Check jump: 1 virtual method call (~10ns)
- Total: ~20ns per tile check
```

### Unified Approach (Events)

```
Per frame with 1 player moving:
- Create event object: ~50ns (can be pooled → ~5ns)
- Publish event: ~100ns
- Invoke handlers: ~50ns per handler
- Total: ~200ns per tile check (10x slower)

But: Event pooling reduces to ~155ns (8x slower)
```

**Verdict**: Current approach is **faster** for collision checks (called frequently).

However:
- 155ns vs 20ns difference = 0.000135ms
- Even checking 100 tiles per frame = 0.0135ms overhead
- Well within 16.67ms frame budget (60 FPS)

---

## Trade-offs Summary

### Current (TileBehaviorScriptBase) - Pros ✅

1. **Faster**: Direct method calls (10ns vs 155ns)
2. **Type-safe**: Compiler enforces tile-specific methods
3. **Self-documenting**: Inheritance shows "this is a tile behavior"
4. **Simpler system code**: Just call methods
5. **Better IntelliSense**: Shows exactly 7 relevant tile methods

### Current - Cons ❌

1. **Polling-like**: Systems must query methods
2. **Less composable**: Can't easily combine multiple behaviors per tile
3. **Separate base class**: One more class to learn

### Unified (ScriptBase) - Pros ✅

1. **Consistent API**: Same base class for all script types
2. **Event-driven**: Scripts react instead of being queried
3. **Composable**: Multiple scripts can attach to same tile
4. **Easier testing**: Mock events instead of entire ScriptContext
5. **More concise**: Can use lambdas (22 lines vs 42)

### Unified - Cons ❌

1. **Slower**: Event overhead (155ns vs 10ns per check)
2. **Runtime type safety**: No compile-time enforcement
3. **More complex system code**: Event creation and publishing
4. **Learning curve**: Event patterns vs simple method overrides
5. **API pollution**: NPC scripts see tile events (not relevant to them)

---

## Migration Checklist

If you decide to migrate to unified approach:

- [ ] Create event type definitions (CollisionCheckEvent, JumpCheckEvent, etc.)
- [ ] Update TileBehaviorSystem to publish events instead of calling methods
- [ ] Create ScriptBase with event subscription helpers
- [ ] Migrate 47 tile behavior scripts (automated script possible)
- [ ] Test each script after migration
- [ ] Update documentation
- [ ] Notify mod developers of breaking change
- [ ] Provide migration tool/guide

**Estimated Effort**: 3-5 days for all 47 scripts + system changes

---

## Recommendation

### For Jump Scripts Specifically:

**Keep TileBehaviorScriptBase** because:

1. Jump/collision checks happen **frequently** (every movement attempt)
2. Performance matters: 10ns vs 155ns × 60 FPS × multiple checks = measurable
3. Type safety prevents bugs (compiler catches missing methods)
4. Current code is already clean and simple
5. Only 42 lines - not worth 3-5 days migration effort

### Alternative: Hybrid Approach

Keep `TileBehaviorScriptBase` but add **optional** event support:

```csharp
public abstract class TileBehaviorScriptBase : TypeScriptBase
{
    // EXISTING: Virtual methods (unchanged)
    public virtual bool IsBlockedFrom(ScriptContext ctx, Direction from, Direction to) => false;
    public virtual Direction GetJumpDirection(ScriptContext ctx, Direction from) => Direction.None;

    // NEW: Optional event support
    public override void RegisterEventHandlers(ScriptContext ctx) { }
}
```

**Scripts can choose**:
- Use virtual methods (performance)
- Use events (composability)
- Use both (hybrid)

---

## Conclusion

**For `jump_south.csx`**: The current implementation is **optimal**.

- ✅ Simple and clear
- ✅ Fast (10ns method calls)
- ✅ Type-safe
- ✅ Self-documenting

**Migration would**:
- ❌ Make it 8x slower (155ns)
- ❌ Lose compile-time type safety
- ❌ Require 3-5 days effort for all tile scripts
- ✅ Make it 20 lines shorter (not worth the trade-offs)

**The current architecture is already excellent for tile behaviors.**

If you want unified scripting, focus on **NPC behaviors** where event-driven patterns shine (less frequent calls, more complex state machines, better composability).

---

**Question**: Do you want to see the NPC script migration comparison? NPC behaviors might benefit more from unification than tile behaviors do.
