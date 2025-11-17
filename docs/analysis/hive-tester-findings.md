# Hive Mind Tester Analysis: Tile Behavior Roslyn Integration Testing Requirements

**Agent**: Tester
**Mission**: Identify testing gaps and validation requirements for Roslyn-based tile behavior system
**Date**: 2025-11-16
**Status**: Critical Testing Gaps Identified

---

## Executive Summary

The Roslyn integration for tile behaviors introduces **significant testing complexity** that is currently **not addressed** in the existing test suite. The migration from hardcoded `TileLedge` components to dynamic Roslyn scripts creates **245 unique behavior scenarios** that must be validated for correctness, performance, and integration.

**Critical Finding**: Current test coverage is **insufficient** for a production migration. The existing `MovementSystemTests.cs` validates movement logic but **does not test**:
- Script compilation correctness
- Tile behavior script execution
- Integration between scripts and collision system
- All 245 Pokemon Emerald behaviors
- Edge cases (multi-entity, elevation, concurrent access)
- Performance under script interpretation overhead

---

## 1. Required Test Coverage

### 1.1 Current Test Coverage Analysis

**Existing Tests** (`MovementSystemTests.cs`):
- ✅ Movement system query optimization
- ✅ Animation state management
- ✅ Collision checking (mocked)
- ✅ Ledge jumping logic (hardcoded `TileLedge`)
- ✅ Direction mapping and caching
- ✅ Tile size caching
- ✅ Component pooling (MovementRequest)

**Coverage Metrics**:
- Lines: ~85% (movement system only)
- Scenarios: ~12 test cases
- Edge Cases: Minimal (boundary checks, null handling)

### 1.2 Missing Test Coverage for Roslyn Integration

**Untested Areas** (0% coverage):
1. ❌ **Tile behavior script compilation**
2. ❌ **Script execution and lifecycle**
3. ❌ **Behavior-to-collision integration**
4. ❌ **All 245 Pokemon Emerald behaviors**
5. ❌ **Script error handling and recovery**
6. ❌ **Performance under script interpretation**
7. ❌ **Memory allocation from script execution**
8. ❌ **Concurrent script access**
9. ❌ **Elevation-based collision filtering**
10. ❌ **Multi-entity collision scenarios**

---

## 2. Test Cases Needed for Tile Behavior Scripts

### 2.1 Script Compilation Tests

**Purpose**: Validate Roslyn compilation pipeline for tile behavior scripts

**Test Suite**: `TileBehaviorCompilationTests.cs`

```csharp
public class TileBehaviorCompilationTests
{
    [Fact]
    public void CompileScript_ValidJumpSouthBehavior_ShouldSucceed()
    {
        // Arrange: Valid .csx script
        var scriptPath = "tiles/jump_south.csx";

        // Act: Compile script via TypeRegistry
        var result = _behaviorRegistry.LoadBehavior("jump_south", scriptPath);

        // Assert: Compilation succeeds
        result.Should().NotBeNull();
        result.Should().BeAssignableTo<TileBehaviorScriptBase>();
    }

    [Fact]
    public void CompileScript_SyntaxError_ShouldFailWithDiagnostics()
    {
        // Arrange: Script with syntax error
        var scriptSource = "public class Invalid { missing_brace";

        // Act: Attempt compilation
        var exception = Assert.Throws<ScriptCompilationException>(
            () => _scriptService.Compile<TileBehaviorScriptBase>(scriptSource)
        );

        // Assert: Detailed error diagnostics
        exception.Diagnostics.Should().ContainSingle();
        exception.Diagnostics[0].Should().Contain("missing closing brace");
    }

    [Fact]
    public void CompileScript_MissingBaseClass_ShouldFailValidation()
    {
        // Arrange: Script not inheriting TileBehaviorScriptBase
        var scriptSource = "public class NotABehavior { }";

        // Act & Assert
        Assert.Throws<InvalidScriptTypeException>(
            () => _scriptService.Compile<TileBehaviorScriptBase>(scriptSource)
        );
    }

    [Fact]
    public void ScriptCache_ShouldReuseCompiledScripts()
    {
        // Arrange: Load same script twice
        var script1 = _behaviorRegistry.GetScript("jump_south");
        var script2 = _behaviorRegistry.GetScript("jump_south");

        // Assert: Same instance (cached)
        ReferenceEquals(script1, script2).Should().BeTrue();
    }
}
```

**Coverage Target**: 100% of compilation paths (success, syntax errors, type errors)

---

### 2.2 Script Execution Tests

**Purpose**: Validate behavior script lifecycle and hook execution

**Test Suite**: `TileBehaviorExecutionTests.cs`

```csharp
public class TileBehaviorExecutionTests
{
    [Fact]
    public void IsBlockedFrom_JumpSouthLedge_ShouldBlockNorthMovement()
    {
        // Arrange: Jump south ledge script
        var script = CreateJumpSouthScript();
        var context = CreateScriptContext();

        // Act: Check if north movement is blocked
        var blocked = script.IsBlockedFrom(context, Direction.North, Direction.South);

        // Assert: North movement should be blocked
        blocked.Should().BeTrue();
    }

    [Fact]
    public void GetJumpDirection_JumpSouthLedge_ShouldReturnSouth()
    {
        // Arrange
        var script = CreateJumpSouthScript();
        var context = CreateScriptContext();

        // Act
        var jumpDir = script.GetJumpDirection(context, Direction.North);

        // Assert
        jumpDir.Should().Be(Direction.South);
    }

    [Fact]
    public void GetForcedMovement_IceTile_ShouldContinueSliding()
    {
        // Arrange: Ice behavior script
        var script = CreateIceScript();
        var context = CreateScriptContext();

        // Act: Player moving east on ice
        var forcedDir = script.GetForcedMovement(context, Direction.East);

        // Assert: Should continue east (sliding)
        forcedDir.Should().Be(Direction.East);
    }

    [Fact]
    public void AllowsRunning_HotSprings_ShouldDisableRunning()
    {
        // Arrange
        var script = CreateHotSpringsScript();
        var context = CreateScriptContext();

        // Act
        var canRun = script.AllowsRunning(context);

        // Assert
        canRun.Should().BeFalse();
    }

    [Fact]
    public void OnStep_CrackedFloor_ShouldTriggerAfterDelay()
    {
        // Arrange
        var script = CreateCrackedFloorScript();
        var context = CreateScriptContext();
        var entity = CreatePlayerEntity();

        // Act: Step on cracked floor 3 times
        script.OnStep(context, entity);
        script.OnStep(context, entity);
        script.OnStep(context, entity);

        // Assert: Floor should break (warp triggered)
        var warpTriggered = context.State.Get<bool>("warp_triggered");
        warpTriggered.Should().BeTrue();
    }
}
```

**Coverage Target**: 100% of TileBehaviorScriptBase virtual methods

---

### 2.3 Integration Tests: Scripts + Collision System

**Purpose**: Validate that TileBehaviorSystem correctly integrates with CollisionService

**Test Suite**: `TileBehaviorCollisionIntegrationTests.cs`

```csharp
public class TileBehaviorCollisionIntegrationTests
{
    [Fact]
    public void IsPositionWalkable_WithJumpSouthLedge_ShouldBlockFromNorth()
    {
        // Arrange: Map with jump south ledge at (5, 5)
        var world = CreateWorldWithLedge(5, 5, Direction.South);

        // Act: Check if walkable from north
        var walkable = _collisionService.IsPositionWalkable(
            mapId: 1,
            tileX: 5,
            tileY: 5,
            fromDirection: Direction.North,
            entityElevation: Elevation.Default
        );

        // Assert: Should be blocked
        walkable.Should().BeFalse();
    }

    [Fact]
    public void GetTileCollisionInfo_WithLedge_ShouldReturnAllInfoInOneQuery()
    {
        // Arrange: Ledge tile
        var world = CreateWorldWithLedge(5, 5, Direction.South);

        // Act: Get collision info (SINGLE query)
        var (isLedge, jumpDir, walkable) = _collisionService.GetTileCollisionInfo(
            mapId: 1,
            tileX: 5,
            tileY: 5,
            entityElevation: Elevation.Default,
            fromDirection: Direction.North
        );

        // Assert
        isLedge.Should().BeTrue();
        jumpDir.Should().Be(Direction.South);
        walkable.Should().BeFalse(); // Blocked from north
    }

    [Fact]
    public void MovementSystem_WithForcedMovementTile_ShouldAutoSlide()
    {
        // Arrange: Ice tile at (5, 5)
        var world = CreateWorldWithIceTile(5, 5);
        var player = CreatePlayerAt(5, 5);

        // Act: Request movement east
        RequestMovement(player, Direction.East);
        _movementSystem.Update(world, 0.016f);

        // Assert: Player should slide multiple tiles
        var position = world.Get<Position>(player);
        position.X.Should().BeGreaterThan(5); // Slid past ice
    }

    [Fact]
    public void TileBehaviorSystem_ScriptError_ShouldNotCrashCollisionSystem()
    {
        // Arrange: Behavior script that throws exception
        var world = CreateWorldWithFaultyBehavior(5, 5);

        // Act: Check collision (should isolate error)
        var action = () => _collisionService.IsPositionWalkable(1, 5, 5);

        // Assert: Should not throw (error isolated)
        action.Should().NotThrow();
    }
}
```

**Coverage Target**: 100% of integration points between systems

---

## 3. Testing All 245 Pokemon Emerald Behaviors

### 3.1 Behavior Test Matrix

**Challenge**: 245 unique behaviors to validate

**Strategy**: Parameterized tests with behavior categories

**Test Suite**: `AllPokemonEmeraldBehaviorsTests.cs`

```csharp
[Theory]
[InlineData("MB_JUMP_SOUTH", Direction.South, Direction.North, true)]
[InlineData("MB_JUMP_NORTH", Direction.North, Direction.South, true)]
[InlineData("MB_JUMP_EAST", Direction.East, Direction.West, true)]
[InlineData("MB_JUMP_WEST", Direction.West, Direction.East, true)]
// ... all 8 jump directions
public void JumpBehaviors_ShouldBlockOppositeDirection(
    string behaviorId,
    Direction jumpDir,
    Direction blockedDir,
    bool shouldBlock
)
{
    // Arrange: Create behavior from ID
    var script = _behaviorRegistry.GetScript(behaviorId);
    var context = CreateContext();

    // Act: Check if blocked
    var blocked = script.IsBlockedFrom(context, blockedDir, jumpDir);

    // Assert
    blocked.Should().Be(shouldBlock);
}

[Theory]
[InlineData("MB_IMPASSABLE_EAST", Direction.East, true)]
[InlineData("MB_IMPASSABLE_WEST", Direction.West, true)]
[InlineData("MB_IMPASSABLE_NORTH", Direction.North, true)]
[InlineData("MB_IMPASSABLE_SOUTH", Direction.South, true)]
// ... all directional blocking behaviors
public void ImpassableBehaviors_ShouldBlockCorrectDirection(
    string behaviorId,
    Direction blockedDir,
    bool shouldBlock
)
{
    // Test directional blocking
}

[Theory]
[InlineData("MB_ICE", Direction.East, Direction.East)]
[InlineData("MB_EASTWARD_CURRENT", Direction.None, Direction.East)]
[InlineData("MB_WESTWARD_CURRENT", Direction.None, Direction.West)]
// ... all forced movement behaviors
public void ForcedMovementBehaviors_ShouldForceCorrectDirection(
    string behaviorId,
    Direction currentDir,
    Direction expectedForcedDir
)
{
    // Test forced movement
}
```

**Test Data**: CSV file with all 245 behaviors + expected outcomes

```csv
BehaviorID,Category,ExpectedBlockedDirs,ExpectedForcedDir,AllowsRunning
MB_NORMAL,basic,None,None,true
MB_TALL_GRASS,encounter,None,None,true
MB_LONG_GRASS,encounter,None,None,false
MB_ICE,forced,None,Current,false
MB_JUMP_SOUTH,jump,North,South,true
... (245 rows)
```

**Coverage Target**: 100% of all 245 behaviors

---

### 3.2 Behavior Category Tests

**Categories** (from research docs):
1. Basic Terrain (42 behaviors)
2. Collision/Impassable (10 behaviors)
3. Jump Tiles (8 behaviors)
4. Forced Movement (14 behaviors)
5. Doors and Warps (20 behaviors)
6. Bridges (14 behaviors)
7. Interactive Objects (13 behaviors)
8. Secret Base (40 behaviors)
9. Special Terrain (7 behaviors)
10. Bookshelves/Furniture (7 behaviors)

**Test per Category**: Dedicated test class with category-specific assertions

---

## 4. Edge Case Testing Requirements

### 4.1 Multi-Entity Collision Tests

**Scenario**: Multiple entities on same tile with different elevations

```csharp
[Fact]
public void Collision_MultipleEntities_DifferentElevations_ShouldFilterCorrectly()
{
    // Arrange: 3 entities on (5, 5)
    // - Entity A: Elevation 0 (ground)
    // - Entity B: Elevation 1 (bridge)
    // - Entity C: Elevation 2 (flying)
    var world = CreateWorld();
    CreateEntityAt(5, 5, elevation: 0); // Ground blocker
    CreateEntityAt(5, 5, elevation: 1); // Bridge blocker

    // Act: Check if walkable for elevation 0 entity
    var walkableGround = _collisionService.IsPositionWalkable(
        1, 5, 5, Direction.None, entityElevation: 0
    );

    // Act: Check if walkable for elevation 1 entity
    var walkableBridge = _collisionService.IsPositionWalkable(
        1, 5, 5, Direction.None, entityElevation: 1
    );

    // Assert: Ground blocked, bridge blocked, different elevations
    walkableGround.Should().BeFalse();
    walkableBridge.Should().BeFalse();
}
```

### 4.2 Elevation Mismatch Tests

**Pokemon Emerald Behavior**: Elevation must match for collision

```csharp
[Fact]
public void Collision_ElevationMismatch_ShouldAllowPassThrough()
{
    // Arrange: Ground entity (elevation 0) tries to move to bridge tile (elevation 1)
    var world = CreateWorld();
    var player = CreatePlayerAt(5, 5, elevation: 0);
    CreateBlockerAt(6, 5, elevation: 1); // Bridge blocker

    // Act: Try to move east (different elevation)
    var walkable = _collisionService.IsPositionWalkable(
        1, 6, 5, Direction.East, entityElevation: 0
    );

    // Assert: Should be walkable (different elevation)
    walkable.Should().BeTrue();
}
```

### 4.3 Forced Movement Edge Cases

**Scenario**: Forced movement into blocked tile

```csharp
[Fact]
public void ForcedMovement_Ice_SlidingIntoWall_ShouldStopAtWall()
{
    // Arrange: Ice tile -> Ice tile -> Wall
    var world = CreateWorld();
    CreateIceTileAt(5, 5);
    CreateIceTileAt(6, 5);
    CreateWallAt(7, 5);
    var player = CreatePlayerAt(5, 5);

    // Act: Request movement east (start sliding)
    RequestMovement(player, Direction.East);
    for (int i = 0; i < 10; i++) // 10 frames
        _movementSystem.Update(world, 0.016f);

    // Assert: Should stop at (6, 5), not slide through wall
    var position = world.Get<Position>(player);
    position.X.Should().Be(6);
    position.Y.Should().Be(5);
}
```

### 4.4 Ledge Jumping Edge Cases

**Scenario**: Jump landing blocked

```csharp
[Fact]
public void LedgeJump_LandingBlocked_ShouldPreventJump()
{
    // Arrange: Ledge at (5, 5), landing at (5, 7) blocked
    var world = CreateWorld();
    CreateJumpSouthLedgeAt(5, 5); // Jump to (5, 7)
    CreateBlockerAt(5, 7); // Block landing
    var player = CreatePlayerAt(5, 4);

    // Act: Try to jump
    RequestMovement(player, Direction.South);
    _movementSystem.Update(world, 0.016f);

    // Assert: Jump should fail, player stays at (5, 4)
    var position = world.Get<Position>(player);
    position.X.Should().Be(5);
    position.Y.Should().Be(4);
}

[Fact]
public void LedgeJump_OutOfBounds_ShouldPreventJump()
{
    // Arrange: Ledge at map edge
    var world = CreateWorld(width: 10, height: 10);
    CreateJumpSouthLedgeAt(5, 9); // Landing would be (5, 11) = out of bounds
    var player = CreatePlayerAt(5, 8);

    // Act: Try to jump
    RequestMovement(player, Direction.South);
    _movementSystem.Update(world, 0.016f);

    // Assert: Jump prevented
    var position = world.Get<Position>(player);
    position.Y.Should().Be(8); // Didn't move
}
```

---

## 5. Script Compilation Error Testing

### 5.1 Compilation Error Scenarios

**Test Suite**: `TileBehaviorCompilationErrorTests.cs`

```csharp
[Fact]
public void ScriptCompilation_MissingSemicolon_ShouldProvideDetailedError()
{
    // Arrange: Script with missing semicolon
    var source = @"
        public class JumpSouth : TileBehaviorScriptBase
        {
            public override Direction GetJumpDirection(ScriptContext ctx, Direction from)
            {
                return Direction.South // <-- missing semicolon
            }
        }";

    // Act
    var exception = Assert.Throws<ScriptCompilationException>(
        () => _scriptService.Compile<TileBehaviorScriptBase>(source)
    );

    // Assert: Error message should be helpful
    exception.Message.Should().Contain("expected ';'");
    exception.Line.Should().Be(6);
    exception.Column.Should().BeGreaterThan(0);
}

[Fact]
public void ScriptCompilation_UndefinedType_ShouldListAvailableTypes()
{
    // Arrange: Script referencing non-existent type
    var source = @"
        public class Invalid : TileBehaviorScriptBase
        {
            public override bool IsBlockedFrom(ScriptContext ctx, Direction from, Direction to)
            {
                var undefined = new UndefinedType(); // Error
                return false;
            }
        }";

    // Act
    var exception = Assert.Throws<ScriptCompilationException>(
        () => _scriptService.Compile<TileBehaviorScriptBase>(source)
    );

    // Assert: Should suggest valid types
    exception.Message.Should().Contain("UndefinedType");
    exception.Diagnostics.Should().Contain(d => d.Contains("available types"));
}

[Fact]
public void ScriptCompilation_WrongReturnType_ShouldFailValidation()
{
    // Arrange: Method returns wrong type
    var source = @"
        public class Invalid : TileBehaviorScriptBase
        {
            public override Direction GetJumpDirection(ScriptContext ctx, Direction from)
            {
                return ""not a direction""; // Error: string instead of Direction
            }
        }";

    // Act & Assert
    Assert.Throws<ScriptCompilationException>(
        () => _scriptService.Compile<TileBehaviorScriptBase>(source)
    );
}
```

**Coverage Target**: All common C# compilation errors

---

### 5.2 Runtime Error Tests

**Scenario**: Script throws exception during execution

```csharp
[Fact]
public void ScriptExecution_ThrowsException_ShouldIsolateError()
{
    // Arrange: Behavior that throws
    var faultyScript = CreateScriptThatThrows();
    var world = CreateWorld();
    var tileEntity = CreateTileWithBehavior(5, 5, faultyScript);

    // Act: Execute behavior (via TileBehaviorSystem)
    var action = () => _behaviorSystem.Update(world, 0.016f);

    // Assert: Should not crash system
    action.Should().NotThrow();

    // Behavior should be deactivated
    var behavior = world.Get<TileBehavior>(tileEntity);
    behavior.IsActive.Should().BeFalse();
}

[Fact]
public void ScriptExecution_InfiniteLoop_ShouldTimeout()
{
    // Arrange: Script with infinite loop
    var loopingScript = CreateInfiniteLoopScript();
    var world = CreateWorld();
    CreateTileWithBehavior(5, 5, loopingScript);

    // Act: Execute with timeout
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
    var action = () => _behaviorSystem.Update(world, 0.016f, cts.Token);

    // Assert: Should timeout and deactivate
    Assert.Throws<TimeoutException>(action);
}
```

---

## 6. Regression Testing After Migration

### 6.1 Migration Validation Strategy

**Goal**: Ensure old `TileLedge` behavior matches new `TileBehavior` scripts

**Test Suite**: `TileBehaviorMigrationRegressionTests.cs`

```csharp
public class TileBehaviorMigrationRegressionTests
{
    // Baseline: Old TileLedge behavior
    private readonly TileLedge _oldLedge = new(Direction.South);

    // New: Roslyn script behavior
    private readonly TileBehaviorScriptBase _newBehavior = LoadScript("jump_south");

    [Theory]
    [InlineData(Direction.North, true)]  // Should block
    [InlineData(Direction.South, false)] // Should allow
    [InlineData(Direction.East, false)]  // Should allow
    [InlineData(Direction.West, false)]  // Should allow
    public void NewBehavior_ShouldMatchOldLedge_ForAllDirections(
        Direction fromDir,
        bool shouldBlock
    )
    {
        // Arrange
        var context = CreateContext();

        // Act: Old behavior
        var oldBlocked = _oldLedge.IsBlockedFrom(fromDir);

        // Act: New behavior
        var newBlocked = _newBehavior.IsBlockedFrom(context, fromDir, Direction.South);

        // Assert: Must match
        newBlocked.Should().Be(oldBlocked);
        newBlocked.Should().Be(shouldBlock);
    }

    [Fact]
    public void MigrationTest_AllExistingMaps_ShouldHaveCorrectBehaviors()
    {
        // Arrange: Load all maps from game data
        var maps = LoadAllMaps();

        foreach (var map in maps)
        {
            // Act: Count TileLedge components
            var ledgeCount = CountLedges(map);

            // Act: Count TileBehavior with jump scripts
            var behaviorCount = CountJumpBehaviors(map);

            // Assert: Counts should match (successful migration)
            behaviorCount.Should().Be(ledgeCount,
                $"Map {map.Id} should have same ledge count after migration");
        }
    }
}
```

**Coverage Target**: 100% parity with old system

---

### 6.2 Performance Regression Tests

**Baseline**: Current performance with hardcoded `TileLedge`
**Target**: Roslyn script performance should be **<2x slower**

```csharp
[Fact]
public void PerformanceRegression_CollisionCheck_ShouldBeFasterThan2ms()
{
    // Arrange: 1000 collision checks
    var world = CreateWorldWithMixedBehaviors();

    // Baseline: Old system (hardcoded)
    var baselineTime = MeasureCollisionChecks(world, useOldSystem: true, iterations: 1000);

    // New: Roslyn script system
    var newTime = MeasureCollisionChecks(world, useOldSystem: false, iterations: 1000);

    // Assert: Should be <2x slower than baseline
    newTime.Should().BeLessThan(baselineTime * 2);
    newTime.Should().BeLessThan(TimeSpan.FromMilliseconds(2));
}

[Fact]
public void PerformanceRegression_ScriptCaching_ShouldEliminateCompilationOverhead()
{
    // Arrange: Same behavior called 1000 times
    var script = _behaviorRegistry.GetScript("jump_south");
    var context = CreateContext();

    // Act: Measure 1000 calls
    var sw = Stopwatch.StartNew();
    for (int i = 0; i < 1000; i++)
    {
        script.IsBlockedFrom(context, Direction.North, Direction.South);
    }
    sw.Stop();

    // Assert: Should be <1ms total (cached, no compilation)
    sw.ElapsedMilliseconds.Should().BeLessThan(1);
}
```

---

## 7. Performance Testing Requirements

### 7.1 Script Execution Performance

**Metrics to Track**:
- Script compilation time (first load)
- Script execution time (cached)
- Memory allocation per script call
- GC pressure from script execution

**Test Suite**: `TileBehaviorPerformanceTests.cs`

```csharp
[Fact]
public void ScriptCompilation_FirstLoad_ShouldComplete_Under100ms()
{
    // Arrange: Fresh registry
    var registry = new TypeRegistry<TileBehaviorDefinition>();

    // Act: Compile script
    var sw = Stopwatch.StartNew();
    var script = registry.LoadBehavior("jump_south", "tiles/jump_south.csx");
    sw.Stop();

    // Assert
    sw.ElapsedMilliseconds.Should().BeLessThan(100);
}

[Fact]
public void ScriptExecution_CachedScript_ShouldNotAllocate()
{
    // Arrange: Pre-compiled script
    var script = _behaviorRegistry.GetScript("jump_south");
    var context = CreateContext();

    GC.Collect();
    GC.WaitForPendingFinalizers();
    var memoryBefore = GC.GetTotalMemory(forceFullCollection: true);

    // Act: Execute 1000 times
    for (int i = 0; i < 1000; i++)
    {
        script.IsBlockedFrom(context, Direction.North, Direction.South);
    }

    var memoryAfter = GC.GetTotalMemory(forceFullCollection: false);
    var allocated = memoryAfter - memoryBefore;

    // Assert: Minimal allocation (<10KB for 1000 calls)
    allocated.Should().BeLessThan(10 * 1024);
}

[Fact]
public void TileBehaviorSystem_100Behaviors_ShouldUpdate_Under16ms()
{
    // Arrange: 100 tiles with behaviors
    var world = CreateWorldWith100Behaviors();

    // Act: Update system (60 FPS target = 16ms budget)
    var sw = Stopwatch.StartNew();
    _behaviorSystem.Update(world, 0.016f);
    sw.Stop();

    // Assert: Within frame budget
    sw.ElapsedMilliseconds.Should().BeLessThan(16);
}
```

**Coverage Target**: All performance-critical paths

---

### 7.2 Memory Allocation Tests

**Goal**: Ensure no memory leaks from script execution

```csharp
[Fact]
public void ScriptCache_ShouldNotLeak_Over1000Loads()
{
    // Arrange
    var registry = new TypeRegistry<TileBehaviorDefinition>();
    var memoryBefore = GC.GetTotalMemory(forceFullCollection: true);

    // Act: Load and unload behaviors 1000 times
    for (int i = 0; i < 1000; i++)
    {
        var script = registry.LoadBehavior("test", "tiles/test.csx");
        registry.Unload("test");

        if (i % 100 == 0)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }

    GC.Collect();
    GC.WaitForPendingFinalizers();
    var memoryAfter = GC.GetTotalMemory(forceFullCollection: true);
    var leaked = memoryAfter - memoryBefore;

    // Assert: Minimal memory growth (<1MB for 1000 iterations)
    leaked.Should().BeLessThan(1 * 1024 * 1024);
}
```

---

## 8. Critical Testing Gaps Summary

### 8.1 High Priority (Blockers for Production)

1. ❌ **All 245 behavior validation** - Currently **0/245 tested**
2. ❌ **Script compilation error handling** - No tests for syntax/type errors
3. ❌ **Integration with CollisionService** - Scripts not tested with collision system
4. ❌ **Performance regression** - No baseline comparison with old system
5. ❌ **Multi-entity collision** - Edge cases not covered

### 8.2 Medium Priority (Should Have)

6. ❌ **Elevation filtering** - Not tested with different elevations
7. ❌ **Forced movement chains** - Ice -> Ice -> Wall scenarios
8. ❌ **Script error isolation** - Runtime exception handling
9. ❌ **Memory leak prevention** - Long-running script execution
10. ❌ **Concurrent access** - Multiple threads accessing same script

### 8.3 Low Priority (Nice to Have)

11. ❌ **Script hot-reloading** - Runtime script updates
12. ❌ **Custom behavior modding** - User-created behaviors
13. ❌ **Behavior debugging tools** - Script breakpoints, logging

---

## 9. Recommended Testing Strategy

### 9.1 Phase 1: Foundation Tests (Week 1)

**Goal**: Validate core script compilation and execution

**Deliverables**:
- `TileBehaviorCompilationTests.cs` (20 tests)
- `TileBehaviorExecutionTests.cs` (30 tests)
- `TileBehaviorScriptBaseTests.cs` (15 tests)

**Coverage Target**: 80% of TileBehaviorScriptBase methods

---

### 9.2 Phase 2: Integration Tests (Week 2)

**Goal**: Validate system integration

**Deliverables**:
- `TileBehaviorCollisionIntegrationTests.cs` (25 tests)
- `TileBehaviorMovementIntegrationTests.cs` (20 tests)
- `TileBehaviorSystemTests.cs` (15 tests)

**Coverage Target**: 90% of integration points

---

### 9.3 Phase 3: Comprehensive Behavior Tests (Week 3-4)

**Goal**: Test all 245 Pokemon Emerald behaviors

**Deliverables**:
- `AllPokemonEmeraldBehaviorsTests.cs` (245 parameterized tests)
- `BehaviorCategoryTests.cs` (10 test classes, one per category)
- Test data CSV with expected outcomes

**Coverage Target**: 100% of all 245 behaviors

---

### 9.4 Phase 4: Edge Cases and Performance (Week 5)

**Goal**: Validate edge cases and performance

**Deliverables**:
- `TileBehaviorEdgeCaseTests.cs` (30 tests)
- `TileBehaviorPerformanceTests.cs` (20 tests)
- `TileBehaviorMemoryTests.cs` (10 tests)

**Coverage Target**: 95% of edge case scenarios

---

### 9.5 Phase 5: Regression and Migration (Week 6)

**Goal**: Ensure migration correctness

**Deliverables**:
- `TileBehaviorMigrationRegressionTests.cs` (40 tests)
- `OldVsNewBehaviorParityTests.cs` (50 tests)
- Migration validation report

**Coverage Target**: 100% parity with old system

---

## 10. Test Infrastructure Requirements

### 10.1 Test Fixtures and Helpers

**Required Utilities**:

```csharp
public class TileBehaviorTestFixture
{
    // Script compilation helpers
    public TileBehaviorScriptBase CompileScript(string source);
    public TileBehaviorScriptBase LoadScriptFromFile(string path);

    // World creation helpers
    public World CreateWorldWithBehavior(int x, int y, string behaviorId);
    public World CreateWorldWithLedge(int x, int y, Direction jumpDir);
    public World CreateWorldWithIceTile(int x, int y);

    // Entity creation helpers
    public Entity CreatePlayerAt(int x, int y, byte elevation = 0);
    public Entity CreateBlockerAt(int x, int y, byte elevation = 0);
    public Entity CreateTileWithBehavior(int x, int y, TileBehaviorScriptBase script);

    // Assertion helpers
    public void AssertBehaviorBlocksDirection(string behaviorId, Direction blocked);
    public void AssertBehaviorAllowsDirection(string behaviorId, Direction allowed);
    public void AssertForcedMovement(string behaviorId, Direction expected);
}
```

---

### 10.2 Test Data Management

**Required Assets**:
1. **Behavior Scripts Directory**: `/tests/Assets/TileBehaviors/`
   - All 245 Pokemon Emerald behavior scripts
   - Test-specific error scripts (syntax errors, runtime errors)

2. **Test Maps**: `/tests/Assets/Maps/`
   - Small test maps (10x10) with specific behaviors
   - Large stress test maps (100x100) with all behaviors

3. **Expected Outcomes CSV**: `/tests/Data/behavior_test_data.csv`
   - All 245 behaviors with expected outcomes
   - Used for parameterized tests

---

### 10.3 Performance Monitoring

**Metrics to Track**:
- Script compilation time (per behavior)
- Script execution time (per call)
- Memory allocation (per system update)
- GC pressure (Gen0/Gen1/Gen2 collections)
- Frame time impact (total system overhead)

**Tools**:
- BenchmarkDotNet for micro-benchmarks
- Custom performance harness for system-level tests
- Memory profiler integration (dotMemory, PerfView)

---

## 11. Success Criteria

### 11.1 Test Coverage Metrics

**Required**:
- Line Coverage: >90%
- Branch Coverage: >85%
- Behavior Coverage: 100% (all 245 behaviors)
- Integration Coverage: 100% (all system integration points)

### 11.2 Performance Benchmarks

**Required**:
- Script compilation: <100ms per behavior
- Script execution: <0.01ms per call (cached)
- System update: <2ms for 100 behaviors
- Memory allocation: <10KB per 1000 script calls
- **No performance regression** >2x vs old system

### 11.3 Quality Metrics

**Required**:
- Zero script compilation errors in production scripts
- Zero runtime exceptions in behavior execution
- Zero memory leaks over 1000+ game loops
- 100% parity with old `TileLedge` behavior

---

## 12. Risks and Mitigation

### 12.1 Testing Risks

| Risk | Impact | Likelihood | Mitigation |
|------|--------|------------|------------|
| **245 behaviors too complex to test** | High | Medium | Use parameterized tests with CSV data |
| **Roslyn compilation too slow for tests** | Medium | Low | Pre-compile scripts in test setup |
| **Edge cases missed in testing** | High | High | Systematic category-based testing |
| **Performance regression not caught** | High | Medium | Baseline benchmarks before migration |
| **Memory leaks in long-running tests** | Medium | Medium | Automated memory profiling in CI |

---

### 12.2 Migration Risks

| Risk | Impact | Likelihood | Mitigation |
|------|--------|------------|------------|
| **Scripts not functionally equivalent to hardcoded logic** | Critical | High | Regression tests comparing old vs new |
| **Script errors crash game** | Critical | Medium | Error isolation and fallback behavior |
| **Performance too slow for real-time** | High | Medium | Performance benchmarks before production |
| **Breaking changes to existing maps** | High | Medium | Migration validation on all maps |

---

## 13. Recommendations

### 13.1 Immediate Actions (This Sprint)

1. ✅ **Create test infrastructure** (fixtures, helpers, test data)
2. ✅ **Implement Phase 1 tests** (compilation and execution)
3. ✅ **Set up performance baseline** (benchmark old system)
4. ✅ **Create behavior test data CSV** (all 245 behaviors)

### 13.2 Short-Term Actions (Next Sprint)

5. ✅ **Implement Phase 2 tests** (integration with collision/movement)
6. ✅ **Implement Phase 3 tests** (all 245 behaviors)
7. ✅ **Set up CI/CD integration** (automated testing)
8. ✅ **Create migration validation tool** (compare old vs new)

### 13.3 Long-Term Actions (Post-Migration)

9. ✅ **Continuous performance monitoring** (track regressions)
10. ✅ **Memory leak detection** (automated profiling)
11. ✅ **Behavior debugging tools** (script breakpoints, logging)
12. ✅ **Modding support validation** (user-created behaviors)

---

## 14. Conclusion

The Roslyn integration for tile behaviors is a **major architectural change** that **significantly increases testing complexity**. The current test suite is **woefully inadequate** for a production migration.

**Critical Findings**:
- ❌ **0% coverage** of script compilation and execution
- ❌ **0/245 behaviors** validated
- ❌ **No integration tests** for scripts + collision system
- ❌ **No performance baseline** for regression detection
- ❌ **No edge case coverage** (multi-entity, elevation, forced movement chains)

**Recommendation**: **DO NOT PROCEED** with production migration until **Phases 1-5 testing** is complete (6 weeks minimum).

**Risk Level**: **CRITICAL** - Production deployment without comprehensive testing could result in:
- Game-breaking collision bugs
- Performance degradation (>2x slower)
- Memory leaks from script execution
- Functional regressions breaking existing maps
- Player-facing crashes from script errors

**Path Forward**:
1. Implement systematic testing strategy (Phases 1-5)
2. Achieve 100% behavior coverage (all 245 behaviors)
3. Validate performance benchmarks (<2x regression)
4. Complete migration validation (100% parity)
5. **THEN** migrate to production

---

**Agent**: Tester
**Status**: Analysis Complete
**Next Agent**: Architect (for test infrastructure design)
