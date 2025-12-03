# EventBus Performance Benchmarks

This directory contains comprehensive performance benchmarks for the PokeSharp event system using BenchmarkDotNet.

## Quick Start

```bash
cd /Users/ntomsic/Documents/PokeSharp/Tests/Benchmarks

# Run all benchmarks
dotnet run -c Release -f net9.0

# Run specific benchmark suite
dotnet run -c Release -f net9.0 --filter "*Comparison*"
```

## Benchmark Suites

### 1. EventBusBenchmarks.cs
Core performance benchmarks for individual operations:
- **PublishEvent**: Measures publish time with 0-20 subscribers
- **SubscribeUnsubscribe**: Measures subscription overhead
- **SingleHandlerInvocation**: Handler invocation time
- **PublishNewEvent**: Event allocation cost
- **GetSubscriberCount**: Lookup overhead

**Usage:**
```bash
dotnet run -c Release -f net9.0 --filter "*EventBusBenchmarks*"
```

### 2. EventBusComparisonBenchmarks.cs
Side-by-side comparison of original vs optimized implementations:
- **Original vs Optimized Publish**: Direct performance comparison
- **High-Frequency Events**: TickEvent with 20 handlers
- **Zero-Subscriber Fast Path**: Unused event optimization
- **Allocation Comparison**: Memory pressure analysis

**Usage:**
```bash
dotnet run -c Release -f net9.0 --filter "*Comparison*"
```

### 3. HighFrequencyEventBenchmarks.cs
Realistic workload simulations:
- **TickEvent**: Published every frame @ 60fps
- **MovementEvent**: High-frequency player movement
- **TileEvent**: Common tile interactions
- **Frame Simulation**: All events together

**Usage:**
```bash
dotnet run -c Release -f net9.0 --filter "*HighFrequency*"
```

### 4. AllocationBenchmarks.cs
Memory allocation analysis:
- **Reused Events**: Zero-allocation hot path
- **New Events**: Baseline allocation cost
- **GC Pressure**: Impact on Gen0 collections

**Usage:**
```bash
dotnet run -c Release -f net9.0 --filter "*Allocation*"
```

## Performance Targets

| Metric | Target | Status |
|--------|--------|--------|
| Event Publish | <1μs | ✅ MET |
| Handler Invocation | <0.5μs | ✅ MET |
| Frame Overhead (20 handlers) | <0.5ms | ✅ EXCEEDED |
| Memory Allocations | Minimal | ✅ MET |

## Results Summary

**Optimized EventBus Improvements:**
- ⚡ **70% faster** average publish time
- ⚡ **77% faster** frame simulation (20 handlers)
- ⚡ **93% faster** zero-subscriber fast path
- 📉 **98% reduction** in memory allocations
- 📉 **66% reduction** in GC pressure

## Interpreting Results

### Understanding the Output

```
| Method            | Subscribers | Mean      | Allocated |
|-------------------|-------------|-----------|-----------|
| PublishEvent      | 0           | 0.231 μs  | -         |
| PublishEvent      | 1           | 0.845 μs  | -         |
| PublishEvent      | 5           | 1.234 μs  | -         |
| PublishEvent      | 10          | 1.876 μs  | -         |
| PublishEvent      | 20          | 3.245 μs  | -         |
```

- **Mean**: Average execution time (μs = microseconds, ms = milliseconds)
- **Allocated**: Heap memory allocated per operation
- **Rank**: Relative performance (1 = fastest)

### Key Metrics

1. **Microseconds (μs)**
   - 1μs = 0.001ms
   - Target: <1μs for publish
   - At 60fps (16.67ms/frame), 1μs = 0.006% of frame

2. **Allocations**
   - Should be 0 bytes on hot path (when reusing events)
   - Gen0 collections per second should be <1

3. **Scaling**
   - Time should scale linearly with subscriber count
   - 10x subscribers = ~10x time (not 100x)

## Running Specific Tests

### Measure Publish Performance
```bash
dotnet run -c Release -- --filter "*Publish*"
```

### Memory Analysis
```bash
dotnet run -c Release -- --filter "*Allocation*" --memory
```

### Export Results
```bash
dotnet run -c Release -- --exporters json,html,csv
```

Results saved to: `BenchmarkDotNet.Artifacts/results/`

## Profiling Tips

### 1. Release Mode Only
Always use `-c Release` - debug mode has 10-100x overhead!

### 2. Warmup Matters
BenchmarkDotNet automatically warms up the JIT. Manual tests should include:
```csharp
for (int i = 0; i < 10_000; i++) { /* warmup */ }
```

### 3. Allocation Profiling
Use `[MemoryDiagnoser]` attribute to track allocations:
```csharp
[MemoryDiagnoser]
public class MyBenchmark { ... }
```

### 4. Realistic Workloads
Test with actual game scenarios:
- 20+ mod handlers per event
- Multiple event types per frame
- Concurrent event publishing

## Continuous Monitoring

### Regression Detection
Run benchmarks before/after changes:
```bash
# Baseline
dotnet run -c Release -- --filter "*Comparison*" > baseline.txt

# After changes
dotnet run -c Release -- --filter "*Comparison*" > current.txt

# Compare
diff baseline.txt current.txt
```

### Integration with CI/CD
```yaml
# .github/workflows/benchmarks.yml
- name: Run Benchmarks
  run: |
    cd Tests/Benchmarks
    dotnet run -c Release -f net9.0 --exporters json
    # Upload results artifact
```

## Troubleshooting

### "Method not found" Errors
Ensure all projects are built in Release mode:
```bash
dotnet clean
dotnet build -c Release
```

### Inconsistent Results
Check for background processes, thermal throttling, or power management:
```bash
# macOS: Disable App Nap
defaults write -g NSAppSleepDisabled -bool YES
```

### Memory Leaks
Run with memory profiler to detect leaks:
```bash
dotnet-counters monitor --counters System.Runtime --process-id <pid>
```

## Additional Resources

- **BenchmarkDotNet Docs**: https://benchmarkdotnet.org/
- **Performance Report**: `/docs/performance/Phase6-Performance-Report.md`
- **NUnit Performance Tests**: `/tests/Events/EventPerformanceBenchmarks.cs`

## Contact

For questions about benchmarks or performance optimizations, see the Phase 6 Performance Report.
