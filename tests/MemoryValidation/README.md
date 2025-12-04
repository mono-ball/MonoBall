# Memory Validation Test Suite

## 🎯 Purpose

This test suite validates that memory optimizations keep the game's memory usage below **500MB** during normal gameplay.

## 🧪 Test Coverage

### 1. **Baseline Memory Test** (`Test01_BaselineMemory_ShouldBeBelow500MB`)
- **What**: Measures memory after game initialization
- **Target**: <500MB baseline
- **Why**: Ensures initialization doesn't over-allocate

### 2. **Map Load Test** (`Test02_MapLoad_ShouldIncreaseMemoryLessThan100MB`)
- **What**: Measures memory increase when loading a single map
- **Target**: <100MB increase per map
- **Why**: Verifies textures are optimized and shared

### 3. **Map Transition Test** (`Test03_MapTransition_ShouldCleanupPreviousMap`)
- **What**: Loads Map A, then Map B, checks if Map A cleaned up
- **Target**: Memory returns to baseline + 1 map worth
- **Why**: Ensures maps are properly unloaded on transition

### 4. **Stress Test** (`Test04_StressTest_Should10MapsStayBelow500MB`)
- **What**: Loads 10 maps sequentially, monitors memory throughout
- **Target**: <500MB throughout, <50MB growth in last 5 maps
- **Why**: Detects memory leaks during extended play

### 5. **LRU Cache Test** (`Test05_LRUCache_ShouldEvictOldestTextures`)
- **What**: Loads maps until cache limit hit, verifies eviction
- **Target**: Cache ≤50MB, oldest textures removed
- **Why**: Confirms LRU cache prevents unbounded growth

## 🚀 Quick Start

### Run All Tests
```bash
# Using the test runner script (recommended)
./tests/MemoryValidation/RunMemoryTests.sh

# Or using dotnet directly
dotnet test --filter "Category=Memory"
```

### Run Specific Test
```bash
dotnet test --filter "FullyQualifiedName~Test01_BaselineMemory"
```

### Run with Verbose Output
```bash
dotnet test --filter "Category=Memory" --logger "console;verbosity=detailed"
```

## 📊 Interpreting Results

### ✅ Success Example
```
Test01_BaselineMemory_ShouldBeBelow500MB: PASSED
  Baseline Memory: 287.3MB
  ✅ PASS: Baseline memory 287.3MB is below 500MB

Test02_MapLoad_ShouldIncreaseMemoryLessThan100MB: PASSED
  Memory Increase: 63.2MB
  ✅ PASS: Map load increased memory by 63.2MB (<100MB)

Test03_MapTransition_ShouldCleanupPreviousMap: PASSED
  Total Increase from Baseline: 68.5MB
  ✅ PASS: Map transition cleaned up previous map

Test04_StressTest_Should10MapsStayBelow500MB: PASSED
  Max Memory: 389.7MB
  Last 5 Maps Growth: 8.3MB
  ✅ PASS: All 10 maps stayed below 500MB

Test05_LRUCache_ShouldEvictOldestTextures: PASSED
  Final Cache Size: 48.9MB
  ✅ PASS: LRU cache stayed within limits
```

### ❌ Failure Example
```
Test04_StressTest_Should10MapsStayBelow500MB: FAILED
  Memory at Map 7: 523.4MB
  ❌ FAIL: Memory 523.4MB exceeded 500MB at map 7

  Likely Issue: Memory leak in map loading or cache not evicting
```

## 🔧 Adding Memory Monitoring to Your Game

### In MonoBall FrameworkGame.cs

```csharp
using MonoBall Framework.Tests.MemoryValidation;

private int _frameCount = 0;

protected override void LoadContent()
{
    base.LoadContent();

    // Log memory after initialization
    QuickMemoryCheck.LogMemoryStats(_assetManager);
}

protected override void Update(GameTime gameTime)
{
    base.Update(gameTime);

    // Monitor memory every 5 seconds (300 frames at 60 FPS)
    QuickMemoryCheck.MonitorMemory(_assetManager, _frameCount++);
}
```

### Manual Testing

```csharp
// In Program.Main or a test harness
QuickMemoryCheck.RunQuickTest();
```

## 📈 Success Criteria

| Metric | Excellent | Good | Acceptable | Fail |
|--------|-----------|------|------------|------|
| **Baseline** | <300MB | <400MB | <500MB | >500MB |
| **Per Map** | <50MB | <80MB | <100MB | >100MB |
| **10 Maps** | <350MB | <450MB | <500MB | >500MB |
| **Cache** | <45MB | <50MB | <55MB | >55MB |

## 🐛 Troubleshooting

### Baseline >500MB
- Review `AssetManager` initialization
- Check for unnecessary preloading
- Verify static allocations are minimal

### Map Load >100MB
- Check texture compression settings
- Verify texture sizes (should be powers of 2)
- Ensure textures are shared across tilesets

### Map Transition Doesn't Cleanup
- Verify `UnloadMap()` calls `Dispose()` on textures
- Check for event handler leaks
- Review object references preventing GC

### Stress Test Fails
- Enable detailed logging in map loading
- Check for texture duplication
- Verify LRU cache is actually evicting

### LRU Cache Exceeds Limit
- Review eviction logic in `AssetManager`
- Confirm texture size calculations are accurate
- Check that oldest textures are tracked correctly

## 📁 Files in This Directory

- **MemoryValidationTests.cs** - Main test suite (5 tests)
- **QuickMemoryCheck.cs** - Utility for manual testing
- **MemoryTestResults.md** - Detailed results template
- **RunMemoryTests.sh** - Bash script to run all tests
- **README.md** - This file

## 🎯 Next Steps

1. **Run tests** after coder implements fixes
2. **All PASS**: Memory optimization complete ✅
3. **Some FAIL**: Review output, fix issues, re-run
4. **Update** `MemoryTestResults.md` with actual results
5. **Commit** test results with memory fix commit

## 📝 Notes

- Tests use `ITestOutputHelper` for detailed console logging
- Each test forces GC for accurate measurements
- Tests are independent and can run in any order
- Expected runtime: ~30-60 seconds for full suite
- Tests are tagged with `[Trait("Category", "Memory")]` for filtering

---

**Remember**: Memory below 500MB is CRITICAL for smooth gameplay. These tests ensure we stay under that limit! 🎮
