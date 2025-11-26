# Mod Override Testing - Quick Checklist

## Pre-Test Setup

### 1. Verify Implementation Complete
- [ ] MapLoader reads from `TiledDataPath` field (not `TiledDataJson`)
- [ ] Path resolution supports both base and mod directories
- [ ] Base game maps still load without errors
- [ ] Database has `SourceMod` field populated

### 2. Setup Test Environment
```bash
# Create mod directories
mkdir -p PokeSharp.Game/Mods/test-mod/Maps
mkdir -p PokeSharp.Game/Mods/test-override/Maps
mkdir -p PokeSharp.Game/Mods/mod-a/Maps
mkdir -p PokeSharp.Game/Mods/mod-b/Maps
```

---

## Test 1: Base Game Loading ✅

### Steps
1. [ ] Start game
2. [ ] Load map: `littleroot_town`
3. [ ] Walk around
4. [ ] Talk to NPCs
5. [ ] Check console for errors

### Expected Results
- [ ] No errors in console
- [ ] Map renders correctly
- [ ] NPCs spawn and move
- [ ] Collision works
- [ ] Tiles animate

### Pass/Fail: ___________

**Notes**:
```
_________________________________________________
_________________________________________________
_________________________________________________
```

---

## Test 2: Mod Adds New Map 🆕

### Prep
```bash
# Copy a simple base map as template
cp PokeSharp.Game/Assets/Data/Maps/LittlerootTown.json \
   PokeSharp.Game/Mods/test-mod/Maps/TestNewMap.json
```

### Mod Manifest
Create `PokeSharp.Game/Mods/test-mod/mod.json`:
```json
{
  "modId": "test-mod",
  "name": "Test Mod",
  "version": "1.0.0",
  "maps": [
    {
      "mapId": "test_new_map",
      "displayName": "Test New Map",
      "file": "Maps/TestNewMap.json"
    }
  ]
}
```

### Database Setup
```sql
INSERT INTO Maps (MapId, DisplayName, Region, TiledDataPath, SourceMod)
VALUES (
  'test_new_map',
  'Test New Map',
  'hoenn',
  'Mods/test-mod/Maps/TestNewMap.json',
  'test-mod'
);
```

### Steps
1. [ ] Run mod loader (or manually insert DB entry)
2. [ ] Load map: `test_new_map`
3. [ ] Verify map loads
4. [ ] Check console shows mod path

### Expected Results
- [ ] Map loads from mod directory
- [ ] Console shows: `Loading from Mods/test-mod/Maps/TestNewMap.json`
- [ ] Map appears in game
- [ ] `GetMapsByModAsync("test-mod")` returns 1 map

### Pass/Fail: ___________

**Notes**:
```
_________________________________________________
_________________________________________________
_________________________________________________
```

---

## Test 3: Mod Overrides Existing Map 🔄

### Prep
```bash
# Create modified version
cp PokeSharp.Game/Assets/Data/Maps/LittlerootTown.json \
   PokeSharp.Game/Mods/test-override/Maps/LittlerootTown.json

# Edit the copy in a text editor:
# - Change displayName to "Littleroot Town (MODDED)"
# - Add an NPC or modify layout (optional)
```

### Mod Manifest
Create `PokeSharp.Game/Mods/test-override/mod.json`:
```json
{
  "modId": "test-override",
  "name": "Test Override Mod",
  "version": "1.0.0",
  "overrides": [
    {
      "mapId": "littleroot_town",
      "file": "Maps/LittlerootTown.json"
    }
  ]
}
```

### Database Update
```sql
UPDATE Maps
SET TiledDataPath = 'Mods/test-override/Maps/LittlerootTown.json',
    SourceMod = 'test-override'
WHERE MapId = 'littleroot_town';
```

### Steps
1. [ ] Apply database update
2. [ ] Load map: `littleroot_town`
3. [ ] Verify modified version loads
4. [ ] Check base game file untouched

### Expected Results
- [ ] Mod version loads (not base game)
- [ ] Console shows: `Loading from Mods/test-override/Maps/LittlerootTown.json`
- [ ] Changes visible in-game
- [ ] Base file exists: `Assets/Data/Maps/LittlerootTown.json` ← unchanged

### Verification
```bash
# Base game file should be unmodified
git diff PokeSharp.Game/Assets/Data/Maps/LittlerootTown.json
# (should show no changes)
```

### Pass/Fail: ___________

**Notes**:
```
_________________________________________________
_________________________________________________
_________________________________________________
```

---

## Test 4: Multiple Mods - Load Order 🔀

### Prep
```bash
# Create Mod A version
cp PokeSharp.Game/Assets/Data/Maps/LittlerootTown.json \
   PokeSharp.Game/Mods/mod-a/Maps/LittlerootTown.json

# Edit: Add object with name="MOD_A_NPC"

# Create Mod B version
cp PokeSharp.Game/Assets/Data/Maps/LittlerootTown.json \
   PokeSharp.Game/Mods/mod-b/Maps/LittlerootTown.json

# Edit: Add object with name="MOD_B_NPC"
```

### Part A: Mod A Loaded
```sql
UPDATE Maps
SET TiledDataPath = 'Mods/mod-a/Maps/LittlerootTown.json',
    SourceMod = 'mod-a'
WHERE MapId = 'littleroot_town';
```

**Steps**:
1. [ ] Apply update
2. [ ] Load map
3. [ ] Check which NPC appears

**Expected**: MOD_A_NPC visible, MOD_B_NPC missing

### Part B: Switch to Mod B
```sql
UPDATE Maps
SET TiledDataPath = 'Mods/mod-b/Maps/LittlerootTown.json',
    SourceMod = 'mod-b'
WHERE MapId = 'littleroot_town';
```

**Steps**:
1. [ ] Apply update
2. [ ] Reload map
3. [ ] Check which NPC appears

**Expected**: MOD_B_NPC visible, MOD_A_NPC missing

### Pass/Fail: ___________

**Notes**:
```
_________________________________________________
_________________________________________________
_________________________________________________
```

---

## Test 5: Error Handling 🚨

### Test Missing File
```sql
UPDATE Maps
SET TiledDataPath = 'Mods/nonexistent/Maps/Fake.json'
WHERE MapId = 'littleroot_town';
```

**Steps**:
1. [ ] Try to load map
2. [ ] Check error message

**Expected**:
- [ ] Clear error message (not crash)
- [ ] Error includes file path
- [ ] Game remains stable

### Test Invalid JSON
```bash
# Create invalid JSON
echo "{ broken json" > PokeSharp.Game/Mods/test-mod/Maps/Bad.json
```

```sql
UPDATE Maps
SET TiledDataPath = 'Mods/test-mod/Maps/Bad.json'
WHERE MapId = 'littleroot_town';
```

**Expected**:
- [ ] JSON parse error shown
- [ ] Error includes file path
- [ ] Game doesn't crash

### Pass/Fail: ___________

---

## Cleanup
```bash
# Remove test mods
rm -rf PokeSharp.Game/Mods/test-mod
rm -rf PokeSharp.Game/Mods/test-override
rm -rf PokeSharp.Game/Mods/mod-a
rm -rf PokeSharp.Game/Mods/mod-b
```

```sql
-- Reset database
DELETE FROM Maps WHERE SourceMod IS NOT NULL;

UPDATE Maps
SET TiledDataPath = 'Assets/Data/Maps/LittlerootTown.json',
    SourceMod = NULL
WHERE MapId = 'littleroot_town';
```

---

## Summary

**Total Tests**: 5
**Passed**: _____ / 5
**Failed**: _____ / 5

### Critical Issues Found
```
1. _________________________________________________
2. _________________________________________________
3. _________________________________________________
```

### Minor Issues Found
```
1. _________________________________________________
2. _________________________________________________
3. _________________________________________________
```

### Recommendations
```
_________________________________________________
_________________________________________________
_________________________________________________
_________________________________________________
```

---

**Tested By**: _______________
**Date**: _______________
**Build**: _______________
**Sign-off**: ⬜ APPROVED  ⬜ NEEDS WORK
