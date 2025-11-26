# Pokeemerald Tile Animation System - Research Report

## Executive Summary

This document details how pokeemerald (Pokémon Emerald decompilation) handles tile and metatile animations on the Game Boy Advance. The system uses **tile replacement** (not palette cycling) where animation frames directly replace tile data in VRAM during vertical blank.

---

## 1. Core Concepts

### 1.1 Tile Hierarchy

Pokemon maps use a 3-level tile hierarchy:

1. **8x8 Tiles (Base Unit)**
   - 4bpp (16 colors) indexed graphics
   - 32 bytes per tile (8x8 pixels × 4 bits/pixel)
   - Stored in VRAM at specific offsets

2. **16x16 Metatiles**
   - Composed of **4 tiles** arranged in 2x2 grid:
     ```
     [Tile 0] [Tile 1]
     [Tile 2] [Tile 3]
     ```
   - Each tile can have different palette indices (0-15)
   - Metatiles define map building blocks (grass, water, rocks, etc.)

3. **Map Layer**
   - Grid of metatile IDs
   - Rendered to screen using metatile definitions

### 1.2 Tileset Structure

Each map uses TWO tilesets:

- **Primary Tileset** (512 tiles, indices 0-511)
  - Common tiles shared across regions (grass, water, trees, flowers)
  - Examples: General, Building

- **Secondary Tileset** (512 tiles, indices 512-1023)
  - Location-specific tiles (town buildings, gym interiors)
  - Examples: Rustboro, Dewford, Lavaridge, Underwater

```c
struct Tileset {
    bool8 isCompressed;           // Tile data compression
    bool8 isSecondary;            // Primary (false) or Secondary (true)
    const u32 *tiles;             // Tile graphics data
    const u16 (*palettes)[16];    // 16 palettes × 16 colors
    const u16 *metatiles;         // Metatile definitions (4 tiles each)
    const u16 *metatileAttributes;// Collision/behavior data
    TilesetCB callback;           // Animation initialization function
};
```

---

## 2. Animation System Architecture

### 2.1 Overview

Pokeemerald uses **tile replacement** for animations:
- Pre-rendered animation frames stored in ROM
- Each frame is a complete set of tile graphics
- DMA copies frames to VRAM at specific tile offsets
- **NOT palette cycling** - actual tile pixel data changes

### 2.2 Key Components

#### Global State Variables
```c
static u16 sPrimaryTilesetAnimCounter;      // Frame counter (0-255)
static u16 sPrimaryTilesetAnimCounterMax;   // Counter wrap value (usually 256)
static u16 sSecondaryTilesetAnimCounter;
static u16 sSecondaryTilesetAnimCounterMax;
static void (*sPrimaryTilesetAnimCallback)(u16);   // Per-tileset update function
static void (*sSecondaryTilesetAnimCallback)(u16);
```

#### DMA Transfer Buffer
```c
static struct {
    const u16 *src;    // Source data (ROM)
    u16 *dest;         // Destination (VRAM address)
    u16 size;          // Transfer size in bytes
} sTilesetDMA3TransferBuffer[20];  // Up to 20 concurrent animations
```

### 2.3 Initialization Flow

```c
void InitTilesetAnimations(void)
{
    ResetTilesetAnimBuffer();
    _InitPrimaryTilesetAnimation();    // Call tileset->callback() if exists
    _InitSecondaryTilesetAnimation();  // Call tileset->callback() if exists
}
```

Each tileset's callback (e.g., `InitTilesetAnim_General()`) sets:
- `sPrimaryTilesetAnimCounter = 0`
- `sPrimaryTilesetAnimCounterMax = 256` (frame count before wrap)
- `sPrimaryTilesetAnimCallback = TilesetAnim_General` (update function)

### 2.4 Update Loop (Called Every Frame)

```c
void UpdateTilesetAnimations(void)
{
    ResetTilesetAnimBuffer();

    // Increment frame counters (wrap at max)
    if (++sPrimaryTilesetAnimCounter >= sPrimaryTilesetAnimCounterMax)
        sPrimaryTilesetAnimCounter = 0;
    if (++sSecondaryTilesetAnimCounter >= sSecondaryTilesetAnimCounterMax)
        sSecondaryTilesetAnimCounter = 0;

    // Call tileset-specific animation functions
    if (sPrimaryTilesetAnimCallback)
        sPrimaryTilesetAnimCallback(sPrimaryTilesetAnimCounter);
    if (sSecondaryTilesetAnimCallback)
        sSecondaryTilesetAnimCallback(sSecondaryTilesetAnimCounter);
}

void TransferTilesetAnimsBuffer(void)
{
    // Perform DMA transfers during VBlank
    for (int i = 0; i < sTilesetDMA3TransferBufferSize; i++)
        DmaCopy16(3, buffer[i].src, buffer[i].dest, buffer[i].size);
    sTilesetDMA3TransferBufferSize = 0;
}
```

---

## 3. Animation Definition Structure

### 3.1 Frame Data Storage

Animation frames are stored as arrays of tile graphics:

```c
// Flower animation - 4 frames stored in ROM
const u16 gTilesetAnims_General_Flower_Frame0[] = INCBIN_U16("data/tilesets/primary/general/anim/flower/0.4bpp");
const u16 gTilesetAnims_General_Flower_Frame1[] = INCBIN_U16("data/tilesets/primary/general/anim/flower/1.4bpp");
const u16 gTilesetAnims_General_Flower_Frame2[] = INCBIN_U16("data/tilesets/primary/general/anim/flower/2.4bpp");

// Frame pointer array (used for cycling)
const u16 *const gTilesetAnims_General_Flower[] = {
    gTilesetAnims_General_Flower_Frame0,
    gTilesetAnims_General_Flower_Frame1,
    gTilesetAnims_General_Flower_Frame0,  // Can repeat frames
    gTilesetAnims_General_Flower_Frame2
};
```

**File Structure:**
- Frames stored in: `data/tilesets/{primary|secondary}/{tileset_name}/anim/{anim_name}/{frame}.4bpp`
- Example: `data/tilesets/primary/general/anim/water/0.4bpp`
- Each `.4bpp` file contains multiple 8x8 tiles laid out horizontally

### 3.2 Tileset-Specific Animation Function

Each tileset has a callback that schedules individual animations:

```c
static void TilesetAnim_General(u16 timer)
{
    // Flower: Updates every 16 frames (256ms @ 60fps)
    if (timer % 16 == 0)
        QueueAnimTiles_General_Flower(timer / 16);

    // Water: Updates every 16 frames, offset by 1
    if (timer % 16 == 1)
        QueueAnimTiles_General_Water(timer / 16);

    // Sand/Water Edge: Updates every 16 frames, offset by 2
    if (timer % 16 == 2)
        QueueAnimTiles_General_SandWaterEdge(timer / 16);

    // Waterfall: Updates every 16 frames, offset by 3
    if (timer % 16 == 3)
        QueueAnimTiles_General_Waterfall(timer / 16);

    // Land/Water Edge: Updates every 16 frames, offset by 4
    if (timer % 16 == 4)
        QueueAnimTiles_General_LandWaterEdge(timer / 16);
}
```

**Key Insight:** Animations are **staggered** across frames using modulo offsets to reduce VRAM transfer load per frame.

### 3.3 Queue Animation Function

Each animation type has a queue function that adds to the DMA buffer:

```c
static void QueueAnimTiles_General_Water(u16 timer)
{
    // Calculate current frame index (8 frames total)
    u8 frameIndex = timer % ARRAY_COUNT(gTilesetAnims_General_Water);

    // Queue DMA transfer to VRAM
    AppendTilesetAnimToBuffer(
        gTilesetAnims_General_Water[frameIndex],      // Source: ROM frame data
        (u16 *)(BG_VRAM + TILE_OFFSET_4BPP(432)),     // Dest: VRAM tile 432
        30 * TILE_SIZE_4BPP                           // Size: 30 tiles × 32 bytes
    );
}
```

### 3.4 Animation Parameters

| Animation | Base Tile | Tile Count | Update Interval | Frames | Duration |
|-----------|-----------|------------|-----------------|--------|----------|
| Flower    | 508       | 4 tiles    | 16 frames       | 4      | 1.07s    |
| Water     | 432       | 30 tiles   | 16 frames       | 8      | 2.13s    |
| SandWaterEdge | 464   | 10 tiles   | 16 frames       | 8      | 2.13s    |
| Waterfall | 496       | 6 tiles    | 16 frames       | 4      | 1.07s    |
| LandWaterEdge | 476   | 4 tiles    | 16 frames       | 4      | 1.07s    |
| Lava      | varies    | varies     | 16 frames       | 4      | 1.07s    |

**Update Interval Timing:**
- 16 frames @ 60fps = ~266ms per frame
- 8 frames @ 60fps = ~133ms per frame
- 4 frames @ 60fps = ~66ms per frame

---

## 4. VRAM Memory Layout

### 4.1 Tile Address Calculation

```c
#define BG_VRAM 0x06000000                    // VRAM base address
#define TILE_SIZE_4BPP 32                     // 32 bytes per 8x8 tile
#define TILE_OFFSET_4BPP(n) ((n) * 32)        // Byte offset for tile N
#define NUM_TILES_IN_PRIMARY 512              // Primary tileset size

// Example: Water animation starts at tile 432
u16 *waterVRAMAddr = (u16 *)(BG_VRAM + TILE_OFFSET_4BPP(432));
// = 0x06000000 + (432 × 32) = 0x06003600
```

### 4.2 Memory Regions

```
┌──────────────────────────────────────────┐
│ VRAM (0x06000000 - 0x06010000)          │
├──────────────────────────────────────────┤
│ Tiles 0-511   (Primary Tileset)         │  0x06000000
│   - Static tiles (most)                  │
│   - Animated tiles (specific offsets)    │
│     • Tile 432: Water (30 tiles)         │
│     • Tile 464: Sand/Water Edge (10)     │
│     • Tile 476: Land/Water Edge (4)      │
│     • Tile 496: Waterfall (6 tiles)      │
│     • Tile 508: Flower (4 tiles)         │
├──────────────────────────────────────────┤
│ Tiles 512-1023 (Secondary Tileset)       │  0x06004000
│   - Location-specific animations         │
│     • Rustboro: windy water, fountain    │
│     • Lavaridge: steam, lava             │
│     • Underwater: seaweed                │
│     • Pacifidlog: log bridges, currents  │
└──────────────────────────────────────────┘
```

---

## 5. Timing and Frame Rate

### 5.1 Counter System

- **Primary/Secondary Counters:** 0-255 (wraps to 0)
- **Update Rate:** Every frame (60fps)
- **Animation Speed Control:** Modulo checks (timer % N)

### 5.2 Frame Timing Examples

```c
// Fast animation: 4 frames @ 60fps = 66ms/frame
if (timer % 4 == 0)
    QueueAnimTiles_BikeShop_BlinkingLights(timer / 4);

// Medium animation: 8 frames @ 60fps = 133ms/frame
if (timer % 8 == 0)
    QueueAnimTiles_Dewford_Flag(timer / 8);

// Slow animation: 16 frames @ 60fps = 266ms/frame
if (timer % 16 == 0)
    QueueAnimTiles_General_Flower(timer / 16);

// Very slow: 64 frames @ 60fps = 1066ms/frame
if (timer % 64 == 1)
    QueueAnimTiles_EliteFour_GroundLights(timer / 64);
```

### 5.3 Staggered Updates

Different animations update on different frames to balance DMA load:

```c
static void TilesetAnim_General(u16 timer)
{
    if (timer % 16 == 0) QueueAnimTiles_General_Flower(timer / 16);      // Frame 0, 16, 32...
    if (timer % 16 == 1) QueueAnimTiles_General_Water(timer / 16);       // Frame 1, 17, 33...
    if (timer % 16 == 2) QueueAnimTiles_General_SandWaterEdge(...);      // Frame 2, 18, 34...
    if (timer % 16 == 3) QueueAnimTiles_General_Waterfall(...);          // Frame 3, 19, 35...
    if (timer % 16 == 4) QueueAnimTiles_General_LandWaterEdge(...);      // Frame 4, 20, 36...
}
```

**Result:** Only 1 animation updates per frame, spreading VRAM transfers across 16 frames.

---

## 6. Relationship Between Tiles and Metatiles

### 6.1 Metatile Composition

A metatile is defined as 4 tile references:

```c
// Example water metatile definition (pseudocode)
Metatile_Water = {
    TopLeft:  Tile 432, Palette 0,  // Animated water tile
    TopRight: Tile 433, Palette 0,  // Animated water tile
    BotLeft:  Tile 434, Palette 0,  // Animated water tile
    BotRight: Tile 435, Palette 0   // Animated water tile
};
```

### 6.2 How Animation Works

1. **Map Placement:** Metatile IDs are placed in map grid
2. **Metatile Lookup:** Renderer reads metatile definition → gets 4 tile IDs
3. **Tile Rendering:** Each tile ID points to VRAM offset
4. **Animation Update:** Every frame, `UpdateTilesetAnimations()` overwrites VRAM at tile 432-435 with new frame data
5. **Visual Result:** Water appears to animate without changing map data or metatile definitions

**Critical Point:** The metatile always references **tile 432**, but the graphics at VRAM offset 432 change each frame.

---

## 7. Special Animation Types

### 7.1 Palette Animations (Battle Dome)

Some animations use **palette blending** instead of tile replacement:

```c
static void BlendAnimPalette_BattleDome_FloorLights(u16 timer)
{
    // Blends between two palettes over time
    // Does NOT replace tile data
}
```

### 7.2 Multi-Tile Animations (Rustboro Fountain)

Complex animations can update multiple tile ranges:

```c
static void QueueAnimTiles_Rustboro_WindyWater(u16 timer, u8 tileOffset)
{
    // Updates 8 different tile regions (0-7)
    // Each region is 8 tiles
    AppendTilesetAnimToBuffer(
        gTilesetAnims_Rustboro_WindyWater[frameIndex],
        (u16 *)(BG_VRAM + TILE_OFFSET_4BPP(NUM_TILES_IN_PRIMARY + tileOffset * 8)),
        8 * TILE_SIZE_4BPP
    );
}

// Called 8 times per update cycle with different offsets
static void TilesetAnim_Rustboro(u16 timer)
{
    if (timer % 8 == 0) QueueAnimTiles_Rustboro_WindyWater(timer / 8, 0);
    if (timer % 8 == 1) QueueAnimTiles_Rustboro_WindyWater(timer / 8, 1);
    // ... offsets 2-7
}
```

---

## 8. Implementation Requirements for PokeSharp

### 8.1 Core Systems Needed

1. **Animation Frame Storage**
   - Load animation frames from Tiled JSON or separate files
   - Store as texture atlas or individual textures

2. **Frame Counter System**
   - Global counter per tileset (0-255, wrapping)
   - 60fps update rate
   - Modulo-based frame timing

3. **VRAM Simulation (Texture Update)**
   - Map tile IDs to texture regions
   - Update texture data when animation advances
   - Use `Graphics.CopyToTexture()` or shader-based approach

4. **Tileset Animation Callbacks**
   - Per-tileset initialization (set counter max, callback)
   - Per-tileset update function (schedule animations)
   - Per-animation queue function (update specific tiles)

### 8.2 Data Structure Requirements

```csharp
public class TilesetAnimationDefinition
{
    public int BaseTileId;           // First tile ID to animate (e.g., 432 for water)
    public int TileCount;            // Number of tiles (e.g., 30 for water)
    public int UpdateInterval;       // Frames between updates (e.g., 16)
    public int FrameCount;           // Number of animation frames (e.g., 8)
    public Texture2D[] Frames;       // Actual frame graphics
    public int CurrentFrame;         // Current frame index
    public int FrameTimer;           // Counter for frame advancement
}

public class TilesetAnimationSystem
{
    private int _primaryCounter;     // 0-255 counter
    private int _secondaryCounter;
    private Dictionary<int, TilesetAnimationDefinition> _animations;

    public void Update(float deltaTime)
    {
        _primaryCounter = (_primaryCounter + 1) % 256;
        _secondaryCounter = (_secondaryCounter + 1) % 256;

        foreach (var anim in _animations)
        {
            if (_primaryCounter % anim.UpdateInterval == anim.FrameOffset)
            {
                UpdateAnimation(anim);
            }
        }
    }

    private void UpdateAnimation(TilesetAnimationDefinition anim)
    {
        // Advance frame
        anim.CurrentFrame = (_primaryCounter / anim.UpdateInterval) % anim.FrameCount;

        // Copy frame data to tileset texture at BaseTileId offset
        UpdateTileTexture(anim.BaseTileId, anim.TileCount, anim.Frames[anim.CurrentFrame]);
    }
}
```

### 8.3 Expected Behavior

**For Water Animation:**
- Base Tile ID: 432
- Tile Count: 30 tiles (240x8 pixels = 1 row of 30 tiles)
- Frame Count: 8 frames
- Update Interval: 16 frames (266ms)
- Total Duration: 8 frames × 266ms = 2.13 seconds per loop

**Visual Appearance:**
- Water tiles animate smoothly
- Multiple water tiles on screen animate **in sync** (same frame at same time)
- Animation loops seamlessly
- No "popping" or texture seams

---

## 9. Summary of Key Findings

### 9.1 How Pokeemerald Tile Animations Work

1. **Pre-rendered Frames:** Animation frames are complete tile graphics stored in ROM
2. **VRAM Replacement:** DMA copies frame data to VRAM at fixed tile offsets
3. **Metatile References:** Metatiles always point to same tile IDs (e.g., 432), but VRAM contents change
4. **Counter-Based Timing:** Global frame counter (0-255) with modulo checks for timing
5. **Staggered Updates:** Animations spread across frames to reduce per-frame DMA load
6. **No Palette Cycling:** Graphics data changes, not palette (except special cases like Battle Dome)

### 9.2 Critical Implementation Details

- **8x8 Tiles:** Base unit is always 8×8 pixels, 4bpp
- **16x16 Metatiles:** Composed of 4 tiles arranged 2×2
- **512 Tiles per Tileset:** Primary (0-511), Secondary (512-1023)
- **60fps Update Rate:** Counter increments every frame
- **Frame Timing:** Modulo division for frame rate control (% 4, % 8, % 16, % 64)
- **VRAM Offsets:** Calculated as `TILE_ID × 32 bytes`

### 9.3 Animation Parameters Reference

| Property | Flower | Water | Waterfall | Lava | Flag | Blinking |
|----------|--------|-------|-----------|------|------|----------|
| Base Tile | 508 | 432 | 496 | varies | varies | varies |
| Tile Count | 4 | 30 | 6 | varies | 6 | varies |
| Frames | 4 | 8 | 4 | 4 | varies | varies |
| Update (frames) | 16 | 16 | 16 | 16 | 8 | 4 |
| Duration (ms) | 1067 | 2133 | 1067 | 1067 | 1067 | 533 |

---

## 10. References

- **Source Files:**
  - `pokeemerald/src/tileset_anims.c` - Main animation system
  - `pokeemerald/include/tileset_anims.h` - Public API
  - `pokeemerald/include/global.fieldmap.h` - Tileset struct definition
  - `pokeemerald/include/fieldmap.h` - VRAM constants

- **Animation Data:**
  - `data/tilesets/primary/{tileset}/anim/{name}/{frame}.4bpp`
  - `data/tilesets/secondary/{tileset}/anim/{name}/{frame}.4bpp`

- **Frame Timing Reference:**
  - GBA runs at 60fps
  - 1 frame = 16.67ms
  - Update intervals: 4 (66ms), 8 (133ms), 16 (266ms), 64 (1066ms)

---

**Document Version:** 1.0
**Date:** 2025-11-25
**Researched By:** Claude Code Research Agent
**Purpose:** Implementation reference for PokeSharp tile animation system
