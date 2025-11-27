"""
Main converter - converts pokeemerald maps to Tiled format.
"""

import json
from pathlib import Path
from typing import Dict, List, Any, Optional, Tuple, Set
from PIL import Image
from .metatile import (
    extract_metatile_id, extract_elevation,
    convert_metatile_to_tile_layers, MetatileLayerType,
    NUM_TILES_PER_METATILE
)
from .utils import load_json, save_json, sanitize_filename, camel_to_snake, TilesetPathResolver
from .constants import (
    NUM_METATILES_IN_PRIMARY,
    NUM_TILES_IN_PRIMARY_VRAM,
    TILE_SIZE,
    METATILE_SIZE,
    FLIP_HORIZONTAL,
    FLIP_VERTICAL
)
from .logging_config import get_logger
from .tileset_builder import TilesetBuilder
from .metatile_renderer import MetatileRenderer
from .animation_scanner import AnimationScanner
from .map_reader import MapReader
from .metatile_processor import MetatileProcessor
from .validators import (
    validate_map_dimensions, validate_map_id, validate_region,
    validate_tileset_name, validate_non_empty_dict
)

logger = get_logger('converter')


class MapConverter:
    """
    Converts pokeemerald maps to Tiled format.
    
    This class orchestrates the conversion of Pokemon Emerald map data to Tiled JSON format.
    It handles metatile processing, tileset generation, and map structure conversion.
    
    The conversion process:
    1. Reads map data from pokeemerald format (map.json, map.bin)
    2. Processes metatiles into individual tiles
    3. Renders metatiles as 16x16 images
    4. Creates per-map tilesets with only used metatiles
    5. Converts events (warps, triggers, etc.) to Tiled objects
    6. Generates Tiled-compatible JSON output
    
    Attributes:
        input_dir: Path to pokeemerald root directory
        output_dir: Path to output directory for converted maps
        map_reader: MapReader instance for reading map files
        tileset_builder: TilesetBuilder instance for building tilesets
        metatile_renderer: MetatileRenderer instance for rendering metatiles
        metatile_processor: MetatileProcessor instance for processing metatiles
        animation_scanner: AnimationScanner instance for scanning animations
        tile_mappings: Dictionary mapping tileset_name -> old_tile_id -> new_tile_id
    """
    
    def __init__(self, input_dir: str, output_dir: str):
        """
        Initialize MapConverter.
        
        Args:
            input_dir: Path to pokeemerald root directory
            output_dir: Path to output directory for converted maps
        """
        self.input_dir = Path(input_dir)
        self.output_dir = Path(output_dir)
        self.map_reader = MapReader(self.input_dir)
        self.tileset_builder = TilesetBuilder(input_dir)
        self.metatile_renderer = MetatileRenderer(input_dir)
        self.metatile_processor = MetatileProcessor(self.metatile_renderer)
        self.animation_scanner = AnimationScanner(input_dir)
        self.tile_mappings: Dict[str, Dict[int, int]] = {}  # tileset_name -> old_id -> new_id
    
    @staticmethod
    def build_warp_lookup(maps: Dict[str, Dict[str, Any]]) -> Dict[Tuple[str, int], Tuple[int, int, int]]:
        """
        Build a lookup table for warp destinations.
        
        Args:
            maps: Dict mapping map_id -> map_info (from find_map_files)
        
        Returns:
            Dict mapping (map_id, warp_index) -> (x, y, elevation)
        """
        from .utils import load_json
        
        warp_lookup: Dict[Tuple[str, int], Tuple[int, int, int]] = {}
        
        for map_id, map_info in maps.items():
            try:
                map_data = load_json(map_info["map_file"])
                warp_events = map_data.get("warp_events", [])
                
                for warp_index, warp in enumerate(warp_events):
                    x = warp.get("x", 0)
                    y = warp.get("y", 0)
                    elevation = warp.get("elevation", 0)
                    warp_lookup[(map_id, warp_index)] = (x, y, elevation)
            except Exception as e:
                # Skip maps that can't be loaded
                continue
        
        return warp_lookup
        
    def load_metatile_attributes(self, tileset_name: str) -> Dict[int, int]:
        """
        Load metatile attributes to get layer types.
        
        Delegates to MapReader.
        """
        validate_tileset_name(tileset_name)
        return self.map_reader.read_metatile_attributes(tileset_name)
    
    def load_metatiles(self, tileset_name: str) -> List[int]:
        """
        Load metatiles array (list of tile IDs, 8 per metatile).
        
        Delegates to MapReader.
        """
        validate_tileset_name(tileset_name)
        return self.map_reader.read_metatiles(tileset_name)
    
    def load_metatiles_with_attributes(self, tileset_name: str) -> List[Tuple[int, int, int]]:
        """
        Load metatiles with full tile attributes (tile_id, flip_flags, palette_index).
        
        Delegates to MapReader.
        """
        validate_tileset_name(tileset_name)
        return self.map_reader.read_metatiles_with_attributes(tileset_name)
    
    def convert_map(
        self,
        map_data: Dict[str, Any],
        layout_data: Dict[str, Any],
        region: str
    ) -> Optional[Dict[str, Any]]:
        """
        Convert a single map to Tiled format.
        
        Returns:
            Tiled map JSON structure
        """
        # Validate inputs
        validate_region(region)
        validate_non_empty_dict(map_data, "map_data")
        validate_non_empty_dict(layout_data, "layout_data")
        
        layout_id = map_data.get("layout", "")
        if not layout_id or layout_id not in layout_data:
            logger.warning(f"Layout {layout_id} not found in layout_data")
            return None
        
        layout = layout_data[layout_id]
        map_bin = layout.get("map_bin")
        if not map_bin:
            logger.warning(f"No map_bin path for layout {layout_id}")
            return None
        
        map_bin_path = Path(map_bin)
        if not map_bin_path.exists():
            logger.warning(f"map.bin not found at {map_bin_path} (resolved from {map_bin})")
            return None
        
        # Read map data
        width = layout["width"]
        height = layout["height"]
        
        # Validate dimensions
        validate_map_dimensions(width, height)
        
        try:
            map_entries = self.map_reader.read_map_bin(map_bin_path, width, height)
        except Exception as e:
            logger.error(f"Error reading map.bin at {map_bin_path}: {e}", exc_info=True)
            return None
        
        # Load tilesets
        primary_tileset = self._get_tileset_name(layout["primary_tileset"])
        secondary_tileset = self._get_tileset_name(layout["secondary_tileset"])
        
        # Load metatiles with full attributes (tile_id, flip, palette)
        primary_metatiles_with_attrs = self.load_metatiles_with_attributes(primary_tileset)
        secondary_metatiles_with_attrs = self.load_metatiles_with_attributes(secondary_tileset)
        
        # Also load simple metatiles for backward compatibility
        primary_metatiles = self.load_metatiles(primary_tileset)
        secondary_metatiles = self.load_metatiles(secondary_tileset)
        primary_attributes = self.load_metatile_attributes(primary_tileset)
        secondary_attributes = self.load_metatile_attributes(secondary_tileset)
        
        if not primary_metatiles or len(primary_metatiles) == 0:
            logger.error(f"Could not load metatiles for {primary_tileset}")
            return None
        
        # Convert metatiles to tile layers
        tile_width = width * 2  # Each metatile is 2x2 tiles
        tile_height = height * 2
        
        # Three BG layers
        bg3_data = [0] * (tile_width * tile_height)  # Bottom layer
        bg2_data = [0] * (tile_width * tile_height)   # Middle layer
        bg1_data = [0] * (tile_width * tile_height)  # Top layer
        
        # Track which tileset each tile belongs to for remapping
        # Format: (tile_id, tileset_name) tuples, or just tile_id if we can infer tileset
        # We'll store this as metadata in the map
        bg3_tilesets = [None] * (tile_width * tile_height)
        bg2_tilesets = [None] * (tile_width * tile_height)
        bg1_tilesets = [None] * (tile_width * tile_height)
        
        # Track used tiles for tileset building
        used_primary_tiles = set()
        used_secondary_tiles = set()
        used_primary_tiles_with_palettes = set()  # (tile_id, palette_index)
        used_secondary_tiles_with_palettes = set()  # (tile_id, palette_index)
        
        # Store palette info for each tile position (for remapping)
        bg3_palettes = [0] * (tile_width * tile_height)
        bg2_palettes = [0] * (tile_width * tile_height)
        bg1_palettes = [0] * (tile_width * tile_height)
        
        # Process each metatile
        for y in range(height):
            for x in range(width):
                entry = map_entries[y][x]
                metatile_id = extract_metatile_id(entry)
                elevation = extract_elevation(entry)
                
                # Determine which tileset
                if metatile_id < NUM_METATILES_IN_PRIMARY:
                    metatiles = primary_metatiles
                    attributes = primary_attributes
                    tileset_name = primary_tileset
                else:
                    metatiles = secondary_metatiles
                    attributes = secondary_attributes
                    tileset_name = secondary_tileset
                    metatile_id -= NUM_METATILES_IN_PRIMARY
                
                # Get layer type
                layer_type_val = attributes.get(metatile_id, 0)
                layer_type = MetatileLayerType(layer_type_val)
                
                # Convert metatile to tile layers with palette info
                # Use metatiles with attributes if available, otherwise fall back to simple metatiles
                metatiles_with_attrs = None
                if tileset_name == primary_tileset and primary_metatiles_with_attrs and len(primary_metatiles_with_attrs) > 0:
                    metatiles_with_attrs = primary_metatiles_with_attrs
                elif tileset_name == secondary_tileset and secondary_metatiles_with_attrs and len(secondary_metatiles_with_attrs) > 0:
                    metatiles_with_attrs = secondary_metatiles_with_attrs
                
                if metatiles_with_attrs and len(metatiles_with_attrs) > 0:
                    from .metatile import convert_metatile_to_tile_layers_with_attrs
                    bg3_tiles, bg2_tiles, bg1_tiles = convert_metatile_to_tile_layers_with_attrs(
                        metatile_id, metatiles_with_attrs, layer_type, x, y, width
                    )
                else:
                    # Fallback to simple conversion (no palette info)
                    # This happens if metatiles_with_attrs is None or empty
                    bg3_tiles, bg2_tiles, bg1_tiles = convert_metatile_to_tile_layers(
                        metatile_id, metatiles, layer_type, x, y, width
                    )
                    # Convert to (tile_id, palette=0) format
                    bg3_tiles = {(k[0], k[1]): (v, 0) for k, v in bg3_tiles.items()}
                    bg2_tiles = {(k[0], k[1]): (v, 0) for k, v in bg2_tiles.items()}
                    bg1_tiles = {(k[0], k[1]): (v, 0) for k, v in bg1_tiles.items()}
                
                # Helper function to determine which tileset a tile ID belongs to
                # Use processor method
                def get_tile_tileset(tile_id: int, current_tileset_name: str) -> Tuple[str, int]:
                    """Returns (tileset_name, adjusted_tile_id)"""
                    return self.metatile_processor.determine_tileset_for_tile(
                        tile_id, current_tileset_name, primary_tileset, secondary_tileset
                    )
                
                # Apply tiles to layer data arrays
                # Helper to add tile to correct used_tiles set (with palette)
                def add_used_tile_with_palette(tile_id: int, palette_index: int, tileset_name_for_tile: str):
                    """Add (tile_id, palette) to the correct used_tiles set based on which tileset it belongs to."""
                    # Explicit routing logic to prevent bugs:
                    # - If from primary metatile: always goes to primary
                    # - If from secondary metatile:
                    #   - tile_id < NUM_TILES_IN_PRIMARY_VRAM: goes to PRIMARY (references primary tileset)
                    #   - tile_id >= NUM_TILES_IN_PRIMARY_VRAM: goes to SECONDARY (offset by NUM_TILES_IN_PRIMARY_VRAM)
                    if tileset_name_for_tile == primary_tileset:
                        # Primary metatile: all tiles go to primary
                        used_primary_tiles.add(tile_id)
                        used_primary_tiles_with_palettes.add((tile_id, palette_index))
                    else:
                        # Secondary metatile: route based on tile_id
                        if tile_id < NUM_TILES_IN_PRIMARY_VRAM:
                            # Tile IDs 0-511 reference PRIMARY tileset
                            used_primary_tiles.add(tile_id)
                            used_primary_tiles_with_palettes.add((tile_id, palette_index))
                        else:
                            # Tile IDs 512+ reference SECONDARY tileset (offset by NUM_TILES_IN_PRIMARY_VRAM)
                            adjusted_id = tile_id - NUM_TILES_IN_PRIMARY_VRAM
                            used_secondary_tiles.add(adjusted_id)
                            used_secondary_tiles_with_palettes.add((adjusted_id, palette_index))
                
                # Process bg3_tiles (now contains (tile_id, palette) tuples)
                for (tx, ty), tile_data in bg3_tiles.items():
                    if isinstance(tile_data, tuple):
                        tile_id, palette_index = tile_data
                    else:
                        # Fallback for old format
                        tile_id = tile_data
                        palette_index = 0
                    
                    if 0 <= tx < tile_width and 0 <= ty < tile_height:
                        idx = ty * tile_width + tx
                        actual_tileset, adjusted_tile_id = get_tile_tileset(tile_id, tileset_name)
                        bg3_data[idx] = adjusted_tile_id  # Store adjusted ID for map
                        bg3_tilesets[idx] = actual_tileset  # Store tileset name for remapping
                        bg3_palettes[idx] = palette_index  # Store palette index
                        add_used_tile_with_palette(tile_id, palette_index, tileset_name)
                
                # Process bg2_tiles
                for (tx, ty), tile_data in bg2_tiles.items():
                    if isinstance(tile_data, tuple):
                        tile_id, palette_index = tile_data
                    else:
                        tile_id = tile_data
                        palette_index = 0
                    
                    if 0 <= tx < tile_width and 0 <= ty < tile_height:
                        idx = ty * tile_width + tx
                        actual_tileset, adjusted_tile_id = get_tile_tileset(tile_id, tileset_name)
                        bg2_data[idx] = adjusted_tile_id
                        bg2_tilesets[idx] = actual_tileset
                        bg2_palettes[idx] = palette_index
                        add_used_tile_with_palette(tile_id, palette_index, tileset_name)
                
                # Process bg1_tiles
                for (tx, ty), tile_data in bg1_tiles.items():
                    if isinstance(tile_data, tuple):
                        tile_id, palette_index = tile_data
                    else:
                        tile_id = tile_data
                        palette_index = 0
                    
                    if 0 <= tx < tile_width and 0 <= ty < tile_height:
                        idx = ty * tile_width + tx
                        actual_tileset, adjusted_tile_id = get_tile_tileset(tile_id, tileset_name)
                        bg1_data[idx] = adjusted_tile_id
                        bg1_tilesets[idx] = actual_tileset
                        bg1_palettes[idx] = palette_index
                        add_used_tile_with_palette(tile_id, palette_index, tileset_name)
        
        # Get maximum tile IDs for each tileset to validate tile IDs
        primary_max_tile = self._get_max_tile_id(primary_tileset)
        secondary_max_tile = self._get_max_tile_id(secondary_tileset)
        
        # Debug: Check for invalid tile IDs and trace their origin
        if primary_max_tile is not None:
            invalid_primary = {tid for tid in used_primary_tiles if tid > primary_max_tile}
            if invalid_primary:
                # This shouldn't happen - primary tiles should always be valid
                logger.error(f"Found {len(invalid_primary)} invalid tile IDs in {primary_tileset}: {sorted(list(invalid_primary))[:10]}... (max: {primary_max_tile})")
                logger.error("This indicates a bug in tile ID routing logic!")
            used_primary_tiles = {tid for tid in used_primary_tiles if 0 <= tid <= primary_max_tile}
        
        if secondary_max_tile is not None:
            invalid_secondary = {tid for tid in used_secondary_tiles if tid > secondary_max_tile}
            if invalid_secondary:
                # Check if these are actually primary tileset tiles that got misrouted
                # Tile IDs < 512 in secondary metatiles should go to primary, not secondary
                misrouted = {tid for tid in invalid_secondary if tid < 512}
                if misrouted:
                    # This is the bug - tile IDs < 512 should never be in secondary_tiles
                    # They should have been routed to primary by get_tile_tileset
                    logger.warning(f"Moving {len(misrouted)} misrouted tile IDs from {secondary_tileset} to {primary_tileset}: {sorted(list(misrouted))[:10]}...")
                    logger.warning("These tile IDs (< 512) from secondary metatiles should reference primary tileset!")
                    # Move them to primary
                    used_primary_tiles.update(misrouted)
                    used_secondary_tiles = {tid for tid in used_secondary_tiles if tid not in misrouted}
                
                # Check remaining invalid IDs (these are >= 512 but out of bounds for secondary)
                remaining_invalid = {tid for tid in used_secondary_tiles if tid > secondary_max_tile}
                if remaining_invalid:
                    logger.warning(f"Filtering {len(remaining_invalid)} invalid tile IDs from {secondary_tileset}: {sorted(list(remaining_invalid))[:10]}... (max: {secondary_max_tile})")
                used_secondary_tiles = {tid for tid in used_secondary_tiles if 0 <= tid <= secondary_max_tile}
        
        # Record used tiles (for backward compatibility)
        self.tileset_builder.add_tiles(primary_tileset, list(used_primary_tiles))
        self.tileset_builder.add_tiles(secondary_tileset, list(used_secondary_tiles))
        
        # Record used tiles with palettes (for palette-aware tileset building)
        if used_primary_tiles_with_palettes:
            unique_palettes = set(p[1] for p in used_primary_tiles_with_palettes)
            logger.debug(f"Recording {len(used_primary_tiles_with_palettes)} tiles with palettes for {primary_tileset}, unique palettes: {sorted(unique_palettes)}")
        if used_secondary_tiles_with_palettes:
            unique_palettes = set(p[1] for p in used_secondary_tiles_with_palettes)
            logger.debug(f"Recording {len(used_secondary_tiles_with_palettes)} tiles with palettes for {secondary_tileset}, unique palettes: {sorted(unique_palettes)}")
        
        self.tileset_builder.add_tiles_with_palettes(primary_tileset, list(used_primary_tiles_with_palettes))
        self.tileset_builder.add_tiles_with_palettes(secondary_tileset, list(used_secondary_tiles_with_palettes))
        
        # Create Tiled map structure
        tiled_map = {
            "compressionlevel": -1,
            "height": tile_height,
            "infinite": False,
            "layers": [],
            "nextlayerid": 1,
            "nextobjectid": 1,
            "orientation": "orthogonal",
            "renderorder": "right-down",
            "tiledversion": "1.11.2",
            "tileheight": 8,  # Pokemon uses 8x8 tiles
            "tilewidth": 8,
            "type": "map",
            "version": "1.11",
            "width": tile_width,
            "properties": []
        }
        
        # Add properties from map_data
        if "name" in map_data:
            tiled_map["properties"].append({
                "name": "displayName",
                "type": "string",
                "value": map_data["name"]
            })
        
        if "region_map_section" in map_data:
            tiled_map["properties"].append({
                "name": "region",
                "type": "string",
                "value": region
            })
        
        # Add other properties...
        for key in ["music", "weather", "map_type", "show_map_name", "can_fly"]:
            if key in map_data:
                value = map_data[key]
                prop_type = "bool" if isinstance(value, bool) else "string"
                tiled_map["properties"].append({
                    "name": key,
                    "type": prop_type,
                    "value": value
                })
        
        # Add BG layers (in reverse order: Bg3, Bg2, Bg1)
        # Store tileset and palette info as custom properties for remapping
        layer_id = 1
        for layer_name, layer_data, layer_tilesets, layer_palettes in [
            ("Ground", bg3_data, bg3_tilesets, bg3_palettes),
            ("Objects", bg2_data, bg2_tilesets, bg2_palettes),
            ("Overhead", bg1_data, bg1_tilesets, bg1_palettes)
        ]:
            # Store tileset and palette info as properties (we'll use this during remapping)
            layer_obj = {
                "data": layer_data,
                "height": tile_height,
                "id": layer_id,
                "name": layer_name,
                "opacity": 1,
                "type": "tilelayer",
                "visible": True,
                "width": tile_width,
                "x": 0,
                "y": 0,
                "properties": [
                    {
                        "name": "_tileset_info",
                        "type": "string",
                        "value": json.dumps(layer_tilesets)  # Store as JSON string
                    },
                    {
                        "name": "_palette_info",
                        "type": "string",
                        "value": json.dumps(layer_palettes)  # Store palette indices
                    }
                ]
            }
            tiled_map["layers"].append(layer_obj)
            layer_id += 1
        
        # Add tilesets
        # Note: We'll need to update tile IDs after tilesets are built
        # For now, use relative paths
        tilesets = []
        first_gid = 1
        
        # Primary tileset
        tilesets.append({
            "firstgid": first_gid,
            "source": self.tileset_builder.get_tileset_path(primary_tileset, region)
        })
        
        # Secondary tileset (if different and used)
        if secondary_tileset != primary_tileset and used_secondary_tiles:
            # Calculate firstgid for secondary (after primary)
            primary_tilecount = len(used_primary_tiles) if used_primary_tiles else 1
            first_gid += primary_tilecount
            tilesets.append({
                "firstgid": first_gid,
                "source": self.tileset_builder.get_tileset_path(secondary_tileset, region)
            })
        
        tiled_map["tilesets"] = tilesets
        
        # Add object layer for NPCs, warps, etc.
        objects_layer = {
            "id": layer_id,
            "name": "Objects",
            "objects": [],
            "opacity": 1,
            "type": "objectgroup",
            "visible": True,
            "x": 0,
            "y": 0
        }
        
        # Convert object events
        object_id = 1
        for obj in map_data.get("object_events", []):
            objects_layer["objects"].append({
                "id": object_id,
                "name": f"NPC_{object_id}",
                "type": "npc",
                "x": obj.get("x", 0) * 8,  # Convert to pixels
                "y": obj.get("y", 0) * 8,
                "width": 16,
                "height": 16,
                "properties": [
                    {"name": "elevation", "type": "int", "value": obj.get("elevation", 3)},
                    {"name": "graphics_id", "type": "string", "value": obj.get("graphics_id", "")},
                ]
            })
            object_id += 1
        
        # Convert warp events
        for warp in map_data.get("warp_events", []):
            objects_layer["objects"].append({
                "id": object_id,
                "name": f"Warp_{object_id}",
                "type": "warp_event",
                "x": warp.get("x", 0) * 8,
                "y": warp.get("y", 0) * 8,
                "width": 8,
                "height": 8,
                "properties": [
                    {"name": "elevation", "type": "int", "value": warp.get("elevation", 0)},
                    {"name": "dest_map", "type": "string", "value": warp.get("dest_map", "")},
                    {"name": "dest_warp_id", "type": "string", "value": str(warp.get("dest_warp_id", "0"))},
                ]
            })
            object_id += 1
        
        if objects_layer["objects"]:
            tiled_map["layers"].append(objects_layer)
            tiled_map["nextobjectid"] = object_id
        
        return tiled_map
    
    def remap_map_tiles(self, map_path: Path, tile_mappings: Dict[str, Dict[Tuple[int, int], int]]) -> bool:
        """
        Remap tile IDs in a map file using the provided tile mappings.
        
        Args:
            map_path: Path to the map JSON file
            tile_mappings: Dict mapping tileset_name -> {(old_tile_id, palette_index): new_tile_id}
        
        Returns:
            True if remapping was successful, False otherwise
        """
        try:
            map_data = load_json(str(map_path))
            if not map_data:
                return False
            
            # Get tileset information from the map
            tilesets = map_data.get("tilesets", [])
            if not tilesets:
                return False
            
            # Build a mapping of tileset names to their firstgid and mappings
            # Need to match tileset names case-insensitively
            tileset_info = {}
            for tileset in tilesets:
                source = tileset.get("source", "")
                # Extract tileset name from source path (e.g., "../../Tilesets/hoenn/general.json" -> "general")
                tileset_name_from_path = Path(source).stem.lower()
                firstgid = tileset.get("firstgid", 1)
                
                # Try to find matching tileset in mappings (case-insensitive)
                mapping = None
                for mapping_key in tile_mappings.keys():
                    if mapping_key.lower() == tileset_name_from_path:
                        mapping = tile_mappings[mapping_key]
                        tileset_info[mapping_key] = {
                            "firstgid": firstgid,
                            "mapping": mapping
                        }
                        break
            
            if not tileset_info:
                # No matching tilesets found - this shouldn't happen if maps were converted correctly
                # But don't fail silently, just skip remapping for this map
                return False
            
            # Remap all tile layers
            for layer in map_data.get("layers", []):
                if layer.get("type") != "tilelayer":
                    continue
                
                data = layer.get("data", [])
                if not data:
                    continue
                
                # Get tileset and palette info for this layer (stored during conversion)
                tileset_info_str = None
                palette_info_str = None
                for prop in layer.get("properties", []):
                    if prop.get("name") == "_tileset_info":
                        tileset_info_str = prop.get("value")
                    elif prop.get("name") == "_palette_info":
                        palette_info_str = prop.get("value")
                
                if tileset_info_str:
                    # Parse tileset info
                    layer_tilesets = json.loads(tileset_info_str)
                else:
                    # Fallback: try to infer from tile IDs
                    layer_tilesets = [None] * len(data)
                
                if palette_info_str:
                    # Parse palette info
                    layer_palettes = json.loads(palette_info_str)
                else:
                    # Fallback: assume palette 0
                    layer_palettes = [0] * len(data)
                
                # Remap each tile ID using (tile_id, palette) key
                for i in range(len(data)):
                    old_tile_id = data[i]
                    if old_tile_id == 0:
                        # Empty tile, keep as 0
                        continue
                    
                    # Get which tileset and palette this tile belongs to
                    tileset_name = layer_tilesets[i] if i < len(layer_tilesets) else None
                    palette_index = layer_palettes[i] if i < len(layer_palettes) else 0
                    
                    if tileset_name:
                        # Match case-insensitively
                        matching_tileset_key = None
                        for ts_key in tileset_info.keys():
                            if ts_key.lower() == tileset_name.lower():
                                matching_tileset_key = ts_key
                                break
                        
                        if matching_tileset_key and matching_tileset_key in tileset_info:
                            # We know which tileset, use its mapping
                            info = tileset_info[matching_tileset_key]
                            mapping = info["mapping"]  # Now Dict[Tuple[int, int], int]
                            firstgid = info["firstgid"]
                            
                            # Use (tile_id, palette) as key
                            tile_key = (old_tile_id, palette_index)
                            if tile_key in mapping:
                                new_tile_id = mapping[tile_key]
                                # Convert to GID: firstgid + new_tile_id - 1
                                # (Tiled uses 1-based tile IDs, so GID = firstgid + tile_index)
                                new_gid = firstgid + new_tile_id - 1
                                data[i] = new_gid
                            else:
                                # Fallback: try without palette (for backward compatibility)
                                if isinstance(mapping, dict) and old_tile_id in mapping:
                                    # Old format mapping (just tile_id)
                                    new_tile_id = mapping[old_tile_id]
                                    new_gid = firstgid + new_tile_id - 1
                                    data[i] = new_gid
                    else:
                        # Try to find which tileset by checking mappings
                        for ts_name, info in tileset_info.items():
                            mapping = info["mapping"]
                            tile_key = (old_tile_id, palette_index)
                            if tile_key in mapping:
                                new_tile_id = mapping[tile_key]
                                firstgid = info["firstgid"]
                                new_gid = firstgid + new_tile_id - 1
                                data[i] = new_gid
                                break
                            elif isinstance(mapping, dict) and old_tile_id in mapping:
                                # Fallback: old format
                                new_tile_id = mapping[old_tile_id]
                                firstgid = info["firstgid"]
                                new_gid = firstgid + new_tile_id - 1
                                data[i] = new_gid
                                break
                
                # Remove the _tileset_info and _palette_info properties after remapping
                layer["properties"] = [p for p in layer.get("properties", []) 
                                     if p.get("name") not in ("_tileset_info", "_palette_info")]
            
            # Save the remapped map
            save_json(map_data, str(map_path))
            return True
            
        except Exception as e:
            # Only print first few errors to avoid spam
            if not hasattr(remap_map_tiles, '_error_count'):
                remap_map_tiles._error_count = 0
            remap_map_tiles._error_count += 1
            if remap_map_tiles._error_count <= 3:
                logger.error(f"Error remapping {map_path.name}: {e}", exc_info=True)
            return False
    
    def _get_tileset_name(self, tileset_id: str) -> str:
        """Extract tileset name from ID like 'gTileset_General' -> 'General'."""
        if tileset_id.startswith("gTileset_"):
            return tileset_id.replace("gTileset_", "")
        return tileset_id
    
    def _get_max_tile_id(self, tileset_name: str) -> Optional[int]:
        """
        Get the maximum valid tile ID for a tileset based on its image dimensions.
        
        Returns:
            Maximum tile ID (0-based, so max_tile_id = total_tiles - 1), or None if image not found
        """
        from PIL import Image
        
        # Try to load the tileset graphics to get dimensions
        category, tileset_dir = self._get_tileset_path(tileset_name)
        tiles_path = tileset_dir / "tiles.png"
        
        if tiles_path.exists():
            try:
                img = Image.open(tiles_path)
                # Convert if needed
                if img.mode in ('P', '1', 'L'):
                    img = img.convert('RGBA')
                
                # Calculate total tiles (8x8 pixel tiles)
                tile_size = 8
                tiles_per_row = img.width // tile_size
                tiles_per_col = img.height // tile_size
                total_tiles = tiles_per_row * tiles_per_col
                
                # Return max tile ID (0-based, so max = total - 1)
                return total_tiles - 1 if total_tiles > 0 else None
            except Exception:
                pass
        
        return None
    
    def _get_tileset_path(self, tileset_name: str) -> Tuple[str, Path]:
        """
        Get tileset directory path and determine if it's primary or secondary.
        
        Returns:
            (category, path) where category is 'primary' or 'secondary'
        """
        resolver = TilesetPathResolver(self.input_dir)
        result = resolver.find_tileset_path(tileset_name)
        
        if result:
            return result
        
        # Return primary as default (will fail later if not found)
        name_variants = [
            camel_to_snake(tileset_name),
            tileset_name.lower(),
            tileset_name.replace("_", "").lower(),
        ]
        return ("primary", self.input_dir / "data" / "tilesets" / "primary" / name_variants[0])
    
    def save_map(self, map_id: str, tiled_map: Dict[str, Any], region: str):
        """
        Save converted map to output directory.
        
        Saves the converted Tiled map JSON to the appropriate location in the output
        directory structure: Data/Maps/{region}/{map_name}.json
        
        Args:
            map_id: Unique identifier for the map (e.g., "MAP_LITTLEROOT_TOWN")
            tiled_map: Complete Tiled map JSON structure
            region: Region name for organizing output (e.g., "hoenn")
        
        Raises:
            ValueError: If map_id or region is invalid
            OSError: If the output directory cannot be created
        """
        # Validate inputs
        validate_map_id(map_id)
        validate_region(region)
        validate_non_empty_dict(tiled_map, "tiled_map")
        
        map_name = sanitize_filename(map_id.replace("MAP_", "").lower())
        output_path = self.output_dir / "Data" / "Maps" / region / f"{map_name}.json"
        output_path.parent.mkdir(parents=True, exist_ok=True)
        save_json(tiled_map, str(output_path))
    
    def _validate_layout(self, map_data: Dict[str, Any], layout_data: Dict[str, Any]) -> Optional[Dict[str, Any]]:
        """Validate and retrieve layout data from map_data."""
        layout_id = map_data.get("layout", "")
        if not layout_id or layout_id not in layout_data:
            logger.warning(f"Layout {layout_id} not found in layout_data")
            return None
        
        layout = layout_data[layout_id]
        map_bin = layout.get("map_bin")
        if not map_bin:
            logger.warning(f"No map_bin path for layout {layout_id}")
            return None
        
        map_bin_path = Path(map_bin)
        if not map_bin_path.exists():
            logger.warning(f"map.bin not found at {map_bin_path}")
            return None
        
        return layout
    
    def _read_and_validate_map_data(self, layout: Dict[str, Any]) -> Optional[Tuple[List[List[int]], int, int]]:
        """Read map.bin and validate dimensions match file size."""
        map_bin = layout.get("map_bin")
        map_bin_path = Path(map_bin)
        width = layout["width"]
        height = layout["height"]
        
        # Validate dimensions match file size
        try:
            map_entries = self.map_reader.read_map_bin(map_bin_path, width, height)
            return (map_entries, width, height)
        except ValueError as e:
            # If dimensions don't match, try to infer from file size
            import os
            file_size = os.path.getsize(map_bin_path)
            expected_entries = width * height
            actual_entries = file_size // 2  # Each entry is 2 bytes (u16)
            
            if actual_entries != expected_entries:
                # Try to calculate correct dimensions from file size
                half_width = width // 2
                half_height = height // 2
                if half_width > 0 and half_height > 0 and actual_entries == half_width * half_height:
                    logger.warning(f"Layout dimensions ({width}x{height}) don't match map.bin size")
                    logger.warning(f"File has {actual_entries} entries, suggesting dimensions: {half_width}x{half_height}")
                    logger.info(f"Using file-based dimensions: {half_width}x{half_height}")
                    width = half_width
                    height = half_height
                    map_entries = self.map_reader.read_map_bin(map_bin_path, width, height)
                    return (map_entries, width, height)
                else:
                    logger.error(f"Cannot determine correct dimensions for {map_bin_path}")
                    return None
            else:
                raise
        except Exception as e:
            logger.error(f"Error reading map.bin at {map_bin_path}: {e}", exc_info=True)
            return None
    
    def _load_tileset_data(self, layout: Dict[str, Any]) -> Optional[Dict[str, Any]]:
        """
        Load metatiles and attributes for primary and secondary tilesets.
        
        This method loads the metatile data and attributes for both the primary and secondary
        tilesets specified in the layout. The metatiles are loaded with full attributes
        (tile_id, flip_flags, palette_index) for proper rendering.
        
        Args:
            layout: Layout dictionary containing primary_tileset and secondary_tileset IDs
        
        Returns:
            Dictionary containing:
            - primary_tileset: Name of primary tileset
            - secondary_tileset: Name of secondary tileset
            - primary_metatiles_with_attrs: List of (tile_id, flip_flags, palette_index) tuples
            - secondary_metatiles_with_attrs: List of (tile_id, flip_flags, palette_index) tuples
            - primary_attributes: Dict mapping metatile_id -> layer_type
            - secondary_attributes: Dict mapping metatile_id -> layer_type
            Or None if loading fails
        """
        primary_tileset = self._get_tileset_name(layout["primary_tileset"])
        secondary_tileset = self._get_tileset_name(layout["secondary_tileset"])
        
        # Load metatiles with attributes
        primary_metatiles_with_attrs = self.load_metatiles_with_attributes(primary_tileset)
        secondary_metatiles_with_attrs = self.load_metatiles_with_attributes(secondary_tileset)
        primary_attributes = self.load_metatile_attributes(primary_tileset)
        secondary_attributes = self.load_metatile_attributes(secondary_tileset)
        
        if not primary_metatiles_with_attrs:
            logger.error(f"Could not load metatiles for {primary_tileset}")
            return None
        
        return {
            "primary_tileset": primary_tileset,
            "secondary_tileset": secondary_tileset,
            "primary_metatiles_with_attrs": primary_metatiles_with_attrs,
            "secondary_metatiles_with_attrs": secondary_metatiles_with_attrs,
            "primary_attributes": primary_attributes,
            "secondary_attributes": secondary_attributes
        }
    
    def _process_metatiles(
        self,
        map_entries: List[List[int]],
        width: int,
        height: int,
        tileset_data: Dict[str, Any]
    ) -> Dict[str, Any]:
        """
        Process all metatiles in the map and render them.
        
        Returns a dictionary containing:
        - used_metatiles: Dict mapping (metatile_id, tileset, layer_type) -> (bottom_img, top_img)
        - metatile_to_gid: Dict mapping (metatile_id, tileset, layer_type, is_top) -> GID
        - tile_id_to_gids: Dict mapping (tile_id, tileset) -> list of (layer_gid, metatile_key, tile_position)
        - metatile_composition: Dict mapping metatile_key -> metatile_tiles
        - image_to_gid: Dict mapping image_bytes -> GID
        - next_gid: int for next available GID
        """
        primary_tileset = tileset_data["primary_tileset"]
        secondary_tileset = tileset_data["secondary_tileset"]
        primary_metatiles_with_attrs = tileset_data["primary_metatiles_with_attrs"]
        secondary_metatiles_with_attrs = tileset_data["secondary_metatiles_with_attrs"]
        primary_attributes = tileset_data["primary_attributes"]
        secondary_attributes = tileset_data["secondary_attributes"]
        
        # Initialize data structures
        used_metatiles: Dict[Tuple[int, str, int], Tuple[Image.Image, Image.Image]] = {}
        metatile_to_gid: Dict[Tuple[int, str, int, bool], int] = {}
        tile_id_to_gids: Dict[Tuple[int, str], List[Tuple[int, Tuple[int, str, int], int]]] = {}
        metatile_composition: Dict[Tuple[int, str, int], List] = {}
        image_to_gid: Dict[bytes, int] = {}
        next_gid = 1
        
        # Process each metatile in the map
        for y in range(height):
            for x in range(width):
                entry = map_entries[y][x]
                metatile_id = extract_metatile_id(entry)
                
                # Determine which tileset using processor
                tileset_name, actual_metatile_id = self.metatile_processor.determine_tileset_for_metatile(
                    metatile_id, primary_tileset, secondary_tileset
                )
                
                # Get appropriate metatiles and attributes
                if tileset_name == primary_tileset:
                    metatiles_with_attrs = primary_metatiles_with_attrs
                    attributes = primary_attributes
                else:
                    metatiles_with_attrs = secondary_metatiles_with_attrs
                    attributes = secondary_attributes
                
                # Process single metatile using processor
                result = self.metatile_processor.process_single_metatile(
                    actual_metatile_id,
                    tileset_name,
                    metatiles_with_attrs,
                    attributes,
                    primary_tileset,
                    secondary_tileset,
                    used_metatiles,
                    image_to_gid,
                    next_gid
                )
                
                metatile_images, single_metatile_to_gid, single_tile_id_to_gids, metatile_tiles, image_to_gid, next_gid = result
                
                # Merge results
                metatile_to_gid.update(single_metatile_to_gid)
                for tile_key, gid_list in single_tile_id_to_gids.items():
                    if tile_key not in tile_id_to_gids:
                        tile_id_to_gids[tile_key] = []
                    tile_id_to_gids[tile_key].extend(gid_list)
                
                # Store metatile composition if we have tiles
                if metatile_tiles:
                    layer_type_val = attributes.get(actual_metatile_id, 0)
                    key = (actual_metatile_id, tileset_name, layer_type_val)
                    metatile_composition[key] = metatile_tiles
        
        return {
            "used_metatiles": used_metatiles,
            "metatile_to_gid": metatile_to_gid,
            "tile_id_to_gids": tile_id_to_gids,
            "metatile_composition": metatile_composition,
            "image_to_gid": image_to_gid,
            "next_gid": next_gid
        }
    
    def _process_border_metatiles(
        self,
        layout: Dict[str, Any],
        tileset_data: Dict[str, Any],
        used_metatiles: Dict[Tuple[int, str, int], Tuple[Image.Image, Image.Image]],
        metatile_to_gid: Dict[Tuple[int, str, int, bool], int],
        image_to_gid: Dict[bytes, int],
        next_gid: int
    ) -> Tuple[Dict[str, int], int]:
        """
        Process border metatiles and convert to GIDs.
        
        Returns:
            Tuple of (border_gids dict, updated next_gid)
        """
        border_gids = {}
        border_bin = layout.get("border_bin")
        if not border_bin:
            return (border_gids, next_gid)
        
        border_bin_path = Path(border_bin)
        if not border_bin_path.exists():
            return (border_gids, next_gid)
        
        primary_tileset = tileset_data["primary_tileset"]
        secondary_tileset = tileset_data["secondary_tileset"]
        primary_metatiles_with_attrs = tileset_data["primary_metatiles_with_attrs"]
        secondary_metatiles_with_attrs = tileset_data["secondary_metatiles_with_attrs"]
        primary_attributes = tileset_data["primary_attributes"]
        secondary_attributes = tileset_data["secondary_attributes"]
        
        try:
            import struct
            with open(border_bin_path, 'rb') as f:
                border_data = f.read()
            
            # Border.bin contains 4 u16 values: [top_left, top_right, bottom_left, bottom_right]
            if len(border_data) >= 8:  # 4 * 2 bytes
                border_corners = ["top_left", "top_right", "bottom_left", "bottom_right"]
                for corner_idx, corner_name in enumerate(border_corners):
                    i = corner_idx * 2
                    if i + 1 < len(border_data):
                        border_entry = struct.unpack('<H', border_data[i:i+2])[0]
                        from .constants import METATILE_ID_MASK
                        border_metatile_id = border_entry & METATILE_ID_MASK
                        
                        # Determine which tileset using processor
                        border_tileset_name, border_actual_id = self.metatile_processor.determine_tileset_for_metatile(
                            border_metatile_id, primary_tileset, secondary_tileset
                        )
                        
                        # Get appropriate metatiles and attributes
                        if border_tileset_name == primary_tileset:
                            border_metatiles_with_attrs = primary_metatiles_with_attrs
                            border_attributes = primary_attributes
                        else:
                            border_metatiles_with_attrs = secondary_metatiles_with_attrs
                            border_attributes = secondary_attributes
                        
                        # Get layer type
                        border_layer_type_val = border_attributes.get(border_actual_id, 0)
                        border_layer_type = MetatileLayerType(border_layer_type_val)
                        
                        # Check if this border metatile is already processed
                        border_key = (border_actual_id, border_tileset_name, border_layer_type_val)
                        if border_key not in used_metatiles:
                            # Process border metatile
                            start_idx = border_actual_id * NUM_TILES_PER_METATILE
                            if start_idx < len(border_metatiles_with_attrs):
                                border_metatile_tiles = border_metatiles_with_attrs[start_idx:start_idx + NUM_TILES_PER_METATILE]
                                
                                if len(border_metatile_tiles) == NUM_TILES_PER_METATILE:
                                    # Render border metatile
                                    border_bottom_img, border_top_img = self.metatile_renderer.render_metatile(
                                        border_metatile_tiles,
                                        primary_tileset,
                                        secondary_tileset,
                                        border_layer_type
                                    )
                                    if border_bottom_img is None:
                                        border_bottom_img = Image.new('RGBA', (METATILE_SIZE, METATILE_SIZE), (0, 0, 0, 0))
                                    if border_top_img is None:
                                        border_top_img = Image.new('RGBA', (METATILE_SIZE, METATILE_SIZE), (0, 0, 0, 0))
                                    used_metatiles[border_key] = (border_bottom_img, border_top_img)
                                    
                                    # Assign GIDs with deduplication
                                    border_bottom_bytes = border_bottom_img.tobytes()
                                    border_top_bytes = border_top_img.tobytes()
                                    
                                    if border_bottom_bytes in image_to_gid:
                                        border_bottom_gid = image_to_gid[border_bottom_bytes]
                                    else:
                                        border_bottom_gid = next_gid
                                        image_to_gid[border_bottom_bytes] = border_bottom_gid
                                        next_gid += 1
                                    
                                    if border_top_bytes in image_to_gid:
                                        border_top_gid = image_to_gid[border_top_bytes]
                                    else:
                                        border_top_gid = next_gid
                                        image_to_gid[border_top_bytes] = border_top_gid
                                        next_gid += 1
                                    
                                    metatile_to_gid[(border_actual_id, border_tileset_name, border_layer_type_val, False)] = border_bottom_gid
                                    metatile_to_gid[(border_actual_id, border_tileset_name, border_layer_type_val, True)] = border_top_gid
                        
                        # Get GIDs for both bottom and top layers of border metatile
                        border_bottom_gid = metatile_to_gid.get((border_actual_id, border_tileset_name, border_layer_type_val, False), 0)
                        border_top_gid = metatile_to_gid.get((border_actual_id, border_tileset_name, border_layer_type_val, True), 0)
                        border_gids[corner_name] = border_bottom_gid
                        border_gids[f"{corner_name}_top"] = border_top_gid
        except Exception as e:
            logger.warning(f"Error reading border.bin at {border_bin_path}: {e}")
        
        return (border_gids, next_gid)
    
    def _build_map_layers(
        self,
        map_entries: List[List[int]],
        width: int,
        height: int,
        metatile_to_gid: Dict[Tuple[int, str, int, bool], int],
        tileset_data: Dict[str, Any],
        border_gids: Dict[str, int]
    ) -> Dict[str, Any]:
        """
        Build map layer data by assigning GIDs to layers based on metatile layer types.
        
        Returns a dictionary containing:
        - layer_data_bg3: List of GIDs for BG3 layer
        - layer_data_bg2: List of GIDs for BG2 layer
        - layer_data_bg1: List of GIDs for BG1 layer
        - used_gids: Set of GIDs actually used in the map
        """
        # Create layer data (one layer per BG layer)
        layer_data_bg3 = [0] * (width * height)
        layer_data_bg2 = [0] * (width * height)
        layer_data_bg1 = [0] * (width * height)
        
        used_gids = set()  # Track which GIDs are actually used in the map
        
        # Add border GIDs to used_gids so they're included in the tileset
        if border_gids:
            for border_gid in border_gids.values():
                if border_gid > 0:
                    used_gids.add(border_gid)
        
        primary_attributes = tileset_data["primary_attributes"]
        secondary_attributes = tileset_data["secondary_attributes"]
        primary_tileset = tileset_data["primary_tileset"]
        secondary_tileset = tileset_data["secondary_tileset"]
        
        for y in range(height):
            for x in range(width):
                entry = map_entries[y][x]
                metatile_id = extract_metatile_id(entry)
                
                # Determine which tileset using processor
                tileset_name, actual_metatile_id = self.metatile_processor.determine_tileset_for_metatile(
                    metatile_id, primary_tileset, secondary_tileset
                )
                
                # Get appropriate attributes
                if tileset_name == primary_tileset:
                    attributes = primary_attributes
                else:
                    attributes = secondary_attributes
                
                # Get layer type
                layer_type_val = attributes.get(actual_metatile_id, 0)
                layer_type = MetatileLayerType(layer_type_val)
                
                # Get GIDs for this metatile
                bottom_gid = metatile_to_gid.get((actual_metatile_id, tileset_name, layer_type_val, False), 0)
                top_gid = metatile_to_gid.get((actual_metatile_id, tileset_name, layer_type_val, True), 0)

                idx = y * width + x
                
                # Assign to layers based on layer type and track used GIDs
                if layer_type == MetatileLayerType.NORMAL:
                    # Bottom -> Bg2, Top -> Bg1
                    layer_data_bg2[idx] = bottom_gid
                    layer_data_bg1[idx] = top_gid
                    if bottom_gid > 0:
                        used_gids.add(bottom_gid)
                    if top_gid > 0:
                        used_gids.add(top_gid)
                elif layer_type == MetatileLayerType.COVERED:
                    # Bottom -> Bg3, Top -> Bg2
                    layer_data_bg3[idx] = bottom_gid
                    layer_data_bg2[idx] = top_gid
                    if bottom_gid > 0:
                        used_gids.add(bottom_gid)
                    if top_gid > 0:
                        used_gids.add(top_gid)
                elif layer_type == MetatileLayerType.SPLIT:
                    # Bottom -> Bg3, Top -> Bg1
                    layer_data_bg3[idx] = bottom_gid
                    layer_data_bg1[idx] = top_gid
                    if bottom_gid > 0:
                        used_gids.add(bottom_gid)
                    if top_gid > 0:
                        used_gids.add(top_gid)
        
        return {
            "layer_data_bg3": layer_data_bg3,
            "layer_data_bg2": layer_data_bg2,
            "layer_data_bg1": layer_data_bg1,
            "used_gids": used_gids
        }
    
    def _create_tileset_for_map(
        self,
        map_id: str,
        region: str,
        used_metatiles: Dict[Tuple[int, str, int], Tuple[Image.Image, Image.Image]],
        metatile_to_gid: Dict[Tuple[int, str, int, bool], int],
        used_gids: Set[int],
        tileset_data: Dict[str, Any],
        tile_id_to_gids: Dict[Tuple[int, str], List[Tuple[int, Tuple[int, str, int], int]]],
        metatile_composition: Dict[Tuple[int, str, int], List]
    ) -> Dict[str, Any]:
        """
        Create tileset image and JSON for the map.
        
        Returns a dictionary containing:
        - tileset_json: The tileset JSON structure
        - tileset_image: The tileset image
        - tileset_dir: Path to tileset directory
        - map_name: Sanitized map name
        """
        map_name = sanitize_filename(map_id.replace("MAP_", "").lower())
        tileset_dir = self.output_dir / "Tilesets" / region / map_name
        tileset_dir.mkdir(parents=True, exist_ok=True)
        
        # Build unique set of images by GID (deduplication already done above)
        # Create mapping: GID -> Image, but only for used GIDs
        gid_to_image: Dict[int, Image.Image] = {}
        for (metatile_id, tileset, layer_type_val, is_top), gid in metatile_to_gid.items():
            if gid in used_gids and gid not in gid_to_image:
                key = (metatile_id, tileset, layer_type_val)
                bottom_img, top_img = used_metatiles[key]
                img_to_use = top_img if is_top else bottom_img
                gid_to_image[gid] = img_to_use
        
        # Count unique tiles (only used ones)
        cols = 16  # Arrange tiles in a grid
        unique_tile_count = len(gid_to_image)
        rows = (unique_tile_count + cols - 1) // cols if unique_tile_count > 0 else 1
        tileset_image = Image.new('RGBA', (cols * METATILE_SIZE, rows * METATILE_SIZE), (0, 0, 0, 0))
        
        # Build tileset image in GID order (1, 2, 3, ...) to match the GIDs assigned to map data
        # Only include GIDs that are actually used in the map
        tile_idx = 0
        for gid in sorted(gid_to_image.keys()):
            img = gid_to_image[gid]
            if not img:
                continue
            
            # Ensure RGBA mode
            if img.mode != 'RGBA':
                img = img.convert('RGBA')
            
            x = (tile_idx % cols) * METATILE_SIZE
            y = (tile_idx // cols) * METATILE_SIZE
            tileset_image.paste(img, (x, y), img)
            tile_idx += 1
        
        # Save tileset image
        tileset_image_path = tileset_dir / f"{map_name}.png"
        tileset_image.save(str(tileset_image_path), "PNG")
        
        # Create tileset JSON
        # Note: firstgid is NOT included in external tileset files - it's only in the map's tilesets array
        tileset_json = {
            "columns": cols,
            "image": f"{map_name}.png",
            "imageheight": rows * METATILE_SIZE,
            "imagewidth": cols * METATILE_SIZE,
            "margin": 0,
            "name": map_name,
            "spacing": 0,
            "tilecount": tile_idx,
            "tileheight": METATILE_SIZE,
            "tilewidth": METATILE_SIZE,
            "type": "tileset"
        }
        
        # Build mapping: (metatile_id, tileset, layer_type) -> GID for bottom layer
        # This is needed to find which GIDs correspond to animated metatiles
        metatile_key_to_bottom_gid: Dict[Tuple[int, str, int], int] = {}
        for (metatile_id, tileset, layer_type_val, is_top), gid in metatile_to_gid.items():
            if not is_top:  # Bottom layer
                key = (metatile_id, tileset, layer_type_val)
                metatile_key_to_bottom_gid[key] = gid
        
        # Add animations from base tilesets
        primary_tileset = tileset_data["primary_tileset"]
        secondary_tileset = tileset_data["secondary_tileset"]
        primary_metatiles_with_attrs = tileset_data["primary_metatiles_with_attrs"]
        secondary_metatiles_with_attrs = tileset_data["secondary_metatiles_with_attrs"]
        
        animations, animation_frames_gids, updated_tileset_image = self._build_metatile_animations(
            primary_tileset, secondary_tileset,
            used_metatiles, metatile_to_gid, used_gids,
            primary_metatiles_with_attrs, secondary_metatiles_with_attrs,
            metatile_key_to_bottom_gid, tile_id_to_gids, metatile_composition,
            tileset_image, tile_idx, cols
        )
        
        # Update tileset image if we added animation frames
        if animations:  # Check animations list, not just animation_frames_gids
            tileset_image = updated_tileset_image
            # Re-save tileset image with animation frames
            tileset_image_path = tileset_dir / f"{map_name}.png"
            tileset_image.save(str(tileset_image_path), "PNG")
            # Update tilecount and dimensions based on ACTUAL image size
            # (animation_frames_gids only tracks bottom layer, but top layer frames are also added)
            actual_rows = tileset_image.height // METATILE_SIZE
            actual_cols = tileset_image.width // METATILE_SIZE
            final_tile_count = actual_rows * actual_cols
            tileset_json["tilecount"] = final_tile_count
            tileset_json["imageheight"] = tileset_image.height
        
        if animations:
            tileset_json["tiles"] = animations
            logger.debug(f"Added {len(animations)} animations to {map_name} tileset")
        
        tileset_json_path = tileset_dir / f"{map_name}.json"
        save_json(tileset_json, str(tileset_json_path))
        
        return {
            "tileset_json": tileset_json,
            "tileset_image": tileset_image,
            "tileset_dir": tileset_dir,
            "map_name": map_name
        }
    
    def _create_tiled_map_structure(
        self,
        map_data: Dict[str, Any],
        map_id: str,
        width: int,
        height: int,
        map_name: str,
        region: str,
        tileset_json: Dict[str, Any],
        layer_data_bg3: List[int],
        layer_data_bg2: List[int],
        layer_data_bg1: List[int],
        border_gids: Dict[str, int],
        warp_lookup: Optional[Dict[Tuple[str, int], Tuple[int, int, int]]] = None
    ) -> Dict[str, Any]:
        """
        Create the final Tiled map JSON structure with all layers, objects, and properties.
        
        This method handles:
        - Map properties (name, border, connections)
        - Tileset references
        - Tile layers (Ground, Objects, Overhead)
        - Object groups (NPCs, Warps, Triggers, Interactions)
        """
        # Create map with metatiles as 16x16 tiles
        tiled_map = {
            "compressionlevel": -1,
            "height": height,  # Map height in metatiles (16x16 tiles)
            "infinite": False,
            "layers": [],
            "nextlayerid": 1,
            "nextobjectid": 1,
            "orientation": "orthogonal",
            "renderorder": "right-down",
            "tiledversion": "1.11.2",
            "tileheight": METATILE_SIZE,  # Metatiles are 16x16
            "tilewidth": METATILE_SIZE,
            "type": "map",
            "version": "1.11",
            "width": width,  # Map width in metatiles
            "properties": []
        }
        
        # Add properties
        if "name" in map_data:
            tiled_map["properties"].append({
                "name": "display_name",
                "type": "string",
                "value": map_data["name"]
            })
        
        # Add border tiles as property if available (using translated GIDs and Border class type)
        # Now includes both bottom layer (ground) and top layer (overhead) tiles
        if border_gids and len(border_gids) >= 4:
            tiled_map["properties"].append({
                "name": "border",
                "propertytype": "Border",
                "type": "class",
                "value": {
                    "top_left": border_gids.get("top_left", 0),
                    "top_right": border_gids.get("top_right", 0),
                    "bottom_left": border_gids.get("bottom_left", 0),
                    "bottom_right": border_gids.get("bottom_right", 0),
                    "top_left_top": border_gids.get("top_left_top", 0),
                    "top_right_top": border_gids.get("top_right_top", 0),
                    "bottom_left_top": border_gids.get("bottom_left_top", 0),
                    "bottom_right_top": border_gids.get("bottom_right_top", 0)
                }
            })
        
        # Add connections as properties (using Connection class type)
        connections = map_data.get("connections", [])
        if connections:
            # Helper function to convert direction to cardinal
            def direction_to_cardinal(direction: str) -> str:
                direction_lower = direction.lower()
                if direction_lower == "up":
                    return "North"
                elif direction_lower == "down":
                    return "South"
                elif direction_lower == "left":
                    return "West"
                elif direction_lower == "right":
                    return "East"
                else:
                    # Return as-is if already cardinal or unknown
                    return direction
            
            # Helper function to convert map ID to map name
            def map_id_to_name(map_id: str) -> str:
                # Remove MAP_ prefix and convert to lowercase with underscores
                if map_id.startswith("MAP_"):
                    name = map_id[4:].lower()
                    # Convert CamelCase to snake_case if needed
                    import re
                    name = re.sub('(.)([A-Z][a-z]+)', r'\1_\2', name)
                    name = re.sub('([a-z0-9])([A-Z])', r'\1_\2', name)
                    return name.lower()
                return map_id.lower()
            
            # Add each connection as a property using Connection class type
            # Use direction in name: connection_North, connection_South, etc.
            for conn in connections:
                connected_map_id = conn.get("map", "")
                if not connected_map_id:
                    continue
                
                # Convert map ID to map name
                connected_map_name = map_id_to_name(connected_map_id)
                
                # Convert direction to cardinal
                direction = conn.get("direction", "")
                cardinal_direction = direction_to_cardinal(direction)
                
                # Get offset (default to 0)
                offset = conn.get("offset", 0)
                
                # Add connection as a class property with direction-based name (lowercase for consistency)
                tiled_map["properties"].append({
                    "name": f"connection_{cardinal_direction.lower()}",
                    "propertytype": "Connection",
                    "type": "class",
                    "value": {
                        "direction": cardinal_direction,
                        "map": connected_map_name,
                        "offset": offset
                    }
                })
        
        # Layer data was already built above when collecting used GIDs
        
        # Add layers
        for layer_name, layer_data in [
            ("Ground", layer_data_bg3),
            ("Objects", layer_data_bg2),
            ("Overhead", layer_data_bg1)
        ]:
            tiled_map["layers"].append({
                "data": layer_data,
                "height": height,
                "id": len(tiled_map["layers"]) + 1,
                "name": layer_name,
                "opacity": 1,
                "type": "tilelayer",
                "visible": True,
                "width": width,
                "x": 0,
                "y": 0
            })
        
        # Add tileset reference
        # Map is at: output/Data/Maps/{region}/{map_name}.json
        # Tileset is at: output/Tilesets/{region}/{map_name}/{map_name}.json
        # From Data/Maps/{region}/, we need to go up three levels (../../../) to reach output/, then into Tilesets/
        tileset_path = f"../../../Tilesets/{region}/{map_name}/{map_name}.json"
        tiled_map["tilesets"] = [{
            "firstgid": 1,
            "source": tileset_path
        }]
        
        # Add warp events as an object layer
        if warp_lookup is not None:
            warp_events = map_data.get("warp_events", [])
            if warp_events:
                warp_objects = []
                object_id = 1
                
                # Helper function to convert map ID to map name
                def map_id_to_name(map_id: str) -> str:
                    # Remove MAP_ prefix and convert to lowercase with underscores
                    if map_id.startswith("MAP_"):
                        name = map_id[4:].lower()
                        # Convert CamelCase to snake_case if needed
                        import re
                        name = re.sub('(.)([A-Z][a-z]+)', r'\1_\2', name)
                        name = re.sub('([a-z0-9])([A-Z])', r'\1_\2', name)
                        return name.lower()
                    return map_id.lower()
                
                for warp in warp_events:
                    source_x = warp.get("x", 0)
                    source_y = warp.get("y", 0)
                    source_elevation = warp.get("elevation", 0)
                    dest_map_id = warp.get("dest_map", "")
                    dest_warp_id_str = warp.get("dest_warp_id", "")
                    
                    if not dest_map_id:
                        continue
                    
                    # Resolve destination coordinates from warp_lookup
                    dest_x = 0
                    dest_y = 0
                    dest_elevation = 0
                    
                    try:
                        dest_warp_id = int(dest_warp_id_str)
                        dest_key = (dest_map_id, dest_warp_id)
                        if dest_key in warp_lookup:
                            dest_x, dest_y, dest_elevation = warp_lookup[dest_key]
                        else:
                            # Warn if destination not found
                            logger.warning(f"Warp destination not found: {dest_map_id}[{dest_warp_id}]")
                    except (ValueError, TypeError):
                        # Invalid dest_warp_id, skip this warp
                        logger.warning(f"Invalid dest_warp_id '{dest_warp_id_str}' for warp at ({source_x}, {source_y})")
                        continue
                    
                    # Convert destination map ID to map name
                    dest_map_name = map_id_to_name(dest_map_id)
                    
                    # Create warp object (convert tile coordinates to pixel coordinates)
                    warp_obj = {
                        "id": object_id,
                        "name": f"Warp to {dest_map_name}",
                        "type": "warp_event",
                        "x": source_x * METATILE_SIZE,  # Convert tiles to pixels
                        "y": source_y * METATILE_SIZE,
                        "width": METATILE_SIZE,
                        "height": METATILE_SIZE,
                        "properties": [
                            {
                                "name": "warp",
                                "propertytype": "Warp",
                                "type": "class",
                                "value": {
                                    "map": dest_map_name,
                                    "x": dest_x,
                                    "y": dest_y,
                                    "elevation": dest_elevation
                                }
                            }
                        ]
                    }
                    warp_objects.append(warp_obj)
                    object_id += 1
                
                if warp_objects:
                    # Add Warps object layer
                    warps_layer = {
                        "id": len(tiled_map["layers"]) + 1,
                        "name": "Warps",
                        "type": "objectgroup",
                        "visible": True,
                        "opacity": 1,
                        "x": 0,
                        "y": 0,
                        "objects": warp_objects
                    }
                    tiled_map["layers"].append(warps_layer)
                    tiled_map["nextobjectid"] = object_id
        
        # Add coord events as an object layer
        coord_events = map_data.get("coord_events", [])
        if coord_events:
            coord_objects = []
            coord_object_id = tiled_map.get("nextobjectid", 1)
            
            for coord_event in coord_events:
                event_type = coord_event.get("type", "")
                x = coord_event.get("x", 0)
                y = coord_event.get("y", 0)
                elevation = coord_event.get("elevation", 0)
                var = coord_event.get("var", "")
                var_value = coord_event.get("var_value", "0")
                script = coord_event.get("script", "")
                
                # Only process "trigger" type events for now
                if event_type != "trigger":
                    continue
                
                if not var or not script:
                    continue
                
                # Convert var_value to integer
                try:
                    var_value_int = int(var_value)
                except (ValueError, TypeError):
                    logger.warning(f"Invalid var_value '{var_value}' for coord event at ({x}, {y})")
                    continue
                
                # Format script path: prefix with "Triggers/" and add ".csx" extension
                trigger_script_path = f"Triggers/{script}.csx"
                
                # Create coord event object (convert tile coordinates to pixel coordinates)
                coord_obj = {
                    "id": coord_object_id,
                    "name": f"Trigger: {var} == {var_value_int}",
                    "type": "trigger_event",
                    "x": x * METATILE_SIZE,  # Convert tiles to pixels
                    "y": y * METATILE_SIZE,
                    "width": METATILE_SIZE,
                    "height": METATILE_SIZE,
                    "properties": [
                        {
                            "name": "trigger",
                            "propertytype": "Trigger",
                            "type": "class",
                            "value": {
                                "variable": var,
                                "value": var_value_int,
                                "triggerScript": trigger_script_path,
                                "elevation": elevation
                            }
                        }
                    ]
                }
                coord_objects.append(coord_obj)
                coord_object_id += 1
            
            if coord_objects:
                # Add Triggers object layer
                coord_events_layer = {
                    "id": len(tiled_map["layers"]) + 1,
                    "name": "Triggers",
                    "type": "objectgroup",
                    "visible": True,
                    "opacity": 1,
                    "x": 0,
                    "y": 0,
                    "objects": coord_objects
                }
                tiled_map["layers"].append(coord_events_layer)
                tiled_map["nextobjectid"] = coord_object_id
        
        # Add bg events as an object layer
        bg_events = map_data.get("bg_events", [])
        if bg_events:
            bg_objects = []
            bg_object_id = tiled_map.get("nextobjectid", 1)
            
            # Helper function to convert facing direction
            def convert_facing_direction(facing_dir: str) -> Optional[str]:
                """Convert BG_EVENT_PLAYER_FACING_* to cardinal direction or None for Any."""
                if not facing_dir:
                    return None
                
                facing_lower = facing_dir.lower()
                if "any" in facing_lower:
                    return None  # Don't include if Any
                elif "north" in facing_lower:
                    return "North"
                elif "south" in facing_lower:
                    return "South"
                elif "east" in facing_lower:
                    return "East"
                elif "west" in facing_lower:
                    return "West"
                else:
                    return None  # Default to Any (don't include)
            
            for bg_event in bg_events:
                event_type = bg_event.get("type", "")
                x = bg_event.get("x", 0)
                y = bg_event.get("y", 0)
                elevation = bg_event.get("elevation", 0)
                
                if event_type == "sign":
                    script = bg_event.get("script", "")
                    player_facing_dir = bg_event.get("player_facing_dir", "")
                    
                    if not script:
                        continue
                    
                    # Format script path: prefix with "Interactions/" and add ".csx" extension
                    interaction_script_path = f"Interactions/{script}.csx"
                    
                    # Convert facing direction
                    facing = convert_facing_direction(player_facing_dir)
                    
                    # Build value object
                    sign_value = {
                        "interactionScript": interaction_script_path,
                        "elevation": elevation
                    }
                    # Only include facing if it's not None (i.e., not "Any")
                    if facing is not None:
                        sign_value["facing"] = facing
                    
                    bg_obj = {
                        "id": bg_object_id,
                        "name": f"Sign: {script}",
                        "type": "sign_event",
                        "x": x * METATILE_SIZE,  # Convert tiles to pixels
                        "y": y * METATILE_SIZE,
                        "width": METATILE_SIZE,
                        "height": METATILE_SIZE,
                        "properties": [
                            {
                                "name": "sign",
                                "propertytype": "SignEvent",
                                "type": "class",
                                "value": sign_value
                            }
                        ]
                    }
                    bg_objects.append(bg_obj)
                    bg_object_id += 1
                
                elif event_type == "hidden_item":
                    item = bg_event.get("item", "")
                    flag = bg_event.get("flag", "")
                    
                    if not item or not flag:
                        continue
                    
                    bg_obj = {
                        "id": bg_object_id,
                        "name": f"Hidden Item: {item}",
                        "type": "hidden_item_event",
                        "x": x * METATILE_SIZE,  # Convert tiles to pixels
                        "y": y * METATILE_SIZE,
                        "width": METATILE_SIZE,
                        "height": METATILE_SIZE,
                        "properties": [
                            {
                                "name": "hidden_item",
                                "propertytype": "HiddenItemEvent",
                                "type": "class",
                                "value": {
                                    "item": item,
                                    "flag": flag,
                                    "elevation": elevation
                                }
                            }
                        ]
                    }
                    bg_objects.append(bg_obj)
                    bg_object_id += 1
                
                elif event_type == "secret_base":
                    secret_base_id = bg_event.get("secret_base_id", "")
                    
                    if not secret_base_id:
                        continue
                    
                    bg_obj = {
                        "id": bg_object_id,
                        "name": "Secret Base",
                        "type": "secret_base_event",
                        "x": x * METATILE_SIZE,  # Convert tiles to pixels
                        "y": y * METATILE_SIZE,
                        "width": METATILE_SIZE,
                        "height": METATILE_SIZE,
                        "properties": [
                            {
                                "name": "secret_base",
                                "propertytype": "SecretBaseEvent",
                                "type": "class",
                                "value": {
                                    "secret_base_id": secret_base_id,
                                    "elevation": elevation
                                }
                            }
                        ]
                    }
                    bg_objects.append(bg_obj)
                    bg_object_id += 1
            
            if bg_objects:
                # Add Interactions object layer
                bg_events_layer = {
                    "id": len(tiled_map["layers"]) + 1,
                    "name": "Interactions",
                    "type": "objectgroup",
                    "visible": True,
                    "opacity": 1,
                    "x": 0,
                    "y": 0,
                    "objects": bg_objects
                }
                tiled_map["layers"].append(bg_events_layer)
                tiled_map["nextobjectid"] = bg_object_id
        
        return tiled_map
    
    def convert_map_with_metatiles(
        self,
        map_id: str,
        map_data: Dict[str, Any],
        layout_data: Dict[str, Any],
        region: str,
        warp_lookup: Optional[Dict[Tuple[str, int], Tuple[int, int, int]]] = None
    ) -> Optional[Dict[str, Any]]:
        """
        Convert a map using 16x16 metatiles, creating a unique tileset per map.
        
        This method processes Pokemon Emerald maps by:
        1. Reading map.bin file containing metatile IDs
        2. Rendering each metatile as 16x16 images (bottom and top layers)
        3. Creating a per-map tileset with only used metatiles
        4. Assigning Global Tile IDs (GIDs) to metatiles with deduplication
        5. Building map layers (BG3, BG2, BG1) based on metatile layer types
        6. Processing border metatiles for map edges
        7. Adding animations for animated tiles
        8. Converting events (warps, coord events, background events) to Tiled objects
        9. Generating the final Tiled map JSON structure
        
        The metatile rendering approach:
        - Each metatile is rendered as two 16x16 images: bottom (tiles 0-3) and top (tiles 4-7)
        - Images are deduplicated by content to minimize tileset size
        - GIDs are assigned sequentially, starting from 1
        
        Args:
            map_id: Unique identifier for the map (e.g., "MAP_LITTLEROOT_TOWN")
            map_data: Parsed map.json data containing map metadata and events
            layout_data: Dictionary mapping layout_id -> layout info (width, height, tilesets, etc.)
            region: Region name for organizing output (e.g., "hoenn")
            warp_lookup: Optional lookup table mapping (map_id, warp_index) -> (x, y, elevation)
                        for resolving warp destinations
        
        Returns:
            Tiled map JSON structure with layers, tilesets, and objects, or None if conversion fails
        
        Raises:
            ValueError: If map dimensions are invalid or inputs are invalid
            FileNotFoundError: If required map files are missing
        """
        # Validate inputs
        validate_map_id(map_id)
        validate_region(region)
        validate_non_empty_dict(map_data, "map_data")
        validate_non_empty_dict(layout_data, "layout_data")
        
        # Validate layout
        layout = self._validate_layout(map_data, layout_data)
        if not layout:
            return None
        
        # Read and validate map data
        result = self._read_and_validate_map_data(layout)
        if not result:
            return None
        map_entries, width, height = result
        
        # Validate dimensions
        validate_map_dimensions(width, height)
        
        # Load tileset data
        tileset_data = self._load_tileset_data(layout)
        if not tileset_data:
            return None
        
        # Process metatiles
        metatile_result = self._process_metatiles(map_entries, width, height, tileset_data)
        used_metatiles = metatile_result["used_metatiles"]
        metatile_to_gid = metatile_result["metatile_to_gid"]
        tile_id_to_gids = metatile_result["tile_id_to_gids"]
        metatile_composition = metatile_result["metatile_composition"]
        image_to_gid = metatile_result["image_to_gid"]
        next_gid = metatile_result["next_gid"]
        
        primary_tileset = tileset_data["primary_tileset"]
        secondary_tileset = tileset_data["secondary_tileset"]
        primary_metatiles_with_attrs = tileset_data["primary_metatiles_with_attrs"]
        secondary_metatiles_with_attrs = tileset_data["secondary_metatiles_with_attrs"]
        primary_attributes = tileset_data["primary_attributes"]
        secondary_attributes = tileset_data["secondary_attributes"]

        # Process border metatiles
        border_gids, next_gid = self._process_border_metatiles(
            layout, tileset_data, used_metatiles, metatile_to_gid, image_to_gid, next_gid
        )
        
        # Build map layers
        layer_result = self._build_map_layers(
            map_entries, width, height, metatile_to_gid, tileset_data, border_gids
        )
        layer_data_bg3 = layer_result["layer_data_bg3"]
        layer_data_bg2 = layer_result["layer_data_bg2"]
        layer_data_bg1 = layer_result["layer_data_bg1"]
        used_gids = layer_result["used_gids"]
        
        # Create tileset for map
        tileset_info = self._create_tileset_for_map(
            map_id, region, used_metatiles, metatile_to_gid, used_gids,
            tileset_data, tile_id_to_gids, metatile_composition
        )
        map_name = tileset_info["map_name"]
        tileset_json = tileset_info["tileset_json"]
        
        # Create final Tiled map structure
        tiled_map = self._create_tiled_map_structure(
            map_data, map_id, width, height, map_name, region, tileset_json,
            layer_data_bg3, layer_data_bg2, layer_data_bg1, border_gids, warp_lookup
        )
        
        return tiled_map
    
    def _build_metatile_animations(
        self,
        primary_tileset: str,
        secondary_tileset: str,
        used_metatiles: Dict[Tuple[int, str, int], Tuple[Image.Image, Image.Image]],
        metatile_to_gid: Dict[Tuple[int, str, int, bool], int],
        used_gids: Set[int],
        primary_metatiles: List[Tuple[int, int, int]],
        secondary_metatiles: List[Tuple[int, int, int]],
        metatile_key_to_bottom_gid: Dict[Tuple[int, str, int], int],
        tile_id_to_gids: Dict[Tuple[int, str], List[Tuple[int, Tuple[int, str, int]]]],
        metatile_composition: Dict[Tuple[int, str, int], List],
        tileset_image: Image.Image,
        current_tile_idx: int,
        cols: int
    ) -> Tuple[List[Dict], Dict[Tuple[int, str, int], int], Image.Image]:
        """
        Build animations for metatiles that contain animated tiles.
        
        Animation frames are 16x16 metatiles, so we can use them directly!
        
        Returns:
            (animations_list, animation_frames_gids, updated_tileset_image) where:
            - animations_list: List of animation definitions
            - animation_frames_gids: Maps (metatile_id, tileset, frame_idx) -> gid
            - updated_tileset_image: Tileset image with animation frames added
        """
        animations = []
        animation_frames_gids = {}  # (metatile_id, tileset, frame_idx) -> gid
        next_gid = current_tile_idx + 1
        
        # Get animations for both primary and secondary tilesets
        primary_anims = self.animation_scanner.get_animations_for_tileset(primary_tileset)
        secondary_anims = self.animation_scanner.get_animations_for_tileset(secondary_tileset)

        if not primary_anims and not secondary_anims:
            return animations, animation_frames_gids, tileset_image
        
        # Extract animation frame data (use default tile_size=8 for proper 8x8 tile extraction)
        # The animation scanner handles 16x16 metatile frames vs 8x8 tile strips automatically
        primary_anim_data = self.animation_scanner.extract_all_animation_tiles(primary_tileset)
        secondary_anim_data = self.animation_scanner.extract_all_animation_tiles(secondary_tileset)
        
        # Process animations from both primary and secondary tilesets
        all_anim_defs = []
        if primary_anims:
            all_anim_defs.extend([(anim_name, anim_def, primary_tileset, primary_anim_data) 
                                 for anim_name, anim_def in primary_anims.items()])
        if secondary_anims:
            all_anim_defs.extend([(anim_name, anim_def, secondary_tileset, secondary_anim_data) 
                                 for anim_name, anim_def in secondary_anims.items()])
        
        if not all_anim_defs:
            return animations, animation_frames_gids, tileset_image

        # PHASE 1: Collect all animation info for each metatile
        # Build: metatile_key -> list of (anim_name, frames, duration_ms, frame_sequence, actual_base_tile_id, num_tiles, animated_tile_range)
        metatile_animations = {}  # metatile_key -> list of animation info
        metatile_all_gids = {}    # metatile_key -> list of GIDs

        for anim_name, anim_def, base_tileset, anim_data in all_anim_defs:
            base_tile_id = anim_def["base_tile_id"]
            num_tiles = anim_def["num_tiles"]
            is_secondary = anim_def.get("is_secondary", False)

            # For SECONDARY tileset animations, tile IDs in metatiles are offset by 512
            actual_base_tile_id = base_tile_id + 512 if is_secondary else base_tile_id
            anim_range_tiles = set(range(actual_base_tile_id, actual_base_tile_id + num_tiles))
            gid_mappings = []
            tiles_found = []

            for tile_id in anim_range_tiles:
                tile_key = (tile_id, base_tileset)
                if tile_key in tile_id_to_gids:
                    gid_mappings.extend(tile_id_to_gids[tile_key])
                    tiles_found.append(tile_id)

            if not gid_mappings:
                continue

            # Get animation frames
            if anim_name not in anim_data:
                continue

            frames_info = anim_data[anim_name]
            frames = frames_info["frames"]
            duration_ms = frames_info["duration_ms"]
            is_metatile = frames_info.get("is_metatile", False)
            frame_sequence = frames_info.get("frame_sequence", None)

            if not frames:
                continue

            animated_tile_range = set(range(actual_base_tile_id, actual_base_tile_id + num_tiles))

            # Group by metatile_key and collect animation info
            # IMPORTANT: Track which specific GID this animation applies to
            # Now includes tile_position to correctly identify bottom vs top layer
            for layer_gid, metatile_key, tile_position in gid_mappings:
                if layer_gid not in used_gids:
                    continue

                # Track GIDs for this metatile (for reference)
                if metatile_key not in metatile_all_gids:
                    metatile_all_gids[metatile_key] = []
                if layer_gid not in metatile_all_gids[metatile_key]:
                    metatile_all_gids[metatile_key].append(layer_gid)

                # Add animation info for this metatile (avoid duplicates)
                if metatile_key not in metatile_animations:
                    metatile_animations[metatile_key] = []

                # Check if this animation type at this position is already added for this metatile
                # BUGFIX: Allow same animation name to be added multiple times if tile_position differs
                # (e.g., same tile appearing in both bottom and top layers should get separate entries)
                existing_anims = [(a[0], a[9]) for a in metatile_animations[metatile_key]]  # (anim_name, tile_position)
                if (anim_name, tile_position) not in existing_anims:
                    # BUGFIX: Include layer_gid AND tile_position so we know which specific GID
                    # this animation applies to and whether it's bottom (pos 0-3) or top (pos 4-7)
                    metatile_animations[metatile_key].append((
                        anim_name, frames, duration_ms, frame_sequence,
                        actual_base_tile_id, num_tiles, animated_tile_range, is_metatile, layer_gid, tile_position
                    ))

        # PHASE 2: Process each metatile ONCE with ALL its animations combined
        for metatile_key, anim_info_list in metatile_animations.items():
            metatile_id, tileset_name, layer_type_val = metatile_key
            gid_list = metatile_all_gids.get(metatile_key, [])

            if not gid_list:
                continue

            # Get base metatile images
            if metatile_key not in used_metatiles:
                continue
            base_bottom, base_top = used_metatiles[metatile_key]

            # Get metatile tile composition (which 8x8 tiles are at each position)
            if metatile_key not in metatile_composition:
                continue
            metatile_tiles = metatile_composition[metatile_key]

            # Check if any animation is metatile-type (like flower)
            has_metatile_anim = any(info[7] for info in anim_info_list)

            if has_metatile_anim:
                # For metatile animations, use the first metatile animation's frames directly
                # (these are already 16x16 composited frames)
                for anim_name, frames, duration_ms, frame_sequence, _, _, _, is_metatile, anim_layer_gid, _ in anim_info_list:
                    if not is_metatile:
                        continue

                    # Add all unique frame images to the tileset
                    frame_gid_map = {}
                    for frame_idx, frame_img in enumerate(frames):
                        tile_idx = next_gid - 1
                        x = (tile_idx % cols) * 16
                        y = (tile_idx // cols) * 16

                        if y + 16 > tileset_image.height:
                            new_height = ((tile_idx // cols) + 1) * 16
                            new_img = Image.new('RGBA', (tileset_image.width, new_height), (0, 0, 0, 0))
                            new_img.paste(tileset_image, (0, 0))
                            tileset_image = new_img

                        if frame_img.mode != 'RGBA':
                            frame_img = frame_img.convert('RGBA')
                        tileset_image.paste(frame_img, (x, y), frame_img)
                        frame_gid_map[frame_idx] = tile_idx
                        next_gid += 1

                    playback_order = frame_sequence if frame_sequence else list(range(len(frames)))
                    shared_frame_gids = []
                    for seq_idx in playback_order:
                        if seq_idx in frame_gid_map:
                            shared_frame_gids.append({
                                "tileid": frame_gid_map[seq_idx],
                                "duration": duration_ms
                            })

                    # BUGFIX: Only apply animation to the SPECIFIC GID that contains animated tiles
                    # Not to all GIDs for this metatile
                    if anim_layer_gid in used_gids and shared_frame_gids:
                        for frame_idx, frame_entry in enumerate(shared_frame_gids):
                            animation_frames_gids[(metatile_id, tileset_name, frame_idx)] = frame_entry["tileid"] + 1
                        animations.append({"id": anim_layer_gid - 1, "animation": shared_frame_gids})
                    break  # Only process first metatile animation
            else:
                # TILE STRIP ANIMATIONS: composite frames from ALL animation types together
                # Collect all animated positions and their animation info
                all_bottom_tiles = {}  # pos -> (tile_id, flip_flags, anim_name, frames, actual_base, num_tiles, frame_seq, duration)
                all_top_tiles = {}

                # BUGFIX: Collect which GIDs are associated with bottom vs top layer animations
                bottom_layer_gids = set()
                top_layer_gids = set()

                for anim_name, frames, duration_ms, frame_sequence, actual_base_tile_id, num_tiles, animated_tile_range, _, anim_layer_gid, orig_tile_position in anim_info_list:
                    # Use the stored orig_tile_position to determine which layer this animation belongs to
                    # This fixes the bug where tiles appearing in BOTH layers were incorrectly assigned to both
                    if orig_tile_position < 4:
                        # This animation entry is for the bottom layer
                        pos = orig_tile_position
                        tile_id, flip_flags, _ = metatile_tiles[pos]
                        if tile_id in animated_tile_range:
                            all_bottom_tiles[pos] = (tile_id, flip_flags, anim_name, frames, actual_base_tile_id, num_tiles, frame_sequence, duration_ms)
                            bottom_layer_gids.add(anim_layer_gid)
                    else:
                        # This animation entry is for the top layer
                        pos = orig_tile_position
                        tile_id, flip_flags, _ = metatile_tiles[pos]
                        if tile_id in animated_tile_range:
                            all_top_tiles[pos - 4] = (tile_id, flip_flags, anim_name, frames, actual_base_tile_id, num_tiles, frame_sequence, duration_ms)
                            top_layer_gids.add(anim_layer_gid)

                if not all_bottom_tiles and not all_top_tiles:
                    continue

                # Determine number of frames - use 8 as standard (most animations are 8 frames)
                # All Pokemon animations sync to 8 frames at 200ms
                num_anim_frames = 8
                duration_ms = 200  # Standard duration

                # Composite ALL animation types together for each frame
                composited_frame_map = {}
                for frame_num in range(num_anim_frames):
                    composite_bottom = base_bottom.copy()
                    composite_top = base_top.copy()

                    # Replace animated tiles in bottom layer from ALL animation types
                    for pos, (tile_id, flip_flags, anim_name, frames, actual_base, num_tiles, frame_seq, _) in all_bottom_tiles.items():
                        # Map frame_num to actual frame using frame_sequence
                        if frame_seq:
                            actual_frame = frame_seq[frame_num % len(frame_seq)]
                        else:
                            # Safe division: check both num_tiles > 0 and frames_per_cycle > 0
                            if num_tiles > 0 and len(frames) > 0:
                                frames_per_cycle = len(frames) // num_tiles
                                if frames_per_cycle > 0:
                                    actual_frame = frame_num % frames_per_cycle
                                else:
                                    actual_frame = 0  # Not enough frames, use first frame
                            else:
                                actual_frame = 0

                        tile_offset = tile_id - actual_base
                        frame_tile_idx = actual_frame * num_tiles + tile_offset
                        if 0 <= frame_tile_idx < len(frames):
                            frame_tile = frames[frame_tile_idx]
                            if frame_tile.mode != 'RGBA':
                                frame_tile = frame_tile.convert('RGBA')
                            # Apply flip flags from metatile definition
                            if flip_flags & FLIP_HORIZONTAL:
                                frame_tile = frame_tile.transpose(Image.FLIP_LEFT_RIGHT)
                            if flip_flags & FLIP_VERTICAL:
                                frame_tile = frame_tile.transpose(Image.FLIP_TOP_BOTTOM)
                            px = (pos % 2) * 8
                            py = (pos // 2) * 8
                            composite_bottom.paste(frame_tile, (px, py), frame_tile)

                    # Replace animated tiles in top layer from ALL animation types
                    for pos, (tile_id, flip_flags, anim_name, frames, actual_base, num_tiles, frame_seq, _) in all_top_tiles.items():
                        if frame_seq:
                            actual_frame = frame_seq[frame_num % len(frame_seq)]
                        else:
                            # Safe division: check both num_tiles > 0 and frames_per_cycle > 0
                            if num_tiles > 0 and len(frames) > 0:
                                frames_per_cycle = len(frames) // num_tiles
                                if frames_per_cycle > 0:
                                    actual_frame = frame_num % frames_per_cycle
                                else:
                                    actual_frame = 0  # Not enough frames, use first frame
                            else:
                                actual_frame = 0

                        tile_offset = tile_id - actual_base
                        frame_tile_idx = actual_frame * num_tiles + tile_offset
                        if 0 <= frame_tile_idx < len(frames):
                            frame_tile = frames[frame_tile_idx]
                            if frame_tile.mode != 'RGBA':
                                frame_tile = frame_tile.convert('RGBA')
                            # Apply flip flags from metatile definition
                            if flip_flags & FLIP_HORIZONTAL:
                                frame_tile = frame_tile.transpose(Image.FLIP_LEFT_RIGHT)
                            if flip_flags & FLIP_VERTICAL:
                                frame_tile = frame_tile.transpose(Image.FLIP_TOP_BOTTOM)
                            px = (pos % 2) * 8
                            py = (pos // 2) * 8
                            composite_top.paste(frame_tile, (px, py), frame_tile)

                    # Add composited BOTTOM image to tileset
                    tile_idx = next_gid - 1
                    x = (tile_idx % cols) * 16
                    y = (tile_idx // cols) * 16
                    if y + 16 > tileset_image.height:
                        new_height = ((tile_idx // cols) + 1) * 16
                        new_img = Image.new('RGBA', (tileset_image.width, new_height), (0, 0, 0, 0))
                        new_img.paste(tileset_image, (0, 0))
                        tileset_image = new_img
                    tileset_image.paste(composite_bottom, (x, y), composite_bottom)
                    composited_frame_map[frame_num] = tile_idx
                    next_gid += 1

                # Also save TOP layer animation frames if there are animated top tiles
                composited_top_frame_map = {}
                if all_top_tiles:
                    for frame_num in range(num_anim_frames):
                        # Get composite_top for this frame (need to rebuild it)
                        composite_top_frame = base_top.copy()
                        for pos, (tile_id, flip_flags, anim_name, frames, actual_base, num_tiles, frame_seq, _) in all_top_tiles.items():
                            if frame_seq:
                                actual_frame = frame_seq[frame_num % len(frame_seq)]
                            else:
                                # Safe division: check both num_tiles > 0 and frames_per_cycle > 0
                                if num_tiles > 0 and len(frames) > 0:
                                    frames_per_cycle = len(frames) // num_tiles
                                    if frames_per_cycle > 0:
                                        actual_frame = frame_num % frames_per_cycle
                                    else:
                                        actual_frame = 0  # Not enough frames, use first frame
                                else:
                                    actual_frame = 0
                            tile_offset = tile_id - actual_base
                            frame_tile_idx = actual_frame * num_tiles + tile_offset
                            if 0 <= frame_tile_idx < len(frames):
                                frame_tile = frames[frame_tile_idx]
                                if frame_tile.mode != 'RGBA':
                                    frame_tile = frame_tile.convert('RGBA')
                                if flip_flags & 0x01:
                                    frame_tile = frame_tile.transpose(Image.FLIP_LEFT_RIGHT)
                                if flip_flags & 0x02:
                                    frame_tile = frame_tile.transpose(Image.FLIP_TOP_BOTTOM)
                                px = (pos % 2) * 8
                                py = (pos // 2) * 8
                                composite_top_frame.paste(frame_tile, (px, py), frame_tile)

                        # Add to tileset
                        tile_idx = next_gid - 1
                        x = (tile_idx % cols) * 16
                        y = (tile_idx // cols) * 16
                        if y + 16 > tileset_image.height:
                            new_height = ((tile_idx // cols) + 1) * 16
                            new_img = Image.new('RGBA', (tileset_image.width, new_height), (0, 0, 0, 0))
                            new_img.paste(tileset_image, (0, 0))
                            tileset_image = new_img
                        tileset_image.paste(composite_top_frame, (x, y), composite_top_frame)
                        composited_top_frame_map[frame_num] = tile_idx
                        next_gid += 1

                # Build animation sequence for BOTTOM layer
                composited_frame_gids = []
                for frame_num in range(num_anim_frames):
                    if frame_num in composited_frame_map:
                        composited_frame_gids.append({
                            "tileid": composited_frame_map[frame_num],
                            "duration": duration_ms
                        })

                # Build animation sequence for TOP layer
                composited_top_frame_gids = []
                for frame_num in range(num_anim_frames):
                    if frame_num in composited_top_frame_map:
                        composited_top_frame_gids.append({
                            "tileid": composited_top_frame_map[frame_num],
                            "duration": duration_ms
                        })

                # Apply BOTTOM layer animation to bottom GIDs
                if composited_frame_gids and all_bottom_tiles:
                    for gid in bottom_layer_gids:
                        if gid in used_gids:
                            for frame_idx, frame_entry in enumerate(composited_frame_gids):
                                animation_frames_gids[(metatile_id, tileset_name, frame_idx)] = frame_entry["tileid"] + 1
                            animations.append({"id": gid - 1, "animation": composited_frame_gids})

                # Apply TOP layer animation to top GIDs
                if composited_top_frame_gids and all_top_tiles:
                    for gid in top_layer_gids:
                        if gid in used_gids:
                            animations.append({"id": gid - 1, "animation": composited_top_frame_gids})

        # Deduplicate animations by tile ID
        # Multiple metatiles can share the same GID due to image deduplication
        # Keep only one animation entry per tile ID
        seen_ids = set()
        deduplicated_animations = []
        for anim in animations:
            anim_id = anim["id"]
            if anim_id not in seen_ids:
                seen_ids.add(anim_id)
                deduplicated_animations.append(anim)

        return deduplicated_animations, animation_frames_gids, tileset_image
    

