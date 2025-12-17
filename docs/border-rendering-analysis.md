# Border Rendering System Analysis

## Overview

This document analyzes the border rendering system in PokeSharp to understand why borders might stop rendering. The system follows Pokemon Emerald's 2x2 border tiling pattern for infinite border rendering outside map bounds.

## System Components

### 1. MapBorder Component (`MapBorder.cs`)

**Purpose**: Stores border tile data for a map using a 2x2 tiling pattern.

**Key Properties**:
- `BottomLayerGids[4]`: Ground-level tiles (grass, tree trunks) in [TopLeft, TopRight, BottomLeft, BottomRight] order
- `TopLayerGids[4]`: Overhead tiles (tree canopy, rooftops) in same order
- `TilesetId`: The tileset containing border tiles
- `BottomSourceRects[4]`: Pre-calculated source rectangles for bottom layer
- `TopSourceRects[4]`: Pre-calculated source rectangles for top layer

**Important Properties**:
```csharp
public readonly bool HasBorder =>
    BottomLayerGids is { Length: 4 } && !string.IsNullOrEmpty(TilesetId);

public readonly bool HasTopLayer =>
    TopLayerGids is { Length: 4 } && TopLayerGids.Any(gid => gid > 0);
```

**Border Tile Selection Algorithm**:
```csharp
public static int GetBorderTileIndex(int x, int y)
{
    // x & 1: 0 for even, 1 for odd (selects left/right)
    // y & 1: 0 for even, 1 for odd (selects top/bottom)
    return (x & 1) + ((y & 1) << 1);
}
```

### 2. BorderProcessor (`BorderProcessor.cs`)

**Purpose**: Processes border data from Tiled map properties and creates MapBorder components.

**Key Methods**:

#### `ParseBorder(TmxDocument tmxDoc, IReadOnlyList<LoadedTileset> tilesets)`
Parses border data from map properties and creates a MapBorder component.

**Conditions for Failure**:
1. **No border property**: Returns `null` if `tmxDoc.Properties` is null or doesn't contain "border" key
2. **Parse failure**: Returns `null` if border data structure is invalid
3. **No tilesets**: Returns `null` if `tilesets.Count == 0`
4. **Exceptions**: Returns `null` on any exception during parsing

**Border Data Format**:
```json
{
  "border": {
    "top_left": 1,
    "top_right": 3,
    "bottom_left": 5,
    "bottom_right": 7,
    "top_left_top": 2,
    "top_right_top": 4,
    "bottom_left_top": 6,
    "bottom_right_top": 8
  }
}
```

**Processing Steps**:
1. Extract border property from TMX document
2. Parse 8 GID values (4 bottom + 4 top layer)
3. Get primary tileset (first tileset in list)
4. Pre-calculate source rectangles for all 4 bottom layer tiles
5. Pre-calculate source rectangles for all 4 top layer tiles (if GID > 0)
6. Create MapBorder struct

#### `AddBorderToEntity(World world, Entity mapInfoEntity, TmxDocument tmxDoc, IReadOnlyList<LoadedTileset> tilesets)`
Adds MapBorder component to the map entity if border data exists.

**Returns**: `true` if border added, `false` otherwise

### 3. ElevationRenderSystem - RenderBorders Method (`ElevationRenderSystem.cs`)

**Purpose**: Renders border tiles when camera extends beyond map bounds.

#### Border Rendering Flow

**Step 1: Early Exit Conditions**
```csharp
if (_cachedMapBorders.Count == 0 || !_cachedCameraBounds.HasValue || _cachedPlayerMapId == null)
{
    return 0; // NO BORDERS RENDERED
}
```

**Condition 1**: `_cachedMapBorders.Count == 0`
- Map has no MapBorder component
- BorderProcessor failed to parse border data
- Map loaded without tilesets

**Condition 2**: `!_cachedCameraBounds.HasValue`
- Camera bounds not calculated (viewport issue)
- Should never happen with proper initialization

**Condition 3**: `_cachedPlayerMapId == null`
- Player position not cached this frame
- No Player entity with Position component exists

**Step 2: Find Player's Map Border**
```csharp
MapBorderInfo? playerMapBorder = null;
foreach (MapBorderInfo borderInfo in _cachedMapBorders)
{
    if (borderInfo.MapIdValue == _cachedPlayerMapId)
    {
        playerMapBorder = borderInfo;
        break;
    }
}

if (!playerMapBorder.HasValue)
{
    return 0; // NO BORDERS RENDERED
}
```

**Critical Condition**: Borders ONLY render for the player's current map. If player is on a different map than expected, no borders render.

**Step 3: Camera Bounds Check**
```csharp
if (cameraBounds.Left >= mapOriginTileX
    && cameraBounds.Right <= mapRightTile
    && cameraBounds.Top >= mapOriginTileY
    && cameraBounds.Bottom <= mapBottomTile)
{
    return 0; // NO BORDERS RENDERED
}
```

**Optimization**: If camera is entirely within map bounds, skip border rendering entirely.

**Step 4: Texture Check**
```csharp
if (!AssetManager.TryGetTexture(border.TilesetId, out Texture2D? texture) || texture == null)
{
    if (!_loggedBorderTextureWarning)
    {
        _logger?.LogWarning(
            "Border texture not found: TilesetId='{TilesetId}', MapId='{MapId}'",
            border.TilesetId,
            primaryBorder.MapIdValue
        );
        _loggedBorderTextureWarning = true;
    }
    return 0; // NO BORDERS RENDERED
}
```

**Critical Condition**: Border tileset texture must be loaded in AssetManager.

**Step 5: Tile-by-Tile Rendering**
```csharp
for (int y = renderTop; y < renderBottom; y++)
{
    for (int x = renderLeft; x < renderRight; x++)
    {
        // Skip tiles INSIDE ANY loaded map
        if (IsTileInsideAnyMap(x, y))
        {
            continue; // DON'T RENDER BORDER HERE
        }

        // Render bottom layer
        // Render top layer (if HasTopLayer)
    }
}
```

**Critical Logic**: Borders only render in tiles that are NOT inside any loaded map's bounds.

### 4. MapLoader Integration (`MapLoader.cs`)

**Border Loading Process** (lines 923-937):
```csharp
// Process border data (Pokemon Emerald-style 2x2 border pattern)
// Adds MapBorder component to map entity if border property exists
if (loadedTilesets.Count > 0)
{
    bool hasBorder = _borderProcessor.AddBorderToEntity(
        world,
        mapInfoEntity,
        tmxDoc,
        loadedTilesets
    );
    if (hasBorder)
    {
        _logger?.LogInformation("Border data loaded for map '{MapName}'", context.MapName);
    }
}
```

**Critical Dependency**: Border processing only happens if `loadedTilesets.Count > 0`.

## Conditions That Stop Border Rendering

### 1. Map Loading Issues

**No Tilesets Loaded**
- Tiled map has no tilesets
- Tileset files failed to load
- Tileset texture files missing
- **Effect**: `loadedTilesets.Count == 0` → Border processing skipped

**No Border Property in Tiled Map**
- Map properties don't contain "border" key
- **Effect**: `BorderProcessor.ParseBorder()` returns `null`

**Invalid Border Data**
- Border property exists but malformed
- JSON parsing fails
- Missing required fields (top_left, top_right, etc.)
- **Effect**: `BorderProcessor.ParseBorder()` returns `null`

### 2. Runtime Rendering Issues

**Player Map ID Mismatch**
- `_cachedPlayerMapId` is null
- `_cachedPlayerMapId` doesn't match any loaded map with borders
- Player entity missing or has no Position component
- **Effect**: `playerMapBorder` not found → borders don't render

**Camera Completely Inside Map**
- Camera bounds entirely within current map bounds
- Optimization: borders not needed
- **Effect**: Early exit, no rendering

**Border Texture Missing**
- Border tileset texture not in AssetManager
- Texture failed to load
- Wrong TilesetId in MapBorder
- **Effect**: Cannot render, warning logged once

**All Tiles Inside Maps**
- Multiple connected maps cover all visible area
- `IsTileInsideAnyMap()` returns true for every tile
- **Effect**: No border tiles rendered (by design)

### 3. Cache/State Issues

**MapBorder Component Missing**
- MapBorder component not added to map entity
- Border processing failed during load
- **Effect**: Map not in `_cachedMapBorders` cache

**Camera Bounds Not Calculated**
- `_cachedCameraBounds` is null
- Camera viewport width/height is 0
- **Effect**: Early exit from RenderBorders

**Map Bounds Cache Empty**
- `_cachedMapBounds.Count == 0`
- No maps with MapInfo + MapWorldPosition loaded
- **Effect**: Border exclusion logic may fail

## Map Connection Relationship

**MapConnection Structure**:
- Defines directional connections between maps
- Contains: `Direction`, `TargetMapId`, `OffsetInTiles`
- Used by MapStreamingSystem for loading adjacent maps

**Border Behavior with Connections**:
1. Borders render for player's current map ONLY
2. When multiple maps are loaded (via connections):
   - Each map can have its own borders
   - `IsTileInsideAnyMap()` checks ALL loaded maps
   - Borders don't render inside any map bounds
3. Connection offset affects map positioning, not border rendering directly

**Example Scenario**:
- Player in Route 101 (has borders)
- Route 101 connects North to Oldale Town
- Both maps loaded at different world offsets
- Borders render ONLY:
  - For Route 101 (player's map)
  - In tiles NOT inside Route 101 OR Oldale Town bounds

## Diagnostic Checklist

When borders stop rendering, check:

### Map Data
- [ ] Does Tiled map have "border" property?
- [ ] Are all 4 bottom layer GIDs valid? (top_left, top_right, bottom_left, bottom_right)
- [ ] Are tilesets loaded? (`loadedTilesets.Count > 0`)
- [ ] Is border tileset ID correct?

### Runtime State
- [ ] Is MapBorder component on map entity? (Check `_cachedMapBorders`)
- [ ] Is player's MapId matching a map with borders? (`_cachedPlayerMapId`)
- [ ] Is camera extending beyond map bounds?
- [ ] Is border texture loaded in AssetManager?

### Multi-Map Scenarios
- [ ] Are other maps covering the border area? (Check `_cachedMapBounds`)
- [ ] Is player transitioning between maps? (MapId might be stale)
- [ ] Are map connections configured correctly?

### Logging
Check logs for:
- "Border data loaded for map '{MapName}'" - Border successfully parsed
- "Border texture not found: TilesetId='...'" - Texture missing
- "No border property found in map" - Border property missing
- "Failed to parse border data from property" - Parse error

## Performance Notes

1. **Border Cache Updated Per Frame**: `UpdateMapBordersCache()` queries all maps with MapBorder components
2. **Render Optimization**: Early exit if camera fully inside map
3. **Tile-Level Culling**: Skip border tiles inside any loaded map bounds
4. **Texture Warning Once**: `_loggedBorderTextureWarning` prevents log spam

## Related Systems

- **MapStreamingSystem**: Loads adjacent maps, affecting border visibility
- **SpatialHashSystem**: Tracks tile positions for efficient queries
- **AssetManager**: Must have border tileset textures loaded
- **Camera**: Bounds determine which border tiles to render

## Summary

Borders stop rendering when:
1. **Map doesn't have border data** (most common)
2. **Player is on a different map** than the one with borders
3. **Border texture is missing** from AssetManager
4. **Camera is entirely inside map bounds** (optimization)
5. **All visible tiles are inside loaded maps** (multi-map scenario)

The system is designed to render borders ONLY for the player's current map, ONLY outside all loaded map bounds, creating seamless transitions between connected maps.
