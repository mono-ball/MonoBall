# Script Base Class Architecture Analysis

## Executive Summary

PokeSharp has **intentionally separated** `TypeScriptBase` and `TileBehaviorScriptBase` for **architectural clarity** and **performance optimization**. The separation reflects fundamentally different use cases: **entity behaviors** (NPCs, general-purpose) vs. **tile-specific behaviors** (collision, forced movement, encounters).

**Key Finding**: `TileBehaviorScriptBase` **inherits from** `TypeScriptBase`, meaning they're already unified at the foundation. The "separation" is actually **specialization** - tile behaviors extend the base with tile-specific hooks while maintaining the core lifecycle.

---

## Architecture Overview

### Class Hierarchy

```
TypeScriptBase (Abstract Base)
├── Lifecycle: OnInitialize(), OnActivated(), OnTick(), OnDeactivated()
├── State Management: Stateless pattern with ScriptContext
└── Used For: General entity behaviors (NPCs, entities)

    ↓ (inherits)

TileBehaviorScriptBase (Specialized Abstract)
├── Inherits: All TypeScriptBase lifecycle hooks
├── Adds Tile-Specific Hooks:
│   ├── IsBlockedFrom(fromDir, toDir) → bool
│   ├── IsBlockedTo(toDir) → bool
│   ├── GetForcedMovement(currentDir) → Direction
│   ├── GetJumpDirection(fromDir) → Direction
│   ├── GetRequiredMovementMode() → string?
│   ├── AllowsRunning() → bool
│   └── OnStep(entity) → void
└── Used For: Tile behaviors (collision, ice, ledges, encounters)
```

**This is NOT duplication** - it's proper **object-oriented specialization**.

---

## 1. Current Architecture

### TypeScriptBase (Line 37-77 in TypeScriptBase.cs)

**Purpose**: General-purpose entity behavior base class

**Lifecycle Hooks**:
- `OnInitialize(ScriptContext ctx)` - Called once when script loads
- `OnActivated(ScriptContext ctx)` - Called when behavior activates on entity
- `OnTick(ScriptContext ctx, float deltaTime)` - Called every frame while active
- `OnDeactivated(ScriptContext ctx)` - Called when behavior deactivates

**Characteristics**:
- **Stateless design**: NO instance fields/properties allowed
- **ScriptContext pattern**: All state via `ctx.GetState<T>()`/`ctx.SetState<T>()`
- **Single script instance**: Reused across all entities with same behavior
- **Per-entity state**: Stored in ECS components via ScriptContext

**Use Cases** (from actual scripts):
- `WanderBehavior` - Random NPC movement (wander_behavior.csx)
- `PatrolBehavior` - NPC waypoint patrol (patrol_behavior.csx)
- `GuardBehavior` - NPC facing direction guarding
- Any entity behavior that needs OnTick execution

### TileBehaviorScriptBase (Line 20-103 in TileBehaviorScriptBase.cs)

**Purpose**: Tile-specific behavior specialization

**Inherits From**: `TypeScriptBase` (Line 20)

**Additional Tile-Specific Hooks** (beyond TypeScriptBase):
```csharp
// Movement blocking (two-way check like Pokemon Emerald)
bool IsBlockedFrom(ctx, fromDirection, toDirection)
bool IsBlockedTo(ctx, toDirection)

// Movement mechanics
Direction GetForcedMovement(ctx, currentDirection)  // Ice sliding
Direction GetJumpDirection(ctx, fromDirection)      // Ledge jumping
string? GetRequiredMovementMode(ctx)                // Surf/Dive requirement
bool AllowsRunning(ctx)                             // Running prevention

// Per-step effects
void OnStep(ctx, entity)                            // Ice cracking, ash gathering
```

**Use Cases** (from actual scripts):
- `IceBehavior` - Forced sliding, no running (ice.csx)
- `JumpEastBehavior` - One-way ledge jumping (jump_east.csx)
- `ImpassableBehavior` - Complete movement blocking (impassable.csx)
- Tall grass encounters, warp tiles, deep sand, etc.

---

## 2. System Execution Comparison

### NPCBehaviorSystem (NPCBehaviorSystem.cs)

**Executes**: Scripts inheriting `TypeScriptBase`

**Query**: `NpcsWithBehavior` (Npc + Behavior components)

**Execution Flow**:
1. Query entities with `Behavior` component
2. Look up script from `TypeRegistry<BehaviorDefinition>`
3. Create `ScriptContext(world, entity, logger, apis)`
4. Initialize: `script.OnActivated(ctx)` (first tick only)
5. Update: `script.OnTick(ctx, deltaTime)` (every tick)
6. Error isolation: One NPC script error doesn't crash others

**Performance**: Priority 75, runs after spatial hash (25), before movement (100)

**State Management**:
- Per-entity state in ECS components (e.g., `WanderState`, `PatrolState`)
- Logger caching per behavior+entity to prevent memory leaks

### TileBehaviorSystem (TileBehaviorSystem.cs)

**Executes**: Scripts inheriting `TileBehaviorScriptBase`

**Query**: `TilePosition + TileBehavior` components

**Execution Flow**:
1. Query entities with `TileBehavior` component
2. Look up script from `TypeRegistry<TileBehaviorDefinition>`
3. Create `ScriptContext(world, tileEntity, logger, apis)`
4. Initialize: `script.OnActivated(ctx)` (first tick only)
5. Update: `script.OnTick(ctx, deltaTime)` (every tick)
6. **Additionally**: Implements `ITileBehaviorSystem` interface for collision queries

**Performance**: Priority 50, runs after spatial hash (25), **BEFORE** NPC behaviors (75) and movement (100)

**Key Difference**: TileBehaviorSystem also provides **on-demand query methods**:
- `IsMovementBlocked()` - Called by CollisionService during movement checks
- `GetForcedMovement()` - Called by MovementSystem for ice sliding
- `GetJumpDirection()` - Called by MovementSystem for ledges
- `GetRequiredMovementMode()` - Called by MovementSystem for surf/dive
- `AllowsRunning()` - Called by MovementSystem for running checks

**State Management**: Same pattern as NPCs (stateless scripts, ECS component state)

---

## 3. Why They Were Separated

### Architectural Reasons

**1. Different Execution Patterns**

| Aspect | TypeScriptBase (NPCs) | TileBehaviorScriptBase (Tiles) |
|--------|----------------------|-------------------------------|
| **Execution Model** | Active every tick | Mostly reactive (queried on-demand) |
| **Primary Hook** | `OnTick()` | Tile-specific methods |
| **Query Frequency** | ~60 FPS | Variable (only when movement occurs) |
| **System Priority** | 75 (after tiles) | 50 (before NPCs) |

**2. Interface Contracts**

TileBehaviorSystem implements `ITileBehaviorSystem` interface for **dependency inversion**:
- CollisionSystem queries tiles WITHOUT direct coupling
- MovementSystem queries tiles WITHOUT circular dependencies
- Allows swapping implementations without breaking consumers

NPCBehaviorSystem has **no such interface** - NPCs are active agents, not queried systems.

**3. Different Definition Types**

```csharp
// BehaviorDefinition (NPCs) - Line 7-44 in BehaviorDefinition.cs
record BehaviorDefinition : IScriptedType {
    float DefaultSpeed              // NPC-specific
    float PauseAtWaypoint          // NPC-specific
    bool AllowInteractionWhileMoving  // NPC-specific
    string BehaviorScript          // Script path
}

// TileBehaviorDefinition (Tiles) - Line 9-38 in TileBehaviorDefinition.cs
record TileBehaviorDefinition : IScriptedType {
    TileBehaviorFlags Flags        // Tile-specific (HasEncounters, Surfable, etc.)
    string BehaviorScript          // Script path
}
```

**Different metadata requirements** justify separate definition types.

**4. Different Component Types**

```csharp
// Behavior (NPCs) - For entities with behavior
struct Behavior {
    string BehaviorTypeId
    bool IsActive
    bool IsInitialized
}

// TileBehavior (Tiles) - For tile entities
struct TileBehavior {
    string BehaviorTypeId
    bool IsActive
    bool IsInitialized
}
```

Same structure but **semantically different** - NPCs have behaviors, tiles have tile behaviors.

### Performance Reasons

**1. Early Execution for Tiles**

Tiles run at **Priority 50**, before NPCs (75) and movement (100):
- **Why**: Movement systems need tile data **before** moving entities
- Forced movement checks (ice) must happen **before** movement is processed
- Collision checks query tiles **synchronously** during movement validation

**2. Query-Based vs. Tick-Based**

Most tile behaviors are **reactive** (queried during collision/movement):
```csharp
// Only executed when entity attempts movement
IsMovementBlocked(world, tileEntity, fromDir, toDir)
GetForcedMovement(world, tileEntity, currentDir)
```

NPC behaviors are **active** (execute every frame):
```csharp
// Executed 60 FPS for every active NPC
OnTick(ctx, deltaTime)
```

**Separating systems allows different optimization strategies**.

**3. Logger Caching Strategy**

Both systems cache loggers per-script+entity to prevent creating thousands of logger instances:
```csharp
// NPCBehaviorSystem: "Script.{behaviorTypeId}.{npcId}"
// TileBehaviorSystem: "TileBehavior.{behaviorTypeId}.{entity.Id}"
```

Separate caches prevent collision and allow tuned cache limits.

---

## 4. Common Ground (Already Unified)

### ScriptContext (ScriptContext.cs, Line 55-459)

**Both systems use identical ScriptContext instances**:
- World access: `ctx.World`
- Entity access: `ctx.Entity`
- Logging: `ctx.Logger`
- API services: `ctx.Player`, `ctx.Npc`, `ctx.Map`, `ctx.GameState`, etc.
- Component access: `ctx.GetState<T>()`, `ctx.SetState<T>()`, `ctx.HasState<T>()`

**No duplication here** - both share the same context implementation.

### Stateless Pattern

Both enforce **strict statelessness**:
- NO instance fields/properties in scripts
- State stored in ECS components via ScriptContext
- Single script instance shared across all entities
- Error isolation (one script error doesn't crash others)

### Lifecycle Hooks

TileBehaviorScriptBase **inherits** all TypeScriptBase lifecycle hooks:
- `OnInitialize()` - Available but rarely used for tiles
- `OnActivated()` - Used for tile initialization
- `OnTick()` - Available for per-frame tile effects (rare)
- `OnDeactivated()` - Used for tile cleanup

**Tiles get the full lifecycle**, they just don't always need it.

---

## 5. Real-World Usage Patterns

### TypeScriptBase Scripts (Entity Behaviors)

**WanderBehavior** (wander_behavior.csx):
- Uses: `OnActivated()`, `OnTick()`, `OnDeactivated()`
- State: `WanderState` component (wait timer, direction, movement count)
- Behavior: Random movement with obstacle avoidance
- Access: Position, GridMovement, MovementRequest components

**PatrolBehavior** (patrol_behavior.csx):
- Uses: `OnActivated()`, `OnTick()`, `OnDeactivated()`
- State: `PatrolState` component (waypoint index, wait timer)
- Behavior: Follow waypoint path with looping
- Access: MovementRoute, Position, GridMovement components

**Pattern**: Active behaviors with complex per-tick logic

### TileBehaviorScriptBase Scripts (Tile Behaviors)

**IceBehavior** (ice.csx):
- Uses: `GetForcedMovement()`, `AllowsRunning()`
- No OnTick needed - purely reactive
- Returns current direction for sliding
- Disables running

**JumpEastBehavior** (jump_east.csx):
- Uses: `IsBlockedFrom()`, `IsBlockedTo()`, `GetJumpDirection()`
- No OnTick needed - purely reactive
- One-way collision logic
- Jump mechanic only when moving east

**ImpassableBehavior** (impassable.csx):
- Uses: `IsBlockedFrom()`, `IsBlockedTo()`
- Simplest possible - blocks all movement
- No state, no OnTick, just collision

**Pattern**: Mostly reactive hooks, minimal OnTick usage

---

## 6. What Would Break if Merged?

### Scenario: Single Unified "ScriptBase"

**Problem 1: Interface Pollution**

If we merge everything into one base class:
```csharp
public abstract class UnifiedScriptBase {
    // Entity lifecycle
    OnInitialize(), OnActivated(), OnTick(), OnDeactivated()

    // Tile-specific hooks
    IsBlockedFrom(), IsBlockedTo(), GetForcedMovement(), etc.
}
```

**Issues**:
- NPC scripts forced to provide dummy implementations for tile hooks
- Tile scripts see NPC-specific metadata (DefaultSpeed, PauseAtWaypoint)
- Loss of semantic clarity - what's a "behavior" vs "tile behavior"?

**Problem 2: System Complexity**

Both systems would need to handle both script types:
```csharp
// NPCBehaviorSystem now needs tile logic checks
if (script is TileCapable) { /* check tile hooks */ }

// TileBehaviorSystem now needs NPC logic checks
if (script is EntityCapable) { /* check entity hooks */ }
```

**Problem 3: Registry Type Safety**

Current type-safe registries would break:
```csharp
TypeRegistry<BehaviorDefinition>        // NPCs
TypeRegistry<TileBehaviorDefinition>    // Tiles
```

Would become:
```csharp
TypeRegistry<UnifiedDefinition>  // Mixed bag, lost type info
```

**Problem 4: Performance Monitoring**

Current separation allows independent performance tracking:
- Tile behavior execution time
- NPC behavior execution time
- Separate logger caches
- Separate error isolation

Merging would muddy metrics.

---

## 7. Current Design Assessment

### ✅ Strengths

**1. Clear Separation of Concerns**
- Entity behaviors: Active, per-frame logic
- Tile behaviors: Reactive, query-based logic
- No confusion about which system handles what

**2. Type Safety**
- Different definition types prevent mismatched metadata
- Compile-time enforcement of tile-specific hooks
- Registry type safety (BehaviorDefinition vs TileBehaviorDefinition)

**3. Performance Optimization**
- Different system priorities (50 for tiles, 75 for NPCs)
- Query-based tile checks vs active NPC ticks
- Independent caching and error isolation

**4. Interface-Based Decoupling**
- ITileBehaviorSystem breaks circular dependencies
- CollisionService doesn't depend on TileBehaviorSystem directly
- MovementSystem queries tiles through interface

**5. Maintainability**
- Tile-specific code in TileBehaviorScriptBase
- NPC-specific code stays in TypeScriptBase
- Changes to tile logic don't affect NPC logic

### ⚠️ Weaknesses

**1. Perception of Duplication**
- Similar component structures (Behavior vs TileBehavior)
- Similar execution patterns in systems
- May confuse developers unfamiliar with the reasoning

**2. Code Similarity**
- NPCBehaviorSystem and TileBehaviorSystem share ~70% code structure
- Logger caching logic duplicated
- Error handling patterns repeated

**3. Documentation Gap**
- Why the separation exists isn't immediately clear from code
- Requires reading multiple files to understand the rationale
- No architectural decision record (ADR)

---

## 8. Unification Options

### Option A: Keep Current Design ✅ RECOMMENDED

**Approach**: No changes, improve documentation

**Pros**:
- No code changes needed
- No risk of breaking existing scripts
- Performance characteristics maintained
- Clear separation of concerns

**Cons**:
- Perceived duplication remains
- Code similarity between systems

**Recommendation**: ADD COMPREHENSIVE DOCUMENTATION
- Create architectural decision record (ADR)
- Document the "why" in both base classes
- Add inline comments explaining specialization

### Option B: Abstract Common System Logic

**Approach**: Extract shared system logic into base class

```csharp
public abstract class BehaviorSystemBase<TDefinition, TComponent>
    where TDefinition : IScriptedType
    where TComponent : struct
{
    // Shared: Logger caching, error handling, registry management
    protected abstract void ExecuteBehavior(/* ... */);
}

public class NPCBehaviorSystem : BehaviorSystemBase<BehaviorDefinition, Behavior> { }
public class TileBehaviorSystem : BehaviorSystemBase<TileBehaviorDefinition, TileBehavior> { }
```

**Pros**:
- Eliminates system code duplication
- Maintains script base class separation
- Reusable for future behavior systems

**Cons**:
- Adds complexity to system architecture
- Generic constraints may be confusing
- Doesn't address script base "duplication"

**Recommendation**: CONSIDER if adding more behavior system types (e.g., ItemBehaviorSystem)

### Option C: Composition Over Inheritance

**Approach**: Use interfaces instead of inheritance

```csharp
public interface IScriptBehavior {
    void OnInitialize(ScriptContext ctx);
    void OnActivated(ScriptContext ctx);
    void OnTick(ScriptContext ctx, float deltaTime);
    void OnDeactivated(ScriptContext ctx);
}

public interface ITileBehavior : IScriptBehavior {
    bool IsBlockedFrom(ScriptContext ctx, Direction from, Direction to);
    // ... other tile hooks
}

// Scripts implement interfaces directly
public class IceBehavior : ITileBehavior { /* ... */ }
public class WanderBehavior : IScriptBehavior { /* ... */ }
```

**Pros**:
- Clear interface contracts
- No inheritance hierarchy
- Easier to add new behavior types
- Scripts implement only what they need

**Cons**:
- MASSIVE BREAKING CHANGE - all scripts need rewrite
- Loss of default implementations (empty virtual methods)
- No shared base class utility methods
- Increased boilerplate in scripts

**Recommendation**: NOT WORTH THE MIGRATION COST

### Option D: Marker Interface Pattern

**Approach**: Single base class with marker interfaces

```csharp
public abstract class ScriptBase {
    // All lifecycle hooks (optional)
    public virtual void OnInitialize(ScriptContext ctx) { }
    public virtual void OnActivated(ScriptContext ctx) { }
    public virtual void OnTick(ScriptContext ctx, float deltaTime) { }
    public virtual void OnDeactivated(ScriptContext ctx) { }
}

public interface ITileCapable {
    bool IsBlockedFrom(ScriptContext ctx, Direction from, Direction to);
    // ... other tile hooks
}

// Tile scripts implement both
public class IceBehavior : ScriptBase, ITileCapable { /* ... */ }

// NPC scripts just extend base
public class WanderBehavior : ScriptBase { /* ... */ }
```

**Pros**:
- Single base class (unified)
- Tile capabilities opt-in via interface
- NPC scripts unaffected
- Systems check `is ITileCapable`

**Cons**:
- Tile scripts must implement interface (no default implementations)
- Systems need runtime type checks (`script is ITileCapable`)
- Loss of compile-time enforcement for tile hooks
- More boilerplate in tile scripts

**Recommendation**: COMPROMISES TOO MUCH for minimal gain

---

## 9. Recommendations

### Primary Recommendation: KEEP CURRENT DESIGN

**Rationale**:
1. **Inheritance is correct**: TileBehaviorScriptBase **already extends** TypeScriptBase
2. **Specialization is appropriate**: Tile behaviors need additional hooks
3. **Performance is optimized**: Different execution patterns warrant different systems
4. **Type safety is preserved**: Compile-time enforcement of tile hooks
5. **No breaking changes**: All existing scripts continue working

**Action Items**:

**✅ IMMEDIATE** (Documentation):
1. Add architectural decision record (ADR) explaining separation
2. Update TypeScriptBase.cs with "why not unified?" comments
3. Update TileBehaviorScriptBase.cs with "why inherit?" comments
4. Create diagram showing inheritance + system relationships
5. Add this analysis to repository docs

**⚠️ OPTIONAL** (Code Improvements):
6. Extract common system logic to `BehaviorSystemBase<TDef, TCmp>` if adding more behavior systems
7. Add `[Obsolete]` warnings if migrating to new pattern in future
8. Consider builder pattern for ScriptContext creation (reduce boilerplate)

### Secondary Recommendation: Monitor for New Behavior Types

**If PokeSharp adds more behavior types** (items, weather, time-of-day):
- **THEN** consider `BehaviorSystemBase<TDef, TCmp>` for system logic reuse
- **KEEP** separate script base classes for each domain (ItemScriptBase, WeatherScriptBase)
- **MAINTAIN** specialization pattern (domain-specific hooks)

### Tertiary Recommendation: Document Common Patterns

Create **scripting guide** covering:
- When to use TypeScriptBase (entity behaviors)
- When to use TileBehaviorScriptBase (tile behaviors)
- How ScriptContext works (stateless pattern)
- Component state management examples
- Performance considerations (query vs tick)

---

## 10. Conclusion

### The "Duplication" is Actually Specialization

PokeSharp's script base classes are **not duplicated** - they follow **proper object-oriented design**:

```
TypeScriptBase (General Purpose)
    ↓ inherits
TileBehaviorScriptBase (Tile Specialization)
```

This is like:

```
Animal (General Purpose)
    ↓ inherits
Bird (Specialization with fly() method)
```

You wouldn't merge Bird back into Animal just because they share eat() and sleep().

### Key Insights

1. **TileBehaviorScriptBase inherits from TypeScriptBase** - they're already unified at the foundation
2. **Tile behaviors have domain-specific requirements** (collision, forced movement, encounters)
3. **Different execution patterns** justify different systems (reactive vs active)
4. **Type safety and performance** are preserved through specialization
5. **No code duplication exists** - shared logic is in ScriptContext and base classes

### Final Verdict

**DO NOT MERGE** - The current design is architecturally sound. Address the perceived duplication through **documentation**, not refactoring.

---

## Appendix A: File Reference

### Core Classes
- **TypeScriptBase**: `/PokeSharp.Game.Scripting/Runtime/TypeScriptBase.cs`
- **TileBehaviorScriptBase**: `/PokeSharp.Game.Scripting/Runtime/TileBehaviorScriptBase.cs`
- **ScriptContext**: `/PokeSharp.Game.Scripting/Runtime/ScriptContext.cs`

### Systems
- **NPCBehaviorSystem**: `/PokeSharp.Game.Scripting/Systems/NPCBehaviorSystem.cs`
- **TileBehaviorSystem**: `/PokeSharp.Game.Scripting/Systems/TileBehaviorSystem.cs`

### Definitions
- **BehaviorDefinition**: `/PokeSharp.Engine.Core/Types/BehaviorDefinition.cs`
- **TileBehaviorDefinition**: `/PokeSharp.Engine.Core/Types/TileBehaviorDefinition.cs`

### Components
- **Behavior**: `/PokeSharp.Game.Components/Components/NPCs/Behavior.cs`
- **TileBehavior**: `/PokeSharp.Game.Components/Components/Tiles/TileBehavior.cs`

### Interfaces
- **ITileBehaviorSystem**: `/PokeSharp.Game.Components/Interfaces/ITileBehaviorSystem.cs`

### Example Scripts

**TypeScriptBase Usage**:
- `/PokeSharp.Game/Assets/Scripts/Behaviors/wander_behavior.csx`
- `/PokeSharp.Game/Assets/Scripts/Behaviors/patrol_behavior.csx`
- `/PokeSharp.Game/Assets/Scripts/Behaviors/guard_behavior.csx`

**TileBehaviorScriptBase Usage**:
- `/PokeSharp.Game/Assets/Scripts/TileBehaviors/ice.csx`
- `/PokeSharp.Game/Assets/Scripts/TileBehaviors/jump_east.csx`
- `/PokeSharp.Game/Assets/Scripts/TileBehaviors/impassable.csx`

---

## Appendix B: Comparison Table

| Aspect | TypeScriptBase | TileBehaviorScriptBase |
|--------|---------------|----------------------|
| **Inheritance** | Abstract base class | Inherits from TypeScriptBase |
| **Lifecycle Hooks** | 4 hooks (OnInitialize, OnActivated, OnTick, OnDeactivated) | Inherits all 4 + adds 7 tile-specific hooks |
| **Primary Hook** | OnTick() | Tile-specific query methods |
| **Execution Model** | Active (every frame) | Reactive (queried on-demand) |
| **System** | NPCBehaviorSystem | TileBehaviorSystem |
| **System Priority** | 75 (after tiles) | 50 (before NPCs) |
| **Query** | Npc + Behavior | TilePosition + TileBehavior |
| **Definition Type** | BehaviorDefinition | TileBehaviorDefinition |
| **Component Type** | Behavior struct | TileBehavior struct |
| **Interface** | None | ITileBehaviorSystem |
| **Use Cases** | NPCs, entities, general behaviors | Tiles, collision, encounters, terrain |
| **Examples** | wander, patrol, guard | ice, ledges, impassable, tall_grass |
| **State Storage** | ECS components via ScriptContext | ECS components via ScriptContext |
| **Stateless Requirement** | Yes (enforced via pattern) | Yes (inherited from TypeScriptBase) |
| **Logger Caching** | Per behavior+npc | Per behavior+tile |
| **Error Isolation** | Per-NPC | Per-tile |

---

**Document Version**: 1.0
**Date**: 2025-12-02
**Author**: Research Agent (Claude Code)
**Status**: Complete Analysis
