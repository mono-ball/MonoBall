# Hardcoded Values & Missing Features Analysis - Tiled Map Loader

**Generated:** 2025-11-08
**Analyzed By:** Coder Agent (Hive Mind)
**Files Analyzed:**
- `PokeSharp.Rendering/Loaders/TiledMapLoader.cs`
- `PokeSharp.Rendering/Loaders/MapLoader.cs`
- `PokeSharp.Game/Initialization/MapInitializer.cs`
- `PokeSharp.Rendering/Loaders/TiledJson/*.cs`

---

## Executive Summary

This analysis identifies **12 hardcoded values** and **15 missing Tiled JSON properties** in the map loading system. The most critical issue is the hardcoded tile size (16) in `MapInitializer.cs:76`, which prevents proper handling of maps with different tile dimensions.

---

## 1. HARDCODED VALUES

### 1.1 Critical Hardcoded Values

#### **MapInitializer.cs - Line 76**
```csharp
const int tileSize = 16;
```
**Impact:** HIGH
**Issue:** Hardcoded tile size prevents maps with 8x8, 32x32, or other tile sizes from working correctly.
**Should Use:** `tmxDoc.TileWidth` or `mapInfo.TileSize` from the loaded map data.
**Location:** `GetMapBounds()` method

---

### 1.2 Default Fallback Values in TiledMapLoader.cs

#### **Lines 76-77 - Tileset Dimensions**
```csharp
TileWidth = tiledTileset.TileWidth ?? 16,
TileHeight = tiledTileset.TileHeight ?? 16,
```
**Impact:** MEDIUM
**Issue:** Defaults to 16x16 if tileset doesn't specify dimensions.
**Recommendation:** These fallbacks are reasonable but should log a warning when used.

#### **Lines 78-80 - Tileset Metadata**
```csharp
TileCount = tiledTileset.TileCount ?? 0,
Spacing = tiledTileset.Spacing ?? 0,
Margin = tiledTileset.Margin ?? 0,
```
**Impact:** LOW
**Issue:** Default spacing/margin of 0 is correct, but TileCount=0 might cause issues.
**Recommendation:** Add validation to ensure TileCount is calculated if not provided.

#### **Lines 97-98 - Image Dimensions**
```csharp
Width = tiledTileset.ImageWidth ?? 0,
Height = tiledTileset.ImageHeight ?? 0,
```
**Impact:** MEDIUM
**Issue:** Image dimensions default to 0, which breaks tileset calculations.
**Recommendation:** Should be calculated from actual texture dimensions or throw error if missing.

#### **Line 164 - Animation Timing Conversion**
```csharp
frameDurations[i] = frame.Duration / 1000f; // Convert milliseconds to seconds
```
**Impact:** LOW
**Issue:** Hardcoded conversion factor. Not really a problem, but magic number.
**Recommendation:** Add constant `const float MS_TO_SECONDS = 1000f;`

---

### 1.3 Default Values in MapLoader.cs

#### **Lines 122-123 - Tileset Image Fallback**
```csharp
tileset.Image?.Width ?? 256,
tileset.Image?.Height ?? 256
```
**Impact:** HIGH
**Issue:** Hardcoded 256x256 default is completely arbitrary and will cause incorrect tile calculations.
**Recommendation:** Should load actual texture to get dimensions or throw error if unavailable.

#### **Line 212 - Assets Root Path**
```csharp
var assetsRoot = "Assets";
```
**Impact:** MEDIUM
**Issue:** Hardcoded "Assets" folder name prevents flexible project structure.
**Recommendation:** Make configurable via constructor parameter or config file.

#### **Line 670 - Default Image Width**
```csharp
var imageWidth = tileset.Image?.Width ?? 256;
```
**Impact:** HIGH
**Issue:** Another hardcoded 256 default that breaks tile source rect calculations.
**Related To:** Lines 122-123 issue above.

#### **Lines 244-248 - Layer Names**
```csharp
var layerName = layerIndex switch
{
    0 => "Ground",
    1 => "Objects",
    2 => "Overhead",
    _ => null,
};
```
**Impact:** HIGH
**Issue:** Hardcoded layer names and limited to exactly 3 layers.
**Recommendation:** Should iterate all layers dynamically and use layer properties to determine rendering order.

#### **Line 74 - Layer Count**
```csharp
for (var layerIndex = 0; layerIndex < 3; layerIndex++)
```
**Impact:** CRITICAL
**Issue:** Hardcoded to exactly 3 layers. Maps with more or fewer layers won't work.
**Recommendation:** Use `tmxDoc.Layers.Count` instead.

#### **Line 602 - Waypoint Wait Time**
```csharp
var waypointWaitTime = 1.0f;
```
**Impact:** LOW
**Issue:** Default wait time of 1 second is hardcoded.
**Note:** Already has override logic on lines 603-610, so this is just a fallback.

---

### 1.4 Hardcoded Constants (Acceptable)

#### **Lines 27-30 - Tiled Flip Flags**
```csharp
private const uint FLIPPED_HORIZONTALLY_FLAG = 0x80000000;
private const uint FLIPPED_VERTICALLY_FLAG = 0x40000000;
private const uint FLIPPED_DIAGONALLY_FLAG = 0x20000000;
private const uint TILE_ID_MASK = 0x1FFFFFFF;
```
**Impact:** NONE
**Status:** These are correct constants from Tiled format specification. No changes needed.

---

## 2. MISSING TILED JSON PROPERTIES

### 2.1 Map-Level Properties (TiledJsonMap.cs)

Properties defined in Tiled JSON spec but **NOT parsed**:

1. **`backgroundcolor`** - Map background color (hex string like "#ff0000")
2. **`hexsidelength`** - For hexagonal maps
3. **`staggeraxis`** - For staggered/hexagonal maps ("x" or "y")
4. **`staggerindex`** - For staggered/hexagonal maps ("odd" or "even")
5. **`parallaxoriginx`** - Parallax scrolling origin X
6. **`parallaxoriginy`** - Parallax scrolling origin Y
7. **`compressionlevel`** - Compression level (-1 to 9)
8. **`nextlayerid`** - Next available layer ID
9. **`nextobjectid`** - Next available object ID

**Current Support:** Basic orthogonal maps only
**Impact:** Cannot load hexagonal, isometric, or parallax maps

---

### 2.2 Layer Properties (TiledJsonLayer.cs)

Properties defined in Tiled JSON spec but **NOT parsed**:

1. **`offsetx`** - Layer X offset in pixels
2. **`offsety`** - Layer Y offset in pixels
3. **`parallaxx`** - Horizontal parallax factor
4. **`parallaxy`** - Vertical parallax factor
5. **`tintcolor`** - Layer tint color (hex string)
6. **`locked`** - Whether layer is locked in editor
7. **`startx`** - For infinite maps
8. **`starty`** - For infinite maps
9. **`chunks`** - For infinite maps (chunk-based data)

**Current Support:** Basic finite layers with opacity/visibility only
**Impact:** Cannot use layer offsets, parallax scrolling, or infinite maps

---

### 2.3 Tileset Properties (TiledJsonTileset.cs)

Properties defined in Tiled JSON spec but **NOT parsed**:

1. **`backgroundcolor`** - Tileset background color
2. **`transparentcolor`** - Transparent color key (hex string)
3. **`objectalignment`** - Object alignment ("topleft", "center", etc.)
4. **`tileoffset`** - Drawing offset for tiles (x, y)
5. **`grid`** - Grid settings for tile drawing
6. **`properties`** - Tileset-level custom properties
7. **`terrains`** - Terrain type definitions
8. **`wangsets`** - Wang tile sets for auto-tiling

**Current Support:** Basic tilesets with images and animations only
**Impact:** Cannot use Wang tiles, terrains, or tileset properties

---

### 2.4 Object Properties (TiledJsonObject.cs)

Properties defined in Tiled JSON spec but **NOT parsed**:

1. **`rotation`** - Object rotation in degrees
2. **`gid`** - Tile GID (for tile objects)
3. **`ellipse`** - Whether object is an ellipse
4. **`point`** - Whether object is a point
5. **`polygon`** - Polygon points array
6. **`polyline`** - Polyline points array
7. **`text`** - Text object properties (font, size, etc.)
8. **`template`** - Reference to external object template

**Current Support:** Rectangular objects only with position/size
**Impact:** Cannot use polygons, circles, points, text objects, or tile objects

---

### 2.5 Tile Definition Properties (TiledJsonTileDefinition.cs)

Properties defined in Tiled JSON spec but **NOT parsed**:

1. **`image`** - Individual tile image (for image collection tilesets)
2. **`imagewidth`** - Tile image width
3. **`imageheight`** - Tile image height
4. **`objectgroup`** - Collision shapes for this tile
5. **`probability`** - Tile probability for random placement
6. **`terrain`** - Terrain type indices

**Current Support:** Animations and properties only
**Impact:** Cannot use image collection tilesets or tile collision shapes

---

## 3. ECS CONVERSION LOGIC ANALYSIS

### 3.1 Tile to Entity Conversion

**Location:** `MapLoader.cs:326-480` (`CreateTileEntity` method)

**Process:**
1. Extract flip flags from GID (lines 85-90)
2. Get tile properties from tileset (lines 340-343)
3. Determine template ID based on properties (line 348)
4. Create entity from template OR manual creation (lines 352-456)
5. Add additional components not in templates (lines 459-479)

**Components Created:**
- **Always:** `TilePosition`, `TileSprite`
- **Template-based:** Varies by template (`tile/wall`, `tile/ledge/*`, `tile/grass`, `tile/ground`)
- **Property-based:** `Collision`, `TileLedge`, `EncounterZone`, `TerrainType`, `TileScript`

**Template Determination Logic (lines 267-324):**
```
Priority Order:
1. ledge_direction → "tile/ledge/{direction}"
2. solid=true → "tile/wall"
3. encounter_rate>0 → "tile/grass"
4. default → "tile/ground"
```

**Issues Found:**
- Hardcoded layer names limit flexibility
- No support for Tiled object collision shapes
- No support for tile rotation property
- No support for terrain types from Tiled

---

### 3.2 Object to Entity Conversion

**Location:** `MapLoader.cs:491-647` (`SpawnMapObjects` method)

**Process:**
1. Get template ID from object type or properties (lines 503-508)
2. Check if template exists (lines 517-521)
3. Convert pixel coords to tile coords (lines 525-526)
4. Spawn entity from template with overrides (lines 531-623)

**Components Created:**
- **Position override:** Always applied
- **Direction override:** From "direction" property
- **NPC-specific:** `Npc`, `Name`, `MovementRoute` (waypoints)

**Special Object Properties Handled:**
- `template` - Alternative to type field
- `direction` - NPC facing direction
- `npcId` - NPC identifier
- `displayName` - NPC display name
- `waypoints` - Patrol route (format: "x1,y1;x2,y2;x3,y3")
- `waypointWaitTime` - Pause duration at waypoints

**Issues Found:**
- Only handles rectangular objects (no polygons, circles, etc.)
- No support for object rotation
- No support for tile objects (gid property)
- No support for object templates (external .tx files)
- Coordinate conversion hardcoded for tileHeight (line 525-526)

---

## 4. DATA FLOW DIAGRAM

```
Tiled JSON File
    ↓
TiledMapLoader.Load()
    ↓ Deserialize
TiledJsonMap
    ↓ Convert
TmxDocument
    ↓
MapLoader.LoadMapEntities()
    ↓
├─ CreateTileEntity() → ECS Entities (tiles)
│   ├─ TilePosition
│   ├─ TileSprite
│   └─ Template components OR manual components
│
├─ CreateAnimatedTileEntities() → AnimatedTile components
│
└─ SpawnMapObjects() → ECS Entities (NPCs, items)
    ├─ Position
    ├─ Direction
    └─ Template components + overrides
```

---

## 5. CRITICAL ISSUES SUMMARY

### Priority 1 (CRITICAL - Breaks Functionality)
1. **Hardcoded 3 layers** (MapLoader.cs:74) - Maps with ≠3 layers fail
2. **Hardcoded layer names** (MapLoader.cs:244-248) - Prevents custom layer structure
3. **Hardcoded tile size** (MapInitializer.cs:76) - Breaks non-16x16 maps
4. **Hardcoded 256x256 image fallback** (MapLoader.cs:122, 670) - Incorrect tile calculations

### Priority 2 (HIGH - Limits Features)
1. **Missing layer offset/parallax properties** - No parallax scrolling
2. **Missing object shape properties** - Only rectangles supported
3. **Missing infinite map support** - Cannot load infinite maps
4. **Missing tile collision shapes** - Cannot use Tiled collision editor

### Priority 3 (MEDIUM - Quality of Life)
1. **Hardcoded "Assets" path** - Limits project flexibility
2. **Missing terrain/Wang sets** - Cannot use auto-tiling features
3. **Missing tileset properties** - Cannot use tileset metadata
4. **No rotation support** - Objects always axis-aligned

---

## 6. RECOMMENDATIONS

### Immediate Fixes
1. **Replace hardcoded layer count with dynamic iteration:**
   ```csharp
   for (var layerIndex = 0; layerIndex < tmxDoc.Layers.Count; layerIndex++)
   ```

2. **Use map's actual tile size instead of constant:**
   ```csharp
   public Rectangle GetMapBounds(int mapWidthInTiles, int mapHeightInTiles, int tileSize)
   {
       return new Rectangle(0, 0, mapWidthInTiles * tileSize, mapHeightInTiles * tileSize);
   }
   ```

3. **Load actual texture dimensions instead of 256 fallback:**
   ```csharp
   // Load texture first to get real dimensions
   var texture = _assetManager.GetTexture(tilesetId);
   var imageWidth = texture?.Width ?? throw new InvalidOperationException("...");
   ```

### Medium-Term Enhancements
1. Add support for layer offsets and parallax
2. Parse polygon/ellipse/point object shapes
3. Support object rotation property
4. Add validation warnings for missing properties

### Long-Term Improvements
1. Full Tiled JSON 1.11.2 spec compliance
2. Support infinite maps (chunk-based loading)
3. Support Wang sets and terrains
4. Support image collection tilesets

---

## 7. TESTING RECOMMENDATIONS

### Test Cases Needed
1. Maps with 8x8, 32x32, 64x64 tile sizes
2. Maps with 1 layer, 2 layers, 5+ layers
3. Maps with custom layer names
4. Maps with layer offsets
5. Tilesets with spacing/margin
6. Objects with polygons, ellipses, points
7. Rotated objects
8. Tile objects (using GID)

---

## Appendix A: Complete Property Checklist

### ✅ Currently Parsed
- Map: version, tiledversion, type, orientation, renderorder, width, height, tilewidth, tileheight, infinite, layers, tilesets, properties
- Layer: id, name, type, width, height, visible, opacity, x, y, data, encoding, compression, objects, properties
- Tileset: firstgid, source, name, tilewidth, tileheight, tilecount, columns, image, imagewidth, imageheight, spacing, margin, tiles
- Object: id, name, type, x, y, width, height, visible, properties
- Tile: id, animation, properties, type

### ❌ Missing but Defined in Spec
- Map: backgroundcolor, hexsidelength, staggeraxis, staggerindex, parallaxoriginx, parallaxoriginy, compressionlevel, nextlayerid, nextobjectid
- Layer: offsetx, offsety, parallaxx, parallaxy, tintcolor, locked, startx, starty, chunks
- Tileset: backgroundcolor, transparentcolor, objectalignment, tileoffset, grid, properties, terrains, wangsets
- Object: rotation, gid, ellipse, point, polygon, polyline, text, template
- Tile: image, imagewidth, imageheight, objectgroup, probability, terrain

---

**End of Report**
