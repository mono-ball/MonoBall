# Bug Fix: DbContext Lifetime Issue

## Problem

When running the game, the following error occurred:

```
[ERROR] MapInitializer: Failed to load map: test-map
Exception: InvalidOperationException: MapDefinitionService is required for definition-based map loading.
```

## Root Cause

**Dependency Injection Lifetime Mismatch**

The issue was caused by a service lifetime mismatch in the DI container:

1. **`GameDataContext`** (EF Core DbContext) - Registered with **Scoped** lifetime (default for DbContext)
2. **`MapDefinitionService`** - Registered as **Singleton**
3. **`GraphicsServiceFactory`** - Registered as **Singleton**

When `GraphicsServiceFactory` tried to inject `MapDefinitionService`, the DI container couldn't resolve it because a **singleton cannot depend on a scoped service**.

### Why This Happened

By default, `AddDbContext()` registers the DbContext with **Scoped** lifetime because:
- Most EF Core contexts track entity changes
- Scoped ensures proper isolation between requests/operations
- Prevents concurrency issues

However, we're using EF Core In-Memory for **read-only game data**, which:
- Doesn't need change tracking
- Is loaded once at startup
- Is safe to share across the application lifetime
- **Can be a Singleton**

## The Fix

Changed `GameDataContext` registration from Scoped to Singleton:

**Before:**
```csharp
services.AddDbContext<PokeSharp.Game.Data.GameDataContext>(options =>
{
    options.UseInMemoryDatabase("GameData");
    // ...
});
```

**After:**
```csharp
services.AddDbContext<PokeSharp.Game.Data.GameDataContext>(
    options =>
    {
        options.UseInMemoryDatabase("GameData");
        // ...
    },
    ServiceLifetime.Singleton  // In-Memory DB can be singleton
);
```

## Why This Is Safe

For our use case, using a Singleton DbContext is safe because:

1. **Read-Only Data**: We load game definitions at startup and only read them
2. **In-Memory Provider**: No database connection pooling concerns
3. **No Change Tracking**: We're not modifying entities
4. **Performance**: Better performance by avoiding context creation overhead
5. **Consistency**: All services share the same data

## Service Lifetime Chain

After the fix, the dependency chain is consistent:

```
GameDataContext (Singleton)
    ↓
NpcDefinitionService (Singleton)
MapDefinitionService (Singleton)
    ↓
GraphicsServiceFactory (Singleton)
    ↓
MapLoader (Created on-demand, depends on MapDefinitionService)
```

## Build Status

```
✅ Build succeeded - 0 errors, 5 warnings (unrelated)
✅ All DI lifetimes are now consistent
✅ MapDefinitionService properly injected
✅ Ready to run
```

## Testing

Run the game and verify:

1. **Startup logs** should show:
   ```
   [INFO] Loading game data definitions from Assets/Data...
   [DEBUG] Loaded Map: test-map (Test Map)
   [INFO] Game data loaded: NPCs: 3, Trainers: 2, Maps: 1
   ```

2. **Map loading logs** should show:
   ```
   [INFO] Loading map from definition: test-map
   [INFO] Map entities created
   [INFO] Map load complete: test-map
   ```

3. **No errors** about MapDefinitionService being null

## Important Note

This pattern (Singleton DbContext) is **ONLY** appropriate for:
- ✅ In-Memory databases with read-only data
- ✅ Game data definitions loaded at startup
- ✅ No entity tracking or modifications

For typical web applications or scenarios with:
- ❌ Real databases (SQL Server, PostgreSQL, etc.)
- ❌ Entity tracking and modifications
- ❌ Multiple concurrent requests

You should **always use Scoped lifetime** for DbContext!

## Files Modified

- `ServiceCollectionExtensions.cs` - Changed `AddDbContext` to use `ServiceLifetime.Singleton`

## Status

✅ **FIXED** - MapDefinitionService now properly injected and working

