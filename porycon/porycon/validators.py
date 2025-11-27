"""
Input validation functions for pokeemerald map converter.

This module provides validation functions to check inputs at function boundaries,
providing helpful error messages for invalid data.
"""

from pathlib import Path
from typing import Optional
from .constants import NUM_METATILES_IN_PRIMARY, NUM_TILES_IN_PRIMARY_VRAM


def validate_map_dimensions(width: int, height: int, max_size: int = 1000) -> None:
    """
    Validate map dimensions.
    
    Args:
        width: Map width in metatiles
        height: Map height in metatiles
        max_size: Maximum allowed dimension (default: 1000)
    
    Raises:
        ValueError: If dimensions are invalid
        TypeError: If dimensions are not integers
    """
    if not isinstance(width, int):
        raise TypeError(f"Map width must be an integer, got {type(width).__name__}")
    if not isinstance(height, int):
        raise TypeError(f"Map height must be an integer, got {type(height).__name__}")
    
    if width <= 0:
        raise ValueError(f"Invalid map width: {width}. Must be greater than 0.")
    if height <= 0:
        raise ValueError(f"Invalid map height: {height}. Must be greater than 0.")
    
    if width > max_size:
        raise ValueError(f"Map width too large: {width}. Maximum allowed: {max_size}.")
    if height > max_size:
        raise ValueError(f"Map height too large: {height}. Maximum allowed: {max_size}.")


def validate_metatile_id(metatile_id: int, max_metatiles: int = 1024) -> None:
    """
    Validate metatile ID.
    
    Args:
        metatile_id: The metatile ID to validate
        max_metatiles: Maximum allowed metatile ID (default: 1024)
    
    Raises:
        ValueError: If metatile ID is invalid
        TypeError: If metatile ID is not an integer
    """
    if not isinstance(metatile_id, int):
        raise TypeError(f"Metatile ID must be an integer, got {type(metatile_id).__name__}")
    
    if metatile_id < 0:
        raise ValueError(f"Invalid metatile ID: {metatile_id}. Must be >= 0.")
    if metatile_id >= max_metatiles:
        raise ValueError(f"Invalid metatile ID: {metatile_id}. Must be < {max_metatiles}.")


def validate_tile_id(tile_id: int, max_tiles: int = 1024) -> None:
    """
    Validate tile ID.
    
    Args:
        tile_id: The tile ID to validate
        max_tiles: Maximum allowed tile ID (default: 1024)
    
    Raises:
        ValueError: If tile ID is invalid
        TypeError: If tile ID is not an integer
    """
    if not isinstance(tile_id, int):
        raise TypeError(f"Tile ID must be an integer, got {type(tile_id).__name__}")
    
    if tile_id < 0:
        raise ValueError(f"Invalid tile ID: {tile_id}. Must be >= 0.")
    if tile_id >= max_tiles:
        raise ValueError(f"Invalid tile ID: {tile_id}. Must be < {max_tiles}.")


def validate_tileset_name(tileset_name: str) -> None:
    """
    Validate tileset name.
    
    Args:
        tileset_name: The tileset name to validate
    
    Raises:
        ValueError: If tileset name is invalid
        TypeError: If tileset name is not a string
    """
    if not isinstance(tileset_name, str):
        raise TypeError(f"Tileset name must be a string, got {type(tileset_name).__name__}")
    
    if not tileset_name or not tileset_name.strip():
        raise ValueError("Tileset name cannot be empty or whitespace only.")


def validate_map_id(map_id: str) -> None:
    """
    Validate map ID.
    
    Args:
        map_id: The map ID to validate
    
    Raises:
        ValueError: If map ID is invalid
        TypeError: If map ID is not a string
    """
    if not isinstance(map_id, str):
        raise TypeError(f"Map ID must be a string, got {type(map_id).__name__}")
    
    if not map_id or not map_id.strip():
        raise ValueError("Map ID cannot be empty or whitespace only.")


def validate_region(region: str) -> None:
    """
    Validate region name.
    
    Args:
        region: The region name to validate
    
    Raises:
        ValueError: If region name is invalid
        TypeError: If region name is not a string
    """
    if not isinstance(region, str):
        raise TypeError(f"Region must be a string, got {type(region).__name__}")
    
    if not region or not region.strip():
        raise ValueError("Region cannot be empty or whitespace only.")


def validate_path(path: Path, must_exist: bool = False, must_be_file: bool = False, must_be_dir: bool = False) -> None:
    """
    Validate a file path.
    
    Args:
        path: The path to validate
        must_exist: If True, path must exist
        must_be_file: If True, path must be a file
        must_be_dir: If True, path must be a directory
    
    Raises:
        ValueError: If path is invalid
        TypeError: If path is not a Path object
    """
    if not isinstance(path, Path):
        raise TypeError(f"Path must be a Path object, got {type(path).__name__}")
    
    if must_exist and not path.exists():
        raise ValueError(f"Path does not exist: {path}")
    
    if must_be_file and not path.is_file():
        raise ValueError(f"Path is not a file: {path}")
    
    if must_be_dir and not path.is_dir():
        raise ValueError(f"Path is not a directory: {path}")


def validate_non_empty_dict(data: dict, name: str = "data") -> None:
    """
    Validate that a dictionary is not empty.
    
    Args:
        data: The dictionary to validate
        name: Name of the dictionary for error messages
    
    Raises:
        ValueError: If dictionary is empty
        TypeError: If data is not a dictionary
    """
    if not isinstance(data, dict):
        raise TypeError(f"{name} must be a dictionary, got {type(data).__name__}")
    
    if not data:
        raise ValueError(f"{name} cannot be empty.")


def validate_positive_integer(value: int, name: str = "value", allow_zero: bool = False) -> None:
    """
    Validate that a value is a positive integer.
    
    Args:
        value: The value to validate
        name: Name of the value for error messages
        allow_zero: If True, allow zero (default: False)
    
    Raises:
        ValueError: If value is not positive
        TypeError: If value is not an integer
    """
    if not isinstance(value, int):
        raise TypeError(f"{name} must be an integer, got {type(value).__name__}")
    
    if allow_zero:
        if value < 0:
            raise ValueError(f"{name} must be >= 0, got {value}")
    else:
        if value <= 0:
            raise ValueError(f"{name} must be > 0, got {value}")

