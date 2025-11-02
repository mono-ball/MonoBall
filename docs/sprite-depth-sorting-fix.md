# Sprite Depth Sorting Fix - Target Position

**Date**: November 2, 2025  
**Issue**: Player rendering behind fences when jumping  
**Root Cause**: Using interpolated visual position for depth sorting  
**Solution**: Use target grid position for moving entities

## The Problem

When the player jumped over a ledge/fence, they would render **behind** the fence during the jump animation, which looked incorrect.

### Why It Happened

The original code calculated sprite depth based on `position.PixelY`:

```csharp
float groundY = position.PixelY + sourceRect.Height;
float layerDepth = CalculateYSortDepth(groundY);
```

But during movement, `Position.PixelY` is **interpolated** between start and end positions:

```csharp
// From MovementSystem.cs
position.PixelY = MathHelper.Lerp(
    movement.StartPosition.Y,
    movement.TargetPosition.Y,
    movement.MovementProgress);
```

**The Result:**
- Player jumping from Y=128 to Y=160
- During jump, PixelY might be at 144 (interpolated mid-point)
- Fence at Y=144 would have same depth, causing flickering/incorrect sorting
- Grid position (position.Y) stays at starting tile until movement completes

## Best Practice: Use Grid Position for Depth

According to standard 2D game engine practices, **depth sorting should be based on logical grid position, not visual pixel position**.

### The Fix

Now the system uses **two different queries**:

1. **Moving Entities** (with `GridMovement` component)
   - Uses **target** grid position for sorting
   - This makes the entity sort as if it's already at the destination
   - Prevents flickering and incorrect sorting during movement

2. **Static Entities** (without `GridMovement` component)
   - Uses current grid position
   - Simple, no movement considerations needed

### Code Changes

```csharp
// Query moving entities
var movingSpriteQuery = new QueryDescription().WithAll<Position, Sprite, GridMovement>();
world.Query(in movingSpriteQuery, (ref Position position, ref Sprite sprite, ref GridMovement movement) =>
{
    RenderMovingSprite(ref position, ref sprite, ref movement);
});

// Query static entities
var staticSpriteQuery = new QueryDescription().WithAll<Position, Sprite>().WithNone<GridMovement>();
world.Query(in staticSpriteQuery, (ref Position position, ref Sprite sprite) =>
{
    RenderStaticSprite(ref position, ref sprite);
});
```

### Moving Sprite Depth Calculation

```csharp
float groundY;
if (movement.IsMoving)
{
    // Use TARGET grid position for sorting
    int targetGridY = (int)(movement.TargetPosition.Y / TileSize);
    groundY = (targetGridY + 1) * TileSize; // +1 for bottom of tile
}
else
{
    // Use current grid position when not moving
    groundY = (position.Y + 1) * TileSize;
}

float layerDepth = CalculateYSortDepth(groundY);
```

## Why This Works

### Jumping Over a Fence

**Scenario**: Player at Y=8 jumps to Y=10, fence at Y=9

**Old Behavior (WRONG)**:
- During jump: Player position.Y = 8 (hasn't updated yet)
- Player depth = based on Y=8 → renders **behind** fence at Y=9
- Result: Player appears behind fence while jumping over it ❌

**New Behavior (CORRECT)**:
- During jump: Player target = Y=10
- Player depth = based on Y=10 → renders **in front** of fence at Y=9  
- Result: Player appears in front of fence while jumping over it ✓

## Best Practices Applied

✅ **Grid-based sorting** - Use logical grid position, not visual pixels  
✅ **Target position for moving entities** - Sort based on destination  
✅ **Separation of concerns** - Visual position vs. logical position  
✅ **No mid-movement flickering** - Depth stays constant during movement  
✅ **Authentic gameplay** - Matches Pokemon and other 2D RPG behavior  

## Key Principles

### Visual Position vs. Logical Position

| Position Type | Purpose | Used For |
|--------------|---------|----------|
| **PixelX/PixelY** | Visual rendering | Sprite draw position (interpolated) |
| **Grid X/Y** | Logical gameplay | Collision, sorting, game logic |
| **Target Position** | Movement goal | Sorting for moving entities |

### Depth Sorting Rules

1. **Static entities**: Use current grid position
2. **Moving entities**: Use target grid position  
3. **Grid position only**: Never use interpolated pixel position for depth
4. **Bottom of tile**: Depth based on bottom edge of grid tile entity occupies

## Testing

To verify the fix:

1. Walk player to a ledge (Y=8)
2. Jump down over fence (ledge at Y=9, landing at Y=10)
3. During jump animation, player should render **in front** of fence ✓
4. Player should not flicker or change sorting mid-jump ✓

## Related Files

- `PokeSharp/PokeSharp.Rendering/Systems/ZOrderRenderSystem.cs`
  - Added `RenderMovingSprite()` - handles moving entities
  - Added `RenderStaticSprite()` - handles static entities
  - Separated queries for moving vs static sprites

## Conclusion

By using the target grid position for moving entities, we ensure that sprites sort correctly during movement and jumping animations. The visual interpolation is purely cosmetic - the depth sorting is based on logical game state, which is the standard approach in 2D game engines.

This matches how authentic Pokemon games handle depth sorting, where entities sort based on which tile they logically occupy, not where they visually appear during movement animations.

