# Porycon - Pokemon Emerald to Tiled Converter

A Python tool to convert Pokemon Emerald decompilation maps to Tiled JSON format, replacing metatiles with individual tile layers for easier editing.

## Features

- Converts pokeemerald map.json files to Tiled format
- Splits metatiles into individual tiles across separate BG layers
- Creates complete tilesets (no metatiles) for Tiled editing
- Generates Tiled world files from map connections
- **Tile animations**: Converts automatic and trigger-based animations (see [Animation Guide](docs/animations.md))
- Organizes output: Maps in region folders, Worlds at root, Tilesets in region folders

## Project Structure

```
porycon/
├── porycon/
│   ├── __init__.py
│   ├── converter.py          # Main conversion logic
│   ├── metatile.py          # Metatile to tile conversion
│   ├── tileset_builder.py   # Complete tileset generation
│   ├── world_builder.py     # World file generation
│   └── utils.py             # Utility functions
├── tests/
├── requirements.txt
└── README.md
```

## Installation

```bash
cd porycon
pip install -e .
```

Or install dependencies directly:
```bash
pip install -r requirements.txt
```

## Usage

### Basic Usage

Convert all maps:
```bash
python -m porycon --input /path/to/pokeemerald --output /path/to/output
```

Convert specific region:
```bash
python -m porycon --input /path/to/pokeemerald --output /path/to/output --region hoenn
```

### Output Structure

The converter creates the following structure:

```
output/
├── Maps/
│   └── hoenn/
│       ├── mauvillecity.json
│       ├── littleroottown.json
│       └── ...
├── Worlds/
│   ├── hoenn.world
│   └── ...
└── Tilesets/
    └── hoenn/
        ├── general.json
        ├── general.png
        ├── mauville.json
        ├── mauville.png
        └── ...
```

### How It Works

1. **Metatile Conversion**: Each 2x2 metatile (8 tiles) is split into individual tiles
2. **Layer Distribution**: Tiles are distributed across 3 BG layers based on metatile layer type:
   - **NORMAL**: Bottom tiles → Objects layer, Top tiles → Overhead layer
   - **COVERED**: Bottom tiles → Ground layer, Top tiles → Objects layer
   - **SPLIT**: Bottom tiles → Ground layer, Top tiles → Overhead layer
3. **Tileset Building**: Complete tilesets are created containing only tiles actually used in maps
4. **World Files**: Tiled world files are generated from map connections

## Requirements

- Python 3.8+
- Pillow (for image processing)
- See requirements.txt for full dependencies

## Documentation

- **[Animation Guide](docs/animations.md)** - Complete guide to tile animations (automatic & trigger-based)
- **Project Structure** - See directory layout above

## Notes

- The converter creates tilesets with only used tiles (not all tiles from source)
- Tile IDs are remapped to be sequential (1-based for Tiled)
- Maps reference tilesets via relative paths
- World files use a simple grid layout (can be improved with graph algorithms)
- Animation support includes water, flowers, waterfalls, and more (see Animation Guide)

