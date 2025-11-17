# Hive Researcher: Tile Behavior Roslyn Integration - Gap Analysis

**Researcher Agent Analysis**
**Date:** 2025-11-16
**Document Analyzed:** TILE_BEHAVIOR_ROSLYN_INTEGRATION_RESEARCH.md
**Mission:** Identify missing considerations, gaps, and unstated assumptions

---

## Executive Summary

The Roslyn integration proposal is **well-structured** and follows good design patterns, but contains **18 critical gaps** across architecture, performance, feature coverage, and migration complexity. Most concerning are:

1. **Missing 75+ behavior types** from Pokemon Emerald (only 8 covered)
2. **No elevation/multi-layer system** (critical for dive/surf mechanics)
3. **Incomplete collision model** (missing double-ledge, elevation checks)
4. **No state persistence** for tile behaviors (ice cracking, floor breaking)
5. **Performance concerns** not addressed (script execution per movement)

---

## Gap Category 1: Incomplete Pokemon Emerald Behavior Coverage

### 🔴 CRITICAL: 75+ Missing Behavior Types

**Current Coverage (8 behaviors):**
- ✅ `jump_south` (ledge)
- ✅ `ice` (forced movement)
- ✅ `impassable_east` (directional blocking)

**Missing Categories (237 behaviors):**

#### 1.1 Water Behaviors (15+ types)
**From TILE_BEHAVIOR_EVALUATION.md lines 40-68:**
- `MB_POND_WATER` (0x10) - Surfable, fishable, encounters, ripples
- `MB_INTERIOR_DEEP_WATER` (0x11) - Surfable, diveable, encounters
- `MB_DEEP_WATER` (0x12) - Surfable, diveable, encounters
- `MB_WATERFALL` (0x13) - Surfable, forced movement
- `MB_SOOTOPOLIS_DEEP_WATER` (0x14) - Reflective, ripples
- `MB_OCEAN_WATER` (0x15) - Surfable, fishable, encounters
- `MB_PUDDLE` (0x16) - Reflective, ripples (no surf)
- `MB_SHALLOW_WATER` (0x17) - Flowing effects
- `MB_NO_SURFACING` (0x19) - Dive-only, cannot surface
- `MB_SEAWEED` (0x22) - Surfable, encounters
- `MB_SEAWEED_NO_SURFACING` (0x2A) - Dive-only seaweed

**Missing Considerations:**
- **Dive mechanics** - Not mentioned in proposal (lines 50-51, 67)
- **Surfacing restrictions** - `NO_SURFACING` behavior (lines 50, 67)
- **Visual effects** - Ripples, reflections, flowing water (lines 42, 47, 56, 67)
- **Fishing eligibility** - Some water is fishable (lines 42, 46, 56, 122-125)
- **Underwater vs surface** - Separate elevation layers (line 51)

**Impact:** Cannot implement Gen 3 water mechanics without these behaviors.

#### 1.2 Encounter Behaviors (13 types)
**From TILE_BEHAVIOR_EVALUATION.md lines 272-276:**
- `MB_TALL_GRASS` (0x02) - Cuttable, encounters
- `MB_LONG_GRASS` (0x03) - Cuttable, encounters, no running
- `MB_DEEP_SAND` (0x06) - Encounters
- `MB_CAVE` (0x08) - Encounters
- `MB_INDOOR_ENCOUNTER` (0x0B) - Indoor encounters
- `MB_ASHGRASS` (0x24) - Cuttable, encounters
- `MB_FOOTPRINTS` (0x25) - Encounters (unused)

**Missing Considerations:**
- **Wild encounter triggering** - How do behaviors trigger encounters?
- **Encounter rate modifiers** - Different rates per behavior
- **HM integration** - Cut interaction with grass behaviors (line 382)
- **Visual effects** - Grass rustling animations
- **Multi-tile grass** - Edge behaviors (line 34, `MB_LONG_GRASS_SOUTH_EDGE`)

**Impact:** Cannot implement wild Pokemon system without encounter behaviors.

#### 1.3 Door/Warp Behaviors (15+ types)
**From TILE_BEHAVIOR_EVALUATION.md lines 127-150:**
- `MB_NON_ANIMATED_DOOR` (0x5F) - Instant warp
- `MB_ANIMATED_DOOR` (0x68) - Opens before warp
- `MB_LADDER` (0x60) - Warp up/down
- `MB_EAST_ARROW_WARP` (0x61) - Directional warp
- `MB_CRACKED_FLOOR_HOLE` (0x65) - Warp down
- `MB_AQUA_HIDEOUT_WARP` (0x66) - Special warp
- `MB_WATER_DOOR` (0x6B) - Surfable warp (has bug)
- `MB_PETALBURG_GYM_DOOR` (0x8E) - Conditional door
- `MB_CLOSED_SOOTOPOLIS_DOOR` (0x8F) - Event-locked door
- `MB_SKY_PILLAR_CLOSED_DOOR` (0xE8) - Conditional door

**Missing Considerations:**
- **Animation timing** - Door opening animations before warp
- **Warp destination data** - Where do behaviors store warp targets?
- **Event flags** - Doors locked by story progress
- **Sound effects** - Door sounds, warp sounds
- **Two-way warps** - Entering/exiting doors
- **Map transitions** - Cross-map warping

**Impact:** Cannot implement dungeon/building navigation without warp behaviors.

#### 1.4 Bridge Behaviors (13 types)
**From TILE_BEHAVIOR_EVALUATION.md lines 152-171:**
- `MB_BRIDGE_OVER_OCEAN` (0x6F) - Also used for Union Room warp
- `MB_BRIDGE_OVER_POND_LOW/MED/HIGH` (0x70-0x72) - Elevation variants
- `MB_PACIFIDLOG_VERTICAL/HORIZONTAL_LOG` (0x73-0x76) - No running
- `MB_FORTREE_BRIDGE` (0x77) - Special bridge
- `MB_BIKE_BRIDGE_OVER_BARRIER` (0x7E) - Requires bike

**Missing Considerations:**
- **Elevation/layer system** - Bridges are above water (lines 157-159)
- **Multi-tile bridges** - Edge variants (lines 166-169)
- **Conditional access** - Bike requirement (line 171)
- **Visual layering** - Reflection under bridge (line 68, `MB_REFLECTION_UNDER_BRIDGE`)

**Impact:** Cannot implement multi-elevation maps without bridge support.

#### 1.5 Secret Base Behaviors (35+ types)
**From TILE_BEHAVIOR_EVALUATION.md lines 194-236:**
- Secret base entrances (7 types, open/closed variants)
- Decoration tiles (10+ types)
- Furniture interactions (PC, register, trainer spot)
- Special mats (jump, spin, glitter, sound)
- Walls, balloons, posters, holes

**Missing Considerations:**
- **Secret base system integration** - Entire feature missing
- **Decoration placement** - How do behaviors track decorations?
- **Trainer battles** - Trainer spot interactions
- **Record mixing** - Register PC behavior
- **Forced movement mats** - Jump/spin/glitter mats (lines 225-227)

**Impact:** Cannot implement Gen 3 secret bases without these behaviors.

#### 1.6 Interactive Object Behaviors (13+ types)
**From TILE_BEHAVIOR_EVALUATION.md lines 173-193, 250-260:**
- `MB_COUNTER` (0x7F) - Shop interaction
- `MB_PC` (0x82) - PC interaction
- `MB_REGION_MAP` (0x84) - Map interaction
- `MB_TELEVISION` (0x85) - TV interaction
- `MB_SLOT_MACHINE` (0x88) - Game corner
- `MB_BOOKSHELF` (0xE1) - Item finding
- `MB_TRASH_CAN` (0xE4) - Item finding
- `MB_SHOP_SHELF` (0xE5) - Visual interaction

**Missing Considerations:**
- **Interaction callbacks** - How do behaviors trigger scripts?
- **Facing direction** - Must face object to interact
- **Multi-tile objects** - Objects spanning multiple tiles
- **Item finding** - Random items in trash cans
- **TV programs** - Dynamic TV content

**Impact:** Cannot implement building interiors without interactive objects.

#### 1.7 Special Terrain Behaviors (10+ types)
**From TILE_BEHAVIOR_EVALUATION.md lines 238-248:**
- `MB_MUDDY_SLOPE` (0xCF) - Forced movement
- `MB_BUMPY_SLOPE` (0xD0) - Acro bike tricks
- `MB_CRACKED_FLOOR` (0xD1) - Breaks after steps
- `MB_THIN_ICE` (0x26) - Can break
- `MB_CRACKED_ICE` (0x27) - Broken ice
- `MB_HOT_SPRINGS` (0x28) - No running
- Rails (4 types) - Acro bike tricks

**Missing Considerations:**
- **State changes over time** - Ice cracking, floor breaking
- **Per-step callbacks** - Counting steps on cracked floor (lines 310-322)
- **Item requirements** - Acro bike for tricks (lines 283-304)
- **Particle effects** - Steam from hot springs

**Impact:** Cannot implement Gen 3 terrain gimmicks without state tracking.

#### 1.8 Forced Movement Variants (18 types)
**From TILE_BEHAVIOR_EVALUATION.md lines 102-125:**
- Walk tiles (4 directions) - Forces single step
- Slide tiles (4 directions) - Forces sliding
- Current tiles (4 directions) - Water currents
- Trick House puzzle floor - Special sliding
- Escalators (up/down) - Forced movement

**Missing Considerations:**
- **Slide distance** - How far does sliding continue?
- **Stopping conditions** - When does sliding stop?
- **Visual feedback** - Slide animations, dust particles
- **Control lockout** - Player cannot control during slide

**Impact:** Proposal only covers basic ice; missing 17 other forced movement types.

#### 1.9 Multi-Directional Jump Tiles (8 types)
**From TILE_BEHAVIOR_EVALUATION.md lines 89-100:**
- `MB_JUMP_NORTHEAST` (0x3F) - Diagonal jump
- `MB_JUMP_NORTHWEST` (0x40) - Diagonal jump
- `MB_JUMP_SOUTHEAST` (0x41) - Diagonal jump
- `MB_JUMP_SOUTHWEST` (0x42) - Diagonal jump
- Plus 4 cardinal direction jumps

**Missing Considerations:**
- **Diagonal jump landing** - 2-tile diagonal jumps
- **Jump distance calculation** - How far is each jump?
- **Multiple jump types** - Different behaviors for different distances

**Impact:** Proposal only covers cardinal jumps; missing diagonal variants.

---

## Gap Category 2: Missing Architecture Considerations

### 🔴 CRITICAL: No Elevation/Multi-Layer System

**Problem:**
Pokemon Emerald uses elevation extensively for dive/surf mechanics, bridges, and multi-layer maps.

**Evidence from Documents:**
- **TILE_BEHAVIOR_MOVEMENT_COLLISION.md line 39:**
  ```c
  else if (IsElevationMismatchAt(objectEvent->currentElevation, x, y))
      return COLLISION_ELEVATION_MISMATCH;
  ```
- **Bridge behaviors** require elevation (lines 157-159)
- **Dive mechanic** uses underwater elevation (line 51)
- **Reflection under bridge** implies layering (line 68)

**Missing from Proposal:**
- No `Elevation` component mentioned
- No elevation checking in `IsMovementBlocked()`
- No underwater layer support
- No bridge-over-water collision logic

**Impact:**
Cannot implement:
- Dive/surface mechanics
- Bridges over water
- Multi-floor dungeons
- Correct collision for elevated entities

**Recommended Addition:**
```csharp
public abstract class TileBehaviorScriptBase
{
    // ADD THIS HOOK
    public virtual byte GetElevation(ScriptContext ctx)
    {
        return Elevation.Default; // 0 = ground, 1 = bridge, 2 = underwater
    }

    // ADD THIS HOOK
    public virtual bool AllowElevationMismatch(ScriptContext ctx, byte entityElevation, byte tileElevation)
    {
        return false; // Default: require elevation match
    }
}
```

### 🟡 MODERATE: No Multi-Tile Entity Support

**Problem:**
Pokemon Emerald behaviors can affect entities larger than 1x1 tiles (e.g., player during multi-tile jump).

**Evidence:**
- Jump ledges move player 2 tiles (TILE_BEHAVIOR_MOVEMENT_COLLISION.md lines 459-468)
- Diagonal jumps move 2 tiles diagonally (lines 95-100)
- Some objects span multiple tiles (interactive objects)

**Missing from Proposal:**
- No multi-tile jump distance support
- No landing position calculation
- No check for valid landing tile

**Impact:**
Cannot implement jumps that span more than 1 tile.

**Recommended Addition:**
```csharp
public abstract class TileBehaviorScriptBase
{
    // ADD THIS HOOK
    public virtual int GetJumpDistance(ScriptContext ctx, Direction jumpDirection)
    {
        return 1; // Default: 1 tile jump (ledge is 2 tiles)
    }
}
```

### 🟡 MODERATE: No Behavior State Persistence

**Problem:**
Some behaviors have state that changes over time (cracked floor, thin ice).

**Evidence from TILE_BEHAVIOR_MOVEMENT_COLLISION.md lines 310-322:**
```c
// Cracked floor breaks after 3 steps
if (MetatileBehavior_IsCrackedFloor(behavior))
{
    tFloor1Delay = 3;  // State: step counter
    tFloor1X = x;
    tFloor1Y = y;
}
```

**Missing from Proposal:**
- No state storage mechanism
- No per-step counter
- No behavior mutation (changing from cracked to hole)

**Recommended Addition:**
```csharp
public struct TileBehavior
{
    // ADD STATE STORAGE
    public Dictionary<string, object>? State { get; set; }

    public int StepCounter { get; set; } // For cracked floors
}

public abstract class TileBehaviorScriptBase
{
    // ADD STATE HOOKS
    public virtual void OnStep(ScriptContext ctx, Entity entity)
    {
        // Access: ctx.GetState<int>("stepCounter")
    }

    public virtual void OnStateChange(ScriptContext ctx, string key, object oldValue, object newValue)
    {
        // React to state changes
    }
}
```

### 🟡 MODERATE: Missing Camera Movement Restrictions

**Problem:**
Pokemon Emerald checks if camera can follow player before allowing movement.

**Evidence from TILE_BEHAVIOR_MOVEMENT_COLLISION.md line 35:**
```c
else if (objectEvent->trackedByCamera && !CanCameraMoveInDirection(direction))
    return COLLISION_IMPASSABLE;
```

**Missing from Proposal:**
- No camera boundary checks
- No camera-based collision

**Impact:**
Player could move off-screen or into invalid camera zones.

**Recommended Addition:**
```csharp
// In CollisionService
if (entity.Has<CameraTracking>())
{
    if (!_cameraService.CanMoveInDirection(direction))
        return false;
}
```

### 🟢 MINOR: No Object-to-Object Collision via Behaviors

**Problem:**
Pokemon Emerald checks both tile AND object collision.

**Evidence from TILE_BEHAVIOR_MOVEMENT_COLLISION.md lines 42-44:**
```c
else if (DoesObjectCollideWithObjectAt(objectEvent, x, y))
    return COLLISION_OBJECT_EVENT;
```

**Missing from Proposal:**
- Behaviors only check tile collision
- No entity-to-entity collision in behavior scripts

**Note:** This might be intentional (separate collision systems), but should be clarified.

---

## Gap Category 3: Missing Lifecycle Hooks

### 🟡 MODERATE: No OnEnter/OnExit Tile Hooks

**Problem:**
Behaviors need to know when entity enters/exits the tile.

**Use Cases:**
- Start/stop water current effects when entering/exiting current tile
- Start/stop ice sliding when entering/exiting ice
- Trigger sound effects (splash when entering water)
- Visual effects (grass rustling when entering grass)

**Missing from Proposal:**
```csharp
public abstract class TileBehaviorScriptBase
{
    // EXISTING: Only OnStep (every frame on tile)
    public virtual void OnStep(ScriptContext ctx, Entity entity) { }

    // MISSING HOOKS:
    public virtual void OnEnter(ScriptContext ctx, Entity entity) { }
    public virtual void OnExit(ScriptContext ctx, Entity entity) { }
    public virtual void OnLand(ScriptContext ctx, Entity entity) { } // After jump
}
```

**Impact:**
Cannot implement:
- Entry/exit sound effects
- State cleanup when leaving tile
- One-time triggers

### 🟡 MODERATE: No Animation/Visual Effect Hooks

**Problem:**
Many behaviors have visual effects (ripples, reflections, grass rustling).

**Evidence from TILE_BEHAVIOR_EVALUATION.md:**
- Ripples on water (lines 42, 47, 56)
- Reflections (lines 45, 56, 68)
- Grass animations (implied from grass behaviors)

**Missing from Proposal:**
```csharp
public abstract class TileBehaviorScriptBase
{
    // MISSING HOOKS:
    public virtual string? GetVisualEffect(ScriptContext ctx) { return null; }
    public virtual bool HasReflection(ScriptContext ctx) { return false; }
    public virtual string? GetFootstepSound(ScriptContext ctx) { return null; }
}
```

**Impact:**
Cannot implement water ripples, reflections, grass rustling without visual hooks.

### 🟢 MINOR: No Behavior Activation/Deactivation Events

**Problem:**
Behaviors need event callbacks for activation changes.

**Current Proposal:**
```csharp
public struct TileBehavior
{
    public bool IsActive { get; set; } // Can change at runtime
}
```

**Missing:**
- No `OnActivated()` callback when `IsActive` becomes true
- No `OnDeactivated()` callback when `IsActive` becomes false

**Impact:**
Behaviors cannot react to being enabled/disabled dynamically.

---

## Gap Category 4: Performance and Optimization Concerns

### 🔴 CRITICAL: Script Execution on Every Movement Check

**Problem:**
Proposal calls behavior scripts during `IsPositionWalkable()` which is called **per movement attempt**.

**Current Proposal (TILE_BEHAVIOR_ROSLYN_INTEGRATION_RESEARCH.md lines 530-544):**
```csharp
public bool IsPositionWalkable(...)
{
    foreach (var entity in entities)
    {
        if (entity.Has<TileBehavior>())
        {
            // PERFORMANCE ISSUE: Executes Roslyn script per collision check
            if (_tileBehaviorSystem.IsMovementBlocked(...))
            {
                return false;
            }
        }
    }
}
```

**Performance Impact:**
- Player tries to move: 1 collision check
- NPC pathfinding: 100+ collision checks per path
- Auto-pathing: 1000+ collision checks per A* search

**Missing Optimization:**
- No caching of script results
- No flag-based fast path
- No precomputed collision map

**Recommended Solutions:**

**Option 1: Flag-Based Fast Path**
```csharp
// Check flags BEFORE executing script
if (definition.Flags.HasFlag(TileBehaviorFlags.BlocksMovement))
{
    // Fast path: always blocked, no script needed
    return false;
}

// Only call script for complex behaviors
if (script.IsBlockedFrom(...))
    return false;
```

**Option 2: Cached Collision Map**
```csharp
// Precompute collision for common cases
private readonly Dictionary<(int x, int y, Direction), bool> _collisionCache = new();

public bool IsPositionWalkable(...)
{
    var cacheKey = (tileX, tileY, fromDirection);
    if (_collisionCache.TryGetValue(cacheKey, out var cached))
        return !cached; // Use cached result

    // ... execute script only on cache miss
}
```

**Option 3: Behavior Tiers**
```csharp
public enum BehaviorComplexity
{
    Simple,   // Always blocked/allowed (use flags only)
    Static,   // Context-dependent but cacheable
    Dynamic   // Must execute script every time
}

// Only execute scripts for Dynamic behaviors
```

### 🟡 MODERATE: No Script Compilation Caching Strategy

**Problem:**
Proposal mentions caching but doesn't specify **when** to invalidate cache.

**Current Proposal (line 246):**
```csharp
private readonly Dictionary<string, TileBehaviorScriptBase> _scriptCache = new();
```

**Missing Details:**
- When is cache invalidated?
- How are script hotswaps handled?
- What happens if script file changes?
- Memory limits on cache size?

**Recommended Addition:**
```csharp
public class TileBehaviorSystem
{
    private readonly Dictionary<string, (TileBehaviorScriptBase script, DateTime loadTime)> _scriptCache;

    // ADD: Cache invalidation policy
    public void InvalidateCache(string behaviorTypeId) { }

    // ADD: Hot reload support
    public void ReloadScript(string behaviorTypeId) { }

    // ADD: Cache size limits
    private const int MaxCachedScripts = 100;
}
```

### 🟢 MINOR: No Batch Processing Optimization

**Problem:**
Proposal processes tiles one-by-one instead of batching.

**Current Proposal (lines 250-276):**
```csharp
world.Query(
    in EcsQueries.TilesWithBehaviors,
    (Entity entity, ref TileBehavior behavior) =>
    {
        // Process each tile individually
    }
);
```

**Optimization Opportunity:**
```csharp
// Batch similar behaviors together
var behaviorGroups = world
    .Query<TileBehavior>()
    .GroupBy(b => b.BehaviorTypeId);

foreach (var group in behaviorGroups)
{
    var script = GetOrLoadScript(group.Key);
    foreach (var (entity, behavior) in group)
    {
        // Process all tiles with same behavior using same script instance
        script.OnTick(context, deltaTime);
    }
}
```

---

## Gap Category 5: Integration Issues

### 🟡 MODERATE: Relationship Between TileBehavior and TileScript Unclear

**Problem:**
Proposal doesn't clarify how `TileBehavior` (movement/collision) relates to `TileScript` (interaction).

**Current System (mentioned in proposal):**
- `TileScript` - For interaction scripts (PC, heal tiles)
- `TileBehavior` (proposed) - For movement/collision

**Unanswered Questions:**
1. Can a tile have both `TileBehavior` AND `TileScript`?
2. If yes, which takes priority?
3. Can behaviors trigger interactions (e.g., door warp)?
4. Can interactions modify behaviors (e.g., cutting grass removes behavior)?

**Missing Specification:**
```csharp
// How do these interact?
var tile = GetTileAt(x, y);

// Case 1: Tile has both
tile.Has<TileBehavior>(); // true (for collision)
tile.Has<TileScript>();   // true (for interaction)

// Case 2: Behavior triggers script?
public abstract class TileBehaviorScriptBase
{
    // MISSING: Interaction hook
    public virtual void OnInteract(ScriptContext ctx, Entity interactor) { }
}
```

**Recommended Clarification:**
Add section: "Behavior vs Script: Division of Responsibilities"
- `TileBehavior`: Passive (collision, forced movement, encounters)
- `TileScript`: Active (interactions requiring player input)
- Both can coexist on same tile

### 🟡 MODERATE: No Migration Strategy for Existing Maps

**Problem:**
Proposal has "Migration Path" but lacks specifics for existing map data.

**Research Question (line 768):**
> How many existing maps use `TileLedge`?

**This question is unanswered.**

**Missing Details:**
1. How many maps have `TileLedge` components?
2. Are there tools to auto-convert?
3. What is the data migration format?
4. How are behaviors stored in map files?

**Recommended Addition:**
```csharp
// Tool: Convert old TileLedge to new TileBehavior
public class TileBehaviorMigrationTool
{
    public void ConvertLedgeToJumpBehavior(Entity tileEntity)
    {
        if (!tileEntity.Has<TileLedge>())
            return;

        ref var ledge = ref tileEntity.Get<TileLedge>();

        // Map ledge direction to behavior type
        var behaviorTypeId = ledge.JumpDirection switch
        {
            Direction.North => "jump_north",
            Direction.South => "jump_south",
            Direction.East => "jump_east",
            Direction.West => "jump_west",
            _ => throw new InvalidOperationException()
        };

        // Add behavior component
        tileEntity.Set(new TileBehavior(behaviorTypeId));

        // Remove old component
        tileEntity.Remove<TileLedge>();
    }
}
```

### 🟢 MINOR: No Error Handling for Script Failures

**Problem:**
What happens if a behavior script throws an exception?

**Missing from Proposal:**
- Exception handling in script execution
- Fallback behavior on script error
- Logging strategy for script errors

**Recommended Addition:**
```csharp
public Direction GetJumpDirection(...)
{
    try
    {
        var script = GetOrLoadScript(behavior.BehaviorTypeId);
        return script.GetJumpDirection(context, fromDirection);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Script error in behavior {BehaviorTypeId}", behavior.BehaviorTypeId);

        // Fallback: Disable behavior on error
        behavior.IsActive = false;
        return Direction.None;
    }
}
```

---

## Gap Category 6: Unstated Assumptions

### 🟡 MODERATE: Assumption - Single Behavior Per Tile

**Unstated Assumption:**
Proposal assumes each tile has only ONE behavior.

**Component Design (line 48):**
```csharp
public struct TileBehavior
{
    public string BehaviorTypeId { get; set; } // SINGULAR
}
```

**Pokemon Emerald Reality:**
- Tiles have ONE behavior ID
- But behaviors can have MULTIPLE effects:
  - `MB_POND_WATER`: Surfable + Encounters + Fishable + Ripples
  - `MB_LONG_GRASS`: Encounters + Cuttable + Disables Running

**Issue:**
If each effect is a separate script, how do you combine them?

**Options:**
1. **Monolithic scripts** - Each script handles all effects (harder to reuse)
2. **Component composition** - Multiple `TileBehavior` components per tile
3. **Behavior inheritance** - Scripts inherit from multiple bases

**Recommended Clarification:**
Specify whether tiles can have multiple behaviors or if behaviors should be monolithic.

### 🟡 MODERATE: Assumption - Static Behavior Assignments

**Unstated Assumption:**
Behaviors are assigned at map load and don't change.

**Counter-Examples from Pokemon Emerald:**
1. **Cut grass** - Behavior changes from `MB_TALL_GRASS` to `MB_NORMAL` after Cut
2. **Cracked floor** - Changes from `MB_CRACKED_FLOOR` to `MB_CRACKED_FLOOR_HOLE`
3. **Thin ice** - Changes from `MB_THIN_ICE` to `MB_CRACKED_ICE` to `MB_NORMAL`
4. **Secret base doors** - Toggle between open/closed variants

**Missing Specification:**
How do behaviors change at runtime?

**Recommended Addition:**
```csharp
public abstract class TileBehaviorScriptBase
{
    // ADD: Runtime behavior mutation
    public virtual string? GetNextBehavior(ScriptContext ctx, string trigger)
    {
        return null; // Return new behavior type ID, or null for no change
    }
}

// Usage
public void CutGrass(Entity tile)
{
    ref var behavior = ref tile.Get<TileBehavior>();
    var script = GetScript(behavior.BehaviorTypeId);

    var nextBehavior = script.GetNextBehavior(context, "cut");
    if (nextBehavior != null)
    {
        behavior.BehaviorTypeId = nextBehavior; // Change behavior
        behavior.IsInitialized = false; // Reinitialize
    }
}
```

### 🟢 MINOR: Assumption - Behaviors Don't Need World State

**Unstated Assumption:**
Behaviors can make decisions using only local context.

**Counter-Example:**
- **Conditional doors** - Require event flags (story progress)
- **Time-based behaviors** - Shoal Cave tides (time of day)
- **Badge-locked areas** - Require HM badges

**Current Proposal:**
`ScriptContext` provides `world` and `entity`, but not clear if scripts can access:
- Global event flags
- Player inventory/badges
- Time of day
- Map-specific state

**Recommended Addition:**
Document what global state is accessible via `ScriptContext`:
```csharp
public class ScriptContext
{
    // ADD DOCUMENTATION:
    // - Can access player state via world.GetPlayer()
    // - Can access event flags via world.GetEventFlags()
    // - Can access time via world.GetTime()
}
```

---

## Gap Category 7: Pokemon Emerald Features Not Mentioned

### 🔴 CRITICAL: No Encounter System Integration

**Missing Feature:**
How do behaviors trigger wild Pokemon encounters?

**Pokemon Emerald Implementation (TILE_BEHAVIOR_EVALUATION.md lines 290-294):**
```c
bool8 MetatileBehavior_IsEncounterTile(u8 metatileBehavior)
{
    if ((sTileBitAttributes[metatileBehavior] & TILE_FLAG_HAS_ENCOUNTERS))
        return TRUE;
    else
        return FALSE;
}
```

**Proposal Coverage:**
- ✅ Flags mention `HasEncounters` (line 129)
- ❌ No encounter triggering logic
- ❌ No encounter rate modifiers
- ❌ No land vs water encounter distinction

**Missing Hooks:**
```csharp
public abstract class TileBehaviorScriptBase
{
    // MISSING ENCOUNTER HOOKS
    public virtual bool CanTriggerEncounter(ScriptContext ctx) { return false; }
    public virtual EncounterType GetEncounterType(ScriptContext ctx) { return EncounterType.None; }
    public virtual float GetEncounterRate(ScriptContext ctx) { return 1.0f; }
}
```

### 🟡 MODERATE: No HM Integration (Cut, Surf, Waterfall, Dive)

**Missing Feature:**
How do behaviors interact with HM moves?

**Pokemon Emerald Examples:**
1. **Cut** - Removes grass behaviors (referenced in line 382)
2. **Surf** - Required for water behaviors (lines 277-281)
3. **Waterfall** - Climb waterfall behaviors
4. **Dive** - Underwater navigation (lines 50-51)

**Proposal Coverage:**
- ✅ Mentions Surf requirement (line 203)
- ❌ No Cut integration
- ❌ No Waterfall climbing
- ❌ No Dive mechanics

**Missing Specification:**
```csharp
public abstract class TileBehaviorScriptBase
{
    // MISSING HM HOOKS
    public virtual bool RequiresHM(ScriptContext ctx, out HMType hm) { hm = HMType.None; return false; }
    public virtual bool CanCut(ScriptContext ctx) { return false; }
    public virtual void OnCut(ScriptContext ctx) { } // Transform behavior
}
```

### 🟡 MODERATE: No Fishing System Integration

**Missing Feature:**
Some water behaviors allow fishing.

**Pokemon Emerald Implementation:**
```c
// From TILE_BEHAVIOR_EVALUATION.md lines 42, 46, 56
// Surfable + Fishable water behaviors
```

**Proposal Coverage:**
- ❌ No fishing flag
- ❌ No fishing zone detection

**Recommended Addition:**
```csharp
public enum TileBehaviorFlags
{
    // ... existing ...
    Fishable = 1 << 5, // Can fish on this tile
}

public abstract class TileBehaviorScriptBase
{
    public virtual bool CanFish(ScriptContext ctx) { return false; }
    public virtual FishingZone? GetFishingZone(ScriptContext ctx) { return null; }
}
```

### 🟢 MINOR: No Reflection/Ripple Visual Effects

**Missing Feature:**
Water behaviors have visual effects.

**Pokemon Emerald Examples (TILE_BEHAVIOR_EVALUATION.md):**
- Line 42: Pond water has **ripples**
- Line 45: Sootopolis water is **reflective**
- Line 68: Reflection under bridge

**Proposal Coverage:**
- ❌ No visual effect hooks

---

## Gap Category 8: Edge Cases and Known Issues

### 🟡 MODERATE: No Handling for Pokemon Emerald's Water Door Bug

**Known Issue from TILE_BEHAVIOR_EVALUATION.md lines 345-348:**
> 1. **Water Door Bug** (Line 865-872 in metatile_behavior.c):
>    - Player can unintentionally emerge on water doors
>    - Fixed with `#ifdef BUGFIX` flag

**Proposal Coverage:**
- ❌ Not mentioned
- ❌ No emergence mechanic specified

**Implication:**
If porting Pokemon Emerald maps, need to handle this bug/feature.

### 🟢 MINOR: No Handling for Unused Behaviors

**Pokemon Emerald has 85-95 unused behaviors (TILE_BEHAVIOR_EVALUATION.md lines 359-362):**
> - **Total Behaviors**: 245 (0-244)
> - **Used Behaviors**: ~150-160 (estimated)
> - **Unused Behaviors**: ~85-95

**Proposal Coverage:**
- ❌ No discussion of unused/invalid behavior IDs

**Recommendation:**
Add validation for invalid behavior type IDs:
```csharp
private TileBehaviorScriptBase? GetOrLoadScript(string behaviorTypeId)
{
    if (!_behaviorRegistry.Has(behaviorTypeId))
    {
        _logger.LogWarning("Unknown behavior type: {TypeId}", behaviorTypeId);
        return null; // Graceful degradation
    }
    // ... load script
}
```

---

## Recommended Additions to Proposal

### Priority 1 (Must Have)

1. **Add Elevation System**
   - `GetElevation()` hook
   - Elevation mismatch checking
   - Underwater layer support

2. **Add Behavior State Persistence**
   - State storage in `TileBehavior` component
   - `OnStep()` counter for cracked floors
   - Behavior mutation mechanism

3. **Add Performance Optimization Strategy**
   - Flag-based fast paths
   - Collision caching
   - Behavior complexity tiers

4. **Add Encounter System Integration**
   - `CanTriggerEncounter()` hook
   - Encounter rate modifiers
   - Land vs water encounter types

5. **Add Missing Lifecycle Hooks**
   - `OnEnter()` / `OnExit()`
   - `OnLand()` (after jump)
   - Visual effect hooks

### Priority 2 (Should Have)

6. **Add Multi-Tile Jump Support**
   - `GetJumpDistance()` hook
   - Landing position validation
   - Diagonal jump support

7. **Add HM Integration**
   - `RequiresHM()` hook
   - `CanCut()` / `OnCut()` hooks
   - Surf/Dive/Waterfall mechanics

8. **Add Migration Tooling**
   - Auto-convert `TileLedge` to `TileBehavior`
   - Map data migration scripts
   - Validation tools

9. **Clarify TileBehavior vs TileScript**
   - Document division of responsibilities
   - Specify interaction between systems
   - Example of tile with both

10. **Add Error Handling**
    - Script exception handling
    - Fallback behaviors
    - Logging strategy

### Priority 3 (Nice to Have)

11. **Add Fishing System**
    - Fishable flag
    - Fishing zone detection

12. **Add Visual Effect System**
    - Reflection/ripple hooks
    - Animation triggers
    - Sound effect hooks

13. **Add Camera Restrictions**
    - Camera boundary checks
    - Camera-based collision

14. **Document Multi-Behavior Strategy**
    - Clarify single vs multiple behaviors per tile
    - Document composition approach

15. **Add Runtime Behavior Mutation**
    - Behavior change mechanism (Cut, ice breaking)
    - State transition validation

---

## Conclusion

The Roslyn integration proposal is **well-designed** but **incomplete** for full Pokemon Emerald parity. Key findings:

### Strengths
✅ Good architecture following existing NPC behavior pattern
✅ Clear base class design with extensible hooks
✅ Proper separation of concerns (system, component, script)
✅ Moddability via JSON + Roslyn scripts

### Critical Gaps
❌ **Only 8 of 245 behaviors covered** (3% coverage)
❌ **No elevation system** (breaks dive/surf/bridges)
❌ **No state persistence** (breaks cracked floors, ice)
❌ **Performance concerns** (script per collision check)
❌ **No encounter integration** (cannot trigger wild Pokemon)

### Recommendations
1. **Add Priority 1 features** before implementation
2. **Benchmark script performance** early
3. **Create behavior library** covering all 245 Pokemon Emerald types
4. **Document edge cases** and design decisions
5. **Build migration tools** for existing maps

### Risk Assessment
- **Implementation Risk:** MODERATE (architecture is sound)
- **Performance Risk:** HIGH (needs optimization)
- **Feature Risk:** HIGH (missing 75+ behaviors)
- **Migration Risk:** MODERATE (need auto-conversion tools)

---

**Next Steps for Hive Mind:**
1. Architect Agent: Design elevation system integration
2. Coder Agent: Prototype with performance benchmarks
3. Planner Agent: Create full behavior migration roadmap
4. Researcher Agent: Catalog all 245 behavior requirements

**Files Referenced:**
- `/mnt/c/Users/nate0/RiderProjects/PokeSharp/docs/research/TILE_BEHAVIOR_EVALUATION.md`
- `/mnt/c/Users/nate0/RiderProjects/PokeSharp/docs/research/TILE_BEHAVIOR_IMPLEMENTATION.md`
- `/mnt/c/Users/nate0/RiderProjects/PokeSharp/docs/research/TILE_BEHAVIOR_MOVEMENT_COLLISION.md`
- `/mnt/c/Users/nate0/RiderProjects/PokeSharp/docs/research/TILE_BEHAVIOR_ROSLYN_INTEGRATION_RESEARCH.md`
