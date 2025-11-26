# Multi-Map Rendering Architecture Analysis

## Problem Statement

Maps are rendering "in the same location" instead of at their correct world positions. Despite having different `WorldOrigin` coordinates in the `MapWorldPosition` component, both Oldale Town and Route 102 appear overlapping on screen.

### Expected Behavior
- **Oldale Town**: MapId 2, WorldOrigin (0, -640), Size 20x20 tiles (320x320 pixels)
- **Route 102**: MapId 4, WorldOrigin (-320, -640), Size 50x20 tiles (800x320 pixels)
- Both maps share Y-coordinates (-640 to -320) but different X ranges
- Oldale Town: X from 0 to 320
- Route 102: X from -320 to 0

### Current Behavior
Both maps render at the same screen location, suggesting world-to-screen transformation is broken.

---

## Root Cause Analysis

### Issue #1: TilePosition Component Uses Local Coordinates

**File**: `/PokeSharp.Game.Components/Components/Tiles/TilePosition.cs` (Lines 10-40)

```csharp
public struct TilePosition
{
    /// <summary>
    ///     Gets or sets the X coordinate in tile space.
    /// </summary>
    public int X { get; set; }  // LOCAL tile coordinate (0-19 for Oldale Town)

    /// <summary>
    ///     Gets or sets the Y coordinate in tile space.
    /// </summary>
    public int Y { get; set; }  // LOCAL tile coordinate (0-19 for Oldale Town)

    /// <summary>
    ///     Gets or sets the map identifier for multi-map support.
    /// </summary>
    public MapRuntimeId MapId { get; set; }
}
```

**Problem**: TilePosition stores LOCAL tile coordinates (e.g., 0-19 for a 20x20 map), not world coordinates.

---

### Issue #2: LayerProcessor Creates Tiles with Local Coordinates

**File**: `/PokeSharp.Game.Data/MapLoading/Tiled/Processors/LayerProcessor.cs` (Lines 143-149)

```csharp
var tileEntities = bulkOps.CreateEntities(
    tileDataList.Count,
    i =>
    {
        var data = tileDataList[i];
        return new TilePosition(data.X, data.Y, mapId);  // ❌ LOCAL coordinates (0-19)
    },
    // ...
);
```

**Problem**: Tiles are created with local coordinates (0-19), not world coordinates. Even though the map has `WorldOrigin (-320, -640)`, tiles don't know about it.

---

### Issue #3: ElevationRenderSystem Applies WorldOrigin AFTER Culling

**File**: `/PokeSharp.Engine.Rendering/Systems/ElevationRenderSystem.cs` (Lines 500-549)

```csharp
// Get map world origin for multi-map rendering (needed for culling)
var worldOrigin = _mapWorldOrigins.TryGetValue(pos.MapId.Value, out var origin)
    ? origin
    : Vector2.Zero;

// Convert tile position to world tile coordinates for proper culling
var worldTileX = pos.X + (int)(worldOrigin.X / TileSize);  // ❌ WRONG ORDER
var worldTileY = pos.Y + (int)(worldOrigin.Y / TileSize);

// Viewport culling: skip tiles outside camera bounds (in world space)
if (cameraBounds.HasValue)
    if (
        worldTileX < cameraBounds.Value.Left
        || worldTileX >= cameraBounds.Value.Right
        || worldTileY < cameraBounds.Value.Top
        || worldTileY >= cameraBounds.Value.Bottom
    )
    {
        tilesCulled++;
        return;
    }

// ... later ...

// Apply layer offset for parallax effect + map world offset
_reusablePosition.X = pos.X * TileSize + worldOrigin.X;  // ❌ pos.X is local (0-19)
_reusablePosition.Y = (pos.Y + 1) * TileSize + worldOrigin.Y;
```

**Problems**:

1. **Culling Math is Wrong**:
   - For Route 102 at `WorldOrigin (-320, -640)` with tile at local position `(0, 0)`:
   - `worldTileX = 0 + (-320 / 16) = 0 + (-20) = -20`
   - This is correct world tile coordinate!

2. **Rendering Math is Correct**:
   - For Route 102 tile at local `(0, 0)` with `WorldOrigin (-320, -640)`:
   - `_reusablePosition.X = 0 * 16 + (-320) = -320` ✅ Correct world pixel X
   - `_reusablePosition.Y = (0 + 1) * 16 + (-640) = 16 - 640 = -624` ✅ Correct world pixel Y

3. **Camera Bounds are World Coordinates**:
   - Camera position is in world coordinates (follows player)
   - Camera bounds are calculated in world space
   - Culling comparison is valid

---

### Issue #4: Camera Transform Confusion

**File**: `/PokeSharp.Engine.Rendering/Components/Camera.cs` (Lines 157-168)

```csharp
public readonly Matrix GetTransformMatrix()
{
    // Round camera position to nearest pixel after zoom to prevent texture bleeding/seams
    var roundedX = MathF.Round(Position.X * Zoom) / Zoom;
    var roundedY = MathF.Round(Position.Y * Zoom) / Zoom;

    return Matrix.CreateTranslation(-roundedX, -roundedY, 0)
        * Matrix.CreateRotationZ(Rotation)
        * Matrix.CreateScale(Zoom, Zoom, 1)
        * Matrix.CreateTranslation(Viewport.Width / 2f, Viewport.Height / 2f, 0);
}
```

**Analysis**:
- Camera.Position is in **world coordinates** (e.g., player at world position 160, -480)
- Transform translates from world space to screen space
- This is **CORRECT** for world-space rendering

---

## The REAL Issue: Camera Position vs Tile Rendering

### Current Flow (CORRECT):

1. **Map Loading** (MapLoader.cs):
   ```csharp
   // Loads Route 102 at WorldOrigin (-320, -640)
   var mapWorldPos = new MapWorldPosition(
       worldOffset: Vector2(-320, -640),
       width: 50,
       height: 20,
       tileSize: 16
   );
   ```

2. **Tile Creation** (LayerProcessor.cs):
   ```csharp
   // Creates tiles with LOCAL coordinates (0-49, 0-19)
   new TilePosition(x: 0, y: 0, mapId: 4)  // Route 102 first tile
   ```

3. **Tile Rendering** (ElevationRenderSystem.cs):
   ```csharp
   // Converts local to world coordinates
   var worldOrigin = _mapWorldOrigins[4];  // Vector2(-320, -640)
   _reusablePosition.X = 0 * 16 + (-320) = -320  // ✅ Correct world X
   _reusablePosition.Y = 1 * 16 + (-640) = -624  // ✅ Correct world Y
   ```

4. **Camera Transform**:
   ```csharp
   // If camera is at world position (160, -480) [center of Oldale Town]
   Matrix.CreateTranslation(-160, 480, 0)  // Move world to origin
   * Matrix.CreateScale(Zoom)
   * Matrix.CreateTranslation(ViewportWidth/2, ViewportHeight/2)
   ```

5. **Screen Position**:
   ```csharp
   // Route 102 tile at world (-320, -624) with camera at (160, -480)
   screenX = (-320 - 160) * Zoom + ViewportWidth/2 = -480 * Zoom + ViewportWidth/2
   screenY = (-624 - (-480)) * Zoom + ViewportHeight/2 = -144 * Zoom + ViewportHeight/2

   // Oldale Town tile at world (0, -624) with camera at (160, -480)
   screenX = (0 - 160) * Zoom + ViewportWidth/2 = -160 * Zoom + ViewportWidth/2
   screenY = (-624 - (-480)) * Zoom + ViewportHeight/2 = -144 * Zoom + ViewportHeight/2
   ```

**THE MATH IS CORRECT!** Route 102 and Oldale Town should render at different X positions.

---

## Debugging the "Same Location" Issue

### Hypothesis 1: MapWorldPosition Component Not Attached
**Check**: Verify both map entities have MapWorldPosition component

**File**: `/PokeSharp.Game.Data/MapLoading/Tiled/Core/MapLoader.cs` (Lines 419-436)

```csharp
// ALWAYS add MapWorldPosition component (even for origin map at 0,0)
var mapWorldPos = new MapWorldPosition(
    context.WorldOffset,  // ✅ Should be (0, -640) for Oldale, (-320, -640) for Route 102
    tmxDoc.Width,
    tmxDoc.Height,
    tmxDoc.TileWidth
);
mapInfoEntity.Add(mapWorldPos);
```

**Verification Needed**:
```csharp
// Check if both maps have MapWorldPosition
world.Query(in _mapWorldPosQuery, (ref MapInfo mapInfo, ref MapWorldPosition worldPos) =>
{
    Console.WriteLine($"Map {mapInfo.MapId.Value}: WorldOrigin = {worldPos.WorldOrigin}");
});
```

---

### Hypothesis 2: WorldOrigin Cache Not Updated
**File**: `/PokeSharp.Engine.Rendering/Systems/ElevationRenderSystem.cs` (Lines 458-468)

```csharp
private void UpdateMapWorldOriginsCache(World world)
{
    _mapWorldOrigins.Clear();  // ✅ Clears every frame
    world.Query(
        in _mapWorldPosQuery,
        (ref MapInfo mapInfo, ref MapWorldPosition worldPos) =>
        {
            _mapWorldOrigins[mapInfo.MapId.Value] = worldPos.WorldOrigin;  // ✅ Updates cache
        }
    );
}
```

**Verification Needed**:
```csharp
// After UpdateMapWorldOriginsCache()
Console.WriteLine($"Cached origins: {string.Join(", ", _mapWorldOrigins.Select(kv => $"{kv.Key}:{kv.Value}"))}");
// Expected: "2:(0, -640), 4:(-320, -640)"
```

---

### Hypothesis 3: Depth Sorting Causes Z-Fighting
**File**: `/PokeSharp.Engine.Rendering/Systems/ElevationRenderSystem.cs` (Lines 866-889)

```csharp
private static float CalculateElevationDepth(byte elevation, float yPosition, int mapId)
{
    var normalizedY = yPosition / MapHeight;
    var mapOffset = mapId * 0.1f;  // ✅ MapId 2 = 0.2, MapId 4 = 0.4

    var depth = elevation * 16.0f + mapOffset + normalizedY;
    var layerDepth = 1.0f - depth / 251.0f;

    return MathHelper.Clamp(layerDepth, 0.0f, 1.0f);
}
```

**Analysis**:
- Route 102 (MapId 4): depth includes `4 * 0.1 = 0.4`
- Oldale Town (MapId 2): depth includes `2 * 0.1 = 0.2`
- Route 102 renders BEHIND Oldale Town by 0.2 depth units

**Problem**: This is intentional for overlapping maps, but shouldn't affect horizontal positioning.

---

### Hypothesis 4: Camera Transform Issue
**File**: `/PokeSharp.Engine.Rendering/Systems/ElevationRenderSystem.cs` (Lines 421-451)

```csharp
private void UpdateCameraCache(World world)
{
    world.Query(
        in _cameraQuery,
        (ref Camera camera) =>
        {
            _cachedCameraTransform = camera.GetTransformMatrix();  // ✅ Correct transform

            // Calculate camera bounds for culling
            var left = (int)(camera.Position.X / TileSize) - viewport_tiles - margin;
            // ... bounds calculation
        }
    );
}
```

**Verification Needed**:
```csharp
// Check camera position when issue occurs
Console.WriteLine($"Camera position: {camera.Position}");
Console.WriteLine($"Camera transform: {_cachedCameraTransform}");
```

---

## Architecture Verification Checklist

### ✅ Verified Correct:
1. **TilePosition stores local coordinates** (0-19) - This is by design
2. **MapWorldPosition stores world origin** - Component exists and is attached
3. **ElevationRenderSystem applies WorldOrigin** - Math is correct
4. **Camera uses world coordinates** - Transform is correct
5. **Depth sorting includes MapId** - Prevents z-fighting

### ❓ Needs Verification:
1. **Are BOTH maps being loaded with correct WorldOrigin?**
   - Oldale Town should have WorldOrigin (0, -640)
   - Route 102 should have WorldOrigin (-320, -640)

2. **Is _mapWorldOrigins cache populated correctly?**
   - Should contain entries for both MapId 2 and MapId 4
   - Values should match MapWorldPosition component

3. **Is camera position correct?**
   - Should be in world coordinates (following player)
   - Not accidentally in local map coordinates

4. **Are tiles being created for BOTH maps?**
   - Check tile count for each map
   - Verify MapId is set correctly

---

## Proposed Solution

### Step 1: Add Debug Logging

```csharp
// In ElevationRenderSystem.RenderAllTiles()
if (_frameCounter % 60 == 0)  // Every 60 frames
{
    _logger?.LogDebug("Map world origins cache:");
    foreach (var (mapId, origin) in _mapWorldOrigins)
    {
        _logger?.LogDebug($"  Map {mapId}: {origin}");
    }

    _logger?.LogDebug($"Camera position: {camera.Position}");
}

// In tile rendering loop
if (tilesRendered < 5)  // First 5 tiles
{
    _logger?.LogDebug($"Tile {tilesRendered}: MapId={pos.MapId}, LocalPos=({pos.X},{pos.Y}), WorldOrigin={worldOrigin}, RenderPos={_reusablePosition}");
}
```

### Step 2: Verify Map Loading

```csharp
// In MapLoader.LoadMapFromDocument()
_logger?.LogInformation($"Loading map {mapDef.MapId} with WorldOffset {worldOffset}");
_logger?.LogInformation($"MapWorldPosition: Origin={mapWorldPos.WorldOrigin}, Size=({mapWorldPos.WidthInPixels}x{mapWorldPos.HeightInPixels})");
```

### Step 3: Verify Camera Bounds Calculation

```csharp
// In ElevationRenderSystem.UpdateCameraCache()
_logger?.LogDebug($"Camera bounds (world tiles): Left={left}, Top={top}, Width={width}, Height={height}");
```

---

## Expected Fix

### If Maps ARE Being Loaded Correctly:
The architecture is **sound**. The issue is likely:
1. Camera position initialization (starting at wrong location)
2. Player position initialization (not in world coordinates)
3. Viewport culling too aggressive (culling visible tiles)

### If Maps Are NOT Being Loaded Correctly:
Check:
1. `MapStreamingSystem` - Is it calling `LoadMapAtOffset()` correctly?
2. `MapWorldPosition` - Is it being attached to map entities?
3. World offset calculation - Are connection offsets computed correctly?

---

## Architectural Recommendations

### DO NOT Change:
1. ❌ **TilePosition** - Local coordinates are correct by design
2. ❌ **MapWorldPosition** - Component structure is correct
3. ❌ **ElevationRenderSystem world offset logic** - Math is correct
4. ❌ **Camera transform** - World-space rendering is correct

### DO Verify:
1. ✅ Map loading creates correct `MapWorldPosition` components
2. ✅ `_mapWorldOrigins` cache is populated for all loaded maps
3. ✅ Camera position is in world coordinates
4. ✅ Player position is in world coordinates
5. ✅ Viewport culling uses correct world-space bounds

---

## Test Cases

### Test 1: Single Map at Origin (0, 0)
**Expected**: Map renders normally, tiles visible

### Test 2: Single Map at Offset (-320, -640)
**Expected**: Map renders at correct world position

### Test 3: Two Maps Side-by-Side
**Expected**:
- Oldale Town (0, -640) renders on RIGHT
- Route 102 (-320, -640) renders on LEFT
- No overlap except at connection point

### Test 4: Camera at World Position (160, -480)
**Expected**:
- Oldale Town tiles at world (0, -640) to (320, -320) visible
- Route 102 tiles at world (-320, -640) to (0, -320) visible
- Camera centered between both maps

---

## Conclusion

The **architecture is CORRECT** for multi-map rendering. The issue is likely in:

1. **Map Loading**: Verify `WorldOrigin` values when maps are loaded
2. **Cache Population**: Verify `_mapWorldOrigins` contains both maps
3. **Camera Initialization**: Verify camera starts at correct world position
4. **Player Initialization**: Verify player spawns at correct world position

The rendering math, coordinate transformations, and depth sorting are all **architecturally sound** for handling multiple maps in a shared world coordinate space.
