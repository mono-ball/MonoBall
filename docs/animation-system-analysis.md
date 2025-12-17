# Tile Animation System Analysis

## Investigation Objective
Analyze why tile animations stop working on certain maps in the PokeSharp game engine.

## System Architecture Overview

### 1. TileAnimationSystem.cs
**Purpose**: Updates animated tile frames based on a global timer (Pokemon-accurate synchronization).

**Key Features**:
- Priority: 850 (after Animation:800, before Render:1000)
- Uses global animation timer shared by all tiles
- Sequential query optimization (faster for 100-200 tiles than parallel)
- Zero runtime overhead via precalculated source rectangles

**Update Flow**:
```
Update(world, deltaTime)
├─ Increment _globalAnimationTimer += deltaTime
├─ Query all entities with AnimatedTile + TileSprite components
│  └─ For each tile: UpdateTileAnimation(ref animTile, ref sprite, globalTimer)
│     ├─ Validate: FrameTileIds, FrameDurations, FrameSourceRects must all exist
│     ├─ Calculate: timeInCycle = globalTimer % totalCycleDuration
│     ├─ Find: current frame index based on accumulated frame durations
│     └─ Update: sprite.SourceRect = animTile.FrameSourceRects[frameIndex]
└─ Log animated tile count on first update (with warnings for missing precalc)
```

**Critical Validation**:
```csharp
// Lines 121-131
if (animTile.FrameTileIds == null || animTile.FrameTileIds.Length == 0
    || animTile.FrameDurations == null || animTile.FrameDurations.Length == 0
    || animTile.FrameSourceRects == null || animTile.FrameSourceRects.Length == 0)
{
    return; // ANIMATION STOPS HERE IF ANY ARRAY IS NULL/EMPTY
}
```

### 2. AnimatedTile Component
**Purpose**: Stores animation data for a single tile.

**Critical Fields**:
- `FrameTileIds[]` - Array of tile IDs in animation sequence
- `FrameDurations[]` - Duration of each frame in seconds
- `FrameSourceRects[]` - **CRITICAL**: Precalculated source rectangles (performance optimization)
- `CurrentFrameIndex` - Current frame being displayed
- `TilesetFirstGid` - First global ID of owning tileset
- `TilesPerRow` - Layout info for source rect calculation

**Constructor Validation**:
```csharp
// Lines 101-112
ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tileWidth);
ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tileHeight);
ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tilesPerRow);
ArgumentNullException.ThrowIfNull(frameSourceRects);

if (frameSourceRects.Length != frameTileIds.Length)
{
    throw new ArgumentException(
        "FrameSourceRects length must match FrameTileIds length",
        nameof(frameSourceRects)
    );
}
```

### 3. AnimatedTileProcessor.cs
**Purpose**: Creates AnimatedTile components for tiles during map loading.

**Processing Flow**:
```
CreateAnimatedTileEntities(world, tmxDoc, mapInfoEntity, tilesets, mapId)
├─ For each tileset in tilesets
│  └─ CreateAnimatedTileEntitiesForTileset(world, tileset, mapId)
│     ├─ Build animationsByTileId dictionary
│     │  └─ For each animation in tileset.Animations
│     │     ├─ Convert local tile IDs to global IDs
│     │     ├─ **CRITICAL**: Precalculate source rectangles
│     │     │  frameSourceRects = globalFrameIds
│     │     │      .Select(frameGid => TilesetUtilities.CalculateSourceRect(frameGid, tileset))
│     │     │      .ToArray();
│     │     └─ Create AnimatedTile component with precalculated rects
│     └─ Execute SINGLE batch query to add components
│        └─ Query<TilePosition, TileSprite>
│           ├─ Filter: pos.MapId == mapId (prevents cross-map corruption)
│           └─ For matching tiles: world.Add(entity, animatedTile)
└─ Return count of animated tiles created
```

**Performance Optimization** (Lines 100-149):
- Pre-builds animation dictionary BEFORE querying entities
- Single batch query instead of N individual queries per animation
- Reduces complexity from O(animations × tiles) to O(tiles + animations)

**Critical Filter** (Lines 156-160):
```csharp
// CRITICAL: Only process tiles belonging to THIS map
if (pos.MapId == null || pos.MapId.Value != mapId.Value)
{
    return; // Skip tiles from other maps
}
```

### 4. MapLoader Integration
**When Animations Are Created** (MapLoader.cs, Line 839-848):
```csharp
int animatedTilesCreated =
    loadedTilesets.Count > 0
        ? _animatedTileProcessor.CreateAnimatedTileEntities(
            world,
            tmxDoc,
            mapInfoEntity,
            loadedTilesets,
            context.MapId
        )
        : 0;
```

**Requirements**:
1. `loadedTilesets.Count > 0` - Must have loaded tilesets
2. Tileset must contain animation definitions in `tileset.Animations` collection
3. Map tiles must have matching GIDs to animation base tile IDs

## Potential Failure Modes

### 1. No Tilesets Loaded
**Symptom**: No animations created at all.
**Cause**: `loadedTilesets.Count == 0`
**Detection**: Check MapLoader logs for tileset loading failures.

### 2. Missing Animation Definitions
**Symptom**: Tiles render but don't animate.
**Cause**: Tileset file missing animation data in `tileset.Animations` dictionary.
**Detection**: Check if Tiled editor shows animations for the tileset.

### 3. Invalid Tileset Metadata
**Symptom**: Exception during processing or no source rects calculated.
**Cause**:
- Negative/zero `tileWidth`, `tileHeight`, `tilesPerRow`
- Negative `spacing` or `margin`
- Invalid `firstGid` (<= 0)
**Detection**: AnimatedTileProcessor throws `InvalidOperationException` (Lines 79-98).

### 4. Source Rectangle Calculation Failure
**Symptom**: Animations created but stop updating immediately.
**Cause**: `TilesetUtilities.CalculateSourceRect()` returns invalid rectangles or throws exception.
**Risk Factors**:
- Invalid tile dimensions
- Missing/invalid tileset image dimensions
- Incorrect margin/spacing values
**Detection**: Check for exceptions in TilesetUtilities during map load.

### 5. MapId Mismatch
**Symptom**: Some tiles animate, others don't (inconsistent).
**Cause**: Tiles have wrong `MapId` in `TilePosition.MapId` component.
**Detection**: AnimatedTileProcessor skips tiles where `pos.MapId != context.MapId`.

### 6. Missing TileSprite Component
**Symptom**: AnimatedTile components added but not updated.
**Cause**: Tiles missing `TileSprite` component (query requires both).
**Detection**: Query `AnimatedTiles` won't match entities without TileSprite.

### 7. Old Map Data (Missing Precalculated Rects)
**Symptom**: Animations stop after first frame.
**Cause**: Map loaded from cache with null `FrameSourceRects` array.
**Detection**: TileAnimationSystem logs warning (Lines 95-101):
```
"PERFORMANCE CHECK: {PrecalcCount}/{TotalCount} tiles have precalculated rects.
{NullCount} tiles missing precalc (OLD MAP DATA - RELOAD REQUIRED!)"
```

### 8. Animation Data Corruption
**Symptom**: Animations flicker or show wrong frames.
**Cause**:
- `FrameTileIds.Length != FrameDurations.Length`
- `FrameSourceRects.Length != FrameTileIds.Length`
- Invalid frame durations (0 or negative)
**Detection**: TileAnimationSystem validation returns early (Line 130).

### 9. Tileset Not Part of LoadedTilesets
**Symptom**: Specific animated tiles missing animations.
**Cause**: Tile's tileset not in `loadedTilesets` collection passed to processor.
**Detection**: AnimatedTileProcessor only processes animations from provided tilesets.

### 10. GID Mismatch
**Symptom**: Animations not applied to correct tiles.
**Cause**:
- Tile `sprite.TileGid` doesn't match animation's global tile ID
- `firstGid` offset calculation incorrect
**Detection**: Dictionary lookup fails (Line 164):
```csharp
if (animationsByTileId.TryGetValue(sprite.TileGid, out AnimatedTile animatedTile))
```

## Diagnostic Checklist

When animations stop on a map:

1. **Check System Priority**:
   - Is TileAnimationSystem enabled?
   - Is it running at priority 850?
   - Are there conflicting systems?

2. **Verify Tileset Loading**:
   - Are tilesets loaded successfully?
   - Does `loadedTilesets.Count > 0`?
   - Are external tilesets resolved correctly?

3. **Inspect Animation Definitions**:
   - Does the Tiled map contain animations?
   - Are animations defined in the tileset JSON?
   - Do `tileset.Animations.Count > 0`?

4. **Check Tileset Metadata**:
   - Valid `tileWidth`, `tileHeight`, `tilesPerRow`?
   - Valid `spacing`, `margin`, `firstGid`?
   - Valid image dimensions?

5. **Verify Component Creation**:
   - How many AnimatedTile components created? (check logs)
   - Do tiles have TileSprite component?
   - Do tiles have correct MapId?

6. **Check Precalculated Data**:
   - Are FrameSourceRects populated?
   - Check warning: "tiles missing precalc"
   - May need to reload map from disk

7. **Validate Animation Data**:
   - Array lengths match (FrameTileIds, FrameDurations, FrameSourceRects)?
   - Frame durations > 0?
   - Total cycle duration > 0?

8. **Test GID Matching**:
   - Do tile GIDs match animation base IDs?
   - Is firstGid offset correct?
   - Are local IDs converted to global IDs?

9. **Check Query Execution**:
   - Is `EcsQueries.AnimatedTiles` defined correctly?
   - Does it use `QueryCache.Get<TilePosition, TileSprite, AnimatedTile>()`?

10. **Look for Exceptions**:
    - Any exceptions during AnimatedTileProcessor.CreateAnimatedTileEntities?
    - Any exceptions in TilesetUtilities.CalculateSourceRect?
    - Any validation failures in AnimatedTile constructor?

## Log Analysis Points

### Success Indicators:
```
"MapInfo: {animatedTilesCreated} animated tiles created"
"Animated tile count: {count}"
"PERFORMANCE CHECK: {precalcCount}/{totalCount} tiles have precalculated rects. 0 tiles missing precalc"
```

### Warning Signs:
```
"PERFORMANCE CHECK: ... {nullCount} tiles missing precalc (OLD MAP DATA - RELOAD REQUIRED!)"
"Tileset '{name}' has invalid dimensions/spacing/margin"
"Map file not found: {path}"
```

### Critical Errors:
```
"InvalidOperationException: Tileset ... has negative spacing/margin"
"ArgumentException: FrameSourceRects length must match FrameTileIds length"
"FileNotFoundException: Map definition not found"
```

## Recommendations for Debugging

1. **Add Debug Logging**:
   - Log animation dictionary size in AnimatedTileProcessor
   - Log each tile GID attempted for animation matching
   - Log source rect calculation for first frame of each animation

2. **Verify Tiled Editor Data**:
   - Open problematic map in Tiled editor
   - Check animation properties on tiles
   - Verify tileset firstgid values

3. **Compare Working vs Broken Maps**:
   - Check tileset differences
   - Compare animation counts
   - Look for missing/different tilesets

4. **Test Map Reload**:
   - Clear TMX cache: `MapLoader.ClearTmxCache()`
   - Reload map from disk
   - Check if precalculated rects regenerate

5. **Monitor Query Results**:
   - Add counter in TileAnimationSystem.Update to track processed tiles
   - Compare to expected animated tile count
   - Check if count drops to zero over time

## System Dependencies

```
TileAnimationSystem
  ├─ Requires: EcsQueries.AnimatedTiles query
  ├─ Components: AnimatedTile, TileSprite, TilePosition
  └─ Priority: 850

AnimatedTileProcessor
  ├─ Called by: MapLoader.LoadMapEntitiesCore (line 841)
  ├─ Requires: LoadedTileset[], GameMapId
  ├─ Creates: AnimatedTile components
  └─ Dependencies: TilesetUtilities.CalculateSourceRect

AnimatedTile Component
  ├─ Required arrays: FrameTileIds, FrameDurations, FrameSourceRects
  ├─ Metadata: TilesetFirstGid, TilesPerRow, TileWidth, TileHeight
  └─ State: CurrentFrameIndex, FrameTimer

MapLoader
  ├─ Loads: Tilesets via TilesetLoader
  ├─ Processes: Layers via LayerProcessor
  ├─ Creates: AnimatedTile via AnimatedTileProcessor
  └─ Order: Tiles -> AnimatedTiles -> ImageLayers -> Objects
```

## Conclusion

Tile animations can stop for multiple reasons, but the most common failure points are:

1. **Tileset loading failures** - No tilesets loaded means no animations
2. **Missing animation definitions** - Tileset JSON lacks animation data
3. **Invalid tileset metadata** - Dimensions, spacing, or margin values invalid
4. **Source rectangle calculation errors** - TilesetUtilities fails to calculate rects
5. **Map ID mismatches** - Tiles filtered out due to wrong MapId
6. **Old cached data** - FrameSourceRects null from old map data

The system has robust validation and will fail safely (no animation) rather than crash. Check logs for warnings like "tiles missing precalc" or "InvalidOperationException" to identify the specific issue.
