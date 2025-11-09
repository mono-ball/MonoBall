# PokeSharp ECS Performance Benchmark Guide

## Overview

This guide covers the comprehensive performance benchmark suite for PokeSharp's ECS (Entity Component System) architecture. The benchmarks measure critical performance metrics across entity creation, query execution, system updates, memory allocation, and spatial hashing.

## Quick Start

### Running Benchmarks

```bash
# Navigate to benchmark project
cd tests/PokeSharp.Benchmarks

# Run all benchmarks (IMPORTANT: Use Release configuration!)
dotnet run -c Release -- all

# Run specific benchmark suite
dotnet run -c Release -- entity
dotnet run -c Release -- query
dotnet run -c Release -- system
dotnet run -c Release -- memory
dotnet run -c Release -- spatial

# Show help
dotnet run -c Release -- help
```

### Why Release Configuration?

**CRITICAL:** Always use `-c Release` configuration for benchmarks:
- **Debug builds** include extra checks, assertions, and no optimizations
- **Release builds** represent actual production performance
- Results from Debug builds are **meaningless** and **misleading**

## Benchmark Suites

### 1. Entity Creation Benchmarks (`EntityCreationBenchmarks`)

**Purpose:** Measures performance of entity creation with various component combinations.

**Scenarios:**
- Single component entity (Position only) - **Baseline**
- Two component entity (Position + Sprite)
- Three component entity (Position + GridMovement + Sprite)
- Full component entity (Position + GridMovement + Sprite + Animation + Collision)
- Batch creation (100, 1000 entities)

**Key Metrics:**
- **Mean Time:** Average time per operation
- **Allocated Memory:** Memory allocated per operation
- **Gen 0/1/2 Collections:** Garbage collection pressure

**Expected Results (Baseline):**
```
Single Entity Creation:        ~50-100 ns
100 Entity Batch:              ~5-10 μs
1000 Entity Batch:             ~50-100 μs
```

**Usage:**
```bash
dotnet run -c Release -- entity
```

---

### 2. Query Performance Benchmarks (`QueryBenchmarks`)

**Purpose:** Tests ECS query execution speed at different scales (100, 1000, 10000 entities).

**Scenarios:**
- Single component query (Position) - **Baseline**
- Two component query (Position + GridMovement)
- Three component query (Position + GridMovement + Animation)
- Query with exclusion filter (WithNone)
- Multiple sequential queries (realistic game loop)

**Key Metrics:**
- **Query Execution Time:** Time to iterate all matching entities
- **Scaling Factor:** Performance at 100 vs 1000 vs 10000 entities
- **Zero-Allocation Validation:** Confirms cached queries allocate nothing

**Expected Results:**
```
100 Entities:
- Single component:     ~1-2 μs
- Two components:       ~1-3 μs
- Three components:     ~2-4 μs

1000 Entities:
- Single component:     ~10-20 μs
- Two components:       ~15-25 μs
- Three components:     ~20-30 μs

10000 Entities:
- Single component:     ~100-200 μs
- Two components:       ~150-250 μs
- Three components:     ~200-300 μs
```

**Usage:**
```bash
# Run query benchmarks
dotnet run -c Release -- query

# Run only with 1000 entities
dotnet run -c Release -- query --filter *1000*
```

---

### 3. System Update Benchmarks (`SystemBenchmarks`)

**Purpose:** Measures system update performance and game loop simulation.

**Scenarios:**
- Single frame update (all systems) - **Baseline**
- 10 frame update
- Individual system updates (Movement, Collision, SpatialHash, Animation)
- 60 FPS game loop (1 second)
- Variable framerate simulation (frame spikes)

**Key Metrics:**
- **Frame Time:** Time per frame update
- **System Overhead:** Time spent in system coordination
- **Sustained Performance:** Performance over multiple frames

**Expected Results:**
```
100 Entities:
- Single frame (all systems):    ~50-100 μs
- 60 frames (1 second):          ~3-6 ms

1000 Entities:
- Single frame (all systems):    ~200-400 μs
- 60 frames (1 second):          ~12-24 ms
```

**Target:** 60 FPS = 16.67ms budget per frame
- With 1000 entities, system updates should take < 1ms
- Leaves 15ms for rendering, input, and other systems

**Usage:**
```bash
dotnet run -c Release -- system
```

---

### 4. Memory Allocation Benchmarks (`MemoryAllocationBenchmarks`)

**Purpose:** Tracks memory allocation patterns and garbage collection pressure.

**Scenarios:**
- Cached query (zero-allocation) - **Baseline**
- Entity creation allocations
- Component addition allocations
- Query with closure capture (potential allocation)
- Entity destruction allocations

**Key Metrics:**
- **Allocated Bytes:** Total memory allocated per operation
- **Gen 0 Collections:** Frequency of GC gen 0 collections
- **Zero-Allocation Validation:** Confirms hot paths allocate nothing

**Expected Results:**
```
Cached Query (zero-alloc):      0 B allocated
Entity Creation (100):          ~1-5 KB allocated
Component Addition:             ~500-2000 B allocated
Multiple Queries:               0-100 B allocated
```

**Goals:**
- Hot paths (queries, system updates) should be **zero-allocation**
- Entity creation allocations should be **minimal**
- No GC collections during normal gameplay

**Usage:**
```bash
dotnet run -c Release -- memory
```

---

### 5. Spatial Hash Benchmarks (`SpatialHashBenchmarks`)

**Purpose:** Tests spatial hash system performance for entity lookup and collision detection.

**Scenarios:**
- Spatial hash rebuild - **Baseline**
- Point queries (entities at position)
- Area queries (entities in rectangle)
- Nearest entity search
- Collision detection (walkability checks)
- Movement simulation with hash updates

**Key Metrics:**
- **Hash Build Time:** Time to rebuild spatial hash
- **Query Time:** Time for spatial lookups
- **Collision Check Time:** Time to validate walkability

**Expected Results:**
```
100 Entities:
- Hash update:              ~10-20 μs
- 100 point queries:        ~5-10 μs
- 100 walkable checks:      ~8-15 μs

1000 Entities:
- Hash update:              ~50-100 μs
- 100 point queries:        ~8-15 μs
- 100 walkable checks:      ~12-20 μs
```

**Usage:**
```bash
dotnet run -c Release -- spatial
```

---

## Interpreting Results

### BenchmarkDotNet Output Format

```
| Method               | EntityCount | Mean      | Error    | StdDev   | Gen 0 | Allocated |
|-------------------- |------------ |----------:|---------:|---------:|------:|----------:|
| QuerySingleComp     | 100         | 1.234 μs  | 0.023 μs | 0.021 μs |     - |         - |
| QueryTwoComponents  | 100         | 1.567 μs  | 0.031 μs | 0.029 μs |     - |         - |
```

**Columns:**
- **Method:** Benchmark method name
- **EntityCount:** Parameter value (number of entities)
- **Mean:** Average execution time
- **Error:** Standard error of the mean
- **StdDev:** Standard deviation
- **Gen 0/1/2:** Number of GC collections per 1000 operations
- **Allocated:** Memory allocated per operation

### Performance Targets

#### Frame Budget (60 FPS = 16.67ms)

```
System Updates:         < 1ms   (6% of budget)
Rendering:             < 8ms   (48% of budget)
Input Processing:      < 1ms   (6% of budget)
Physics/Collision:     < 2ms   (12% of budget)
AI/Game Logic:         < 2ms   (12% of budget)
Buffer:                ~2.67ms (16% of budget)
```

#### Entity Scale Targets

```
Small Game (100 entities):
- Frame time: < 1ms
- Query time: < 10 μs per query
- Hash update: < 20 μs

Medium Game (1000 entities):
- Frame time: < 5ms
- Query time: < 50 μs per query
- Hash update: < 100 μs

Large Game (10000 entities):
- Frame time: < 15ms
- Query time: < 500 μs per query
- Hash update: < 1ms
```

#### Memory Allocation Targets

```
Zero Allocation (per frame):
- ECS queries
- System updates
- Spatial hash queries

Minimal Allocation:
- Entity creation: < 1 KB per entity
- Component addition: < 500 B per component

No GC During Gameplay:
- Gen 0: < 1 per second
- Gen 1/2: Never during gameplay
```

---

## Regression Detection

### Establishing Baselines

After running benchmarks, save results as baseline:

```bash
# Run benchmarks and export results
dotnet run -c Release -- all --exporters json

# Results saved to: BenchmarkDotNet.Artifacts/results/
```

**Baseline Files:**
```
BenchmarkDotNet.Artifacts/
└── results/
    ├── EntityCreationBenchmarks-report.json
    ├── QueryBenchmarks-report.json
    ├── SystemBenchmarks-report.json
    ├── MemoryAllocationBenchmarks-report.json
    └── SpatialHashBenchmarks-report.json
```

### Comparing Results

```bash
# Run benchmarks with baseline comparison
dotnet run -c Release -- all --baseline Baseline

# Or use ResultsComparer tool
dotnet tool install -g BenchmarkDotNet.ResultsComparer
dotnet results-comparer compare --base baseline.json --diff current.json
```

### Regression Thresholds

**Performance Regression:**
- **Critical:** > 50% slowdown (requires immediate investigation)
- **High:** 25-50% slowdown (investigate before merge)
- **Medium:** 10-25% slowdown (review and document)
- **Low:** < 10% slowdown (acceptable variance)

**Memory Regression:**
- **Critical:** New allocations in zero-alloc paths
- **High:** > 2x memory allocation increase
- **Medium:** 50-100% allocation increase
- **Low:** < 50% allocation increase

---

## Best Practices

### When to Run Benchmarks

**Always Benchmark:**
- After ECS system changes
- After query optimization
- Before performance-critical PRs
- After Arch.Core version updates
- When investigating performance issues

**Periodic Benchmarking:**
- Weekly (CI automation)
- Before major releases
- After adding new systems

### Benchmark Development Tips

1. **Use [MemoryDiagnoser]** to track allocations
2. **Use [Params]** to test multiple scales
3. **Use [Baseline]** to establish reference points
4. **Clean up state** in IterationCleanup
5. **Test realistic scenarios** (game loops, not synthetic)
6. **Document expected results** in code comments

### Performance Optimization Workflow

1. **Measure First** - Run benchmarks to establish baseline
2. **Identify Bottlenecks** - Find slowest operations
3. **Optimize** - Make targeted improvements
4. **Measure Again** - Verify improvements
5. **Compare** - Check for regressions in other areas
6. **Document** - Record baseline and changes

---

## CI Integration

### GitHub Actions Workflow

```yaml
name: Performance Benchmarks

on:
  pull_request:
    paths:
      - 'PokeSharp.Core/**'
      - 'tests/PokeSharp.Benchmarks/**'

jobs:
  benchmark:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3

      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '9.0.x'

      - name: Run Benchmarks
        run: |
          cd tests/PokeSharp.Benchmarks
          dotnet run -c Release -- all --exporters json

      - name: Upload Results
        uses: actions/upload-artifact@v3
        with:
          name: benchmark-results
          path: tests/PokeSharp.Benchmarks/BenchmarkDotNet.Artifacts/results/

      - name: Compare with Baseline
        run: |
          # Compare results with baseline (implement comparison logic)
          # Fail if critical regressions detected
```

---

## Troubleshooting

### Common Issues

**Issue: "Debug configuration detected"**
```bash
# Solution: Always use Release configuration
dotnet run -c Release -- all
```

**Issue: "Benchmark takes too long"**
```bash
# Solution: Reduce warmup/iteration count for development
# Edit BenchmarkBase.cs:
# [SimpleJob(warmupCount: 1, iterationCount: 3)]
```

**Issue: "High variance in results"**
```bash
# Solution: Close other applications
# Disable background processes
# Run multiple times and average results
```

**Issue: "Memory allocations detected in zero-alloc path"**
```bash
# Solution: Check for:
# - Lambda closure captures
# - LINQ queries (use foreach instead)
# - String formatting (use ValueStringBuilder)
# - Temporary collections (reuse or use ArrayPool)
```

---

## Advanced Topics

### Custom Benchmark Creation

```csharp
using BenchmarkDotNet.Attributes;

[MemoryDiagnoser]
public class MyCustomBenchmark : BenchmarkBase
{
    [Params(100, 1000)]
    public int EntityCount;

    [GlobalSetup]
    public override void Setup()
    {
        base.Setup();
        // Custom setup
    }

    [Benchmark]
    public void MyBenchmark()
    {
        // Benchmark code
    }
}
```

### Profiling with dotnet-trace

```bash
# Install dotnet-trace
dotnet tool install --global dotnet-trace

# Profile benchmark execution
dotnet trace collect --process-id <pid> --providers Microsoft-DotNETCore-SampleProfiler

# Open trace in Visual Studio or PerfView
```

### Memory Profiling with dotnet-counters

```bash
# Install dotnet-counters
dotnet tool install --global dotnet-counters

# Monitor GC and memory
dotnet counters monitor --process-id <pid> System.Runtime
```

---

## Baseline Results

### Current Baseline (v0.1.0 - Initial Implementation)

**Test Configuration:**
- CPU: [To be recorded]
- OS: [To be recorded]
- .NET: 9.0
- Arch.Core: [Version]

**Entity Creation (100 entities):**
```
Single Component:       [TBD] μs
Two Components:         [TBD] μs
Full Components:        [TBD] μs
```

**Query Performance (1000 entities):**
```
Single Component:       [TBD] μs
Two Components:         [TBD] μs
Three Components:       [TBD] μs
```

**System Updates (1000 entities):**
```
One Frame:             [TBD] μs
60 Frames:             [TBD] ms
```

**Spatial Hash (1000 entities):**
```
Hash Update:           [TBD] μs
100 Queries:           [TBD] μs
```

> **Note:** Run benchmarks and record baselines after initial implementation.

---

## Resources

- **BenchmarkDotNet Docs:** https://benchmarkdotnet.org/
- **Arch ECS Docs:** https://github.com/genaray/Arch
- **Performance Best Practices:** https://learn.microsoft.com/en-us/dotnet/core/diagnostics/
- **ECS Performance Patterns:** https://github.com/SanderMertens/ecs-faq

---

## Continuous Improvement

This benchmark suite is a **living system**:
- Add new benchmarks as new systems are implemented
- Update baselines after optimizations
- Track performance trends over time
- Document optimization techniques that work

**Remember:**
> "Premature optimization is the root of all evil, but **measurement** is the root of all optimization."
> — Donald Knuth (paraphrased)

Always **measure before** and **measure after** any performance work.
