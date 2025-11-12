# Pokemon Emerald NPC Sprite Extractor

This tool extracts NPC sprites and animations from the Pokemon Emerald source code (pokeemerald) and converts them into a format compatible with PokeSharp.Game.

## Features

- Extracts all NPC sprite sheets from pokeemerald
- Splits sprite sheets into individual frames
- Generates animation metadata based on Pokemon Emerald patterns
- Creates JSON manifests for easy integration with PokeSharp
- Supports multiple sprite sizes (16x16, 16x32, 32x32)

## Usage

```bash
# From the tools/SpriteExtractor directory
dotnet run [pokeemerald_path] [output_path]

# Example:
dotnet run ../../pokeemerald ../../PokeSharp.Game/Assets/Sprites
```

## Output Structure

```
PokeSharp.Game/Assets/Sprites/
├── Players/
│   ├── brendan/                       # Player character (Brendan) variants
│   │   ├── normal/
│   │   │   ├── manifest.json          # Sprite-specific manifest
│   │   │   └── spritesheet.png        # Sprite sheet
│   │   ├── surfing/
│   │   ├── machbike/
│   │   └── ...
│   └── may/                           # Player character (May) variants
│       ├── normal/
│       ├── surfing/
│       └── ...
└── NPCs/
    ├── generic/                       # Generic NPCs
    │   ├── boy1/
    │   │   ├── manifest.json
    │   │   └── spritesheet.png
    │   └── ...
    ├── gym_leaders/                   # Gym leader sprites
    │   ├── brawly/
    │   ├── flannery/
    │   └── ...
    ├── elite_four/                    # Elite Four members
    │   ├── sidney/
    │   └── ...
    ├── team_aqua/                     # Team Aqua sprites
    │   └── ...
    └── team_magma/                    # Team Magma sprites
        └── ...
```

## Manifest Format

Each sprite has a `manifest.json` file containing:

```json
{
  "Name": "boy1",
  "Category": "generic",
  "OriginalPath": "boy_1.png",
  "OutputDirectory": "generic/boy1",
  "SpriteSheet": "spritesheet.png",
  "FrameWidth": 16,
  "FrameHeight": 32,
  "FrameCount": 9,
  "Frames": [
    {
      "Index": 0,
      "X": 0,
      "Y": 0,
      "Width": 16,
      "Height": 32
    }
  ],
  "Animations": [
    {
      "Name": "face_south",
      "Loop": true,
      "Frames": [
        {
          "FrameIndex": 0,
          "Duration": 1,
          "HorizontalFlip": false
        }
      ]
    },
    {
      "Name": "go_south",
      "Loop": true,
      "Frames": [
        {
          "FrameIndex": 1,
          "Duration": 8,
          "HorizontalFlip": false
        },
        {
          "FrameIndex": 0,
          "Duration": 8,
          "HorizontalFlip": false
        },
        {
          "FrameIndex": 2,
          "Duration": 8,
          "HorizontalFlip": false
        }
      ]
    }
  ]
}
```

## Animation Data

All animations are extracted directly from pokeemerald's source code, including:
- Frame indices (with proper logical-to-physical frame mapping)
- Frame durations
- Horizontal flip flags
- Animation names (e.g., `face_south`, `go_south`, `go_fast_south`)

### Standard Animation Table (9 frames, 20 animations)
Most NPCs and some player sprites use the standard animation table:
- `face_south`, `face_north`, `face_west`, `face_east` (idle/facing)
- `go_south`, `go_north`, `go_west`, `go_east` (walking)
- `go_fast_*`, `go_faster_*`, `go_fastest_*` (running speeds)

### Player Character Normal Sprites (18 frames, 24 animations)
Player characters in their normal state combine walking and running animations:
- All standard animations plus additional running variations

### Special Sprites
- **Surfing**: 12 frames, 24 animations
- **Acro Bike**: 27 frames, 40 animations
- **Fishing**: 12 frames, 12 animations
- **Field Move**: 5 frames, 1 animation

## Integration with PokeSharp

The extracted sprites can be loaded using PokeSharp's asset system. The animation data is compatible with the sprite animation system in `PokeSharp.Engine.Rendering`.

