# Mod Override System - Implementation Status Report

**Date**: 2025-11-15
**Tester**: QA Agent
**Status**: ⏸️ **WAITING FOR IMPLEMENTATION**

---

## Executive Summary

The mod override system is **partially implemented** but not ready for testing. The architecture has been changed from database-stored JSON to file-based paths (good for modding!), but the MapLoader hasn't been updated to match.

### Current State
- 🟡 **50% Complete**: Schema supports mods, but loading logic doesn't
- ⏸️ **BLOCKED**: Cannot test until file loading is implemented
- ✅ **Good News**: Architecture decision made (file-based paths)

---

## What's Working

### ✅ Database Schema (MapDefinition)
**Location**: `PokeSharp.Game.Data/Entities/MapDefinition.cs`

```csharp
// Working directory (unstaged changes)
public string TiledDataPath { get; set; } = string.Empty;  // Good for mods!
public string? SourceMod { get; set; }  // Tracks which mod owns this map
```

**Status**: ✅ **READY** - Schema supports file-based mod system

### ✅ Mod Querying (MapDefinitionService)
**Location**: `PokeSharp.Game.Data/Services/MapDefinitionService.cs`

```csharp
// Already implemented
public async Task<List<MapDefinition>> GetMapsByModAsync(string modId)
{
    return await _context.Maps
        .AsNoTracking()
        .Where(m => m.SourceMod == modId)
        .ToListAsync();
}
```

**Status**: ✅ **READY** - Can query maps by mod

---

## What's Broken

### ❌ File Loading (MapLoader)
**Location**: `PokeSharp.Game.Data/MapLoading/Tiled/MapLoader.cs` (Line 112)

**Current Code** (WRONG):
```csharp
public Entity LoadMap(World world, string mapId)
{
    var mapDef = _mapDefinitionService.GetMap(mapId);

    // ❌ This field doesn't exist in working directory!
    var tmxDoc = TiledMapLoader.LoadFromJson(mapDef.TiledDataJson, syntheticMapPath);
    //                                              ^^^^^^^^^^^^^^
    //                                              Field removed!
}
```

**Required Fix**:
```csharp
public Entity LoadMap(World world, string mapId)
{
    var mapDef = _mapDefinitionService.GetMap(mapId);

    // ✅ Load from file path instead
    var fullPath = ResolvePath(mapDef.TiledDataPath);  // Support both base/mod paths
    var tmxDoc = TiledMapLoader.Load(fullPath);
}
```

**Status**: ❌ **BROKEN** - Will crash at runtime

### ❌ Path Resolution
**Problem**: No logic to check mod directories before base game

**Required Logic**:
```csharp
private string ResolvePath(string relativePath)
{
    // Check if this is a mod path
    if (relativePath.StartsWith("Mods/"))
    {
        var modPath = Path.Combine(_assetManager.AssetRoot, relativePath);
        if (File.Exists(modPath))
            return modPath;
    }

    // Fallback to base game
    var basePath = Path.Combine(_assetManager.AssetRoot, "Data/Maps",
                                Path.GetFileName(relativePath));
    return basePath;
}
```

**Status**: ❌ **MISSING** - No mod directory support

---

## Test Scenarios - Readiness

| Scenario | Can Test? | Blocker |
|----------|-----------|---------|
| 1. Base game loads | ❌ No | MapLoader reads wrong field |
| 2. Mod adds new map | ❌ No | No file loading |
| 3. Mod overrides map | ❌ No | No path resolution |
| 4. Load order priority | ❌ No | All of the above |

**Test Coverage**: 0% (0/4 scenarios runnable)

---

## Critical Path to Testing

### Step 1: Fix MapLoader ⏸️ **BLOCKING**
**Owner**: Coder agents
**Priority**: 🔴 **CRITICAL**

**Required Changes**:
1. Update `MapLoader.LoadMap()` line 112:
   - Remove: `TiledMapLoader.LoadFromJson(mapDef.TiledDataJson, ...)`
   - Add: `TiledMapLoader.Load(ResolvePath(mapDef.TiledDataPath))`

2. Add `ResolvePath()` method:
   - Check `Mods/{modId}/Maps/` first
   - Fallback to `Assets/Data/Maps/`
   - Return absolute path

3. Test: Load existing map to verify no regression

**Estimated Effort**: 30 minutes
**Risk**: Low (simple field change)

---

### Step 2: Verify Base Game ✅ **READY AFTER STEP 1**
**Owner**: Tester (me!)
**Priority**: 🟢 **HIGH**

**Steps**:
1. Run game
2. Load `littleroot_town`
3. Verify no errors
4. Check map renders

**Exit Criteria**: No errors, map loads correctly

---

### Step 3: Create Mod Infrastructure 🔨 **NEEDS DESIGN**
**Owner**: Coder agents
**Priority**: 🟡 **MEDIUM**

**Components Needed**:
1. `ModManager` service
   - Scan `Mods/` directory
   - Parse `mod.json` manifests
   - Register maps in database

2. Mod manifest schema:
   ```json
   {
     "modId": "my-mod",
     "maps": [
       { "mapId": "new_map", "file": "Maps/NewMap.json" }
     ],
     "overrides": [
       { "mapId": "littleroot_town", "file": "Maps/LittlerootTown.json" }
     ]
   }
   ```

3. Database seeding:
   - Add/update MapDefinition entries
   - Set `SourceMod` field
   - Set `TiledDataPath` to mod file

**Estimated Effort**: 2-4 hours
**Risk**: Medium (new system)

---

### Step 4: Execute Test Plan 🧪 **FINAL STEP**
**Owner**: Tester (me!)
**Priority**: 🟢 **HIGH**

**Execute scenarios**:
1. ✅ Base game loading
2. ✅ Mod adds new map
3. ✅ Mod overrides existing map
4. ✅ Load order priority

**Deliverable**: Test results report

---

## Architecture Decision Record

### Why File-Based (TiledDataPath)?

**Chosen Approach**: ✅ Store file paths, load from disk

**Pros**:
- ✅ Mods are just files in directories
- ✅ Easy to edit with Tiled editor
- ✅ Version control friendly (diff JSON changes)
- ✅ No database import step
- ✅ Hot-reload possible (watch file changes)

**Cons**:
- ❌ Slower than database (file I/O)
- ❌ Must handle missing files
- ❌ Path resolution complexity

**Alternative Rejected**: ❌ Store JSON in database (TiledDataJson)

**Why Rejected**:
- ❌ Mods must import to DB (bad UX)
- ❌ Can't edit maps externally
- ❌ Database bloat (large JSON blobs)
- ❌ Version control nightmare (binary DB)

**Decision**: File-based is correct for modding! ✅

---

## Recommendation

**IMMEDIATE ACTION**: Complete Step 1 (Fix MapLoader)

**Code Review Needed**:
```csharp
// File: PokeSharp.Game.Data/MapLoading/Tiled/MapLoader.cs
// Line: 112

// BEFORE (current - BROKEN):
var tmxDoc = TiledMapLoader.LoadFromJson(mapDef.TiledDataJson, syntheticMapPath);

// AFTER (required - WORKING):
var fullPath = ResolvePath(mapDef.TiledDataPath);
var tmxDoc = TiledMapLoader.Load(fullPath);
```

**Once Fixed**: I can immediately test base game loading (Step 2)

**Estimated Timeline**:
- Step 1 (Fix): 30 min
- Step 2 (Test): 15 min
- Step 3 (Mod infra): 2-4 hours
- Step 4 (Full test): 1 hour

**Total**: 4-6 hours to complete mod system ✅

---

## Questions for Developers

1. **Path Resolution**: Should we support absolute paths or only relative?
   - Recommendation: Relative only (security)

2. **Mod Priority**: If two mods override same map, which wins?
   - Recommendation: Last-loaded mod (database update timestamp)

3. **Hot Reload**: Watch files for changes during gameplay?
   - Recommendation: Development mode only

4. **Validation**: Check map files exist before loading?
   - Recommendation: Yes, throw helpful error if missing

---

## Appendix: File Structure

### Current Base Game
```
PokeSharp.Game/
├── Assets/
│   └── Data/
│       └── Maps/
│           ├── LittlerootTown.json     ← Base game maps
│           ├── Route101.json
│           └── ...
└── Mods/                               ← Future: Mod directory
    └── (empty)
```

### Proposed Mod Structure
```
PokeSharp.Game/
└── Mods/
    ├── my-first-mod/
    │   ├── mod.json                    ← Manifest
    │   └── Maps/
    │       ├── NewMap.json             ← New content
    │       └── LittlerootTown.json     ← Override base
    └── another-mod/
        ├── mod.json
        └── Maps/
            └── Route101.json           ← Another override
```

### Database State After Loading
```sql
-- Base game map
MapId: littleroot_town
TiledDataPath: Assets/Data/Maps/LittlerootTown.json
SourceMod: NULL

-- After loading "my-first-mod"
MapId: littleroot_town
TiledDataPath: Mods/my-first-mod/Maps/LittlerootTown.json  ← Updated!
SourceMod: my-first-mod

-- New map from mod
MapId: new_custom_map
TiledDataPath: Mods/my-first-mod/Maps/NewMap.json
SourceMod: my-first-mod
```

---

**Next Update**: After Step 1 completion
**Contact**: Waiting for coder agents to fix MapLoader
