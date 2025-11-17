# Tile Behavior Scripts Evaluation - Pokemon Emerald

This document provides a comprehensive evaluation of all tile behavior scripts (metatile behaviors) used in Pokemon Emerald.

## Overview

Pokemon Emerald uses a metatile behavior system where each tile can have a behavior value (0-244) that determines:
- Collision/passability
- Wild encounter eligibility
- Surfability
- Special interactions (doors, warps, etc.)
- Forced movement (sliding, currents, etc.)
- Visual effects (reflections, ripples)

**Total Behaviors:** 245 (0-244, where 245 = NUM_METATILE_BEHAVIORS = invalid)

---

## Behavior Categories

### 1. Basic Terrain Types (0-42)

| ID | Name | Flags | Description |
|----|------|-------|-------------|
| 0x00 | `MB_NORMAL` | UNUSED | Standard walkable tile |
| 0x01 | `MB_SECRET_BASE_WALL` | UNUSED | Secret base wall tile |
| 0x02 | `MB_TALL_GRASS` | UNUSED + ENCOUNTERS | Tall grass (cuttable, encounters) |
| 0x03 | `MB_LONG_GRASS` | UNUSED + ENCOUNTERS | Long grass (cuttable, encounters, no running) |
| 0x04 | `MB_UNUSED_04` | - | Unused behavior |
| 0x05 | `MB_UNUSED_05` | ENCOUNTERS | Unused (has encounter flag) |
| 0x06 | `MB_DEEP_SAND` | UNUSED + ENCOUNTERS | Deep sand (encounters) |
| 0x07 | `MB_SHORT_GRASS` | UNUSED | Short grass (no encounters) |
| 0x08 | `MB_CAVE` | UNUSED + ENCOUNTERS | Cave floor (encounters) |
| 0x09 | `MB_LONG_GRASS_SOUTH_EDGE` | UNUSED | Edge of long grass (cuttable) |
| 0x0A | `MB_NO_RUNNING` | UNUSED | Tile that disables running |
| 0x0B | `MB_INDOOR_ENCOUNTER` | UNUSED + ENCOUNTERS | Indoor encounter tile |
| 0x0C | `MB_MOUNTAIN_TOP` | UNUSED | Mountain top terrain |
| 0x0D | `MB_BATTLE_PYRAMID_WARP` | UNUSED | Battle Pyramid warp tile |
| 0x0E | `MB_MOSSDEEP_GYM_WARP` | UNUSED | Mossdeep Gym warp tile |
| 0x0F | `MB_MT_PYRE_HOLE` | UNUSED | Mt. Pyre hole (warp) |
| 0x10 | `MB_POND_WATER` | UNUSED + SURFABLE + ENCOUNTERS | Pond water (surfable, fishable, encounters, ripples) |
| 0x11 | `MB_INTERIOR_DEEP_WATER` | UNUSED + SURFABLE + ENCOUNTERS | Interior deep water (surfable, diveable, encounters) |
| 0x12 | `MB_DEEP_WATER` | UNUSED + SURFABLE + ENCOUNTERS | Deep water (surfable, diveable, encounters) |
| 0x13 | `MB_WATERFALL` | UNUSED + SURFABLE | Waterfall (surfable, forced movement) |
| 0x14 | `MB_SOOTOPOLIS_DEEP_WATER` | UNUSED + SURFABLE | Sootopolis deep water (surfable, reflective, ripples) |
| 0x15 | `MB_OCEAN_WATER` | UNUSED + SURFABLE + ENCOUNTERS | Ocean water (surfable, fishable, encounters) |
| 0x16 | `MB_PUDDLE` | UNUSED | Puddle (reflective, ripples) |
| 0x17 | `MB_SHALLOW_WATER` | UNUSED | Shallow flowing water |
| 0x18 | `MB_UNUSED_SOOTOPOLIS_DEEP_WATER` | - | Unused Sootopolis water variant |
| 0x19 | `MB_NO_SURFACING` | UNUSED + SURFABLE | Water where player cannot surface (dive-only) |
| 0x1A | `MB_UNUSED_SOOTOPOLIS_DEEP_WATER_2` | - | Unused Sootopolis water variant (reflective) |
| 0x1B | `MB_STAIRS_OUTSIDE_ABANDONED_SHIP` | UNUSED | Stairs (also acts as north arrow warp) |
| 0x1C | `MB_SHOAL_CAVE_ENTRANCE` | UNUSED | Shoal Cave entrance (also acts as south arrow warp) |
| 0x1D | `MB_UNUSED_1D` | - | Unused |
| 0x1E | `MB_UNUSED_1E` | - | Unused |
| 0x1F | `MB_UNUSED_1F` | - | Unused |
| 0x20 | `MB_ICE` | UNUSED | Ice tile (reflective, forced movement/sliding) |
| 0x21 | `MB_SAND` | UNUSED | Sand terrain |
| 0x22 | `MB_SEAWEED` | UNUSED + SURFABLE + ENCOUNTERS | Seaweed (surfable, encounters) |
| 0x23 | `MB_UNUSED_23` | UNUSED | Unused |
| 0x24 | `MB_ASHGRASS` | UNUSED + ENCOUNTERS | Ash grass (cuttable, encounters) |
| 0x25 | `MB_FOOTPRINTS` | UNUSED + ENCOUNTERS | Footprints tile (not used by any metatiles) |
| 0x26 | `MB_THIN_ICE` | UNUSED | Thin ice (can break) |
| 0x27 | `MB_CRACKED_ICE` | UNUSED | Cracked ice |
| 0x28 | `MB_HOT_SPRINGS` | UNUSED | Hot springs (no running) |
| 0x29 | `MB_LAVARIDGE_GYM_B1F_WARP` | UNUSED | Lavaridge Gym B1F warp |
| 0x2A | `MB_SEAWEED_NO_SURFACING` | UNUSED + SURFABLE + ENCOUNTERS | Seaweed (dive-only, cannot surface) |
| 0x2B | `MB_REFLECTION_UNDER_BRIDGE` | UNUSED | Reflection under bridge |
| 0x2C | `MB_UNUSED_2C` | - | Unused |
| 0x2D | `MB_UNUSED_2D` | - | Unused |
| 0x2E | `MB_UNUSED_2E` | - | Unused |
| 0x2F | `MB_UNUSED_2F` | - | Unused |

### 2. Collision/Impassable Tiles (0x33-0x3C)

| ID | Name | Flags | Description |
|----|------|-------|-------------|
| 0x33 | `MB_IMPASSABLE_EAST` | UNUSED | Blocks movement from east |
| 0x34 | `MB_IMPASSABLE_WEST` | UNUSED | Blocks movement from west |
| 0x35 | `MB_IMPASSABLE_NORTH` | UNUSED | Blocks movement from north |
| 0x36 | `MB_IMPASSABLE_SOUTH` | UNUSED | Blocks movement from south |
| 0x37 | `MB_IMPASSABLE_NORTHEAST` | UNUSED | Blocks movement from northeast |
| 0x38 | `MB_IMPASSABLE_NORTHWEST` | UNUSED | Blocks movement from northwest |
| 0x39 | `MB_IMPASSABLE_SOUTHEAST` | UNUSED | Blocks movement from southeast |
| 0x3A | `MB_IMPASSABLE_SOUTHWEST` | UNUSED | Blocks movement from southwest |
| 0xC5 | `MB_IMPASSABLE_SOUTH_AND_NORTH` | UNUSED | Blocks movement from both north and south |
| 0xC6 | `MB_IMPASSABLE_WEST_AND_EAST` | UNUSED | Blocks movement from both east and west |

### 3. Jump Tiles (0x3B-0x42)

| ID | Name | Flags | Description |
|----|------|-------|-------------|
| 0x3B | `MB_JUMP_EAST` | UNUSED | Forces jump east |
| 0x3C | `MB_JUMP_WEST` | UNUSED | Forces jump west |
| 0x3D | `MB_JUMP_NORTH` | UNUSED | Forces jump north |
| 0x3E | `MB_JUMP_SOUTH` | UNUSED | Forces jump south |
| 0x3F | `MB_JUMP_NORTHEAST` | UNUSED | Forces jump northeast |
| 0x40 | `MB_JUMP_NORTHWEST` | UNUSED | Forces jump northwest |
| 0x41 | `MB_JUMP_SOUTHEAST` | UNUSED | Forces jump southeast |
| 0x42 | `MB_JUMP_SOUTHWEST` | UNUSED | Forces jump southwest |

### 4. Forced Movement Tiles (0x43-0x4D, 0x55-0x58)

| ID | Name | Flags | Description |
|----|------|-------|-------------|
| 0x43 | `MB_WALK_EAST` | UNUSED | Forces walk east |
| 0x44 | `MB_WALK_WEST` | UNUSED | Forces walk west |
| 0x45 | `MB_WALK_NORTH` | UNUSED | Forces walk north |
| 0x46 | `MB_WALK_SOUTH` | UNUSED | Forces walk south |
| 0x47 | `MB_SLIDE_EAST` | UNUSED | Forces slide east (ice-like) |
| 0x48 | `MB_SLIDE_WEST` | UNUSED | Forces slide west |
| 0x49 | `MB_SLIDE_NORTH` | UNUSED | Forces slide north |
| 0x4A | `MB_SLIDE_SOUTH` | UNUSED | Forces slide south |
| 0x4B | `MB_TRICK_HOUSE_PUZZLE_8_FLOOR` | UNUSED | Trick House puzzle slippery floor |
| 0x4C | `MB_UNUSED_49` | UNUSED | Unused |
| 0x4D | `MB_UNUSED_4A` | UNUSED | Unused |
| 0x4E | `MB_UNUSED_4B` | UNUSED | Unused |
| 0x4F | `MB_UNUSED_4C` | UNUSED | Unused |
| 0x50 | `MB_UNUSED_4D` | UNUSED | Unused |
| 0x51 | `MB_UNUSED_4E` | UNUSED | Unused |
| 0x52 | `MB_UNUSED_4F` | UNUSED | Unused |
| 0x55 | `MB_EASTWARD_CURRENT` | UNUSED + SURFABLE | Water current pushing east (surfable, fishable) |
| 0x56 | `MB_WESTWARD_CURRENT` | UNUSED + SURFABLE | Water current pushing west (surfable, fishable) |
| 0x57 | `MB_NORTHWARD_CURRENT` | UNUSED + SURFABLE | Water current pushing north (surfable, fishable) |
| 0x58 | `MB_SOUTHWARD_CURRENT` | UNUSED + SURFABLE | Water current pushing south (surfable, fishable) |

### 5. Doors and Warps (0x5F-0x73)

| ID | Name | Flags | Description |
|----|------|-------|-------------|
| 0x5F | `MB_NON_ANIMATED_DOOR` | UNUSED | Non-animated door (instant warp) |
| 0x60 | `MB_LADDER` | UNUSED | Ladder (warp up/down) |
| 0x61 | `MB_EAST_ARROW_WARP` | UNUSED | Arrow warp pointing east |
| 0x62 | `MB_WEST_ARROW_WARP` | UNUSED | Arrow warp pointing west |
| 0x63 | `MB_NORTH_ARROW_WARP` | UNUSED | Arrow warp pointing north |
| 0x64 | `MB_SOUTH_ARROW_WARP` | UNUSED | Arrow warp pointing south |
| 0x65 | `MB_CRACKED_FLOOR_HOLE` | UNUSED | Cracked floor hole (warp down) |
| 0x66 | `MB_AQUA_HIDEOUT_WARP` | UNUSED | Aqua Hideout warp tile |
| 0x67 | `MB_LAVARIDGE_GYM_1F_WARP` | UNUSED | Lavaridge Gym 1F warp |
| 0x68 | `MB_ANIMATED_DOOR` | UNUSED | Animated door (opens before warp) |
| 0x69 | `MB_UP_ESCALATOR` | UNUSED | Up escalator |
| 0x6A | `MB_DOWN_ESCALATOR` | UNUSED | Down escalator |
| 0x6B | `MB_WATER_DOOR` | UNUSED + SURFABLE | Water door (surfable, bug: allows emergence) |
| 0x6C | `MB_WATER_SOUTH_ARROW_WARP` | UNUSED + SURFABLE | Water south arrow warp (surfable) |
| 0x6D | `MB_DEEP_SOUTH_WARP` | UNUSED | Deep south warp (non-animated door variant) |
| 0x6E | `MB_UNUSED_6F` | UNUSED + SURFABLE | Unused (surfable) |
| 0x8E | `MB_PETALBURG_GYM_DOOR` | UNUSED | Petalburg Gym door |
| 0x8F | `MB_CLOSED_SOOTOPOLIS_DOOR` | UNUSED | Closed Sootopolis door |
| 0x90 | `MB_TRICK_HOUSE_PUZZLE_DOOR` | UNUSED | Trick House puzzle door |
| 0xE8 | `MB_SKY_PILLAR_CLOSED_DOOR` | UNUSED | Sky Pillar closed door |

### 6. Bridges (0x6F-0x7C)

| ID | Name | Flags | Description |
|----|------|-------|-------------|
| 0x6F | `MB_BRIDGE_OVER_OCEAN` | UNUSED | Bridge over ocean (also used for Union Room warp) |
| 0x70 | `MB_BRIDGE_OVER_POND_LOW` | UNUSED | Low bridge over pond |
| 0x71 | `MB_BRIDGE_OVER_POND_MED` | UNUSED | Medium bridge over pond |
| 0x72 | `MB_BRIDGE_OVER_POND_HIGH` | UNUSED | High bridge over pond |
| 0x73 | `MB_PACIFIDLOG_VERTICAL_LOG_TOP` | UNUSED | Pacifidlog vertical log top (no running) |
| 0x74 | `MB_PACIFIDLOG_VERTICAL_LOG_BOTTOM` | UNUSED | Pacifidlog vertical log bottom (no running) |
| 0x75 | `MB_PACIFIDLOG_HORIZONTAL_LOG_LEFT` | UNUSED | Pacifidlog horizontal log left (no running) |
| 0x76 | `MB_PACIFIDLOG_HORIZONTAL_LOG_RIGHT` | UNUSED | Pacifidlog horizontal log right (no running) |
| 0x77 | `MB_FORTREE_BRIDGE` | UNUSED | Fortree bridge |
| 0x78 | `MB_UNUSED_79` | UNUSED | Unused |
| 0x79 | `MB_BRIDGE_OVER_POND_MED_EDGE_1` | UNUSED | Medium bridge edge variant 1 |
| 0x7A | `MB_BRIDGE_OVER_POND_MED_EDGE_2` | UNUSED | Medium bridge edge variant 2 |
| 0x7B | `MB_BRIDGE_OVER_POND_HIGH_EDGE_1` | UNUSED | High bridge edge variant 1 |
| 0x7C | `MB_BRIDGE_OVER_POND_HIGH_EDGE_2` | UNUSED | High bridge edge variant 2 |
| 0x7D | `MB_UNUSED_BRIDGE` | UNUSED | Unused bridge |
| 0x7E | `MB_BIKE_BRIDGE_OVER_BARRIER` | UNUSED | Bike bridge over barrier |

### 7. Interactive Objects (0x7F-0x8D)

| ID | Name | Flags | Description |
|----|------|-------|-------------|
| 0x7F | `MB_COUNTER` | UNUSED | Shop counter (interaction) |
| 0x80 | `MB_UNUSED_81` | UNUSED | Unused |
| 0x81 | `MB_UNUSED_82` | UNUSED | Unused |
| 0x82 | `MB_PC` | UNUSED | PC (interaction) |
| 0x83 | `MB_CABLE_BOX_RESULTS_1` | UNUSED | Cable box results monitor 1 |
| 0x84 | `MB_REGION_MAP` | UNUSED | Region map (interaction) |
| 0x85 | `MB_TELEVISION` | UNUSED | Television (interaction) |
| 0x86 | `MB_POKEBLOCK_FEEDER` | UNUSED | Pokeblock feeder (interaction) |
| 0x87 | `MB_UNUSED_88` | UNUSED | Unused |
| 0x88 | `MB_SLOT_MACHINE` | UNUSED | Slot machine (interaction) |
| 0x89 | `MB_ROULETTE` | UNUSED | Roulette (unused interaction) |
| 0x8A | `MB_RUNNING_SHOES_INSTRUCTION` | UNUSED | Running shoes instruction tile |
| 0x8B | `MB_QUESTIONNAIRE` | UNUSED | Questionnaire (interaction) |
| 0x8C | `MB_CABLE_BOX_RESULTS_2` | UNUSED | Cable box results monitor 2 |
| 0xE7 | `MB_WIRELESS_BOX_RESULTS` | UNUSED | Wireless box results monitor |
| 0xE9 | `MB_TRAINER_HILL_TIMER` | UNUSED | Trainer Hill timer |

### 8. Secret Base Tiles (0x8D-0xC7)

| ID | Name | Flags | Description |
|----|------|-------|-------------|
| 0x8D | `MB_SECRET_BASE_SPOT_RED_CAVE` | UNUSED | Secret base spot (red cave, closed) |
| 0x8E | `MB_SECRET_BASE_SPOT_RED_CAVE_OPEN` | UNUSED | Secret base spot (red cave, open) |
| 0x8F | `MB_SECRET_BASE_SPOT_BROWN_CAVE` | UNUSED | Secret base spot (brown cave, closed) |
| 0x90 | `MB_SECRET_BASE_SPOT_BROWN_CAVE_OPEN` | UNUSED | Secret base spot (brown cave, open) |
| 0x91 | `MB_SECRET_BASE_SPOT_YELLOW_CAVE` | UNUSED | Secret base spot (yellow cave, closed) |
| 0x92 | `MB_SECRET_BASE_SPOT_YELLOW_CAVE_OPEN` | UNUSED | Secret base spot (yellow cave, open) |
| 0x93 | `MB_SECRET_BASE_SPOT_TREE_LEFT` | UNUSED | Secret base spot (tree left, closed) |
| 0x94 | `MB_SECRET_BASE_SPOT_TREE_LEFT_OPEN` | UNUSED | Secret base spot (tree left, open) |
| 0x95 | `MB_SECRET_BASE_SPOT_SHRUB` | UNUSED | Secret base spot (shrub, closed) |
| 0x96 | `MB_SECRET_BASE_SPOT_SHRUB_OPEN` | UNUSED | Secret base spot (shrub, open) |
| 0x97 | `MB_SECRET_BASE_SPOT_BLUE_CAVE` | UNUSED | Secret base spot (blue cave, closed) |
| 0x98 | `MB_SECRET_BASE_SPOT_BLUE_CAVE_OPEN` | UNUSED | Secret base spot (blue cave, open) |
| 0x99 | `MB_SECRET_BASE_SPOT_TREE_RIGHT` | UNUSED | Secret base spot (tree right, closed) |
| 0x9A | `MB_SECRET_BASE_SPOT_TREE_RIGHT_OPEN` | UNUSED | Secret base spot (tree right, open) |
| 0x9B | `MB_UNUSED_9E` | - | Unused |
| 0x9C | `MB_UNUSED_9F` | - | Unused |
| 0x9D | `MB_BERRY_TREE_SOIL` | UNUSED | Berry tree soil |
| 0x9E-0xA7 | `MB_UNUSED_A1-AF` | - | 11 unused behaviors |
| 0xB1 | `MB_SECRET_BASE_PC` | UNUSED | Secret base PC |
| 0xB2 | `MB_SECRET_BASE_REGISTER_PC` | UNUSED | Secret base register PC (record mixing) |
| 0xB3 | `MB_SECRET_BASE_SCENERY` | UNUSED | Secret base scenery floor |
| 0xB4 | `MB_SECRET_BASE_TRAINER_SPOT` | UNUSED | Secret base trainer spot floor |
| 0xB5 | `MB_SECRET_BASE_DECORATION` | UNUSED | Secret base decoration |
| 0xB6 | `MB_HOLDS_SMALL_DECORATION` | UNUSED | Holds small decoration |
| 0xB7 | `MB_SECRET_BASE_NORTH_WALL` | UNUSED | Secret base north wall |
| 0xB8 | `MB_SECRET_BASE_BALLOON` | UNUSED | Secret base balloon |
| 0xB9 | `MB_SECRET_BASE_IMPASSABLE` | UNUSED | Secret base impassable tile |
| 0xBA | `MB_SECRET_BASE_GLITTER_MAT` | UNUSED | Secret base glitter mat (forced movement) |
| 0xBB | `MB_SECRET_BASE_JUMP_MAT` | UNUSED | Secret base jump mat (forced movement) |
| 0xBC | `MB_SECRET_BASE_SPIN_MAT` | UNUSED | Secret base spin mat (forced movement) |
| 0xBD | `MB_SECRET_BASE_SOUND_MAT` | UNUSED | Secret base sound mat |
| 0xBE | `MB_SECRET_BASE_BREAKABLE_DOOR` | UNUSED | Secret base breakable door |
| 0xBF | `MB_SECRET_BASE_SAND_ORNAMENT` | UNUSED | Secret base sand ornament |
| 0xC1 | `MB_HOLDS_LARGE_DECORATION` | UNUSED | Holds large decoration |
| 0xC2 | `MB_SECRET_BASE_TV_SHIELD` | UNUSED | Secret base TV shield |
| 0xC3 | `MB_PLAYER_ROOM_PC_ON` | UNUSED | Player room PC (on) |
| 0xC4 | `MB_SECRET_BASE_DECORATION_BASE` | UNUSED | Secret base decoration base |
| 0xC5 | `MB_SECRET_BASE_POSTER` | UNUSED | Secret base poster |
| 0xC7 | `MB_SECRET_BASE_HOLE` | UNUSED | Secret base hole |

### 9. Special Terrain (0xCF-0xDB)

| ID | Name | Flags | Description |
|----|------|-------|-------------|
| 0xCF | `MB_MUDDY_SLOPE` | UNUSED | Muddy slope (forced movement) |
| 0xD0 | `MB_BUMPY_SLOPE` | UNUSED | Bumpy slope |
| 0xD1 | `MB_CRACKED_FLOOR` | UNUSED | Cracked floor (forced movement) |
| 0xD2 | `MB_ISOLATED_VERTICAL_RAIL` | UNUSED | Isolated vertical rail |
| 0xD3 | `MB_ISOLATED_HORIZONTAL_RAIL` | UNUSED | Isolated horizontal rail |
| 0xD4 | `MB_VERTICAL_RAIL` | UNUSED | Vertical rail |
| 0xD5 | `MB_HORIZONTAL_RAIL` | UNUSED | Horizontal rail |

### 10. Bookshelves and Furniture (0xE0-0xE6)

| ID | Name | Flags | Description |
|----|------|-------|-------------|
| 0xE0 | `MB_PICTURE_BOOK_SHELF` | UNUSED | Picture book shelf (interaction) |
| 0xE1 | `MB_BOOKSHELF` | UNUSED | Bookshelf (interaction) |
| 0xE2 | `MB_POKEMON_CENTER_BOOKSHELF` | UNUSED | Pokemon Center bookshelf (interaction) |
| 0xE3 | `MB_VASE` | UNUSED | Vase (interaction) |
| 0xE4 | `MB_TRASH_CAN` | UNUSED | Trash can (interaction) |
| 0xE5 | `MB_SHOP_SHELF` | UNUSED | Shop shelf (interaction) |
| 0xE6 | `MB_BLUEPRINT` | UNUSED | Blueprint (interaction) |

---

## Flag System

Each behavior has flags stored in `sTileBitAttributes`:

- **TILE_FLAG_HAS_ENCOUNTERS (0x01)**: Tile can trigger wild Pokemon encounters
- **TILE_FLAG_SURFABLE (0x02)**: Tile is surfable (requires Surf HM)
- **TILE_FLAG_UNUSED (0x04)**: Set on most traversable tiles (set but never read)

### Behaviors with Encounters Flag:
- `MB_TALL_GRASS`, `MB_LONG_GRASS`, `MB_UNUSED_05`, `MB_DEEP_SAND`, `MB_CAVE`, `MB_INDOOR_ENCOUNTER`
- `MB_POND_WATER`, `MB_INTERIOR_DEEP_WATER`, `MB_DEEP_WATER`, `MB_OCEAN_WATER`
- `MB_SEAWEED`, `MB_ASHGRASS`, `MB_FOOTPRINTS`, `MB_SEAWEED_NO_SURFACING`

### Behaviors with Surfable Flag:
- `MB_POND_WATER`, `MB_INTERIOR_DEEP_WATER`, `MB_DEEP_WATER`, `MB_WATERFALL`
- `MB_SOOTOPOLIS_DEEP_WATER`, `MB_OCEAN_WATER`, `MB_NO_SURFACING`
- `MB_SEAWEED`, `MB_SEAWEED_NO_SURFACING`
- `MB_EASTWARD_CURRENT`, `MB_WESTWARD_CURRENT`, `MB_NORTHWARD_CURRENT`, `MB_SOUTHWARD_CURRENT`
- `MB_WATER_DOOR`, `MB_WATER_SOUTH_ARROW_WARP`, `MB_UNUSED_6F`

---

## Special Behavior Functions

The codebase includes many helper functions to check tile behaviors:

### Encounter Functions:
- `MetatileBehavior_IsEncounterTile()` - Checks if tile has encounters
- `MetatileBehavior_IsLandWildEncounter()` - Land-based encounters
- `MetatileBehavior_IsWaterWildEncounter()` - Water-based encounters
- `MetatileBehavior_IsIndoorEncounter()` - Indoor encounters

### Movement Functions:
- `MetatileBehavior_IsForcedMovementTile()` - Checks for forced movement
- `MetatileBehavior_IsRunningDisallowed()` - Checks if running is disabled
- `MetatileBehavior_IsEastBlocked()`, `IsWestBlocked()`, etc. - Collision checks

### Water Functions:
- `MetatileBehavior_IsSurfableWaterOrUnderwater()` - Checks surfability
- `MetatileBehavior_IsDiveable()` - Checks if diveable
- `MetatileBehavior_IsUnableToEmerge()` - Checks if cannot surface
- `MetatileBehavior_IsSurfableAndNotWaterfall()` - Surfable but not waterfall
- `MetatileBehavior_IsSurfableFishableWater()` - Can fish while surfing

### Interaction Functions:
- `MetatileBehavior_IsPC()` - PC interaction
- `MetatileBehavior_IsCounter()` - Shop counter
- `MetatileBehavior_IsPlayerFacingTVScreen()` - TV interaction
- `MetatileBehavior_IsRegionMap()` - Region map interaction

### Secret Base Functions:
- `MetatileBehavior_IsSecretBaseCave()`, `IsSecretBaseTree()`, etc.
- `MetatileBehavior_IsSecretBaseJumpMat()`, `IsSecretBaseSpinMat()`, etc.

---

## Usage Patterns

### Wild Encounters:
- Land encounters: Tall grass, long grass, deep sand, cave, indoor encounter, ash grass
- Water encounters: Pond water, deep water, ocean water, seaweed

### Warps:
- Arrow warps: Directional warps (north/south/east/west)
- Special warps: Battle Pyramid, Mossdeep Gym, Lavaridge Gym, Aqua Hideout, Mt. Pyre hole
- Doors: Animated doors, non-animated doors, water doors

### Forced Movement:
- Sliding: Ice tiles, slide tiles, trick house puzzle floor
- Currents: Water currents in four directions
- Jumping: Jump tiles in eight directions
- Walking: Walk tiles in four directions

### Collision:
- Impassable tiles block movement from specific directions
- Some tiles block multiple directions (north+south, east+west)

---

## Known Issues/Bugs

1. **Water Door Bug** (Line 865-872 in metatile_behavior.c):
   - Player can unintentionally emerge on water doors
   - Fixed with `#ifdef BUGFIX` flag

2. **Unused Behaviors**:
   - Many behaviors are defined but never used in the game
   - Some have encounter flags but no actual encounter data

3. **Bridge Reuse**:
   - `MB_BRIDGE_OVER_OCEAN` is reused for Union Room warp (line 1147)

---

## Statistics

- **Total Behaviors**: 245 (0-244)
- **Used Behaviors**: ~150-160 (estimated)
- **Unused Behaviors**: ~85-95
- **Behaviors with Encounters**: 13
- **Behaviors with Surfable**: 12
- **Secret Base Behaviors**: ~35
- **Bridge Behaviors**: 13
- **Door/Warp Behaviors**: ~15
- **Forced Movement Behaviors**: ~20

---

## Files Referenced

- `include/constants/metatile_behaviors.h` - Behavior constant definitions
- `include/metatile_behavior.h` - Function declarations
- `src/metatile_behavior.c` - Implementation and helper functions
- `src/fieldmap.c` - `MapGridGetMetatileBehaviorAt()` function
- `src/wild_encounter.c` - Encounter system usage
- `src/secret_base.c` - Secret base system usage
- `src/overworld.c` - Overworld movement and interaction
- `src/fldeff_cut.c` - Cut HM usage with grass behaviors

---

## Conclusion

The tile behavior system in Pokemon Emerald is comprehensive and handles:
- Terrain types and movement
- Wild Pokemon encounters
- Water mechanics (surf, dive, fishing)
- Warps and doors
- Secret base functionality
- Forced movement mechanics
- Interactive objects

The system is well-structured with helper functions for common checks, though there are many unused behaviors that could be repurposed for ROM hacks or new features.

