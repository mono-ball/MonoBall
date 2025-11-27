# Implementation Notes

## Current Limitations

1. **Tile ID Remapping**: The current implementation doesn't fully remap tile IDs after tileset creation. Maps may need manual adjustment or a second pass to update tile GIDs.

2. **Tileset Source Detection**: The converter tries multiple paths to find tileset graphics, but may not find them if they're in non-standard locations.

3. **World Layout**: World files use a simple grid layout. A graph-based layout algorithm would be better for positioning maps based on connections.

4. **Secondary Tileset Handling**: When a map uses both primary and secondary tilesets, tile IDs need to be offset correctly. This is partially implemented.

## Future Improvements

1. **Two-Pass Conversion**:
   - Pass 1: Convert all maps, track which tileset each tile belongs to
   - Pass 2: Build tilesets, remap all tile IDs in maps

2. **Better World Layout**:
   - Use graph algorithms (force-directed, hierarchical) to position maps
   - Respect connection offsets and directions

3. **Tileset Merging**:
   - Option to merge primary and secondary tilesets into one
   - Or keep separate but handle GID offsets correctly

4. **Metatile Attributes**:
   - Preserve collision and behavior data as tile properties
   - Export elevation as layer or tile property

## File Structure

```
pokeemerald/
├── data/
│   ├── maps/
│   │   └── {MapName}/
│   │       └── map.json
│   ├── layouts/
│   │   ├── layouts.json
│   │   └── {LayoutName}/
│   │       ├── map.bin
│   │       └── border.bin
│   └── tilesets/
│       └── {TilesetName}/
│           ├── tiles.png (or tiles.4bpp.png)
│           ├── metatiles.bin
│           └── metatile_attributes.bin
```

## Metatile Format

- Each metatile is 2x2 tiles = 8 tiles total
- Tiles 0-3: Bottom layer (2x2)
- Tiles 4-7: Top layer (2x2)
- Map entry (u16): metatile_id (10 bits) | collision (2 bits) | elevation (4 bits)
- Metatile attributes (u16): behavior (8 bits) | unused (4 bits) | layer_type (4 bits)

## Layer Type Distribution

- **NORMAL**: Bottom tiles → Bg2 (Objects), Top tiles → Bg1 (Overhead)
- **COVERED**: Bottom tiles → Bg3 (Ground), Top tiles → Bg2 (Objects)  
- **SPLIT**: Bottom tiles → Bg3 (Ground), Top tiles → Bg1 (Overhead)







