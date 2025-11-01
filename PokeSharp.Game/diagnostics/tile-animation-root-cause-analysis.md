# Tile Animation Root Cause Analysis

## Executive Summary

**ROOT CAUSE IDENTIFIED**: Architecture mismatch between entity creation and system query logic.

The TileAnimationSystem queries for entities that have **BOTH** `TileMap` and `AnimatedTile` components on the **SAME** entity. However, the code creates these components on **SEPARATE** entities, causing the query to never match anything.

## Problem Statement

Tile animations (water ripples, grass swaying) are not working in the game despite:
- TileAnimationSystem being registered and initialized
- Animated tile data being successfully loaded from the map file
- The system logic appearing correct

## Architecture Analysis

### Current Entity Creation (PokeSharpGame.cs:159-186)

```csharp
// Load test map from JSON
var tileMap = _mapLoader.LoadMap("Assets/Maps/test-map.json");
var tileCollider = _mapLoader.LoadCollision("Assets/Maps/test-map.json");
var animatedTiles = _mapLoader.LoadAnimatedTiles("Assets/Maps/test-map.json");

// Create map entity with TileMap and TileCollider components
var mapEntity = _world.Create(tileMap, tileCollider);

// Create entities for each animated tile
foreach (var animTile in animatedTiles)
{
    _world.Create(animTile);  // ❌ SEPARATE entities!
}
```

**Result**:
- Entity #1: Has `TileMap` + `TileCollider`
- Entity #2: Has `AnimatedTile` (water)
- Entity #3: Has `AnimatedTile` (grass)
- Entity #4: Has `AnimatedTile` (flowers)

### System Query (TileAnimationSystem.cs:23)

```csharp
_tileMapQuery = new QueryDescription().WithAll<TileMap, AnimatedTile>();
```

**This query requires**: BOTH components on the SAME entity.

### Query Match Result

```
Query: Find entities with [TileMap AND AnimatedTile]
Entity #1: [TileMap, TileCollider] ❌ Missing AnimatedTile
Entity #2: [AnimatedTile]           ❌ Missing TileMap
Entity #3: [AnimatedTile]           ❌ Missing TileMap
Entity #4: [AnimatedTile]           ❌ Missing TileMap

MATCHES: 0
```

**The query NEVER matches any entities**, so `TileAnimationSystem.Update()` never executes its animation logic.

## Why This Design Doesn't Work

### The Conceptual Mismatch

1. **AnimatedTile** represents metadata about animation frames (frame IDs, durations)
2. **TileMap** represents the actual tile grid that needs to be modified
3. **TileAnimationSystem** needs BOTH to:
   - Read animation frame data from `AnimatedTile`
   - Update tile IDs in `TileMap` layers

### The ECS Problem

In Arch ECS (and most ECS frameworks), a query like `WithAll<A, B>` finds entities that have components A **AND** B on the **SAME** entity. There's no built-in way to say "find all pairs where component A is on one entity and component B is on another."

## Solution Options

### Option 1: Store AnimatedTile ON the TileMap Entity (Recommended)

**Change**: Modify entity creation to add AnimatedTile components to the map entity.

**Pros**:
- Minimal code changes
- Keeps current system logic intact
- Matches the query naturally

**Cons**:
- Multiple AnimatedTile components on one entity (requires Arch support or array storage)

**Implementation**:

```csharp
// Option 1A: Multiple AnimatedTile components (if Arch supports it)
var mapEntity = _world.Create(tileMap, tileCollider);
foreach (var animTile in animatedTiles)
{
    _world.Add(mapEntity, animTile);
}

// Option 1B: Store array in TileMap component
public struct TileMap
{
    // ... existing fields ...
    public AnimatedTile[] AnimatedTiles { get; set; }
}

var tileMap = _mapLoader.LoadMap("...");
tileMap.AnimatedTiles = _mapLoader.LoadAnimatedTiles("...");
var mapEntity = _world.Create(tileMap, tileCollider);
```

### Option 2: Change System to Query Separately

**Change**: Modify TileAnimationSystem to query AnimatedTile entities separately and find the TileMap entity.

**Pros**:
- Keeps entity creation as-is
- More flexible architecture

**Cons**:
- More complex system logic
- Performance overhead (finding TileMap entity each frame)
- Need to handle missing TileMap gracefully

**Implementation**:

```csharp
public override void Initialize(World world)
{
    base.Initialize(world);
    _animatedTileQuery = new QueryDescription().WithAll<AnimatedTile>();
    _tileMapQuery = new QueryDescription().WithAll<TileMap>();
}

public override void Update(World world, float deltaTime)
{
    // Find THE tilemap entity (should be only one)
    Entity tileMapEntity = Entity.Null;
    TileMap tileMap = default;

    world.Query(in _tileMapQuery, (Entity entity, ref TileMap map) =>
    {
        tileMapEntity = entity;
        tileMap = map;
    });

    if (tileMapEntity == Entity.Null)
        return;

    // Update each animated tile
    world.Query(in _animatedTileQuery, (Entity entity, ref AnimatedTile animTile) =>
    {
        UpdateTileAnimation(ref tileMap, ref animTile, deltaTime);
    });

    // Write the modified TileMap back
    world.Set(tileMapEntity, tileMap);
}
```

### Option 3: Use Entity Reference in AnimatedTile

**Change**: Store a reference to the TileMap entity in each AnimatedTile.

**Pros**:
- Explicit relationship
- Flexible for multiple TileMaps

**Cons**:
- More complex data model
- Need to manage entity references

**Implementation**:

```csharp
public struct AnimatedTile
{
    // ... existing fields ...
    public Entity TileMapEntity { get; set; }
}

// System queries AnimatedTile and retrieves TileMap by reference
world.Query(in _animatedTileQuery, (ref AnimatedTile animTile) =>
{
    if (world.TryGet(animTile.TileMapEntity, out TileMap tileMap))
    {
        UpdateTileAnimation(ref tileMap, ref animTile, deltaTime);
        world.Set(animTile.TileMapEntity, tileMap);
    }
});
```

## Recommended Solution

**Use Option 1B**: Store AnimatedTile array in TileMap component.

### Rationale

1. **Simplicity**: Minimal changes to existing code
2. **Performance**: Single query, no entity lookups
3. **Logical**: All tile data lives together on the map entity
4. **Maintainability**: Clear data ownership

### Implementation Steps

1. **Modify TileMap component** to include AnimatedTile array
2. **Update MapLoader** to return AnimatedTiles with TileMap
3. **Update PokeSharpGame** to create single entity with all map data
4. **Update TileAnimationSystem** to iterate over AnimatedTiles array

## Verification Plan

After implementing the fix:

1. **Compile**: Ensure no build errors
2. **Run**: Start the game and load test map
3. **Observe**: Watch for tile animations (water should ripple every 0.5s)
4. **Console**: Check for TileAnimationSystem output
5. **Debug**: Add logging to verify system Update() is called

## Additional Findings

### System Registration

```csharp
// TileAnimationSystem is correctly registered (line 98)
_systemManager.RegisterSystem(new TileAnimationSystem());
```

✅ System is registered with correct priority (850).

### Data Loading

```csharp
// AnimatedTiles are successfully loaded (line 166)
var animatedTiles = _mapLoader.LoadAnimatedTiles("Assets/Maps/test-map.json");
```

✅ MapLoader.LoadAnimatedTiles() correctly parses animation data from Tiled JSON.

### System Logic

The animation update logic in TileAnimationSystem is **correct**:
- Frame timer increments correctly
- Frame advance logic works properly
- TileMap layer updates are implemented correctly

**The only issue is the query never matches any entities.**

## Conclusion

The tile animation system has a **fundamental architecture flaw** where the system expects components to be co-located on the same entity, but the entity creation code separates them. This is a classic ECS "query mismatch" bug.

**Fix**: Restructure data to store AnimatedTile information WITH the TileMap entity, not as separate entities.

**Estimated effort**: 30-60 minutes for Option 1B implementation.

---

**Analysis Date**: 2025-11-01
**Analyzer**: Code Review Agent
**Status**: Root cause confirmed, solution recommended
