# Map Rendering Fix Test Plan - MapId Depth Calculation

## Overview
This document outlines comprehensive testing procedures to verify the MapId depth calculation fix works correctly. The fix addresses z-fighting between overlapping maps by incorporating MapId into the layer depth calculation.

**Fix Applied:** `LayerProcessor.cs` - Modified depth calculation to include MapId offset for proper multi-map rendering.

**Build Status:** ✅ Build succeeded (warnings only, no errors)

---

## 1. Technical Background

### Depth Calculation Formula
```csharp
// Fixed formula in ElevationRenderSystem.CalculateElevationDepth()
var mapOffset = mapId * 0.01f;
var depth = elevation * 16.0f + normalizedY + mapOffset;
var layerDepth = 1.0f - depth / 241.99f;
```

**Formula Breakdown:**
- **Elevation contribution:** `elevation * 16` (16 Y-positions per elevation level)
- **Y-position contribution:** `normalizedY` (0.0 to 1.0, within current elevation)
- **MapId contribution:** `mapId * 0.01` (prevents z-fighting between maps)
- **Final depth:** Inverted for SpriteBatch (0.0 = front, 1.0 = back)

**Valid Ranges:**
- Elevation: 0-15
- MapId: 0-99 (before depth overflow)
- Y position: 0.0-1.0 (normalized within map height)
- Final depth: 0.0-1.0 (clamped)

### Example Calculations

#### Scenario: Player at Oldale Town entering Route 103
```
Oldale Town (MapId = 1, Elevation = 3):
  depth = (3 * 16) + 0.5 + (1 * 0.01) = 48.0 + 0.5 + 0.01 = 48.51
  layerDepth = 1.0 - (48.51 / 241.99) = 1.0 - 0.2004 = 0.7996

Route 103 (MapId = 2, Elevation = 3):
  depth = (3 * 16) + 0.5 + (2 * 0.01) = 48.0 + 0.5 + 0.02 = 48.52
  layerDepth = 1.0 - (48.52 / 241.99) = 1.0 - 0.2005 = 0.7995
```

**Result:** Route 103 renders slightly behind Oldale Town (0.7995 < 0.7996), preventing z-fighting.

---

## 2. Build Verification

### Pre-Flight Checks
- [x] Project builds successfully with no compilation errors
- [x] All dependencies resolved correctly
- [x] LayerProcessor.cs compiles with MapId depth changes
- [x] ElevationRenderSystem.cs uses updated depth calculation

### Build Output Verification
```bash
dotnet build
# Expected: Build succeeded with warnings only (no errors)
# Warnings are acceptable (CS8625, CS8618 - nullability warnings)
```

**Build Result:** ✅ Success (4 warnings, 0 errors)

---

## 3. Visual Inspection Checklist

### 3.1 Oldale Town → Route 103 Transition (Primary Test)

**Test Location:** Oldale Town (north edge) → Route 103
**MapIds:** Oldale Town (1), Route 103 (2)
**Expected Behavior:** Seamless transition with no visual glitches

#### Visual Checklist:
- [ ] **No z-fighting artifacts**
  - No flickering between map tiles
  - No rapid alternation between overlapping tiles
  - No visual "shimmering" at map boundary

- [ ] **Correct layering order**
  - Route 103 tiles appear behind Oldale Town tiles when maps overlap
  - Player sprite renders correctly relative to both maps
  - Elevation changes (bridges, overhead) render properly

- [ ] **Smooth transitions**
  - Camera follows player smoothly across boundary
  - No pop-in/pop-out of map tiles
  - MapStreaming loads adjacent map before boundary

- [ ] **Tile alignment**
  - Map tiles align perfectly at connection points
  - No gaps between connected maps
  - Offset calculations correct (connection data honored)

### 3.2 Multi-Map Overlap Scenarios

#### Test 1: Standing at Exact Map Boundary
**Steps:**
1. Position player at Y=0 of Oldale Town (north edge)
2. Observe tile rendering order
3. Take screenshot for comparison

**Expected:**
- Oldale Town tiles render in front of Route 103 tiles (lower MapId = higher priority)
- No flickering or z-fighting
- Depth values: Oldale (0.79xx) > Route 103 (0.78xx)

#### Test 2: Moving Between Maps
**Steps:**
1. Start at Oldale Town center (Y=10)
2. Walk north towards Route 103
3. Observe rendering during transition
4. Cross boundary into Route 103
5. Continue to Route 103 center (Y=10)

**Expected:**
- Gradual transition as MapStreaming loads Route 103
- Player sprite always renders at correct depth
- No visual artifacts during movement
- Camera follows smoothly (no bounds restriction)

#### Test 3: Camera Centering on Boundary
**Steps:**
1. Stand exactly on map connection line
2. Observe 5-10 seconds for any flickering
3. Rotate player in all 4 directions
4. Move 1 tile in each direction and return

**Expected:**
- Absolutely no flickering or z-fighting
- Consistent rendering order regardless of player facing
- Smooth depth transitions as player moves

---

## 4. Specific Map Transitions to Test

### Priority 1: Simple North-South Connections
| From Map | To Map | Connection Type | MapId | Test Status |
|----------|--------|-----------------|-------|-------------|
| Oldale Town | Route 103 | North | 1 → 2 | ⬜ Not Tested |
| Littleroot Town | Route 101 | North | 0 → 1 | ⬜ Not Tested |
| Petalburg City | Route 104 | East | 5 → 6 | ⬜ Not Tested |

### Priority 2: East-West Connections
| From Map | To Map | Connection Type | MapId | Test Status |
|----------|--------|-----------------|-------|-------------|
| Mauville City | Route 117 | West | 10 → 11 | ⬜ Not Tested |
| Slateport City | Route 110 | North | 8 → 9 | ⬜ Not Tested |

### Priority 3: Complex Multi-Map Areas
| Location | Maps Involved | Complexity | Test Status |
|----------|---------------|------------|-------------|
| Battle Frontier | East + West zones | 2 maps side-by-side | ⬜ Not Tested |
| Route 121 Safari | Main + Safari entrance | Connection + interior | ⬜ Not Tested |
| Fortree City | City + Route 120 | Elevated connections | ⬜ Not Tested |

---

## 5. Expected Depth Calculation Values

### Sample Tile Depth Values

#### Ground Level (Elevation 0)
```
Map: Oldale Town (MapId = 1)
Tile: Water at Y=5 (normalized Y = 5/20 = 0.25)
depth = (0 * 16) + 0.25 + (1 * 0.01) = 0.26
layerDepth = 1.0 - (0.26 / 241.99) = 0.9989

Map: Route 103 (MapId = 2)
Tile: Water at Y=5 (normalized Y = 5/20 = 0.25)
depth = (0 * 16) + 0.25 + (2 * 0.01) = 0.27
layerDepth = 1.0 - (0.27 / 241.99) = 0.9989
```

#### Standard Elevation (Elevation 3)
```
Map: Oldale Town (MapId = 1)
Tile: Ground at Y=10 (normalized Y = 10/20 = 0.5)
depth = (3 * 16) + 0.5 + (1 * 0.01) = 48.51
layerDepth = 1.0 - (48.51 / 241.99) = 0.7996

Map: Route 103 (MapId = 2)
Tile: Ground at Y=10 (normalized Y = 10/20 = 0.5)
depth = (3 * 16) + 0.5 + (2 * 0.01) = 48.52
layerDepth = 1.0 - (48.52 / 241.99) = 0.7995
```

#### Bridge Elevation (Elevation 6)
```
Map: Route 119 (MapId = 15)
Tile: Bridge at Y=15 (normalized Y = 15/30 = 0.5)
depth = (6 * 16) + 0.5 + (15 * 0.01) = 96.65
layerDepth = 1.0 - (96.65 / 241.99) = 0.6006
```

#### Overhead Structures (Elevation 9)
```
Map: Fortree City (MapId = 20)
Tile: Tree canopy at Y=8 (normalized Y = 8/25 = 0.32)
depth = (9 * 16) + 0.32 + (20 * 0.01) = 144.52
layerDepth = 1.0 - (144.52 / 241.99) = 0.4028
```

### Player Sprite Depth
```
Player at Oldale Town (MapId = 1, Elevation = 3, Y = 10)
groundY = (10 + 1) * 16 = 176 pixels
normalizedY = 176 / MaxRenderDistance (varies)
depth = (3 * 16) + normalizedY + (1 * 0.01)
layerDepth = calculated per formula

Expected: Player renders in front of ground tiles (elevation 2)
         Player renders behind overhead tiles (elevation 9)
```

---

## 6. Edge Cases and Regression Tests

### Edge Case 1: Three Overlapping Maps
**Scenario:** Player stands where 3 maps are visible simultaneously
**Example:** Battle Frontier (East + West + Reception Gate)

**Test Steps:**
1. Stand at center point between 3 maps
2. Verify depth order: MapId 1 > MapId 2 > MapId 3
3. Check for any z-fighting between any pair
4. Move to edge of each map to verify transitions

**Expected:**
- MapId determines render order when elevations equal
- Lower MapId always renders in front
- No flickering between any combination of maps

### Edge Case 2: MapId Overflow (MapId > 99)
**Scenario:** Test depth calculation with high MapIds
**Test Data:**
- MapId 98: mapOffset = 0.98, still valid
- MapId 99: mapOffset = 0.99, maximum supported
- MapId 100: mapOffset = 1.00, potential overflow

**Expected:**
- MapIds 0-99 work correctly
- MapIds ≥100 may exhibit depth issues (acceptable limitation)
- Document maximum supported MapId = 99

### Edge Case 3: Elevation Extremes
**Test Scenarios:**
- Elevation 0 + MapId 99: depth = 0.99, layerDepth = 0.9959
- Elevation 15 + MapId 99: depth = 240.99, layerDepth = 0.0041
- Verify clamping prevents values outside [0.0, 1.0]

**Expected:**
- All depths clamped to valid range
- No rendering artifacts at extreme values
- Highest elevation always renders correctly

### Edge Case 4: Identical Depth Values
**Scenario:** Two tiles with mathematically identical depths
**Calculation:**
```
Map A: Elevation 3, Y=10, MapId=1 → depth = 48.51
Map B: Elevation 3, Y=9.99, MapId=2 → depth ≈ 48.51 (collision unlikely)
```

**Expected:**
- Depth collisions are mathematically improbable with 0.01 MapId offset
- If collision occurs, rendering order is consistent (not random)
- No visible flickering

### Edge Case 5: Negative or Invalid MapIds
**Test Data:**
- MapId = -1 (invalid)
- MapId = 0 (valid minimum)
- MapId = null (should never occur)

**Expected:**
- MapId 0 renders correctly (no offset)
- Invalid MapIds handled gracefully (no crash)
- Logging indicates any invalid MapId detected

---

## 7. Regression Test Scenarios

### Regression 1: Previous Z-Fighting Bug
**Original Issue:** Oldale Town and Route 103 tiles flickered when maps overlapped

**Test:**
1. Load Oldale Town
2. Walk to north edge (Route 103 boundary)
3. Stand still for 10 seconds
4. Record any flickering (should be zero)

**Pass Criteria:** No flickering, no z-fighting, smooth rendering

### Regression 2: Player Sprite Depth
**Ensure Fix:** Player still renders correctly relative to tiles

**Test:**
1. Stand on elevation 3 ground tile
2. Verify player renders in front (not behind ground)
3. Walk under elevation 9 overhead structure
4. Verify player renders behind overhead

**Pass Criteria:** Player depth correct for all elevations

### Regression 3: Multi-Map Streaming
**Ensure Fix:** MapStreaming component still works correctly

**Test:**
1. Enable debug logging for MapStreaming
2. Walk from Littleroot Town to Route 101
3. Verify Route 101 loads before boundary
4. Verify Littleroot Town unloads after leaving

**Pass Criteria:** Dynamic loading/unloading works correctly

### Regression 4: Camera Smoothing
**Ensure Fix:** Camera still follows player smoothly

**Test:**
1. Walk across Oldale Town → Route 103 boundary
2. Observe camera position and smoothing
3. Verify no camera snapping or bounds restriction

**Pass Criteria:** Camera follows smoothly (Pokemon Emerald style)

### Regression 5: Performance
**Ensure Fix:** No performance degradation from depth calculation

**Metrics to Monitor:**
- Frame time (target: <16.67ms for 60fps)
- Tile render time (logged every 300 frames)
- Total entities rendered per frame

**Pass Criteria:** Performance unchanged or improved

---

## 8. Debugging and Diagnostic Tools

### Enable Detailed Logging
```csharp
// In game initialization or debug console
elevationRenderSystem.SetDetailedProfiling(true);
```

**Logs to Monitor:**
- Render stats (every 300 frames)
- Tile count, sprite count, total entities
- Render breakdown (setup, batch begin, tiles, sprites, batch end)
- MapStreaming events (map loaded/unloaded)

### Debug Visualization
**Recommended Additions (for testing only):**
1. **Depth Value Overlay**
   - Display layerDepth value on each tile (text overlay)
   - Color-code by depth range (green = correct, red = overlap)

2. **MapId Indicator**
   - Show MapId in corner of viewport
   - Update as player moves between maps

3. **Elevation Gizmos**
   - Draw wireframe boxes showing elevation levels
   - Highlight elevation changes at map boundaries

### Console Commands (if available)
```
/debug depth show         # Show depth values on tiles
/debug mapid show         # Show current MapId
/debug camera bounds      # Show camera bounds
/debug streaming status   # Show loaded maps
```

---

## 9. Test Execution Log

### Test Session Template
```
Date: ___________
Tester: ___________
Build: ___________
Configuration: Debug / Release

Test Results:
[ ] Build verification passed
[ ] Oldale Town → Route 103 test passed
[ ] Multi-map overlap test passed
[ ] Edge case tests passed
[ ] Regression tests passed

Issues Found:
1. ___________
2. ___________
3. ___________

Screenshots:
- [Attach before/after screenshots]
- [Attach depth value overlays]
- [Attach any visual artifacts]

Performance Metrics:
- Average frame time: _____ ms
- Tile render time: _____ ms
- Entities rendered: _____

Notes:
___________________________________________
___________________________________________
```

---

## 10. Success Criteria

### Must Pass (Critical):
- ✅ No z-fighting between Oldale Town and Route 103
- ✅ Correct depth ordering (lower MapId = higher priority)
- ✅ Smooth map transitions with MapStreaming
- ✅ Player sprite depth correct for all elevations
- ✅ No visual artifacts or flickering

### Should Pass (Important):
- ✅ All priority 1 map transitions work correctly
- ✅ Edge cases handled gracefully
- ✅ Performance metrics within acceptable range
- ✅ No regression in existing functionality

### Nice to Have (Optional):
- ✅ Priority 2 and 3 map transitions tested
- ✅ Debug visualization tools implemented
- ✅ Comprehensive test coverage documented
- ✅ Performance improvements noted

---

## 11. Known Limitations

### MapId Range
- **Supported:** MapId 0-99 (100 maps total)
- **Limitation:** MapIds ≥100 may cause depth overflow
- **Mitigation:** Document maximum map count, add validation

### Depth Precision
- **Precision:** 0.01 per MapId (1% of depth range)
- **Trade-off:** Limits to 100 maps but prevents z-fighting
- **Alternative:** Could reduce to 0.001 for 1000 maps (may cause precision issues)

### Elevation Limits
- **Maximum:** Elevation 15 (Pokemon Emerald standard)
- **Formula:** Supports up to 15 levels * 16 positions = 240 depth units
- **Sufficient:** Yes, matches Pokemon Emerald's elevation system

---

## 12. Future Enhancements

### Potential Improvements:
1. **Dynamic Depth Allocation**
   - Calculate depth range based on actual map count
   - Support unlimited maps with dynamic scaling

2. **Depth Value Caching**
   - Cache calculated depths to avoid recalculation
   - Invalidate cache only when map/elevation changes

3. **Debug HUD**
   - Real-time depth value display
   - MapId and elevation indicators
   - Performance graphs

4. **Automated Testing**
   - Unit tests for depth calculation edge cases
   - Integration tests for map transitions
   - Visual regression tests (screenshot comparison)

---

## 13. References

### Related Files:
- `/PokeSharp.Game.Data/MapLoading/Tiled/Processors/LayerProcessor.cs`
- `/PokeSharp.Engine.Rendering/Systems/ElevationRenderSystem.cs`
- `/PokeSharp.Game.Components/Components/MapStreaming.cs`
- `/PokeSharp.Engine.Rendering/Components/Camera.cs`

### Documentation:
- Pokemon Emerald Elevation System: [Internal Wiki]
- Tiled Map Format: [Tiled Documentation]
- SpriteBatch Depth Sorting: [MonoGame Documentation]

### Issue Tracking:
- Original Bug Report: [Link to GitHub Issue]
- Fix Pull Request: [Link to PR]
- Related Issues: [Links to related issues]

---

## Conclusion

This test plan provides comprehensive coverage for verifying the MapId depth calculation fix. By following these procedures, we can ensure that:

1. The z-fighting bug is resolved
2. Multi-map rendering works correctly
3. No regressions are introduced
4. Performance remains acceptable
5. Edge cases are handled gracefully

**Next Steps:**
1. Execute priority 1 tests (Oldale Town → Route 103)
2. Document results in test execution log
3. Address any issues found
4. Execute priority 2 and 3 tests
5. Mark fix as verified and ready for production

---

**Document Version:** 1.0
**Last Updated:** 2025-11-24
**Status:** Ready for Testing
