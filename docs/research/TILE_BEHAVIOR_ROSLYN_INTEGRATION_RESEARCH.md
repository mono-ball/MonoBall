# Tile Behavior System: Roslyn Integration Research

## Overview

This document researches how to translate Pokemon Emerald's hardcoded C behavior functions into a Roslyn-based scripted system for PokeSharp, replacing the custom ledge implementation with a unified behavior system.

---

## Current System Analysis

### Your Current Architecture

1. **NPC Behavior System** (Already Implemented)
   - `Behavior` component references behavior type ID
   - `BehaviorDefinition` loaded from JSON
   - Roslyn scripts via `TypeScriptBase` with lifecycle hooks
   - `NPCBehaviorSystem` executes scripts per-frame
   - Type registry pattern

2. **Tile Components** (Current)
   - `TileScript` - For interaction scripts (PC, heal tiles, etc.)
   - `TileLedge` - Custom component for ledge jumping
   - `Collision` - Solid/non-solid collision
   - Entity-based (one entity per tile)

3. **Collision/Movement System**
   - `CollisionService` - Checks if position is walkable
   - `MovementSystem` - Handles movement requests
   - Custom ledge logic in `MovementSystem.TryStartMovement()`
   - Directional blocking via `TileLedge.IsBlockedFrom()`

### Pokemon Emerald's System

1. **Behavior IDs** - 0-244 numeric IDs stored in binary
2. **Hardcoded Functions** - C functions like `MetatileBehavior_IsJumpSouth()`
3. **Collision Integration** - Behaviors checked during `GetCollisionAtCoords()`
4. **Forced Movement** - Behaviors trigger automatic movement (ice, currents)
5. **Unified System** - All tile logic (ledges, water, doors) uses same behavior system

---

## Proposed Architecture

### 1. Tile Behavior Component

Similar to your `Behavior` component for NPCs:

```csharp
namespace PokeSharp.Game.Components.Tiles;

/// <summary>
///     Component that references a tile behavior type.
///     Links a tile entity to a moddable behavior from the TypeRegistry.
///     Replaces TileLedge, Collision flags, and other tile-specific components.
/// </summary>
public struct TileBehavior
{
    /// <summary>
    ///     Type identifier for the behavior (e.g., "jump_south", "impassable_east", "ice").
    ///     References a type in the TileBehaviorDefinition TypeRegistry.
    /// </summary>
    public string BehaviorTypeId { get; set; }
    
    /// <summary>
    ///     Whether this behavior is currently active.
    /// </summary>
    public bool IsActive { get; set; }
    
    /// <summary>
    ///     Whether this behavior has been initialized.
    /// </summary>
    public bool IsInitialized { get; set; }
    
    public TileBehavior(string behaviorTypeId)
    {
        BehaviorTypeId = behaviorTypeId;
        IsActive = true;
        IsInitialized = false;
    }
}
```

### 2. Tile Behavior Definition

Similar to `BehaviorDefinition`:

```csharp
namespace PokeSharp.Engine.Core.Types;

/// <summary>
///     Type definition for tile behaviors.
///     Loaded from JSON and used by the TypeRegistry system.
/// </summary>
public record TileBehaviorDefinition : IScriptedType
{
    /// <summary>
    ///     Unique identifier for this behavior type (e.g., "jump_south", "ice", "tall_grass").
    /// </summary>
    public required string TypeId { get; init; }
    
    /// <summary>
    ///     Display name for this behavior.
    /// </summary>
    public required string DisplayName { get; init; }
    
    /// <summary>
    ///     Description of what this behavior does.
    /// </summary>
    public string? Description { get; init; }
    
    /// <summary>
    ///     Path to the Roslyn .csx script file that implements this behavior.
    ///     Relative to the Scripts directory.
    /// </summary>
    public string? BehaviorScript { get; init; }
    
    /// <summary>
    ///     Behavior flags (for fast lookup without script execution).
    ///     Similar to Pokemon Emerald's sTileBitAttributes.
    /// </summary>
    public TileBehaviorFlags Flags { get; init; } = TileBehaviorFlags.None;
}

[Flags]
public enum TileBehaviorFlags
{
    None = 0,
    HasEncounters = 1 << 0,  // Can trigger wild Pokemon encounters
    Surfable = 1 << 1,       // Requires Surf HM
    BlocksMovement = 1 << 2, // Blocks all movement (impassable)
    ForcesMovement = 1 << 3, // Forces automatic movement (ice, currents)
    DisablesRunning = 1 << 4, // Prevents running
    // ... etc
}
```

### 3. Tile Behavior Script Base Class

New base class for tile behavior scripts:

```csharp
namespace PokeSharp.Game.Scripting.Runtime;

/// <summary>
///     Base class for tile behavior scripts.
///     Provides hooks for collision checking, forced movement, and interactions.
/// </summary>
public abstract class TileBehaviorScriptBase : TypeScriptBase
{
    /// <summary>
    ///     Called when checking if movement is blocked from a direction.
    ///     Return true to block movement, false to allow.
    /// </summary>
    /// <param name="ctx">Script context</param>
    /// <param name="fromDirection">Direction entity is moving from</param>
    /// <param name="toDirection">Direction entity wants to move to</param>
    /// <returns>True if movement is blocked, false if allowed</returns>
    public virtual bool IsBlockedFrom(ScriptContext ctx, Direction fromDirection, Direction toDirection)
    {
        return false; // Default: allow movement
    }
    
    /// <summary>
    ///     Called when checking if movement is blocked to a direction.
    ///     Return true to block movement, false to allow.
    /// </summary>
    /// <param name="ctx">Script context</param>
    /// <param name="toDirection">Direction entity wants to move to</param>
    /// <returns>True if movement is blocked, false if allowed</returns>
    public virtual bool IsBlockedTo(ScriptContext ctx, Direction toDirection)
    {
        return false; // Default: allow movement
    }
    
    /// <summary>
    ///     Called to check if this tile forces movement.
    ///     Return the direction to force movement, or Direction.None for no forced movement.
    /// </summary>
    /// <param name="ctx">Script context</param>
    /// <param name="currentDirection">Current movement direction</param>
    /// <returns>Direction to force movement, or Direction.None</returns>
    public virtual Direction GetForcedMovement(ScriptContext ctx, Direction currentDirection)
    {
        return Direction.None; // Default: no forced movement
    }
    
    /// <summary>
    ///     Called when checking if this tile allows jumping (ledges).
    ///     Return the jump direction if allowed, or Direction.None.
    /// </summary>
    /// <param name="ctx">Script context</param>
    /// <param name="fromDirection">Direction entity is moving from</param>
    /// <returns>Jump direction if allowed, or Direction.None</returns>
    public virtual Direction GetJumpDirection(ScriptContext ctx, Direction fromDirection)
    {
        return Direction.None; // Default: no jumping
    }
    
    /// <summary>
    ///     Called when checking if this tile requires special movement mode (surf, dive).
    /// </summary>
    /// <param name="ctx">Script context</param>
    /// <returns>Required movement mode, or null</returns>
    public virtual MovementMode? GetRequiredMovementMode(ScriptContext ctx)
    {
        return null; // Default: no special mode required
    }
    
    /// <summary>
    ///     Called when checking if running is allowed on this tile.
    /// </summary>
    /// <param name="ctx">Script context</param>
    /// <returns>True if running is allowed, false if disabled</returns>
    public virtual bool AllowsRunning(ScriptContext ctx)
    {
        return true; // Default: allow running
    }
    
    /// <summary>
    ///     Called when entity steps onto this tile.
    ///     Use for per-step effects (ice cracking, ash gathering, etc.).
    /// </summary>
    /// <param name="ctx">Script context</param>
    /// <param name="entity">Entity that stepped on tile</param>
    public virtual void OnStep(ScriptContext ctx, Entity entity) { }
}
```

### 4. Tile Behavior System

Similar to `NPCBehaviorSystem`, but for tiles:

```csharp
namespace PokeSharp.Game.Systems;

/// <summary>
///     System that executes tile behavior scripts.
///     Handles collision checking, forced movement, and tile interactions.
/// </summary>
public class TileBehaviorSystem : BaseSystem
{
    private readonly IBehaviorRegistry _behaviorRegistry;
    private readonly IScriptService _scriptService;
    private readonly ILogger<TileBehaviorSystem> _logger;
    
    // Cache compiled scripts
    private readonly Dictionary<string, TileBehaviorScriptBase> _scriptCache = new();
    
    public void Update(World world, float deltaTime)
    {
        // Query all tiles with behavior components
        world.Query(
            in EcsQueries.TilesWithBehaviors,
            (Entity entity, ref TileBehavior behavior) =>
            {
                if (!behavior.IsActive)
                    return;
                
                // Get script from cache or load it
                var script = GetOrLoadScript(behavior.BehaviorTypeId);
                if (script == null)
                    return;
                
                // Create context
                var context = new ScriptContext(world, entity, _logger, _apis);
                
                // Initialize if needed
                if (!behavior.IsInitialized)
                {
                    script.OnActivated(context);
                    behavior.IsInitialized = true;
                }
                
                // Execute per-step logic (for ice, cracked floors, etc.)
                script.OnTick(context, deltaTime);
            }
        );
    }
    
    /// <summary>
    ///     Checks if movement is blocked by tile behaviors.
    ///     Called by CollisionService.
    /// </summary>
    public bool IsMovementBlocked(
        World world,
        Entity tileEntity,
        Direction fromDirection,
        Direction toDirection
    )
    {
        if (!tileEntity.Has<TileBehavior>())
            return false;
        
        ref var behavior = ref tileEntity.Get<TileBehavior>();
        if (!behavior.IsActive)
            return false;
        
        var script = GetOrLoadScript(behavior.BehaviorTypeId);
        if (script == null)
            return false;
        
        var context = new ScriptContext(world, tileEntity, null, _apis);
        
        // Check both directions (like Pokemon Emerald's two-way check)
        if (script.IsBlockedFrom(context, fromDirection, toDirection))
            return true;
        
        if (script.IsBlockedTo(context, toDirection))
            return true;
        
        return false;
    }
    
    /// <summary>
    ///     Gets forced movement direction from tile behaviors.
    ///     Called by MovementSystem.
    /// </summary>
    public Direction GetForcedMovement(
        World world,
        Entity tileEntity,
        Direction currentDirection
    )
    {
        if (!tileEntity.Has<TileBehavior>())
            return Direction.None;
        
        ref var behavior = ref tileEntity.Get<TileBehavior>();
        if (!behavior.IsActive)
            return Direction.None;
        
        var script = GetOrLoadScript(behavior.BehaviorTypeId);
        if (script == null)
            return Direction.None;
        
        var context = new ScriptContext(world, tileEntity, null, _apis);
        return script.GetForcedMovement(context, currentDirection);
    }
    
    /// <summary>
    ///     Gets jump direction from tile behaviors (replaces TileLedge).
    ///     Called by MovementSystem.
    /// </summary>
    public Direction GetJumpDirection(
        World world,
        Entity tileEntity,
        Direction fromDirection
    )
    {
        if (!tileEntity.Has<TileBehavior>())
            return Direction.None;
        
        ref var behavior = ref tileEntity.Get<TileBehavior>();
        if (!behavior.IsActive)
            return Direction.None;
        
        var script = GetOrLoadScript(behavior.BehaviorTypeId);
        if (script == null)
            return Direction.None;
        
        var context = new ScriptContext(world, tileEntity, null, _apis);
        return script.GetJumpDirection(context, fromDirection);
    }
}
```

---

## Example: Ledge Behavior Script

Replace `TileLedge` component with a behavior script:

**JSON Definition:**
```json
{
  "typeId": "jump_south",
  "displayName": "Jump South Ledge",
  "description": "Allows jumping south but blocks north movement",
  "behaviorScript": "tiles/jump_south.csx",
  "flags": ["BlocksMovement"]
}
```

**Roslyn Script (`tiles/jump_south.csx`):**
```csharp
using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Scripting.Runtime;

public class JumpSouthBehavior : TileBehaviorScriptBase
{
    public override bool IsBlockedFrom(ScriptContext ctx, Direction fromDirection, Direction toDirection)
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
        // Allow jumping south
        if (fromDirection == Direction.North)
            return Direction.South;
        
        return Direction.None;
    }
}
```

---

## Example: Ice Behavior Script

**JSON Definition:**
```json
{
  "typeId": "ice",
  "displayName": "Ice Tile",
  "description": "Forces sliding movement",
  "behaviorScript": "tiles/ice.csx",
  "flags": ["ForcesMovement"]
}
```

**Roslyn Script (`tiles/ice.csx`):**
```csharp
using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Scripting.Runtime;

public class IceBehavior : TileBehaviorScriptBase
{
    public override Direction GetForcedMovement(ScriptContext ctx, Direction currentDirection)
    {
        // Continue sliding in current direction
        if (currentDirection != Direction.None)
            return currentDirection;
        
        return Direction.None;
    }
    
    public override bool AllowsRunning(ScriptContext ctx)
    {
        // Can't run on ice
        return false;
    }
}
```

---

## Example: Impassable East Behavior Script

**JSON Definition:**
```json
{
  "typeId": "impassable_east",
  "displayName": "Impassable East",
  "description": "Blocks movement from east",
  "behaviorScript": "tiles/impassable_east.csx",
  "flags": ["BlocksMovement"]
}
```

**Roslyn Script (`tiles/impassable_east.csx`):**
```csharp
using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Scripting.Runtime;

public class ImpassableEastBehavior : TileBehaviorScriptBase
{
    public override bool IsBlockedFrom(ScriptContext ctx, Direction fromDirection, Direction toDirection)
    {
        // Block if moving from east
        if (fromDirection == Direction.East)
            return true;
        
        return false;
    }
    
    public override bool IsBlockedTo(ScriptContext ctx, Direction toDirection)
    {
        // Block if trying to enter from east
        if (toDirection == Direction.East)
            return true;
        
        return false;
    }
}
```

---

## Integration with Collision System

### Modified CollisionService

```csharp
public class CollisionService : ICollisionService
{
    private readonly TileBehaviorSystem _tileBehaviorSystem;
    
    public bool IsPositionWalkable(
        int mapId,
        int tileX,
        int tileY,
        Direction fromDirection = Direction.None,
        byte entityElevation = Elevation.Default
    )
    {
        var entities = _spatialQuery.GetEntitiesAt(mapId, tileX, tileY);
        
        foreach (var entity in entities)
        {
            // Check elevation (existing logic)
            if (entity.Has<Elevation>())
            {
                ref var elevation = ref entity.Get<Elevation>();
                if (elevation.Value != entityElevation)
                    continue;
            }
            
            // NEW: Check tile behaviors for blocking
            if (entity.Has<TileBehavior>())
            {
                var toDirection = GetOppositeDirection(fromDirection);
                if (_tileBehaviorSystem.IsMovementBlocked(
                    _world,
                    entity,
                    fromDirection,
                    toDirection
                ))
                {
                    return false;
                }
            }
            
            // Existing collision check (for non-behavior entities)
            if (entity.Has<Collision>())
            {
                ref var collision = ref entity.Get<Collision>();
                if (collision.IsSolid)
                    return false;
            }
        }
        
        return true;
    }
}
```

---

## Integration with Movement System

### Modified MovementSystem

```csharp
private void TryStartMovement(
    World world,
    Entity entity,
    ref Position position,
    ref GridMovement movement,
    Direction direction
)
{
    // ... existing boundary checks ...
    
    // NEW: Check for forced movement from current tile
    var currentTile = GetTileAt(world, position.MapId, position.X, position.Y);
    if (currentTile.Has<TileBehavior>())
    {
        var forcedDir = _tileBehaviorSystem.GetForcedMovement(
            world,
            currentTile,
            direction
        );
        
        if (forcedDir != Direction.None)
        {
            // Override direction with forced movement
            direction = forcedDir;
        }
    }
    
    // Calculate target position
    var targetX = position.X;
    var targetY = position.Y;
    // ... move calculation ...
    
    // NEW: Check for jump behavior (replaces TileLedge logic)
    var targetTile = GetTileAt(world, position.MapId, targetX, targetY);
    if (targetTile.Has<TileBehavior>())
    {
        var jumpDir = _tileBehaviorSystem.GetJumpDirection(
            world,
            targetTile,
            direction
        );
        
        if (jumpDir != Direction.None && jumpDir == direction)
        {
            // Perform jump (existing jump logic)
            var jumpLandX = targetX;
            var jumpLandY = targetY;
            // ... calculate landing position ...
            
            movement.StartMovement(jumpStart, jumpEnd);
            return;
        }
    }
    
    // ... existing movement logic ...
}
```

---

## Migration Path

### Phase 1: Add Tile Behavior System
1. Create `TileBehavior` component
2. Create `TileBehaviorDefinition` type
3. Create `TileBehaviorScriptBase` class
4. Create `TileBehaviorSystem`
5. Register in type registry

### Phase 2: Create Behavior Scripts
1. Create scripts for common behaviors:
   - `jump_south`, `jump_north`, `jump_east`, `jump_west`
   - `impassable_east`, `impassable_west`, etc.
   - `ice`, `water_current_east`, etc.
2. Load scripts into registry

### Phase 3: Integrate with Collision
1. Modify `CollisionService` to check behaviors
2. Test collision blocking

### Phase 4: Integrate with Movement
1. Modify `MovementSystem` to check forced movement
2. Replace ledge logic with behavior checks
3. Test forced movement (ice, currents)

### Phase 5: Migrate Existing Tiles
1. Convert `TileLedge` components to `TileBehavior` with jump scripts
2. Update map loaders to use behaviors
3. Remove `TileLedge` component and mapper

### Phase 6: Cleanup
1. Remove `TileLedge` component
2. Remove `LedgeMapper`
3. Remove custom ledge logic from `MovementSystem`

---

## Benefits

### 1. Unified System
- All tile logic uses same pattern (behaviors)
- Consistent with NPC behavior system
- Easier to understand and maintain

### 2. Moddability
- Behaviors defined in JSON
- Scripts can be modified without recompiling
- Easy to add new behaviors

### 3. Flexibility
- Scripts can implement complex logic
- Can combine multiple behaviors per tile
- Can access world state, other entities, etc.

### 4. Performance
- Scripts cached after first load
- Flags allow fast checks without script execution
- Only active behaviors execute

### 5. Removes Custom Code
- No more `TileLedge` component
- No more custom ledge logic in `MovementSystem`
- All logic in scripts

---

## Performance Considerations

### Optimization Strategies

1. **Flags for Fast Checks**
   - Check `TileBehaviorFlags` before executing scripts
   - Only call scripts when necessary

2. **Script Caching**
   - Cache compiled scripts in `TileBehaviorSystem`
   - Reuse script instances across tiles

3. **Batch Queries**
   - Query all tiles with behaviors at once
   - Process in batches

4. **Lazy Script Loading**
   - Only load scripts when needed
   - Unload unused scripts

---

## Comparison: Before vs After

### Before (Current System)
```csharp
// Custom component
public struct TileLedge { Direction JumpDirection; }

// Custom logic in MovementSystem
if (isLedge && direction == allowedJumpDir) { /* jump */ }

// Custom logic in CollisionService
if (entity.Has<TileLedge>() && ledge.IsBlockedFrom(fromDirection)) { return false; }
```

### After (Behavior System)
```csharp
// Unified component
public struct TileBehavior { string BehaviorTypeId; }

// Behavior script handles logic
public class JumpSouthBehavior : TileBehaviorScriptBase
{
    public override Direction GetJumpDirection(...) { return Direction.South; }
    public override bool IsBlockedFrom(...) { /* logic */ }
}

// System calls behavior
var jumpDir = _tileBehaviorSystem.GetJumpDirection(...);
```

---

## Research Questions

1. **Script Execution Performance**
   - How much overhead does Roslyn script execution add?
   - Should we cache script results?
   - Can we optimize hot paths?

2. **Behavior Composition**
   - Can a tile have multiple behaviors?
   - How do we handle conflicts?
   - Should we support behavior priorities?

3. **State Management**
   - How do tile behaviors store state (ice cracking, floor breaking)?
   - Use ScriptContext state or component state?

4. **Interaction with Existing Systems**
   - How do behaviors interact with `TileScript` (interaction scripts)?
   - Should behaviors replace `TileScript` or complement it?

5. **Migration Complexity**
   - How many existing maps use `TileLedge`?
   - Can we auto-convert or require manual migration?

---

## Next Steps

1. **Prototype** - Create minimal `TileBehaviorSystem` with one behavior
2. **Benchmark** - Measure script execution overhead
3. **Design Review** - Review API design with team
4. **Implement** - Follow migration path above
5. **Test** - Ensure all existing functionality works

---

## Conclusion

Translating Pokemon Emerald's behavior system to Roslyn scripts provides:
- **Unified architecture** - Same pattern for NPCs and tiles
- **Moddability** - Scripts can be modified without recompiling
- **Flexibility** - Complex logic in scripts, not hardcoded
- **Maintainability** - Less custom code, more reusable patterns

The main trade-off is **performance** - script execution is slower than hardcoded C functions, but the flexibility and moddability benefits likely outweigh this for a moddable game engine.

