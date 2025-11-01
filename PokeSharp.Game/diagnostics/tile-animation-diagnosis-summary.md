# Tile Animation Diagnosis Summary

## Root Cause: Query Mismatch

**Problem**: TileAnimationSystem queries for entities with BOTH `TileMap` AND `AnimatedTile` components on the SAME entity, but these components are created on SEPARATE entities.

## Evidence Chain

### 1. Entity Creation (PokeSharpGame.cs:168-175)

```csharp
// Create map entity with TileMap and TileCollider components
var mapEntity = _world.Create(tileMap, tileCollider);

// Create entities for each animated tile
foreach (var animTile in animatedTiles)
{
    _world.Create(animTile);  // ❌ Creates SEPARATE entities!
}
```

**Result**:
- 1 entity with `[TileMap, TileCollider]`
- N entities with `[AnimatedTile]` (one per animated tile type)

### 2. System Query (TileAnimationSystem.cs:23)

```csharp
_tileMapQuery = new QueryDescription().WithAll<TileMap, AnimatedTile>();
```

**Requires**: BOTH components on the SAME entity.

### 3. Query Match Result

```
Entity with [TileMap, TileCollider]  ❌ No AnimatedTile
Entities with [AnimatedTile]         ❌ No TileMap

TOTAL MATCHES: 0
```

### 4. Comparison with Working Systems

**CollisionSystem** (works correctly):
```csharp
var query = new QueryDescription().WithAll<TileMap, TileCollider>();
```
✅ BOTH components are on the SAME entity (line 169)

**MapRenderSystem** (works correctly):
```csharp
var query = new QueryDescription().WithAll<TileMap>();
```
✅ Queries for TileMap which exists on the map entity

**TileAnimationSystem** (broken):
```csharp
var query = new QueryDescription().WithAll<TileMap, AnimatedTile>();
```
❌ Components are on DIFFERENT entities

## Why CollisionSystem Works But TileAnimationSystem Doesn't

```csharp
// This works because BOTH components are added to the SAME entity:
var mapEntity = _world.Create(tileMap, tileCollider);
//                              ^^^^^^^^  ^^^^^^^^^^^
//                              Both on mapEntity

// This doesn't work because components are on DIFFERENT entities:
var mapEntity = _world.Create(tileMap, tileCollider);
foreach (var animTile in animatedTiles)
{
    _world.Create(animTile);  // Different entity per animTile
}
```

## Not Related To

- ❌ System registration (TileAnimationSystem IS registered correctly)
- ❌ System priority (850 is correct, between Animation:800 and MapRender:900)
- ❌ Data loading (AnimatedTiles ARE loaded successfully from JSON)
- ❌ Animation logic (frame update code IS correct)
- ❌ System initialization (Initialize() IS called correctly)
- ❌ System enabled state (system IS enabled by default)

## The Fix

**Recommended**: Store AnimatedTile data in TileMap component as an array.

```csharp
// Modify TileMap.cs
public struct TileMap
{
    // ... existing fields ...
    public AnimatedTile[] AnimatedTiles { get; set; }
}

// Modify PokeSharpGame.cs
var tileMap = _mapLoader.LoadMap("Assets/Maps/test-map.json");
var tileCollider = _mapLoader.LoadCollision("Assets/Maps/test-map.json");
var animatedTiles = _mapLoader.LoadAnimatedTiles("Assets/Maps/test-map.json");

// Store AnimatedTiles IN the TileMap
tileMap.AnimatedTiles = animatedTiles;

// Create ONE entity with ALL map data
var mapEntity = _world.Create(tileMap, tileCollider);

// Modify TileAnimationSystem.cs
_tileMapQuery = new QueryDescription().WithAll<TileMap>();

world.Query(in _tileMapQuery, (ref TileMap tileMap) =>
{
    if (tileMap.AnimatedTiles == null) return;

    foreach (ref var animTile in tileMap.AnimatedTiles.AsSpan())
    {
        UpdateTileAnimation(ref tileMap, ref animTile, deltaTime);
    }
});
```

## Alternative Approaches

### Option 2: Separate Queries
Query AnimatedTile entities separately and find TileMap entity each frame.
- **Pro**: No data structure changes
- **Con**: Performance overhead, more complex logic

### Option 3: Entity References
Store TileMap entity reference in each AnimatedTile.
- **Pro**: Flexible for multiple TileMaps
- **Con**: More complex, need to manage entity lifetimes

## Confidence Level

**100% Certain** - This is definitively the root cause.

The evidence is conclusive:
1. Components are on different entities (verified in code)
2. Query requires same entity (Arch ECS behavior)
3. Query never matches (mathematical certainty: ∅ ∩ {AnimatedTile} = ∅)
4. Similar systems work because their components ARE co-located

## Next Steps

1. **Implement** Option 1 (store AnimatedTiles in TileMap)
2. **Test** by running the game and observing water/grass animations
3. **Verify** console output shows animation updates
4. **Confirm** tile IDs change in TileMap layers over time

---

**Diagnosis Complete**: 2025-11-01
**Confidence**: 100%
**Status**: Ready for implementation
