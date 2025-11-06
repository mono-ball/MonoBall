# Template System Expansion Opportunities

**Date**: November 5, 2025  
**Status**: Analysis  

## Current State

### Existing Templates
- `player` - Player character with full movement, input, collision, animation
- `npc/generic` - Basic NPC with slower movement

### Current Manual Entity Creation
- **Tiles**: ~3,000+ entities created manually in `MapLoader.CreateTileEntity()` with property-based component addition
- **Test entities**: Manual creation in test files
- **MapInfo/TilesetInfo**: Metadata entities created manually

## Expansion Opportunities

### 1. **Tile Templates** (HIGH IMPACT)

#### Problem
Currently, every tile entity is created manually with complex property parsing logic:
```csharp
var entity = world.Create(position, sprite);
// Then 50+ lines of if/else to add components based on properties
if (props.TryGetValue("solid", out var solidValue) || isLedge) { ... }
if (props.TryGetValue("ledge_direction", out var ledgeValue)) { ... }
// etc.
```

#### Solution
Create tile templates that encapsulate common tile configurations:

```csharp
// Template IDs
tile/ground           - Basic walkable tile (TilePosition, TileSprite)
tile/wall             - Solid obstacle (+ Collision)
tile/grass            - Grass with encounters (+ EncounterZone)
tile/water            - Water tile (+ TerrainType, + special sprite)
tile/ledge/down       - Down-facing ledge (+ TileLedge, + Collision)
tile/ledge/up         - Up-facing ledge
tile/ledge/left       - Left-facing ledge
tile/ledge/right      - Right-facing ledge
tile/door             - Door tile (+ TileScript)
tile/sign             - Sign tile (+ TileScript)
tile/animated/water   - Animated water tile (+ AnimatedTile)
```

#### Benefits
- **Cleaner MapLoader**: Replace 100+ lines of property parsing with template lookups
- **Better performance**: Pre-compiled templates vs runtime property parsing
- **Easier modding**: Modders can define new tile types via templates
- **Type safety**: Templates are validated at registration, not at runtime
- **Reusability**: Same tile configurations across multiple maps

#### Implementation Approach
```csharp
// In MapLoader.CreateTileEntity:
string templateId = DetermineTileTemplate(props, layer);
var entity = await _entityFactory.SpawnFromTemplateAsync(
    templateId,
    world,
    builder => {
        builder.OverrideComponent(new TilePosition(x, y, mapId));
        builder.OverrideComponent(new TileSprite(tilesetId, tileGid, layer, sourceRect));
    }
);
```

### 2. **Specialized NPC Templates** (MEDIUM IMPACT)

#### Current Limitation
Only one generic NPC template exists. Different NPC types require manual component addition.

#### Proposed Templates
```csharp
npc/trainer           - Trainer NPCs (+ Trainer component, + Dialog, + Battle)
npc/shop-keeper       - Shop NPCs (+ Shop component, + Inventory)
npc/stationary        - NPCs that don't move (no GridMovement)
npc/patrol            - Patrolling NPCs (+ AI component, + Waypoints)
npc/rival             - Rival character (+ special stats)
npc/gym-leader        - Gym leader NPCs (+ Badge, + special team)
npc/wild-pokemon      - Wild Pokemon encounters (different component set)
```

#### Benefits
- Quick NPC spawning with appropriate components
- Consistent behavior across NPC types
- Easy to add new NPC types without code changes
- Better for future scripting/modding systems

### 3. **Interactive Object Templates** (MEDIUM IMPACT)

#### Use Case
World objects that aren't tiles but need to be placeable in maps.

#### Proposed Templates
```csharp
object/item-ball      - Item pickup (+ Item, + Sprite, + Collision, + TileScript)
object/hidden-item    - Hidden item (+ Item, no visible sprite until found)
object/warp           - Warp zones (+ Warp component)
object/cut-tree       - Cuttable tree (+ Obstacle, + HM requirement)
object/boulder        - Pushable boulder (+ Moveable, + Collision)
object/pc             - PC storage system (+ TileScript)
object/healing-station - Pokemon Center healing (+ TileScript)
```

#### Benefits
- Standardized object behaviors
- Easy map decoration
- Consistent interaction patterns
- Better for future event/script systems

### 4. **Metadata Entity Templates** (LOW IMPACT)

#### Current
MapInfo and TilesetInfo entities created manually.

#### Proposed
```csharp
meta/map-info         - Map metadata entity
meta/tileset-info     - Tileset metadata entity
meta/region-info      - Region/zone metadata
meta/encounter-table  - Encounter configuration
```

#### Benefits
- Consistent metadata structure
- Validation at creation time
- Easier to query and manage

### 5. **Template Variants System** (ADVANCED)

#### Concept
Allow templates to extend other templates with variations:

```csharp
npc/generic           - Base NPC
  └─ npc/trainer      - Extends npc/generic, adds Trainer component
      └─ npc/gym-leader - Extends npc/trainer, adds Badge component
```

#### Implementation
```csharp
var trainerTemplate = new EntityTemplate {
    TemplateId = "npc/trainer",
    BaseTemplate = "npc/generic",  // Inherit components
    // Only specify differences
};
trainerTemplate.WithComponent(new Trainer(...));
```

#### Benefits
- Reduces duplication in template definitions
- Easier to maintain hierarchies
- Natural inheritance of behaviors

## Priority Implementation Plan

### Phase 1: Tile Templates (Highest ROI)
1. Create core tile templates (ground, wall, grass, ledges)
2. Refactor MapLoader to use templates
3. Add template-based tile property system
4. Performance benchmark (expect 10-20% improvement in map loading)

### Phase 2: Specialized NPCs
1. Add trainer/shop-keeper/stationary templates
2. Update test NPCs to use new templates
3. Add NPC spawning from map object layers

### Phase 3: Interactive Objects
1. Create item/warp/obstacle templates
2. Add object layer parsing to MapLoader
3. Integrate with future scripting system

### Phase 4: Template Variants (Optional)
1. Implement template inheritance system
2. Refactor existing templates to use inheritance
3. Add tooling for template hierarchy visualization

## Technical Considerations

### Template Loading Performance
- Current: O(1) lookup from TemplateCache
- With 50+ templates: Still O(1) with Dictionary lookup
- Template registration: One-time cost at startup (~10ms for 50 templates)

### Memory Usage
- Each template: ~1-2 KB (metadata + component definitions)
- 50 templates: ~50-100 KB (negligible)
- Runtime: Templates are copied, not shared (correct behavior for ECS)

### Modding Support
Future enhancement: Load templates from JSON/YAML files:
```yaml
# mods/my-mod/templates/npc-custom.yaml
templateId: npc/custom-trainer
baseTemplate: npc/trainer
components:
  - type: Position
    x: 0
    y: 0
  - type: Sprite
    textureId: custom-sprite
```

## Comparison: Manual vs Template-Based Creation

### Manual Creation (Current)
```csharp
var entity = world.Create(position, sprite);
if (props.TryGetValue("solid", out var solidValue)) {
    bool isSolid = solidValue switch {
        bool b => b,
        string s => bool.TryParse(s, out var result) && result,
        _ => false,
    };
    if (isSolid) world.Add(entity, new Collision(true));
}
// 50+ more lines...
```
**Lines of code**: ~100 per entity type  
**Maintainability**: Low (scattered logic)  
**Validation**: Runtime only  
**Performance**: Property parsing overhead  

### Template-Based Creation
```csharp
var entity = await _entityFactory.SpawnFromTemplateAsync(
    "tile/wall",
    world,
    builder => builder.OverrideComponent(new TilePosition(x, y, mapId))
);
```
**Lines of code**: ~3 per entity spawn  
**Maintainability**: High (centralized definitions)  
**Validation**: At registration time  
**Performance**: Pre-compiled component arrays  

## Recommendations

1. **Start with tile templates** - Biggest immediate impact on code quality and performance
2. **Use template variants** - Once we have 10+ templates, implement inheritance to reduce duplication
3. **Plan for external templates** - Design with future JSON/YAML loading in mind
4. **Add template validation tools** - CLI tool to validate all templates at build time
5. **Consider template hot-reload** - For development workflow (already supported via TemplateCache invalidation)

## Next Steps

1. Create `TemplateRegistry.RegisterTileTemplates()` method
2. Add tile template determination logic to MapLoader
3. Refactor MapLoader.CreateTileEntity to use templates
4. Add unit tests for new tile templates
5. Benchmark map loading performance improvements
6. Document template system usage for modders

---

**Estimated Effort**: 
- Phase 1 (Tile Templates): 4-6 hours
- Phase 2 (NPC Templates): 2-3 hours  
- Phase 3 (Object Templates): 3-4 hours
- Phase 4 (Variants): 4-6 hours

**Total**: ~13-19 hours for complete template system expansion



