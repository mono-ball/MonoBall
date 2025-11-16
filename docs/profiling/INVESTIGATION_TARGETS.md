# Phase 3: Investigation Targets for Profiling

**Date:** 2025-11-16
**Purpose:** Specific files and code patterns to investigate during profiling
**Reference:** See PHASE_3_PROFILING_STRATEGY.md for methodology

---

## Systems to Profile (Priority Order)

Based on codebase analysis, these systems are most likely to contain allocations:

### 🔴 Tier 1: High Priority (Update every frame @ 60fps)

| System | File | Suspicion Level | Reason |
|--------|------|-----------------|--------|
| **MovementSystem** | `/PokeSharp.Game.Systems/Movement/MovementSystem.cs` | 🟡 MEDIUM | Complex queries, component reads/writes |
| **CollisionSystem** | `/PokeSharp.Game.Systems/Movement/CollisionSystem.cs` | 🟠 MEDIUM-HIGH | Spatial hash queries, validation logic |
| **SpatialHashSystem** | `/PokeSharp.Game.Systems/Spatial/SpatialHashSystem.cs` | 🟢 LOW | Already optimized, pooled buffer |
| **PathfindingSystem** | `/PokeSharp.Game.Systems/NPCs/PathfindingSystem.cs` | 🔴 HIGH | LINQ detected, pathfinding algorithms |
| **RelationshipSystem** | `/PokeSharp.Game.Systems/RelationshipSystem.cs` | 🟢 LOW | Phase 1 optimized, reusable lists |
| **TileAnimationSystem** | `/PokeSharp.Game.Systems/Tiles/TileAnimationSystem.cs` | 🟡 MEDIUM | Similar to SpriteAnimationSystem |

### 🟠 Tier 2: Medium Priority (Update/Render @ 60fps)

| System | File | Suspicion Level | Reason |
|--------|------|-----------------|--------|
| **SpriteAnimationSystem** | `/PokeSharp.Game/Systems/Rendering/SpriteAnimationSystem.cs` | 🟢 LOW | Phase 1+2 optimized |
| **ElevationRenderSystem** | `/PokeSharp.Engine.Rendering/Systems/ElevationRenderSystem.cs` | 🟢 LOW | Optimized with static vectors |
| **SpriteTextureLoader** | `/PokeSharp.Game/Systems/Rendering/SpriteTextureLoader.cs` | 🟡 MEDIUM | Async loading, caching |
| **SystemManager** | `/PokeSharp.Engine.Systems/Management/SystemManager.cs` | 🔴 HIGH | LINQ in Update/Render (known issue) |

### 🟡 Tier 3: Lower Priority (Periodic or event-driven)

| System | File | Suspicion Level | Reason |
|--------|------|-----------------|--------|
| **PoolCleanupSystem** | `/PokeSharp.Game.Systems/PoolCleanupSystem.cs` | 🟢 LOW | Cleanup-only, not hot path |
| **GameDataLoader** | `/PokeSharp.Game.Data/Loading/GameDataLoader.cs` | 🟢 LOW | Startup only, not in game loop |
| **MapLoader** | `/PokeSharp.Game.Data/MapLoading/Tiled/MapLoader.cs` | 🟢 LOW | Map load only, not per-frame |

---

## Specific Code Patterns to Investigate

### Pattern 1: LINQ in Hot Paths 🔴

**Known Locations:**
```
SystemManager.cs:232 - Update() method
SystemManager.cs:275 - Render() method
PathfindingSystem.cs - TBD (LINQ detected via grep)
```

**What to look for in profiler:**
- `Enumerable.Where` allocations
- `Enumerable.ToArray` / `ToList` allocations
- Iterator allocations for `FirstOrDefault`, `Any`, etc.

**Expected impact:** 20-30 KB/sec per LINQ query @ 60fps

---

### Pattern 2: World.Query Lambda Allocations 🟡

**Potential in every system using:**
```csharp
world.Query(in query, (Entity e, ref ComponentA a, ref ComponentB b) => {
    // If this captures 'this' or local variables, it allocates a closure
});
```

**Files to check:**
- MovementSystem.cs (lines 93, 245)
- CollisionSystem.cs (check all queries)
- SpatialHashSystem.cs (lines 51, 68)

**What to look for in profiler:**
- `DisplayClass` allocations (closure objects)
- Delegate allocations

**Expected impact:** 5-10 KB/sec per capturing lambda @ 60fps

---

### Pattern 3: Component Reads/Writes 🟡

**Arch ECS component access patterns:**
```csharp
// TryGet - may allocate if struct is large
world.TryGet(entity, out LargeStruct component)

// Set - writes component back (necessary but costly for large structs)
world.Set(entity, modifiedComponent)
```

**Files to audit:**
- MovementSystem.cs (line 98, 110) - Animation component write
- Any system with >64 byte structs

**What to look for in profiler:**
- Arch.Core archetype transition allocations
- Component copy allocations

**Expected impact:** Variable, depends on struct size and frequency

---

### Pattern 4: Collection Growth 🟠

**Suspect collections:**

1. **SpatialHashSystem._queryResultBuffer** (line 27)
   - Pre-allocated to 128 capacity ✅
   - But may grow if >128 entities at one position 🟡

2. **MovementSystem._entitiesToRemove** (line 39)
   - Pre-allocated to 32 capacity ✅
   - Should be sufficient for typical usage ✅

3. **RelationshipSystem._entitiesToFix** (line 42)
   - Pre-allocated but no initial capacity ⚠️
   - Should have capacity set based on typical entity count

**What to look for in profiler:**
- `List<T>.Grow` allocations
- `Dictionary<K,V>.Resize` allocations
- Array allocations for collection backing

**Expected impact:** 10-30 KB/sec if collections frequently exceed capacity

---

### Pattern 5: Logging in Hot Paths 🟠

**Grep results found logging in:**
```bash
# Check for logging inside Update/Render methods
grep -A5 -B5 "public override void Update\|public void Update" \
  PokeSharp.Game.Systems/**/*.cs | \
  grep "Log(Information\|Debug\|Trace)"
```

**Known safe locations:**
- PerformanceMonitor.cs - Periodic logging (every 5 seconds)
- SystemPerformanceTracker.cs - Periodic logging

**Suspect locations to check in profiler:**
- Any Debug/Trace logging without LogLevel guards
- String interpolation in log messages

**What to look for in profiler:**
- String allocations for log messages
- Structured logging parameter boxing
- LoggerMessage allocations

**Expected impact:** 20-50 KB/sec if debug logging enabled

---

### Pattern 6: String Operations 🟡

**Already optimized:**
- ✅ SpriteAnimationSystem ManifestKey caching
- ✅ Direction.ToString() elimination

**Still to check:**
- Animation name lookups (string keys)
- Map ID conversions
- Component name strings
- Debug/logging strings

**What to look for in profiler:**
- `String.Concat` allocations
- String interpolation allocations
- `String.Format` allocations

**Expected impact:** 10-20 KB/sec for remaining string ops

---

## Profiling Session Checklist

### Before Starting
- [ ] Build project in Release mode
- [ ] Close unnecessary applications
- [ ] Install dotnet-trace and dotnet-counters
- [ ] Review PHASE_3_PROFILING_STRATEGY.md

### During Profiling
- [ ] Start game and let stabilize (30+ seconds)
- [ ] Monitor with dotnet-counters (60 seconds)
- [ ] Record baseline GC metrics
- [ ] Collect trace with dotnet-trace (30 seconds)
- [ ] Take 2-3 additional traces for consistency

### After Profiling
- [ ] Analyze traces for top allocation sources
- [ ] Document findings in PHASE_3_PROFILING_RESULTS.md
- [ ] Cross-reference with this investigation targets list
- [ ] Prioritize fixes by KB/sec impact
- [ ] Create optimization tasks

---

## Expected Findings Breakdown

Based on prior analysis, we expect allocations to break down as:

```
Expected Allocation Sources (300-400 KB/sec total):

🔴 Tier 1: High Impact (>50 KB/sec each)
├─ SystemManager LINQ: ~70 KB/sec (CONFIRMED via code review)
├─ PathfindingSystem LINQ: ~60 KB/sec (suspected, needs profiling)
└─ Logging in hot paths: ~50 KB/sec (if debug enabled)

🟠 Tier 2: Medium Impact (20-50 KB/sec each)
├─ World.Query closures: ~30 KB/sec (suspected)
├─ Collection growth: ~25 KB/sec (occasional)
├─ String operations: ~20 KB/sec (remaining after optimizations)
└─ Component reads/writes: ~20 KB/sec (ECS overhead)

🟡 Tier 3: Low Impact (<20 KB/sec each)
├─ Framework allocations: ~15 KB/sec (MonoGame, Arch)
├─ Input handling: ~10 KB/sec (60fps polling)
└─ Misc one-time allocations: ~10 KB/sec (caching, etc.)
```

---

## Code Review Findings Summary

### ✅ Well-Optimized Systems

**MovementSystem.cs:**
- ✅ Cached direction names (no ToString() allocations)
- ✅ Reusable collections (_entitiesToRemove, _tileSizeCache)
- ✅ Single query with TryGet for optional components
- ✅ Good optimization comments documenting choices

**SpatialHashSystem.cs:**
- ✅ Pooled query result buffer (128 capacity)
- ✅ Dirty tracking for static tiles (build once)
- ✅ Clear separation of static/dynamic entities
- ✅ Reusable internal data structures

**RelationshipSystem.cs:**
- ✅ Phase 1 optimizations applied
- ✅ Reusable _entitiesToFix list
- ✅ Marking invalid instead of removing (avoids ECS structural changes)

### ⚠️ Systems Needing Investigation

**PathfindingSystem.cs:**
- ⚠️ LINQ detected via grep (needs manual review)
- ⚠️ Not yet examined for allocation patterns
- ⚠️ Pathfinding algorithms may allocate paths/nodes

**SystemManager.cs:**
- 🔴 CONFIRMED: LINQ in Update() and Render() methods
- 🔴 `.Where(s => s.Enabled).ToArray()` every frame (120x/sec)
- 🔴 High priority fix target

**CollisionSystem.cs:**
- ⚠️ Needs profiling to check spatial hash query overhead
- ⚠️ May have validation logic allocations

---

## Profiling Priorities

### Session 1: Baseline and Hot Path Analysis
**Duration:** 30 minutes
**Goal:** Identify top 3 allocation sources

1. Collect baseline metrics with dotnet-counters
2. Capture 3x 30-second traces
3. Analyze for methods allocating >50 KB/sec
4. Document top 3 sources

### Session 2: Deep Dive on Top Source
**Duration:** 45 minutes
**Goal:** Understand #1 allocation source in detail

1. Focus profiling on identified hot path
2. Examine call stacks and object types
3. Determine root cause (LINQ, closures, collections, etc.)
4. Design optimization approach

### Session 3: Verification After Fixes
**Duration:** 20 minutes per fix
**Goal:** Measure improvement from each optimization

1. Apply single fix
2. Re-profile with dotnet-counters
3. Capture new trace for comparison
4. Verify GC rate improvement
5. Document results

---

## Integration with Existing Diagnostics

### PerformanceMonitor.cs Enhancements

**Current capabilities:**
- Frame time tracking (avg, min, max)
- GC collection counts (Gen0, Gen1, Gen2)
- Memory usage (total MB)
- Slow frame warnings

**Recommended additions:**
```csharp
// Track allocation rate
private long _lastTotalMemory;

private void LogMemoryStats()
{
    var totalMemoryBytes = GC.GetTotalMemory(false);
    var allocatedBytes = totalMemoryBytes - _lastTotalMemory;
    var allocRateKBPerSec = (allocatedBytes / 1024.0) / 5.0;

    _logger.LogInformation(
        "Memory: {Memory:F1} MB | Alloc: {Alloc:F1} KB/sec | " +
        "Gen0: {Gen0} ({Delta}/5s), Gen1: {Gen1}, Gen2: {Gen2}",
        totalMemoryMb, allocRateKBPerSec,
        gen0, gen0 - _lastGen0Count, gen1, gen2
    );

    _lastTotalMemory = totalMemoryBytes;
}
```

This will give us **built-in allocation tracking** without profiler.

---

## Success Metrics

After completing Phase 3 profiling and optimizations:

### Target Metrics
```
Gen0 GC:         46.8 → 5-8 collections/sec     (-82-89%)
Gen2 GC:         14.6 → 0-1 collections/sec     (-93-100%)
Alloc Rate:      750 → 80-130 KB/sec            (-83-89%)
Per-Frame:       12.5 KB → 1.3-2.2 KB @ 60fps   (-82-89%)
```

### Validation
- [ ] Run game for 5+ minutes without GC issues
- [ ] Frame times consistent (no GC pauses)
- [ ] Memory usage stable (no growth over time)
- [ ] No Gen2 collections during normal gameplay
- [ ] Allocation rate <150 KB/sec sustained

---

## Next Steps

1. **Read PHASE_3_PROFILING_STRATEGY.md** for detailed methodology
2. **Run profiling Session 1** to establish baseline
3. **Create PHASE_3_PROFILING_RESULTS.md** to document findings
4. **Prioritize fixes** based on KB/sec impact
5. **Implement and verify** each optimization

---

**Document Status:** ✅ Ready for use
**Last Updated:** 2025-11-16
**Complements:** PHASE_3_PROFILING_STRATEGY.md
