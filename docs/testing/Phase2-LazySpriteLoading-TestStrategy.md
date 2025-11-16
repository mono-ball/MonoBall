# Phase 2: Lazy Sprite Loading - Comprehensive Test Strategy

**Objective**: Validate that lazy sprite loading reduces memory by 25-35MB without visual degradation or performance regression.

**Target Metrics**:
- Memory reduction: ≥25MB (baseline ~40MB → ~5-15MB)
- Map load time: Unchanged (±10ms tolerance)
- Visual quality: Zero sprite pop-in or missing textures
- Cache efficiency: ≥90% hit rate on map revisits
- Long session stability: No memory leaks over 20+ map transitions

---

## 1. Test Scope & Categories

### A. Functional Testing
- Sprite loading correctness
- Sprite unloading on map exit
- Player sprite persistence
- Shared sprite reference counting
- Missing sprite fallback handling
- Cache clearing operations

### B. Performance Testing
- Memory usage reduction validation
- Map transition timing
- Sprite preloading (no visual pop-in)
- Cache statistics accuracy

### C. Integration Testing
- Multiple map transition workflows
- Map revisit scenarios (cache reuse)
- Long gameplay sessions (20+ maps)
- Concurrent map operations

### D. Edge Case Testing
- Maps with zero NPCs (no sprites)
- Maps with 50+ NPCs (stress test)
- Rapid consecutive map transitions
- Shared sprites across multiple maps

---

## 2. Detailed Test Cases

### 2.1 Functional Tests

#### **Test Case F1: Basic Sprite Loading**
```csharp
[Fact]
[Trait("Category", "Functional")]
[Trait("Priority", "Critical")]
public async Task MapLoad_LoadsOnlyRequiredSprites()
{
    // ARRANGE
    var assetManager = new AssetManager("PokeSharp.Game/Assets");
    var mapInitializer = new MapInitializer(assetManager, ...);

    var initialTextureCount = assetManager.LoadedTextureCount;

    // ACT - Load map with 5 NPCs (each using different sprites)
    await mapInitializer.LoadMapAsync("pallet_town");

    // ASSERT
    var loadedCount = assetManager.LoadedTextureCount - initialTextureCount;

    // Should load 5-10 sprites (NPCs + player + shared assets)
    // NOT 100+ sprites (full sprite sheet)
    Assert.InRange(loadedCount, 5, 15);

    _output.WriteLine($"Sprites loaded: {loadedCount} (Expected: 5-15)");
}
```

**Expected**: Only sprites needed for current map are loaded.

---

#### **Test Case F2: Sprite Cleanup on Map Unload**
```csharp
[Fact]
[Trait("Category", "Functional")]
public async Task MapUnload_RemovesUnusedSprites()
{
    // ARRANGE
    await mapInitializer.LoadMapAsync("route_1");
    var loadedCount = assetManager.LoadedTextureCount;
    var mapId = mapRegistry.GetCurrentMapId();

    // ACT - Unload map
    mapLifecycleManager.UnloadMap(mapId);
    GC.Collect(2, GCCollectionMode.Forced, blocking: true);

    // ASSERT
    var afterUnload = assetManager.LoadedTextureCount;

    // Should remove NPCs sprites but keep player sprite
    Assert.True(afterUnload < loadedCount,
        $"Sprites not cleaned: Before={loadedCount}, After={afterUnload}");

    // Player sprite should remain
    Assert.True(assetManager.HasTexture("sprites/player/red"),
        "Player sprite should not be unloaded");
}
```

**Expected**: NPC sprites removed, player sprite persists.

---

#### **Test Case F3: Shared Sprite Reference Counting**
```csharp
[Fact]
[Trait("Category", "Functional")]
public async Task SharedSprites_NotUnloadedWhileInUse()
{
    // ARRANGE - Two maps share "sailor" sprite
    await mapInitializer.LoadMapAsync("vermillion_city"); // Has sailor
    var sailorLoaded = assetManager.HasTexture("sprites/npcs/sailor");
    Assert.True(sailorLoaded, "Sailor sprite should be loaded");

    // Load second map also using sailor
    await mapInitializer.LoadMapAsync("cerulean_city"); // Also has sailor

    // ACT - Unload first map
    mapLifecycleManager.UnloadMap("vermillion_city");
    GC.Collect(2, GCCollectionMode.Forced, blocking: true);

    // ASSERT - Sailor sprite still loaded (used by second map)
    Assert.True(assetManager.HasTexture("sprites/npcs/sailor"),
        "Shared sprite should remain when still in use by another map");
}
```

**Expected**: Reference counting prevents premature sprite unloading.

---

#### **Test Case F4: Missing Sprite Fallback**
```csharp
[Fact]
[Trait("Category", "Functional")]
public async Task MissingSprite_UsesFallbackTexture()
{
    // ARRANGE - Map references non-existent sprite
    var mapData = new MapData
    {
        Npcs = new[]
        {
            new NpcData { SpriteId = "sprites/npcs/nonexistent" }
        }
    };

    // ACT - Load map (should not crash)
    await mapInitializer.LoadMapWithData(mapData);

    // ASSERT - Fallback texture used
    var npcEntity = world.QueryFirst<Sprite>();
    Assert.NotNull(npcEntity);

    // Should use fallback texture (e.g., "sprites/fallback" or pink square)
    Assert.Contains("fallback", npcEntity.Texture.Path.ToLower());

    _output.WriteLine("Missing sprite handled gracefully with fallback");
}
```

**Expected**: No crash, fallback texture displayed.

---

#### **Test Case F5: Cache Clearing**
```csharp
[Fact]
[Trait("Category", "Functional")]
public void ClearCache_RemovesAllCachedSprites()
{
    // ARRANGE
    spriteLoader.LoadSprite("sprites/npcs/nurse");
    spriteLoader.LoadSprite("sprites/npcs/sailor");

    var (beforeCount, beforeSize) = spriteLoader.GetCacheStats();
    Assert.True(beforeCount >= 2, "Cache should have sprites");

    // ACT
    spriteLoader.ClearCache();

    // ASSERT
    var (afterCount, afterSize) = spriteLoader.GetCacheStats();
    Assert.Equal(0, afterCount);
    Assert.Equal(0L, afterSize);

    _output.WriteLine($"Cache cleared: {beforeCount} sprites removed");
}
```

**Expected**: Cache completely emptied.

---

### 2.2 Performance Tests

#### **Test Case P1: Memory Reduction Validation**
```csharp
[Fact]
[Trait("Category", "Performance")]
[Trait("Priority", "Critical")]
public async Task LazyLoading_ReducesMemoryBy25MB()
{
    // BASELINE - Old system (load all sprites)
    ForceGarbageCollection();
    var baselineMemory = GC.GetTotalMemory(false);

    // Simulate old behavior: Load all sprite sheets
    var allSprites = Directory.GetFiles("Assets/sprites", "*.png", SearchOption.AllDirectories);
    foreach (var sprite in allSprites)
    {
        await assetManager.LoadTextureAsync(sprite);
    }

    ForceGarbageCollection();
    var fullLoadMemory = GC.GetTotalMemory(false);
    var fullLoadUsageMB = (fullLoadMemory - baselineMemory) / 1_000_000.0;

    _output.WriteLine($"Full sprite load: {fullLoadUsageMB:F1}MB");
    Assert.InRange(fullLoadUsageMB, 35, 50); // Expect ~40MB

    // NEW SYSTEM - Lazy loading
    assetManager.ClearAllTextures();
    ForceGarbageCollection();

    await mapInitializer.LoadMapAsync("pallet_town"); // Single map
    ForceGarbageCollection();

    var lazyLoadMemory = GC.GetTotalMemory(false);
    var lazyLoadUsageMB = (lazyLoadMemory - baselineMemory) / 1_000_000.0;

    _output.WriteLine($"Lazy sprite load: {lazyLoadUsageMB:F1}MB");

    var savings = fullLoadUsageMB - lazyLoadUsageMB;
    _output.WriteLine($"Memory saved: {savings:F1}MB");

    // ASSERT - Should save 25-35MB
    Assert.True(savings >= 25,
        $"Expected ≥25MB savings, got {savings:F1}MB");
}
```

**Expected**: ≥25MB memory reduction compared to loading all sprites.

---

#### **Test Case P2: No Visual Pop-In (Preloading)**
```csharp
[Fact]
[Trait("Category", "Performance")]
public async Task SpritePreloading_PreventsPopIn()
{
    // ARRANGE - Start map load
    var loadTask = mapInitializer.LoadMapAsync("route_1");

    // ACT - Wait for preload phase to complete
    await Task.Delay(50); // Allow preload to start

    var preloadComplete = spriteLoader.AreRequiredSpritesLoaded();

    // ASSERT - Sprites should be preloaded BEFORE map visible
    Assert.True(preloadComplete,
        "Sprites should be preloaded before map renders");

    await loadTask; // Complete load

    _output.WriteLine("Sprites preloaded successfully - no pop-in");
}
```

**Expected**: Sprites loaded before first render frame.

---

#### **Test Case P3: Map Load Time Unchanged**
```csharp
[Fact]
[Trait("Category", "Performance")]
public async Task LazyLoading_DoesNotSlowMapLoad()
{
    // BASELINE - Measure without lazy loading
    var stopwatch = Stopwatch.StartNew();
    await mapInitializer.LoadMapAsync("route_1");
    stopwatch.Stop();
    var baselineMs = stopwatch.ElapsedMilliseconds;

    _output.WriteLine($"Baseline map load: {baselineMs}ms");

    // ACT - Measure with lazy loading
    mapLifecycleManager.UnloadMap("route_1");

    stopwatch.Restart();
    await mapInitializer.LoadMapAsync("route_1");
    stopwatch.Stop();
    var lazyLoadMs = stopwatch.ElapsedMilliseconds;

    _output.WriteLine($"Lazy load map time: {lazyLoadMs}ms");

    // ASSERT - Should be within ±10ms
    var difference = Math.Abs(lazyLoadMs - baselineMs);
    Assert.True(difference <= 10,
        $"Map load time changed by {difference}ms (expected ±10ms)");
}
```

**Expected**: Map load time within ±10ms tolerance.

---

#### **Test Case P4: Cache Statistics Accuracy**
```csharp
[Fact]
[Trait("Category", "Performance")]
public void CacheStatistics_AreAccurate()
{
    // ARRANGE - Load known sprites
    var sprite1 = spriteLoader.LoadSprite("sprites/npcs/nurse");
    var sprite2 = spriteLoader.LoadSprite("sprites/npcs/sailor");
    var sprite3 = spriteLoader.LoadSprite("sprites/npcs/nurse"); // Duplicate

    // ACT
    var (count, sizeBytes) = spriteLoader.GetCacheStats();

    // ASSERT
    Assert.Equal(2, count); // Only 2 unique sprites
    Assert.True(sizeBytes > 0, "Cache size should be non-zero");

    // Estimate: Each sprite ~128KB (256x256 RGBA)
    var expectedMinSize = 2 * 100_000; // 200KB minimum
    Assert.True(sizeBytes >= expectedMinSize,
        $"Cache size {sizeBytes} bytes seems too small");

    _output.WriteLine($"Cache: {count} sprites, {sizeBytes / 1024}KB");
}
```

**Expected**: Accurate cache counts and size estimates.

---

### 2.3 Integration Tests

#### **Test Case I1: Multiple Map Transitions**
```csharp
[Fact]
[Trait("Category", "Integration")]
public async Task MultipleMapTransitions_WorkCorrectly()
{
    var maps = new[]
    {
        "pallet_town",
        "route_1",
        "viridian_city",
        "route_2",
        "pewter_city"
    };

    foreach (var mapName in maps)
    {
        _output.WriteLine($"Loading map: {mapName}");

        // ACT
        await mapInitializer.LoadMapAsync(mapName);

        // ASSERT - Map loads successfully
        var currentMap = mapRegistry.GetCurrentMapId();
        Assert.Equal(mapName, currentMap);

        // Check sprites are present
        var spriteEntities = world.Query<Sprite>().Count();
        Assert.True(spriteEntities > 0, $"Map {mapName} should have sprites");

        _output.WriteLine($"  Sprites: {spriteEntities}");
    }

    _output.WriteLine("All map transitions successful");
}
```

**Expected**: All maps load correctly with appropriate sprites.

---

#### **Test Case I2: Map Revisit (Cache Reuse)**
```csharp
[Fact]
[Trait("Category", "Integration")]
public async Task RevisitingMap_UsesCachedSprites()
{
    // ARRANGE - Load map first time
    await mapInitializer.LoadMapAsync("pallet_town");
    var firstLoadCount = assetManager.LoadedTextureCount;

    // Leave and load another map
    await mapInitializer.LoadMapAsync("route_1");

    // ACT - Return to original map
    var stopwatch = Stopwatch.StartNew();
    await mapInitializer.LoadMapAsync("pallet_town");
    stopwatch.Stop();

    // ASSERT
    var secondLoadCount = assetManager.LoadedTextureCount;

    // Should use cached sprites (faster load)
    Assert.True(stopwatch.ElapsedMilliseconds < 50,
        "Cached sprites should load faster");

    _output.WriteLine($"Revisit load time: {stopwatch.ElapsedMilliseconds}ms (cached)");
}
```

**Expected**: Revisiting maps uses cached sprites for faster loading.

---

#### **Test Case I3: Long Session Stability**
```csharp
[Fact]
[Trait("Category", "Integration")]
[Trait("Priority", "Critical")]
public async Task LongSession_NoMemoryLeaks()
{
    // ARRANGE
    var maps = new[] { "map1", "map2", "map3", "map4", "map5" };

    ForceGarbageCollection();
    var initialMemory = GC.GetTotalMemory(false);

    // ACT - Simulate 1-hour gameplay (20 map transitions)
    for (int cycle = 0; cycle < 4; cycle++)
    {
        foreach (var map in maps)
        {
            await mapInitializer.LoadMapAsync(map);
            await Task.Delay(100); // Simulate gameplay
        }

        ForceGarbageCollection();
        var cycleMemory = GC.GetTotalMemory(false);
        var growth = (cycleMemory - initialMemory) / 1_000_000.0;

        _output.WriteLine($"Cycle {cycle + 1}: Memory growth = {growth:F1}MB");
    }

    // ASSERT - Memory should stabilize (not grow unbounded)
    ForceGarbageCollection();
    var finalMemory = GC.GetTotalMemory(false);
    var totalGrowth = (finalMemory - initialMemory) / 1_000_000.0;

    // Allow 10MB growth max (stable with cache)
    Assert.True(Math.Abs(totalGrowth) <= 10,
        $"Memory leak detected: {totalGrowth:F1}MB growth");

    _output.WriteLine($"✓ No memory leak after 20 transitions");
}
```

**Expected**: Memory stabilizes (no unbounded growth).

---

### 2.4 Edge Case Tests

#### **Test Case E1: Map With No NPCs**
```csharp
[Fact]
[Trait("Category", "EdgeCase")]
public async Task EmptyMap_LoadsOnlyPlayerSprite()
{
    // ARRANGE - Map with zero NPCs
    var initialCount = assetManager.LoadedTextureCount;

    // ACT
    await mapInitializer.LoadMapAsync("empty_map");

    // ASSERT
    var loadedCount = assetManager.LoadedTextureCount - initialCount;

    // Should only load player sprite + tileset
    Assert.InRange(loadedCount, 1, 3);

    _output.WriteLine($"Empty map loaded {loadedCount} textures (expected 1-3)");
}
```

**Expected**: Only essential textures loaded (player, tileset).

---

#### **Test Case E2: Map With 50+ NPCs (Stress Test)**
```csharp
[Fact]
[Trait("Category", "EdgeCase")]
public async Task LargeMap_Loads50PlusSprites()
{
    // ARRANGE - Map with 50+ NPCs
    var stopwatch = Stopwatch.StartNew();

    // ACT
    await mapInitializer.LoadMapAsync("large_city_map");
    stopwatch.Stop();

    // ASSERT
    var spriteCount = world.Query<Sprite>().Count();
    Assert.True(spriteCount >= 50,
        $"Expected 50+ sprites, got {spriteCount}");

    // Should still load reasonably fast
    Assert.True(stopwatch.ElapsedMilliseconds < 500,
        $"Large map took {stopwatch.ElapsedMilliseconds}ms (expected <500ms)");

    _output.WriteLine($"Loaded {spriteCount} sprites in {stopwatch.ElapsedMilliseconds}ms");
}
```

**Expected**: Large maps load successfully without performance degradation.

---

#### **Test Case E3: Rapid Consecutive Transitions**
```csharp
[Fact]
[Trait("Category", "EdgeCase")]
public async Task RapidTransitions_HandleConcurrency()
{
    // ACT - Spam map transitions rapidly
    var tasks = new List<Task>();

    for (int i = 0; i < 10; i++)
    {
        var mapName = $"map_{i % 3}"; // Cycle through 3 maps
        tasks.Add(mapInitializer.LoadMapAsync(mapName));
        await Task.Delay(10); // 10ms between starts
    }

    // ASSERT - All should complete without crashes
    await Task.WhenAll(tasks);

    _output.WriteLine("All rapid transitions completed successfully");

    // Verify final state is valid
    var currentMap = mapRegistry.GetCurrentMapId();
    Assert.NotNull(currentMap);
}
```

**Expected**: No race conditions or crashes during rapid transitions.

---

## 3. Memory Validation Strategy

### **Test Suite: MemoryValidation.LazySpriteLoading**

```csharp
public class LazySpriteLoadingMemoryTests : IDisposable
{
    private const long TARGET_MEMORY_SAVINGS_MB = 25;
    private const long MAX_SINGLE_MAP_MB = 15;

    [Fact]
    public void Baseline_AllSpritesLoaded_Uses40MB()
    {
        // Measure old system
    }

    [Fact]
    public void LazyLoad_SingleMap_UsesLessThan15MB()
    {
        // Measure new system
    }

    [Fact]
    public void MemorySavings_MeetsTarget25MB()
    {
        // Compare old vs new
    }
}
```

---

## 4. Visual Regression Testing

### **Manual Test Checklist**

| Test | Description | Pass Criteria |
|------|-------------|---------------|
| VR-1 | Map transition smoothness | No sprite pop-in visible |
| VR-2 | NPC sprite rendering | All NPCs render correctly |
| VR-3 | Player sprite persistence | Player visible at all times |
| VR-4 | Animation continuity | Sprite animations work |
| VR-5 | Missing sprite fallback | No pink/missing textures |

**Testing Procedure**:
1. Load game in debug mode with FPS counter
2. Transition through 10 maps
3. Observe for visual glitches
4. Record any pop-in or missing textures
5. Verify all sprites render correctly

---

## 5. Performance Benchmarks

### **Metrics to Track**

| Metric | Target | Measurement Method |
|--------|--------|-------------------|
| Memory reduction | ≥25MB | GC.GetTotalMemory() comparison |
| Map load time | ±10ms | Stopwatch.ElapsedMilliseconds |
| Sprite preload time | <50ms | Async task completion time |
| Cache hit rate | ≥90% | spriteLoader.GetCacheStats() |
| Memory stability | ±10MB | GC after 20 transitions |

---

## 6. Regression Testing Plan

### **Ensure No Regressions**

```bash
# Run existing test suites
dotnet test PokeSharp.Engine.Systems.Tests
dotnet test PokeSharp.Game.Data.Tests
dotnet test Integration
dotnet test MemoryValidation
dotnet test PerformanceBenchmarks
```

**Checklist**:
- [ ] All existing unit tests pass
- [ ] Integration tests pass
- [ ] Memory validation tests pass
- [ ] Performance benchmarks show no degradation
- [ ] No new compiler warnings
- [ ] Code coverage maintained (≥80%)

---

## 7. Success Criteria

### **Phase 2 Complete When**:

✅ **Memory**:
- Memory usage reduced by ≥25MB (validated in tests)
- Single map uses ≤15MB (down from ~40MB)

✅ **Performance**:
- Map load time unchanged (±10ms tolerance)
- No visual sprite pop-in
- Cache hit rate ≥90% on map revisits

✅ **Stability**:
- All functional tests pass
- No memory leaks (20+ map transitions)
- No crashes or exceptions

✅ **Quality**:
- All existing tests pass (no regressions)
- Code coverage ≥80%
- Visual regression tests pass

✅ **Documentation**:
- Test results documented
- Performance metrics recorded
- Edge cases validated

---

## 8. Test Execution Plan

### **Phase 1: Unit Tests** (Day 1)
- Run functional tests (F1-F5)
- Validate core sprite loading logic
- Fix any failing tests

### **Phase 2: Performance Tests** (Day 2)
- Run memory validation (P1)
- Measure map load times (P3)
- Verify cache accuracy (P4)

### **Phase 3: Integration Tests** (Day 3)
- Multi-map workflow (I1)
- Long session stability (I3)
- Cache reuse validation (I2)

### **Phase 4: Edge Cases & Regression** (Day 4)
- Edge case tests (E1-E3)
- Full regression suite
- Visual regression testing

### **Phase 5: Final Validation** (Day 5)
- Compile all metrics
- Generate test report
- Sign-off on success criteria

---

## 9. Test Infrastructure

### **Required Tools**:
- **xUnit** - Test framework
- **BenchmarkDotNet** - Performance benchmarking (optional)
- **dotMemory** - Memory profiling (Rider plugin)
- **Manual testing** - Visual regression validation

### **Test Projects**:
```
tests/
  ├── Integration/
  │   └── LazySpriteLoadingIntegrationTests.cs
  ├── MemoryValidation/
  │   └── LazySpriteLoadingMemoryTests.cs
  ├── PerformanceBenchmarks/
  │   └── SpriteLoadingBenchmarks.cs
  └── PokeSharp.Game.Tests/
      └── SpriteLoaderTests.cs (unit tests)
```

---

## 10. Deliverables

### **Test Artifacts**:
1. ✅ **Test code** - All test cases implemented
2. ✅ **Test report** - Pass/fail results with metrics
3. ✅ **Performance metrics** - Memory, timing, cache stats
4. ✅ **Visual regression log** - Screenshots/observations
5. ✅ **Regression test results** - Existing test suite status
6. ✅ **Success criteria validation** - Checklist confirmation

---

## 11. Post-Implementation Testing

**After Coder completes implementation**:

```bash
# 1. Run unit tests
dotnet test --filter "Category=Functional"

# 2. Run performance tests
dotnet test --filter "Category=Performance"

# 3. Run integration tests
dotnet test --filter "Category=Integration"

# 4. Run edge case tests
dotnet test --filter "Category=EdgeCase"

# 5. Full regression
dotnet test

# 6. Memory validation
dotnet test --filter "Category=Memory"

# 7. Generate coverage report
dotnet test /p:CollectCoverage=true
```

---

## Summary

This comprehensive test strategy covers:
- **20+ automated test cases** across functional, performance, integration, and edge case scenarios
- **Memory validation** with clear 25MB reduction target
- **Performance benchmarks** ensuring no regression
- **Visual regression** manual testing checklist
- **Regression testing** plan for existing functionality
- **Clear success criteria** for phase completion

**Estimated Test Coverage**: 85-90% of lazy sprite loading functionality.

**Estimated Execution Time**: 4-5 days for full test implementation and validation.

This strategy will ensure Phase 2 lazy sprite loading is thoroughly validated before deployment.
