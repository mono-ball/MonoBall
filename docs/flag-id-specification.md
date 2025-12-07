# PokeSharp Flag ID Format Specification v1.0

## Overview

This specification defines a standardized flag ID format for PokeSharp that replaces pokeemerald's verbose `FLAG_HIDE_LITTLEROOT_TOWN_FAT_MAN` style with a hierarchical, machine-readable format while maintaining human understandability.

## Format Structure

```
base:flag:{category}/{region}/{location}/{entity}[/{qualifier}]
```

### Components

1. **Prefix**: `base:flag:` (consistent with PokeSharp ID system)
2. **Category**: Flag type/purpose (see taxonomy below)
3. **Region**: Game region (hoenn, kanto, johto, etc.)
4. **Location**: Map/area identifier (snake_case)
5. **Entity**: Specific object/character/item (snake_case)
6. **Qualifier**: Optional additional context (variant, state, etc.)

## Category Taxonomy

### 1. `visibility` - Entity Visibility Flags
**Purpose**: Control NPC, object, or sprite visibility
**Description**: Tracks whether entities should be shown or hidden based on game state

**Examples**:
- `base:flag:visibility/hoenn/littleroot_town/fat_man`
- `base:flag:visibility/hoenn/rustboro_city/devon_worker`
- `base:flag:visibility/hoenn/route103/rival`

---

### 2. `item` - Obtainable Item Flags
**Purpose**: Track ground items, gift items, and pickups
**Description**: Indicates whether a visible item has been collected

**Examples**:
- `base:flag:item/hoenn/victory_road_b1f/tm_psychic`
- `base:flag:item/hoenn/petalburg_woods/potion`
- `base:flag:item/hoenn/littleroot_town/running_shoes`

---

### 3. `hidden_item` - Hidden Item Flags
**Purpose**: Track hidden items found via Itemfinder/Dowsing Machine
**Description**: Indicates whether a hidden item has been discovered and collected

**Examples**:
- `base:flag:hidden_item/hoenn/route127_underwater/heart_scale`
- `base:flag:hidden_item/hoenn/petalburg_city/rare_candy`
- `base:flag:hidden_item/hoenn/route110/revive`

---

### 4. `story` - Story Progression Flags
**Purpose**: Track main story events and milestones
**Description**: Major plot points, cutscenes, and story-critical events

**Examples**:
- `base:flag:story/hoenn/rustboro_city/rescued_devon_worker`
- `base:flag:story/hoenn/mt_chimney/magma_defeated`
- `base:flag:story/hoenn/sootopolis_city/rayquaza_awakened`

---

### 5. `badge` - Gym Badge Flags
**Purpose**: Track gym badge acquisition
**Description**: Indicates which gym badges have been earned

**Examples**:
- `base:flag:badge/hoenn/rustboro_city/stone_badge`
- `base:flag:badge/hoenn/dewford_town/knuckle_badge`
- `base:flag:badge/hoenn/mauville_city/dynamo_badge`

---

### 6. `trainer` - Trainer Battle Flags
**Purpose**: Track defeated trainers
**Description**: Indicates whether a specific trainer has been battled

**Examples**:
- `base:flag:trainer/hoenn/route102/youngster_calvin`
- `base:flag:trainer/hoenn/rustboro_gym/leader_roxanne`
- `base:flag:trainer/hoenn/victory_road/ace_trainer_edgar`

---

### 7. `daily` - Daily Reset Flags
**Purpose**: Track daily events and resets
**Description**: Events that reset every 24 hours (berry growth, daily gifts, etc.)

**Examples**:
- `base:flag:daily/hoenn/lilycove_city/berry_master_gift`
- `base:flag:daily/hoenn/fallarbor_town/fossil_maniac_check`
- `base:flag:daily/hoenn/rustboro_city/stone_shop_sale`

---

### 8. `temporary` - Temporary State Flags
**Purpose**: Short-lived state tracking
**Description**: Flags that are set and cleared within a single session or event

**Examples**:
- `base:flag:temporary/hoenn/safari_zone/balls_remaining`
- `base:flag:temporary/hoenn/battle_tent/challenge_active`
- `base:flag:temporary/hoenn/contest_hall/performance_complete`

---

### 9. `unlock` - Feature Unlock Flags
**Purpose**: Track unlocked features and abilities
**Description**: Permanent unlocks like HM usage, areas, or mechanics

**Examples**:
- `base:flag:unlock/hoenn/global/fly`
- `base:flag:unlock/hoenn/global/surf`
- `base:flag:unlock/hoenn/mauville_city/bike_shop`

---

### 10. `quest` - Side Quest Flags
**Purpose**: Track optional quests and tasks
**Description**: Non-story optional content completion

**Examples**:
- `base:flag:quest/hoenn/slateport_city/contest_pass_obtained`
- `base:flag:quest/hoenn/rustboro_city/pokeblock_case_received`
- `base:flag:quest/hoenn/lavaridge_town/go_goggles_acquired`

---

### 11. `defeated` - Legendary/Special Battle Flags
**Purpose**: Track defeated/caught legendary Pokémon
**Description**: One-time legendary encounters and captures

**Examples**:
- `base:flag:defeated/hoenn/cave_of_origin/kyogre`
- `base:flag:defeated/hoenn/sky_pillar/rayquaza`
- `base:flag:defeated/hoenn/southern_island/latias`

---

### 12. `trigger` - Event Trigger Flags
**Purpose**: Enable/disable script triggers
**Description**: Control when scripted events can be triggered

**Examples**:
- `base:flag:trigger/hoenn/route103/rival_battle_available`
- `base:flag:trigger/hoenn/littleroot_town/mom_running_shoes_cutscene`
- `base:flag:trigger/hoenn/petalburg_city/wally_capture_tutorial`

---

### 13. `interaction` - Object Interaction Flags
**Purpose**: Track object interactions and state changes
**Description**: Doors unlocked, switches flipped, boulders moved, etc.

**Examples**:
- `base:flag:interaction/hoenn/seafloor_cavern/door_unlocked`
- `base:flag:interaction/hoenn/mt_pyre/grave_visited`
- `base:flag:interaction/hoenn/new_mauville/generator_off`

---

### 14. `collection` - Collection/Completion Flags
**Purpose**: Track collection achievements
**Description**: TMs collected, Pokédex milestones, etc.

**Examples**:
- `base:flag:collection/hoenn/global/tm_earthquake_obtained`
- `base:flag:collection/hoenn/global/nat_dex_unlocked`
- `base:flag:collection/hoenn/global/all_badges_obtained`

---

## Conversion Rules

### Rule 1: Remove FLAG_ Prefix
**Old**: `FLAG_HIDE_*`
**New**: `base:flag:visibility/*`

### Rule 2: Category Detection
```
FLAG_HIDE_* → visibility/
FLAG_ITEM_* → item/
FLAG_HIDDEN_ITEM_* → hidden_item/
FLAG_DEFEATED_* → defeated/
FLAG_BADGE_* → badge/
FLAG_DAILY_* → daily/
FLAG_*_TRAINER_* → trainer/
FLAG_UNUSED_* → [deprecated, do not convert]
```

### Rule 3: Region Extraction
- Default to `hoenn` for Emerald flags
- Extract from context or map data
- Use `global` for region-independent flags

### Rule 4: Location Normalization
```
LITTLEROOT_TOWN → littleroot_town
VICTORY_ROAD_B1F → victory_road_b1f
ROUTE_127_UNDERWATER → route127_underwater
MT_CHIMNEY → mt_chimney
```

### Rule 5: Entity Simplification
```
FAT_MAN → fat_man
DEVON_WORKER → devon_worker
TM_PSYCHIC → tm_psychic
HEART_SCALE → heart_scale
```

### Rule 6: Qualifier Handling
For entities with variants or states:
```
base:flag:trainer/hoenn/route103/rival/battle_1
base:flag:trainer/hoenn/route103/rival/battle_2
base:flag:item/hoenn/petalburg_city/potion/post_gym
```

---

## Example Conversions

### Visibility Flags
```
FLAG_HIDE_LITTLEROOT_TOWN_FAT_MAN
→ base:flag:visibility/hoenn/littleroot_town/fat_man

FLAG_HIDE_RUSTBORO_CITY_DEVON_WORKER
→ base:flag:visibility/hoenn/rustboro_city/devon_worker

FLAG_HIDE_ROUTE_103_RIVAL
→ base:flag:visibility/hoenn/route103/rival
```

### Item Flags
```
FLAG_ITEM_VICTORY_ROAD_B1F_TM_PSYCHIC
→ base:flag:item/hoenn/victory_road_b1f/tm_psychic

FLAG_ITEM_PETALBURG_WOODS_POTION
→ base:flag:item/hoenn/petalburg_woods/potion

FLAG_ITEM_ROUTE_102_ORAN_BERRY
→ base:flag:item/hoenn/route102/oran_berry
```

### Hidden Item Flags
```
FLAG_HIDDEN_ITEM_UNDERWATER_127_HEART_SCALE
→ base:flag:hidden_item/hoenn/route127_underwater/heart_scale

FLAG_HIDDEN_ITEM_PETALBURG_CITY_RARE_CANDY
→ base:flag:hidden_item/hoenn/petalburg_city/rare_candy

FLAG_HIDDEN_ITEM_ROUTE_110_REVIVE
→ base:flag:hidden_item/hoenn/route110/revive
```

### Story Flags
```
FLAG_RESCUED_DEVON_WORKER
→ base:flag:story/hoenn/rustboro_city/rescued_devon_worker

FLAG_DEFEATED_MAGMA_SPACE_CENTER
→ base:flag:story/hoenn/mossdeep_city/defeated_magma_space_center

FLAG_RAYQUAZA_AWAKENED
→ base:flag:story/hoenn/sootopolis_city/rayquaza_awakened
```

### Badge Flags
```
FLAG_BADGE01_GET
→ base:flag:badge/hoenn/rustboro_city/stone_badge

FLAG_BADGE02_GET
→ base:flag:badge/hoenn/dewford_town/knuckle_badge

FLAG_BADGE08_GET
→ base:flag:badge/hoenn/sootopolis_city/rain_badge
```

### Trainer Flags
```
FLAG_DEFEATED_RUSTBORO_GYM_LEADER
→ base:flag:trainer/hoenn/rustboro_gym/leader_roxanne

FLAG_DEFEATED_ROUTE_102_YOUNGSTER_CALVIN
→ base:flag:trainer/hoenn/route102/youngster_calvin

FLAG_DEFEATED_ELITE_FOUR_SIDNEY
→ base:flag:trainer/hoenn/ever_grande_city/elite_four_sidney
```

### Legendary Flags
```
FLAG_DEFEATED_KYOGRE
→ base:flag:defeated/hoenn/cave_of_origin/kyogre

FLAG_DEFEATED_RAYQUAZA
→ base:flag:defeated/hoenn/sky_pillar/rayquaza

FLAG_DEFEATED_LATIAS
→ base:flag:defeated/hoenn/southern_island/latias
```

### Daily Flags
```
FLAG_DAILY_LILYCOVE_DEPARTMENT_STORE_LADY
→ base:flag:daily/hoenn/lilycove_city/department_store_lady

FLAG_DAILY_SOOTOPOLIS_MYSTERY_GIFT
→ base:flag:daily/hoenn/sootopolis_city/mystery_gift
```

### Unlock Flags
```
FLAG_SYS_SURF_ENABLED
→ base:flag:unlock/hoenn/global/surf

FLAG_SYS_FLY_ENABLED
→ base:flag:unlock/hoenn/global/fly

FLAG_BIKE_SHOP_UNLOCKED
→ base:flag:unlock/hoenn/mauville_city/bike_shop
```

---

## Edge Cases & Special Handling

### Edge Case 1: Numbered Variants
**Scenario**: Multiple instances of the same entity type

**Old Format**:
```
FLAG_HIDE_ROUTE_101_ZIGZAGOON_1
FLAG_HIDE_ROUTE_101_ZIGZAGOON_2
```

**New Format**:
```
base:flag:visibility/hoenn/route101/zigzagoon/instance_1
base:flag:visibility/hoenn/route101/zigzagoon/instance_2
```

---

### Edge Case 2: Progressive Story Events
**Scenario**: Same location, multiple story stages

**Old Format**:
```
FLAG_TEAM_AQUA_HIDEOUT_STATE_1
FLAG_TEAM_AQUA_HIDEOUT_STATE_2
```

**New Format**:
```
base:flag:story/hoenn/aqua_hideout/state_1
base:flag:story/hoenn/aqua_hideout/state_2
```

---

### Edge Case 3: Global System Flags
**Scenario**: Flags not tied to specific locations

**Old Format**:
```
FLAG_SYS_POKEDEX_GET
FLAG_SYS_NAT_DEX_GET
```

**New Format**:
```
base:flag:unlock/hoenn/global/pokedex
base:flag:unlock/hoenn/global/national_dex
```

---

### Edge Case 4: Multi-Region Items
**Scenario**: Items that exist in multiple regions

**Old Format**:
```
FLAG_ITEM_KANTO_ROUTE_1_POTION
FLAG_ITEM_HOENN_ROUTE_101_POTION
```

**New Format**:
```
base:flag:item/kanto/route1/potion
base:flag:item/hoenn/route101/potion
```

---

### Edge Case 5: Conditional Visibility
**Scenario**: Entity visibility based on game version or choices

**Old Format**:
```
FLAG_HIDE_LATIAS
FLAG_HIDE_LATIOS
```

**New Format**:
```
base:flag:visibility/hoenn/southern_island/latias
base:flag:visibility/hoenn/southern_island/latios
```

---

### Edge Case 6: Multi-Floor Dungeons
**Scenario**: Same item/entity on different floors

**Old Format**:
```
FLAG_ITEM_VICTORY_ROAD_B1F_TM_PSYCHIC
FLAG_ITEM_VICTORY_ROAD_B2F_FULL_RESTORE
```

**New Format**:
```
base:flag:item/hoenn/victory_road_b1f/tm_psychic
base:flag:item/hoenn/victory_road_b2f/full_restore
```

**Note**: Floor number is part of the location, not a qualifier.

---

### Edge Case 7: Weather-Dependent Flags
**Scenario**: Flags that depend on weather or time

**Old Format**:
```
FLAG_MIRAGE_ISLAND_APPEARED
FLAG_SHOAL_CAVE_LOW_TIDE
```

**New Format**:
```
base:flag:temporary/hoenn/route130/mirage_island_appeared
base:flag:temporary/hoenn/shoal_cave/low_tide
```

---

### Edge Case 8: Mystery Event Flags
**Scenario**: Special event distributions

**Old Format**:
```
FLAG_MYSTERY_EVENT_EONTICKET_DELIVERED
FLAG_MYSTERY_EVENT_MYSTICTICKET_DELIVERED
```

**New Format**:
```
base:flag:quest/hoenn/global/eon_ticket_delivered
base:flag:quest/hoenn/global/mystic_ticket_delivered
```

---

## Implementation Guidelines

### 1. Backward Compatibility
- Maintain a mapping table: `old_flag_id → new_flag_id`
- Provide conversion utilities for existing save data
- Support both formats during migration period

### 2. Validation Rules
```typescript
// Flag ID must match pattern
const FLAG_PATTERN = /^base:flag:[a-z_]+\/[a-z_]+\/[a-z0-9_]+\/[a-z0-9_]+(\/[a-z0-9_]+)?$/;

// Category must be in approved list
const VALID_CATEGORIES = [
  'visibility', 'item', 'hidden_item', 'story', 'badge',
  'trainer', 'daily', 'temporary', 'unlock', 'quest',
  'defeated', 'trigger', 'interaction', 'collection'
];

// Region must be valid
const VALID_REGIONS = ['hoenn', 'kanto', 'johto', 'sinnoh', 'unova', 'global'];
```

### 3. Database Schema
```sql
CREATE TABLE game_flags (
    flag_id VARCHAR(255) PRIMARY KEY,
    category VARCHAR(50) NOT NULL,
    region VARCHAR(50) NOT NULL,
    location VARCHAR(100) NOT NULL,
    entity VARCHAR(100) NOT NULL,
    qualifier VARCHAR(50),
    legacy_flag_id VARCHAR(100), -- For backward compatibility
    description TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    INDEX idx_category (category),
    INDEX idx_region_location (region, location),
    INDEX idx_legacy (legacy_flag_id)
);
```

### 4. Code Generation
Provide utilities to auto-generate flag constants:

```csharp
// Auto-generated from flag definitions
public static class GameFlags
{
    public static class Visibility
    {
        public const string LittlerootTownFatMan = "base:flag:visibility/hoenn/littleroot_town/fat_man";
        public const string RustboroCityDevonWorker = "base:flag:visibility/hoenn/rustboro_city/devon_worker";
    }

    public static class Items
    {
        public const string VictoryRoadB1FTmPsychic = "base:flag:item/hoenn/victory_road_b1f/tm_psychic";
    }

    // ... etc
}
```

---

## Benefits

### 1. **Improved Readability**
- Clear hierarchical structure
- Consistent formatting
- Self-documenting category system

### 2. **Machine Parsability**
- Easy to query by category, region, or location
- Supports database indexing
- Enables automated tooling

### 3. **Scalability**
- Easy to add new categories
- Supports multiple regions
- Handles complex qualifiers

### 4. **Maintainability**
- Clear naming conventions
- Reduces naming conflicts
- Facilitates bulk operations

### 5. **Integration**
- Consistent with PokeSharp ID system
- Compatible with existing tools
- Supports cross-referencing with maps, sprites, scripts

---

## Migration Strategy

### Phase 1: Preparation
1. Audit all existing FLAGS from pokeemerald
2. Create comprehensive mapping table
3. Generate conversion utilities
4. Document edge cases

### Phase 2: Implementation
1. Implement new flag system alongside old
2. Add validation layer
3. Create auto-migration tools
4. Update documentation

### Phase 3: Migration
1. Convert all flag definitions
2. Update all flag references in code
3. Migrate save data format
4. Run comprehensive tests

### Phase 4: Deprecation
1. Mark old format as deprecated
2. Provide warning logs
3. Remove old format in next major version

---

## Version History

- **v1.0** (2025-12-06): Initial specification
  - 14 flag categories
  - Complete conversion rules
  - Edge case handling
  - Migration strategy

---

## References

- PokeSharp ID System Documentation
- pokeemerald Flag Definitions
- Game Data Asset Management Specification
