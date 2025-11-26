# Map Streaming System - Usage Scenarios

This document provides scenarios demonstrating how the map streaming system works in PokeSharp.

> **Note**: The streaming system uses **automatic connection-based loading**, not configurable radius. When the player approaches a map boundary (within 80px), all connected maps automatically load.

---

## Scenario 1: Basic Two-Map Transition (Littleroot Town → Route 101)

### Architecture Overview

The MapStreamingSystem automatically:
1. Detects when player is within 80px of a map boundary
2. Loads connected maps using the `NorthMapId`, `SouthMapId`, `EastMapId`, `WestMapId` properties
3. Applies correct world offset from MapConnection data
4. Unloads maps when player is >160px from any boundary (hysteresis)

### Expected Behavior

1. **Player starts in Littleroot Town** at grid position (10, 2)
2. **Walking north** toward Y = 0
3. **Within 80px of north boundary**: Route 101 automatically loads at offset (0, -320)
4. **Crossing boundary**: Player's MapId updates, grid coordinates recalculate
5. **160px+ from boundary**: Distant maps unload to save memory

### Debug Output (Actual Format)

```
[MapStreaming] Player approaching north boundary of littleroot_town (distance: 48px)
[MapStreaming] Loading connected map: route101 at world offset (0, -320)
[MapStreaming] MapWorldPosition component added | mapId: 1, offsetX: 0, offsetY: -320
[MapStreaming] Player crossed boundary: littleroot_town -> route101 at world pixels (160, -16)
[MapStreaming] Grid coordinates recalculated: (10, 19) in route101 local space
```

### Common Issues and Fixes

| Issue | Cause | Fix |
|-------|-------|-----|
| Map doesn't load when approaching boundary | Connection data NULL | Check MapDefinition has NorthMapId/etc parsed correctly |
| Map loads but renders at (0,0) | Offset not applied | Verify LoadMapAtOffset() is called, not LoadMap() |
| Map loads but is invisible | Viewport culling wrong space | Check world coordinates used in culling |
| Player stuck at boundary | Movement in local space | Ensure GetMapWorldOffset() applied to target pixels |
| Ping-ponging between maps | Stale grid coordinates | Grid coords must recalculate when MapId changes |

---

## Scenario 2: Multi-Direction Hub (Oldale Town)

### Overview

Oldale Town connects to:
- **North**: Route 103
- **South**: Route 101
- **West**: Route 102

### How Multiple Connections Work

When player is in center of Oldale Town, only Oldale is loaded. As they approach edges:

```
Player moves north:
  → Within 80px of north edge
  → Route 103 loads at calculated offset
  → Route 101 and 102 remain unloaded (>160px away)

Player at northeast corner:
  → Within 80px of BOTH north AND east edges
  → Route 103 loads (north connection)
  → No east connection defined → nothing loads east
```

### Key Points

1. **Maximum loaded maps**: Current map + 4 adjacent (N/S/E/W) = 5 maps max
2. **No diagonal connections**: Pokemon games don't use diagonal map connections
3. **Automatic unloading**: Maps >160px from ANY boundary unload
4. **Memory management**: System naturally limits memory via distance-based unloading

---

## Scenario 3: Indoor/Outdoor Transitions

### Current Implementation

Indoor maps (Pokemon Centers, houses) are handled differently:
- They have their own coordinate space (start at 0,0)
- No streaming - load entirely on warp
- Use warp tiles, not boundary crossing

### Warp vs Streaming

| Feature | Streaming | Warping |
|---------|-----------|---------|
| Trigger | Walk across boundary | Step on warp tile |
| Transition | Seamless | Fade/loading possible |
| Coordinate space | Continuous world | Separate per building |
| Map loading | Gradual (80px threshold) | Instant on warp |

---

## Scenario 4: Edge Cases

### Map Boundary Coordinate Recalculation

When crossing from Littleroot (offset 0,0) to Route 101 (offset 0,-320):

```
BEFORE crossing:
  position.X = 10, position.Y = -1 (Littleroot local grid)
  position.PixelX = 160, position.PixelY = -16 (world pixels)
  position.MapId = 0 (Littleroot)

AFTER crossing (movement completes):
  position.PixelX = 160, position.PixelY = -16 (unchanged)
  position.MapId = 1 (Route 101)

  Recalculate grid from world pixels:
  mapOffset = (0, -320)
  position.X = (160 - 0) / 16 = 10
  position.Y = (-16 - (-320)) / 16 = 19

  Result: position.X = 10, position.Y = 19 (Route 101 local grid)
```

### Hysteresis Prevents Oscillation

```
Load threshold:   80px from boundary
Unload threshold: 160px from boundary (2x)

Player at 90px from boundary:
  → Outside load threshold (80px), don't load

Player at 70px from boundary:
  → Inside load threshold, LOAD adjacent map

Player backs up to 120px from boundary:
  → Still inside unload threshold (160px), DON'T unload

Player backs up to 170px from boundary:
  → Outside unload threshold, UNLOAD adjacent map
```

---

## Testing Checklist

### Basic Streaming
- [ ] Walk from Littleroot to Route 101 (north)
- [ ] Walk back from Route 101 to Littleroot (south)
- [ ] Verify no visual pop-in at boundaries
- [ ] Verify player can move freely in loaded map

### Multi-Map
- [ ] Walk through Oldale Town in all directions
- [ ] Verify correct maps load/unload
- [ ] Check memory doesn't grow unbounded
- [ ] Verify Z-ordering correct (no tile overlap)

### Edge Cases
- [ ] Walk along map boundary without crossing
- [ ] Rapidly change direction at boundary
- [ ] Save/load while on map boundary
- [ ] Check performance with 5 maps loaded

### Console Verification
- [ ] MapWorldPosition offsets are correct
- [ ] Grid coordinates recalculate on MapId change
- [ ] Load/unload messages appear at expected distances
- [ ] No "ping-pong" rapid load/unload logs

---

## Related Documentation

- [MAP_STREAMING_BUGS_SUMMARY.md](../architecture/MAP_STREAMING_BUGS_SUMMARY.md) - Common bugs and fixes
- [MAP_STREAMING_INTEGRATION.md](../api/MAP_STREAMING_INTEGRATION.md) - API reference
- [MAP_STREAMING_PERFORMANCE.md](../performance/MAP_STREAMING_PERFORMANCE.md) - Optimization guide
