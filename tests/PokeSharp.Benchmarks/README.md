# PokeSharp ECS Performance Benchmarks

Comprehensive benchmark suite for measuring ECS performance using BenchmarkDotNet.

## Quick Start

```bash
# Navigate to benchmark project
cd tests/PokeSharp.Benchmarks

# Run all benchmarks (REQUIRED: Release configuration!)
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

## Benchmark Suites

### 1. EntityCreationBenchmarks
- Single entity creation with varying component counts
- Batch creation (100, 1000 entities)
- Tests entity creation overhead and memory allocation

### 2. QueryBenchmarks
- Query performance at 100, 1000, and 10,000 entity scales
- Single, two, and three component queries
- Queries with exclusion filters (WithNone)
- Multiple sequential queries (realistic game loop)
- Tests centralized query cache effectiveness

### 3. SystemBenchmarks
- System update performance (100, 1000 entities)
- Individual system benchmarks (Movement, Collision, SpatialHash, Animation)
- Full game loop simulation (60 FPS over 1 second)
- Frame spike simulation (variable framerate)

### 4. MemoryAllocationBenchmarks
- Zero-allocation query verification
- Entity creation allocations
- Component addition allocations
- Lambda closure capture allocations
- GC pressure measurement

### 5. SpatialHashBenchmarks
- Spatial hash rebuild performance (100, 500, 1000 entities)
- Point queries (entities at position)
- Area queries (entities in bounds)
- Movement simulation with hash updates
- Realistic collision detection scenarios

## Results Location

After running benchmarks, results are saved to:
```
tests/PokeSharp.Benchmarks/BenchmarkDotNet.Artifacts/results/
```

## Documentation

For comprehensive usage, interpretation, and optimization guidance, see:
```
docs/performance/BENCHMARK_GUIDE.md
```

## Performance Targets

**60 FPS Frame Budget: 16.67ms**
- System updates: < 1ms (6% of budget)
- Query operations: < 50μs per query (1000 entities)
- Entity creation: < 10μs per entity
- Zero allocations in hot paths

## CI Integration

Benchmarks should be run:
- Before performance-critical PRs
- After ECS system changes
- Weekly for regression detection
- Before major releases

## Notes

- **Always use `-c Release`** - Debug builds are meaningless for benchmarks
- Results vary by hardware - establish baselines on target hardware
- Run multiple times for statistical significance
- Warm up period minimizes JIT compilation variance
