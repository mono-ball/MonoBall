# Action Items from Scripting API Research

**Research Date:** November 7, 2025
**Status:** Architecture validated at 9/10
**Priority:** Immediate actions this week, short-term within 2-3 sprints

---

## ✅ Immediate (This Week)

### 1. Validate Roslyn Compilation Caching
**File:** `PokeSharp.Scripting/Compilation/RoslynScriptCompiler.cs`

**Check:**
```csharp
// Ensure we're caching MetadataReference objects
private static readonly MetadataReference[] _cachedReferences = ...;

// Reuse across compilations
var compilation = CSharpCompilation.Create(...)
    .AddReferences(_cachedReferences); // ✅ Should be cached

// Fork from base compilation instead of rebuilding
var newCompilation = baseCompilation.ReplaceSyntaxTree(oldTree, newTree);
```

**Expected Result:** 10-50x speedup for incremental compilation

**Owner:** Coder/Performance Specialist

---

### 2. Profile Roslyn Compilation Stages
**File:** `PokeSharp.Scripting/Compilation/RoslynScriptCompiler.cs`

**Add:**
```csharp
public class CompilationMetrics
{
    public TimeSpan ParseTime { get; set; }      // Expected: ~10ms
    public TimeSpan BuildTime { get; set; }      // Expected: ~50ms
    public TimeSpan EmitTime { get; set; }       // Expected: 500-2000ms (bottleneck)
    public TimeSpan TotalTime => ParseTime + BuildTime + EmitTime;
}

public CompilationResult CompileWithMetrics(string source)
{
    var sw = Stopwatch.StartNew();

    // Parse
    var parseStart = sw.Elapsed;
    var syntaxTree = CSharpSyntaxTree.ParseText(source);
    var parseTime = sw.Elapsed - parseStart;

    // Build
    var buildStart = sw.Elapsed;
    var compilation = CSharpCompilation.Create(...);
    var buildTime = sw.Elapsed - buildStart;

    // Emit
    var emitStart = sw.Elapsed;
    var result = compilation.Emit(stream);
    var emitTime = sw.Elapsed - emitStart;

    return new CompilationResult
    {
        Metrics = new CompilationMetrics
        {
            ParseTime = parseTime,
            BuildTime = buildTime,
            EmitTime = emitTime
        }
    };
}
```

**Owner:** Performance Specialist

---

### 3. Document ScriptContext Pattern
**File:** `docs/architecture/scripting-context-pattern.md` (new)

**Include:**
- Pattern overview (Context Object)
- Comparison with Unity (GameObject) and Godot (Node)
- Entity scripts vs Global scripts
- Code examples
- Migration guide from other engines

**Owner:** Documentation Specialist

---

## 🔧 Short-Term (Next 2-3 Sprints)

### 4. Add State Migration for Hot Reload
**File:** `PokeSharp.Scripting/Runtime/TypeScriptBase.cs`

**Add:**
```csharp
/// <summary>
/// Called before hot reload with the new script instance.
/// Use this to transfer state from old to new instance.
/// </summary>
public virtual void OnBeforeReload(TypeScriptBase newInstance, ScriptContext ctx)
{
    // Example implementation:
    // var counter = ctx.GetState<int>("counter");
    // newInstance.SetInitialState(ctx, "counter", counter);
}
```

**File:** `PokeSharp.Scripting/HotReload/ScriptHotReloadService.cs`

**Modify:**
```csharp
private void ReplaceScriptInstance(Type scriptType, object oldInstance, object newInstance)
{
    if (oldInstance is TypeScriptBase oldScript && newInstance is TypeScriptBase newScript)
    {
        // Call migration hook
        oldScript.OnBeforeReload(newScript, GetContextForScript(oldScript));
    }

    // Swap instances
    SwapScriptInstances(oldInstance, newInstance);
}
```

**Testing:**
- Create script with state
- Modify script code
- Verify state transfers to new instance

**Owner:** Hot Reload Specialist / Coder

---

### 5. Implement Query Caching in ScriptContext
**File:** `PokeSharp.Scripting/Runtime/ScriptContext.cs`

**Add:**
```csharp
private readonly Dictionary<Type, object> _cachedQueries = new();

/// <summary>
/// Gets or creates a cached query for the specified component type.
/// Reuses query across multiple frames for performance.
/// </summary>
public Query GetCachedQuery<T>() where T : struct
{
    if (!_cachedQueries.TryGetValue(typeof(T), out var cachedQuery))
    {
        var query = World.Query(new QueryDescription().WithAll<T>());
        _cachedQueries[typeof(T)] = query;
        return query;
    }

    return (Query)cachedQuery;
}

/// <summary>
/// Clears all cached queries. Call when world structure changes significantly.
/// </summary>
public void ClearQueryCache()
{
    _cachedQueries.Clear();
}
```

**Usage in scripts:**
```csharp
// Instead of:
var query = ctx.World.Query(new QueryDescription().WithAll<Position>());

// Use cached:
var query = ctx.GetCachedQuery<Position>(); // Reused across frames
```

**Owner:** ECS Specialist / Performance

---

### 6. Add Priority-Based Event Subscriptions
**File:** `PokeSharp.Core/Events/IEventBus.cs`

**Extend:**
```csharp
/// <summary>
/// Subscribe to events with execution priority.
/// Higher priority handlers execute first.
/// </summary>
IDisposable Subscribe<TEvent>(Action<TEvent> handler, int priority = 0)
    where TEvent : TypeEventBase;
```

**File:** `PokeSharp.Core/Events/EventBus.cs`

**Implement:**
```csharp
private class Subscription
{
    public Delegate Handler { get; set; }
    public int Priority { get; set; }
}

private readonly Dictionary<Type, List<Subscription>> _subscriptions = new();

public IDisposable Subscribe<TEvent>(Action<TEvent> handler, int priority = 0)
    where TEvent : TypeEventBase
{
    var subscription = new Subscription
    {
        Handler = handler,
        Priority = priority
    };

    if (!_subscriptions.TryGetValue(typeof(TEvent), out var list))
    {
        list = new List<Subscription>();
        _subscriptions[typeof(TEvent)] = list;
    }

    list.Add(subscription);
    list.Sort((a, b) => b.Priority.CompareTo(a.Priority)); // Descending

    return new DisposableSubscription(this, typeof(TEvent), subscription);
}

public void Publish<TEvent>(TEvent eventData) where TEvent : TypeEventBase
{
    if (_subscriptions.TryGetValue(typeof(TEvent), out var list))
    {
        foreach (var sub in list)
        {
            ((Action<TEvent>)sub.Handler).Invoke(eventData);
        }
    }
}
```

**Testing:**
- Subscribe handlers with different priorities
- Verify execution order
- Test priority changes

**Owner:** Event System Specialist

---

### 7. Create Extension Point Interfaces
**File:** `PokeSharp.Core/Extensions/ICustomSystem.cs` (new)

```csharp
namespace PokeSharp.Core.Extensions;

/// <summary>
/// Extension point for custom systems from scripts or mods.
/// </summary>
public interface ICustomSystem
{
    /// <summary>
    /// System name for registration and debugging.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// System priority for execution order (higher = earlier).
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Called once when system is registered.
    /// </summary>
    void OnInitialize(World world);

    /// <summary>
    /// Called every frame.
    /// </summary>
    void OnSystemTick(World world, float deltaTime);

    /// <summary>
    /// Called when system is unregistered.
    /// </summary>
    void OnShutdown(World world);
}
```

**File:** `PokeSharp.Core/Extensions/IEffectProvider.cs` (new)

```csharp
namespace PokeSharp.Core.Extensions;

/// <summary>
/// Extension point for custom visual effects.
/// </summary>
public interface IEffectProvider
{
    /// <summary>
    /// Effect provider name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Check if this provider can handle the given effect ID.
    /// </summary>
    bool CanProvideEffect(string effectId);

    /// <summary>
    /// Spawn a visual effect.
    /// </summary>
    void SpawnEffect(string effectId, Point position, float duration, float scale, Color? tint);
}
```

**File:** `PokeSharp.Core/Extensions/IMapScriptExtension.cs` (new)

```csharp
namespace PokeSharp.Core.Extensions;

/// <summary>
/// Extension point for custom map logic.
/// </summary>
public interface IMapScriptExtension
{
    /// <summary>
    /// Called when a map is loaded.
    /// </summary>
    void OnMapLoad(int mapId, World world);

    /// <summary>
    /// Called when a map is unloaded.
    /// </summary>
    void OnMapUnload(int mapId, World world);

    /// <summary>
    /// Called every frame for the active map.
    /// </summary>
    void OnMapTick(int mapId, World world, float deltaTime);
}
```

**Owner:** API Architect

---

## 🔮 Future Considerations (Not Urgent)

### 8. API Versioning Strategy
**Goal:** Support legacy scripts when API changes

**Approach:**
```csharp
public interface IPlayerApi_v1 { /* 9 methods */ }
public interface IPlayerApi_v2 : IPlayerApi_v1 { /* + 3 new methods */ }

// Adapter pattern for old scripts
public class PlayerApiAdapter_v1_to_v2 : IPlayerApi_v2 { ... }
```

**Owner:** API Architect

---

### 9. Event Sourcing for Save/Load
**Goal:** Replay events to reconstruct state

**Components:**
- Event store (persist all TypeEventBase events)
- Snapshot system (periodic state snapshots)
- Replay engine (rebuild state from events)

**Use Cases:**
- Save/load system
- Replay functionality
- Debug time-travel

**Owner:** Architecture Team

---

### 10. Parallel Scripting with Access Tracking
**Goal:** Run independent scripts concurrently

**Approach:**
- Implement Bevy's FilteredAccessSet pattern
- Track read/write component access per script
- Automatic parallelization for non-conflicting scripts

**Complexity:** High (requires threading model changes)

**Owner:** Performance Team

---

### 11. Mod Sandboxing
**Goal:** Safe execution of untrusted scripts

**Features:**
- API whitelist (restrict dangerous operations)
- Resource limits (CPU time, memory, entity count)
- Code scanning (detect malicious patterns)

**Owner:** Security Specialist

---

### 12. Disk-Based Compilation Cache
**Goal:** Faster editor restarts

**Approach:**
```csharp
// Cache compiled assemblies to disk
var cacheKey = ComputeHash(sourceCode + references);
var cachePath = $"cache/scripts/{cacheKey}.dll";

if (File.Exists(cachePath) && IsUpToDate(cachePath, sourceFile))
{
    return Assembly.LoadFrom(cachePath);
}

// Compile and cache
var compilation = CompileScript(source);
compilation.Emit(cachePath);
```

**Owner:** Compilation Specialist

---

## Testing Checklist

For each action item, ensure:
- [ ] Unit tests added
- [ ] Integration tests pass
- [ ] Performance benchmarks run
- [ ] Documentation updated
- [ ] Code reviewed by team
- [ ] Merged to main branch

---

## References

- **Full Research:** `docs/research/scripting-api-best-practices.md`
- **Summary:** `docs/research/RESEARCH-SUMMARY.md`
- **Memory Key:** `hive/research/patterns` (if memory system is working)

---

## Questions or Blockers?

Contact Research Agent or review team lead.

**Research Status:** ✅ COMPLETE
**Next Review:** After immediate actions are implemented
