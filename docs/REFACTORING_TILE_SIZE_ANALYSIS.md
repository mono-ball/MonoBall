# Refactoring Analysis: Remove Hardcoded Tile Size Constants

**Date:** 2025-11-15
**Objective:** Replace all hardcoded tile size constants with map-driven values from `MapInfo.TileSize`

---

## Executive Summary

**Total Usages Found:** 2 direct usages of `RenderingConstants.TileSize` + multiple hardcoded `16` defaults

**Recommendation:** **SAFE TO PROCEED** - The refactoring is low-risk with minimal breaking changes.

---

## 1. Direct Usages of `RenderingConstants.TileSize`

### File: `/PokeSharp.Engine.Rendering/Systems/ElevationRenderSystem.cs`

#### Usage 1: Field Initialization (Line 45)
```csharp
private int _tileSize = RenderingConstants.TileSize;
```

**Category:** Initialization (before map is loaded)
**Current Behavior:** Uses constant `8` as default tile size
**Analysis:** This is the initial value before any map is loaded
**Refactoring:** ✅ KEEP AS-IS - This is a valid fallback before map data is available
**Recommended Change:** Change default to `16` to match actual game tile size

**Action:**
```csharp
// BEFORE:
private int _tileSize = RenderingConstants.TileSize;  // 8

// AFTER:
private int _tileSize = 16;  // Default to standard Pokemon tile size
```

---

#### Usage 2: Fallback in SetTileSize (Line 66)
```csharp
var clamped = tileSize > 0 ? tileSize : RenderingConstants.TileSize;
```

**Category:** Default/fallback value
**Current Behavior:** Falls back to constant `8` if invalid tileSize passed
**Analysis:** This is a safety fallback for invalid inputs
**Refactoring:** ✅ KEEP AS FALLBACK - Valid defensive programming
**Recommended Change:** Change fallback to `16`

**Action:**
```csharp
// BEFORE:
var clamped = tileSize > 0 ? tileSize : RenderingConstants.TileSize;  // 8

// AFTER:
var clamped = tileSize > 0 ? tileSize : 16;  // Standard Pokemon tile size fallback
```

---

## 2. Hardcoded `16` Tile Size Defaults

### File: `/PokeSharp.Game.Components/Components/Movement/Position.cs`

#### Usage 1: Constructor Parameter Default (Line 42)
```csharp
public Position(int x, int y, int mapId = 0, int tileSize = 16)
```

**Category:** Initialization (used when creating Position without explicit tile size)
**Current Behavior:** Defaults to 16 if not specified
**Analysis:** Used in multiple places - backward compatibility fallback
**Refactoring:** ✅ KEEP AS-IS - This is correct and provides backward compatibility
**Action:** NO CHANGE NEEDED

---

#### Usage 2: SyncPixelsToGrid Parameter Default (Line 55)
```csharp
public void SyncPixelsToGrid(int tileSize = 16)
```

**Category:** Rendering code (should use MapInfo.TileSize)
**Current Behavior:** Defaults to 16 if not specified
**Analysis:** This is called from MovementSystem which passes the tile size explicitly
**Refactoring:** ⚠️ NEEDS REVIEW - Should this require tile size parameter?
**Recommended Change:** Make tile size required (remove default)

**Action:**
```csharp
// BEFORE:
public void SyncPixelsToGrid(int tileSize = 16)

// AFTER (OPTION 1 - Require parameter):
public void SyncPixelsToGrid(int tileSize)

// AFTER (OPTION 2 - Keep default for backward compatibility):
public void SyncPixelsToGrid(int tileSize = 16)  // Keep as-is
```

**Recommendation:** OPTION 2 - Keep default for backward compatibility

---

### File: `/PokeSharp.Game.Systems/Movement/MovementSystem.cs`

#### Usage 1: GetTileSize Default (Line 406)
```csharp
var tileSize = 16; // default
world.Query(
    in EcsQueries.MapInfo,
    (ref MapInfo mapInfo) =>
    {
        if (mapInfo.MapId == mapId)
            tileSize = mapInfo.TileSize;
    }
);
```

**Category:** Rendering code (should use MapInfo.TileSize)
**Current Behavior:** Falls back to 16 if no MapInfo found
**Analysis:** This is correct - uses MapInfo.TileSize when available, falls back to 16
**Refactoring:** ✅ KEEP AS-IS - Already using map-driven values!
**Action:** NO CHANGE NEEDED

---

### File: `/PokeSharp.Game/Initialization/PlayerFactory.cs`

#### Usage 1: PlayerFactory Default (Line 38)
```csharp
var tileSize = 16;
var mapInfoQuery = QueryCache.Get<MapInfo>();
_world.Query(
    in mapInfoQuery,
    (ref MapInfo mapInfo) =>
    {
        tileSize = mapInfo.TileSize;
    }
);
```

**Category:** Initialization code (before player is created)
**Current Behavior:** Falls back to 16 if no MapInfo found
**Analysis:** This is correct - uses MapInfo.TileSize when available
**Refactoring:** ✅ KEEP AS-IS - Already using map-driven values!
**Action:** NO CHANGE NEEDED

---

### File: `/PokeSharp.Game/Initialization/MapInitializer.cs`

#### Usage: SetTileSize from MapInfo (Lines 99-109)
```csharp
world.Query(
    in EcsQueries.MapInfo,
    (ref MapInfo mapInfo) =>
    {
        renderSystem.SetTileSize(mapInfo.TileSize);
        logger.LogWorkflowStatus(
            "Camera bounds updated",
            ("widthPx", mapInfo.PixelWidth),
            ("heightPx", mapInfo.PixelHeight)
        );
    }
);
```

**Category:** Map initialization (sets render system tile size)
**Current Behavior:** Reads from MapInfo.TileSize and sets it on render system
**Analysis:** This is the CORRECT implementation - already map-driven!
**Refactoring:** ✅ PERFECT - This is the model to follow
**Action:** NO CHANGE NEEDED

---

### File: `/PokeSharp.Game.Scripting/Services/MapApiService.cs`

#### Usage: GetTileSize Method (Lines 140-154)
```csharp
private int GetTileSize(int mapId)
{
    var tileSize = 16;

    _world.Query(
        in EcsQueries.MapInfo,
        (ref MapInfo mapInfo) =>
        {
            if (mapInfo.MapId == mapId)
                tileSize = mapInfo.TileSize;
        }
    );

    return tileSize;
}
```

**Category:** Rendering code (already using MapInfo)
**Current Behavior:** Falls back to 16 if no MapInfo found
**Analysis:** This is correct - uses MapInfo.TileSize when available
**Refactoring:** ✅ KEEP AS-IS - Already using map-driven values!
**Action:** NO CHANGE NEEDED

---

## 3. Initialization Order Analysis

### Current Initialization Flow:

```
1. ElevationRenderSystem created
   └─> _tileSize = RenderingConstants.TileSize (8)  ❌ WRONG DEFAULT

2. MapInitializer loads map
   └─> MapInfo entity created with TileSize from TMX
   └─> renderSystem.SetTileSize(mapInfo.TileSize)  ✅ CORRECT

3. PlayerFactory creates player
   └─> Reads MapInfo.TileSize (or defaults to 16)  ✅ CORRECT
   └─> new Position(x, y, mapId, tileSize)         ✅ CORRECT

4. MovementSystem updates entities
   └─> GetTileSize(mapId) from MapInfo            ✅ CORRECT
```

### Issues Found:

1. **ElevationRenderSystem default is 8, should be 16**
   - Before map is loaded, renders at wrong size
   - Fixed by MapInitializer.SetTileSize() once map loads
   - **Impact:** Minimal - only affects pre-map rendering

2. **No circular dependencies detected** ✅

3. **All systems properly query MapInfo.TileSize** ✅

---

## 4. Breaking Changes Analysis

### What Breaks if We Delete `RenderingConstants.TileSize`?

**Files Affected:** 1 file (ElevationRenderSystem.cs)

**Breaking Changes:**
1. Line 45: `private int _tileSize = RenderingConstants.TileSize;`
   - **Fix:** Change to `private int _tileSize = 16;`

2. Line 66: `var clamped = tileSize > 0 ? tileSize : RenderingConstants.TileSize;`
   - **Fix:** Change to `var clamped = tileSize > 0 ? tileSize : 16;`

**Impact:** ⚠️ LOW - Only 2 lines need changing

---

## 5. Recommended Refactoring Steps

### Step 1: Update ElevationRenderSystem Defaults

**File:** `/PokeSharp.Engine.Rendering/Systems/ElevationRenderSystem.cs`

```csharp
// Line 45 - BEFORE:
private int _tileSize = RenderingConstants.TileSize;

// Line 45 - AFTER:
private int _tileSize = 16; // Default to standard Pokemon tile size (updated from map)

// Line 66 - BEFORE:
var clamped = tileSize > 0 ? tileSize : RenderingConstants.TileSize;

// Line 66 - AFTER:
var clamped = tileSize > 0 ? tileSize : 16; // Fallback to standard tile size
```

---

### Step 2: Remove Constants from RenderingConstants.cs

**File:** `/PokeSharp.Engine.Rendering/RenderingConstants.cs`

```csharp
// DELETE THESE:
public const int DefaultImageWidth = 128;   // Not used anywhere
public const int DefaultImageHeight = 128;  // Not used anywhere
public const int TileSize = 8;              // Only used in ElevationRenderSystem (now replaced)
```

**Keep These:**
- `MaxRenderDistance` - Used in ElevationRenderSystem
- `SpriteRenderAfterLayer` - Not found in search (may be used elsewhere)
- `PerformanceLogInterval` - Used in ElevationRenderSystem
- `DefaultAssetRoot` - May be used in AssetManager

---

### Step 3: Add Documentation Comment

**File:** `/PokeSharp.Engine.Rendering/Systems/ElevationRenderSystem.cs`

```csharp
/// <summary>
/// Default tile size used before map is loaded. This is updated to the actual
/// tile size from MapInfo when SetTileSize() is called during map initialization.
/// Pokemon games typically use 16x16 pixel tiles.
/// </summary>
private int _tileSize = 16; // Updated from MapInfo during map load
```

---

## 6. Testing Strategy

### Unit Tests Needed:

1. **Test ElevationRenderSystem with no map loaded**
   - Verify it uses default 16px tile size
   - Verify rendering doesn't crash

2. **Test ElevationRenderSystem.SetTileSize()**
   - Verify it accepts valid tile sizes (8, 16, 32)
   - Verify it rejects invalid tile sizes (0, -1)
   - Verify fallback to 16 for invalid inputs

3. **Test MapInitializer**
   - Verify it reads TileSize from MapInfo
   - Verify it calls renderSystem.SetTileSize()

4. **Test Position constructor**
   - Verify default tile size of 16 works
   - Verify explicit tile size overrides work

---

## 7. Summary Table

| File | Line | Current Code | Refactoring | Risk |
|------|------|--------------|-------------|------|
| `ElevationRenderSystem.cs` | 45 | `_tileSize = RenderingConstants.TileSize` | Change to `16` | ⚠️ LOW |
| `ElevationRenderSystem.cs` | 66 | Fallback to `RenderingConstants.TileSize` | Change to `16` | ⚠️ LOW |
| `Position.cs` | 42 | Default `tileSize = 16` | ✅ Keep as-is | ✅ NONE |
| `Position.cs` | 55 | Default `tileSize = 16` | ✅ Keep as-is | ✅ NONE |
| `MovementSystem.cs` | 406 | Fallback `16`, then MapInfo | ✅ Keep as-is | ✅ NONE |
| `PlayerFactory.cs` | 38 | Fallback `16`, then MapInfo | ✅ Keep as-is | ✅ NONE |
| `MapInitializer.cs` | 103 | Reads from MapInfo.TileSize | ✅ Keep as-is | ✅ NONE |
| `MapApiService.cs` | 142 | Fallback `16`, then MapInfo | ✅ Keep as-is | ✅ NONE |
| `RenderingConstants.cs` | 17-23 | `DefaultImageWidth/Height` | ❌ Delete (unused) | ✅ NONE |
| `RenderingConstants.cs` | 55 | `TileSize = 8` | ❌ Delete | ⚠️ LOW |

---

## 8. Conclusion

### ✅ SAFE TO PROCEED

**Total Changes Required:** 4 lines across 2 files

1. Update `ElevationRenderSystem.cs` (2 lines)
2. Delete unused constants from `RenderingConstants.cs` (2 constants)

**Benefits:**
- Eliminates hardcoded tile size assumptions
- All systems already use map-driven tile sizes
- Consistent 16px default across codebase
- Better support for maps with different tile sizes

**Risks:**
- ⚠️ LOW - Only 2 lines of code change
- ⚠️ LOW - Default changes from 8 to 16 (matches actual game data)
- ✅ NONE - No initialization order issues
- ✅ NONE - No circular dependencies

**Next Steps:**
1. Update `ElevationRenderSystem.cs` defaults from 8 to 16
2. Delete unused constants from `RenderingConstants.cs`
3. Add documentation comments
4. Test rendering before/after map load
5. Verify map transitions work correctly

---

## 9. Additional Notes

### Why is RenderingConstants.TileSize = 8?

The constant is set to 8, but the actual game uses 16px tiles. This appears to be:
- Either a leftover from early development
- Or intended for Pokemon Emerald's 8x8 base tiles (which combine into 16x16 metatiles)

**Evidence that 16 is correct:**
- All Position constructors default to 16
- All GetTileSize() methods fall back to 16
- MapInfo defaults to 16
- Comments in code mention "16x16 pixels per tile"

### Related Files to Review

These files contain tile size references but are already correct:
- `/PokeSharp.Game.Data/MapLoading/Tiled/MapLoader.cs` (Line 1752) - Uses `tileHeight` from map
- `/PokeSharp.Game/Templates/ComponentDeserializerSetup.cs` (Line 137) - Template deserialization
- All documentation and README files referencing 16px tiles

---

**Analysis Complete** ✅
