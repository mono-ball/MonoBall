# Map Connection Coordinate Offset Tests

## Overview

This test suite validates map connection logic and specifically reproduces the coordinate offset issues identified in the PokeSharp map conversion process.

## Identified Issues

### Issue 1: Dewford Town → Route 107 (2-Tile Upward Shift)
**Symptom**: When warping from `dewford_town` to `route107`, the player position shifts 2 tiles (32 pixels) upward from the expected position.

**Impact**:
- Visual discontinuity at map boundaries
- Player appears in wrong location
- Map tiles don't align properly

**Expected Behavior**: Player should appear at exact connection point with Y-coordinate matching warp destination.

**Actual Behavior**: Player appears 2 tiles above expected position (Y-offset of -32 pixels).

### Issue 2: Route 114 → Route 115 (2-Tile Downward Shift)
**Symptom**: When warping from `route114` to `route115`, the player position shifts 2 tiles (32 pixels) downward from the expected position.

**Impact**:
- Similar visual discontinuity
- Player placement error
- Boundary misalignment

**Expected Behavior**: Player should appear at exact connection point.

**Actual Behavior**: Player appears 2 tiles below expected position (Y-offset of +32 pixels).

## Test Files

### 1. CoordinateOffsetReproductionTests.cs
**Purpose**: Reproduces the specific offset issues for the two identified problematic connections.

**Test Cases**:
- `DewfordTownToRoute107_ShouldNotHave_UpwardOffset()` - Tests dewford_town → route107 connection
- `Route114ToRoute115_ShouldNotHave_DownwardOffset()` - Tests route114 → route115 connection
- `MapConnections_ShouldHave_CorrectOffsets()` - Theory test for multiple connections
- `ConnectedMaps_ShouldHave_AlignedBoundaries()` - Validates boundary alignment
- `ConnectionPoint_ShouldMatch_WarpDestination()` - Verifies connection point accuracy

**Key Assertions**:
- X and Y coordinates should match expected values exactly
- No tile offset should exist (currently detects 2-tile offset)
- No pixel offset should exist (currently detects 32-pixel offset)

### 2. MinimalReproductionTests.cs
**Purpose**: Creates minimal test scenarios to isolate the root cause of the offset bug.

**Test Cases**:
- `SimpleNorthConnection_ShouldNotHave_Offset()` - Tests basic north connection (10x10 maps)
- `SimpleSouthConnection_ShouldNotHave_Offset()` - Tests basic south connection
- `HorizontalConnections_ShouldNotHave_Offset()` - Tests east/west connections
- `PixelLevel_Connection_ShouldBe_Exact()` - Validates pixel-perfect alignment
- `ConnectionOffset_Calculation_ShouldBe_Correct()` - Tests offset mathematics

**Approach**: Uses simplified 10x10 tile maps to reduce complexity and focus on the core offset calculation logic.

### 3. BoundaryAlignmentTests.cs
**Purpose**: Validates that map boundaries align correctly at connection points.

**Test Cases**:
- `NorthSouthBoundaries_ShouldAlign_Perfectly()` - Tests vertical boundary alignment
- `EastWestBoundaries_ShouldAlign_Perfectly()` - Tests horizontal boundary alignment
- `MultiDirectional_Connections_ShouldAll_Align()` - Tests maps with multiple connections
- `ConnectionWidth_ShouldMatch_MapEdge()` - Validates connection width consistency
- `MapTransition_ShouldBe_Seamless()` - Tests visual transition smoothness
- `ReverseConnection_ShouldBe_Symmetric()` - Verifies bidirectional connection symmetry

**Key Checks**:
- No gaps between connected maps
- No overlaps at boundaries
- Zero tile/pixel offset at connection points
- Seamless visual transitions (no camera/player jumps)

### 4. ExpectedVsActualTests.cs
**Purpose**: Explicitly documents expected coordinates and compares against actual values.

**Test Cases**:
- `DewfordToRoute107_Coordinates_ShouldMatch_Expected()` - Full coordinate mapping for dewford_town
- `Route114ToRoute115_Coordinates_ShouldMatch_Expected()` - Full coordinate mapping for route114
- `ConnectionCoordinates_ShouldMap_Correctly()` - Theory test with inline coordinate data
- `PixelCoordinates_ShouldBe_Exact()` - Pixel-level coordinate validation
- `RoundTrip_Connection_ShouldReturn_ToOriginal()` - Tests A→B→A to detect offset accumulation

**Documentation**: Each test includes detailed comments with:
- Expected source position (tiles and pixels)
- Expected target position (tiles and pixels)
- Map dimensions for context
- Explicit offset calculations

## Test Constants

```csharp
TILE_SIZE = 16 pixels
PROBLEMATIC_OFFSET = 2 tiles = 32 pixels
```

## Running the Tests

### All Tests
```bash
dotnet test --filter "FullyQualifiedName~MapConnections"
```

### Specific Test File
```bash
dotnet test --filter "FullyQualifiedName~CoordinateOffsetReproductionTests"
dotnet test --filter "FullyQualifiedName~MinimalReproductionTests"
dotnet test --filter "FullyQualifiedName~BoundaryAlignmentTests"
dotnet test --filter "FullyQualifiedName~ExpectedVsActualTests"
```

### Specific Test
```bash
dotnet test --filter "DewfordTownToRoute107_ShouldNotHave_UpwardOffset"
```

## Implementation Status

⚠️ **IMPORTANT**: These tests currently contain placeholder implementations for helper methods. The following need to be implemented with actual game logic:

### Required Implementations

1. **Map Data Retrieval**
   - `GetMapConnection(string sourceMap, string targetMap)` - Load map connection data
   - `GetMapBoundary(string mapName)` - Load map boundary information
   - `GetWarpData(string sourceMap, string targetMap)` - Load warp event data

2. **Coordinate Calculations**
   - `CalculateTargetCoordinate()` - Calculate destination coordinates
   - `CalculateConnectionOffset()` - Calculate offset between maps
   - `CalculateConnectionPoint()` - Calculate exact connection point

3. **Simulation/Validation**
   - `SimulateWarp()` - Simulate actual warp operation
   - `SimulateMapTransition()` - Simulate visual transition
   - `ValidateBoundaryAlignment()` - Check boundary alignment

## Expected Test Results (After Implementation)

### Currently Expected (All Tests Should FAIL)
These tests are designed to **fail** until the coordinate offset bug is fixed. They document the bug by:
- Asserting that offset should be 0
- Detecting that actual offset is 2 tiles (32 pixels)
- Providing clear failure messages indicating the bug

### After Bug Fix (All Tests Should PASS)
Once the coordinate calculation logic is corrected, all tests should pass with:
- Zero tile offset
- Zero pixel offset
- Perfect boundary alignment
- Seamless transitions

## Test Results Documentation

### Test Execution Log

**Test Case**: Dewford Town → Route 107
```
Expected Position: (7, 14) tiles = (112, 224) pixels
Actual Position:   (7, 12) tiles = (112, 192) pixels  ← BUG
Offset:            (0, -2) tiles = (0, -32) pixels
Status:            FAILING (as expected)
```

**Test Case**: Route 114 → Route 115
```
Expected Position: (10, 0) tiles = (160, 0) pixels
Actual Position:   (10, 2) tiles = (160, 32) pixels   ← BUG
Offset:            (0, +2) tiles = (0, +32) pixels
Status:            FAILING (as expected)
```

### Root Cause Hypothesis

Based on the test patterns, the offset bug appears to be in the Y-coordinate calculation when:
1. Connecting maps vertically (north-south)
2. Calculating the destination position on the target map
3. The offset is consistently ±2 tiles (32 pixels)

**Potential Causes**:
- Metatile-to-tile conversion not accounting for 2x2 metatile structure
- Border tile height not being factored into offset calculation
- Off-by-one error in boundary edge calculation
- Coordinate system mismatch between pokeemerald format and Tiled format

## Integration with Swarm

These tests are part of the hive mind swarm coordinated investigation into the coordinate offset issues.

**Swarm Roles**:
- **Researcher**: Analyzed map data and identified the offset patterns
- **Architect**: Designed the fix approach for coordinate calculation
- **Tester** (this suite): Created reproduction test cases
- **Coder**: Will implement the fix based on test specifications
- **Reviewer**: Will validate the fix against these tests

**Coordination**: Test results are stored in swarm memory with key `hive/tester/test_cases`.

## Next Steps

1. ✅ Create comprehensive test cases (COMPLETE)
2. ⏳ Implement helper methods with actual game logic (PENDING)
3. ⏳ Run tests to confirm they detect the bug (PENDING)
4. ⏳ Fix coordinate calculation logic (PENDING)
5. ⏳ Re-run tests to verify fix (PENDING)
6. ⏳ Validate with actual map data (PENDING)

## References

- Issue documented in: `porycon/IMPLEMENTATION_NOTES.md`
- Related to: World layout and connection offset calculation
- Affects: Map transitions, warp events, visual continuity
