# MonoBall Framework Performance Benchmarks

## Quick Start

```bash
cd /Users/ntomsic/Documents/MonoBall Framework/tests/PerformanceBenchmarks
dotnet run -c Release
```

## What This Tests

1. **Entity Pooling**: Create/destroy 10K entities with and without pooling
2. **Query Performance**: Update 50K entities with Position + GridMovement components
3. **System Execution**: Run movement and render prep systems
4. **GC Behavior**: Create 100K entities and run 10 query cycles

## Latest Results

```
Entity Pooling:      1.71x faster (7ms vs 12ms for 10K entities)
Query Throughput:    25M entities/second
System Execution:    2ms for 50K entities
GC Collections:      0 (zero pressure)
Memory Per Entity:   ~65 bytes
Frame Budget:        12% used (88% headroom)
```

## Reports

- **Detailed Analysis**: `/docs/performance/PHASE-4-BASELINE-PERFORMANCE.md`
- **Quick Summary**: `/docs/performance/PERFORMANCE-SUMMARY.md`
- **Parallel Execution**: `/MonoBall Framework.Core/tests/ParallelExecutionValidationReport.md`

## Performance Targets

| Metric | Target | Actual | Status |
|--------|--------|--------|--------|
| Entity Pooling | 1.5-2x | 1.71x | ✅ |
| Frame Time | <16.67ms | 2ms | ✅ |
| GC Collections | <5 Gen0 | 0 | ✅ |
| Memory/Entity | <100 bytes | 65 bytes | ✅ |

## Conclusion

**All performance targets exceeded. Engine is production-ready.**
