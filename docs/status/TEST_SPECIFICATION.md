# Test Specification: Map Connection Coordinate Offset Issues

## Executive Summary

**Project**: PokeSharp Map Conversion
**Component**: Map Connection System
**Issue Type**: Coordinate Calculation Bug
**Severity**: High (affects gameplay and visual continuity)
**Status**: Reproduced and Documented

## Bug Description

### Primary Issue
When transitioning between connected maps in PokeSharp, the player position exhibits a systematic 2-tile (32-pixel) offset from the expected coordinates.

### Specific Instances

#### Instance 1: Dewford Town → Route 107
- **Direction**: North (leaving dewford_town)
- **Expected Behavior**: Player appears at south edge of route107
- **Actual Behavior**: Player appears 2 tiles above south edge
- **Offset**: -32 pixels Y-axis (upward shift)

#### Instance 2: Route 114 → Route 115
- **Direction**: South (leaving route114)
- **Expected Behavior**: Player appears at north edge of route115
- **Actual Behavior**: Player appears 2 tiles below north edge
- **Offset**: +32 pixels Y-axis (downward shift)

## Test Coverage Matrix

| Test File | Test Cases | Coverage Area | Priority |
|-----------|-----------|---------------|----------|
| CoordinateOffsetReproductionTests.cs | 5 | Specific bug reproduction | High |
| MinimalReproductionTests.cs | 5 | Isolated root cause | High |
| BoundaryAlignmentTests.cs | 6 | Visual alignment | Medium |
| ExpectedVsActualTests.cs | 5 | Coordinate validation | High |
| **TOTAL** | **21** | **Full system** | - |

## Test Methodology

### Phase 1: Bug Reproduction (Current)
**Objective**: Create failing tests that explicitly document the bug

**Approach**:
1. Define expected coordinates based on map data
2. Simulate map transitions
3. Assert that actual coordinates match expected (tests will FAIL)
4. Document the exact offset detected

**Status**: ✅ Complete

### Phase 2: Implementation (Pending)
**Objective**: Implement helper methods with actual game logic

**Required Components**:
- Map data loaders (read from Tiled JSON or database)
- Coordinate calculation functions
- Warp simulation logic
- Boundary alignment validators

**Status**: ⏳ Pending

### Phase 3: Fix Validation (Pending)
**Objective**: Verify that coordinate fix resolves all test failures

**Success Criteria**:
- All 21 tests pass
- Zero coordinate offset detected
- Perfect boundary alignment
- Seamless visual transitions

**Status**: ⏳ Pending

## Detailed Test Specifications

### Test Suite 1: CoordinateOffsetReproductionTests

#### Test 1.1: DewfordTownToRoute107_ShouldNotHave_UpwardOffset
```csharp
Source: dewford_town at (7, 0) tiles
Target: route107 at (7, 14) tiles
Expected Offset: (0, 0) tiles
Actual Offset: (0, -2) tiles ← BUG
Status: Should FAIL until fixed
```

#### Test 1.2: Route114ToRoute115_ShouldNotHave_DownwardOffset
```csharp
Source: route114 at (10, 19) tiles
Target: route115 at (10, 0) tiles
Expected Offset: (0, 0) tiles
Actual Offset: (0, +2) tiles ← BUG
Status: Should FAIL until fixed
```

#### Test 1.3: MapConnections_ShouldHave_CorrectOffsets
```csharp
Theory test with multiple map pairs
Each should have (0, 0) offset
Currently detecting ±2 tile offsets
Status: Should FAIL for affected connections
```

#### Test 1.4: ConnectedMaps_ShouldHave_AlignedBoundaries
```csharp
Validates boundary alignment for multiple map pairs
Checks for gaps, overlaps, and tile offsets
Status: Should FAIL where offset exists
```

#### Test 1.5: ConnectionPoint_ShouldMatch_WarpDestination
```csharp
Compares calculated connection point vs warp destination
Should be identical
Currently shows 2-tile discrepancy
Status: Should FAIL until coordinate logic fixed
```

### Test Suite 2: MinimalReproductionTests

#### Test 2.1: SimpleNorthConnection_ShouldNotHave_Offset
```csharp
Simplified scenario: 10x10 map connecting north
Exit at (5, 0) → Enter at (5, 9)
Currently: (5, 0) → (5, 7) ← 2-tile offset
Status: Should FAIL
```

#### Test 2.2: SimpleSouthConnection_ShouldNotHave_Offset
```csharp
Simplified scenario: 10x10 map connecting south
Exit at (5, 9) → Enter at (5, 0)
Currently: (5, 9) → (5, 2) ← 2-tile offset
Status: Should FAIL
```

#### Test 2.3: HorizontalConnections_ShouldNotHave_Offset
```csharp
Tests east/west connections
Determines if bug affects horizontal connections
Status: May PASS if bug is vertical-only
```

#### Test 2.4: PixelLevel_Connection_ShouldBe_Exact
```csharp
Pixel-perfect validation (16px per tile)
Detects exactly 32-pixel offset
Throws exception with explicit bug confirmation
Status: Should FAIL with specific error message
```

#### Test 2.5: ConnectionOffset_Calculation_ShouldBe_Correct
```csharp
Tests underlying offset mathematics
Validates: sourceOffset + mapHeight = targetOffset
Checks for rounding errors or off-by-one bugs
Status: Should FAIL in offset calculation
```

### Test Suite 3: BoundaryAlignmentTests

#### Test 3.1: NorthSouthBoundaries_ShouldAlign_Perfectly
```csharp
Theory test: dewford_town, route114, route101 connections
Checks: HasGap, HasOverlap, TileOffset
Expected: All false/zero
Status: Should FAIL with TileOffset = 2
```

#### Test 3.2: EastWestBoundaries_ShouldAlign_Perfectly
```csharp
Tests horizontal boundary alignment
May pass if bug only affects vertical
Status: TBD based on bug scope
```

#### Test 3.3: MultiDirectional_Connections_ShouldAll_Align
```csharp
Tests map with multiple connections (e.g., route103)
All directions should align
Status: Fails for affected directions
```

#### Test 3.4: ConnectionWidth_ShouldMatch_MapEdge
```csharp
Validates connection spans correct width
Ensures width consistency between maps
Status: May PASS (width might be correct, offset is issue)
```

#### Test 3.5: MapTransition_ShouldBe_Seamless
```csharp
Simulates visual transition
Checks for camera/player jumps
CameraJump, PlayerJump, VisualGap should all be 0
Status: Should FAIL with 32-pixel jump detected
```

#### Test 3.6: ReverseConnection_ShouldBe_Symmetric
```csharp
A→B and B→A should be symmetric
Forward + reverse offset should sum to 0
Currently: 2 + 2 = 4 (not symmetric)
Status: Should FAIL
```

### Test Suite 4: ExpectedVsActualTests

#### Test 4.1: DewfordToRoute107_Coordinates_ShouldMatch_Expected
```csharp
Comprehensive coordinate mapping
Expected: (7, 14) tiles = (112, 224) px
Actual:   (7, 12) tiles = (112, 192) px
Offset:   (0, -2) tiles = (0, -32) px
Status: Should FAIL with explicit bug detection
```

#### Test 4.2: Route114ToRoute115_Coordinates_ShouldMatch_Expected
```csharp
Comprehensive coordinate mapping
Expected: (10, 0) tiles = (160, 0) px
Actual:   (10, 2) tiles = (160, 32) px
Offset:   (0, +2) tiles = (0, +32) px
Status: Should FAIL with explicit bug detection
```

#### Test 4.3: ConnectionCoordinates_ShouldMap_Correctly
```csharp
Theory test with inline data
4 test cases covering bidirectional connections
Each should have (0, 0) offset
Status: Should FAIL for affected cases
```

#### Test 4.4: PixelCoordinates_ShouldBe_Exact
```csharp
Pixel-level coordinate validation
Checks for 32-pixel offset specifically
Throws exception if detected
Status: Should FAIL with specific error
```

#### Test 4.5: RoundTrip_Connection_ShouldReturn_ToOriginal
```csharp
A→B→A should return to start position
Tests offset accumulation
Expected: 0 accumulated offset
Actual: May accumulate 4-tile offset (2+2)
Status: Should FAIL if offsets accumulate
```

## Test Data Requirements

### Map Data Needed
1. **Map Dimensions**
   - dewford_town: Width × Height
   - route107: Width × Height
   - route114: Width × Height
   - route115: Width × Height

2. **Connection Data**
   - Connection points (tile coordinates)
   - Connection directions (north, south, east, west)
   - Connection widths (tiles)

3. **Warp Event Data**
   - Source positions
   - Destination positions
   - Warp types

### Constants
```csharp
TILE_SIZE_PIXELS = 16
METATILE_SIZE = 2 // 2x2 tiles per metatile
BUGGY_OFFSET_TILES = 2
BUGGY_OFFSET_PIXELS = 32
```

## Root Cause Hypotheses

### Hypothesis 1: Metatile Conversion Error
**Theory**: The conversion from pokeemerald metatiles (2×2 tiles) to individual tiles doesn't properly account for the metatile structure.

**Evidence**:
- Offset is exactly 2 tiles (one metatile dimension)
- Bug appears in vertical connections (Y-axis)

**Test Validation**: MinimalReproductionTests.ConnectionOffset_Calculation_ShouldBe_Correct

### Hypothesis 2: Border Tile Exclusion
**Theory**: Border tiles (used in pokeemerald for map edges) are not being factored into coordinate calculations.

**Evidence**:
- Pokemon Emerald maps have 1-metatile border (2 tiles)
- Offset matches border size

**Test Validation**: BoundaryAlignmentTests.NorthSouthBoundaries_ShouldAlign_Perfectly

### Hypothesis 3: Coordinate System Mismatch
**Theory**: Different coordinate origin points between pokeemerald and Tiled formats.

**Evidence**:
- Offset is systematic and consistent
- Affects specific directions

**Test Validation**: ExpectedVsActualTests.PixelCoordinates_ShouldBe_Exact

### Hypothesis 4: Off-By-One in Height Calculation
**Theory**: Map height calculation is off by exactly 2 tiles.

**Evidence**:
- Both instances involve vertical (Y-axis) movement
- Offset magnitude is constant

**Test Validation**: MinimalReproductionTests.SimpleNorthConnection_ShouldNotHave_Offset

## Expected Test Results Timeline

### Phase 1: Initial Run (Before Fix)
```
Expected: 21/21 tests FAIL
Actual:   (TBD after implementation)
Reason:   Tests designed to detect bug
```

### Phase 2: After Coordinate Fix
```
Expected: 21/21 tests PASS
Actual:   (TBD after fix)
Reason:   Bug resolved, offsets eliminated
```

### Phase 3: Regression Testing
```
Expected: 21/21 tests PASS (ongoing)
Reason:   Prevent reintroduction of bug
```

## Success Criteria

### Test Success
- ✅ All 21 tests pass
- ✅ Zero coordinate offset in any direction
- ✅ Perfect boundary alignment
- ✅ Seamless visual transitions
- ✅ Symmetric bidirectional connections

### Fix Validation
- ✅ dewford_town → route107: Player at (7, 14)
- ✅ route114 → route115: Player at (10, 0)
- ✅ No visual "jump" during transitions
- ✅ Map tiles align perfectly at boundaries
- ✅ Round-trip returns to original position

## Implementation Notes

### Priority Order
1. **HIGH**: Implement map data retrieval functions
2. **HIGH**: Implement coordinate calculation logic
3. **HIGH**: Implement warp simulation
4. **MEDIUM**: Implement boundary validation
5. **LOW**: Implement visual transition simulation

### Integration Points
- Map data source: Tiled JSON files or PokeSharp database
- Coordinate system: Tile-based (16px per tile)
- Warp system: Integration with existing warp logic

### Testing Strategy
1. Implement one helper method at a time
2. Run subset of tests after each implementation
3. Debug failures with detailed logging
4. Iterate until all tests pass

## Swarm Coordination

**Memory Key**: `hive/tester/test_cases`

**Stored Data**:
- Test file locations
- Expected test results
- Bug reproduction steps
- Root cause hypotheses

**Next Agent**: Coder
**Required Input**: Test specifications, root cause analysis
**Expected Output**: Fixed coordinate calculation logic

## Maintenance

### Version Control
- Track test changes in git
- Document any test modifications
- Update success criteria as needed

### Continuous Integration
- Run tests on every commit
- Alert on new failures
- Generate coverage reports

### Documentation Updates
- Keep README.md synchronized
- Update root cause hypotheses as evidence emerges
- Document any new test cases added
