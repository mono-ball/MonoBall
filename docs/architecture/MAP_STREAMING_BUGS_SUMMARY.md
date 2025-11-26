# Map Streaming System - Bug Fixes Summary

This document consolidates the key bugs fixed during the map streaming system implementation. For detailed analysis, see individual fix documents in this directory.

## Overview

The map streaming system enables seamless transitions between adjacent maps (like Pokemon games). Five critical bugs were identified and fixed:

| # | Bug | File | Impact | Status |
|---|-----|------|--------|--------|
| 1 | Infinite load/unload loop | MapStreamingSystem.cs | Game freeze, crash | ✅ Fixed |
| 2 | Maps rendering at wrong position | MapLoader.cs, ElevationRenderSystem.cs | Visual overlap | ✅ Fixed |
| 3 | Viewport culling in wrong space | ElevationRenderSystem.cs | Maps invisible | ✅ Fixed |
| 4 | Movement stuck at boundaries | MovementSystem.cs | Cannot explore | ✅ Fixed |
| 5 | Ping-ponging between maps | MovementSystem.cs | Rapid flickering | ✅ Fixed |

---

## Bug #1: Infinite Load/Unload Loop

**Symptom**: Route101 loads and immediately unloads hundreds of times per second, causing:
- 108 Gen0 GC collections in 5 seconds
- Game frozen at ~6 FPS
- Projected crash in 30-90 seconds

**Root Cause**: `UnloadDistantMaps()` calculated distance to arbitrary center point (160,160) instead of nearest boundary point. This caused maps to be simultaneously "close enough to load" and "far enough to unload."

**Fix**: Implemented `CalculateDistanceToMapBoundary()` helper that calculates shortest distance to rectangle boundary. Uses hysteresis (80px load threshold, 160px unload threshold).

**Details**: [INFINITE_LOOP_BUG_FIX.md](./INFINITE_LOOP_BUG_FIX.md)

---

## Bug #2: Maps Rendering at Same Position

**Symptom**: Route101 renders at (0,0) instead of (0,-320), causing tile overlap with Littleroot Town.

**Root Cause**: Two-part failure:
1. `MapStreamingSystem` called `LoadMap()` which doesn't accept offset parameter
2. `ElevationRenderSystem` didn't read `MapWorldPosition` component for offset

**Fix**:
1. Changed to `LoadMapAtOffset(world, mapId, offset)`
2. Added per-frame caching of MapWorldPosition offsets in renderer
3. Applied world offset during tile rendering

**Details**: [MAP_OFFSET_RENDERING_FIX.md](./MAP_OFFSET_RENDERING_FIX.md)

---

## Bug #3: Viewport Culling in Wrong Coordinate Space

**Symptom**: Route101 loads correctly but tiles don't appear on screen.

**Root Cause**: Viewport culling compared local tile coordinates (0-19) against world-space camera bounds. Route101 tiles at local (10,10) passed culling but rendered 320px above camera view.

**Fix**: Convert tile position to world coordinates BEFORE culling check:
```csharp
// Get world origin FIRST
var worldOrigin = _mapWorldOrigins[pos.MapId.Value];

// Calculate WORLD tile position for culling
var worldTileX = pos.X + (int)(worldOrigin.X / TileSize);
var worldTileY = pos.Y + (int)(worldOrigin.Y / TileSize);

// THEN do culling check in world space
if (worldTileX < cameraBounds.Left || worldTileX >= cameraBounds.Right)
    return; // Cull
```

**Details**: [VIEWPORT_CULLING_BUG_FIX.md](./VIEWPORT_CULLING_BUG_FIX.md)

---

## Bug #4: Movement Stuck at Map Boundaries

**Symptom**: Player crosses from Littleroot to Route101 but cannot move deeper into Route101.

**Root Cause**: `MovementSystem.TryStartMovement()` calculated target pixels in LOCAL space without map offset:
```csharp
// ❌ WRONG - Local space
var targetPixels = new Vector2(targetX * tileSize, targetY * tileSize);
```

**Fix**: Added `GetMapWorldOffset()` helper and include offset in movement calculations:
```csharp
// ✅ CORRECT - World space
var mapOffset = GetMapWorldOffset(position.MapId);
var targetPixels = new Vector2(
    targetX * tileSize + mapOffset.X,
    targetY * tileSize + mapOffset.Y
);
```

**Details**: [MAP_COORDINATE_SPACE_FIX.md](./MAP_COORDINATE_SPACE_FIX.md)

---

## Bug #5: Ping-Ponging Between Maps

**Symptom**: Player rapidly crosses back and forth between Route101 and Oldale Town (17ms apart).

**Root Cause**: Grid coordinates set at movement START but never recalculated when MapId changes during movement. After crossing, grid coords are in OLD map's space but MapId points to NEW map.

**Fix**: Recalculate grid coordinates from world pixels when movement completes if MapId changed:
```csharp
if (movement.MovementProgress >= 1.0f)
{
    // Get current map offset
    var mapOffset = GetMapWorldOffset(position.MapId);

    // Recalculate grid from world pixels
    position.X = (int)((position.PixelX - mapOffset.X) / tileSize);
    position.Y = (int)((position.PixelY - mapOffset.Y) / tileSize);

    movement.CompleteMovement();
}
```

**Details**: [GRID_COORDINATE_SYNC_FIX.md](./GRID_COORDINATE_SYNC_FIX.md)

---

## Key Architectural Concepts

### Three Coordinate Systems

1. **Grid Coordinates** (`position.X`, `position.Y`): Tile-based, relative to current map (0-19 for 20-tile maps)
2. **Local Pixel Coordinates**: `grid * tileSize`, relative to map origin
3. **World Pixel Coordinates** (`position.PixelX`, `position.PixelY`): Absolute position including map offset

**Conversion**:
```
World Pixels = Local Pixels + MapWorldPosition.WorldOrigin
Grid = (World Pixels - MapWorldPosition.WorldOrigin) / TileSize
```

### Hysteresis for Streaming

- **Load threshold**: 80 pixels from boundary
- **Unload threshold**: 160 pixels from boundary (2x load)
- **Purpose**: Prevents rapid load/unload cycles at boundaries

### Distance-to-Boundary Algorithm

Standard approach for streaming systems - calculate shortest distance to rectangle edge, not center:
```csharp
float CalculateDistanceToMapBoundary(Vector2 point, Rectangle mapBounds)
{
    float dx = Max(mapBounds.Left - point.X, 0, point.X - mapBounds.Right);
    float dy = Max(mapBounds.Top - point.Y, 0, point.Y - mapBounds.Bottom);
    return Sqrt(dx*dx + dy*dy);
}
```

---

## Related Documents

- [MAP_CONNECTION_PARSING_FIX.md](./MAP_CONNECTION_PARSING_FIX.md) - Tiled JSON connection parsing
- [ARCHITECTURAL_ANALYSIS_MAP_OFFSET_BUG.md](./ARCHITECTURAL_ANALYSIS_MAP_OFFSET_BUG.md) - Component design decisions
- [MULTI_MAP_RENDERING_ARCHITECTURE.md](./MULTI_MAP_RENDERING_ARCHITECTURE.md) - Z-ordering and depth sorting
