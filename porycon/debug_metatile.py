"""
Debug script to inspect metatile data at route117 (22, 17)
"""
import json
import struct
import sys
from pathlib import Path

# Add parent directory to path
sys.path.insert(0, str(Path(__file__).parent.parent))

from porycon.porycon.metatile import read_map_bin, extract_metatile_id, extract_elevation

def _get_tileset_path(input_dir, tileset_name):
    """Helper to get tileset path (simplified version)"""
    input_dir = Path(input_dir)
    name_variants = [
        tileset_name.replace("_", "").lower(),
        tileset_name.lower(),
        tileset_name,
    ]
    
    for tileset_lower in name_variants:
        primary_path = input_dir / "data" / "tilesets" / "primary" / tileset_lower
        if primary_path.exists():
            return ("primary", primary_path)
        secondary_path = input_dir / "data" / "tilesets" / "secondary" / tileset_lower
        if secondary_path.exists():
            return ("secondary", secondary_path)
    
    return (None, None)

def load_metatiles_with_attrs(input_dir, tileset_name):
    """Load metatiles with full attributes"""
    category, tileset_dir = _get_tileset_path(input_dir, tileset_name)
    if not tileset_dir:
        return []
    
    metatiles_path = tileset_dir / "metatiles.bin"
    if not metatiles_path.exists():
        return []
    
    with open(metatiles_path, 'rb') as f:
        data = f.read()
    
    metatiles = []
    for i in range(0, len(data), 2):
        if i + 1 < len(data):
            tile_attr = struct.unpack('<H', data[i:i+2])[0]
            tile_id = tile_attr & 0x3FF
            flip_flags = (tile_attr >> 10) & 0x3
            palette_index = (tile_attr >> 12) & 0xF
            metatiles.append((tile_id, flip_flags, palette_index))
    
    return metatiles

def load_metatile_attributes(input_dir, tileset_name):
    """Load metatile attributes"""
    category, tileset_dir = _get_tileset_path(input_dir, tileset_name)
    if not tileset_dir:
        return {}
    
    attributes_path = tileset_dir / "metatile_attributes.bin"
    if not attributes_path.exists():
        return {}
    
    with open(attributes_path, 'rb') as f:
        data = f.read()
    
    attributes = {}
    for i in range(0, len(data), 2):
        if i + 1 < len(data):
            attr = struct.unpack('<H', data[i:i+2])[0]
            metatile_id = i // 2
            layer_type = (attr >> 12) & 0x0F
            attributes[metatile_id] = layer_type
    
    return attributes

def main():
    input_dir = Path("pokeemerald")
    
    # Load map data
    map_json_path = input_dir / "data" / "maps" / "Route117" / "map.json"
    with open(map_json_path, 'r') as f:
        map_data = json.load(f)
    
    layout_id = map_data["layout"]
    
    # Load layout data - use the same method as the converter
    from porycon.porycon.utils import find_layout_files
    layout_data = find_layout_files(str(input_dir))
    
    if layout_id not in layout_data:
        print(f"ERROR: Layout {layout_id} not found!")
        print(f"Available layouts (first 10): {list(layout_data.keys())[:10]}")
        return
    
    layout = layout_data[layout_id]
    
    # Get map dimensions
    width = layout["width"]
    height = layout["height"]
    
    # Read map.bin
    map_bin_path = Path(layout["map_bin"])
    if not map_bin_path.exists():
        print(f"ERROR: map.bin not found at {map_bin_path}")
        return
    
    map_entries = read_map_bin(str(map_bin_path), width, height)
    
    # Check position (22, 17)
    x, y = 22, 17
    if y >= height or x >= width:
        print(f"ERROR: Position ({x}, {y}) is out of bounds! Map size is {width}x{height}")
        return
    
    entry = map_entries[y][x]
    metatile_id = extract_metatile_id(entry)
    elevation = extract_elevation(entry)
    
    print(f"=== Route117 Metatile at ({x}, {y}) ===")
    print(f"Map entry (raw): 0x{entry:04X}")
    print(f"Metatile ID: {metatile_id}")
    print(f"Elevation: {elevation}")
    
    # Get tilesets
    def _get_tileset_name(tileset_id):
        if tileset_id.startswith("gTileset_"):
            return tileset_id.replace("gTileset_", "")
        return tileset_id
    
    primary_tileset = _get_tileset_name(layout["primary_tileset"])
    secondary_tileset = _get_tileset_name(layout["secondary_tileset"])
    
    print(f"\nPrimary tileset: {primary_tileset}")
    print(f"Secondary tileset: {secondary_tileset}")
    
    # Determine which tileset
    if metatile_id < 512:
        tileset_name = primary_tileset
        actual_metatile_id = metatile_id
    else:
        tileset_name = secondary_tileset
        actual_metatile_id = metatile_id - 512
    
    print(f"\nUsing tileset: {tileset_name}")
    print(f"Actual metatile ID (within tileset): {actual_metatile_id}")
    
    # Load metatile attributes
    attributes = load_metatile_attributes(input_dir, tileset_name)
    layer_type_val = attributes.get(actual_metatile_id, 0)
    
    layer_type_names = ["NORMAL", "COVERED", "SPLIT"]
    layer_type_name = layer_type_names[layer_type_val] if layer_type_val < len(layer_type_names) else f"UNKNOWN({layer_type_val})"
    
    print(f"\nLayer type: {layer_type_name} ({layer_type_val})")
    
    # Load metatile tiles
    metatiles_with_attrs = load_metatiles_with_attrs(input_dir, tileset_name)
    if not metatiles_with_attrs:
        print(f"\nERROR: Could not load metatiles for {tileset_name}")
        return
    
    start_idx = actual_metatile_id * 8
    if start_idx + 8 > len(metatiles_with_attrs):
        print(f"\nERROR: Metatile {actual_metatile_id} is out of bounds!")
        print(f"  Start index: {start_idx}, Metatiles length: {len(metatiles_with_attrs)}")
        return
    
    metatile_tiles = metatiles_with_attrs[start_idx:start_idx + 8]
    
    print(f"\n=== Metatile Tiles (8 tiles total) ===")
    print(f"Tile positions in metatile:")
    print(f"  Bottom row (tiles 0-3):")
    print(f"    [0] Top-Left   (TL): tile_id={metatile_tiles[0][0]:3d}, flip={metatile_tiles[0][1]:d}, palette={metatile_tiles[0][2]:d}")
    print(f"    [1] Top-Right  (TR): tile_id={metatile_tiles[1][0]:3d}, flip={metatile_tiles[1][1]:d}, palette={metatile_tiles[1][2]:d}")
    print(f"    [2] Bottom-Left (BL): tile_id={metatile_tiles[2][0]:3d}, flip={metatile_tiles[2][1]:d}, palette={metatile_tiles[2][2]:d}")
    print(f"    [3] Bottom-Right(BR): tile_id={metatile_tiles[3][0]:3d}, flip={metatile_tiles[3][1]:d}, palette={metatile_tiles[3][2]:d}")
    print(f"  Top row (tiles 4-7):")
    print(f"    [4] Top-Left   (TL): tile_id={metatile_tiles[4][0]:3d}, flip={metatile_tiles[4][1]:d}, palette={metatile_tiles[4][2]:d}")
    print(f"    [5] Top-Right  (TR): tile_id={metatile_tiles[5][0]:3d}, flip={metatile_tiles[5][1]:d}, palette={metatile_tiles[5][2]:d}")
    print(f"    [6] Bottom-Left (BL): tile_id={metatile_tiles[6][0]:3d}, flip={metatile_tiles[6][1]:d}, palette={metatile_tiles[6][2]:d}")
    print(f"    [7] Bottom-Right(BR): tile_id={metatile_tiles[7][0]:3d}, flip={metatile_tiles[7][1]:d}, palette={metatile_tiles[7][2]:d}")
    
    print(f"\n=== Analysis ===")
    if layer_type_val == 2:  # SPLIT
        print("This is a SPLIT metatile:")
        print("  - Bottom tiles (0-3) should render bottom 8 rows -> Ground layer (Bg3)")
        print("  - Top tiles (4-7) should render top 8 rows -> Overhead layer (Bg1)")
        
        bottom_tiles = metatile_tiles[0:4]
        top_tiles = metatile_tiles[4:8]
        
        print(f"\nBottom tiles (for bottom half):")
        for i, (tile_id, flip, palette) in enumerate(bottom_tiles):
            if tile_id == 0:
                print(f"  [{i}] EMPTY (tile_id=0)")
            else:
                print(f"  [{i}] tile_id={tile_id}, flip={flip}, palette={palette}")
        
        print(f"\nTop tiles (for top half):")
        for i, (tile_id, flip, palette) in enumerate(top_tiles):
            if tile_id == 0:
                print(f"  [{i+4}] EMPTY (tile_id=0)")
            else:
                print(f"  [{i+4}] tile_id={tile_id}, flip={flip}, palette={palette}")
    else:
        print(f"This is a {layer_type_name} metatile (not SPLIT)")

if __name__ == "__main__":
    main()
