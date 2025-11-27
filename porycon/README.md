# Porycon - Pokemon Emerald to Tiled Converter

A Python tool to convert Pokemon Emerald decompilation maps to Tiled JSON format, replacing metatiles with individual tile layers for easier editing.

## Features

- Converts pokeemerald map.json files to Tiled format
- Splits metatiles into individual tiles across separate BG layers
- Creates complete tilesets (no metatiles) for Tiled editing
- Generates Tiled world files from map connections
- Organizes output: Maps in region folders, Worlds at root, Tilesets in region folders

## Project Structure

```
porycon/
├── porycon/
│   ├── __init__.py
│   ├── __main__.py          # CLI entry point
│   ├── converter.py          # Main conversion logic (MapConverter class)
│   ├── map_reader.py         # File I/O and parsing (MapReader class)
│   ├── metatile_processor.py # Metatile processing logic (MetatileProcessor class)
│   ├── metatile_renderer.py  # Metatile rendering (MetatileRenderer class)
│   ├── metatile.py          # Metatile utilities and layer types
│   ├── tileset_builder.py   # Complete tileset generation
│   ├── world_builder.py     # World file generation
│   ├── animation_scanner.py  # Animation frame scanning
│   ├── palette_loader.py   # Palette file loading
│   ├── validators.py        # Input validation functions
│   ├── constants.py         # Constants and magic numbers
│   ├── logging_config.py   # Logging configuration
│   └── utils.py             # Utility functions
├── tests/
├── requirements.txt
└── README.md
```

## Architecture

### Core Components

**MapConverter** (`converter.py`)
- Main orchestrator for map conversion
- Coordinates the conversion pipeline
- Handles map structure, events, and tileset generation

**MapReader** (`map_reader.py`)
- Handles all file I/O operations
- Reads map.bin, metatiles.bin, and metatile_attributes.bin files
- Provides clean interface for file access

**MetatileProcessor** (`metatile_processor.py`)
- Processes individual metatiles
- Determines tileset membership (primary/secondary)
- Validates metatile bounds
- Handles metatile-to-tile conversion logic

**MetatileRenderer** (`metatile_renderer.py`)
- Renders metatiles as 16x16 images
- Applies palettes and flips
- Caches tileset images and palettes for performance

**TilesetBuilder** (`tileset_builder.py`)
- Builds complete tilesets from used tiles
- Handles tile remapping and consolidation
- Generates tileset images and JSON

**WorldBuilder** (`world_builder.py`)
- Generates Tiled world files
- Handles map connections and positioning

### Conversion Pipeline

1. **Input Validation** (`validators.py`)
   - Validates map dimensions, IDs, tileset names, etc.
   - Provides clear error messages for invalid inputs

2. **Map Reading** (`MapReader`)
   - Reads map.json and map.bin files
   - Loads metatiles and attributes for both tilesets

3. **Metatile Processing** (`MetatileProcessor`)
   - Determines which tileset each metatile belongs to
   - Validates metatile bounds
   - Processes metatiles through the renderer

4. **Metatile Rendering** (`MetatileRenderer`)
   - Renders each metatile as bottom/top 16x16 images
   - Applies palettes and handles flips
   - Caches results for performance

5. **GID Assignment**
   - Assigns Global Tile IDs (GIDs) to metatiles
   - Deduplicates identical images
   - Builds mappings for animations

6. **Layer Building**
   - Creates BG3, BG2, BG1 layers based on metatile layer types
   - Assigns GIDs to appropriate layers

7. **Tileset Generation** (`TilesetBuilder`)
   - Creates per-map tilesets with only used metatiles
   - Generates tileset images and JSON
   - Adds animation data

8. **Event Conversion**
   - Converts warps, coord events, and background events to Tiled objects
   - Handles warp destination resolution

9. **Output Generation**
   - Creates final Tiled map JSON structure
   - Saves maps, tilesets, and world files

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

The converter uses a **per-map tileset** approach where each map gets its own tileset containing only the metatiles actually used in that map. This approach:

1. **Metatile Rendering**: Each 2x2 metatile (8 tiles) is rendered as two 16x16 images:
   - **Bottom layer**: Tiles 0-3 (bottom-left, bottom-right, top-left, top-right)
   - **Top layer**: Tiles 4-7 (same positions, upper layer)

2. **GID Assignment**: Global Tile IDs (GIDs) are assigned to metatiles with deduplication:
   - Identical images share the same GID
   - GIDs start at 1 (Tiled uses 1-based indexing)
   - Each metatile gets two GIDs: one for bottom, one for top

3. **Layer Distribution**: Metatiles are assigned to layers based on their layer type:
   - **NORMAL (0)**: Bottom → BG1, Top → BG2
   - **COVERED (1)**: Bottom → BG1, Top → BG2 (with different rendering)
   - **SPLIT (2)**: Bottom → BG1, Top → BG3
   - **SPLIT_SPECIAL (3)**: Similar to SPLIT with special handling

4. **Tileset Building**: Per-map tilesets are created containing:
   - Only metatiles used in that specific map
   - Animation data for animated tiles
   - Optimized tile layout

5. **Event Conversion**: Pokemon Emerald events are converted to Tiled objects:
   - **Warps**: Converted to Tiled objects with custom properties
   - **Coord Events**: Converted to trigger objects
   - **Background Events**: Converted to interaction objects

6. **World Files**: Tiled world files are generated from map connections, allowing Tiled to show the full map layout

## Requirements

- Python 3.8+
- Pillow (for image processing)
- See requirements.txt for full dependencies

## Data Formats

### Pokemon Emerald Format

**map.bin**: Binary file containing metatile entries
- Each entry is 2 bytes (u16)
- Bits 0-9: Metatile ID (0-1023)
- Bits 10-11: Collision type
- Bits 12-15: Elevation

**metatiles.bin**: Binary file containing metatile tile data
- Each metatile consists of 8 tiles (2x2 grid, 2 layers)
- Each tile entry is 2 bytes (u16)
- Bits 0-9: Tile ID (0-1023)
- Bit 10: Horizontal flip
- Bit 11: Vertical flip
- Bits 12-15: Palette index (0-15)

**metatile_attributes.bin**: Binary file containing metatile attributes
- Each entry is 2 bytes (u16)
- Bits 0-7: Behavior
- Bits 12-15: Layer type (0=NORMAL, 1=COVERED, 2=SPLIT, 3=SPLIT_SPECIAL)

### Tiled Format

The converter generates Tiled-compatible JSON maps with:
- **Layers**: BG3, BG2, BG1 (from bottom to top)
- **Tilesets**: Per-map tilesets with only used metatiles
- **Objects**: Warps, triggers, and interactions as Tiled objects
- **Properties**: Custom properties for game-specific data

## Notes

- The converter creates tilesets with only used metatiles (not all metatiles from source)
- GIDs are assigned sequentially starting from 1 (Tiled uses 1-based indexing)
- Maps reference tilesets via relative paths
- World files use a simple grid layout (can be improved with graph algorithms)
- Metatile images are deduplicated to minimize tileset size

