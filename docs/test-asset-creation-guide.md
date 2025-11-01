# Test Asset Creation Guide

This guide explains how to create the minimal test assets needed to run Phase 1 of PokeSharp.

## Required Assets

Based on `Assets/manifest.json`, you need to create:

1. **Tileset**: `Assets/Tilesets/test-tileset.png` (64x64 pixels, 4x4 grid of 16x16 tiles)
2. **Player Sprite**: `Assets/Sprites/player.png` (16x16 pixels)

## Option 1: Quick Creation with ImageMagick (Recommended)

If you have ImageMagick installed, run these commands:

```bash
cd PokeSharp.Game/Assets

# Create test tileset (64x64, 4x4 grid)
magick -size 64x64 xc:white \
  -fill "#228b22" -draw "rectangle 0,0 15,15" \
  -fill "#8b4513" -draw "rectangle 16,0 31,15" \
  -fill "#4682b4" -draw "rectangle 32,0 47,15" \
  -fill "#daa520" -draw "rectangle 48,0 63,15" \
  -fill "#696969" -draw "rectangle 0,16 15,31" \
  -fill "#ff6347" -draw "rectangle 16,16 31,31" \
  -fill "#9370db" -draw "rectangle 32,16 47,31" \
  -fill "#20b2aa" -draw "rectangle 48,16 63,31" \
  -fill "#ffa500" -draw "rectangle 0,32 15,47" \
  -fill "#dc143c" -draw "rectangle 16,32 31,47" \
  -fill "#00ced1" -draw "rectangle 32,32 47,47" \
  -fill "#ff1493" -draw "rectangle 48,32 63,47" \
  -fill "#32cd32" -draw "rectangle 0,48 15,63" \
  -fill "#ff4500" -draw "rectangle 16,48 31,63" \
  -fill "#1e90ff" -draw "rectangle 32,48 47,63" \
  -fill "#ffd700" -draw "rectangle 48,48 63,63" \
  Tilesets/test-tileset.png

# Create player sprite (16x16, simple character)
magick -size 16x16 xc:none \
  -fill "#ff6347" -draw "circle 8,5 8,2" \
  -fill "#4682b4" -draw "rectangle 5,6 11,12" \
  -fill "#ffd700" -draw "rectangle 4,8 6,14" \
  -fill "#ffd700" -draw "rectangle 10,8 12,14" \
  Sprites/player.png

echo "✅ Test assets created successfully!"
```

## Option 2: Manual Creation with Any Image Editor

### Test Tileset (test-tileset.png)

**Specifications:**
- Dimensions: **64x64 pixels**
- Grid: **4x4 tiles** (16x16 pixels each)
- Total: **16 unique tiles**
- Format: PNG with transparency

**Tile Layout (from test-map.json):**
```
Tile IDs used in map:
- ID 1: Green (grass/ground)
- ID 2: Brown (dirt/path)
- ID 3: Blue (water)
- ID 4: Gold (decorative)
- ID 5: Gray (stone/wall)
- ID 6: Red (door/entrance)
```

**Steps:**
1. Open your image editor (GIMP, Photoshop, Aseprite, Pixelotar, etc.)
2. Create new image: 64x64 pixels
3. Divide into 4x4 grid (16x16 per tile)
4. Fill each tile with distinct colors:
   - Row 1: Green, Brown, Blue, Gold
   - Row 2: Gray, Red, Purple, Teal
   - Row 3: Orange, Dark Red, Cyan, Pink
   - Row 4: Lime, Orange Red, Dodger Blue, Yellow
5. Save as `test-tileset.png` in `Assets/Tilesets/`

**Example Grid:**
```
┌────┬────┬────┬────┐
│ 1  │ 2  │ 3  │ 4  │  Row 1 (Tiles 1-4)
│GRN │BRN │BLU │GLD │
├────┼────┼────┼────┤
│ 5  │ 6  │ 7  │ 8  │  Row 2 (Tiles 5-8)
│GRY │RED │PRP │TEL │
├────┼────┼────┼────┤
│ 9  │ 10 │ 11 │ 12 │  Row 3 (Tiles 9-12)
│ORG │DRD │CYN │PNK │
├────┼────┼────┼────┤
│ 13 │ 14 │ 15 │ 16 │  Row 4 (Tiles 13-16)
│LIM │ORD │DBL │YLW │
└────┴────┴────┴────┘
```

### Player Sprite (player.png)

**Specifications:**
- Dimensions: **16x16 pixels**
- Simple character design
- Format: PNG with transparency

**Steps:**
1. Create new image: 16x16 pixels
2. Draw a simple character:
   - Head: Circle at top (red/orange)
   - Body: Rectangle in middle (blue)
   - Legs: Two rectangles at bottom (yellow/brown)
3. Save as `player.png` in `Assets/Sprites/`

**Simple Design Example:**
```
    oooo        <- Head (red circle)
   oooooo
   oooooo
    oooo
  ████████      <- Body (blue)
  ████████
  ████████
  ████████
   ██  ██       <- Legs (yellow)
   ██  ██
   ██  ██
```

## Option 3: Copy from Existing Resources

If you have existing Pokemon-style assets:

1. **Tileset**: Any 16x16 tileset will work (overworld tiles, grass, water, etc.)
   - Must be at least 64x64 pixels (4x4 grid minimum)
   - Can be larger (8x8 grid = 128x128, etc.)

2. **Player Sprite**: Any 16x16 character sprite
   - Pokemon trainer sprite
   - Generic RPG character
   - Simple colored square works for testing

## Verification

After creating assets, verify they exist:

```bash
cd PokeSharp.Game/Assets
ls -lh Tilesets/test-tileset.png
ls -lh Sprites/player.png
file Tilesets/test-tileset.png  # Should show: PNG image data, 64 x 64, ...
file Sprites/player.png          # Should show: PNG image data, 16 x 16, ...
```

## What Happens Without Assets

The game will still run but you'll see console warnings:
- `⚠️ Failed to load manifest: ...` - Manifest file not found
- `⚠️ Failed to load test map: ...` - Tileset texture not available
- Player sprite won't render (only if "player" texture is missing)

The AssetManager has graceful fallbacks, so the game won't crash.

## Testing the Complete Phase 1

Once assets are created:

```bash
cd PokeSharp.Game
dotnet run
```

**Expected Output:**
```
✅ Asset manifest loaded successfully
✅ Loaded test map: test-map (20x15 tiles)
   Map entity: Entity(id: 0)
✅ Created player entity: Entity(id: 1)
🎮 Use WASD or Arrow Keys to move!
```

**Expected Behavior:**
- Window opens showing 800x600 game view
- Test map renders with colored tiles in a border pattern
- Player sprite appears at grid position (10, 8)
- WASD/Arrow keys move player in grid-based movement (Pokemon-style)

## Troubleshooting

**Issue**: `FileNotFoundException` for assets
- **Solution**: Check file paths are correct relative to `Assets/` directory
- **Solution**: Verify file names match exactly (case-sensitive on Linux)

**Issue**: Black screen with no rendering
- **Solution**: Check console for error messages
- **Solution**: Verify PNG files are valid (not corrupted)

**Issue**: Player doesn't move
- **Solution**: Check console - likely manifest/sprite loading issue
- **Solution**: Grid-based movement may be in progress (wait for animation)

## Next Steps After Assets

Once you have working assets:

1. ✅ Verify test-map.json renders correctly
2. ✅ Test player movement with keyboard input
3. ✅ Verify collision detection (if enabled in map)
4. Document Phase 1 completion
5. Move to Phase 2: Battle System

---

**Phase 1 Status**: ~95% Complete
- ✅ Runtime asset loading (no Content Pipeline)
- ✅ Tiled 1.11.2 JSON map support
- ✅ 3-layer map rendering (Ground, Objects, Overhead)
- ✅ ECS integration with Arch
- ✅ Grid-based player movement
- ⏭️ Test assets creation (this guide)
- ⏭️ End-to-end testing
