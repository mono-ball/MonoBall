# Phase 3: Profiling Strategy for Mystery Allocations

**Date:** 2025-11-16
**Objective:** Identify and eliminate the remaining ~300-400 KB/sec of mystery allocations
**Current Status:** 🔴 46.8 Gen0 GC/sec (23x higher than normal)
**Target:** 🟢 5-8 Gen0 GC/sec (normal for 60fps game)

---

## Executive Summary

### What We Know
```
✅ Phase 1 Optimizations (Completed):
├─ SpriteAnimationSystem string allocations: ELIMINATED (cached ManifestKey)
├─ RelationshipSystem temporary lists: ELIMINATED (reusable collections)
└─ SystemPerformanceTracker LINQ: ELIMINATED (List.Sort instead of LINQ)

✅ Phase 2 Optimizations (Completed):
├─ SpriteAnimationSystem HashSet: ELIMINATED (using ulong bit field)
└─ Direction.ToString() calls: ELIMINATED (cached string array)

🔴 Remaining Mystery (~300-400 KB/sec):
├─ Known small sources: ~50-80 KB/sec (identified but not yet fixed)
└─ Unknown sources: ~250-350 KB/sec (TO BE FOUND via profiling)
```

### Why Profiling is Critical

**The 80/20 Rule:** We've eliminated obvious allocations, but 70% of GC pressure remains hidden. Profiling will reveal:
- Hot path allocations we haven't found via code review
- Unexpected framework/library allocations
- Hidden boxing or collection growth
- LINQ queries in paths we haven't examined

---

## Profiling Methodology

### Overview of Available Tools

| Tool | Best For | Platform | Allocation Detail | Ease of Use |
|------|----------|----------|-------------------|-------------|
| **dotnet-trace** | Quick allocation profiling | Cross-platform | Medium | ⭐⭐⭐⭐⭐ |
| **dotnet-counters** | Real-time GC metrics | Cross-platform | Low | ⭐⭐⭐⭐⭐ |
| **PerfView** | Deep allocation analysis | Windows only | Very High | ⭐⭐⭐ |
| **dotMemory** | Visual profiling | Cross-platform | Very High | ⭐⭐⭐⭐ |
| **VS Profiler** | IDE integration | Windows only | High | ⭐⭐⭐⭐ |

**Recommended Primary Tool:** `dotnet-trace` (works on WSL2, quick setup, good detail)
**Recommended Secondary Tool:** `dotnet-counters` (for real-time validation)

---

## Step-by-Step Profiling Plan

### Phase 1: Real-Time Monitoring (5 minutes)

**Goal:** Confirm current GC pressure and establish baseline

```bash
# Install tools (if needed)
dotnet tool install --global dotnet-counters

# Terminal 1: Start game in Release mode
cd /mnt/c/Users/nate0/RiderProjects/PokeSharp/PokeSharp.Game
dotnet run --configuration Release

# Terminal 2: Find process ID
ps aux | grep PokeSharp | grep -v grep

# Monitor live GC metrics
dotnet-counters monitor --process-id <PID> \
  --counters System.Runtime[gen-0-gc-count,gen-1-gc-count,gen-2-gc-count,alloc-rate,gc-heap-size]

# Let it run for 60 seconds to get stable baseline
# Record the allocation rate (KB/sec) and GC counts
```

**Expected Output:**
```
[System.Runtime]
    Gen 0 GC Count (Count / 1 sec)                    46-50
    Gen 1 GC Count (Count / 1 sec)                     2-4
    Gen 2 GC Count (Count / 1 sec)                    14-16
    Allocation Rate (B / 1 sec)                  700,000-800,000
    GC Heap Size (MB)                                  50-100
```

---

### Phase 2: Allocation Trace Collection (10 minutes)

**Goal:** Capture detailed allocation call stacks

```bash
# Install dotnet-trace
dotnet tool install --global dotnet-trace

# Collect 30-second trace with allocation sampling
dotnet-trace collect \
  --process-id <PID> \
  --providers Microsoft-Windows-DotNETRuntime:0x1:5 \
  --duration 00:00:30 \
  --output pokesharp-allocations.nettrace

# This will create a .nettrace file with:
# - Every GC collection event
# - Sampled allocation call stacks
# - Type information for allocations
```

**Provider Flags Explained:**
- `0x1` = GC events
- `:5` = Verbose level (captures allocation samples)

---

### Phase 3: Analysis (15-30 minutes)

**Goal:** Identify top allocation sources

#### Option A: Command-Line Analysis (Quick)

```bash
# View top allocation sites
dotnet-trace report pokesharp-allocations.nettrace --top-allocations

# Look for:
# - Methods allocating >50 KB/sec
# - Allocations in *System.cs files (game loop)
# - String allocations
# - Collection allocations (List, Dictionary)
```

#### Option B: Visual Studio (Detailed - Windows Only)

1. Copy `.nettrace` file to Windows
2. Open in Visual Studio 2022
3. Go to **Analyze → Performance Profiler**
4. Load trace file
5. Switch to **Allocation** view
6. Sort by **Total Allocations (Bytes)**
7. Examine call trees for hot paths

#### Option C: PerfView (Advanced - Windows Only)

1. Download PerfView from GitHub
2. Open `.nettrace` file
3. Navigate to **GC Stats** view
4. Click **GC Heap Alloc Stacks**
5. Sort by **Inc %** (inclusive percentage)
6. Look for hot paths in game systems

---

## Known Suspects to Investigate

Based on codebase analysis, prioritize profiling these areas:

### 🔴 Priority 1: World.Query Patterns

**Why:** ECS queries may allocate enumerators or delegates

**Files to check:**
```bash
grep -rn "world\.Query" \
  --include="*System*.cs" \
  PokeSharp.Game.Systems/ \
  PokeSharp.Engine.Systems/
```

**What to look for:**
- Delegate allocations for query lambdas
- Enumerator allocations if using foreach
- Query descriptor caching issues

**Example allocation source:**
```csharp
// BAD: Lambda may allocate closure
world.Query(in query, (Entity e, ref Position p) => {
    var localVar = someField; // Captures 'this' -> allocates closure
    DoSomething(localVar);
});

// GOOD: No closure capture
world.Query(in query, (Entity e, ref Position p) => {
    DoSomething(p); // Only uses parameters
});
```

---

### 🟠 Priority 2: Logging in Hot Paths

**Why:** String interpolation and structured logging allocate

**Files to check:**
```bash
grep -rn "Log(Information|Debug|Trace|Warning)" \
  --include="*System*.cs" \
  PokeSharp.Game.Systems/ | \
  grep -v "PerformanceMonitor\|LogPerformance"
```

**What to look for:**
- Logging inside `Update()` or `Render()` methods
- String interpolation: `$"text {value}"`
- Structured logging: `Log("text {Param}", param)`

**Example allocation source:**
```csharp
// BAD: Allocates string every frame
public override void Update(World world, float deltaTime)
{
    _logger.LogDebug($"Processing {entityCount} entities"); // 60x/sec!
}

// GOOD: Guard with log level check
public override void Update(World world, float deltaTime)
{
    if (_logger.IsEnabled(LogLevel.Debug))
        _logger.LogDebug("Processing {Count} entities", entityCount);
}

// BETTER: Remove logging from hot path entirely
```

---

### 🟠 Priority 3: Collection Growth

**Why:** List/Dictionary capacity expansion allocates new arrays

**Files to check:**
```bash
grep -rn "new List\|new Dictionary\|new HashSet" \
  --include="*System*.cs" \
  PokeSharp.Game.Systems/
```

**What to look for:**
- Collections created in `Update()` methods
- Collections without initial capacity
- Collections that grow beyond initial size

**Example allocation source:**
```csharp
// BAD: Creates new list every frame
public override void Update(World world, float deltaTime)
{
    var results = new List<Entity>(); // Allocates!
    world.Query(in query, (Entity e) => results.Add(e));
}

// GOOD: Reusable pooled list
private readonly List<Entity> _results = new(128);

public override void Update(World world, float deltaTime)
{
    _results.Clear(); // Reuses capacity
    world.Query(in query, (Entity e) => _results.Add(e));
}
```

---

### 🟡 Priority 4: Component Reads/Writes

**Why:** TryGet/Get/Set may allocate in certain scenarios

**Files to check:**
```bash
grep -rn "world\.TryGet\|world\.Get\|world\.Set" \
  --include="*System*.cs" \
  PokeSharp.Game.Systems/
```

**What to look for:**
- Excessive component reads in tight loops
- Writing components back unnecessarily
- Large struct components (>64 bytes) causing copies

**Example from MovementSystem.cs (GOOD):**
```csharp
// OPTIMIZATION: TryGet once, write once
if (world.TryGet(entity, out Animation animation))
{
    ProcessMovementWithAnimation(ref animation, deltaTime);
    world.Set(entity, animation); // Single write
}
```

---

### 🟡 Priority 5: String Operations

**Why:** String concatenation and formatting allocate

**Files to check:**
```bash
grep -rn '\$"\|+ "' \
  --include="*System*.cs" \
  PokeSharp.Game.Systems/
```

**What to look for:**
- String interpolation in loops
- String.Format calls
- String concatenation with `+`

---

## Profiling Best Practices

### Before Starting

1. **Build in Release mode** - Debug builds have extra allocations
2. **Let game run for 30+ seconds** - Skip startup allocations
3. **Keep game state stable** - Don't change maps or trigger events
4. **Close other apps** - Reduce noise in profiler

### During Profiling

1. **Take multiple samples** - Verify consistency (3-5 traces)
2. **Focus on hot paths** - Look for >50 KB/sec allocations
3. **Ignore one-time allocations** - Caches, initialization
4. **Ignore framework allocations** - MonoGame/Arch internals (unless excessive)

### After Profiling

1. **Document findings** - Update this file with results
2. **Estimate fix effort** - Quick win vs. major refactor
3. **Prioritize by impact** - Target highest allocation rates first
4. **Verify improvements** - Re-profile after each fix

---

## Expected Profiling Results

Based on prior analysis, we expect to find:

### Category 1: System Update Loops (50-100 KB/sec)
- **MovementSystem** - Query delegates, TryGet allocations
- **CollisionSystem** - Spatial hash queries, validation
- **PathfindingSystem** - LINQ in pathfinding logic (suspected)
- **TileAnimationSystem** - Similar to SpriteAnimationSystem

### Category 2: Logging (30-60 KB/sec)
- **String interpolation** in debug logging (if enabled)
- **Structured logging** parameter boxing
- **LogLevel guards** missing in hot paths

### Category 3: Collections (50-100 KB/sec)
- **Temporary lists** created in update methods
- **Dictionary resizing** from initial capacity too small
- **HashSet operations** if any remain after Phase 2

### Category 4: Framework/Library (50-100 KB/sec)
- **Arch ECS internals** - Query enumerators, archetype transitions
- **MonoGame** - Input handling, graphics allocations
- **Logging framework** - Microsoft.Extensions.Logging allocations

### Category 5: Mystery (50-150 KB/sec)
- **Unknown sources** requiring deep dive profiling

---

## Success Criteria

After profiling and fixes, we should see:

```
Target Metrics:
├─ Gen0 GC rate:        5-8 collections/sec (currently 46.8)
├─ Gen2 GC rate:        0-1 collections/sec (currently 14.6)
├─ Allocation rate:     80-130 KB/sec (currently 750 KB/sec)
└─ Per-frame budget:    1.3-2.2 KB @ 60fps (currently 12.5 KB)

Success Indicators:
✅ 80-85% reduction in allocation rate
✅ Gen2 collections eliminated or rare (<1/sec)
✅ Frame times consistent without GC pauses
✅ Memory usage stable over time
```

---

## Post-Profiling Action Plan

### If We Find Large Allocations (>100 KB/sec)

1. **Document the source** - File, line number, method
2. **Estimate scope** - How many entities/calls affected
3. **Design fix** - Caching, pooling, or elimination
4. **Implement fix** - Make minimal targeted change
5. **Verify improvement** - Re-profile to confirm

### If We Find Many Small Allocations (<10 KB/sec each)

1. **Prioritize by total impact** - Sum related allocations
2. **Look for patterns** - Same issue in multiple systems?
3. **Consider systemic fix** - Shared utility or base class
4. **Document all sources** - Create optimization backlog

### If Allocations Are Framework/Library

1. **Verify it's not our usage** - Check API best practices
2. **Look for alternatives** - Different API or approach
3. **Consider trade-offs** - Performance vs. maintainability
4. **File issues** - Report to library maintainers if warranted

---

## Profiling Commands Reference

### Quick Reference Card

```bash
# 1. Monitor live GC
dotnet-counters monitor --process-id <PID> \
  --counters System.Runtime[gen-0-gc-count,alloc-rate]

# 2. Collect allocation trace
dotnet-trace collect --process-id <PID> \
  --providers Microsoft-Windows-DotNETRuntime:0x1:5 \
  --duration 00:00:30

# 3. Analyze trace
dotnet-trace report trace.nettrace --top-allocations

# 4. Find process ID
ps aux | grep PokeSharp | grep -v grep

# 5. Build release
dotnet build -c Release
```

---

## Common Allocation Patterns Cheat Sheet

### Pattern 1: LINQ Allocations
```csharp
// BAD
var results = collection.Where(x => x.IsActive).ToList();

// GOOD
var results = new List<T>(collection.Count);
foreach (var item in collection)
    if (item.IsActive) results.Add(item);
```

### Pattern 2: String Allocations
```csharp
// BAD
var key = $"{category}/{name}";

// GOOD
var key = string.Concat(category, "/", name); // Still allocates
// BETTER
private readonly Dictionary<(string, string), T> _cache; // ValueTuple key
```

### Pattern 3: Collection Allocations
```csharp
// BAD
var list = new List<T>(); // Default capacity 4, grows to 8, 16, 32...

// GOOD
var list = new List<T>(expectedSize); // Pre-allocate
// BETTER
private readonly List<T> _reusableList = new(128);
_reusableList.Clear(); // Reuse capacity
```

### Pattern 4: Boxing Allocations
```csharp
// BAD
object boxed = myStruct; // Boxes value type to heap

// GOOD
// Avoid object/interface returns for value types
// Use generics with struct constraint instead
```

### Pattern 5: Closure Allocations
```csharp
// BAD
var localVar = someField;
world.Query(query, (Entity e) => {
    DoSomething(localVar); // Captures local -> allocates closure
});

// GOOD
world.Query(query, static (Entity e) => {
    DoSomething(e); // No captures, no allocation
});
```

---

## Existing Infrastructure

### Performance Monitoring

We already have `PerformanceMonitor.cs` tracking:
- ✅ Frame times (average, min, max)
- ✅ GC counts (Gen0, Gen1, Gen2)
- ✅ Memory usage (total MB)
- ❌ Allocation rate (not yet tracked)

**Recommendation:** Enhance `PerformanceMonitor` to track allocation rate:

```csharp
// Add to LogMemoryStats():
private long _lastTotalMemory;

var allocatedBytes = totalMemoryBytes - _lastTotalMemory;
var allocRateKBPerSec = (allocatedBytes / 1024.0) / 5.0; // 5-second interval

_logger.LogInformation(
    "Memory: {Memory:F1} MB | Alloc Rate: {AllocRate:F1} KB/sec | Gen0: {Gen0}",
    totalMemoryMb, allocRateKBPerSec, gen0
);

_lastTotalMemory = totalMemoryBytes;
```

### System Performance Tracking

We have `SystemPerformanceTracker.cs` tracking:
- ✅ Per-system execution times
- ✅ Slow system warnings
- ✅ Performance statistics logging
- ❌ Per-system allocation tracking (not feasible without profiler)

---

## Next Steps After This Document

1. **Run initial profiling session** (Phase 1 + Phase 2 above)
2. **Document findings** in new file: `PHASE_3_PROFILING_RESULTS.md`
3. **Create optimization tasks** for top 5 allocation sources
4. **Implement fixes** one at a time with verification
5. **Re-profile after each fix** to measure improvement
6. **Update this document** with lessons learned

---

## Appendix: Profiling Tool Installation

### Install dotnet-trace and dotnet-counters
```bash
dotnet tool install --global dotnet-trace
dotnet tool install --global dotnet-counters

# Verify installation
dotnet-trace --version
dotnet-counters --version

# Update if already installed
dotnet tool update --global dotnet-trace
dotnet tool update --global dotnet-counters
```

### Install PerfView (Windows, optional)
```powershell
# Download from GitHub releases
https://github.com/microsoft/perfview/releases

# Or via Chocolatey
choco install perfview
```

### Install dotMemory (optional, paid)
```bash
# Download from JetBrains
https://www.jetbrains.com/dotmemory/

# Or use command-line tool
dotnet tool install --global JetBrains.dotMemory.Console
```

---

## Appendix: WSL2-Specific Considerations

### Process ID Finding
```bash
# WSL2 process (running in Linux subsystem)
ps aux | grep PokeSharp

# Windows process (if running on Windows side)
ps -W | grep PokeSharp
```

### File Paths
```bash
# Access Windows files from WSL2
/mnt/c/Users/nate0/RiderProjects/PokeSharp

# Access WSL2 files from Windows
\\wsl$\Ubuntu\home\user\...
```

### Performance Considerations
- WSL2 has near-native Linux performance
- File I/O across boundary (WSL ↔ Windows) is slower
- Keep trace files on same side as analysis tool
- dotnet-trace works natively in WSL2

---

**Document Status:** ✅ Ready for use
**Last Updated:** 2025-11-16
**Next Review:** After Phase 3 profiling session completed
