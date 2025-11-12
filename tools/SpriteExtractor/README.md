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
dotnet run ../../pokeemerald ../../PokeSharp.Game/Assets/Sprites/NPCs
```

## Output Structure

```
PokeSharp.Game/Assets/Sprites/NPCs/
├── npc_sprites_manifest.json          # Master manifest of all sprites
├── generic/                            # Generic NPCs
│   ├── boy_1/
│   │   ├── manifest.json              # Sprite-specific manifest
│   │   ├── spritesheet.png            # Original sprite sheet
│   │   ├── frame_00.png               # Individual frames
│   │   ├── frame_01.png
│   │   └── ...
│   └── ...
├── gym_leaders/                        # Gym leader sprites
│   ├── brawly/
│   ├── flannery/
│   └── ...
└── team_aqua/                          # Team Aqua sprites
    └── ...
```

## Manifest Format

Each sprite has a `manifest.json` file containing:

```json
{
  "Name": "boy_1",
  "Category": "generic",
  "OriginalPath": "boy_1.png",
  "OutputDirectory": "generic/boy_1",
  "SpriteSheet": "spritesheet.png",
  "FrameWidth": 16,
  "FrameHeight": 32,
  "FrameCount": 9,
  "Frames": [
    {
      "Index": 0,
      "FileName": "frame_00.png",
      "Width": 16,
      "Height": 32
    }
  ],
  "Animations": [
    {
      "Name": "walk_down",
      "Loop": true,
      "FrameIndices": [0, 1, 0, 2],
      "FrameDuration": 0.15
    }
  ]
}
```

## Standard Animation Patterns

### 9-Frame Sprites (Most NPCs)
- Frames 0-2: Facing down (idle + 2 walk frames)
- Frames 3-5: Facing up (idle + 2 walk frames)
- Frames 6-8: Facing side (idle + 2 walk frames)

### 18-Frame Sprites (Player characters)
- Frames 0-8: Walking animations
- Frames 9-17: Running animations

## Integration with PokeSharp

The extracted sprites can be loaded using PokeSharp's asset system. The animation data is compatible with the sprite animation system in `PokeSharp.Engine.Rendering`.

