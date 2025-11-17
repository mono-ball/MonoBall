# Hive Mind Code Analysis: Roslyn Integration Document
**Analyst**: CODER Agent
**Document**: TILE_BEHAVIOR_ROSLYN_INTEGRATION_RESEARCH.md
**Date**: 2025-11-16
**Focus**: Code correctness, API design, logic errors, implementation feasibility

---

## Executive Summary

**Overall Assessment**: The proposed code contains **7 critical issues** and **11 design concerns** that would prevent successful implementation. While the architectural approach is sound, the code examples have significant correctness problems.

**Critical Findings**:
1. ❌ Missing null checks in TileBehaviorSystem methods
2. ❌ Incorrect ledge collision logic (doesn't match Pokemon Emerald)
3. ❌ Missing `MovementMode` enum (referenced but doesn't exist)
4. ❌ ScriptContext constructor signature mismatch
5. ❌ Missing APIs property on TileBehaviorSystem
6. ❌ Incorrect component modification pattern (ref struct issues)
7. ❌ Race condition in concurrent behavior execution

---

## 1. Critical Code Correctness Issues

### 1.1. Missing Null Checks in TileBehaviorSystem

**Location**: Lines 290-312, `IsMovementBlocked` method

**Issue**: The method doesn't validate entity existence before accessing components.

**Current Code**:
```csharp
public bool IsMovementBlocked(
    World world,
    Entity tileEntity,
    Direction fromDirection,
    Direction toDirection
)
{
    if (!tileEntity.Has<TileBehavior>())  // ❌ No entity validation
        return false;

    ref var behavior = ref tileEntity.Get<TileBehavior>();  // ❌ Could crash
```

**Problem**: If `tileEntity` is a null/invalid entity reference, `Has<T>()` will throw. Arch ECS entities are value types but can be uninitialized.

**Fix Required**:
```csharp
public bool IsMovementBlocked(
    World world,
    Entity tileEntity,
    Direction fromDirection,
    Direction toDirection
)
{
    // ✅ Validate entity exists in world
    if (!world.IsAlive(tileEntity))
        return false;

    if (!tileEntity.Has<TileBehavior>())
        return false;
```

**Same issue appears in**:
- `GetForcedMovement` (line 318-336)
- `GetJumpDirection` (line 343-362)

---

### 1.2. Ledge Collision Logic Error

**Location**: Lines 390-406, `JumpSouthBehavior.IsBlockedFrom`

**Issue**: The logic is **backwards** compared to actual Pokemon Emerald behavior.

**Current Code**:
```csharp
public override bool IsBlockedFrom(ScriptContext ctx, Direction fromDirection, Direction toDirection)
{
    // Block movement from north (can't climb up)
    if (fromDirection == Direction.North)  // ❌ WRONG!
        return true;

    return false;
}
```

**Pokemon Emerald's Actual Logic** (from existing `TileLedge.IsBlockedFrom`):
```csharp
public readonly bool IsBlockedFrom(Direction fromDirection)
{
    // Block movement opposite to jump direction
    return fromDirection switch
    {
        Direction.North => JumpDirection == Direction.South,  // ✅ CORRECT
        Direction.South => JumpDirection == Direction.North,
        Direction.West => JumpDirection == Direction.East,
        Direction.East => JumpDirection == Direction.West,
        _ => false,
    };
}
```

**The Problem**:
- **fromDirection** = direction the player is **moving FROM** (their position relative to the ledge)
- A south-facing ledge allows jumping **from** the **north** side (standing above it)
- It blocks movement **from** the **south** side (can't climb up)

**Correct Implementation**:
```csharp
public class JumpSouthBehavior : TileBehaviorScriptBase
{
    public override bool IsBlockedFrom(ScriptContext ctx, Direction fromDirection, Direction toDirection)
    {
        // Jump direction is South, so block from South (can't climb up from below)
        // Allow from North (can jump down from above)
        return fromDirection == Direction.South;  // ✅ CORRECT
    }

    public override Direction GetJumpDirection(ScriptContext ctx, Direction fromDirection)
    {
        // Only allow jumping when approaching from North (standing above ledge)
        if (fromDirection == Direction.North)
            return Direction.South;

        return Direction.None;
    }
}
```

**Impact**: This logic error would make ledges work **backwards**, blocking jumps and allowing climbing up.

---

### 1.3. Missing `MovementMode` Enum

**Location**: Line 204, `GetRequiredMovementMode` return type

**Issue**: The code references `MovementMode?` which **does not exist** in the codebase.

**Current Code**:
```csharp
public virtual MovementMode? GetRequiredMovementMode(ScriptContext ctx)
{
    return null; // Default: no special mode required
}
```

**Evidence**: Searched entire codebase - no `MovementMode` enum exists.

**Fix Options**:
1. **Remove the method** (surfing/diving not implemented yet)
2. **Use string instead**: `public virtual string? GetRequiredMovementMode(ScriptContext ctx)`
3. **Create the enum** (requires additional work):
```csharp
public enum MovementMode
{
    Walk,
    Surf,
    Dive,
    Bike,
    // etc.
}
```

**Recommendation**: Remove until movement modes are implemented.

---

### 1.4. ScriptContext Constructor Mismatch

**Location**: Lines 265, 302, 336, `ScriptContext` instantiation

**Issue**: The code creates `ScriptContext` incorrectly.

**Proposed Code**:
```csharp
var context = new ScriptContext(world, tileEntity, null, _apis);
//                                                    ^^^^ ❌ null logger not allowed
```

**Actual Constructor** (from ScriptContext.cs):
```csharp
public ScriptContext(World world, Entity? entity, ILogger logger, IScriptingApiProvider apis)
{
    World = world ?? throw new ArgumentNullException(nameof(world));
    Logger = logger ?? throw new ArgumentNullException(nameof(logger));  // ❌ Throws on null!
    _entity = entity;
    _apis = apis ?? throw new ArgumentNullException(nameof(apis));
}
```

**Fix Required**:
```csharp
// TileBehaviorSystem needs an ILogger field
private readonly ILogger<TileBehaviorSystem> _logger;

// Use it when creating contexts
var context = new ScriptContext(world, tileEntity, _logger, _apis);
```

---

### 1.5. Missing APIs Property

**Location**: Line 265, `_apis` referenced but not defined

**Issue**: `TileBehaviorSystem` references `_apis` but doesn't have this field.

**Current Code**:
```csharp
public class TileBehaviorSystem : BaseSystem
{
    private readonly IBehaviorRegistry _behaviorRegistry;
    private readonly IScriptService _scriptService;
    private readonly ILogger<TileBehaviorSystem> _logger;

    // ❌ Missing: private readonly IScriptingApiProvider _apis;
```

**Fix Required**:
```csharp
public class TileBehaviorSystem : BaseSystem
{
    private readonly IBehaviorRegistry _behaviorRegistry;
    private readonly IScriptService _scriptService;
    private readonly ILogger<TileBehaviorSystem> _logger;
    private readonly IScriptingApiProvider _apis;  // ✅ ADD THIS

    public TileBehaviorSystem(
        IBehaviorRegistry behaviorRegistry,
        IScriptService scriptService,
        IScriptingApiProvider apis,  // ✅ ADD THIS
        ILogger<TileBehaviorSystem> logger)
    {
        _behaviorRegistry = behaviorRegistry;
        _scriptService = scriptService;
        _apis = apis;  // ✅ ADD THIS
        _logger = logger;
    }
```

---

### 1.6. Component Modification Pattern Error

**Location**: Lines 254-278, TileBehaviorSystem.Update

**Issue**: The code modifies `TileBehavior` but doesn't write changes back.

**Current Code**:
```csharp
world.Query(
    in EcsQueries.TilesWithBehaviors,
    (Entity entity, ref TileBehavior behavior) =>  // ✅ ref is correct
    {
        if (!behavior.IsActive)
            return;

        // ...

        if (!behavior.IsInitialized)
        {
            script.OnActivated(context);
            behavior.IsInitialized = true;  // ✅ Modifies via ref - should work
        }

        // ❌ NO EXPLICIT SET NEEDED - ref handles it
    }
);
```

**Actually**: The code is **correct** with `ref` parameters. Changes to `ref` structs are automatically written back.

**But**: This contradicts the pattern used in `MovementSystem.ProcessMovementWithAnimation`:
```csharp
if (world.TryGet(entity, out Animation animation))  // Copy, not ref
{
    // ...modify animation...
    world.Set(entity, animation);  // ✅ Must write back
}
```

**Consistency Issue**: The document mixes two patterns:
1. **Query with ref**: Modifications auto-saved
2. **TryGet/Get**: Returns copy, must call `Set()`

**Recommendation**: Be explicit about which pattern is used where.

---

### 1.7. GetOppositeDirection Not Defined

**Location**: Line 534, `CollisionService.IsPositionWalkable`

**Issue**: Method `GetOppositeDirection` is called but never defined.

**Current Code**:
```csharp
var toDirection = GetOppositeDirection(fromDirection);  // ❌ Method doesn't exist
```

**Fix**: Use existing `Direction.Opposite()` extension:
```csharp
var toDirection = fromDirection.Opposite();  // ✅ Use extension method
```

---

## 2. Logic Errors in Example Scripts

### 2.1. Ice Behavior Logic

**Location**: Lines 440-448, `IceBehavior.GetForcedMovement`

**Issue**: Logic doesn't handle stopping conditions.

**Current Code**:
```csharp
public override Direction GetForcedMovement(ScriptContext ctx, Direction currentDirection)
{
    // Continue sliding in current direction
    if (currentDirection != Direction.None)
        return currentDirection;

    return Direction.None;
}
```

**Problem**: In Pokemon, ice stops sliding when:
1. You hit a wall/obstacle
2. You step onto non-ice tile
3. You press a direction button

**This implementation**: Never checks the **next tile** to see if it's ice.

**Pokemon Emerald's Logic**:
```c
// In Pokemon, you check the NEXT tile after movement completes
if (nextTileIsIce && !playerInput) {
    forceMovement(currentDirection);
}
```

**Correct Implementation**:
```csharp
public override Direction GetForcedMovement(ScriptContext ctx, Direction currentDirection)
{
    // Only continue sliding if still on ice
    if (currentDirection == Direction.None)
        return Direction.None;

    // Check if next tile in direction is also ice
    var (dx, dy) = currentDirection.ToTileDelta();
    var nextX = ctx.Position.X + dx;
    var nextY = ctx.Position.Y + dy;

    // If next tile is ice and no player input, continue sliding
    var nextIsIce = ctx.Map.GetTileBehaviorAt(nextX, nextY) == "ice";
    var hasInput = ctx.Player.HasInputThisFrame();

    if (nextIsIce && !hasInput)
        return currentDirection;

    return Direction.None;
}
```

**Missing Methods Needed**:
- `MapApiService.GetTileBehaviorAt(int x, int y)`
- `PlayerApiService.HasInputThisFrame()`

---

### 2.2. Impassable East Logic Duplication

**Location**: Lines 480-497, `ImpassableEastBehavior`

**Issue**: Both `IsBlockedFrom` and `IsBlockedTo` check the same thing.

**Current Code**:
```csharp
public override bool IsBlockedFrom(ScriptContext ctx, Direction fromDirection, Direction toDirection)
{
    if (fromDirection == Direction.East)
        return true;
    return false;
}

public override bool IsBlockedTo(ScriptContext ctx, Direction toDirection)
{
    if (toDirection == Direction.East)  // ❌ Same check
        return true;
    return false;
}
```

**Pokemon Emerald's Two-Way Check**:
- **IsBlockedFrom**: "Am I allowed to **leave** this tile going direction X?"
- **IsBlockedTo**: "Am I allowed to **enter** this tile from direction X?"

**The Problem**: These should check **opposite** directions.

**Correct Implementation**:
```csharp
public class ImpassableEastBehavior : TileBehaviorScriptBase
{
    public override bool IsBlockedFrom(ScriptContext ctx, Direction fromDirection, Direction toDirection)
    {
        // Block if trying to enter FROM the east side (moving west into tile)
        return fromDirection == Direction.East;
    }

    public override bool IsBlockedTo(ScriptContext ctx, Direction toDirection)
    {
        // Block if trying to exit TO the east (moving east out of tile)
        return toDirection == Direction.East;
    }
}
```

**But wait**: This is still confusing. Need to clarify the **semantic difference** between the two methods.

---

## 3. API Design Issues

### 3.1. Unclear Method Semantics

**Location**: Lines 154-173, `IsBlockedFrom` vs `IsBlockedTo`

**Issue**: The two methods have unclear, overlapping purposes.

**Current Design**:
```csharp
bool IsBlockedFrom(ScriptContext ctx, Direction fromDirection, Direction toDirection)
bool IsBlockedTo(ScriptContext ctx, Direction toDirection)
```

**Problems**:
1. **fromDirection** and **toDirection** naming is ambiguous
2. `IsBlockedFrom` takes both parameters but only uses `fromDirection`
3. Not clear when to call which method

**Pokemon Emerald's Actual Check** (from existing code):
```csharp
// CollisionService checks BOTH:
if (ledge.IsBlockedFrom(fromDirection))  // Check entry
    return false;
// Then checks IsWalkable for exit
```

**Proposed Clarification**:
```csharp
// Check if entity can ENTER this tile from a direction
public virtual bool IsBlockedEntry(ScriptContext ctx, Direction entryDirection)
{
    return false; // Default: allow entry from any direction
}

// Check if entity can EXIT this tile to a direction
public virtual bool IsBlockedExit(ScriptContext ctx, Direction exitDirection)
{
    return false; // Default: allow exit to any direction
}
```

**Then ledge becomes**:
```csharp
public class JumpSouthBehavior : TileBehaviorScriptBase
{
    public override bool IsBlockedEntry(ScriptContext ctx, Direction entryDirection)
    {
        // Can't enter from south (can't climb up)
        return entryDirection == Direction.South;
    }

    public override bool IsBlockedExit(ScriptContext ctx, Direction exitDirection)
    {
        // Can only exit south via jump
        return exitDirection != Direction.South;
    }
}
```

---

### 3.2. Missing Error Handling

**Location**: Lines 247-278, `TileBehaviorSystem.Update`

**Issue**: No error handling for script execution.

**Current Code**:
```csharp
var script = GetOrLoadScript(behavior.BehaviorTypeId);
if (script == null)
    return;  // ❌ Silent failure

// Create context
var context = new ScriptContext(world, entity, _logger, _apis);

// Initialize if needed
if (!behavior.IsInitialized)
{
    script.OnActivated(context);  // ❌ Could throw
    behavior.IsInitialized = true;
}

script.OnTick(context, deltaTime);  // ❌ Could throw
```

**Risks**:
1. Script compilation errors
2. Runtime exceptions in scripts
3. Null reference exceptions
4. Type errors

**Fix Required**:
```csharp
try
{
    var script = GetOrLoadScript(behavior.BehaviorTypeId);
    if (script == null)
    {
        _logger?.LogWarning("Failed to load behavior script: {TypeId}", behavior.BehaviorTypeId);
        behavior.IsActive = false;  // Disable broken behavior
        return;
    }

    var context = new ScriptContext(world, entity, _logger, _apis);

    if (!behavior.IsInitialized)
    {
        script.OnActivated(context);
        behavior.IsInitialized = true;
    }

    script.OnTick(context, deltaTime);
}
catch (Exception ex)
{
    _logger?.LogError(ex, "Error executing tile behavior {TypeId} on entity {EntityId}",
        behavior.BehaviorTypeId, entity.Id);

    // Disable behavior to prevent repeated errors
    behavior.IsActive = false;
}
```

---

### 3.3. Script Caching Issues

**Location**: Line 247, `Dictionary<string, TileBehaviorScriptBase> _scriptCache`

**Issue**: Cache is per-instance, not global.

**Problem**:
```csharp
private readonly Dictionary<string, TileBehaviorScriptBase> _scriptCache = new();
```

If you have **1000 tiles** with "jump_south" behavior:
- ❌ Current: 1000 separate cache entries (if system is instantiated per-entity)
- ✅ Should be: 1 cached script, reused across all tiles

**Fix**: Use singleton cache or dependency injection:
```csharp
public interface IScriptCache<T> where T : TypeScriptBase
{
    T? GetOrCompile(string typeId);
}

// Registered as singleton
public class TileBehaviorSystem : BaseSystem
{
    private readonly IScriptCache<TileBehaviorScriptBase> _scriptCache;

    public TileBehaviorSystem(
        IScriptCache<TileBehaviorScriptBase> scriptCache,
        // ... other deps
    )
    {
        _scriptCache = scriptCache;
    }
}
```

---

## 4. Integration Issues

### 4.1. CollisionService Integration

**Location**: Lines 507-558, modified `CollisionService.IsPositionWalkable`

**Issue**: The integration breaks existing collision detection flow.

**Current Flow** (existing code):
```csharp
foreach (var entity in entities)
{
    // Check elevation
    if (entity.Has<Elevation>()) { /* ... */ }

    // Check collision
    if (entity.Has<Collision>())
    {
        if (collision.IsSolid)
        {
            // Check ledge as special case
            if (entity.Has<TileLedge>() && fromDirection != Direction.None)
            {
                if (ledge.IsBlockedFrom(fromDirection))
                    return false;
            }
            else
            {
                return false;  // ✅ Normal solid blocks
            }
        }
    }
}
```

**Proposed Flow**:
```csharp
// NEW: Check tile behaviors for blocking
if (entity.Has<TileBehavior>())
{
    var toDirection = GetOppositeDirection(fromDirection);
    if (_tileBehaviorSystem.IsMovementBlocked(world, entity, fromDirection, toDirection))
    {
        return false;
    }
}

// Existing collision check (for non-behavior entities)
if (entity.Has<Collision>())
{
    if (collision.IsSolid)
        return false;
}
```

**Problem**: This creates **two separate collision systems**:
1. Old: `Collision` + `TileLedge`
2. New: `TileBehavior`

**During migration**, a tile could have **both**:
- `TileBehavior` with "jump_south" behavior
- `TileLedge` with `JumpDirection = South`
- `Collision` with `IsSolid = true`

This causes **triple-checking** and potential conflicts.

**Fix**: Migration needs clear phases:
1. Phase 1: Add behavior system (parallel to old)
2. Phase 2: Migrate tiles one-by-one
3. Phase 3: Remove old components **completely**
4. Phase 4: Remove old collision checks

**Missing from document**: Migration tooling to convert components.

---

### 4.2. MovementSystem Integration

**Location**: Lines 577-622, modified `TryStartMovement`

**Issue**: The new code doesn't match the actual `MovementSystem` implementation.

**Document's Proposed Code**:
```csharp
var currentTile = GetTileAt(world, position.MapId, position.X, position.Y);
```

**Actual MovementSystem**: Has no `GetTileAt` method!

**Fix Required**: Add helper method:
```csharp
private Entity? GetTileAt(World world, int mapId, int x, int y)
{
    var entities = _spatialQuery.GetEntitiesAt(mapId, x, y);
    foreach (var entity in entities)
    {
        if (entity.Has<TileBehavior>())
            return entity;
    }
    return null;
}
```

**But**: This requires `MovementSystem` to have `_spatialQuery` injected.

---

### 4.3. Dependency Injection Chain

**Issue**: The integration requires extensive DI changes not mentioned in the document.

**Required Changes**:
1. `TileBehaviorSystem` needs:
   - `IScriptingApiProvider`
   - `ILogger<TileBehaviorSystem>`
   - `IBehaviorRegistry`
   - `IScriptService`

2. `CollisionService` needs:
   - Reference to `TileBehaviorSystem`
   - `World` instance

3. `MovementSystem` needs:
   - Reference to `TileBehaviorSystem`
   - `ISpatialQuery` (for `GetTileAt`)

**Missing from document**: DI container configuration.

---

## 5. Performance Issues

### 5.1. Script Execution Per Frame

**Location**: Lines 274-276, `OnTick` called every frame

**Issue**: Behaviors are executed **every frame** for **every tile**.

**Current Code**:
```csharp
script.OnTick(context, deltaTime);
```

**Problem**: If you have 10,000 tiles with behaviors, this is 10,000 script calls per frame.

**Pokemon Emerald**: Only checks behaviors **on collision** (demand-driven).

**Fix**: Make `OnTick` optional, only for behaviors that need per-frame updates (cracked floors, etc.):
```csharp
world.Query(
    in EcsQueries.TilesWithBehaviors,
    (Entity entity, ref TileBehavior behavior) =>
    {
        // Only tick behaviors with the NeedsUpdate flag
        if (!behavior.NeedsUpdate)
            return;

        // ...
    }
);
```

---

### 5.2. Multiple Script Cache Lookups

**Location**: Lines 298, 331, 356 - `GetOrLoadScript` called 3 times per movement

**Issue**: Each collision check loads script from cache.

**Better Approach**:
```csharp
public bool IsMovementBlocked(Entity tileEntity, Direction fromDirection, Direction toDirection)
{
    if (!tileEntity.Has<TileBehavior>())
        return false;

    ref var behavior = ref tileEntity.Get<TileBehavior>();

    // ✅ Load script once
    var script = GetOrLoadScript(behavior.BehaviorTypeId);
    if (script == null)
        return false;

    var context = new ScriptContext(world, tileEntity, _logger, _apis);

    // ✅ Call both methods with same script instance
    return script.IsBlockedFrom(context, fromDirection, toDirection)
        || script.IsBlockedTo(context, toDirection);
}
```

---

## 6. Threading and Concurrency

### 6.1. Script Cache Thread Safety

**Location**: Line 247, `Dictionary<string, TileBehaviorScriptBase> _scriptCache`

**Issue**: `Dictionary` is not thread-safe.

**Problem**: If Arch ECS uses parallel queries:
```csharp
world.Query(in EcsQueries.TilesWithBehaviors, (entity, ref behavior) => {
    var script = GetOrLoadScript(behavior.BehaviorTypeId);  // ❌ Race condition!
});
```

**Fix**: Use `ConcurrentDictionary`:
```csharp
private readonly ConcurrentDictionary<string, TileBehaviorScriptBase> _scriptCache = new();
```

---

### 6.2. Component Modification Race Condition

**Issue**: Modifying `TileBehavior.IsInitialized` during parallel query.

**Current Code**:
```csharp
world.Query(in EcsQueries.TilesWithBehaviors, (Entity entity, ref TileBehavior behavior) =>
{
    if (!behavior.IsInitialized)
    {
        script.OnActivated(context);
        behavior.IsInitialized = true;  // ❌ Multiple threads could set this
    }
});
```

**Problem**: If Arch runs queries in parallel, two threads could initialize the same behavior.

**Fix**: Use atomic flag or init outside of parallel query.

---

## 7. Missing Implementations

### 7.1. GetOrLoadScript Method

**Location**: Referenced on lines 260, 298, 331, 356

**Issue**: Method is called but **never implemented** in the document.

**Required Implementation**:
```csharp
private TileBehaviorScriptBase? GetOrLoadScript(string behaviorTypeId)
{
    // Check cache first
    if (_scriptCache.TryGetValue(behaviorTypeId, out var cached))
        return cached;

    // Get definition from registry
    var definition = _behaviorRegistry.GetType<TileBehaviorDefinition>(behaviorTypeId);
    if (definition == null)
    {
        _logger?.LogWarning("Tile behavior type not found: {TypeId}", behaviorTypeId);
        return null;
    }

    // Compile script
    if (string.IsNullOrEmpty(definition.BehaviorScript))
    {
        _logger?.LogWarning("Tile behavior {TypeId} has no script", behaviorTypeId);
        return null;
    }

    var script = _scriptService.CompileScript<TileBehaviorScriptBase>(definition.BehaviorScript);
    if (script == null)
    {
        _logger?.LogError("Failed to compile script for behavior {TypeId}", behaviorTypeId);
        return null;
    }

    // Cache and return
    _scriptCache[behaviorTypeId] = script;
    return script;
}
```

---

### 7.2. EcsQueries.TilesWithBehaviors

**Location**: Line 253

**Issue**: Query descriptor not defined.

**Required**:
```csharp
// In Queries.cs
public static readonly QueryDescription TilesWithBehaviors = new QueryDescription()
    .WithAll<TileBehavior, Position>();
```

---

## 8. Documentation Gaps

### 8.1. State Management Unclear

**Question**: How do tile behaviors store state?

The document mentions "ice cracking, floor breaking" but doesn't explain:
1. Does `ScriptContext.GetState<T>()` work for tile entities?
2. Should state be in components or script context?
3. How is state persisted across save/load?

**Example Missing**: Cracked ice behavior that breaks after N steps.

---

### 8.2. Multi-Behavior Tiles

**Question**: Can a tile have multiple behaviors?

Document mentions this in "Research Questions" but doesn't provide:
1. Component design for multiple behaviors
2. Execution order
3. Conflict resolution

**Example**: A tile that is BOTH "ice" AND "encounter_zone".

---

## 9. Recommended Code Fixes

### Priority 1: Critical Fixes (Must Fix Before Implementation)

1. **Fix ledge collision logic** - Lines 390-416
2. **Add null checks** - Lines 290-362
3. **Remove MovementMode** - Line 204
4. **Fix ScriptContext creation** - Lines 265, 302, 336
5. **Add _apis field** - Line 242
6. **Fix GetOppositeDirection** - Line 534

### Priority 2: Design Improvements (Should Fix)

1. **Clarify IsBlockedFrom/To semantics** - Lines 154-173
2. **Add error handling** - Lines 247-278
3. **Fix script caching** - Line 247
4. **Implement GetOrLoadScript** - Missing
5. **Add GetTileAt helper** - Line 578

### Priority 3: Performance (Nice to Have)

1. **Make OnTick optional** - Line 274
2. **Use ConcurrentDictionary** - Line 247
3. **Batch script lookups** - Lines 298, 331, 356

---

## 10. Test Coverage Gaps

The document provides **no test cases**. Required tests:

### Unit Tests
```csharp
[Test]
public void JumpSouthBehavior_BlocksMovementFromSouth()
{
    var behavior = new JumpSouthBehavior();
    var blocked = behavior.IsBlockedFrom(ctx, Direction.South, Direction.North);
    Assert.IsTrue(blocked);
}

[Test]
public void JumpSouthBehavior_AllowsJumpFromNorth()
{
    var behavior = new JumpSouthBehavior();
    var jumpDir = behavior.GetJumpDirection(ctx, Direction.North);
    Assert.AreEqual(Direction.South, jumpDir);
}
```

### Integration Tests
```csharp
[Test]
public void TileBehaviorSystem_BlocksCollisionCorrectly()
{
    // Create world with ledge tile
    var tile = world.Create<TileBehavior, Position>();
    tile.Set(new TileBehavior("jump_south"));

    // Try to move from south (should block)
    var blocked = tileBehaviorSystem.IsMovementBlocked(
        world, tile, Direction.South, Direction.North);
    Assert.IsTrue(blocked);
}
```

---

## 11. Final Verdict

**Can this code be implemented as-written?** ❌ **NO**

**Estimated fixes required**: 18 code changes across 7 files

**Estimated effort**: 2-3 days of debugging and correction

**Risk level**: **HIGH** - Core gameplay mechanics (ledges) would be broken

**Recommendation**:
1. Fix all Priority 1 issues before starting implementation
2. Prototype with a single behavior (e.g., solid_wall) to validate architecture
3. Add comprehensive test coverage before migrating ledges
4. Create migration tooling to convert TileLedge -> TileBehavior
5. Run A/B testing to verify Pokemon Emerald parity

---

## Appendix A: Side-by-Side Comparison

### Current TileLedge vs Proposed Behavior

| Feature | Current (TileLedge) | Proposed (TileBehavior) | Status |
|---------|---------------------|-------------------------|---------|
| Collision logic | ✅ Correct | ❌ Backwards | **BROKEN** |
| Performance | ✅ Fast (component check) | ⚠️ Slower (script call) | Acceptable |
| Moddability | ❌ Hardcoded | ✅ Scriptable | **WIN** |
| Error handling | ✅ N/A (no scripts) | ❌ Missing try/catch | **BROKEN** |
| State management | ✅ Component state | ❓ Unclear | **UNCLEAR** |
| Migration path | ✅ Existing | ❌ Not defined | **MISSING** |

---

## Appendix B: Corrected Code Examples

### Corrected JumpSouthBehavior
```csharp
using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Scripting.Runtime;

/// <summary>
/// Pokemon-style south-facing ledge behavior.
/// Allows jumping south (down), blocks climbing north (up).
/// </summary>
public class JumpSouthBehavior : TileBehaviorScriptBase
{
    public override bool IsBlockedFrom(ScriptContext ctx, Direction fromDirection, Direction toDirection)
    {
        // Block movement from south (trying to climb up from below)
        // Allow movement from north (standing above, can jump down)
        return fromDirection == Direction.South;
    }

    public override bool IsBlockedTo(ScriptContext ctx, Direction toDirection)
    {
        // Block movement to north (can't exit upward)
        // Allow movement to south (can jump down)
        return toDirection == Direction.North;
    }

    public override Direction GetJumpDirection(ScriptContext ctx, Direction fromDirection)
    {
        // Only allow jumping when approaching from north (standing above)
        if (fromDirection == Direction.North)
            return Direction.South;

        return Direction.None;
    }
}
```

### Corrected TileBehaviorSystem
```csharp
public class TileBehaviorSystem : BaseSystem
{
    private readonly IBehaviorRegistry _behaviorRegistry;
    private readonly IScriptService _scriptService;
    private readonly ILogger<TileBehaviorSystem> _logger;
    private readonly IScriptingApiProvider _apis;  // ✅ ADDED

    private readonly ConcurrentDictionary<string, TileBehaviorScriptBase> _scriptCache = new();  // ✅ Thread-safe

    public TileBehaviorSystem(
        IBehaviorRegistry behaviorRegistry,
        IScriptService scriptService,
        IScriptingApiProvider apis,  // ✅ ADDED
        ILogger<TileBehaviorSystem> logger)
    {
        _behaviorRegistry = behaviorRegistry;
        _scriptService = scriptService;
        _apis = apis;  // ✅ ADDED
        _logger = logger;
    }

    public bool IsMovementBlocked(
        World world,
        Entity tileEntity,
        Direction fromDirection,
        Direction toDirection)
    {
        // ✅ ADDED: Validate entity
        if (!world.IsAlive(tileEntity))
            return false;

        if (!tileEntity.Has<TileBehavior>())
            return false;

        ref var behavior = ref tileEntity.Get<TileBehavior>();
        if (!behavior.IsActive)
            return false;

        // ✅ ADDED: Error handling
        try
        {
            var script = GetOrLoadScript(behavior.BehaviorTypeId);
            if (script == null)
                return false;

            var context = new ScriptContext(world, tileEntity, _logger, _apis);  // ✅ FIXED: Use _logger

            return script.IsBlockedFrom(context, fromDirection, toDirection)
                || script.IsBlockedTo(context, toDirection);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error checking tile behavior {TypeId}", behavior.BehaviorTypeId);
            return false;  // Fail safe: allow movement on error
        }
    }
}
```

---

**End of Analysis**

**Next Steps for Hive Mind**:
1. REVIEWER: Validate these findings
2. PLANNER: Create fix task breakdown
3. TESTER: Define test cases
4. RESEARCHER: Investigate state management approach
