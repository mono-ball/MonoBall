"""
Animation scanner - scans anim folders and extracts animation frame data.

This module scans the pokeemerald anim folders to find animation frames
and maps them to tile IDs based on the hardcoded offsets in tileset_anims.c.
"""

from pathlib import Path
from typing import Dict, List, Tuple, Optional
from PIL import Image
import json
from .utils import camel_to_snake, TilesetPathResolver
from .logging_config import get_logger

logger = get_logger('animation_scanner')


# Mapping from tileset name to animation definitions
# Format: {tileset_name: {animation_name: {base_tile_id: int, num_tiles: int, frames: List[str]}}}
# These are extracted from tileset_anims.c VRAM offsets
# frame_sequence defines the ORDER frames are played (from tileset_anims.c arrays)
# Without frame_sequence, frames play linearly (0, 1, 2, ...)
ANIMATION_MAPPINGS = {
    "general": {
        "flower": {
            "base_tile_id": 508,
            "num_tiles": 4,
            "anim_folder": "flower",
            "duration_ms": 200,  # Default duration per frame
            # From gTilesetAnims_General_Flower[]: Frame0, Frame1, Frame0, Frame2
            "frame_sequence": [0, 1, 0, 2]
        },
        "water": {
            "base_tile_id": 432,
            "num_tiles": 30,
            "anim_folder": "water",
            "duration_ms": 200,
            # From gTilesetAnims_General_Water[]: Frame0-7 linear
            "frame_sequence": [0, 1, 2, 3, 4, 5, 6, 7]
        },
        "sand_water_edge": {
            "base_tile_id": 464,
            "num_tiles": 10,
            "anim_folder": "sand_water_edge",
            "duration_ms": 200,
            # From gTilesetAnims_General_SandWaterEdge[]: ends with Frame0 for smooth loop
            "frame_sequence": [0, 1, 2, 3, 4, 5, 6, 0]
        },
        "waterfall": {
            "base_tile_id": 496,
            "num_tiles": 6,
            "anim_folder": "waterfall",
            "duration_ms": 200,
            # From gTilesetAnims_General_Waterfall[]: Frame0-3 linear
            "frame_sequence": [0, 1, 2, 3]
        },
        "land_water_edge": {
            "base_tile_id": 480,
            "num_tiles": 10,
            "anim_folder": "land_water_edge",
            "duration_ms": 200,
            # From gTilesetAnims_General_LandWaterEdge[]: Frame0-3 linear
            "frame_sequence": [0, 1, 2, 3]
        }
    },
    "building": {
        "tv_turned_on": {
            "base_tile_id": 496,
            "num_tiles": 4,
            "anim_folder": "tv_turned_on",
            "duration_ms": 200
        }
    },
    "rustboro": {
        "windy_water": {
            "base_tile_id": 128,  # NUM_TILES_IN_PRIMARY + 128 = 512 + 128 = 640, but in secondary tileset it's offset
            "num_tiles": 8,
            "anim_folder": "windy_water",
            "duration_ms": 200,
            "is_secondary": True
        },
        "fountain": {
            "base_tile_id": 448,  # NUM_TILES_IN_PRIMARY + 448
            "num_tiles": 4,
            "anim_folder": "fountain",
            "duration_ms": 200,
            "is_secondary": True
        }
    },
    "dewford": {
        "flag": {
            "base_tile_id": 170,  # NUM_TILES_IN_PRIMARY + 170
            "num_tiles": 6,
            "anim_folder": "flag",
            "duration_ms": 200,
            "is_secondary": True
        }
    },
    "slateport": {
        "balloons": {
            "base_tile_id": 224,  # NUM_TILES_IN_PRIMARY + 224
            "num_tiles": 4,
            "anim_folder": "balloons",
            "duration_ms": 200,
            "is_secondary": True
        }
    },
    "mauville": {
        "flower_1": {
            "base_tile_id": 96,  # NUM_TILES_IN_PRIMARY + 96
            "num_tiles": 4,
            "anim_folder": "flower_1",
            "duration_ms": 200,
            "is_secondary": True
        },
        "flower_2": {
            "base_tile_id": 128,  # NUM_TILES_IN_PRIMARY + 128
            "num_tiles": 4,
            "anim_folder": "flower_2",
            "duration_ms": 200,
            "is_secondary": True
        }
    },
    "lavaridge": {
        "steam": {
            "base_tile_id": 288,  # NUM_TILES_IN_PRIMARY + 288
            "num_tiles": 4,
            "anim_folder": "steam",
            "duration_ms": 200,
            "is_secondary": True,
            # From gTilesetAnims_Lavaridge_Steam[]: Frame0-3 linear
            "frame_sequence": [0, 1, 2, 3]
        },
        "lava": {
            "base_tile_id": 160,  # NUM_TILES_IN_PRIMARY + 160
            "num_tiles": 4,
            "anim_folder": "lava",
            "duration_ms": 200,
            "is_secondary": True,
            # From gTilesetAnims_Lavaridge_Cave_Lava[]: Frame0-3 linear
            "frame_sequence": [0, 1, 2, 3]
        }
    },
    "ever_grande": {
        "flowers": {
            "base_tile_id": 224,  # NUM_TILES_IN_PRIMARY + 224
            "num_tiles": 4,
            "anim_folder": "flowers",
            "duration_ms": 200,
            "is_secondary": True
        }
    },
    "pacifidlog": {
        "log_bridges": {
            "base_tile_id": 464,  # NUM_TILES_IN_PRIMARY + 464
            "num_tiles": 30,
            "anim_folder": "log_bridges",
            "duration_ms": 200,
            "is_secondary": True,
            # From gTilesetAnims_Pacifidlog_LogBridges[]: Frame0, Frame1, Frame2, Frame1 (ping-pong)
            "frame_sequence": [0, 1, 2, 1]
        },
        "water_currents": {
            "base_tile_id": 496,  # NUM_TILES_IN_PRIMARY + 496
            "num_tiles": 8,
            "anim_folder": "water_currents",
            "duration_ms": 200,
            "is_secondary": True,
            # From gTilesetAnims_Pacifidlog_WaterCurrents[]: Frame0-7 linear
            "frame_sequence": [0, 1, 2, 3, 4, 5, 6, 7]
        }
    },
    "sootopolis": {
        "stormy_water": {
            "base_tile_id": 240,  # NUM_TILES_IN_PRIMARY + 240
            "num_tiles": 96,
            "anim_folder": "stormy_water",
            "duration_ms": 200,
            "is_secondary": True
        }
    },
    "underwater": {
        "seaweed": {
            "base_tile_id": 496,  # NUM_TILES_IN_PRIMARY + 496
            "num_tiles": 4,
            "anim_folder": "seaweed",
            "duration_ms": 200,
            "is_secondary": True,
            # From gTilesetAnims_Underwater_Seaweed[]: Frame0-3 linear
            "frame_sequence": [0, 1, 2, 3]
        }
    },
    "cave": {
        "lava": {
            "base_tile_id": 416,  # NUM_TILES_IN_PRIMARY + 416
            "num_tiles": 4,
            "anim_folder": "lava",
            "duration_ms": 200,
            "is_secondary": True,
            # From gTilesetAnims_Lavaridge_Cave_Lava[]: Frame0-3 linear
            "frame_sequence": [0, 1, 2, 3]
        }
    },
    "battle_frontier_outside_west": {
        "flag": {
            "base_tile_id": 218,  # NUM_TILES_IN_PRIMARY + 218
            "num_tiles": 6,
            "anim_folder": "flag",
            "duration_ms": 200,
            "is_secondary": True
        }
    },
    "battle_frontier_outside_east": {
        "flag": {
            "base_tile_id": 218,  # NUM_TILES_IN_PRIMARY + 218
            "num_tiles": 6,
            "anim_folder": "flag",
            "duration_ms": 200,
            "is_secondary": True
        }
    },
    "mauville_gym": {
        "electric_gates": {
            "base_tile_id": 144,  # NUM_TILES_IN_PRIMARY + 144
            "num_tiles": 16,
            "anim_folder": "electric_gates",
            "duration_ms": 200,
            "is_secondary": True
        }
    },
    "sootopolis_gym": {
        "side_waterfall": {
            "base_tile_id": 496,  # NUM_TILES_IN_PRIMARY + 496
            "num_tiles": 12,
            "anim_folder": "side_waterfall",
            "duration_ms": 200,
            "is_secondary": True
        },
        "front_waterfall": {
            "base_tile_id": 464,  # NUM_TILES_IN_PRIMARY + 464
            "num_tiles": 20,
            "anim_folder": "front_waterfall",
            "duration_ms": 200,
            "is_secondary": True
        }
    },
    "elite_four": {
        "floor_light": {
            "base_tile_id": 480,  # NUM_TILES_IN_PRIMARY + 480
            "num_tiles": 4,
            "anim_folder": "floor_light",
            "duration_ms": 200,
            "is_secondary": True
        },
        "wall_lights": {
            "base_tile_id": 504,  # NUM_TILES_IN_PRIMARY + 504
            "num_tiles": 1,
            "anim_folder": "wall_lights",
            "duration_ms": 200,
            "is_secondary": True
        }
    },
    "bike_shop": {
        "blinking_lights": {
            "base_tile_id": 496,  # NUM_TILES_IN_PRIMARY + 496
            "num_tiles": 9,
            "anim_folder": "blinking_lights",
            "duration_ms": 200,
            "is_secondary": True
        }
    },
    "battle_pyramid": {
        "torch": {
            "base_tile_id": 151,  # NUM_TILES_IN_PRIMARY + 151
            "num_tiles": 8,
            "anim_folder": "torch",
            "duration_ms": 200,
            "is_secondary": True
        },
        "statue_shadow": {
            "base_tile_id": 135,  # NUM_TILES_IN_PRIMARY + 135
            "num_tiles": 8,
            "anim_folder": "statue_shadow",
            "duration_ms": 200,
            "is_secondary": True
        }
    }
}


class AnimationScanner:
    """Scans anim folders and extracts animation frame data."""
    
    def __init__(self, input_dir: str):
        self.input_dir = Path(input_dir)
    
    def find_anim_folder(self, tileset_name: str, is_secondary: bool = False) -> Optional[Path]:
        """Find the anim folder for a tileset."""
        resolver = TilesetPathResolver(self.input_dir)
        result = resolver.find_tileset_path(tileset_name)
        
        if result:
            category, tileset_dir = result
            # Check if category matches requested type
            if (is_secondary and category == "secondary") or (not is_secondary and category == "primary"):
                anim_path = tileset_dir / "anim"
                if anim_path.exists() and anim_path.is_dir():
                    return anim_path
        
        # Fallback: try direct lookup if resolver didn't find it
        name_variants = [
            camel_to_snake(tileset_name),
            tileset_name.lower(),
            tileset_name.replace("_", "").lower(),
        ]
        
        for tileset_lower in name_variants:
            if is_secondary:
                anim_path = self.input_dir / "data" / "tilesets" / "secondary" / tileset_lower / "anim"
            else:
                anim_path = self.input_dir / "data" / "tilesets" / "primary" / tileset_lower / "anim"
            
            if anim_path.exists() and anim_path.is_dir():
                return anim_path
        
        return None
    
    def scan_animation_frames(self, tileset_name: str, anim_folder_name: str, is_secondary: bool = False) -> List[Path]:
        """Scan for animation frame images in an anim subfolder."""
        anim_folder = self.find_anim_folder(tileset_name, is_secondary)
        if not anim_folder:
            return []
        
        anim_subfolder = anim_folder / anim_folder_name
        if not anim_subfolder.exists():
            return []
        
        # Find all frame images (0.png, 1.png, etc.)
        frames = []
        for frame_file in sorted(anim_subfolder.glob("*.png")):
            # Extract frame number from filename (e.g., "0.png" -> 0)
            try:
                frame_num = int(frame_file.stem)
                frames.append((frame_num, frame_file))
            except ValueError:
                # Skip files that don't have numeric names
                continue
        
        # Sort by frame number and return paths
        frames.sort(key=lambda x: x[0])
        return [path for _, path in frames]
    
    def extract_tiles_from_frame(self, frame_path: Path, base_tile_id: int, num_tiles: int, tile_size: int = 8) -> List[Image.Image]:
        """
        Extract tiles from an animation frame image.

        Animation frames can be:
        - 16x16 images (single metatile) - return as single tile
        - Multiple 8x8 tiles laid out horizontally (wide image)
        - Multiple 8x8 tiles laid out vertically (tall image like water animations)
        - Multiple 8x8 tiles in a 2-column grid (16xN images like water - 16x120 for 30 tiles)
        """
        if not frame_path.exists():
            return []

        try:
            frame_img = Image.open(frame_path)
            # BUGFIX: Handle transparency for palette mode images
            # In GBA, palette index 0 is always transparent
            if frame_img.mode == 'P':
                # Get the original palette index data before conversion
                original_data = list(frame_img.getdata())
                original_width = frame_img.width
                # Set transparency info for palette index 0
                frame_img.info['transparency'] = 0
                # Convert to RGBA - PIL will honor the transparency index
                frame_img = frame_img.convert('RGBA')
                # Manually make pixels that were palette index 0 transparent
                pixels = list(frame_img.getdata())
                new_pixels = []
                for orig_idx, pixel in zip(original_data, pixels):
                    if orig_idx == 0:  # Palette index 0
                        new_pixels.append((pixel[0], pixel[1], pixel[2], 0))
                    else:
                        new_pixels.append(pixel)
                frame_img.putdata(new_pixels)
            elif frame_img.mode != 'RGBA':
                frame_img = frame_img.convert('RGBA')

            # Check if this is a 16x16 image (single metatile)
            if frame_img.width == 16 and frame_img.height == 16:
                # This is a single 16x16 metatile frame - return it directly
                return [frame_img]

            tiles = []

            # For tile-strip animations, always use 8x8 tiles regardless of tile_size parameter
            # The tile_size parameter is used for metatile animations (16x16)
            actual_tile_size = 8

            # Calculate dimensions
            tiles_per_row = frame_img.width // actual_tile_size if frame_img.width >= actual_tile_size else 1
            tiles_per_col = frame_img.height // actual_tile_size if frame_img.height >= actual_tile_size else 1
            total_available_tiles = tiles_per_row * tiles_per_col

            # Determine layout type:
            # 1. Pure vertical (single column): width == 8, height >= num_tiles * 8
            # 2. Pure horizontal (single row): height == 8, width >= num_tiles * 8
            # 3. Multi-column grid: width == 16 (2 cols), height >= (num_tiles / 2) * 8 (e.g., water 16x120)
            # 4. General grid: tiles laid out left-to-right, top-to-bottom

            is_single_column = (tiles_per_row == 1 and tiles_per_col >= num_tiles)
            is_single_row = (tiles_per_col == 1 and tiles_per_row >= num_tiles)
            is_two_column_grid = (tiles_per_row == 2 and total_available_tiles >= num_tiles)
            is_general_grid = (total_available_tiles >= num_tiles)

            if is_single_column:
                # SINGLE COLUMN VERTICAL: tiles stacked top to bottom (8xN image)
                for i in range(num_tiles):
                    x = 0
                    y = i * actual_tile_size
                    if y + actual_tile_size <= frame_img.height:
                        tile = frame_img.crop((x, y, x + actual_tile_size, y + actual_tile_size))
                        tiles.append(tile)
                    else:
                        tiles.append(Image.new('RGBA', (actual_tile_size, actual_tile_size), (0, 0, 0, 0)))
            elif is_single_row:
                # SINGLE ROW HORIZONTAL: tiles side by side (Nx8 image)
                for i in range(num_tiles):
                    x = i * actual_tile_size
                    y = 0
                    if x + actual_tile_size <= frame_img.width:
                        tile = frame_img.crop((x, y, x + actual_tile_size, y + actual_tile_size))
                        tiles.append(tile)
                    else:
                        tiles.append(Image.new('RGBA', (actual_tile_size, actual_tile_size), (0, 0, 0, 0)))
            elif is_two_column_grid or is_general_grid:
                # GRID LAYOUT: tiles laid out left-to-right, then top-to-bottom
                # Water animations: 16x120 = 2 columns x 15 rows = 30 tiles
                # Waterfall: may be similar format
                for i in range(num_tiles):
                    col = i % tiles_per_row
                    row = i // tiles_per_row
                    x = col * actual_tile_size
                    y = row * actual_tile_size
                    if x + actual_tile_size <= frame_img.width and y + actual_tile_size <= frame_img.height:
                        tile = frame_img.crop((x, y, x + actual_tile_size, y + actual_tile_size))
                        tiles.append(tile)
                    else:
                        tiles.append(Image.new('RGBA', (actual_tile_size, actual_tile_size), (0, 0, 0, 0)))
            else:
                # Fallback: try to extract what we can
                logger.warning(f"Frame {frame_path.name} ({frame_img.width}x{frame_img.height}) doesn't fit expected layout for {num_tiles} tiles")
                for i in range(min(num_tiles, total_available_tiles)):
                    col = i % tiles_per_row
                    row = i // tiles_per_row
                    x = col * actual_tile_size
                    y = row * actual_tile_size
                    tile = frame_img.crop((x, y, x + actual_tile_size, y + actual_tile_size))
                    tiles.append(tile)
                # Pad with empty tiles if needed
                while len(tiles) < num_tiles:
                    tiles.append(Image.new('RGBA', (actual_tile_size, actual_tile_size), (0, 0, 0, 0)))

            return tiles
        except Exception as e:
            logger.warning(f"Error extracting tiles from {frame_path}: {e}")
            return []
    
    def get_animations_for_tileset(self, tileset_name: str) -> Dict[str, Dict]:
        """Get animation definitions for a tileset."""
        # Strip common prefixes (gTileset_, Tileset_) before normalizing
        clean_name = tileset_name
        for prefix in ['gTileset_', 'Tileset_', 'g_tileset_']:
            if clean_name.startswith(prefix):
                clean_name = clean_name[len(prefix):]
                break

        # Normalize tileset name: convert CamelCase to snake_case, then lowercase
        tileset_key = camel_to_snake(clean_name).lower()

        # Try exact match first
        if tileset_key in ANIMATION_MAPPINGS:
            return ANIMATION_MAPPINGS[tileset_key]

        # Try case-insensitive match
        for key, value in ANIMATION_MAPPINGS.items():
            if key.lower() == tileset_key:
                return value

        return {}
    
    def extract_all_animation_tiles(
        self,
        tileset_name: str,
        tile_size: int = 8
    ) -> Dict[str, Dict]:
        """
        Extract all animation frames.
        
        Returns:
            Dict mapping animation_name -> {
                "frames": List of frame images (16x16 metatiles or 8x8 tiles),
                "base_tile_id": int,
                "num_tiles": int,
                "duration_ms": int,
                "is_metatile": bool  # True if frames are 16x16 metatiles
            }
        """
        result = {}
        tileset_animations = self.get_animations_for_tileset(tileset_name)
        
        if not tileset_animations:
            return result
        
        for anim_name, anim_def in tileset_animations.items():
            base_tile_id = anim_def["base_tile_id"]
            num_tiles = anim_def["num_tiles"]
            anim_folder_name = anim_def["anim_folder"]
            anim_is_secondary = anim_def.get("is_secondary", False)
            
            # Scan for frame images
            frame_paths = self.scan_animation_frames(tileset_name, anim_folder_name, anim_is_secondary)
            if not frame_paths:
                # Try without is_secondary flag
                frame_paths = self.scan_animation_frames(tileset_name, anim_folder_name, False)
            
            if not frame_paths:
                continue
            
            # Extract frames - check if they're 16x16 metatiles or 8x8 tiles
            frames = []
            is_metatile = False
            for frame_path in frame_paths:
                try:
                    frame_img = Image.open(frame_path)
                    # Check if this is a 16x16 metatile frame
                    if frame_img.width == 16 and frame_img.height == 16:
                        is_metatile = True
                        # BUGFIX: Handle transparency for palette mode images
                        # In GBA, palette index 0 is always transparent
                        if frame_img.mode == 'P':
                            # Get the original palette index data
                            original_data = list(frame_img.getdata())
                            # Set transparency info for palette index 0
                            frame_img.info['transparency'] = 0
                            # Convert to RGBA - PIL will honor the transparency index
                            frame_img = frame_img.convert('RGBA')
                            # Double-check: manually make pixels that were index 0 transparent
                            pixels = list(frame_img.getdata())
                            new_pixels = []
                            for idx, (orig_idx, pixel) in enumerate(zip(original_data, pixels)):
                                if orig_idx == 0:  # Palette index 0
                                    new_pixels.append((pixel[0], pixel[1], pixel[2], 0))
                                else:
                                    new_pixels.append(pixel)
                            frame_img.putdata(new_pixels)
                        elif frame_img.mode != 'RGBA':
                            frame_img = frame_img.convert('RGBA')
                        frames.append(frame_img)
                    else:
                        # Extract 8x8 tiles
                        frame_tiles = self.extract_tiles_from_frame(frame_path, base_tile_id, num_tiles, tile_size)
                        frames.extend(frame_tiles)
                except Exception as e:
                    logger.warning(f"Error loading animation frame {frame_path}: {e}")
                    continue
            
            if frames:
                result[anim_name] = {
                    "frames": frames,
                    "base_tile_id": base_tile_id,
                    "num_tiles": num_tiles,
                    "duration_ms": anim_def.get("duration_ms", 200),
                    "is_metatile": is_metatile,
                    # Frame sequence defines playback order (e.g., [0, 1, 0, 2] for ping-pong)
                    # If not defined, will play linearly (0, 1, 2, ...)
                    "frame_sequence": anim_def.get("frame_sequence", None)
                }
        
        return result
    
    def build_animation_data(
        self,
        tileset_name: str,
        tile_mapping: Dict[Tuple[int, int], int],
        animation_tile_mapping: Dict[int, int],
        tile_size: int = 8
    ) -> List[Dict]:
        """
        Build animation data for Tiled tileset format.
        
        Args:
            tileset_name: Name of the tileset
            tile_mapping: Mapping from (old_tile_id, palette) -> new_tile_id (for base tiles)
            animation_tile_mapping: Mapping from animation_tile_index -> new_tile_id (for animation frame tiles)
        
        Returns:
            List of animation definitions in Tiled format
        """
        animations = []
        tileset_animations = self.get_animations_for_tileset(tileset_name)
        
        if not tileset_animations:
            return animations
        
        # Extract all animation tiles
        anim_data = self.extract_all_animation_tiles(tileset_name, tile_size)
        
        for anim_name, anim_def in tileset_animations.items():
            if anim_name not in anim_data:
                continue
            
            base_tile_id = anim_def["base_tile_id"]
            num_tiles = anim_def["num_tiles"]
            duration_ms = anim_data[anim_name]["duration_ms"]
            frames = anim_data[anim_name]["frames"]
            
            if not frames or not frames[0]:
                continue
            
            # For each tile in the animation range, create an animation entry
            for tile_offset in range(num_tiles):
                current_tile_id = base_tile_id + tile_offset
                
                # Find the new base tile ID in the mapping
                # Try palette 0 first (most common), then try other palettes
                new_base_tile_id = None
                for (old_tid, palette), new_tid in tile_mapping.items():
                    if old_tid == current_tile_id:
                        # Prefer palette 0, but accept any palette if 0 isn't available
                        if palette == 0:
                            new_base_tile_id = new_tid
                            break
                        elif new_base_tile_id is None:
                            # Use first matching tile as fallback
                            new_base_tile_id = new_tid
                
                if new_base_tile_id is None:
                    # Tile not in mapping - this is OK, it means the tile isn't used in any maps
                    # We'll still add the animation frames to the tileset, but won't create an animation entry
                    # unless the tile is actually used
                    continue
                
                # Build animation frames
                animation_frames = []
                for frame_idx, frame_tiles in enumerate(frames):
                    if tile_offset < len(frame_tiles):
                        # Calculate the animation tile index
                        # Format: anim_name_tile_offset_frame_idx
                        anim_tile_key = f"{anim_name}_{tile_offset}_{frame_idx}"
                        # Use a simpler index: base_offset + frame_idx * num_tiles + tile_offset
                        anim_tile_index = base_tile_id * 10000 + frame_idx * 1000 + tile_offset
                        
                        # Find the new tile ID for this animation frame
                        if anim_tile_index in animation_tile_mapping:
                            frame_tile_id = animation_tile_mapping[anim_tile_index]
                            animation_frames.append({
                                "tileid": frame_tile_id - 1,  # Tiled uses 0-based
                                "duration": duration_ms
                            })
                
                if animation_frames:
                    animations.append({
                        "id": new_base_tile_id - 1,  # Tiled uses 0-based indexing
                        "animation": animation_frames
                    })
        
        return animations

