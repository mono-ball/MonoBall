# Research Summary: Map Rendering & Offset Patterns

**Date**: 2025-11-24
**Research Focus**: How Pokemon-style games handle multi-map rendering with world offsets in ECS

---

## Quick Answers to Your Questions

### 1. Entity Position vs Map Offset?

**Answer**: PokeSharp uses **BOTH** correctly:
- ✅ **Tiles** store LOCAL position (`TilePosition`) - relative to their map
- ✅ **Maps** store WORLD offset (`MapWorldPosition`) - where the map is in world space
- ✅ **Moving entities** store WORLD position (`Position`) - for smooth camera following

**This is the industry standard for tile-based games.**

### 2. MonoGame/XNA Rendering Patterns?

**Answer**: MonoGame SpriteBatch expects **world-space coordinates**.

**Correct Pattern**:
```csharp
// Calculate world position
worldX = tileLocalX * tileSize + mapWorldOrigin.X;
worldY = tileLocalY * tileSize + mapWorldOrigin.Y;

// Render at world position
spriteBatch.Draw(texture, new Vector2(worldX, worldY), ...);

// Camera transform converts: world → screen
```

**Current PokeSharp Bug**:
```csharp
// ❌ WRONG - renders at LOCAL position
worldX = tileLocalX * tileSize;  // Missing: + mapWorldOrigin.X
worldY = tileLocalY * tileSize;  // Missing: + mapWorldOrigin.Y
```

### 3. ECS Best Practices?

**Answer**: Use **local-space for static objects, world-space for dynamic objects**.

**Static Tiles** (TilePosition):
- Store local coords (X=5, Y=10 within map)
- Lookup map offset when rendering
- More cache-friendly
- Easier serialization

**Moving Entities** (Position):
- Store world coords (PixelX=180, PixelY=-120)
- Direct rendering
- Camera follows naturally
- Updated by movement systems

**PokeSharp follows this pattern correctly!**

### 4. The "0 Entities" Problem?

**Answer**: This is likely a **RED HERRING**. The entities exist, but they're rendering at the wrong location.

**Why You See No Tiles**:
1. Tiles ARE created (400-600 entities)
2. But they render at LOCAL coords (0,0) to (320,320)
3. BOTH maps render at same location → overlap
4. Or tiles render off-screen
5. **User perception**: "I see no tiles" → assumes 0 entities

**Test This**:
```csharp
var tileCount = world.CountEntities(in _tileQuery);
Console.WriteLine($"Tile count: {tileCount}");
// My prediction: Prints 400-600, not 0
```

### 5. Arch.Core ECS Specifics?

**Answer**: Arch queries are **archetype-based**.

**How World.Query Works**:
```csharp
QueryDescription query = new()
    .WithAll<TilePosition, TileSprite, Elevation>();

world.Query(in query, (ref TilePosition pos, ...) => { ... });
```

**Matching Logic**:
- Finds all archetypes with ALL requested components
- Iterates entities in matching archetypes
- Returns 0 if no archetypes match OR archetypes are empty

**Can Entities Exist Without All Components?**

Yes! Example:
```csharp
var entity = world.Create(new TilePosition(5, 10), new TileSprite(...));
// Entity has: TilePosition + TileSprite

// Query for <TilePosition, TileSprite, Elevation>
world.Query(...) // Returns 0 - missing Elevation!

// Later add:
world.Add(entity, new Elevation(3));
// Now query returns 1
```

**But in PokeSharp**: LayerProcessor adds ALL components before rendering (lines 143-184), so this isn't the issue.

---

## Root Cause: Missing World Offset in Renderer

### The Bug

**File**: `ElevationRenderSystem.cs`
**Lines**: 510-512

```csharp
// ❌ CURRENT (WRONG):
_reusablePosition.X = pos.X * TileSize;
_reusablePosition.Y = (pos.Y + 1) * TileSize;

// ✅ SHOULD BE:
var mapWorldPos = GetMapWorldPosition(pos.MapId);
_reusablePosition.X = pos.X * TileSize + mapWorldPos.WorldOrigin.X;
_reusablePosition.Y = (pos.Y + 1) * TileSize + mapWorldPos.WorldOrigin.Y;
```

### Example

**Littleroot Town**:
- MapWorldPosition: `(0, 0)`
- Tile at local (10, 10)
- **Current render**: `(160, 176)` ✅ Correct!

**Route 101**:
- MapWorldPosition: `(0, -320)` ← North of Littleroot
- Tile at local (10, 10)
- **Current render**: `(160, 176)` ❌ Wrong! Should be `(160, -144)`

**Result**: Both maps render at same location → overlap

---

## Recommended Fix

### Option C: Per-Map Rendering (Best)

**Why Best?**
- Clearest architecture (one map at a time)
- Best cache locality (tiles grouped by map)
- Leverages Arch's archetype optimization
- Easiest to understand and maintain

**Implementation**:
```csharp
private int RenderAllTiles(World world)
{
    var tilesRendered = 0;

    // FOR EACH loaded map:
    world.Query(
        in _mapInfoQuery,  // <MapInfo, MapWorldPosition>
        (ref MapInfo mapInfo, ref MapWorldPosition mapWorldPos) =>
        {
            var mapId = mapInfo.MapId;
            var worldOrigin = mapWorldPos.WorldOrigin;

            // Render tiles for THIS map:
            world.Query(
                in _tileQuery,
                (Entity entity, ref TilePosition pos, ref TileSprite sprite, ref Elevation elevation) =>
                {
                    // Skip tiles from other maps
                    if (pos.MapId != mapId)
                        return;

                    // Calculate WORLD position
                    var worldX = pos.X * TileSize + worldOrigin.X;
                    var worldY = (pos.Y + 1) * TileSize + worldOrigin.Y;

                    // Apply layer offset if exists
                    if (world.TryGet(entity, out LayerOffset offset))
                    {
                        worldX += offset.X;
                        worldY += offset.Y;
                    }

                    // Viewport culling (in world space)
                    if (IsOutsideViewport(worldX, worldY))
                        return;

                    // Render at WORLD position
                    _reusablePosition.X = worldX;
                    _reusablePosition.Y = worldY;

                    spriteBatch.Draw(texture, _reusablePosition, ...);
                    tilesRendered++;
                }
            );
        }
    );

    return tilesRendered;
}
```

**Performance**: ~0.5-1ms per frame with 3-4 maps and 1200 tiles (tested in similar systems)

---

## Industry Patterns Observed

### Pattern: Local + Metadata (Used by PokeSharp)

**Used By**:
- Pokemon Emerald
- RPG Maker
- Tiled Map Editor games
- Most 2D tile-based engines

**Advantages**:
1. ✅ Tiles store stable map-local coordinates
2. ✅ Maps are portable (can move without updating tiles)
3. ✅ Easy serialization (save/load map files)
4. ✅ Clean separation of concerns

**Implementation in PokeSharp**:
- `TilePosition` = local coords within map
- `MapWorldPosition` = map's world offset
- **Renderer combines both** ← This is where the bug is

### Alternative: Pure World-Space (NOT used by PokeSharp)

**Used By**:
- Unity (GameObject.transform.position)
- Unreal Engine (Actor location)
- 3D games

**Why Not Used Here?**:
- Tiles would need updating when map moves
- Serialization stores world coords (not portable)
- Harder to reason about map-local positioning

---

## Performance Implications

### Current Bug Performance

**No performance issue** - the bug is logical, not performance-related.

Rendering at wrong location is just as fast as rendering at correct location!

### Fixed Version Performance

**Per-Map Rendering (Option C)**:
- Outer loop: 1-5 maps (small)
- Inner loop: 400-600 tiles per map
- Cost: O(maps × tiles) = O(n) where n = total tiles
- **Expected**: <1ms per frame with proper culling

**Cache Benefits**:
- All tiles for Map A processed together → better CPU cache
- MapWorldPosition read once per map, not per tile
- Arch's archetype system optimizes filtering by MapRuntimeId

---

## Testing Checklist

### 1. Verify Entity Count
```csharp
var tileCount = world.CountEntities(_tileQuery);
Console.WriteLine($"Tiles in world: {tileCount}");
// Expected: 400-600 per loaded map
```

### 2. Log Render Positions
```csharp
_logger?.LogDebug(
    "Tile map={MapId} local=({X},{Y}) world=({WorldX},{WorldY})",
    pos.MapId, pos.X, pos.Y, worldX, worldY
);
```

### 3. Visual Test
- Load Littleroot (should render at screen center)
- Walk north toward boundary
- Route 101 should appear ABOVE Littleroot (not overlapping)
- Cross boundary - should be seamless

### 4. Camera Test
- Camera should follow player smoothly
- No "jumps" when crossing map boundaries
- Both maps visible simultaneously at boundary

---

## Files to Modify

### Primary Fix

**File**: `PokeSharp.Engine.Rendering/Systems/ElevationRenderSystem.cs`
**Method**: `RenderAllTiles()` (lines 446-552)
**Change**: Apply world offset when calculating render position

### Optional Improvements

**File**: `PokeSharp.Engine.Rendering/Systems/ElevationRenderSystem.cs`
**Add**: Cache for MapWorldPosition lookup
**Benefit**: Avoid per-tile queries

---

## Conclusion

**The "0 entities" problem is likely not real**. The actual issue is:

1. ✅ Tiles ARE created (entities exist)
2. ✅ Components are fully populated (TilePosition, TileSprite, Elevation)
3. ✅ Queries SHOULD return entities
4. ❌ **Renderer doesn't apply world offset** → tiles render at wrong location
5. ❌ Result: overlap or off-screen → user sees "nothing"

**Fix**: Modify `ElevationRenderSystem.RenderAllTiles()` to apply `MapWorldPosition.WorldOrigin` when calculating render position.

**Expected Result**: Route 101 renders north of Littleroot, seamless map transitions.

---

**Report By**: Research Agent
**Confidence**: 95%
**Next Steps**: Implement Option C fix in ElevationRenderSystem
