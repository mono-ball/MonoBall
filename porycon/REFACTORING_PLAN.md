# Porycon Refactoring Plan

## Overview

This document outlines a phased approach to fixing code smells, bugs, and architecture issues identified in the code analysis. The plan is organized by priority and includes specific tasks, file locations, and implementation steps.

---

## Phase 1: Critical Fixes (Week 1-2)

### Goal: Fix bugs and prevent data corruption

---

### Task 1.1: Fix Race Conditions in Multiprocessing
**Priority:** 🔴 Critical  
**Files:** `porycon/__main__.py`  
**Effort:** 4-6 hours  
**Risk:** Medium

**Current Issue:**
- Lines 100-152: Unsafe concurrent access to shared dictionaries
- Multiple processes updating `all_used_tiles` and `all_used_tiles_with_palettes` without locks

**Solution:**
1. Remove shared state collection from workers
2. Return used tiles as part of worker result
3. Merge results sequentially after all workers complete

**Implementation Steps:**
```python
# In __main__.py, modify convert_single_map return value:
# Change from: (status, map_id, error_msg, world_data, tiled_map, used_tiles_dict, used_tiles_with_palettes_dict)
# To: (status, map_id, error_msg, world_data, tiled_map, used_tiles_dict, used_tiles_with_palettes_dict)

# After all futures complete, merge sequentially:
for future in as_completed(future_to_map):
    # ... existing code ...
    if used_tiles_dict:
        for tileset_name, tile_ids in used_tiles_dict.items():
            if tileset_name not in all_used_tiles:
                all_used_tiles[tileset_name] = set()
            all_used_tiles[tileset_name].update(tile_ids)  # Now safe - sequential
```

**Testing:**
- Run conversion with multiple workers
- Verify no data loss or corruption
- Check that all tiles are collected correctly

---

### Task 1.2: Fix Memory Leaks in Image Caching
**Priority:** 🔴 Critical  
**Files:** `porycon/metatile_renderer.py`  
**Effort:** 2-3 hours  
**Risk:** Low

**Current Issue:**
- Lines 19-20: Caches grow unbounded
- No eviction policy or size limits

**Solution:**
1. Use `functools.lru_cache` with size limits
2. Or implement manual cache with max size and LRU eviction
3. Add cache clearing method for large batch operations

**Implementation Steps:**
```python
from functools import lru_cache
from collections import OrderedDict

class MetatileRenderer:
    def __init__(self, input_dir: str, max_cache_size: int = 50):
        self.input_dir = Path(input_dir)
        self.max_cache_size = max_cache_size
        self._tileset_cache: OrderedDict[str, Image.Image] = OrderedDict()
        self._palette_cache: OrderedDict[str, List] = OrderedDict()
    
    def _get_cached_tileset(self, tileset_name: str) -> Optional[Image.Image]:
        """Get from cache with LRU eviction."""
        if tileset_name in self._tileset_cache:
            # Move to end (most recently used)
            self._tileset_cache.move_to_end(tileset_name)
            return self._tileset_cache[tileset_name]
        return None
    
    def _cache_tileset(self, tileset_name: str, image: Image.Image):
        """Cache with size limit."""
        if len(self._tileset_cache) >= self.max_cache_size:
            # Remove least recently used
            self._tileset_cache.popitem(last=False)
        self._tileset_cache[tileset_name] = image
    
    def clear_cache(self):
        """Clear all caches."""
        self._tileset_cache.clear()
        self._palette_cache.clear()
```

**Testing:**
- Run conversion with many maps
- Monitor memory usage
- Verify cache eviction works

---

### Task 1.3: Fix Index Out of Bounds Bug
**Priority:** 🔴 Critical  
**Files:** `porycon/converter.py`  
**Effort:** 1-2 hours  
**Risk:** Low

**Current Issue:**
- Line 980: Check happens but may be too late
- Need to validate before accessing metatiles array

**Solution:**
1. Add bounds checking before array access
2. Validate metatile_id early in processing loop

**Implementation Steps:**
```python
# In convert_map_with_metatiles, around line 980:
for y in range(height):
    for x in range(width):
        entry = map_entries[y][x]
        metatile_id = extract_metatile_id(entry)
        elevation = extract_elevation(entry)
        
        # Determine which tileset
        if metatile_id < 512:
            metatiles_with_attrs = primary_metatiles_with_attrs
            # ... existing code ...
            actual_metatile_id = metatile_id
        else:
            # ... existing code ...
            actual_metatile_id = metatile_id - 512
        
        # VALIDATE BEFORE ACCESS (add this check)
        start_idx = actual_metatile_id * NUM_TILES_PER_METATILE
        if start_idx + NUM_TILES_PER_METATILE > len(metatiles_with_attrs):
            # Create empty metatile
            key = (actual_metatile_id, tileset_name, layer_type_val)
            if key not in used_metatiles:
                empty_img = Image.new('RGBA', (16, 16), (0, 0, 0, 0))
                used_metatiles[key] = (empty_img, empty_img)
                # ... assign GIDs ...
            continue
        
        # Now safe to access
        metatile_tiles = metatiles_with_attrs[start_idx:start_idx + NUM_TILES_PER_METATILE]
```

**Testing:**
- Test with maps that have out-of-bounds metatile IDs
- Verify graceful handling (empty metatiles)

---

### Task 1.4: Fix Division by Zero Risk
**Priority:** 🔴 Critical  
**Files:** `porycon/tileset_builder.py`, `porycon/converter.py`  
**Effort:** 1 hour  
**Risk:** Low

**Current Issue:**
- Line 203 in tileset_builder.py: Potential division by zero
- Similar issues in converter.py

**Solution:**
1. Add explicit checks before division
2. Provide safe defaults

**Implementation Steps:**
```python
# In tileset_builder.py, around line 203:
if frame_seq:
    actual_frame = frame_seq[frame_num % len(frame_seq)]
else:
    # Safe division with validation
    if num_tiles > 0 and len(frames) > 0:
        frames_per_cycle = len(frames) // num_tiles
        if frames_per_cycle > 0:
            actual_frame = frame_num % frames_per_cycle
        else:
            actual_frame = 0
    else:
        actual_frame = 0
```

**Testing:**
- Test with animations that have 0 tiles
- Test with empty frame arrays

---

## Phase 2: Code Quality & Maintainability (Week 2-3)

### Goal: Improve code structure and reduce technical debt

---

### Task 2.1: Extract Duplicated Code to Utilities
**Priority:** 🟠 High  
**Files:** `porycon/utils.py`, `porycon/converter.py`, `porycon/tileset_builder.py`, `porycon/metatile_renderer.py`, `porycon/animation_scanner.py`  
**Effort:** 3-4 hours  
**Risk:** Low

**Current Issue:**
- `_camel_to_snake()` duplicated in 4+ files
- Similar path resolution logic duplicated

**Solution:**
1. Move `_camel_to_snake()` to `utils.py`
2. Create `TilesetPathResolver` class for path resolution
3. Update all files to use shared utilities

**Implementation Steps:**
```python
# In utils.py, add:
def camel_to_snake(name: str) -> str:
    """Convert CamelCase to snake_case (e.g., 'InsideShip' -> 'inside_ship')."""
    import re
    s1 = re.sub('(.)([A-Z][a-z]+)', r'\1_\2', name)
    s2 = re.sub('([a-z0-9])([A-Z])', r'\1_\2', s1)
    return s2.lower()

class TilesetPathResolver:
    """Centralized tileset path resolution."""
    
    def __init__(self, input_dir: Path):
        self.input_dir = input_dir
    
    def find_tileset_path(self, tileset_name: str) -> Optional[Tuple[str, Path]]:
        """Find tileset directory, returns (category, path) or None."""
        name_variants = [
            camel_to_snake(tileset_name),
            tileset_name.lower(),
            tileset_name.replace("_", "").lower(),
        ]
        
        for tileset_lower in name_variants:
            primary_path = self.input_dir / "data" / "tilesets" / "primary" / tileset_lower
            if primary_path.exists():
                return ("primary", primary_path)
            
            secondary_path = self.input_dir / "data" / "tilesets" / "secondary" / tileset_lower
            if secondary_path.exists():
                return ("secondary", secondary_path)
        
        return None
```

**Update all files:**
- Replace `_camel_to_snake()` calls with `utils.camel_to_snake()`
- Replace path resolution with `TilesetPathResolver`

**Testing:**
- Verify all tileset lookups still work
- Test with various tileset name formats

---

### Task 2.2: Replace Magic Numbers with Constants
**Priority:** 🟠 High  
**Files:** `porycon/metatile.py`, `porycon/converter.py`, `porycon/tileset_builder.py`  
**Effort:** 2-3 hours  
**Risk:** Low

**Current Issue:**
- `512`, `8`, `16`, `0x03FF`, etc. hardcoded throughout

**Solution:**
1. Create constants module
2. Replace all magic numbers

**Implementation Steps:**
```python
# Create porycon/constants.py:
"""Constants for Pokemon Emerald format."""

# Tileset constants
NUM_METATILES_IN_PRIMARY = 512
NUM_TILES_IN_PRIMARY_VRAM = 512

# Tile dimensions
TILE_SIZE = 8  # 8x8 pixels
METATILE_SIZE = 16  # 16x16 pixels (2x2 tiles)
NUM_TILES_PER_METATILE = 8

# Bit masks for metatile data
METATILE_ID_MASK = 0x03FF  # Bits 0-9
COLLISION_MASK = 0x0C00    # Bits 10-11
ELEVATION_MASK = 0xF000    # Bits 12-15
PALETTE_MASK = 0xF000      # Bits 12-15 (same as elevation in different context)

# Flip flags
FLIP_HORIZONTAL = 0x01
FLIP_VERTICAL = 0x02

# Palette constants
NUM_PALETTES_PER_TILESET = 16
PALETTE_COLORS = 16

# Animation constants
DEFAULT_ANIMATION_DURATION_MS = 200
STANDARD_ANIMATION_FRAMES = 8
```

**Update imports:**
```python
# In all files, replace:
from .constants import (
    NUM_METATILES_IN_PRIMARY,
    TILE_SIZE,
    METATILE_SIZE,
    METATILE_ID_MASK,
    # ... etc
)
```

**Testing:**
- Run full conversion
- Verify all values match original behavior

---

### Task 2.3: Break Down `convert_map_with_metatiles()`
**Priority:** 🟠 High  
**Files:** `porycon/converter.py`  
**Effort:** 8-12 hours  
**Risk:** Medium

**Current Issue:**
- Method is 800+ lines
- Too many responsibilities

**Solution:**
Break into smaller methods:
1. `_read_and_validate_map_data()` - Read map.bin and validate
2. `_load_tileset_data()` - Load metatiles and attributes
3. `_process_metatiles()` - Process all metatiles in map
4. `_process_border_metatiles()` - Handle border
5. `_build_map_layers()` - Build layer data
6. `_create_tileset_for_map()` - Create per-map tileset
7. `_add_animations_to_tileset()` - Handle animations
8. `_create_tiled_map_structure()` - Build final map JSON

**Implementation Steps:**
```python
def convert_map_with_metatiles(self, ...):
    """Main entry point - now just orchestrates."""
    # Validate inputs
    layout = self._validate_layout(map_data, layout_data)
    
    # Read map data
    map_entries, width, height = self._read_and_validate_map_data(layout)
    
    # Load tileset data
    tileset_data = self._load_tileset_data(layout)
    
    # Process metatiles
    metatile_data = self._process_metatiles(
        map_entries, width, height, tileset_data
    )
    
    # Process border
    border_data = self._process_border_metatiles(layout, tileset_data)
    
    # Build layers
    layer_data = self._build_map_layers(
        map_entries, width, height, metatile_data, tileset_data
    )
    
    # Create tileset
    tileset_info = self._create_tileset_for_map(
        map_id, region, metatile_data, layer_data
    )
    
    # Add animations
    self._add_animations_to_tileset(tileset_info, tileset_data)
    
    # Create final map
    return self._create_tiled_map_structure(
        map_data, layer_data, tileset_info, border_data, warp_lookup
    )

# Then implement each helper method...
```

**Testing:**
- Test each method independently
- Verify end-to-end conversion still works
- Compare output with original

---

### Task 2.4: Replace Print Statements with Logging
**Priority:** 🟠 High  
**Files:** All files  
**Effort:** 4-6 hours  
**Risk:** Low

**Current Issue:**
- Debug prints throughout code
- No log levels or configuration

**Solution:**
1. Use Python `logging` module
2. Add log levels (DEBUG, INFO, WARNING, ERROR)
3. Remove debug code or gate behind verbose flag

**Implementation Steps:**
```python
# Create porycon/logging_config.py:
import logging
import sys

def setup_logging(verbose: bool = False, debug: bool = False):
    """Configure logging for porycon."""
    level = logging.DEBUG if debug else (logging.INFO if verbose else logging.WARNING)
    
    logging.basicConfig(
        level=level,
        format='%(asctime)s - %(name)s - %(levelname)s - %(message)s',
        handlers=[
            logging.StreamHandler(sys.stdout)
        ]
    )
    
    return logging.getLogger('porycon')

# In __main__.py:
import logging
from .logging_config import setup_logging

def main():
    parser.add_argument('--verbose', '-v', action='store_true')
    parser.add_argument('--debug', '-d', action='store_true')
    args = parser.parse_args()
    
    logger = setup_logging(args.verbose, args.debug)
    
    logger.info("Starting conversion...")
    # Replace all print() with logger.info/warning/error/debug
```

**Update all files:**
- Replace `print()` with appropriate log level
- Remove debug-only prints or gate behind `--debug`

**Testing:**
- Test with `--verbose` and `--debug` flags
- Verify log output is appropriate

---

## Phase 3: Architecture Improvements (Week 3-4)

### Goal: Improve testability and maintainability

---

### Task 3.1: Extract Constants Module
**Priority:** 🟡 Medium  
**Files:** New file `porycon/constants.py`  
**Effort:** 1-2 hours (already done in Task 2.2)  
**Risk:** Low

**Status:** Covered in Task 2.2

---

### Task 3.2: Create MapReader Class
**Priority:** 🟡 Medium  
**Files:** New file `porycon/map_reader.py`  
**Effort:** 4-6 hours  
**Risk:** Medium

**Solution:**
Extract file I/O and parsing logic from `MapConverter`

**Implementation:**
```python
# porycon/map_reader.py
class MapReader:
    """Handles reading and parsing map files."""
    
    def __init__(self, input_dir: Path):
        self.input_dir = input_dir
    
    def read_map_json(self, map_file: Path) -> Dict[str, Any]:
        """Read and parse map.json file."""
        # Move logic from converter
    
    def read_map_bin(self, map_bin_path: Path, width: int, height: int) -> List[List[int]]:
        """Read map.bin file."""
        # Move from metatile.py
    
    def read_metatiles(self, tileset_name: str) -> List[Tuple[int, int, int]]:
        """Read metatiles.bin with attributes."""
        # Move from converter
    
    def read_metatile_attributes(self, tileset_name: str) -> Dict[int, int]:
        """Read metatile_attributes.bin."""
        # Move from converter
```

**Update converter:**
```python
# In converter.py:
def __init__(self, input_dir: str, output_dir: str):
    self.map_reader = MapReader(Path(input_dir))
    # ... rest of init
```

**Testing:**
- Test MapReader independently
- Verify converter still works

---

### Task 3.3: Create MetatileProcessor Class
**Priority:** 🟡 Medium  
**Files:** New file `porycon/metatile_processor.py`  
**Effort:** 6-8 hours  
**Risk:** Medium

**Solution:**
Extract metatile conversion logic

**Implementation:**
```python
# porycon/metatile_processor.py
class MetatileProcessor:
    """Handles metatile to tile conversion."""
    
    def __init__(self, metatile_renderer: MetatileRenderer):
        self.renderer = metatile_renderer
    
    def process_metatile(
        self,
        metatile_id: int,
        tileset_name: str,
        metatiles_with_attrs: List[Tuple[int, int, int]],
        layer_type: MetatileLayerType,
        primary_tileset: str,
        secondary_tileset: str
    ) -> Tuple[Image.Image, Image.Image]:
        """Process a single metatile."""
        # Extract logic from convert_map_with_metatiles
    
    def determine_tileset_for_tile(
        self, tile_id: int, current_tileset: str, primary_tileset: str
    ) -> Tuple[str, int]:
        """Determine which tileset a tile belongs to."""
        # Extract from converter
```

**Testing:**
- Unit test metatile processing
- Verify output matches original

---

### Task 3.4: Add Input Validation
**Priority:** 🟡 Medium  
**Files:** All public methods  
**Effort:** 4-6 hours  
**Risk:** Low

**Solution:**
Add validation at function boundaries

**Implementation:**
```python
# Create porycon/validators.py:
def validate_map_dimensions(width: int, height: int):
    """Validate map dimensions."""
    if width <= 0 or height <= 0:
        raise ValueError(f"Invalid map dimensions: {width}x{height}")
    if width > 1000 or height > 1000:
        raise ValueError(f"Map dimensions too large: {width}x{height}")

def validate_metatile_id(metatile_id: int, max_metatiles: int = 1024):
    """Validate metatile ID."""
    if metatile_id < 0 or metatile_id >= max_metatiles:
        raise ValueError(f"Invalid metatile ID: {metatile_id}")

# Add to all public methods:
def convert_map_with_metatiles(self, map_id: str, map_data: Dict, ...):
    validate_map_dimensions(layout["width"], layout["height"])
    # ... rest of method
```

**Testing:**
- Test with invalid inputs
- Verify helpful error messages

---

### Task 3.5: Standardize Path Handling
**Priority:** 🟡 Medium  
**Files:** All files  
**Effort:** 3-4 hours  
**Risk:** Low

**Solution:**
1. Use `Path` objects consistently
2. Create path utilities
3. Remove string path handling

**Implementation:**
```python
# In utils.py:
def resolve_path(path: Union[str, Path], base: Optional[Path] = None) -> Path:
    """Resolve path to absolute Path object."""
    if isinstance(path, str):
        path = Path(path)
    
    if path.is_absolute():
        return path.resolve()
    
    if base:
        return (base / path).resolve()
    
    return path.resolve()

# Update all functions to accept Path and return Path
```

**Testing:**
- Test with various path formats
- Verify cross-platform compatibility

---

## Phase 4: Testing & Documentation (Week 4-5)

### Goal: Add tests and improve documentation

---

### Task 4.1: Add Unit Tests
**Priority:** 🟡 Medium  
**Files:** New directory `tests/`  
**Effort:** 12-16 hours  
**Risk:** Low

**Solution:**
Create test suite using pytest

**Structure:**
```
tests/
├── __init__.py
├── conftest.py
├── test_metatile.py
├── test_converter.py
├── test_tileset_builder.py
├── test_utils.py
└── fixtures/
    ├── sample_map.json
    ├── sample_metatiles.bin
    └── sample_tileset.png
```

**Key Tests:**
1. Metatile conversion
2. Tileset building
3. Path resolution
4. Error handling
5. Edge cases (empty maps, invalid data)

**Implementation:**
```python
# tests/test_metatile.py
import pytest
from porycon.metatile import (
    unpack_metatile_data,
    split_metatile_to_layers,
    MetatileLayerType
)

def test_unpack_metatile_data():
    metatile_data = [1, 2, 3, 4, 5, 6, 7, 8] * 10
    result = unpack_metatile_data(0, metatile_data)
    assert result == [1, 2, 3, 4, 5, 6, 7, 8]

def test_unpack_metatile_data_out_of_bounds():
    metatile_data = [1, 2, 3, 4]
    result = unpack_metatile_data(100, metatile_data)
    assert result == [0] * 8
```

---

### Task 4.2: Improve Documentation
**Priority:** 🟢 Low  
**Files:** All files  
**Effort:** 4-6 hours  
**Risk:** Low

**Solution:**
1. Add docstrings to all public methods
2. Document complex algorithms
3. Add type hints where missing
4. Update README with architecture info

**Format:**
```python
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
    2. Rendering each metatile as 16x16 images
    3. Creating a per-map tileset with only used metatiles
    4. Adding animations for animated tiles
    5. Converting events (warps, triggers, etc.) to Tiled objects
    
    Args:
        map_id: Unique identifier for the map (e.g., "MAP_LITTLEROOT_TOWN")
        map_data: Parsed map.json data
        layout_data: Dictionary of layout_id -> layout info
        region: Region name for organizing output (e.g., "hoenn")
        warp_lookup: Optional lookup table for warp destinations
    
    Returns:
        Tiled map JSON structure, or None if conversion fails
    
    Raises:
        ValueError: If map dimensions are invalid
        FileNotFoundError: If required files are missing
    """
```

---

## Implementation Timeline

### Week 1: Critical Fixes
- Day 1-2: Task 1.1 (Race conditions)
- Day 3: Task 1.2 (Memory leaks)
- Day 4: Task 1.3 (Index bounds)
- Day 5: Task 1.4 (Division by zero)

### Week 2: Code Quality
- Day 1-2: Task 2.1 (Extract duplicates)
- Day 3: Task 2.2 (Constants)
- Day 4-5: Task 2.3 (Break down method) - Part 1

### Week 3: Code Quality & Architecture
- Day 1-2: Task 2.3 (Break down method) - Part 2
- Day 3: Task 2.4 (Logging)
- Day 4-5: Task 3.2 (MapReader)

### Week 4: Architecture
- Day 1-2: Task 3.3 (MetatileProcessor)
- Day 3: Task 3.4 (Input validation)
- Day 4: Task 3.5 (Path handling)
- Day 5: Task 4.2 (Documentation)

### Week 5: Testing
- Day 1-3: Task 4.1 (Unit tests)
- Day 4-5: Integration testing and bug fixes

---

## Risk Mitigation

### High Risk Tasks
1. **Task 2.3 (Break down method)**: Large refactoring
   - **Mitigation**: 
     - Do incrementally, one method at a time
     - Test after each extraction
     - Keep original method as fallback

2. **Task 3.2/3.3 (Extract classes)**: Architecture changes
   - **Mitigation**:
     - Create new classes alongside old code
     - Gradually migrate
     - Keep old code until new code is proven

### Testing Strategy
- After each task: Run full conversion on test dataset
- Compare outputs: Ensure no functional changes
- Regression tests: Test edge cases after each change

---

## Success Criteria

### Phase 1 Complete When:
- ✅ No race conditions in multiprocessing
- ✅ Memory usage stable during long conversions
- ✅ No crashes on invalid input
- ✅ All critical bugs fixed

### Phase 2 Complete When:
- ✅ No code duplication
- ✅ All magic numbers replaced
- ✅ Methods < 100 lines
- ✅ Proper logging in place

### Phase 3 Complete When:
- ✅ Classes have single responsibility
- ✅ Code is testable
- ✅ Path handling is consistent
- ✅ Input validation in place

### Phase 4 Complete When:
- ✅ >80% test coverage
- ✅ All public APIs documented
- ✅ README updated

---

## Notes

- **Incremental Approach**: Each task should be done incrementally with testing
- **Backward Compatibility**: Ensure output format doesn't change
- **Performance**: Monitor performance after each change
- **Code Review**: Review each phase before moving to next

---

## Quick Reference: Task Checklist

### Phase 1 (Critical)
- [ ] Task 1.1: Fix race conditions
- [ ] Task 1.2: Fix memory leaks
- [ ] Task 1.3: Fix index bounds
- [ ] Task 1.4: Fix division by zero

### Phase 2 (Code Quality)
- [ ] Task 2.1: Extract duplicates
- [ ] Task 2.2: Replace magic numbers
- [ ] Task 2.3: Break down method
- [ ] Task 2.4: Add logging

### Phase 3 (Architecture)
- [ ] Task 3.2: Create MapReader
- [ ] Task 3.3: Create MetatileProcessor
- [ ] Task 3.4: Add validation
- [ ] Task 3.5: Standardize paths

### Phase 4 (Testing)
- [ ] Task 4.1: Add unit tests
- [ ] Task 4.2: Improve docs

---

**Last Updated:** [Date]  
**Status:** Planning Phase

