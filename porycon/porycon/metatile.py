"""
Metatile conversion utilities.

Converts Pokemon Emerald metatiles (2x2 groups of 8 tiles) into individual tiles
distributed across BG layers based on layer type.
"""

from typing import List, Tuple, Optional
from enum import IntEnum
from .constants import (
    NUM_TILES_PER_METATILE,
    METATILE_WIDTH,
    METATILE_HEIGHT,
    METATILE_ID_MASK,
    ELEVATION_MASK,
    COLLISION_MASK
)


class MetatileLayerType(IntEnum):
    """Metatile layer type determines how tiles are distributed across BG layers."""
    NORMAL = 0   # Bottom 4 tiles -> Bg2 (middle), Top 4 tiles -> Bg1 (top)
    COVERED = 1  # Bottom 4 tiles -> Bg3 (bottom), Top 4 tiles -> Bg2 (middle)
    SPLIT = 2    # Bottom 4 tiles -> Bg3 (bottom), Top 4 tiles -> Bg1 (top)


# Metatile structure: 8 tiles total
# Tiles 0-3: Bottom layer (2x2)
# Tiles 4-7: Top layer (2x2)
# Note: NUM_TILES_PER_METATILE, METATILE_WIDTH, METATILE_HEIGHT are imported from constants


def unpack_metatile_data(metatile_id: int, metatile_data: List[int]) -> List[int]:
    """
    Extract the 8 tiles from a metatile.
    
    Args:
        metatile_id: The metatile ID (0-1023)
        metatile_data: The full metatiles array from tileset
        
    Returns:
        List of 8 tile IDs: [bottom_tl, bottom_tr, bottom_bl, bottom_br, top_tl, top_tr, top_bl, top_br]
    """
    if metatile_id < 0 or metatile_id * NUM_TILES_PER_METATILE >= len(metatile_data):
        return [0] * NUM_TILES_PER_METATILE
    
    start_idx = metatile_id * NUM_TILES_PER_METATILE
    return metatile_data[start_idx:start_idx + NUM_TILES_PER_METATILE]


def unpack_metatile_data_with_attrs(metatile_id: int, metatile_data: List[Tuple[int, int, int]]) -> List[Tuple[int, int, int]]:
    """
    Extract the 8 tiles from a metatile with full attributes.
    
    Args:
        metatile_id: The metatile ID (0-1023)
        metatile_data: The full metatiles array with (tile_id, flip_flags, palette_index) tuples
        
    Returns:
        List of 8 tuples: [(tile_id, flip, palette), ...]
    """
    if metatile_id < 0 or metatile_id * NUM_TILES_PER_METATILE >= len(metatile_data):
        return [(0, 0, 0)] * NUM_TILES_PER_METATILE
    
    start_idx = metatile_id * NUM_TILES_PER_METATILE
    return metatile_data[start_idx:start_idx + NUM_TILES_PER_METATILE]


def split_metatile_to_layers(
    metatile_tiles: List[int],
    layer_type: MetatileLayerType
) -> Tuple[List[int], List[int], List[int]]:
    """
    Split a metatile's 8 tiles into three BG layers based on layer type.
    
    Args:
        metatile_tiles: 8 tile IDs [bottom_tl, bottom_tr, bottom_bl, bottom_br, top_tl, top_tr, top_bl, top_br]
        layer_type: How to distribute tiles across layers
        
    Returns:
        Tuple of (bg3_tiles, bg2_tiles, bg1_tiles) - each is a 2x2 grid as flat list
        Format: [tl, tr, bl, br] for each layer
    """
    bottom_tiles = metatile_tiles[0:4]  # [tl, tr, bl, br]
    top_tiles = metatile_tiles[4:8]      # [tl, tr, bl, br]
    
    if layer_type == MetatileLayerType.NORMAL:
        # Bottom -> Bg2, Top -> Bg1, Bg3 empty
        return ([0, 0, 0, 0], bottom_tiles, top_tiles)
    
    elif layer_type == MetatileLayerType.COVERED:
        # Bottom -> Bg3, Top -> Bg2, Bg1 empty
        return (bottom_tiles, top_tiles, [0, 0, 0, 0])
    
    elif layer_type == MetatileLayerType.SPLIT:
        # Bottom -> Bg3, Top -> Bg1, Bg2 empty
        return (bottom_tiles, [0, 0, 0, 0], top_tiles)
    
    else:
        # Default to NORMAL
        return ([0, 0, 0, 0], bottom_tiles, top_tiles)


def convert_metatile_to_tile_layers_with_attrs(
    metatile_id: int,
    metatile_data: List[Tuple[int, int, int]],
    layer_type: MetatileLayerType,
    x: int,
    y: int,
    map_width: int
) -> Tuple[dict, dict, dict]:
    """
    Convert a metatile with attributes to tile layers, preserving palette information.
    
    Args:
        metatile_id: Metatile ID
        metatile_data: List of (tile_id, flip_flags, palette_index) tuples
        layer_type: How to distribute across layers
        x: Metatile X position
        y: Metatile Y position
        map_width: Map width in metatiles
    
    Returns:
        Tuple of three dicts: (bg3_tiles, bg2_tiles, bg1_tiles)
        Each dict maps (tile_x, tile_y) -> (tile_id, palette_index)
    """
    metatile_tiles = unpack_metatile_data_with_attrs(metatile_id, metatile_data)
    
    # Split into bottom (0-3) and top (4-7) tiles
    bottom_tiles = metatile_tiles[0:4]  # [(tile_id, flip, palette), ...]
    top_tiles = metatile_tiles[4:8]
    
    # Determine which layers get which tiles based on layer_type
    if layer_type == MetatileLayerType.NORMAL:
        # Bottom -> Bg2, Top -> Bg1, Bg3 empty
        bg3_tiles_data = []
        bg2_tiles_data = bottom_tiles
        bg1_tiles_data = top_tiles
    elif layer_type == MetatileLayerType.COVERED:
        # Bottom -> Bg3, Top -> Bg2, Bg1 empty
        bg3_tiles_data = bottom_tiles
        bg2_tiles_data = top_tiles
        bg1_tiles_data = []
    elif layer_type == MetatileLayerType.SPLIT:
        # Bottom -> Bg3, Top -> Bg1, Bg2 empty
        bg3_tiles_data = bottom_tiles
        bg2_tiles_data = []
        bg1_tiles_data = top_tiles
    else:
        # Default to NORMAL
        bg3_tiles_data = []
        bg2_tiles_data = bottom_tiles
        bg1_tiles_data = top_tiles
    
    # Convert to dicts with palette info
    base_tile_x = x * 2
    base_tile_y = y * 2
    
    def create_tile_dict(tiles_data: List[Tuple[int, int, int]]) -> dict:
        """Create dict mapping (tile_x, tile_y) -> (tile_id, palette_index) for a 2x2 grid."""
        result = {}
        tile_idx = 0
        for ty in range(2):
            for tx in range(2):
                tile_x = base_tile_x + tx
                tile_y = base_tile_y + ty
                if tile_idx < len(tiles_data):
                    tile_id, flip_flags, palette_idx = tiles_data[tile_idx]
                    # Extract palette index from the tuple (it's the 3rd element)
                    if tile_id != 0:  # Only add non-empty tiles
                        result[(tile_x, tile_y)] = (tile_id, palette_idx)
                tile_idx += 1
        return result
    
    bg3_tiles = create_tile_dict(bg3_tiles_data)
    bg2_tiles = create_tile_dict(bg2_tiles_data)
    bg1_tiles = create_tile_dict(bg1_tiles_data)
    
    return bg3_tiles, bg2_tiles, bg1_tiles


def convert_metatile_to_tile_layers(
    metatile_id: int,
    metatile_data: List[int],
    layer_type: MetatileLayerType,
    x: int,
    y: int,
    map_width: int
) -> Tuple[dict, dict, dict]:
    """
    Convert a single metatile at position (x, y) to individual tiles in three layers.
    
    Args:
        metatile_id: Metatile ID
        metatile_data: Full metatiles array
        layer_type: How to distribute across layers
        x: Metatile X position
        y: Metatile Y position
        map_width: Map width in metatiles
        
    Returns:
        Tuple of three dicts: (bg3_tiles, bg2_tiles, bg1_tiles)
        Each dict maps (tile_x, tile_y) -> tile_id
    """
    metatile_tiles = unpack_metatile_data(metatile_id, metatile_data)
    bg3_tiles, bg2_tiles, bg1_tiles = split_metatile_to_layers(metatile_tiles, layer_type)
    
    # Convert 2x2 metatile to individual tile positions
    tile_x = x * METATILE_WIDTH
    tile_y = y * METATILE_HEIGHT
    
    def create_tile_dict(tiles: List[int]) -> dict:
        """Create dict mapping (tile_x, tile_y) -> tile_id for a 2x2 grid."""
        return {
            (tile_x, tile_y): tiles[0],      # Top-left
            (tile_x + 1, tile_y): tiles[1],  # Top-right
            (tile_x, tile_y + 1): tiles[2], # Bottom-left
            (tile_x + 1, tile_y + 1): tiles[3], # Bottom-right
        }
    
    return (
        create_tile_dict(bg3_tiles),
        create_tile_dict(bg2_tiles),
        create_tile_dict(bg1_tiles)
    )


def read_map_bin(filepath: str, width: int, height: int) -> List[List[int]]:
    """
    Read a map.bin file containing metatile data.
    
    Format: Each entry is u16 with:
    - Bits 0-9: Metatile ID
    - Bits 10-11: Collision
    - Bits 12-15: Elevation
    
    Returns:
        2D list [y][x] of metatile entries (u16 values)
    """
    import struct
    
    with open(filepath, 'rb') as f:
        data = f.read()
    
    # Each entry is 2 bytes (u16)
    entries = []
    for i in range(0, len(data), 2):
        if i + 1 < len(data):
            entry = struct.unpack('<H', data[i:i+2])[0]
            entries.append(entry)
    
    # Reshape to 2D [y][x]
    if len(entries) != width * height:
        raise ValueError(f"Expected {width * height} entries, got {len(entries)}")
    
    map_data = []
    for y in range(height):
        row = entries[y * width:(y + 1) * width]
        map_data.append(row)
    
    return map_data


def extract_metatile_id(entry: int) -> int:
    """Extract metatile ID from map entry (bits 0-9)."""
    return entry & METATILE_ID_MASK


def extract_elevation(entry: int) -> int:
    """Extract elevation from map entry (bits 12-15)."""
    return (entry & ELEVATION_MASK) >> 12


def extract_collision(entry: int) -> int:
    """Extract collision from map entry (bits 10-11)."""
    return (entry & COLLISION_MASK) >> 10

