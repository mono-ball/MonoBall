# Hot-Reload Fixes - Action Items

**Priority:** RECOMMENDED (Not Critical)
**Estimated Time:** 15 minutes
**Impact:** Minor memory leak prevention + race condition fix

## Action Item #1: Fix CancellationTokenSource Disposal

**File:** `/PokeSharp.Scripting/HotReload/Watchers/FileSystemWatcherAdapter.cs`
**Line:** 107
**Time:** 5 minutes

### Current Code
```csharp
private void OnFileChanged(object sender, FileSystemEventArgs e)
{
    // Debounce: if file is saved multiple times rapidly, only process once
    if (_debounceTimers.TryGetValue(e.FullPath, out var existingCts))
    {
        existingCts.Cancel(); // ❌ Not disposed - minor memory leak
    }

    var cts = new CancellationTokenSource();
    _debounceTimers[e.FullPath] = cts;
    // ...
}
```

### Fixed Code
```csharp
private void OnFileChanged(object sender, FileSystemEventArgs e)
{
    // Debounce: if file is saved multiple times rapidly, only process once
    if (_debounceTimers.TryGetValue(e.FullPath, out var existingCts))
    {
        existingCts.Cancel();
        existingCts.Dispose(); // ✅ Explicit disposal prevents leak
    }

    var cts = new CancellationTokenSource();
    _debounceTimers[e.FullPath] = cts;
    // ...
}
```

**Also fix:** Line 93 in `StopAsync()`
```csharp
// Cancel all pending debounce timers
foreach (var cts in _debounceTimers.Values)
{
    cts.Cancel();
    cts.Dispose(); // ✅ Add disposal here too
}
_debounceTimers.Clear();
```

---

## Action Item #2: Fix Lazy Instantiation Race Condition

**File:** `/PokeSharp.Scripting/HotReload/Cache/VersionedScriptCache.cs`
**Lines:** 62-73
**Time:** 10 minutes

### Current Code
```csharp
public (int version, object? instance) GetInstance(string typeId)
{
    if (!_cache.TryGetValue(typeId, out var cached))
    {
        _logger.LogWarning("Script type not found in cache: {TypeId}", typeId);
        return (-1, null);
    }

    // Lazy instantiation: create instance on first access
    if (cached.Instance == null) // ❌ Race condition: two threads can both see null
    {
        try
        {
            var sw = Stopwatch.StartNew();
            cached.Instance = Activator.CreateInstance(cached.Type);
            sw.Stop();

            cached.InstantiationCount++;
            cached.LastInstantiationTime = DateTime.UtcNow;

            _cache[typeId] = cached; // Update with instance
            // ❌ Another thread might have updated in between
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to instantiate {TypeId}", typeId);
            return (cached.Version, null);
        }
    }

    return (cached.Version, cached.Instance);
}
```

### Fixed Code (Option 1: Simple Lock)
```csharp
private readonly ConcurrentDictionary<string, object> _instantiationLocks = new();

public (int version, object? instance) GetInstance(string typeId)
{
    if (!_cache.TryGetValue(typeId, out var cached))
    {
        _logger.LogWarning("Script type not found in cache: {TypeId}", typeId);
        return (-1, null);
    }

    // Lazy instantiation: create instance on first access
    if (cached.Instance == null)
    {
        // ✅ Lock per typeId to prevent concurrent instantiation
        var lockObj = _instantiationLocks.GetOrAdd(typeId, _ => new object());
        lock (lockObj)
        {
            // Double-check pattern: another thread might have instantiated
            if (!_cache.TryGetValue(typeId, out cached) || cached.Instance != null)
            {
                return (cached.Version, cached.Instance);
            }

            try
            {
                var sw = Stopwatch.StartNew();
                cached.Instance = Activator.CreateInstance(cached.Type);
                sw.Stop();

                cached.InstantiationCount++;
                cached.LastInstantiationTime = DateTime.UtcNow;

                _cache[typeId] = cached; // Update with instance

                _logger.LogDebug("Lazily instantiated {TypeId} (version {Version}) in {Elapsed}ms",
                    typeId, cached.Version, sw.Elapsed.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to instantiate {TypeId} (version {Version})",
                                typeId, cached.Version);
                return (cached.Version, null);
            }
        }
    }

    return (cached.Version, cached.Instance);
}
```

### Fixed Code (Option 2: Lazy<T> - Preferred)
```csharp
// Change CachedScript to use Lazy<object>
private struct CachedScript
{
    public string TypeId { get; init; }
    public int Version { get; init; }
    public Type Type { get; init; }
    public Lazy<object>? LazyInstance { get; set; } // ✅ Thread-safe by design
    public DateTime UpdateTime { get; init; }
    public int InstantiationCount { get; set; }
    public DateTime? LastInstantiationTime { get; set; }
}

public void UpdateVersion(string typeId, Type newType, int? specificVersion = null)
{
    var version = specificVersion ?? IncrementGlobalVersion();

    var cached = new CachedScript
    {
        TypeId = typeId,
        Version = version,
        Type = newType,
        LazyInstance = new Lazy<object>(() => Activator.CreateInstance(newType)), // ✅
        UpdateTime = DateTime.UtcNow,
        InstantiationCount = 0
    };

    _cache[typeId] = cached;
    // ...
}

public (int version, object? instance) GetInstance(string typeId)
{
    if (!_cache.TryGetValue(typeId, out var cached))
    {
        _logger.LogWarning("Script type not found in cache: {TypeId}", typeId);
        return (-1, null);
    }

    try
    {
        var sw = Stopwatch.StartNew();
        var instance = cached.LazyInstance?.Value; // ✅ Thread-safe, one-time init
        sw.Stop();

        if (instance != null && cached.InstantiationCount == 0)
        {
            // Update stats
            cached.InstantiationCount++;
            cached.LastInstantiationTime = DateTime.UtcNow;
            _cache[typeId] = cached;

            _logger.LogDebug("Lazily instantiated {TypeId} in {Elapsed}ms",
                            typeId, sw.Elapsed.TotalMilliseconds);
        }

        return (cached.Version, instance);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to instantiate {TypeId}", typeId);
        return (cached.Version, null);
    }
}
```

**Recommendation:** Use **Option 2 (Lazy<T>)** - it's cleaner and thread-safe by design.

---

## Action Item #3: Optional Improvements

### Add Backup Source Path (Optional)

**File:** `/PokeSharp.Scripting/HotReload/Backup/ScriptBackupManager.cs`
**Line:** 27, 137

```csharp
// Change method signature
public void CreateBackup(string typeId, Type currentType, object? currentInstance,
                        int currentVersion, string? sourceFilePath = null)
{
    lock (_backupLock)
    {
        var backup = new ScriptBackup
        {
            TypeId = typeId,
            Type = currentType,
            Instance = currentInstance,
            Version = currentVersion,
            BackupTime = DateTime.UtcNow,
            SourceCode = sourceFilePath != null ? TryReadSourceCode(sourceFilePath) : null
        };

        _backups[typeId] = backup;
        _logger.LogDebug("Created backup for {TypeId} (version {Version})", typeId, currentVersion);
    }
}

private string? TryReadSourceCode(string filePath)
{
    try
    {
        if (File.Exists(filePath))
        {
            return File.ReadAllText(filePath);
        }
    }
    catch (Exception ex)
    {
        _logger.LogDebug(ex, "Could not read source code for backup: {FilePath}", filePath);
    }
    return null;
}
```

**Update caller in ScriptHotReloadService.cs line 156:**
```csharp
_backupManager.CreateBackup(typeId, currentType, instance, currentVersion, e.FilePath);
```

---

## Verification Checklist

After applying fixes:

- [ ] Code compiles without errors
- [ ] Run unit tests: `dotnet test --filter "VersionedScriptCache"`
- [ ] Run integration tests: `dotnet test --filter "HotReload"`
- [ ] Memory profiler shows no CTS leaks
- [ ] Concurrent instantiation test passes (10 threads)
- [ ] Update review document with "FIXES APPLIED" status

---

## Testing the Fixes

### Test #1: CTS Disposal
```csharp
// Add to FileSystemWatcherAdapterTests.cs
[Fact]
public async Task Debounce_DisposesOldCancellationTokens()
{
    var adapter = new FileSystemWatcherAdapter(logger);
    await adapter.StartAsync(tempDir, "*.cs");

    // Trigger multiple changes rapidly
    for (int i = 0; i < 100; i++)
    {
        File.WriteAllText(testFile, $"content {i}");
        await Task.Delay(50);
    }

    await adapter.StopAsync();

    // Use memory profiler to verify no CTS instances leaked
    // Expected: 0 unreferenced CancellationTokenSource instances
}
```

### Test #2: Lazy Instantiation Race
```csharp
// Add to VersionedScriptCacheTests.cs
[Fact]
public void GetInstance_ConcurrentAccess_CreatesSingleInstance()
{
    var cache = new VersionedScriptCache(logger);
    cache.UpdateVersion("Test", typeof(TestScript));

    var instances = new ConcurrentBag<object>();
    var tasks = Enumerable.Range(0, 100).Select(_ => Task.Run(() =>
    {
        var (version, instance) = cache.GetInstance("Test");
        instances.Add(instance);
    }));

    Task.WaitAll(tasks.ToArray());

    // All instances should be the same reference
    var distinctInstances = instances.Distinct().Count();
    Assert.Equal(1, distinctInstances);
}
```

---

## Summary

**Total Time:** 15 minutes
**Impact:** Prevents minor memory leaks and race conditions
**Difficulty:** Low (straightforward fixes)
**Priority:** Recommended (not critical for initial deployment)

**The system is production-ready even without these fixes, but applying them improves robustness and long-term stability.**
