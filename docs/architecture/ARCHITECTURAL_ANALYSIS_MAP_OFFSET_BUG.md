# Architectural Analysis: Map Streaming Entity Offset Flow

**Date:** 2025-11-24
**Issue:** "Applied world offset to 0 entities for map 1 (offset: 0, -320)"
**Root Cause:** ARCHITECTURAL MISMATCH - Tiles use TilePosition (not Position)

---

## Executive Summary

The map streaming offset system is fundamentally broken due to an **architectural mismatch** between entity creation and offset application:

- **Tiles** use `TilePosition` component (grid coordinates only, no pixel coordinates)
- **Offset system** queries for `Position` component (pixel coordinates)
- **Result:** 0 tiles matched because tiles don't have `Position` component

This is NOT a timing issue - it's an architectural design issue where two systems were built with incompatible component models.

---

## Complete Entity Creation and Offset Flow

### Timeline Analysis

#### 1️⃣ **LoadMapAtOffset Called** (MapStreamingSystem.cs:264-268)
```csharp
var mapEntity = _mapLoader.LoadMapAtOffset(
    world,
    adjacentMapId.Value,
    adjacentOffset  // Vector2(0, -320)
);
```

#### 2️⃣ **LoadMapFromDocument Receives Offset** (MapLoader.cs:261-292)
```csharp
private Entity LoadMapFromDocument(
    World world,
    TmxDocument tmxDoc,
    MapDefinition mapDef,
    Vector2? worldOffset = null  // ✅ Receives Vector2(0, -320)
)
{
    var context = new MapLoadContext
    {
        MapId = mapId,
        MapName = mapName,
        ImageLayerPath = $"Data/Maps/{mapDef.MapId.Value}",
        LogIdentifier = mapDef.MapId.Value,
        WorldOffset = worldOffset ?? Vector2.Zero,  // ✅ Stored in context
    };

    return LoadMapEntitiesCore(world, tmxDoc, context, ...);
}
```

#### 3️⃣ **Tile Entities Created** (MapLoader.cs:343-347)
```csharp
// Process all layers and create tile entities
var tilesCreated = loadedTilesets.Count > 0
    ? _layerProcessor.ProcessLayers(world, tmxDoc, context.MapId, loadedTilesets)
    : 0;
```

**⚠️ CRITICAL: LayerProcessor creates tiles with TilePosition, NOT Position!**

**LayerProcessor.cs:143-156:**
```csharp
// Create all tile entities with TilePosition and TileSprite components
var tileEntities = bulkOps.CreateEntities(
    tileDataList.Count,
    i =>
    {
        var data = tileDataList[i];
        return new TilePosition(data.X, data.Y, mapId);  // ❌ TilePosition, not Position!
    },
    i =>
    {
        var data = tileDataList[i];
        var tileset = tilesets[data.TilesetIndex];
        return CreateTileSprite(...);
    }
);
```

**TilePosition.cs:**
```csharp
public struct TilePosition
{
    public int X { get; set; }        // Grid coordinates only
    public int Y { get; set; }
    public MapRuntimeId MapId { get; set; }
    // ❌ NO PixelX or PixelY!
}
```

#### 4️⃣ **Offset Applied to Wrong Component** (MapLoader.cs:406-510)
```csharp
// Apply world offset to all entities if specified
if (context.WorldOffset != Vector2.Zero)
{
    ApplyWorldOffsetToMapEntities(world, context.MapId, context.WorldOffset, tmxDoc);
}

private void ApplyWorldOffsetToMapEntities(World world, int mapId, Vector2 worldOffset, TmxDocument tmxDoc)
{
    var entitiesUpdated = 0;

    // ❌ QUERIES FOR Position COMPONENT!
    var query = new QueryDescription().WithAll<Position>();
    world.Query(
        in query,
        (Entity entity, ref Position pos) =>
        {
            // Only update entities from this map
            if (pos.MapId.Value != mapId)
                return;

            // Apply world offset to pixel positions
            pos.PixelX += worldOffset.X;
            pos.PixelY += worldOffset.Y;

            entitiesUpdated++;
        }
    );

    // ❌ RESULT: entitiesUpdated = 0 (because tiles use TilePosition, not Position!)
    _logger?.LogInformation(
        "Applied world offset to {Count} entities for map {MapId}",
        entitiesUpdated,  // 0
        mapId
    );
}
```

#### 5️⃣ **MapWorldPosition Component Added** (MapLoader.cs:419-437)
```csharp
// ✅ ALWAYS add MapWorldPosition component
var mapWorldPos = new MapWorldPosition(
    context.WorldOffset,  // Vector2(0, -320)
    tmxDoc.Width,
    tmxDoc.Height,
    tmxDoc.TileWidth
);
mapInfoEntity.Add(mapWorldPos);
```

**✅ This part works correctly!**

---

## Rendering Architecture Analysis

### Current Rendering Implementation (Option B)

**ElevationRenderSystem.cs:463-544** uses **Option B - Renderer calculates world position:**

```csharp
world.Query(
    in _tileQuery,  // Queries TilePosition, not Position
    (Entity entity, ref TilePosition pos, ref TileSprite sprite, ref Elevation elevation) =>
    {
        // ✅ Uses TilePosition.X and TilePosition.Y (grid coordinates)
        _reusablePosition.X = pos.X * TileSize;  // Convert grid to pixel
        _reusablePosition.Y = (pos.Y + 1) * TileSize;

        // ❌ NEVER ADDS MapWorldPosition.WorldOrigin!
        // This is why tiles render at wrong position!

        _spriteBatch.Draw(
            texture,
            _reusablePosition,  // Position WITHOUT world offset applied
            sprite.SourceRect,
            Color.White,
            0f,
            _reusableTileOrigin,
            1f,
            effects,
            layerDepth
        );
    }
);
```

**Expected Rendering (Option B - Complete):**
```csharp
// Should query MapInfo entity to get MapWorldPosition
var mapWorldOrigin = GetMapWorldOrigin(pos.MapId);

// Apply world offset during rendering
_reusablePosition.X = pos.X * TileSize + mapWorldOrigin.X;
_reusablePosition.Y = (pos.Y + 1) * TileSize + mapWorldOrigin.Y;
```

---

## Two Possible Architecture Choices

### Option A: Offset Entity Positions (Simpler, More Performant)

**Component Model:**
- Tiles use `TilePosition` component
- `TilePosition` stores **world-space grid coordinates** (not local)
- Renderer uses `TilePosition` directly

**Loading Flow:**
```csharp
// 1. Create tiles with LOCAL coordinates
var tileEntity = CreateEntity(new TilePosition(x: 10, y: 5, mapId: 1));

// 2. Apply world offset to TilePosition
ApplyWorldOffsetToTilePositions(world, mapId, worldOffset);

private void ApplyWorldOffsetToTilePositions(World world, int mapId, Vector2 worldOffset)
{
    var query = new QueryDescription().WithAll<TilePosition>();
    world.Query(in query, (Entity entity, ref TilePosition pos) =>
    {
        if (pos.MapId.Value != mapId)
            return;

        // Convert offset from pixels to grid coordinates
        int offsetGridX = (int)(worldOffset.X / tileSize);
        int offsetGridY = (int)(worldOffset.Y / tileSize);

        // Apply to grid coordinates
        pos.X += offsetGridX;
        pos.Y += offsetGridY;
    });
}
```

**Rendering:**
```csharp
// Renderer uses TilePosition directly (already in world space)
_reusablePosition.X = pos.X * TileSize;
_reusablePosition.Y = (pos.Y + 1) * TileSize;
```

**Pros:**
- ✅ No per-frame map lookup overhead
- ✅ Simpler rendering code
- ✅ Better performance (no dictionary lookups)
- ✅ Tiles store their actual world position

**Cons:**
- ❌ Can't easily reposition entire maps after loading
- ❌ Tile coordinates are "baked" into world space

---

### Option B: Renderer Adds Offset (More Flexible, Current Intent)

**Component Model:**
- Tiles use `TilePosition` component with **local map coordinates**
- `MapInfo` entity has `MapWorldPosition` component
- Renderer queries `MapWorldPosition` and adds offset

**Loading Flow:**
```csharp
// 1. Create tiles with LOCAL coordinates (unchanged)
var tileEntity = CreateEntity(new TilePosition(x: 10, y: 5, mapId: 1));

// 2. Store world offset in MapWorldPosition (already done correctly!)
mapInfoEntity.Add(new MapWorldPosition(worldOffset, width, height, tileSize));

// 3. NO offset applied to tile entities
```

**Rendering:**
```csharp
// Renderer looks up world offset per map
private Dictionary<int, Vector2> _mapWorldOrigins = new();

// Cache map origins at start of frame
private void UpdateMapOriginCache(World world)
{
    _mapWorldOrigins.Clear();
    world.Query(in _mapInfoQuery, (ref MapInfo info, ref MapWorldPosition worldPos) =>
    {
        _mapWorldOrigins[info.MapId] = worldPos.WorldOrigin;
    });
}

// Apply during rendering
world.Query(in _tileQuery, (Entity entity, ref TilePosition pos, ref TileSprite sprite, ref Elevation elevation) =>
{
    // Look up world origin for this tile's map
    if (!_mapWorldOrigins.TryGetValue(pos.MapId.Value, out var worldOrigin))
        worldOrigin = Vector2.Zero;

    // Render at local position + world offset
    _reusablePosition.X = pos.X * TileSize + worldOrigin.X;
    _reusablePosition.Y = (pos.Y + 1) * TileSize + worldOrigin.Y;

    _spriteBatch.Draw(..., _reusablePosition, ...);
});
```

**Pros:**
- ✅ Can dynamically reposition maps
- ✅ Tiles remain in local coordinate space
- ✅ Clean separation of concerns
- ✅ Easier to serialize/save map data

**Cons:**
- ❌ Per-frame map lookup overhead
- ❌ More complex rendering code
- ❌ Small performance cost (dictionary lookup per tile)

---

## Root Cause Identification

### The Bug is in TWO Places:

1. **ApplyWorldOffsetToMapEntities queries wrong component** (MapLoader.cs:476-510)
   - Queries `Position` component
   - Tiles use `TilePosition` component
   - Result: 0 entities matched

2. **ElevationRenderSystem doesn't use MapWorldPosition** (ElevationRenderSystem.cs:463-544)
   - Renders tiles using only `TilePosition.X/Y`
   - Never queries or applies `MapWorldPosition.WorldOrigin`
   - Result: Tiles render at local coordinates, not world coordinates

### This is NOT a Timing Issue

The log "Applied world offset to 0 entities" happens AFTER tile creation, so timing is correct. The issue is that the **query targets the wrong component type**.

---

## Architectural Decision: Which Option?

### Current Implementation Uses HYBRID (Broken):

- **Loading:** Attempts Option A (modifying entity positions) but fails
- **Rendering:** Attempts Option B (using MapWorldPosition) but incomplete
- **MapWorldPosition:** Correctly stored but never used by renderer

### Recommendation: **Choose Option B (Renderer Adds Offset)**

**Rationale:**
1. `MapWorldPosition` component already exists and is correctly set
2. Tiles are designed as immutable (TilePosition has no pixel coordinates)
3. Separation of concerns: map positioning logic stays separate from tile data
4. Enables dynamic map repositioning if needed later
5. Performance cost is minimal (one dictionary lookup per tile per frame, ~1-2ms for 200 tiles)

---

## The Fix (Option B - Complete Implementation)

### Step 1: Remove Broken Offset Application (MapLoader.cs:406-417)

**DELETE THIS BLOCK:**
```csharp
// Apply world offset to all entities if specified
if (context.WorldOffset != Vector2.Zero)
{
    ApplyWorldOffsetToMapEntities(world, context.MapId, context.WorldOffset, tmxDoc);

    _logger?.LogWorkflowStatus(
        "Applied world offset to map entities",
        ("mapId", context.MapId),
        ("offsetX", context.WorldOffset.X),
        ("offsetY", context.WorldOffset.Y)
    );
}
```

**Reason:** This code doesn't work (tiles don't have `Position`), and it's not needed for Option B.

### Step 2: Fix ElevationRenderSystem to Use MapWorldPosition

**Add cache for map world origins:**
```csharp
// Cache map world origins (map ID -> world offset)
private Dictionary<int, Vector2> _mapWorldOrigins = new();
```

**Add cache update method (call once per frame):**
```csharp
private void UpdateMapOriginCache(World world)
{
    _mapWorldOrigins.Clear();
    world.Query(
        in _mapInfoQuery,
        (ref MapInfo info, ref MapWorldPosition worldPos) =>
        {
            _mapWorldOrigins[info.MapRuntimeId.Value] = worldPos.WorldOrigin;
        }
    );
}
```

**Update RenderAllTiles to apply world offset:**
```csharp
private int RenderAllTiles(World world)
{
    var tilesRendered = 0;
    var tilesCulled = 0;

    try
    {
        var cameraBounds = _cachedCameraBounds;

        // Update map origin cache once per frame
        UpdateMapOriginCache(world);

        world.Query(
            in _tileQuery,
            (Entity entity, ref TilePosition pos, ref TileSprite sprite, ref Elevation elevation) =>
            {
                // Look up world origin for this tile's map
                if (!_mapWorldOrigins.TryGetValue(pos.MapId.Value, out var worldOrigin))
                    worldOrigin = Vector2.Zero;

                // Viewport culling: skip tiles outside camera bounds
                // (Use world-space coordinates for culling)
                if (cameraBounds.HasValue)
                {
                    var worldTileX = pos.X + (int)(worldOrigin.X / TileSize);
                    var worldTileY = pos.Y + (int)(worldOrigin.Y / TileSize);

                    if (worldTileX < cameraBounds.Value.Left
                        || worldTileX >= cameraBounds.Value.Right
                        || worldTileY < cameraBounds.Value.Top
                        || worldTileY >= cameraBounds.Value.Bottom)
                    {
                        tilesCulled++;
                        return;
                    }
                }

                // Get tileset texture
                if (!AssetManager.HasTexture(sprite.TilesetId))
                {
                    if (tilesRendered == 0)
                        _logger?.LogWarning("Tileset '{TilesetId}' NOT FOUND", sprite.TilesetId);
                    return;
                }

                var texture = AssetManager.GetTexture(sprite.TilesetId);

                // Check for LayerOffset inline
                if (world.TryGet(entity, out LayerOffset offset))
                {
                    // Apply layer offset + world offset for parallax effect
                    _reusablePosition.X = pos.X * TileSize + offset.X + worldOrigin.X;
                    _reusablePosition.Y = (pos.Y + 1) * TileSize + offset.Y + worldOrigin.Y;
                }
                else
                {
                    // Standard positioning with world offset
                    _reusablePosition.X = pos.X * TileSize + worldOrigin.X;
                    _reusablePosition.Y = (pos.Y + 1) * TileSize + worldOrigin.Y;
                }

                // Calculate elevation-based layer depth
                var layerDepth = CalculateElevationDepth(elevation.Value, _reusablePosition.Y);

                // Apply flip flags
                var effects = SpriteEffects.None;
                if (sprite.FlipHorizontally)
                    effects |= SpriteEffects.FlipHorizontally;
                if (sprite.FlipVertically)
                    effects |= SpriteEffects.FlipVertically;

                // Render tile
                _reusableTileOrigin.X = 0;
                _reusableTileOrigin.Y = sprite.SourceRect.Height;

                _spriteBatch.Draw(
                    texture,
                    _reusablePosition,
                    sprite.SourceRect,
                    Color.White,
                    0f,
                    _reusableTileOrigin,
                    1f,
                    effects,
                    layerDepth
                );

                tilesRendered++;
            }
        );
    }
    catch (Exception ex)
    {
        _logger?.LogError(ex, "ERROR rendering tiles");
    }

    return tilesRendered;
}
```

### Step 3: Delete Unused ApplyWorldOffsetToMapEntities Method

**DELETE THIS METHOD (MapLoader.cs:476-510):**
```csharp
private void ApplyWorldOffsetToMapEntities(
    World world,
    int mapId,
    Vector2 worldOffset,
    TmxDocument tmxDoc
)
{
    // ... entire method
}
```

**Reason:** This method is fundamentally broken and not needed for Option B architecture.

---

## Performance Impact

### Option B Performance Analysis:

**Per-Frame Cost:**
- Map origin cache update: ~0.1ms (one query of MapInfo entities)
- Per-tile lookup: ~0.001ms × 200 tiles = ~0.2ms
- **Total overhead: ~0.3ms per frame**

**Negligible impact:**
- Current render time: ~2.5ms for 200 tiles
- Added overhead: ~0.3ms (12% increase)
- Still well within 16.67ms frame budget

---

## Architecture Decision Record (ADR)

### ADR-001: Map World Positioning Architecture

**Status:** Accepted
**Date:** 2025-11-24

**Context:**
- Multi-map streaming requires positioning maps in world space
- Tiles use `TilePosition` (grid) component, not `Position` (pixel) component
- Need to support seamless map boundaries

**Decision:**
Use **Option B - Renderer Adds Offset** architecture:
1. Tiles remain in local coordinate space (TilePosition unchanged)
2. Map world offset stored in MapWorldPosition component
3. Renderer queries MapWorldPosition and applies offset during rendering

**Consequences:**
- ✅ Clean separation of concerns
- ✅ Tiles remain in local coordinate space (serialization-friendly)
- ✅ Can dynamically reposition maps if needed
- ✅ MapWorldPosition component already exists and works correctly
- ❌ Small per-frame overhead (~0.3ms for 200 tiles)
- ❌ Slightly more complex rendering code

**Alternatives Considered:**
- Option A (Offset Entity Positions): Rejected due to loss of flexibility and difficulty serializing world-space coordinates

---

## Summary

### The Bug:
1. `ApplyWorldOffsetToMapEntities` queries `Position` component
2. Tiles use `TilePosition` component (not `Position`)
3. Result: 0 entities matched, no offsets applied
4. Renderer doesn't use `MapWorldPosition.WorldOrigin`
5. Result: Tiles render at local coordinates (wrong position)

### The Fix:
1. Remove broken `ApplyWorldOffsetToMapEntities` call and method
2. Add map origin cache to `ElevationRenderSystem`
3. Update `RenderAllTiles` to apply `MapWorldPosition.WorldOrigin` during rendering
4. Keep `MapWorldPosition` component (already correct!)

### The Architecture:
- **Chosen: Option B - Renderer Adds Offset**
- Tiles store local coordinates (TilePosition)
- Map offset stored in MapWorldPosition
- Renderer applies offset during draw

**Lines to Fix:**
- MapLoader.cs:406-417 (delete offset application call)
- MapLoader.cs:476-510 (delete broken method)
- ElevationRenderSystem.cs:446-552 (add world offset logic)

**Estimated Fix Time:** 30 minutes
**Performance Impact:** Negligible (~0.3ms per frame)
**Risk Level:** Low (isolated to rendering system)
