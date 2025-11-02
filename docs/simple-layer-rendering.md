# Simple Layer-Based Rendering with Y-Sorting

**Date**: November 2, 2025  
**Approach**: Standard Tiled Layer Order + Y-Sorting

## Overview

Refactored rendering to follow standard Tiled conventions: **render layers in the order they appear in Tiled**, with Y-sorting applied to the object layer where sprites interact with tiles.

This is the simplest and most maintainable approach that respects Tiled's design philosophy.

## Rendering Strategy

### Layer Order (from Tiled file)

```
0. Ground Layer   → Flat rendering (layerDepth = 0.95)
1. Object Layer   → Y-sorted with sprites (layerDepth = 0.4-0.6)
2. Overhead Layer → Flat rendering on top (layerDepth = 0.05)
3. Collision      → Not rendered (objectgroup for collision data)
```

### Layer Depth Ranges

| Layer         | Depth Range | Sorting      | Purpose                           |
|---------------|-------------|--------------|-----------------------------------|
| Ground        | 0.95 (fixed) | None         | Base terrain, always at back      |
| Object+Sprites | 0.4-0.6     | **Y-sorted** | Interactive depth, natural sorting |
| Overhead      | 0.05 (fixed) | None         | Tree canopies, roofs, always on top |

## How It Works

### 1. Ground Layer (Flat)

Renders all ground tiles at a fixed `layerDepth = 0.95`:
```csharp
RenderFlatLayer(tileMap.GroundLayer, ..., layerDepth: 0.95f);
```

**No sorting needed** - just base terrain that's always behind everything.

### 2. Object Layer + Sprites (Y-Sorted Together)

Both object tiles AND sprites calculate their `layerDepth` based on Y position:

```csharp
float yBottom = position.Y + height;
float layerDepth = CalculateYSortDepth(yBottom);
// Returns value in range 0.4 (front) to 0.6 (back)
```

**Y-Sorting Formula:**
```
layerDepth = 0.6 - (normalizedY * 0.2)

Where:
- normalizedY = yPosition / MaxRenderDistance
- Lower Y (top of screen) → layerDepth closer to 0.6 (renders first/behind)
- Higher Y (bottom of screen) → layerDepth closer to 0.4 (renders last/in front)
```

This makes sprites and object tiles **naturally sort together** - if you walk north of a rock, you appear in front; walk south and you're behind it.

### 3. Overhead Layer (Flat)

Renders all overhead tiles at a fixed `layerDepth = 0.05`:
```csharp
RenderFlatLayer(tileMap.OverheadLayer, ..., layerDepth: 0.05f);
```

**Always renders on top** because it's the last layer in Tiled's order with the lowest layer depth.

## Benefits of This Approach

✅ **Follows Tiled Conventions** - Layers render in the order they appear in Tiled  
✅ **Simple & Maintainable** - No complex special cases or overhead logic  
✅ **Standard Practice** - This is how most 2D engines handle Tiled maps  
✅ **Y-Sorting Where It Matters** - Only the object layer needs dynamic sorting  
✅ **Predictable Behavior** - Overhead layer is just "the next layer" after sprites  

## Standard Tiled Techniques Used

This implementation uses these standard techniques from the Tiled ecosystem:

1. **Layer-based rendering**: Layers render in order from the Tiled file
2. **Y-sorting**: Sprites and object tiles sort by Y-coordinate within a specific layer
3. **Depth ranges**: Different layers use different layerDepth ranges to maintain order

## Design Guidelines

### Ground Layer
- Base terrain (grass, water, floors)
- No collision, no interaction
- Always visible at back

### Object Layer
- **Interactive objects** that should Y-sort with sprites
- Tree trunks, rocks, fence posts, signs
- These create natural depth as player moves around them

### Overhead Layer
- **Always on top** elements
- Tree canopies, building roofs, bridge tops, awnings
- Player never appears in front of these

## Example Scenarios

### Walking Past a Tree
```
Tree trunk on Object layer at Y=100
Tree canopy on Overhead layer at Y=96

Player at Y=120 (south of tree):
→ Player layerDepth ≈ 0.48 (in front)
→ Trunk layerDepth ≈ 0.51 (behind)
→ Canopy layerDepth = 0.05 (always on top)
Result: Player appears in front of trunk, behind canopy ✓

Player at Y=80 (north of tree):
→ Player layerDepth ≈ 0.52 (behind)
→ Trunk layerDepth ≈ 0.51 (in front)
→ Canopy layerDepth = 0.05 (always on top)
Result: Player appears behind trunk and canopy ✓
```

## Code Structure

### Single Unified System

`ZOrderRenderSystem` handles everything:

1. **RenderFlatLayer()** - Fixed layerDepth (ground/overhead)
2. **RenderYSortedLayer()** - Dynamic layerDepth (objects)
3. **RenderSprite()** - Same Y-sorting as objects
4. **CalculateYSortDepth()** - Y-to-depth calculation (0.4-0.6 range)

### Single SpriteBatch Pass

All rendering happens in one `SpriteBatch.Begin()` / `.End()` call:
```csharp
_spriteBatch.Begin(sortMode: SpriteSortMode.BackToFront, ...);

// Render ground (0.95)
// Render objects + sprites Y-sorted (0.4-0.6)
// Render overhead (0.05)

_spriteBatch.End();
```

GPU automatically sorts everything by `layerDepth` value.

## Performance

**Compared to the original 3-system approach:**
- 67% fewer system updates per frame (1 vs 3)
- 67% fewer SpriteBatch calls (1 vs 3)
- Same visual result, simpler code

## Conclusion

By following standard Tiled conventions and using simple layer ordering with Y-sorting only where needed, we achieve authentic Pokemon-style depth with clean, maintainable code.

The "overhead" layer isn't special - it's just the last tile layer in Tiled's order, which naturally renders on top of everything that came before it.

