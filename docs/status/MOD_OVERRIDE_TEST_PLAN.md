# Mod Override System - Test Plan

## Overview
Testing that the file-based map loading approach supports mod content overrides while maintaining EF Core metadata storage.

## Current Implementation Status

### ✅ Implemented
1. **EF Core Metadata Storage**: `MapDefinition` entity with `SourceMod` field (line 114)
2. **File-based Tiled Loading**: `MapLoader.LoadMap()` loads from `TiledDataJson` (line 112)
3. **Mod Querying**: `MapDefinitionService.GetMapsByModAsync()` (line 117)

### ⚠️ ARCHITECTURE DECISION NEEDED
**Current Implementation (HEAD commit):**
- MapDefinition has `TiledDataJson` field - stores entire JSON in database
- MapLoader reads from `TiledDataJson` field (line 112)
- **This prevents file-based mod overrides!**

**Working Directory (Unstaged Changes):**
- MapDefinition changed to `TiledDataPath` field - stores file path
- **This supports file-based mods but needs MapLoader update**

**DECISION REQUIRED**: Which approach for mod support?
1. **File-based** (TiledDataPath): Mods can edit files directly ✅ RECOMMENDED
2. **Database-based** (TiledDataJson): Mods must import to DB ❌ Less flexible

## Test Scenarios

### 1. Base Game Loading
**Objective**: Verify base game maps load correctly from filesystem

**Test Steps:**
1. Ensure database has MapDefinition entries for base maps
2. Verify `TiledDataPath` points to: `Assets/Data/Maps/{MapId}.json`
3. Load map via `MapLoader.LoadMap(world, "littleroot_town")`
4. Verify:
   - Map tiles render correctly
   - NPCs spawn at correct positions
   - Collision data works
   - Metadata matches EF Core definition

**Expected Result:**
- Map loads successfully
- All entities created
- No errors in logs

**Files to Check:**
- `/mnt/c/Users/nate0/RiderProjects/PokeSharp/PokeSharp.Game/Assets/Data/Maps/LittlerootTown.json`
- Database: `Maps` table entry for `littleroot_town`

---

### 2. Mod Adds New Map
**Objective**: Verify mods can add entirely new maps

**Test Steps:**
1. Create test mod directory structure:
   ```
   PokeSharp.Game/Mods/test-mod/
   ├── mod.json (manifest)
   └── Maps/
       └── TestNewMap.json (new Tiled map)
   ```

2. Add MapDefinition to database:
   ```sql
   INSERT INTO Maps (MapId, DisplayName, Region, TiledDataPath, SourceMod)
   VALUES ('test_new_map', 'Test New Map', 'hoenn',
           'Mods/test-mod/Maps/TestNewMap.json', 'test-mod');
   ```

3. Load map: `MapLoader.LoadMap(world, "test_new_map")`

4. Verify:
   - Map loads from mod directory
   - Tileset textures resolve correctly
   - Map appears in `GetMapsByModAsync("test-mod")`

**Expected Result:**
- New map loads successfully
- No conflicts with base game
- `SourceMod` field correctly set to "test-mod"

**Files to Create:**
- `/mnt/c/Users/nate0/RiderProjects/PokeSharp/PokeSharp.Game/Mods/test-mod/mod.json`
- `/mnt/c/Users/nate0/RiderProjects/PokeSharp/PokeSharp.Game/Mods/test-mod/Maps/TestNewMap.json`

---

### 3. Mod Overrides Existing Map
**Objective**: Verify mods can replace base game map content

**Test Steps:**
1. Create modified version of LittlerootTown:
   ```
   PokeSharp.Game/Mods/test-override/
   └── Maps/
       └── LittlerootTown.json (modified layout)
   ```

2. **CRITICAL**: Update MapDefinition to point to mod version:
   ```sql
   UPDATE Maps
   SET TiledDataPath = 'Mods/test-override/Maps/LittlerootTown.json',
       SourceMod = 'test-override'
   WHERE MapId = 'littleroot_town';
   ```

3. Load map: `MapLoader.LoadMap(world, "littleroot_town")`

4. Verify:
   - Mod's version loads (not base game)
   - Modified tiles/NPCs appear
   - Original base file remains unchanged

**Expected Result:**
- Mod version loads instead of base game
- Changes are visible in-game
- Base game file untouched

**Files to Create:**
- `/mnt/c/Users/nate0/RiderProjects/PokeSharp/PokeSharp.Game/Mods/test-override/Maps/LittlerootTown.json`

---

### 4. Multiple Mods - Load Order
**Objective**: Verify last-registered mod wins for same map

**Test Setup:**
1. Mod A overrides LittlerootTown (adds NPC "Mod A Person")
2. Mod B overrides LittlerootTown (adds NPC "Mod B Person")
3. Load order: Base → Mod A → Mod B

**Test Steps:**
1. Create Mod A version:
   ```
   Mods/mod-a/Maps/LittlerootTown.json
   ```

2. Create Mod B version:
   ```
   Mods/mod-b/Maps/LittlerootTown.json
   ```

3. Simulate load order by updating database:
   ```sql
   -- First, Mod A is active
   UPDATE Maps SET TiledDataPath = 'Mods/mod-a/Maps/LittlerootTown.json',
                    SourceMod = 'mod-a'
   WHERE MapId = 'littleroot_town';
   ```

4. Load map, verify Mod A's NPC appears

5. Change to Mod B:
   ```sql
   UPDATE Maps SET TiledDataPath = 'Mods/mod-b/Maps/LittlerootTown.json',
                    SourceMod = 'mod-b'
   WHERE MapId = 'littleroot_town';
   ```

6. Reload map, verify Mod B's NPC appears

**Expected Result:**
- Only one mod version loads at a time
- Last-registered mod (Mod B) takes precedence
- Database correctly tracks which mod is active

**Files to Create:**
- `/mnt/c/Users/nate0/RiderProjects/PokeSharp/PokeSharp.Game/Mods/mod-a/Maps/LittlerootTown.json`
- `/mnt/c/Users/nate0/RiderProjects/PokeSharp/PokeSharp.Game/Mods/mod-b/Maps/LittlerootTown.json`

---

## Implementation Gaps

### Must Fix Before Testing

1. **Complete File-Based Architecture**:
   - ✅ MapDefinition changed to `TiledDataPath` (working directory)
   - ⏸️ MapLoader still reads from `TiledDataJson` field
   - **TODO**: Update MapLoader.LoadMap() to:
     ```csharp
     // Instead of: var tmxDoc = TiledMapLoader.LoadFromJson(mapDef.TiledDataJson, ...);
     // Use: var tmxDoc = TiledMapLoader.Load(mapDef.TiledDataPath);
     ```
   - **TODO**: Resolve paths relative to Assets or Mods directory

2. **Mod Loading System**:
   - Need mod manifest (`mod.json`) parser
   - Need mod registration service to update database
   - Need conflict detection (multiple mods override same map)

3. **File Resolution**:
   - MapLoader needs to check:
     1. Mod directory first: `Mods/{SourceMod}/Maps/{MapId}.json`
     2. Base game fallback: `Assets/Data/Maps/{MapId}.json`

### Nice-to-Have Features

1. **Mod Priority System**:
   - Add `Priority` field to MapDefinition
   - Higher priority mods win conflicts

2. **Partial Overrides**:
   - Allow mods to override only NPCs/objects (not entire map)
   - Requires layer merging logic

3. **Hot Reload**:
   - Watch mod directories for changes
   - Reload maps without restarting game

---

## Test Execution Plan

### Phase 1: Fix Schema Mismatch ⏸️ BLOCKING
**Status**: Waiting for coder agents

**Required Changes**:
1. Update `MapDefinition.cs`:
   - Keep `TiledDataPath` field
   - Remove references to `TiledDataJson`

2. Update `MapLoader.LoadMap()`:
   - Read JSON from file at `TiledDataPath`
   - Support relative paths from both base and mod directories

**Test**: Run existing map loading to verify no regressions

---

### Phase 2: Base Game Verification ✅ READY
**Status**: Ready to test after Phase 1

**Steps**:
1. Run game with base maps
2. Load LittlerootTown
3. Verify all features work
4. Check logs for errors

**Success Criteria**:
- No errors in console
- Map renders correctly
- NPCs spawn and move

---

### Phase 3: Mod Directory Structure 🔨 IMPLEMENTATION NEEDED
**Status**: Need mod loading infrastructure

**Tasks**:
1. Create `ModManager` service
2. Parse `mod.json` manifests
3. Register mods in database
4. Update `MapLoader` to check mod directories first

**Test**: Create test mod and verify it loads

---

### Phase 4: Override Testing 🧪 INTEGRATION
**Status**: Depends on Phase 1-3

**Execute All Test Scenarios**: 1, 2, 3, 4

---

## Test Data Templates

### Mod Manifest Template
```json
// Mods/test-mod/mod.json
{
  "modId": "test-mod",
  "name": "Test Mod",
  "version": "1.0.0",
  "author": "Test Author",
  "description": "Test mod for override system",
  "dependencies": [],
  "maps": [
    {
      "mapId": "test_new_map",
      "displayName": "Test New Map",
      "file": "Maps/TestNewMap.json"
    }
  ],
  "overrides": [
    {
      "mapId": "littleroot_town",
      "file": "Maps/LittlerootTown.json"
    }
  ]
}
```

### Minimal Test Map Template
```json
// Mods/test-mod/Maps/TestNewMap.json
{
  "compressionlevel": -1,
  "height": 10,
  "width": 10,
  "infinite": false,
  "layers": [
    {
      "data": [1, 1, 1, ...], // 100 tiles
      "height": 10,
      "width": 10,
      "name": "Ground",
      "type": "tilelayer",
      "visible": true,
      "x": 0,
      "y": 0
    }
  ],
  "nextlayerid": 2,
  "nextobjectid": 1,
  "orientation": "orthogonal",
  "renderorder": "right-down",
  "tiledversion": "1.10.0",
  "tileheight": 16,
  "tilewidth": 16,
  "tilesets": [
    {
      "firstgid": 1,
      "source": "../../Assets/Tilesets/primary.json"
    }
  ],
  "type": "map",
  "version": "1.10"
}
```

---

## Success Metrics

### Must Pass
- [ ] Base game maps load without errors
- [ ] Mods can add new maps
- [ ] Mods can override existing maps
- [ ] Last-loaded mod wins conflicts
- [ ] Base game files remain unmodified

### Should Pass
- [ ] Database tracks `SourceMod` correctly
- [ ] `GetMapsByModAsync()` returns correct results
- [ ] Tileset paths resolve correctly for mods
- [ ] Logs show which mod/file is loaded

### Nice to Have
- [ ] Multiple mods can coexist peacefully
- [ ] Disable mod without deleting files
- [ ] Mod load order configurable
- [ ] Hot reload works for mod changes

---

## Current Blockers

1. **CRITICAL**: Schema mismatch between `MapDefinition` and `MapLoader`
   - Entity expects `TiledDataPath` (file path)
   - Loader expects `TiledDataJson` (JSON string)
   - **Action**: Wait for coder agents to fix

2. **HIGH**: No mod loading infrastructure
   - Need `ModManager` service
   - Need manifest parser
   - Need database registration
   - **Action**: Will implement after schema fix

3. **MEDIUM**: File path resolution logic
   - MapLoader needs to check mod directories
   - Needs fallback to base game
   - **Action**: Part of mod infrastructure

---

## Next Steps

1. ⏸️ **WAIT** for coder agents to fix schema mismatch
2. ✅ **VERIFY** base game maps still load
3. 🔨 **IMPLEMENT** mod loading infrastructure
4. 🧪 **EXECUTE** test scenarios 1-4
5. 📊 **REPORT** results and issues

---

## Notes for Developers

- Current implementation uses database for metadata, files for content (good!)
- `SourceMod` field exists but isn't used yet
- Need to decide: store JSON in DB or read from files?
  - **Recommendation**: Files (easier to edit, version control, mod)
- MapLoader reads from `ResolveMapDirectoryBase()` which points to `Assets/Data/Maps`
  - Needs to support `Mods/{ModId}/Maps` paths too

---

**Test Plan Created**: 2025-11-15
**Status**: Waiting for implementation completion
**Next Review**: After schema mismatch resolved
