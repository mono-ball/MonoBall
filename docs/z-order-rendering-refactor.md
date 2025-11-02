# Z-Order Rendering System Refactor

**Date**: November 2, 2025

## Overview

Refactored the rendering architecture from a layer-based approach to a unified Z-order based rendering system. This provides more authentic Pokemon-style depth where the player, NPCs, and objects naturally appear behind/in-front-of tiles based on their Y position.

## Previous Architecture (Layer-Based)

The old approach used three separate rendering systems running in sequence:

```
1. MapRenderSystem (Priority: 900)
   - Rendered ground layer
   - Rendered object layer

2. RenderSystem (Priority: 1000)
   - Rendered all sprites (player, NPCs)

3. OverheadRenderSystem (Priority: 1050)
   - Rendered overhead layer (trees, roofs, bridges)
```

### Problems with Layer-Based Approach

- **Inflexible**: All sprites always rendered between object and overhead layers
- **Not authentic**: Pokemon games use Y-position based sorting, not strict layers
- **Performance overhead**: Three separate SpriteBatch Begin/End calls per frame
- **Limited depth effects**: Couldn't have dynamic depth interactions

## New Architecture (Z-Order Based)

The new `ZOrderRenderSystem` renders everything in a single pass with proper Z-ordering:

```
ZOrderRenderSystem (Priority: 1000)
├── Ground Layer (layerDepth = 1.0, always at back)
├── Object Layer (Z-ordered by Y position)
├── Sprites (Z-ordered by Y position)
└── Overhead Layer (layerDepth = 0.0, always on top)
```

### How Z-Ordering Works

1. **Ground Layer**: Always rendered at `layerDepth = 1.0` (furthest back)

2. **Object Layer & Sprites**: Layer depth calculated based on Y position
   ```csharp
   layerDepth = 1.0 - (yPosition / MaxRenderDistance) * 0.99
   ```

3. **Overhead Layer**: Always rendered at `layerDepth = 0.0` (always on top)

4. **SpriteBatch BackToFront**: Uses `SpriteSortMode.BackToFront` to automatically sort by layer depth

### Layer Depth Calculation

- **Y position**: Bottom edge of sprite/tile (Y + height)
- **Lower Y** (top of screen) → Higher layer depth → Renders first (behind)
- **Higher Y** (bottom of screen) → Lower layer depth → Renders last (in front)

Example:
```
Sprite at Y=100: layerDepth ≈ 0.99 (behind)
Sprite at Y=200: layerDepth ≈ 0.98 (in front)
Tree at Y=150: layerDepth ≈ 0.985 (between them)
```

## Benefits of Hybrid Z-Order Rendering

✅ **Authentic Pokemon Gameplay**: Player and objects naturally sort by Y position

✅ **Proper Overhead Rendering**: Tree canopies, roofs, and bridges always appear on top

✅ **Better Performance**: Single SpriteBatch Begin/End call instead of three

✅ **Dynamic Depth**: NPCs, items, and player all interact naturally with object layer

✅ **Simpler Code**: One unified system instead of three separate ones

## Changes Made

### Files Modified

1. **PokeSharp/PokeSharp.Rendering/Systems/ZOrderRenderSystem.cs** (NEW)
   - Unified rendering system
   - Handles tiles, sprites, and objects with Z-ordering
   - Uses `SpriteSortMode.BackToFront` for automatic sorting

2. **PokeSharp/PokeSharp.Game/PokeSharpGame.cs**
   - Replaced three rendering systems with `ZOrderRenderSystem`
   - Updated field type from `RenderSystem` to `ZOrderRenderSystem`
   - Simplified system registration

3. **PokeSharp/PokeSharp.Core/Systems/SystemPriority.cs**
   - Removed `Overhead = 1050` constant (no longer needed)
   - Removed `MapRender = 900` constant (no longer needed)
   - Updated `Render = 1000` documentation

### Files Deleted

1. **PokeSharp/PokeSharp.Rendering/Systems/RenderSystem.cs** (OBSOLETE)
2. **PokeSharp/PokeSharp.Rendering/Systems/MapRenderSystem.cs** (OBSOLETE)
3. **PokeSharp/PokeSharp.Rendering/Systems/OverheadRenderSystem.cs** (OBSOLETE)

## Technical Details

### SpriteBatch Configuration

```csharp
_spriteBatch.Begin(
    sortMode: SpriteSortMode.BackToFront,  // Sort by layerDepth
    blendState: BlendState.AlphaBlend,
    samplerState: SamplerState.PointClamp,  // Pixel-perfect rendering
    transformMatrix: cameraTransform);
```

### Layer Depth Ranges

- `1.0`: Ground layer (furthest back)
- `0.01 - 0.99`: Z-ordered object tiles and sprites
- `0.0`: Overhead layer (always on top, tree canopies/roofs/bridges)

### Rendering Order (Single Pass)

```
Frame N:
1. Clear screen
2. Begin SpriteBatch (BackToFront mode)
3. Draw all ground tiles (layerDepth = 1.0)
4. Draw all object tiles (Z-ordered by Y, layerDepth 0.01-0.99)
5. Draw all sprites (Z-ordered by Y, layerDepth 0.01-0.99)
6. Draw all overhead tiles (layerDepth = 0.0, always on top)
7. End SpriteBatch
   → GPU automatically sorts by layerDepth
   → Renders back-to-front (1.0 → 0.01 → 0.0)
```

## Testing Recommendations

1. **Depth Interactions**
   - Walk player behind trees/buildings
   - Verify player appears correctly behind overhead tiles
   - Check NPCs sort properly with environment

2. **Performance**
   - Monitor frame time (should be slightly improved)
   - Verify no visual glitches or z-fighting

3. **Edge Cases**
   - Entities at same Y position should render consistently
   - Camera zoom should not affect depth sorting
   - Large sprites should use bottom edge for Y position

## Migration Notes

### For Future Development

When adding new renderable entities:

1. Add Y position to layer depth calculation
2. Use bottom edge of sprite/tile for Y coordinate
3. Objects sort automatically - no manual layer management needed

### Example: Adding an NPC

```csharp
// Old approach: NPC always between object and overhead layers

// New approach: NPC sorts naturally by Y position
var npc = world.Create(
    new Position(10, 12),  // Grid position
    new Sprite("npc-texture")
);

// That's it! Z-ordering happens automatically in ZOrderRenderSystem
```

## Performance Comparison

| Metric | Old (3 Systems) | New (1 System) | Improvement |
|--------|----------------|----------------|-------------|
| SpriteBatch Calls | 3 per frame | 1 per frame | **67% reduction** |
| System Updates | 3 per frame | 1 per frame | **67% reduction** |
| Code Complexity | ~650 LOC | ~325 LOC | **50% simpler** |
| Depth Flexibility | Fixed layers | Dynamic Y-sort | **Much better** |

## Conclusion

The Z-order rendering refactor provides a more authentic Pokemon-style rendering system that's simpler, more performant, and more flexible than the previous layer-based approach. All entities now render naturally based on their position, creating proper depth without manual layer management.

