# PokeSharp Performance Profiling Documentation

**Last Updated:** 2025-11-16
**Status:** Phase 3 - Profiling Strategy Ready

---

## Overview

This directory contains documentation for identifying and eliminating memory allocations that cause excessive GC pressure in PokeSharp.

### Current Status

```
🔴 CRITICAL GC Pressure:
├─ Gen0 GC rate: 46.8 collections/sec (23x higher than normal)
├─ Gen2 GC rate: 14.6 collections/sec (should be near zero)
├─ Allocation rate: ~750 KB/sec (normal is ~100 KB/sec)
└─ Per-frame budget: 12.5 KB @ 60fps (normal is ~500 bytes)

✅ Completed Optimizations:
├─ Phase 1: String caching, reusable collections, LINQ elimination
└─ Phase 2: HashSet → bit field, Direction.ToString() caching

🎯 Remaining Work:
├─ Phase 3: Profile to find mystery 300-400 KB/sec allocations
└─ Phase 4: Implement fixes and verify improvements
```

---

## Document Index

### 1. [PHASE_3_PROFILING_STRATEGY.md](./PHASE_3_PROFILING_STRATEGY.md)
**Purpose:** Complete methodology for profiling allocations
**Contents:**
- Tool selection and installation (dotnet-trace, dotnet-counters, PerfView)
- Step-by-step profiling workflow
- Analysis techniques
- Common allocation patterns and fixes
- Expected results and success criteria

**Use this when:** Starting a profiling session

---

### 2. [INVESTIGATION_TARGETS.md](./INVESTIGATION_TARGETS.md)
**Purpose:** Specific files and code patterns to investigate
**Contents:**
- System-by-system priority list
- Known LINQ allocation locations
- Collection growth suspects
- Logging allocation sources
- Expected allocation breakdown

**Use this when:** Planning what to profile first

---

### 3. [SpriteAnimationSystem-Analysis.md](./SpriteAnimationSystem-Analysis.md)
**Purpose:** Detailed analysis of Phase 1 optimizations
**Contents:**
- Complete update flow diagram
- String allocation analysis (Line 76 - now fixed)
- HashSet.Clear() analysis (Line 132 - now fixed)
- Before/after performance metrics

**Use this when:** Reference for optimization approach

---

### 4. GC_PRESSURE_CRITICAL_ANALYSIS.md (in /docs/)
**Purpose:** Root cause analysis of GC pressure
**Contents:**
- Severity assessment (23x worse than normal)
- Known vs. mystery allocation breakdown
- Diagnostic strategy
- Performance impact calculations

**Use this when:** Understanding the big picture

---

### 5. FIND_MYSTERY_ALLOCATIONS.md (in /docs/)
**Purpose:** Quick reference for finding allocations
**Contents:**
- dotnet-trace commands
- Visual Studio profiler steps
- Manual code search patterns
- Expected culprits list

**Use this when:** Quick profiling refresher

---

## Quick Start Guide

### New to Profiling?

**Start here:** Read documents in this order:
1. This README (you are here)
2. [PHASE_3_PROFILING_STRATEGY.md](./PHASE_3_PROFILING_STRATEGY.md) - Full methodology
3. [INVESTIGATION_TARGETS.md](./INVESTIGATION_TARGETS.md) - What to look for

### Ready to Profile?

**Run this profiling session:**

```bash
# 1. Build in Release mode
cd /mnt/c/Users/nate0/RiderProjects/PokeSharp
dotnet build -c Release

# 2. Start the game
cd PokeSharp.Game
dotnet run --configuration Release

# 3. In another terminal, find PID
ps aux | grep PokeSharp | grep -v grep

# 4. Monitor live (run for 60 seconds)
dotnet-counters monitor --process-id <PID> \
  --counters System.Runtime[gen-0-gc-count,alloc-rate]

# 5. Collect detailed trace
dotnet-trace collect --process-id <PID> \
  --providers Microsoft-Windows-DotNETRuntime:0x1:5 \
  --duration 00:00:30

# 6. Analyze results
dotnet-trace report pokesharp-allocations.nettrace --top-allocations
```

See [PHASE_3_PROFILING_STRATEGY.md](./PHASE_3_PROFILING_STRATEGY.md) for detailed instructions.

---

## Optimization History

### ✅ Phase 1: Low-Hanging Fruit (Completed)

**Date:** 2025-11-15 to 2025-11-16

| Optimization | File | Impact | Status |
|--------------|------|--------|--------|
| String caching (ManifestKey) | SpriteAnimationSystem.cs | ~192-384 KB/sec | ✅ DONE |
| Reusable collections | RelationshipSystem.cs | ~30-50 KB/sec | ✅ DONE |
| LINQ elimination | SystemPerformanceTracker.cs | ~5-10 KB/sec | ✅ DONE |

**Total Phase 1 Savings:** ~230-450 KB/sec

---

### ✅ Phase 2: Additional Optimizations (Completed)

**Date:** 2025-11-16

| Optimization | File | Impact | Status |
|--------------|------|--------|--------|
| HashSet → ulong bit field | Animation.cs | ~6.4 KB/sec | ✅ DONE |
| Direction.ToString() cache | MovementSystem.cs | ~2-5 KB/sec | ✅ DONE |

**Total Phase 2 Savings:** ~8-11 KB/sec

---

### 🔄 Phase 3: Profiling Mystery Allocations (In Progress)

**Date:** 2025-11-16 onwards

**Goal:** Find and eliminate remaining ~300-400 KB/sec

**Expected Sources:**
- SystemManager LINQ (~70 KB/sec) - Known, not yet fixed
- PathfindingSystem LINQ (~60 KB/sec) - Suspected
- World.Query closures (~30 KB/sec) - Suspected
- Logging in hot paths (~50 KB/sec) - If enabled
- Collection growth (~25 KB/sec) - Occasional
- Framework/library allocations (~50-100 KB/sec) - TBD

**Next Steps:**
1. Run profiling session (see Quick Start above)
2. Document findings in PHASE_3_PROFILING_RESULTS.md
3. Implement top 3 fixes
4. Verify improvements

---

## Known Issues Still to Fix

### 🔴 High Priority (>50 KB/sec impact)

#### SystemManager LINQ Allocations
**File:** `/PokeSharp.Engine.Systems/Management/SystemManager.cs`
**Lines:** 232 (Update), 275 (Render)
**Issue:** `.Where(s => s.Enabled).ToArray()` runs 120x/sec
**Impact:** ~70 KB/sec
**Fix:** Cache enabled systems, only rebuild on enable/disable
**Effort:** ~30 minutes
**Status:** ⏳ Not started

---

### 🟠 Medium Priority (20-50 KB/sec impact)

#### PathfindingSystem LINQ (Suspected)
**File:** `/PokeSharp.Game.Systems/NPCs/PathfindingSystem.cs`
**Issue:** LINQ detected via grep, needs manual review
**Impact:** ~60 KB/sec (estimated)
**Fix:** TBD after profiling
**Effort:** TBD
**Status:** 🔍 Needs profiling

#### Logging in Hot Paths
**Files:** Multiple systems
**Issue:** Debug/trace logging without LogLevel guards
**Impact:** ~30-50 KB/sec (if enabled)
**Fix:** Add LogLevel guards or remove from hot paths
**Effort:** ~1 hour (multiple files)
**Status:** 🔍 Needs profiling to confirm

---

### 🟡 Low Priority (<20 KB/sec impact)

These will be addressed based on profiling results.

---

## Performance Targets

### Current (Before Phase 3)
```
Gen0 GC:         ~46.8 collections/sec
Gen2 GC:         ~14.6 collections/sec
Allocation:      ~750 KB/sec
Frame budget:    ~12.5 KB @ 60fps
```

### Target (After All Optimizations)
```
Gen0 GC:         5-8 collections/sec       ✅ 82-89% reduction
Gen2 GC:         0-1 collections/sec       ✅ 93-100% reduction
Allocation:      80-130 KB/sec             ✅ 83-89% reduction
Frame budget:    1.3-2.2 KB @ 60fps        ✅ 82-89% reduction
```

### Acceptable Range for 60fps Game
```
Gen0 GC:         1-10 collections/sec      (10-15 KB/sec allocation)
Gen2 GC:         0-2 collections/sec       (rare, <1/minute ideal)
Allocation:      <150 KB/sec               (<2.5 KB per frame)
Frame budget:    <3 KB @ 60fps             (leaves room for GC overhead)
```

---

## Profiling Tools Reference

### Essential Tools (Cross-Platform)
- **dotnet-trace** - Allocation profiling
- **dotnet-counters** - Real-time GC monitoring

### Optional Tools
- **PerfView** - Deep analysis (Windows only)
- **dotMemory** - Visual profiling (paid)
- **VS Profiler** - IDE integration (Windows only)

### Installation
```bash
dotnet tool install --global dotnet-trace
dotnet tool install --global dotnet-counters
```

See [PHASE_3_PROFILING_STRATEGY.md](./PHASE_3_PROFILING_STRATEGY.md) for full installation guide.

---

## Contributing to Profiling Docs

### When Adding New Optimizations

1. **Document the finding** - What was allocating, where, how much
2. **Explain the fix** - What changed, why it works
3. **Measure the impact** - Before/after GC rates
4. **Update this README** - Add to optimization history

### Document Template

Create new file: `docs/profiling/[SystemName]-Optimization.md`

```markdown
# [System Name] Optimization

**Date:** YYYY-MM-DD
**File:** /path/to/file.cs
**Issue:** Brief description of allocation source
**Impact:** XX KB/sec reduction

## Problem
[Detailed explanation of what was allocating]

## Solution
[What was changed and why]

## Results
Before:
- Allocation rate: XX KB/sec
- Gen0 GC: YY/sec

After:
- Allocation rate: XX KB/sec (-ZZ%)
- Gen0 GC: YY/sec (-ZZ%)

## Code Changes
[Key code snippets showing before/after]
```

---

## Useful Commands

### Profiling Session
```bash
# Monitor live
dotnet-counters monitor -p <PID> --counters System.Runtime

# Collect trace
dotnet-trace collect -p <PID> --providers Microsoft-Windows-DotNETRuntime:0x1:5

# Analyze
dotnet-trace report trace.nettrace --top-allocations
```

### Code Analysis
```bash
# Find LINQ in systems
grep -rn "\.Where\|\.Select\|\.ToList" --include="*System*.cs" .

# Find collections in systems
grep -rn "new List\|new Dictionary" --include="*System*.cs" .

# Find logging in systems
grep -rn "Log(Information\|Debug\|Trace)" --include="*System*.cs" .
```

### Build Commands
```bash
# Release build
dotnet build -c Release

# Run release
dotnet run -c Release --project PokeSharp.Game
```

---

## FAQ

### Q: Why are we targeting 80-130 KB/sec allocation rate?

**A:** Normal 60fps games allocate 1-3 KB per frame. At 60fps:
- 1 KB/frame = 60 KB/sec
- 2 KB/frame = 120 KB/sec
- 3 KB/frame = 180 KB/sec (upper limit)

Our target of 80-130 KB/sec keeps us well within normal range with headroom for gameplay events.

### Q: What's the difference between Gen0, Gen1, and Gen2 GC?

**A:**
- **Gen0:** Young objects, collected frequently (every ~16 KB allocated)
- **Gen1:** Objects that survived 1 GC, collected less often
- **Gen2:** Long-lived objects, expensive full-heap collection

**Impact:**
- Gen0 GC: ~0.5-1ms pause (acceptable)
- Gen2 GC: ~10-50ms pause (BLOCKS ALL THREADS, causes stutters)

### Q: How do I know if my optimization worked?

**A:** Run dotnet-counters before and after:
1. Collect baseline (60 seconds)
2. Apply fix
3. Collect new metrics (60 seconds)
4. Compare Gen0 GC rate and allocation rate

Expected improvement should be visible immediately.

### Q: What if profiling doesn't show obvious allocations?

**A:** Try:
1. Longer trace duration (60 seconds instead of 30)
2. Different workload (different map, more NPCs)
3. Visual Studio profiler (more detailed)
4. PerfView (most detailed, Windows only)

### Q: Should I optimize everything to zero allocations?

**A:** No! Optimize for **impact**, not perfection:
- Fix anything >50 KB/sec (high impact)
- Consider anything >20 KB/sec (medium impact)
- Ignore anything <10 KB/sec unless trivial to fix

Focus on the 80/20 rule: 20% of code causes 80% of allocations.

---

## Support

### Internal Resources
- This profiling documentation directory
- `/docs/GC_PRESSURE_CRITICAL_ANALYSIS.md` - Root cause analysis
- `/docs/optimizations/` - Prior optimization notes

### External Resources
- [.NET Performance Best Practices](https://docs.microsoft.com/en-us/dotnet/standard/performance/)
- [dotnet-trace Documentation](https://docs.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-trace)
- [PerfView Tutorial](https://github.com/microsoft/perfview/blob/main/documentation/Tutorial.md)

---

**Document Status:** ✅ Complete and ready for use
**Next Update:** After Phase 3 profiling session
**Maintained By:** Performance optimization team
