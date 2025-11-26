# Pokémon Emerald Border Rendering System - Technical Analysis

## Executive Summary

This document provides a comprehensive technical analysis of how pokeemerald (the decompiled Pokémon Emerald) handles map border rendering. This analysis is based on direct examination of the pokeemerald source code and is intended to guide C# implementation in PokeSharp.

## Table of Contents

1. [Border Data Structures](#border-data-structures)
2. [Border Storage Format](#border-storage-format)
3. [Border Rendering Algorithm](#border-rendering-algorithm)
4. [Camera and Viewport Integration](#camera-and-viewport-integration)
5. [Coordinate Transformation System](#coordinate-transformation-system)
6. [Implementation Recommendations for C#](#implementation-recommendations-for-c)

---

## Border Data Structures

### 1. MapLayout Structure

The border is part of the `MapLayout` structure defined in `include/global.fieldmap.h`:

```c
struct MapLayout {
    s32 width;                              // Map width in metatiles
    s32 height;                             // Map height in metatiles
    const u16 *border;                      // Pointer to border metatile data (2x2 array)
    const u16 *map;                         // Pointer to main map grid data
    const struct Tileset *primaryTileset;   // Primary tileset reference
    const struct Tileset *secondaryTileset; // Secondary tileset reference
};
```

**Key Insight**: The `border` pointer references a **2x2 array of metatile IDs** that tile infinitely to fill areas outside the map bounds.

### 2. Border Block Structure

Border blocks are stored as 16-bit values with the following bit layout:

```
Bits 0-9   (10 bits): Metatile ID (MAPGRID_METATILE_ID_MASK = 0x03FF)
Bits 10-11 (2 bits):  Collision    (MAPGRID_COLLISION_MASK  = 0x0C00)
Bits 12-15 (4 bits):  Elevation    (MAPGRID_ELEVATION_MASK  = 0xF000)
```

**Critical Detail**: Border blocks are **ALWAYS** marked as impassable by ORing with `MAPGRID_COLLISION_MASK`.

### 3. Default Border Metatiles

For pokeemerald (and pokeruby), the default 2x2 border uses these metatile IDs:

```
+-------+-------+
| 0x1D4 | 0x1D5 |  Top row
+-------+-------+
| 0x1DC | 0x1DD |  Bottom row
+-------+-------+
```

**Layout Order**: Top-left, top-right, bottom-left, bottom-right

---

## Border Storage Format

### Default 2x2 Border

The border is stored as a simple 4-element array:

```c
const u16 border[4] = {
    0x1D4,  // border[0] = top-left
    0x1D5,  // border[1] = top-right
    0x1DC,  // border[2] = bottom-left
    0x1DD   // border[3] = bottom-right
};
```

### Custom Border Sizes (Extended Feature)

While pokeemerald defaults to 2x2, some ROM hacks support custom dimensions:
- **pokefirered**: Supports configurable width/height via `Border Width` and `Border Height`
- **Modified projects**: Can enable custom sizes via project settings

For custom sizes, border metatiles are stored left-to-right, top-to-bottom:

```
For a 3x2 border:
[0][1][2]  // First row
[3][4][5]  // Second row
```

---

## Border Rendering Algorithm

### 1. Core Border Retrieval Macro

Defined in `src/fieldmap.c`:

```c
#define GetBorderBlockAt(x, y) ({ \
    u16 block; \
    int i; \
    const u16 *border = gMapHeader.mapLayout->border; \
    \
    i = (x + 1) & 1;              /* Get x coordinate modulo 2 */ \
    i += ((y + 1) & 1) * 2;       /* Add y coordinate modulo 2, times 2 */ \
    \
    block = border[i] | MAPGRID_COLLISION_MASK;  /* Get border block, mark impassable */ \
})
```

**Algorithm Breakdown**:

1. **Index Calculation**:
   - `i = (x + 1) % 2 + ((y + 1) % 2) * 2`
   - The `& 1` operation is a fast modulo 2
   - The `+ 1` offset ensures proper tiling pattern alignment

2. **Tiling Pattern**:
   ```
   For coordinates (x, y):
   x % 2 = 0, y % 2 = 0 → index 0 (top-left)
   x % 2 = 1, y % 2 = 0 → index 1 (top-right)
   x % 2 = 0, y % 2 = 1 → index 2 (bottom-left)
   x % 2 = 1, y % 2 = 1 → index 3 (bottom-right)
   ```

3. **Collision Marking**:
   - Result is ORed with `MAPGRID_COLLISION_MASK` (0x0C00)
   - This sets bits 10-11 to 1, marking the tile as impassable

### 2. Map Grid Block Retrieval

The main accessor that integrates borders:

```c
#define GetMapGridBlockAt(x, y) \
    (AreCoordsWithinMapGridBounds(x, y) ? \
        gBackupMapLayout.map[x + gBackupMapLayout.width * y] : \
        GetBorderBlockAt(x, y))
```

**Logic Flow**:
1. Check if coordinates are within map bounds
2. If YES: Return map data from the grid
3. If NO: Return border block using the tiling algorithm

### 3. Bounds Checking

```c
#define AreCoordsWithinMapGridBounds(x, y) \
    (x >= 0 && x < gBackupMapLayout.width && \
     y >= 0 && y < gBackupMapLayout.height)
```

---

## Camera and Viewport Integration

### 1. Camera Structure

Defined in `include/global.fieldmap.h`:

```c
struct Camera {
    bool8 active:1;  // Camera activation flag
    s32 x;           // Camera X position (in pixels)
    s32 y;           // Camera Y position (in pixels)
};

extern struct Camera gCamera;
```

### 2. Viewport Constants

From `include/fieldmap.h`:

```c
#define MAP_OFFSET     7    // Offset for map buffer (in metatiles)
#define MAP_OFFSET_W   (MAP_OFFSET * 2 + 1)  // 15 metatiles width
#define MAP_OFFSET_H   (MAP_OFFSET * 2)      // 14 metatiles height
```

**Screen Rendering Area**:
- Visible area: **32x32 tiles** (not metatiles)
- Metatile rendering buffer: **15x14 metatiles** (MAP_OFFSET_W x MAP_OFFSET_H)
- Each metatile = 2x2 tiles, so 15x14 metatiles = 30x28 tiles visible

### 3. Drawing the Entire Viewport

From `src/field_camera.c`:

```c
static void DrawWholeMapViewInternal(int x, int y, const struct MapLayout *mapLayout)
{
    int i;
    int j;

    // Iterate through 32x32 tile grid
    for (i = 0; i < 32; i++)
    {
        for (j = 0; j < 32; j++)
        {
            // Calculate metatile coordinates relative to camera
            int metatileX = (x / 16) + (i / 2);  // Convert to metatile coords
            int metatileY = (y / 16) + (j / 2);  // Convert to metatile coords

            // Draw the metatile at this screen position
            // This internally calls GetMapGridBlockAt, which returns
            // border blocks for out-of-bounds coordinates
            DrawMetatileAt(metatileX, metatileY);
        }
    }

    ScheduleBgCopyTilemapToVram(2);  // Mark BG2 for VRAM copy
}
```

**Key Points**:
- The function draws a 32x32 tile grid
- Each metatile covers 2x2 tiles
- Out-of-bounds coordinates automatically render border tiles
- No special "border rendering" code needed—it's integrated into the grid system

### 4. Incremental Border Updates (Scrolling)

When the camera pans, only the newly visible edge is redrawn:

```c
// Redraw top edge when scrolling north
static void RedrawMapSliceNorth(int x, int y)
{
    int i;
    for (i = 0; i < 32; i++)
    {
        DrawMetatileAt(x + (i / 2), y + 28);  // Top edge
    }
}

// Redraw bottom edge when scrolling south
static void RedrawMapSliceSouth(int x, int y)
{
    int i;
    for (i = 0; i < 32; i++)
    {
        DrawMetatileAt(x + (i / 2), y);  // Bottom edge
    }
}

// Redraw left edge when scrolling west
static void RedrawMapSliceWest(int x, int y)
{
    int i;
    for (i = 0; i < 32; i++)
    {
        DrawMetatileAt(x + 28, y + (i / 2));  // Left edge
    }
}

// Redraw right edge when scrolling east
static void RedrawMapSliceEast(int x, int y)
{
    int i;
    for (i = 0; i < 32; i++)
    {
        DrawMetatileAt(x, y + (i / 2));  // Right edge
    }
}
```

**Optimization**: Only redraw the single edge that scrolled into view, not the entire screen.

---

## Coordinate Transformation System

### 1. Map Coordinate to Tilemap Buffer Offset

From `src/field_camera.c`:

```c
static s16 MapPosToBgTilemapOffset(int x, int y)
{
    int adjustedX = (x - gSaveBlock1Ptr->pos.x) + MAP_OFFSET;
    int adjustedY = (y - gSaveBlock1Ptr->pos.y) + MAP_OFFSET;

    // Check if within 32x32 visible region
    if (adjustedX < 0 || adjustedX >= 32 || adjustedY < 0 || adjustedY >= 32)
        return -1;

    // Calculate tilemap buffer offset with camera wrap
    int cameraXOffset = gCamera.x / 16 % 32;
    int cameraYOffset = gCamera.y / 16 % 32;

    return ((adjustedY + cameraYOffset) % 32) * 32 + ((adjustedX + cameraXOffset) % 32);
}
```

**Coordinate Spaces**:
1. **World Coordinates**: Absolute position in the map (metatiles)
2. **Screen Coordinates**: Relative to player position, offset by MAP_OFFSET
3. **Buffer Coordinates**: Tilemap buffer index with camera wrapping

### 2. Player Position and Camera

```c
// Player position is stored in save data
gSaveBlock1Ptr->pos.x;  // Player X (metatile coordinate)
gSaveBlock1Ptr->pos.y;  // Player Y (metatile coordinate)

// Camera position includes pixel-level offsets
gCamera.x;  // Camera X (pixel coordinate)
gCamera.y;  // Camera Y (pixel coordinate)
```

### 3. Border Coordinate Calculation

When rendering at screen coordinates that fall outside the map:

```c
// Example: Rendering at screen position (-2, -1)
int worldX = gSaveBlock1Ptr->pos.x + (-2);  // Could be negative
int worldY = gSaveBlock1Ptr->pos.y + (-1);  // Could be negative

// GetMapGridBlockAt checks bounds
if (worldX < 0 || worldX >= mapWidth || worldY < 0 || worldY >= mapHeight)
{
    // Use border tiling algorithm
    int borderIndex = (worldX + 1) & 1;
    borderIndex += ((worldY + 1) & 1) * 2;
    return border[borderIndex] | MAPGRID_COLLISION_MASK;
}
```

---

## Implementation Recommendations for C#

### 1. Data Structures

```csharp
/// <summary>
/// Represents a 2x2 border tile pattern that repeats infinitely
/// </summary>
public class MapBorder
{
    /// <summary>
    /// Border metatile IDs in order: [0]=top-left, [1]=top-right,
    /// [2]=bottom-left, [3]=bottom-right
    /// </summary>
    public ushort[] BorderMetatiles { get; set; } = new ushort[4];

    /// <summary>
    /// Gets the border metatile ID at the given world coordinates
    /// using the 2x2 tiling pattern
    /// </summary>
    public ushort GetBorderMetatileAt(int x, int y)
    {
        // Calculate index using modulo 2 tiling pattern
        int index = ((x + 1) & 1) + (((y + 1) & 1) << 1);

        // Return border metatile with impassable collision flag
        const ushort COLLISION_IMPASSABLE = 0x0C00;
        return (ushort)(BorderMetatiles[index] | COLLISION_IMPASSABLE);
    }
}

/// <summary>
/// Map layout containing map data and border
/// </summary>
public class MapLayout
{
    public int Width { get; set; }
    public int Height { get; set; }
    public MapBorder Border { get; set; }
    public ushort[] MapData { get; set; }  // Width * Height array

    /// <summary>
    /// Gets the block at the given coordinates, returning border if out of bounds
    /// </summary>
    public ushort GetBlockAt(int x, int y)
    {
        if (x >= 0 && x < Width && y >= 0 && y < Height)
        {
            return MapData[x + Width * y];
        }
        else
        {
            return Border.GetBorderMetatileAt(x, y);
        }
    }
}
```

### 2. Camera and Viewport System

```csharp
public class Camera
{
    public bool Active { get; set; }
    public int X { get; set; }  // Pixel coordinates
    public int Y { get; set; }  // Pixel coordinates

    /// <summary>
    /// Gets the camera position in metatile coordinates
    /// </summary>
    public (int x, int y) GetMetatilePosition()
    {
        return (X / 16, Y / 16);  // 16 pixels per metatile
    }
}

public class MapRenderer
{
    private const int MAP_OFFSET = 7;
    private const int VISIBLE_TILES_WIDTH = 32;
    private const int VISIBLE_TILES_HEIGHT = 32;

    /// <summary>
    /// Renders the entire visible map viewport including borders
    /// </summary>
    public void DrawWholeMapView(MapLayout layout, Camera camera, int playerX, int playerY)
    {
        int cameraMetatileX = camera.X / 16;
        int cameraMetatileY = camera.Y / 16;

        // Draw 32x32 tile grid (16x16 metatiles)
        for (int screenY = 0; screenY < VISIBLE_TILES_HEIGHT; screenY += 2)
        {
            for (int screenX = 0; screenX < VISIBLE_TILES_WIDTH; screenX += 2)
            {
                // Convert screen coordinates to world coordinates
                int worldX = cameraMetatileX + (screenX / 2);
                int worldY = cameraMetatileY + (screenY / 2);

                // Get block (automatically handles borders for out-of-bounds)
                ushort block = layout.GetBlockAt(worldX, worldY);

                // Render the metatile at screen position
                DrawMetatileAt(block, screenX, screenY);
            }
        }
    }

    /// <summary>
    /// Optimized rendering for camera scrolling - only updates visible edge
    /// </summary>
    public void RedrawMapSliceNorth(MapLayout layout, Camera camera)
    {
        int cameraMetatileX = camera.X / 16;
        int cameraMetatileY = camera.Y / 16;
        int topEdgeY = cameraMetatileY + 28;  // Top edge of visible area

        for (int screenX = 0; screenX < VISIBLE_TILES_WIDTH; screenX += 2)
        {
            int worldX = cameraMetatileX + (screenX / 2);
            ushort block = layout.GetBlockAt(worldX, topEdgeY);
            DrawMetatileAt(block, screenX, 28);
        }
    }

    // Similar methods for RedrawMapSliceSouth, East, West...
}
```

### 3. Metatile Bit Packing

```csharp
/// <summary>
/// Utilities for working with packed metatile data
/// </summary>
public static class MetatileUtils
{
    public const ushort METATILE_ID_MASK = 0x03FF;    // Bits 0-9
    public const ushort COLLISION_MASK = 0x0C00;      // Bits 10-11
    public const ushort ELEVATION_MASK = 0xF000;      // Bits 12-15

    public static ushort GetMetatileId(ushort block)
    {
        return (ushort)(block & METATILE_ID_MASK);
    }

    public static byte GetCollision(ushort block)
    {
        return (byte)((block & COLLISION_MASK) >> 10);
    }

    public static byte GetElevation(ushort block)
    {
        return (byte)((block & ELEVATION_MASK) >> 12);
    }

    public static bool IsImpassable(ushort block)
    {
        return (block & COLLISION_MASK) != 0;
    }

    public static ushort CreateBlock(ushort metatileId, byte collision, byte elevation)
    {
        return (ushort)(
            (metatileId & METATILE_ID_MASK) |
            ((collision << 10) & COLLISION_MASK) |
            ((elevation << 12) & ELEVATION_MASK)
        );
    }
}
```

### 4. Border Tiling Visualization

The 2x2 border pattern tiles like this across the map edges:

```
... | D4 D5 D4 D5 | D4 D5 D4 D5 | ...
... | DC DD DC DD | DC DD DC DD | ...
----+-------------+-------------+----
... | D4 D5 D4 D5 | [MAP DATA ] | ...
... | DC DD DC DD | [  HERE   ] | ...
----+-------------+-------------+----
... | D4 D5 D4 D5 | [         ] | ...
... | DC DD DC DD | [         ] | ...
----+-------------+-------------+----

Legend:
D4 = 0x1D4 (top-left)
D5 = 0x1D5 (top-right)
DC = 0x1DC (bottom-left)
DD = 0x1DD (bottom-right)
```

### 5. Performance Optimization Tips

1. **Cache Border Calculations**: Pre-calculate border indices for common viewport positions
2. **Dirty Rectangle Tracking**: Only redraw changed regions, not the entire viewport
3. **Background Layer Separation**: Use separate layers for borders vs. map data (like GBA BG layers)
4. **Metatile Atlasing**: Store metatiles in a texture atlas for fast GPU rendering
5. **Frustum Culling**: Only render metatiles within camera frustum + small margin

---

## Key Differences from Current PokeSharp Implementation

Based on the provided source files, here are areas where the current implementation may need adjustment:

### Current Issues Identified:

1. **ElevationRenderSystem.cs**:
   - Currently uses `MapLoaderService.GetBlock()` which may not handle borders
   - Need to integrate border fallback logic

2. **MapDefinition.cs**:
   - `Border` property exists but border tiling algorithm may not be implemented
   - Need to add `GetBlockAt(x, y)` method with border fallback

3. **CameraFollowSystem.cs**:
   - Camera bounds clamping may prevent rendering borders
   - Need to allow camera to extend beyond map bounds for border visibility

4. **LayerProcessor.cs**:
   - Border layer processing may need separate handling
   - Ensure border metatiles are marked as impassable

### Recommended Changes:

1. **Add Border Tiling to MapDefinition**:
   ```csharp
   public ushort GetBlockAt(int x, int y)
   {
       if (x >= 0 && x < Width && y >= 0 && y < Height)
           return Blocks[y * Width + x];
       else
           return Border.GetBorderMetatileAt(x, y);
   }
   ```

2. **Update Camera Bounds**:
   - Don't clamp camera to map edges
   - Allow rendering MAP_OFFSET metatiles beyond map bounds

3. **Update Rendering Systems**:
   - Use `GetBlockAt()` instead of direct array access
   - Handle negative coordinates in rendering loops

---

## References

- [pokeemerald src/fieldmap.c](https://github.com/pret/pokeemerald/blob/master/src/fieldmap.c) - Border retrieval macros and grid access
- [pokeemerald include/global.fieldmap.h](https://github.com/pret/pokeemerald/blob/master/include/global.fieldmap.h) - Data structure definitions
- [pokeemerald include/fieldmap.h](https://github.com/pret/pokeemerald/blob/master/include/fieldmap.h) - Constants and function declarations
- [pokeemerald src/field_camera.c](https://github.com/pret/pokeemerald/blob/master/src/field_camera.c) - Viewport rendering and metatile drawing
- [Porymap Documentation - Editing Map Tiles](https://huderlem.github.io/porymap/manual/editing-map-tiles.html) - Map editor documentation
- [Porymap Documentation - Settings and Options](https://huderlem.github.io/porymap/manual/settings-and-options.html) - Border configuration options
- [Triple Layer Metatiles Wiki](https://github.com/pret/pokeemerald/wiki/Triple-layer-metatiles) - Metatile rendering details

---

## Conclusion

pokeemerald's border rendering system is elegantly simple:

1. **Borders are 2x2 metatile patterns** stored in the MapLayout
2. **Tiling algorithm** uses modulo 2 on coordinates to index into the 2x2 array
3. **Border blocks are always impassable** via collision flag
4. **No special rendering code** - borders integrate seamlessly into the grid system
5. **Camera viewport** draws a 32x32 tile area, automatically rendering borders when coordinates are out of bounds

The key insight is that borders aren't a special rendering pass—they're just what you get when you ask for a block outside the map bounds. This makes the implementation clean and efficient.
