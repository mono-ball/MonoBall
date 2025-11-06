# Patrol Script Comparison: Legacy vs Flyweight

## The Critical Difference

**`patrol_behavior.csx`** (LEGACY - HAS THE BUG) vs **`patrol_flyweight.csx`** (NEW - FIXED)

---

## patrol_behavior.csx - ❌ LEGACY (State Corruption Bug)

**Location:** `PokeSharp.Game/Assets/Scripts/Behaviors/patrol_behavior.csx`

```csharp
public class PatrolBehavior : TypeScriptBase
{
    private int _currentWaypoint = 0;     // ❌ BUG: Shared across ALL NPCs!
    private float _waitTimer = 0f;         // ❌ BUG: Shared state!

    public override void OnTick(float deltaTime)
    {
        // Uses instance fields (WRONG - shared singleton)
        if (_waitTimer > 0)
        {
            _waitTimer -= deltaTime;      // ❌ All NPCs decrement same timer!
            return;
        }

        _currentWaypoint++;               // ❌ All NPCs increment same waypoint!
    }
}

new PatrolBehavior()  // ❌ ONE instance shared by ALL NPCs
```

### The Problem

**ONE instance is shared by ALL NPCs** → All NPCs with patrol behavior share `_currentWaypoint` and `_waitTimer`.

**Example bug scenario:**
```
Frame 1:
- NPC_A (Guard) ticks → _currentWaypoint = 1, _waitTimer = 2.0
- NPC_B (Trainer) ticks → _currentWaypoint = 2, _waitTimer = 1.5  ← Overwrites NPC_A's state!

Frame 2:
- NPC_A expects _currentWaypoint = 1, but gets 2  ← STATE CORRUPTION!
```

**Result:** NPCs teleport between waypoints, timers behave erratically, random behavior.

---

## patrol_flyweight.csx - ✅ NEW (Flyweight Pattern - Fixed)

**Location:** `PokeSharp.Game/Assets/Scripts/Behaviors/patrol_flyweight.csx`

```csharp
public class PatrolBehaviorScript : IBehaviorLogic
{
    // NO instance fields!  ✅ Stateless singleton (shared safely)

    public void OnActivated(World world, Entity entity)
    {
        // Create per-entity state component
        world.Add(entity, new PatrolState
        {
            CurrentWaypoint = 0,    // ✅ Each NPC has its own!
            WaitTimer = 0f,          // ✅ Each NPC has its own!
            WaitDuration = 1.0f,
            Speed = 4.0f
        });
    }

    public void OnTick(World world, Entity entity, float deltaTime)
    {
        // Get THIS entity's state (not shared)
        ref var state = ref world.Get<PatrolState>(entity);  // ✅ Per-NPC state!

        if (state.WaitTimer > 0)
        {
            state.WaitTimer -= deltaTime;    // ✅ Only affects THIS NPC!
        }

        state.CurrentWaypoint++;              // ✅ Only affects THIS NPC!
    }
}

return new PatrolBehaviorScript();  // ✅ ONE shared instance (stateless), safe!
```

### The Solution

**Shared logic (singleton) + Per-entity state (component)**

**PatrolState component** (each NPC has its own):
```csharp
public struct PatrolState
{
    public int CurrentWaypoint;   // ✅ Separate for each NPC
    public float WaitTimer;        // ✅ Separate for each NPC
    public float WaitDuration;
    public float Speed;
}
```

**Example correct scenario:**
```
Frame 1:
- NPC_A (Guard):
    state_A.CurrentWaypoint = 1, state_A.WaitTimer = 2.0  ✅ Independent
- NPC_B (Trainer):
    state_B.CurrentWaypoint = 2, state_B.WaitTimer = 1.5  ✅ Independent

Frame 2:
- NPC_A: state_A.CurrentWaypoint = 1  ✅ Still correct!
- NPC_B: state_B.CurrentWaypoint = 2  ✅ Still correct!
```

**Result:** Each NPC has independent waypoint tracking and timing. No interference.

---

## Side-by-Side Comparison

| Aspect | patrol_behavior.csx (LEGACY) | patrol_flyweight.csx (NEW) |
|--------|------------------------------|----------------------------|
| **Base Class** | `TypeScriptBase` | `IBehaviorLogic` |
| **Instance Fields** | ❌ `_currentWaypoint`, `_waitTimer` | ✅ None (stateless) |
| **State Storage** | ❌ Instance fields (shared) | ✅ `PatrolState` component (per-entity) |
| **Bug** | ❌ State corruption (all NPCs share state) | ✅ No bug (isolated state) |
| **Pattern** | ❌ Singleton anti-pattern | ✅ Flyweight pattern |
| **Performance** | Same | Same |
| **Hot-Reload** | ✅ Supported | ✅ Supported |
| **Backward Compatible** | ✅ Yes (deprecated) | ✅ Yes (preferred) |

---

## Migration Guide

### Step 1: Identify State Fields

**Legacy script:**
```csharp
public class MyBehavior : TypeScriptBase
{
    private int _state1;      // ← Move to component
    private float _state2;    // ← Move to component
    private bool _state3;     // ← Move to component
}
```

### Step 2: Create State Component

```csharp
// In PokeSharp.Core/Components/BehaviorStates.cs
public struct MyBehaviorState
{
    public int State1;
    public float State2;
    public bool State3;
}
```

### Step 3: Refactor to IBehaviorLogic

**New script:**
```csharp
public class MyBehavior : IBehaviorLogic
{
    // No fields!

    public void OnActivated(World world, Entity entity)
    {
        world.Add(entity, new MyBehaviorState
        {
            State1 = 0,
            State2 = 0f,
            State3 = false
        });
    }

    public void OnTick(World world, Entity entity, float deltaTime)
    {
        ref var state = ref world.Get<MyBehaviorState>(entity);
        state.State1++;  // Per-entity state!
    }

    public void OnDeactivated(World world, Entity entity)
    {
        world.Remove<MyBehaviorState>(entity);
    }
}
```

---

## System Behavior

**NpcBehaviorSystem** checks in this order:

```csharp
// Line 88-106 in NpcBehaviorSystem.cs
var behaviorLogic = _behaviorRegistry.GetBehaviorLogic(behavior.BehaviorTypeId);
if (behaviorLogic != null)
{
    // ✅ PREFERRED: Uses flyweight pattern (patrol_flyweight.csx)
    behaviorLogic.OnTick(world, entity, deltaTime);
    return;
}

// ❌ LEGACY: Falls back to TypeScriptBase (patrol_behavior.csx)
// Issues deprecation warning
var scriptInstance = _behaviorRegistry.GetBehavior(behavior.BehaviorTypeId);
```

**Warning logged:**
```
Consider migrating {TypeId} to IBehaviorLogic to prevent state corruption
```

---

## Why Keep patrol_behavior.csx?

**Backward compatibility during migration:**

1. Existing save files reference `"patrol_behavior"` type ID
2. Old mods may still use TypeScriptBase pattern
3. Gradual migration path (no breaking changes)

**Deprecation plan:**
- ✅ Phase 1: Both patterns work (current)
- ⚠️ Phase 2: Warnings for TypeScriptBase usage (current)
- 🔜 Phase 3: Auto-migration tool for save files
- 🚫 Phase 4: Remove TypeScriptBase support (future)

---

## Recommendations

### For Vanilla Behaviors
Use **compiled C#** (`BehaviorLogic/PatrolBehaviorLogic.cs`) for:
- Core game behaviors
- Best performance
- Type safety

### For Modded Behaviors
Use **patrol_flyweight.csx** pattern for:
- Custom mod behaviors
- Hot-reload support
- No state corruption bug

### Do NOT Use
**patrol_behavior.csx** pattern (TypeScriptBase):
- ❌ Has state corruption bug
- ❌ Deprecated
- ❌ Only for backward compatibility

---

## Testing the Difference

Create two NPCs with patrol behavior:

**With patrol_behavior.csx (LEGACY - BROKEN):**
```csharp
NPC_A: Patrol waypoints [0,0] → [5,5] → [10,10]
NPC_B: Patrol waypoints [20,20] → [25,25]

Result: ❌ Both NPCs share _currentWaypoint
- NPC_A reaches waypoint 1, increments _currentWaypoint to 1
- NPC_B ticks, reads _currentWaypoint = 1, skips to waypoint 1 (wrong!)
```

**With patrol_flyweight.csx (NEW - CORRECT):**
```csharp
NPC_A: Patrol waypoints [0,0] → [5,5] → [10,10]
NPC_B: Patrol waypoints [20,20] → [25,25]

Result: ✅ Each NPC has its own PatrolState component
- NPC_A: state_A.CurrentWaypoint = 0, 1, 2... (independent)
- NPC_B: state_B.CurrentWaypoint = 0, 1... (independent)
```

---

## Summary

| File | Status | Use Case |
|------|--------|----------|
| `patrol_behavior.csx` | ❌ **DEPRECATED** | Legacy compatibility only |
| `patrol_flyweight.csx` | ✅ **RECOMMENDED** | New modded behaviors |
| `BehaviorLogic/PatrolBehaviorLogic.cs` | ✅ **BEST** | Vanilla game behaviors |

**The bug:** Instance fields in `TypeScriptBase` are shared by all NPCs.
**The fix:** Flyweight pattern with per-entity state components.
