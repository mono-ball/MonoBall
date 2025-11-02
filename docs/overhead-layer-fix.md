# Overhead Layer Rendering Fix

**Date**: November 2, 2025  
**Issue**: Player rendering on top of overhead layer tiles  
**Status**: ✅ Fixed

## The Problem

After the initial Z-order rendering refactor, the overhead layer was being Z-ordered along with sprites and object tiles. This caused the player to render **on top of** overhead tiles (tree canopies, roofs, bridges) when positioned below them, which was incorrect.

### Visual Issue
- Orange overhead tiles at Y=64-96 pixels
- Player at Y=128 pixels (below the tiles)
- Result: Player rendered IN FRONT of overhead tiles ❌

## Root Cause

In the original `ZOrderRenderSystem`, **all layers** were being Z-ordered by Y position:

```csharp
// WRONG: Overhead layer was Z-ordered
tilesRendered += RenderZOrderedLayer(tileMap.OverheadLayer, ...);
```

This meant overhead tiles at lower Y positions (top of screen) would be assigned higher `layerDepth` values and render behind sprites at higher Y positions.

## The Fix

The overhead layer should **always** render on top, regardless of sprite positions. 

### Code Changes

Created a new `RenderOverheadLayer()` method that uses fixed `layerDepth = 0.0`:

```csharp
private int RenderOverheadLayer(int[,] layer, ...)
{
    // ...
    _spriteBatch.Draw(
        texture: tilesetTexture,
        position: position,
        sourceRectangle: sourceRect,
        color: Color.White,
        rotation: 0f,
        origin: Vector2.Zero,
        scale: 1f,
        effects: SpriteEffects.None,
        layerDepth: 0.0f); // Always on top
    // ...
}
```

### Rendering Order (Corrected)

```
SpriteBatch.Begin(sortMode: SpriteSortMode.BackToFront)

1. Ground Layer    → layerDepth = 1.0        (furthest back)
2. Object Tiles    → layerDepth = 0.01-0.99  (Z-ordered by Y)
3. Sprites         → layerDepth = 0.01-0.99  (Z-ordered by Y)
4. Overhead Layer  → layerDepth = 0.0        (always on top)

SpriteBatch.End()
→ GPU sorts: 1.0 → 0.99 → ... → 0.01 → 0.0
```

## Expected Behavior (After Fix)

✅ **Overhead tiles** (tree canopies, roofs, bridges) **always** render on top of player  
✅ **Object tiles** (tree trunks, rocks) Z-order with player based on Y position  
✅ **Player and NPCs** naturally walk behind object tiles when positioned above them

## Layer Design Guidelines

Based on this fix, here's how to use the three tile layers:

### Ground Layer
- **Purpose**: Base terrain (grass, water, floors)
- **Rendering**: Always at back (`layerDepth = 1.0`)
- **Examples**: Grass tiles, water tiles, floor tiles

### Object Layer
- **Purpose**: Interactive objects that should Z-order with sprites
- **Rendering**: Z-ordered by Y position (`layerDepth = 0.01-0.99`)
- **Examples**: Tree trunks, rocks, fence posts, signs

### Overhead Layer
- **Purpose**: Elements that should always appear above everything
- **Rendering**: Always on top (`layerDepth = 0.0`)
- **Examples**: Tree canopies, building roofs, bridge tops, awnings

## Testing

To verify the fix works:

1. Run the game
2. Position player below overhead tiles (orange tiles at rows 4-5)
3. Confirm overhead tiles render **on top of** player ✓
4. Move player to interact with object layer tiles
5. Confirm player Z-orders naturally with object tiles ✓

## Related Files

- `PokeSharp/PokeSharp.Rendering/Systems/ZOrderRenderSystem.cs` - Added `RenderOverheadLayer()` method
- `PokeSharp/docs/z-order-rendering-refactor.md` - Updated documentation
- `PokeSharp/PokeSharp.Game/Assets/Maps/test-map.json` - Test map with overhead tiles

## Conclusion

The overhead layer now correctly renders on top of all sprites, matching the expected behavior for Pokemon-style games where tree canopies, roofs, and bridges create the illusion of depth without requiring the player to ever appear in front of them.

