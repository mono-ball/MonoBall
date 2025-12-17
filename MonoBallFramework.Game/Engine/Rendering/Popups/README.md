# Map Popup Rendering System

This folder contains the rendering system for map location popups, based on pokeemerald's map name display system.

## Architecture

### Definitions

- **`PopupBackgroundDefinition.cs`**: Defines background bitmap styles
- **`PopupOutlineDefinition.cs`**: Defines outline/border styles (supports both tile sheets and legacy 9-slice)
- **`PopupTileDefinition.cs`**: Defines individual tiles in a tile sheet
- **`PopupTileUsage.cs`**: Maps tile indices to their frame usage

### Rendering

- **`MapPopupScene.cs`**: Scene that displays the popup with animation
- **`PopupRegistry.cs`**: Manages available popup styles

## Rendering Modes

The system supports two rendering modes for outlines:

### 1. Tile Sheet Mode (GBA-Accurate)

**Format**: `Type: "TileSheet"`

Used for pokeemerald-extracted popups. Assembles frames from individual 8×8 pixel tiles.

**Features**:

- Matches GBA's tile-based hardware
- No stretching or scaling
- Tiles are repeated along edges to fill the frame
- Pixel-perfect rendering

**How It Works**:

1. Loads tile sheet texture (80×24 pixels = 30 tiles)
2. Uses `TileUsage` to select appropriate tiles for each frame section
3. Draws tiles at 8-pixel intervals

**Example**:

```
Top edge: [tile 0][tile 1][tile 2]...[tile 11] (repeated as needed)
Corners: [tile 12] (TL), [tile 13] (TR), [tile 16] (BL), [tile 17] (BR)
Sides: [tile 14] (left), [tile 15] (right) (repeated as needed)
Bottom: [tile 18][tile 19]...[tile 29] (repeated as needed)
```

### 2. Legacy 9-Slice Mode

**Format**: `Type: "9Slice"` or missing `Type` field

Used for custom popups or legacy content. Uses traditional 9-slice/9-patch rendering.

**Features**:

- Corners are never stretched
- Edges are stretched along one axis
- Center is transparent
- More flexible for varying sizes

## Tile Sheet Structure

Pokeemerald outlines use a 10×3 tile grid (30 tiles total):

```
┌───┬───┬───┬───┬───┬───┬───┬───┬───┬───┐
│ 0 │ 1 │ 2 │ 3 │ 4 │ 5 │ 6 │ 7 │ 8 │ 9 │ Row 0
├───┼───┼───┼───┼───┼───┼───┼───┼───┼───┤
│10 │11 │12 │13 │14 │15 │16 │17 │18 │19 │ Row 1
├───┼───┼───┼───┼───┼───┼───┼───┼───┼───┤
│20 │21 │22 │23 │24 │25 │26 │27 │28 │29 │ Row 2
└───┴───┴───┴───┴───┴───┴───┴───┴───┴───┘
```

### Tile Usage Mapping

```json
{
  "TopEdge": [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11],
  "LeftTopCorner": 12,
  "RightTopCorner": 13,
  "LeftMiddle": 14,
  "RightMiddle": 15,
  "LeftBottomCorner": 16,
  "RightBottomCorner": 17,
  "BottomEdge": [18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29]
}
```

## Rendering Process

### Initialization (MapPopupScene)

1. Load background and outline definitions from registry
2. Load textures via asset provider
3. Load font system
4. Calculate popup dimensions based on text

### Animation

**Pokeemerald-accurate timing**:

- **Slide In**: 0.4 seconds (from left, cubic ease-out)
- **Display**: 2.5 seconds (stationary in top-left corner)
- **Slide Out**: 0.4 seconds (to left, cubic ease-in)

### Drawing (per frame)

1. **Background**: Stretch bitmap to popup size
2. **Outline**:
    - If tile sheet: Assemble from tiles
    - If 9-slice: Render with corner/edge slicing
3. **Text**: Draw map name with shadow (dark navy blue)

## Tile Sheet Rendering Algorithm

```csharp
// 1. Draw corners (fixed positions)
DrawTile(LeftTopCorner, x, y);
DrawTile(RightTopCorner, x + width - tileW, y);
DrawTile(LeftBottomCorner, x, y + height - tileH);
DrawTile(RightBottomCorner, x + width - tileW, y + height - tileH);

// 2. Draw top/bottom edges (repeat tiles)
for (int i = 0; i < tilesNeeded; i++) {
    int tileIndex = TopEdge[i % TopEdge.Count];
    DrawTile(tileIndex, x + tileW + (i * tileW), y);
}

// 3. Draw left/right edges (repeat tiles)
for (int i = 0; i < tilesNeeded; i++) {
    DrawTile(LeftMiddle, x, y + tileH + (i * tileH));
    DrawTile(RightMiddle, x + width - tileW, y + tileH + (i * tileH));
}

// 4. Bottom edge (similar to top)
```

## Backwards Compatibility

The system maintains backwards compatibility with older formats:

### Property Name Compatibility

Both PascalCase and camelCase property names are supported:

```json
{
  "Id": "wood",           // New format
  "id": "wood",           // Old format (still works)
  "DisplayName": "Wood",  // New format
  "displayName": "Wood"   // Old format (still works)
}
```

### Type Detection

- If `Type: "TileSheet"` and tile data present → Use tile sheet rendering
- If `Type: "9Slice"` or no type → Use legacy 9-slice rendering
- Missing tile data → Fallback to 9-slice

## Performance

### Tile Sheet Mode

- **Pros**: Accurate to original GBA, no scaling artifacts
- **Cons**: More draw calls (one per tile)
- **Optimization**: Tiles are small (8×8), batch rendering minimizes overhead

### 9-Slice Mode

- **Pros**: Fewer draw calls (9 total), scalable
- **Cons**: Can show stretching artifacts on edges

## Usage Example

```csharp
// Get definitions from registry
var backgroundDef = popupRegistry.GetDefaultBackground();
var outlineDef = popupRegistry.GetDefaultOutline();

// Create popup scene
var popupScene = new MapPopupScene(
    graphicsDevice,
    services,
    logger,
    assetProvider,
    backgroundDef,
    outlineDef,
    "LITTLEROOT TOWN"
);

// Push to scene manager
sceneManager.PushScene(popupScene);
```

## Adding New Popup Styles

1. Extract graphics from pokeemerald using `porycon --extract-popups`
2. Manifests are automatically created in `Assets/Definitions/Maps/Popups/`
3. PNG textures are placed in `Assets/Graphics/Maps/Popups/`
4. Restart game to load new styles
5. Update registry or map metadata to use new style

## Future Enhancements

- [ ] Per-map popup style selection via map metadata
- [ ] Custom popup dimensions per style
- [ ] Animation curve customization
- [ ] Sound effects (pokeemerald has none, but we could add)
- [ ] Fade transitions
- [ ] Multiple popup styles per region



