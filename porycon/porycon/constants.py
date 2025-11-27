"""
Constants for Pokemon Emerald format.

This module contains all magic numbers and constants used throughout the codebase
to make the code more maintainable and self-documenting.
"""

# Tileset constants
NUM_METATILES_IN_PRIMARY = 512
NUM_TILES_IN_PRIMARY_VRAM = 512

# Tile dimensions
TILE_SIZE = 8  # 8x8 pixels
METATILE_SIZE = 16  # 16x16 pixels (2x2 tiles)
NUM_TILES_PER_METATILE = 8
METATILE_WIDTH = 2  # Metatiles are 2 tiles wide
METATILE_HEIGHT = 2  # Metatiles are 2 tiles tall

# Bit masks for metatile data
METATILE_ID_MASK = 0x03FF  # Bits 0-9: Metatile ID
COLLISION_MASK = 0x0C00    # Bits 10-11: Collision type
ELEVATION_MASK = 0xF000    # Bits 12-15: Elevation
PALETTE_MASK = 0xF000      # Bits 12-15: Palette index (same bits as elevation in different context)

# Flip flags
FLIP_HORIZONTAL = 0x01
FLIP_VERTICAL = 0x02

# Palette constants
NUM_PALETTES_PER_TILESET = 16
PALETTE_COLORS = 16

# Animation constants
DEFAULT_ANIMATION_DURATION_MS = 200
STANDARD_ANIMATION_FRAMES = 8

# Tileset image layout
TILES_PER_ROW_DEFAULT = 16  # Default tiles per row in tileset images

