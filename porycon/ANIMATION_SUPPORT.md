# Tile Animation Support in Porycon

## Overview

Porycon now supports converting pokeemerald tile animations to Tiled format. This allows animated tiles (like water, flowers, waterfalls) to be properly displayed and edited in Tiled.

## How It Works

### 1. Animation Detection

The `AnimationScanner` class scans for animation frames in the `anim` folders:
- `data/tilesets/primary/{tileset_name}/anim/{animation_name}/0.png, 1.png, ...`
- `data/tilesets/secondary/{tileset_name}/anim/{animation_name}/0.png, 1.png, ...`

### 2. Animation Mappings

Animations are mapped based on hardcoded tile offsets from `tileset_anims.c`. The mappings are defined in `animation_scanner.py`:

```python
ANIMATION_MAPPINGS = {
    "general": {
        "water": {
            "base_tile_id": 432,
            "num_tiles": 30,
            "anim_folder": "water",
            "duration_ms": 200
        },
        # ... more animations
    }
}
```

### 3. Tile Extraction

For each animation:
1. All frame images are loaded from the `anim` folder
2. Tiles are extracted from each frame (frames contain multiple tiles laid out horizontally)
3. Animation tiles are added to the tileset image after the regular tiles

### 4. Tiled Format

Animations are added to the tileset JSON in Tiled's format:

```json
{
  "tiles": [
    {
      "id": 42,
      "animation": [
        {"tileid": 100, "duration": 200},
        {"tileid": 101, "duration": 200},
        {"tileid": 102, "duration": 200}
      ]
    }
  ]
}
```

## Supported Animations

Currently supported tilesets and animations:

### Primary Tilesets
- **general**: flower, water, sand_water_edge, waterfall, land_water_edge
- **building**: tv_turned_on

### Secondary Tilesets
- **rustboro**: windy_water, fountain
- **dewford**: flag
- **slateport**: balloons
- **mauville**: flower_1, flower_2
- **lavaridge**: steam, lava
- **ever_grande**: flowers
- **pacifidlog**: log_bridges, water_currents
- **sootopolis**: stormy_water
- **underwater**: seaweed
- **cave**: lava
- **battle_frontier_outside_west/east**: flag
- **mauville_gym**: electric_gates
- **sootopolis_gym**: side_waterfall, front_waterfall
- **elite_four**: floor_light, wall_lights
- **bike_shop**: blinking_lights
- **battle_pyramid**: torch, statue_shadow

## Usage

Animations are automatically detected and added when running porycon:

```bash
python -m porycon --input /path/to/pokeemerald --output /path/to/output
```

The converter will:
1. Scan for `anim` folders in each tileset
2. Extract animation frames
3. Add animation tiles to the tileset image
4. Create animation entries in the tileset JSON

## Technical Details

### Tile ID Mapping

- Base tiles use the existing `tile_mapping` from `(old_tile_id, palette) -> new_tile_id`
- Animation frame tiles are assigned sequential IDs after all base tiles
- Animation tile indices are calculated as: `base_tile_id * 10000 + frame_idx * 1000 + tile_offset`

### Frame Duration

Default frame duration is 200ms (5 frames per second). This can be adjusted in `ANIMATION_MAPPINGS` per animation.

## Limitations

1. **Palette Support**: Currently only palette 0 is used for animation base tiles. Multi-palette animations may not work correctly.

2. **Tile Offset Mapping**: The base_tile_id mappings are hardcoded based on `tileset_anims.c`. If pokeemerald's tile layout changes, these may need updating.

3. **Frame Layout**: Animation frames are assumed to contain tiles laid out horizontally. If frames use different layouts, extraction may fail.

## Future Improvements

- Support for multi-palette animations
- Automatic detection of animation frame layouts
- Configurable frame durations per animation
- Support for palette animations (like Battle Dome floor lights)




