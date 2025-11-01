# Analysis Report: Player Sprite Not Visible

**Date:** 2025-11-01
**Analyst:** Code Analyzer Agent
**Status:** ✅ ROOT CAUSE IDENTIFIED

---

## Executive Summary

The player sprite fails to render because **no textures are being loaded** into AssetManager due to a JSON deserialization case-sensitivity issue. The manifest file uses lowercase property names (`tilesets`, `sprites`, `maps`) while the C# classes use PascalCase (`Tilesets`, `Sprites`, `Maps`). System.Text.Json's default case-sensitive deserialization causes all asset lists to deserialize as `null`.

---

## Investigation Process

### 1. Initial Verification ✅
- **Map rendering:** WORKING (confirms rendering pipeline functional)
- **Asset manifest loading:** WORKING (file read succeeds)
- **Player entity creation:** WORKING (entity exists with correct components)
- **player.png file:** VALID (16x16 PNG, 4-bit colormap, non-interlaced)

### 2. Component Analysis ✅

#### Position Component
```csharp
// Player at grid (10, 8) → pixel (160, 128)
public Position(int x, int y) {
    X = x;
    Y = y;
    PixelX = x * 16f;  // 10 * 16 = 160
    PixelY = y * 16f;  // 8 * 16 = 128
}
```
- **Position:** (160, 128) pixels
- **Screen dimensions:** 800x600
- **Conclusion:** Player position IS within visible area ✅

#### RenderSystem Query
```csharp
var query = new QueryDescription().WithAll<Position, Sprite>();
world.Query(in query, (ref Position position, ref Sprite sprite) => {
    _spriteCount++;
    RenderSprite(ref position, ref sprite);
});
```
- **Query logic:** CORRECT ✅
- **Component check:** `HasTexture("player")` → **FALSE** ❌

### 3. Critical Discovery 🔍

#### Diagnostic Output
```
✅ Asset manifest loaded successfully
📄 Manifest JSON content:
{
  "tilesets": [
    {
      "id": "test-tileset",
      "path": "Tilesets/test-tileset.png",
      "tileWidth": 16,
      "tileHeight": 16
    }
  ],
  "sprites": [
    {
      "id": "player",
      "path": "Sprites/player.png"
    }
  ],
  ...
}

🔍 Deserialized manifest:
   Tilesets: 0  ❌
   Sprites: 0   ❌
   Maps: 0      ❌

Total Loaded Textures: 0
HasTexture('player'): False
```

---

## Root Cause Analysis

### The Problem: Case-Sensitive JSON Deserialization

**JSON Property Names (manifest.json):**
```json
{
  "tilesets": [...],  // lowercase
  "sprites": [...],   // lowercase
  "maps": [...]       // lowercase
}
```

**C# Property Names (AssetManifest.cs):**
```csharp
public class AssetManifest {
    public List<TilesetAssetEntry>? Tilesets { get; set; }  // PascalCase
    public List<SpriteAssetEntry>? Sprites { get; set; }    // PascalCase
    public List<MapAssetEntry>? Maps { get; set; }          // PascalCase
}
```

**Deserialization Code:**
```csharp
_manifest = JsonSerializer.Deserialize<AssetManifest>(json);
// Uses System.Text.Json with DEFAULT case-sensitive settings
```

### Why It Fails

1. `System.Text.Json` is **case-sensitive by default**
2. JSON keys: `"tilesets"` ≠ C# property: `Tilesets`
3. Deserialization succeeds but properties remain `null`
4. `_manifest.Sprites == null` → no sprites loaded
5. `AssetManager.HasTexture("player") == false`
6. RenderSystem skips sprite: `"Texture 'player' NOT FOUND"`

---

## Impact Chain

```
JSON case mismatch
    ↓
AssetManifest.Sprites = null
    ↓
LoadManifest() skips sprite loading loop
    ↓
AssetManager._textures dictionary is empty
    ↓
HasTexture("player") returns false
    ↓
RenderSystem.RenderSprite() returns early
    ↓
Player sprite never drawn ❌
```

---

## Solution Options

### Option 1: Fix JSON Property Names (Change JSON)
```json
{
  "Tilesets": [...],  // Match C# PascalCase
  "Sprites": [...],
  "Maps": [...]
}
```

### Option 2: Configure Case-Insensitive Deserialization (Recommended)
```csharp
var options = new JsonSerializerOptions {
    PropertyNameCaseInsensitive = true
};
_manifest = JsonSerializer.Deserialize<AssetManifest>(json, options);
```

### Option 3: Use JsonPropertyName Attributes
```csharp
public class AssetManifest {
    [JsonPropertyName("tilesets")]
    public List<TilesetAssetEntry>? Tilesets { get; set; }

    [JsonPropertyName("sprites")]
    public List<SpriteAssetEntry>? Sprites { get; set; }

    [JsonPropertyName("maps")]
    public List<MapAssetEntry>? Maps { get; set; }
}
```

---

## Recommendation

**Use Option 2** (case-insensitive deserialization):
- ✅ Simplest fix (one line change)
- ✅ Most robust (handles future case variations)
- ✅ No breaking changes to JSON format
- ✅ No attribute pollution in model classes

---

## Additional Findings

### Why the Map Still Works

The map loads via `MapLoader.LoadMap()` which:
1. Reads the map JSON directly (not via AssetManifest)
2. Loads tileset texture independently
3. Uses separate deserialization logic

This is why the map renders correctly while the player sprite does not.

---

## Files Analyzed

| File | Location | Purpose |
|------|----------|---------|
| AssetManager.cs | /PokeSharp.Rendering/Assets/ | Asset loading and caching |
| AssetManifest.cs | /PokeSharp.Rendering/Assets/ | Manifest data model |
| manifest.json | /PokeSharp.Game/Assets/ | Asset definitions |
| RenderSystem.cs | /PokeSharp.Rendering/Systems/ | Sprite rendering |
| PokeSharpGame.cs | /PokeSharp.Game/ | Game initialization |
| Position.cs | /PokeSharp.Core/Components/ | Position component |
| player.png | /PokeSharp.Game/Assets/Sprites/ | Player sprite texture |

---

## Test Evidence

### Console Output Before Fix
```
✅ Asset manifest loaded successfully
🔍 Deserialized manifest:
   Tilesets: 0
   Sprites: 0
   Maps: 0

Total Loaded Textures: 0
HasTexture('player'): False
❌ Player texture NOT found in AssetManager!
```

### Expected Output After Fix
```
✅ Asset manifest loaded successfully
🔍 Deserialized manifest:
   Tilesets: 1
   Sprites: 1
   Maps: 1

📦 Loading 1 tileset(s)...
   ✅ Tileset 'test-tileset' loaded successfully
🎨 Loading 1 sprite(s)...
   ✅ Sprite 'player' loaded successfully

Total Loaded Textures: 2
HasTexture('player'): True
✅ Player texture loaded: 16x16px
```

---

## Conclusion

**Problem:** JSON property name case mismatch preventing asset deserialization
**Impact:** Zero textures loaded, player sprite invisible
**Fix Complexity:** TRIVIAL (one line change)
**Confidence:** 100% (root cause confirmed via diagnostic logging)

The rendering pipeline is fully functional. Once the JSON deserialization is fixed, the player sprite will render immediately.

---

**Investigation completed successfully** ✅
