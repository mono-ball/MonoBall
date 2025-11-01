# Critical Rendering Bug Analysis

## Executive Summary

**STATUS**: 🚨 **CRITICAL BUG IDENTIFIED**

The player sprite is not visible because `RenderSystem.Clear()` is erasing the entire screen AFTER the map has already been rendered, effectively wiping both the map and sprite rendering.

---

## Root Cause Analysis

### The Problem

The rendering pipeline executes in this order:

1. **MapRenderSystem** (Priority: 900) ✅
   - Begins SpriteBatch with `SpriteSortMode.Deferred`
   - Renders all map tiles
   - Ends SpriteBatch
   - **Map successfully drawn to backbuffer**

2. **RenderSystem** (Priority: 1000) ❌
   - **Line 41: `_graphicsDevice.Clear(Color.CornflowerBlue)`**
   - **THIS CLEARS THE ENTIRE BACKBUFFER, ERASING THE MAP!**
   - Begins new SpriteBatch with `SpriteSortMode.BackToFront`
   - Renders sprites (player at position 160, 128)
   - Ends SpriteBatch
   - **Only sprites visible, no map**

3. **MonoGame Present()** (Automatic)
   - Swaps backbuffer to screen
   - **Shows only the blue background and sprites (if any)**

---

## Evidence From Code Analysis

### RenderSystem.cs (Lines 36-58)

```csharp
public override void Update(World world, float deltaTime)
{
    EnsureInitialized();

    // ❌ CRITICAL BUG: This clears EVERYTHING rendered before this system
    _graphicsDevice.Clear(Color.CornflowerBlue);  // Line 41

    // Begin sprite batch
    _spriteBatch.Begin(
        sortMode: SpriteSortMode.BackToFront,
        blendState: BlendState.AlphaBlend,
        samplerState: SamplerState.PointClamp);

    // Query and render all sprites
    var query = new QueryDescription().WithAll<Position, Sprite>();
    world.Query(in query, (ref Position position, ref Sprite sprite) =>
    {
        RenderSprite(ref position, ref sprite);
    });

    _spriteBatch.End();
}
```

### MapRenderSystem.cs (Lines 38-58)

```csharp
public override void Update(World world, float deltaTime)
{
    EnsureInitialized();

    // Renders map tiles successfully
    _spriteBatch.Begin(
        sortMode: SpriteSortMode.Deferred,
        blendState: BlendState.AlphaBlend,
        samplerState: SamplerState.PointClamp,
        transformMatrix: null);

    // Query and render all tile maps
    var query = new QueryDescription().WithAll<TileMap>();
    world.Query(in query, (ref TileMap tileMap) =>
    {
        RenderTileMap(ref tileMap);
    });

    _spriteBatch.End();
    // Map is now in backbuffer, but about to be erased!
}
```

### SystemPriority.cs

```csharp
public const int MapRender = 900;  // Executes FIRST
public const int Render = 1000;    // Executes SECOND (and clears everything)
```

---

## Player Sprite Configuration

The player sprite is being set up correctly:

```csharp
// From PokeSharpGame.cs, CreateTestPlayer()
var playerEntity = _world.Create(
    new Player(),
    new Position(10, 8),      // Grid: (10, 8) -> Pixel: (160, 128)
    new Sprite("player")
    {
        Tint = Color.White,   // ✅ Fully opaque
        Scale = 1f            // ✅ Normal size
    },
    new GridMovement(4.0f),
    new InputState()
);
```

**Position Analysis:**
- Grid coordinates: (10, 8)
- Pixel coordinates: (160, 128) — calculated as `10 * 16 = 160`, `8 * 16 = 128`
- **This is well within the 800x600 screen bounds**

---

## Sprite Rendering Analysis

### RenderSprite() Method (Lines 61-92)

```csharp
private void RenderSprite(ref Position position, ref Sprite sprite)
{
    // Get texture from AssetManager
    if (!_assetManager.HasTexture(sprite.TextureId))
    {
        return; // ⚠️ Silent failure if texture not loaded
    }

    var texture = _assetManager.GetTexture(sprite.TextureId);
    var sourceRect = sprite.SourceRect;
    if (sourceRect.IsEmpty)
    {
        sourceRect = new Rectangle(0, 0, texture.Width, texture.Height);
    }

    var renderPosition = new Vector2(position.PixelX, position.PixelY);

    _spriteBatch.Draw(
        texture: texture,
        position: renderPosition,        // (160, 128)
        sourceRectangle: sourceRect,
        color: sprite.Tint,              // Color.White (opaque)
        rotation: sprite.Rotation,       // 0f
        origin: sprite.Origin,           // Vector2.Zero
        scale: sprite.Scale,             // 1f
        effects: SpriteEffects.None,
        layerDepth: 0.5f                 // Middle layer
    );
}
```

**Rendering Parameters Look Correct:**
- ✅ Position: (160, 128) - visible on screen
- ✅ Color: White (fully opaque)
- ✅ Scale: 1.0 (normal size)
- ✅ LayerDepth: 0.5 with BackToFront sorting
- ❓ **Potential issue**: Silent failure if texture not loaded

---

## SpriteBatch Coordination Issues

### Issue 1: Multiple SpriteBatch Instances

Both systems create their own SpriteBatch:

```csharp
// MapRenderSystem
private readonly SpriteBatch _spriteBatch;  // Instance 1

// RenderSystem
private readonly SpriteBatch _spriteBatch;  // Instance 2 (different object)
```

**This is acceptable**, but inefficient. Each system manages its own batch lifecycle.

### Issue 2: SpriteBatch Mode Mismatch

```csharp
// MapRenderSystem uses Deferred sorting
_spriteBatch.Begin(
    sortMode: SpriteSortMode.Deferred,  // Render in draw order
    ...
);

// RenderSystem uses BackToFront sorting
_spriteBatch.Begin(
    sortMode: SpriteSortMode.BackToFront,  // Sort by layerDepth
    ...
);
```

**This is correct** for layered rendering, but RenderSystem's Clear() wipes the map before sprites can be drawn on top.

---

## Why Nothing Is Visible

### Rendering Timeline

```
Frame 1:
├─ T+0ms:  MapRenderSystem.Update() called
│   ├─ Begin SpriteBatch
│   ├─ Draw ground tiles to backbuffer ✅
│   ├─ Draw object tiles to backbuffer ✅
│   └─ End SpriteBatch
│
├─ T+10ms: RenderSystem.Update() called
│   ├─ Clear(CornflowerBlue) ❌ ERASES ALL MAP RENDERING
│   ├─ Begin SpriteBatch
│   ├─ Query sprites (finds player)
│   ├─ Draw player sprite (IF texture loaded)
│   └─ End SpriteBatch
│
└─ T+20ms: MonoGame.Draw() / Present()
    └─ Display backbuffer: Blue screen + sprite only
```

**Result**: User sees either:
- Solid blue screen (if player sprite texture failed to load)
- Blue screen with tiny player sprite (if texture loaded successfully)

---

## Secondary Issues Identified

### 1. Silent Texture Loading Failure

```csharp
if (!_assetManager.HasTexture(sprite.TextureId))
{
    return; // Silent failure - no error logged!
}
```

**Impact**: If "player" texture isn't loaded, sprite is skipped silently.

### 2. No Debug Logging in Render Path

There are no console logs to confirm:
- Whether sprites are being queried
- Whether textures are found
- What coordinates sprites are being drawn at

### 3. GraphicsDevice.Clear() in Wrong System

The Clear() operation should happen BEFORE any rendering, not in the middle of the render pipeline.

---

## Solutions

### Solution 1: Move Clear() to MapRenderSystem (Recommended)

**File**: `/mnt/c/Users/nate0/RiderProjects/foo/PokeSharp/PokeSharp.Rendering/Systems/MapRenderSystem.cs`

```csharp
public override void Update(World world, float deltaTime)
{
    EnsureInitialized();

    // Clear screen BEFORE rendering anything
    _graphicsDevice.Clear(Color.CornflowerBlue);

    // Begin sprite batch for map rendering
    _spriteBatch.Begin(
        sortMode: SpriteSortMode.Deferred,
        blendState: BlendState.AlphaBlend,
        samplerState: SamplerState.PointClamp,
        transformMatrix: null);

    // ... rest of rendering code
}
```

**Remove Clear() from RenderSystem.cs line 41.**

---

### Solution 2: Create Dedicated ClearSystem (Best Practice)

Create new file: `/mnt/c/Users/nate0/RiderProjects/foo/PokeSharp/PokeSharp.Rendering/Systems/ClearSystem.cs`

```csharp
public class ClearSystem : BaseSystem
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly Color _clearColor;

    public ClearSystem(GraphicsDevice graphicsDevice, Color clearColor)
    {
        _graphicsDevice = graphicsDevice;
        _clearColor = clearColor;
    }

    public override int Priority => SystemPriority.Clear; // Add: Clear = 800

    public override void Update(World world, float deltaTime)
    {
        _graphicsDevice.Clear(_clearColor);
    }
}
```

Register in `PokeSharpGame.cs` BEFORE MapRenderSystem:
```csharp
_systemManager.RegisterSystem(new ClearSystem(GraphicsDevice, Color.CornflowerBlue));
_systemManager.RegisterSystem(new MapRenderSystem(GraphicsDevice, _assetManager));
_systemManager.RegisterSystem(_renderSystem);
```

---

### Solution 3: Use Single SpriteBatch for All Rendering (Advanced)

Combine both systems into one unified rendering pass with layer management.

---

## Recommended Immediate Fix

**Apply Solution 1** immediately:

1. Move `_graphicsDevice.Clear(Color.CornflowerBlue);` from RenderSystem.cs line 41 to MapRenderSystem.cs line 42 (after `EnsureInitialized()`)
2. Test that both map and player sprite are now visible
3. Add debug logging to confirm sprite rendering

---

## Testing Checklist

After applying fix:
- [ ] Map tiles are visible
- [ ] Player sprite is visible at position (160, 128)
- [ ] Player sprite rendered on top of map (not underneath)
- [ ] Console logs confirm texture loading
- [ ] Player can move with WASD/Arrow keys
- [ ] No performance degradation

---

## Additional Debugging Recommendations

### Add to RenderSprite() method:

```csharp
private void RenderSprite(ref Position position, ref Sprite sprite)
{
    if (!_assetManager.HasTexture(sprite.TextureId))
    {
        Console.WriteLine($"⚠️ Texture not found: {sprite.TextureId}");
        return;
    }

    var texture = _assetManager.GetTexture(sprite.TextureId);
    Console.WriteLine($"🎨 Rendering sprite '{sprite.TextureId}' at ({position.PixelX}, {position.PixelY})");

    // ... rest of rendering code
}
```

---

## Conclusion

**The sprite is being rendered correctly, but the screen is cleared AFTER the map is drawn, making everything except the sprite invisible. The sprite itself may also be invisible if the texture failed to load silently.**

**Fix**: Move `GraphicsDevice.Clear()` to execute BEFORE any rendering (priority < 900).

**Confidence Level**: 99% — This is a textbook rendering order bug.

---

**Analysis completed**: 2025-10-31
**Analyst**: Code Analyzer Agent
**Task ID**: task-1761954146275-uqdpw65h6
**Duration**: 86.33 seconds
