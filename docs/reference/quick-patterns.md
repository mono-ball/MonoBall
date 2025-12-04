# Quick Reference: Arch ECS Patterns

Visual guide for correct vs incorrect patterns in your codebase.

---

## ❌ vs ✅ Pattern Reference

### 1. Structural Changes During Queries

#### ❌ WRONG: Modify During Iteration
```csharp
// DON'T DO THIS - Can crash or corrupt entities
world.Query(in query, entity =>
{
    entity.Add(new SomeComponent());      // ❌ Structural change
    entity.Remove<OtherComponent>();      // ❌ Structural change
    world.Destroy(entity);                // ❌ Structural change
});
```

**Why it's bad:** Changes archetype while iterating, undefined behavior

#### ✅ CORRECT: Collect Then Modify
```csharp
// Step 1: Collect (read-only pass)
var entities = new List<Entity>();
world.Query(in query, entity => entities.Add(entity));

// Step 2: Modify (after iteration)
foreach (var entity in entities)
{
    if (world.IsAlive(entity))
    {
        entity.Add(new SomeComponent());  // ✅ Safe
    }
}
```

**Why it's good:** Iteration completes before modifications

---

### 2. Component Modification

#### ❌ WRONG: Modify Copy Without Writing Back
```csharp
world.Query(in query, (Entity entity) =>
{
    Animation anim = entity.Get<Animation>();  // Gets COPY
    anim.CurrentFrame++;                        // Modifies copy
    // ❌ Changes lost - never written back!
});
```

**Why it's bad:** Structs are value types, changes are to a copy

#### ✅ CORRECT: Modify by Reference
```csharp
world.Query(in query, (Entity entity, ref Animation anim) =>
{
    anim.CurrentFrame++;  // ✅ Modifies in-place via ref
});
```

**Why it's good:** `ref` gives direct access to component data

#### ✅ ALSO CORRECT: TryGet Pattern
```csharp
if (world.TryGet(entity, out Animation anim))
{
    anim.CurrentFrame++;
    world.Set(entity, anim);  // ✅ Write modified copy back
}
```

**Why it's good:** Explicit write-back, clear intent

---

### 3. Query Caching

#### ❌ BAD: Create Every Time
```csharp
public void Update(World world, float deltaTime)
{
    // ❌ Allocates new query every frame
    var query = new QueryDescription().WithAll<Position, Velocity>();
    world.Query(in query, (ref Position pos, ref Velocity vel) =>
    {
        pos.X += vel.X * deltaTime;
    });
}
```

**Impact:** Allocations every frame, GC pressure

#### ⚠️ OK: Create Once in Initialize
```csharp
private QueryDescription _movementQuery;

public override void Initialize(World world)
{
    _movementQuery = new QueryDescription().WithAll<Position, Velocity>();
}

public void Update(World world, float deltaTime)
{
    world.Query(in _movementQuery, ...);  // ⚠️ Better, but not ideal
}
```

**Impact:** No allocations, but query stored per-system instance

#### ✅ BEST: Static Shared Query
```csharp
// EcsQueries.cs
public static class EcsQueries
{
    public static readonly QueryDescription Movement = 
        new QueryDescription().WithAll<Position, Velocity>();
}

// System.cs
public void Update(World world, float deltaTime)
{
    world.Query(in EcsQueries.Movement, ...);  // ✅ Zero allocation, shared
}
```

**Impact:** Single query instance shared across all systems

---

### 4. Exception Handling

#### ❌ BAD: Exceptions for Control Flow
```csharp
try
{
    entity = pool.Acquire();  // ❌ Throws if empty
}
catch (InvalidOperationException)
{
    entity = world.Create();  // Fallback path
}
```

**Why it's bad:**
- Slow (exception overhead)
- Unclear intent (is this error or normal?)
- Brittle (depends on exception message)

#### ✅ GOOD: Try Pattern
```csharp
if (pool.TryAcquire(out entity))
{
    // Success path
}
else
{
    entity = world.Create();  // Normal fallback
}
```

**Why it's good:**
- Fast (no exception)
- Clear intent (expected branching)
- Type-safe return

---

### 5. Magic Numbers vs Constants

#### ❌ BAD: Hardcoded Values
```csharp
public void RenderPanel()
{
    Rectangle bounds = new(10, 10, 800, 600);  // ❌ Magic numbers
    
    if (scrollOffset > 300)  // ❌ What does 300 mean?
        scrollOffset = 300;
    
    padding = 8;  // ❌ Why 8?
}
```

**Issues:** Hard to maintain, unclear intent, inconsistent

#### ⚠️ OK: Local Constants
```csharp
private const int PANEL_WIDTH = 800;
private const int PANEL_HEIGHT = 600;
private const int MAX_SCROLL = 300;
private const int PADDING = 8;

public void RenderPanel()
{
    Rectangle bounds = new(10, 10, PANEL_WIDTH, PANEL_HEIGHT);  // ⚠️ Better
}
```

**Issues:** Still duplicated if used in multiple places

#### ✅ BEST: Theme/Config System
```csharp
public void RenderPanel()
{
    var theme = ThemeManager.Current;
    Rectangle bounds = new(
        theme.PanelEdgeGap,
        theme.PanelEdgeGap,
        theme.PanelDefaultWidth,   // ✅ Semantic, reusable
        theme.PanelDefaultHeight
    );
    
    padding = theme.PaddingMedium;  // ✅ Consistent everywhere
}
```

**Benefits:** Single source of truth, semantic names, themeable

---

### 6. String Literals vs Constants

#### ❌ BAD: String Literals
```csharp
poolManager.RegisterPool("player", 10, 50);
// ... 50 lines later ...
entity = poolManager.Acquire("playre");  // ❌ TYPO! Runtime error
```

**Issues:** Typos not caught at compile time, hard to refactor

#### ✅ GOOD: String Constants
```csharp
public static class PoolNames
{
    public const string Player = "player";
    public const string Npc = "npc";
}

poolManager.RegisterPool(PoolNames.Player, 10, 50);
entity = poolManager.Acquire(PoolNames.Player);  // ✅ Type-safe
```

**Benefits:** Compile-time checks, refactoring support, autocomplete

---

### 7. Optional Components

#### ❌ WRONG: Has + Get (Two Lookups)
```csharp
world.Query(in query, (Entity entity) =>
{
    if (entity.Has<Animation>())  // ❌ First lookup
    {
        var anim = entity.Get<Animation>();  // ❌ Second lookup
        // ... use anim
    }
});
```

**Impact:** Double hash table lookup, slower

#### ✅ CORRECT: TryGet (One Lookup)
```csharp
world.Query(in query, (Entity entity) =>
{
    if (world.TryGet(entity, out Animation anim))  // ✅ Single lookup
    {
        // ... use anim
        world.Set(entity, anim);  // ✅ Write back if modified
    }
});
```

**Impact:** 2x faster for optional components

---

### 8. Batch Operations

#### ❌ SLOW: Loop with Individual Operations
```csharp
for (int i = 0; i < 1000; i++)
{
    Entity e = world.Create();       // ❌ 1000 allocations
    e.Add(new Position());           // ❌ 1000 archetype lookups
    e.Add(new Velocity());
}
```

**Impact:** Slow, fragmented memory, poor cache locality

#### ✅ FAST: Bulk Creation
```csharp
// Create 1000 entities at once (same archetype)
Entity[] entities = world.Create<Position, Velocity>(1000);

// Initialize in bulk
for (int i = 0; i < entities.Length; i++)
{
    ref Position pos = ref entities[i].Get<Position>();
    pos.X = i * 10;
}
```

**Impact:** 10-100x faster, better memory layout

#### ✅ ALSO GOOD: Entity Pool
```csharp
// Acquire from pool (reuses memory)
Entity e = poolManager.Acquire(PoolNames.Npc);

// ... use entity ...

// Release back to pool (no deallocation)
poolManager.Release(e);
```

**Impact:** No GC pressure, consistent performance

---

## Component Design Patterns

### ✅ GOOD: Small Value Components
```csharp
public struct Position  // ✅ Small, value type
{
    public float X;
    public float Y;
}

public struct Health  // ✅ Simple data
{
    public int Current;
    public int Max;
}
```

**Why:** Fast to copy, cache-friendly, ECS-optimal

### ❌ BAD: Large Reference Components
```csharp
public struct BadComponent  // ❌ Too large
{
    public Dictionary<string, object> Data;  // ❌ Reference type
    public List<int> Items;                  // ❌ Reference type
    public byte[] Buffer;                    // ❌ Large array
}
```

**Why:** Breaks cache coherence, GC pressure, slow copies

### ✅ BETTER: Hybrid Approach
```csharp
public struct EntityId  // ✅ Small component
{
    public int Id;
}

// Separate lookup table for large data
Dictionary<int, LargeData> _dataLookup = new();

// In system:
world.Query(in query, (ref EntityId id) =>
{
    if (_dataLookup.TryGetValue(id.Id, out var data))
    {
        // Use large data from lookup
    }
});
```

**Why:** Components stay small, cache-friendly iteration

---

## Performance Checklist

### For Every System Update():

- [ ] ✅ Query is cached (static or readonly field)
- [ ] ✅ No structural changes during iteration
- [ ] ✅ Components accessed by `ref` when modified
- [ ] ✅ Optional components use `TryGet()`
- [ ] ✅ No exceptions in hot path
- [ ] ✅ Batch operations where possible

### For Every Component:

- [ ] ✅ Struct (not class)
- [ ] ✅ Small size (<128 bytes ideal)
- [ ] ✅ No reference types (use IDs instead)
- [ ] ✅ Immutable where possible
- [ ] ✅ Semantic name (clear purpose)

### For Every Service:

- [ ] ✅ Uses DI (registered in service collection)
- [ ] ✅ Proper lifetime (Singleton/Scoped/Transient)
- [ ] ✅ No static mutable state
- [ ] ✅ Testable (interface-based)
- [ ] ✅ Fail-fast on errors

---

## Common Pitfalls

### 1. "Chunk Iteration" Anti-pattern
```csharp
// ❌ DON'T iterate by chunk manually
foreach (var chunk in world.GetAllChunks())
{
    // Manual iteration - hard to maintain
}

// ✅ DO use Query API
world.Query(in query, (ref Position pos) => { });
```

### 2. "God Component" Anti-pattern
```csharp
// ❌ BAD: One component with everything
public struct Character
{
    public Position Position;
    public Stats Stats;
    public Inventory Inventory;
    public Abilities Abilities;
    // ... 50 more fields
}

// ✅ GOOD: Separate components
public struct Position { public float X, Y; }
public struct Stats { public int HP, MP; }
public struct Inventory { public EntityId InventoryEntity; }
public struct Abilities { public int[] AbilityIds; }
```

### 3. "Sync Everything" Anti-pattern
```csharp
// ❌ BAD: Sync all entity state every frame
world.Query(in allEntities, entity =>
{
    SyncToDatabase(entity);  // ❌ Expensive!
});

// ✅ GOOD: Mark dirty, sync on schedule
world.Query(in dirtyEntities, entity =>
{
    if (ShouldSync(entity))
        QueueForSync(entity);
});
```

---

## Your Code Status

### ✅ Already Doing Well
- Query caching (mostly)
- Entity pooling
- TryGet pattern
- Good documentation
- Proper component sizes

### 🔴 Needs Fixing
- Structural changes in `BulkQueryOperations`
- Exception control flow in `EntityFactoryService`
- Some query caching inconsistency
- String literal pool names

### ⚠️ Could Improve
- Theme constant usage (mostly good)
- TODO comment management
- GC pressure in bulk operations

---

## Quick Wins

**30 Minutes:**
1. Fix `BulkQueryOperations` (collect-then-modify)
2. Create `PoolNames` constants class

**1 Hour:**
3. Add `TryAcquire()` to `EntityPoolManager`
4. Move queries to `EcsQueries` static class

**2 Hours:**
5. Add missing theme constants
6. Update UI components to use theme

**Total: ~4 hours for all critical + high priority fixes**

---

## When In Doubt

1. **Structural changes?** → Collect first, then modify
2. **Query allocation?** → Make it static
3. **Optional component?** → Use `TryGet()`
4. **Magic number?** → Add to theme/config
5. **String literal?** → Make it a constant
6. **Exception expected?** → Use Try pattern

**Remember:** If `MapLifecycleManager` does it, it's probably correct! 😊

---

For detailed fixes, see:
- `docs/CRITICAL_FIXES_EXAMPLES.md` - Complete code examples
- `docs/CODE_REVIEW_ANALYSIS.md` - Full analysis
- `docs/CODE_REVIEW_SUMMARY.md` - Executive summary

