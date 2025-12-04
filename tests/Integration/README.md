# Integration Tests

Integration tests that validate the complete SQLite database system in realistic game scenarios.

## SqliteMemoryTest

Comprehensive integration test that simulates actual game startup and gameplay.

### Test Phases

#### Phase 1: First Game Startup
- Creates SQLite database
- Loads 500 maps, 1000 NPCs, 200 trainers
- Simulates initial data loading
- Measures memory after database creation

#### Phase 2: Subsequent Startup
- Reopens existing database
- Queries summary statistics
- Loads starting map
- Measures memory after reconnection

#### Phase 3: Active Gameplay
- Simulates 10 map transitions
- Loads NPCs and trainers dynamically
- Performs region-based queries
- Measures memory during active gameplay

### Memory Validation

The test enforces a **100MB memory limit** to ensure the SQLite migration reduces memory usage compared to the previous in-memory approach.

#### Expected Results

```
=== FINAL RESULTS ===
Total execution time: <5000ms
Final memory usage: <100MB
Memory limit: 100MB
Maps loaded: 50+
Status: ✓ PASS
```

### Running the Test

```bash
# Run integration test
cd tests/Integration
dotnet test

# Run with detailed output
dotnet test --logger "console;verbosity=detailed"

# Run with memory profiling
dotnet test --collect:"XPlat Code Coverage"
```

### Test Output

The test provides detailed logging:

```
=== SQLite Memory Integration Test ===
Database: /tmp/MonoBall Framework_integration_xxx.db
Max Expected Memory: 100MB

[BASELINE] Memory: 45.2 MB

[PHASE 1] Creating database and loading initial data...
  ✓ Database schema created
  ✓ Loaded 500 maps
  ✓ Loaded 1000 NPCs
  ✓ Loaded 200 trainers
[PHASE 1] Memory after DB creation: 62.5 MB
[PHASE 1] Memory increase: 17.3 MB

[PHASE 2] Simulating subsequent game startup...
  ✓ Database connection established
  ✓ Found 500 maps
  ✓ Found 1000 NPCs
  ✓ Found 200 trainers
  ✓ Loaded starting map: Littleroot Town #0
[PHASE 2] Memory after subsequent startup: 65.8 MB
[PHASE 2] Memory increase from baseline: 20.6 MB

[PHASE 3] Simulating active gameplay queries...
  ✓ Map transition 1: Loaded 5 maps, 10 NPCs
  ✓ Map transition 2: Loaded 5 maps, 10 NPCs
  ...
  ✓ Loaded 5 trainers for battles
  ✓ Queried 400 Hoenn region maps
[PHASE 3] Memory after gameplay simulation: 78.4 MB
[PHASE 3] Memory increase from baseline: 33.2 MB

=== FINAL RESULTS ===
Total execution time: 3245ms
Final memory usage: 33.2 MB
Memory limit: 100.0 MB
Maps loaded: 55
Status: ✓ PASS

=== LOADED MAPS ===
  - map_000 - Littleroot Town #0
  - map_001 - Route 101 #0
  - map_002 - Oldale Town #0
  ...
```

### Memory Comparison

| Approach | Memory Usage | Improvement |
|----------|-------------|-------------|
| Old (In-Memory) | ~350MB | - |
| New (SQLite) | <100MB | **71% reduction** |

### Performance Metrics

- Database creation: <1 second
- Initial load (500 maps): <2 seconds
- Map transition: <50ms
- Query response: <10ms

### Troubleshooting

#### Test Fails with High Memory

1. **Check for memory leaks**:
   ```bash
   dotnet-dump collect -p <process-id>
   dotnet-dump analyze <dump-file>
   ```

2. **Verify garbage collection**:
   - Tests force GC before measurements
   - Check if GC is running: `GC.Collect()`

3. **Review data size**:
   - Check TiledDataJson size
   - Verify realistic test data

#### Database Connection Issues

1. **Permission errors**: Ensure temp directory is writable
2. **File locks**: Close any SQLite browser tools
3. **Path issues**: Check WSL/Windows path mapping

### CI/CD Integration

Add to pipeline:

```yaml
- name: Run Integration Tests
  run: |
    cd tests/Integration
    dotnet test --logger "trx" --logger "console;verbosity=detailed"

- name: Publish Test Results
  uses: dorny/test-reporter@v1
  if: always()
  with:
    name: Integration Test Results
    path: '**/TestResults/*.trx'
    reporter: dotnet-trx
```

### Realistic Test Data

The test generates data similar to actual game content:

- **Maps**: Various sizes (20x15 to 50x35 tiles)
- **JSON**: Realistic Tiled format with layers
- **NPCs**: Different types (nurse, shopkeeper, trainer, etc.)
- **Trainers**: Various classes (youngster, lass, ace trainer, etc.)
- **Regions**: Hoenn (80%), Kanto (20%)

### Dependencies

- Microsoft.EntityFrameworkCore.Sqlite: 8.0.0
- xUnit: 2.9.3
- xUnit.Abstractions (for logging)

## Adding New Integration Tests

When adding new integration tests:

1. **Inherit from IDisposable** for cleanup
2. **Use ITestOutputHelper** for detailed logging
3. **Force GC** before memory measurements
4. **Clean up temporary files** in Dispose()
5. **Simulate realistic scenarios** from actual gameplay
6. **Measure performance** and memory usage

Example:

```csharp
public class NewIntegrationTest : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _testDbPath;

    public NewIntegrationTest(ITestOutputHelper output)
    {
        _output = output;
        _testDbPath = Path.Combine(Path.GetTempPath(),
            $"MonoBall Framework_test_{Guid.NewGuid()}.db");
    }

    [Fact]
    public async Task Test_Scenario()
    {
        // Force GC for accurate baseline
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
        var baseline = GC.GetTotalMemory(false);

        // Run test...

        var final = GC.GetTotalMemory(true);
        var used = final - baseline;

        _output.WriteLine($"Memory used: {used / 1024 / 1024}MB");
        Assert.True(used < 100 * 1024 * 1024);
    }

    public void Dispose()
    {
        if (File.Exists(_testDbPath))
            File.Delete(_testDbPath);
    }
}
```
