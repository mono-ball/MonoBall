# ECS Conversion Analysis: Tiled to Arch ECS

**Analyst**: Hive Mind - Analyst Agent
**Date**: 2025-11-08
**Scope**: Complete ECS conversion pipeline from Tiled maps to Arch entities

---

## Executive Summary

This analysis examines how Tiled map data flows through the system and transforms into ECS entities and components. The conversion pipeline demonstrates a **sophisticated hybrid architecture** combining template-based entity creation with data-driven property mapping, but reveals several **architectural concerns** around tight coupling and missing abstraction layers.

### Key Findings

1. **Conversion Pipeline**: TiledJsonMap → TmxDocument → ECS Entities (3-stage transformation)
2. **Dual-Path Architecture**: Template-based (preferred) + Fallback manual creation
3. **Data Preservation**: ~75% complete (tile properties, animations, objects preserved; some metadata lost)
4. **Coupling Issues**: Tiled format tightly coupled to ECS layer
5. **Extensibility**: Limited by hardcoded property mappings

---

## 1. Complete Conversion Pipeline

### Stage 1: JSON Deserialization (TiledMapLoader)

**File**: `PokeSharp.Rendering/Loaders/TiledMapLoader.cs`

```
Tiled JSON File (.json)
    ↓
TiledJsonMap (raw deserialized data)
    • Version, TiledVersion
    • Width, Height, TileWidth, TileHeight
    • Layers[] (tilelayer, objectgroup)
    • Tilesets[] (embedded or external)
    • Properties[] (custom map properties)
```

**Key Transformations**:
- Base64-encoded tile data → Decompressed int arrays
- External tilesets (.tsx) → Loaded and merged
- Tile animations parsed from tileset definitions
- Custom properties extracted and typed (bool, int, string)

**Code Reference** (lines 28-39):
```csharp
public static TmxDocument Load(string mapPath)
{
    var json = File.ReadAllText(mapPath);
    var tiledMap = JsonSerializer.Deserialize<TiledJsonMap>(json, JsonOptions);
    return ConvertToTmxDocument(tiledMap, mapPath);
}
```

### Stage 2: TMX Document Conversion (TiledMapLoader)

**File**: `PokeSharp.Rendering/Loaders/TiledMapLoader.cs`

```
TiledJsonMap
    ↓
TmxDocument (intermediate representation)
    • Layers converted to TmxLayer[]
    • Object groups converted to TmxObjectGroup[]
    • Tilesets converted to TmxTileset[]
    • Tile properties indexed by local tile ID
    • Animations indexed by tile ID
```

**Data Transformation** (lines 41-56):
```csharp
private static TmxDocument ConvertToTmxDocument(TiledJsonMap tiledMap, string mapPath)
{
    return new TmxDocument
    {
        Version = tiledMap.Version,
        Width = tiledMap.Width, Height = tiledMap.Height,
        Tilesets = ConvertTilesets(tiledMap.Tilesets, mapPath),
        Layers = ConvertLayers(tiledMap.Layers, tiledMap.Width, tiledMap.Height),
        ObjectGroups = ConvertObjectGroups(tiledMap.Layers)
    };
}
```

**Critical Feature**: Layer data decompression (lines 228-306):
- Supports plain arrays, base64, gzip, and zlib compression
- Converts byte arrays to 2D int[height, width] arrays
- Handles flip flags embedded in tile GIDs (upper 3 bits)

### Stage 3: ECS Entity Creation (MapLoader)

**File**: `PokeSharp.Rendering/Loaders/MapLoader.cs`

```
TmxDocument
    ↓
Arch ECS World
    • Tile Entities (TilePosition + TileSprite + behavior components)
    • MapInfo Entity (map metadata)
    • TilesetInfo Entity (tileset metadata)
    • Animated Tile Entities (AnimatedTile component added)
    • Object Entities (NPCs, items, triggers from object layers)
```

**Two Conversion Paths**:

#### Path A: Template-Based Creation (Preferred)

**Code Reference** (lines 352-374):
```csharp
if (_entityFactory != null && templateId != null && _entityFactory.HasTemplate(templateId))
{
    entity = _entityFactory.SpawnFromTemplate(
        templateId,
        world,
        builder =>
        {
            builder.OverrideComponent(position);
            builder.OverrideComponent(sprite);
        }
    );
}
```

**Template Determination Logic** (lines 267-324):
```csharp
private static string? DetermineTileTemplate(Dictionary<string, object> props)
{
    // Priority 1: Ledge detection (ledge_direction property)
    if (props.TryGetValue("ledge_direction", out var ledgeValue))
        return ledgeValue switch
        {
            "down" => "tile/ledge/down",
            "up" => "tile/ledge/up",
            "left" => "tile/ledge/left",
            "right" => "tile/ledge/right",
            _ => null
        };

    // Priority 2: Solid walls (solid property)
    if (props.TryGetValue("solid", out var solidValue) && IsSolid(solidValue))
        return "tile/wall";

    // Priority 3: Encounter zones (encounter_rate property)
    if (props.TryGetValue("encounter_rate", out var encounterValue) && encounterValue > 0)
        return "tile/grass";

    // Default: Ground tile
    return "tile/ground";
}
```

**Templates Defined** (TemplateRegistry.cs):
- `tile/base` → Base template (TilePosition + TileSprite)
- `tile/ground` → Walkable tile (inherits base)
- `tile/wall` → Solid obstacle (base + Collision)
- `tile/grass` → Encounter zone (base + EncounterZone)
- `tile/ledge/down|up|left|right` → Directional ledges (wall + TileLedge)

#### Path B: Manual Fallback Creation (Legacy)

**Code Reference** (lines 377-456):
```csharp
else
{
    // Manual creation without templates
    entity = world.Create(position, sprite);

    // Add components based on properties
    if (props.TryGetValue("solid", out var solidValue) && IsSolid(solidValue))
        world.Add(entity, new Collision(true));

    if (props.TryGetValue("ledge_direction", out var ledgeValue))
        world.Add(entity, new TileLedge(ParseDirection(ledgeValue)));

    if (props.TryGetValue("encounter_rate", out var encounterRateValue))
        world.Add(entity, new EncounterZone(encounterTableId, encounterRate));
}
```

**Additional Components** (applied to both paths, lines 459-479):
```csharp
// TerrainType (terrain_type + footstep_sound properties)
if (props.TryGetValue("terrain_type", out var terrainValue))
    world.Add(entity, new TerrainType(terrainType, footstepSound));

// TileScript (script property)
if (props.TryGetValue("script", out var scriptValue))
    world.Add(entity, new TileScript(scriptPath));
```

---

## 2. Data Flow Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                    TILED MAP EDITOR                             │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐          │
│  │ Tile Layers  │  │   Objects    │  │  Properties  │          │
│  │ (Ground,     │  │ (NPCs,       │  │ (solid,      │          │
│  │  Objects,    │  │  Items)      │  │  encounter)  │          │
│  │  Overhead)   │  │              │  │              │          │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘          │
│         └──────────────────┴────────────────┬┘                  │
│                                             ↓                   │
│                                    Export as JSON               │
└─────────────────────────────────────────────┬───────────────────┘
                                              ↓
┌─────────────────────────────────────────────────────────────────┐
│                  TILEDMAPLOADER.CS                              │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │ JSON Deserialization (TiledJsonMap)                      │   │
│  │  • Decompress layer data (gzip/zlib)                     │   │
│  │  • Parse external tilesets                               │   │
│  │  • Extract tile animations                               │   │
│  │  • Convert properties to Dictionary<string, object>      │   │
│  └──────────────────┬───────────────────────────────────────┘   │
│                     ↓                                           │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │ TMX Document Conversion                                  │   │
│  │  • TiledJsonMap → TmxDocument                            │   │
│  │  • Layers → TmxLayer[] (2D int arrays)                   │   │
│  │  • Objects → TmxObjectGroup[]                            │   │
│  │  • Tilesets → TmxTileset[] (with properties)             │   │
│  └──────────────────┬───────────────────────────────────────┘   │
└─────────────────────┼───────────────────────────────────────────┘
                      ↓
┌─────────────────────────────────────────────────────────────────┐
│                     MAPLOADER.CS                                │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │ Tile Entity Creation Loop                                │   │
│  │  FOR each layer (Ground, Objects, Overhead)              │   │
│  │    FOR each tile at (x, y):                              │   │
│  │      1. Extract flip flags from GID                      │   │
│  │      2. Get tile properties from tileset                 │   │
│  │      3. Determine template ID                            │   │
│  │         ┌─────────────────────────────────────────────┐  │   │
│  │         │ DetermineTileTemplate(props)                │  │   │
│  │         │  Priority:                                  │  │   │
│  │         │   1. ledge_direction → "tile/ledge/X"       │  │   │
│  │         │   2. solid → "tile/wall"                    │  │   │
│  │         │   3. encounter_rate → "tile/grass"          │  │   │
│  │         │   4. default → "tile/ground"                │  │   │
│  │         └─────────────────────────────────────────────┘  │   │
│  │      4. Create entity (template or manual)               │   │
│  │      5. Add additional components (terrain, script)      │   │
│  └──────────────────┬───────────────────────────────────────┘   │
│                     ↓                                           │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │ Animated Tile Post-Processing                            │   │
│  │  FOR each animated tile definition:                      │   │
│  │    QUERY all entities with matching TileGid              │   │
│  │    ADD AnimatedTile component                            │   │
│  └──────────────────┬───────────────────────────────────────┘   │
│                     ↓                                           │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │ Object Entity Creation                                   │   │
│  │  FOR each object in object layers:                       │   │
│  │    1. Get template ID from object.Type                   │   │
│  │    2. Convert pixel coords to tile coords                │   │
│  │    3. SpawnFromTemplate with overrides:                  │   │
│  │       - Position(tileX, tileY, mapId)                    │   │
│  │       - Direction (if specified)                         │   │
│  │       - Npc (if NPC object)                              │   │
│  │       - MovementRoute (if waypoints specified)           │   │
│  └──────────────────┬───────────────────────────────────────┘   │
└─────────────────────┼───────────────────────────────────────────┘
                      ↓
┌─────────────────────────────────────────────────────────────────┐
│                   ENTITYFACTORYSERVICE.CS                       │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │ Template Resolution                                      │   │
│  │  1. Get template from cache                              │   │
│  │  2. Resolve inheritance (if BaseTemplateId exists)       │   │
│  │  3. Merge components (base → derived)                    │   │
│  │  4. Validate template                                    │   │
│  └──────────────────┬───────────────────────────────────────┘   │
│                     ↓                                           │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │ Component Instantiation                                  │   │
│  │  1. Build component array from template                  │   │
│  │  2. Apply context overrides                              │   │
│  │  3. Create empty entity                                  │   │
│  │  4. Add each component via reflection:                   │   │
│  │     world.Add<T>(entity, component)                      │   │
│  └──────────────────┬───────────────────────────────────────┘   │
└─────────────────────┼───────────────────────────────────────────┘
                      ↓
┌─────────────────────────────────────────────────────────────────┐
│                      ARCH ECS WORLD                             │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │ Entities Created:                                        │   │
│  │  • Tile Entities                                         │   │
│  │    - TilePosition (x, y, mapId)                          │   │
│  │    - TileSprite (tilesetId, gid, layer, rect, flips)     │   │
│  │    - Collision (if solid or ledge)                       │   │
│  │    - TileLedge (if ledge)                                │   │
│  │    - EncounterZone (if grass)                            │   │
│  │    - TerrainType (if terrain specified)                  │   │
│  │    - TileScript (if script specified)                    │   │
│  │    - AnimatedTile (if animated)                          │   │
│  │                                                          │   │
│  │  • MapInfo Entity (metadata)                             │   │
│  │  • TilesetInfo Entity (tileset data)                     │   │
│  │  • Object Entities (NPCs, items, triggers)               │   │
│  └──────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
```

---

## 3. Data Preservation Analysis

### ✅ Information Preserved

| **Data Source** | **Preserved As** | **Completeness** |
|----------------|------------------|------------------|
| Tile coordinates (x, y) | `TilePosition` component | 100% |
| Tile GID | `TileSprite.TileGid` | 100% |
| Tile flip flags | `TileSprite.Flip*` (3 booleans) | 100% |
| Layer membership | `TileSprite.Layer` enum | 100% |
| Tileset reference | `TileSprite.TilesetId` string | 100% |
| Tile animations | `AnimatedTile` component | 100% |
| Custom properties | Multiple components (see below) | ~75% |
| Object positions | `Position` component (converted to tiles) | 100% |
| Object properties | Component overrides via EntityBuilder | ~80% |
| Map dimensions | `MapInfo` entity | 100% |
| Tileset metadata | `TilesetInfo` entity | 100% |

### Custom Property Mappings

**Tile Properties**:
```
Tiled Property         → ECS Component
─────────────────────────────────────────────
solid = true          → Collision(true)
ledge_direction = X   → TileLedge(Direction.X) + Collision(true)
encounter_rate = N    → EncounterZone(tableId, N)
encounter_table = S   → EncounterZone(S, rate)
terrain_type = S      → TerrainType(S, footstepSound)
footstep_sound = S    → TerrainType(terrain, S)
script = "path"       → TileScript("path")
```

**Object Properties**:
```
Tiled Property         → ECS Component
─────────────────────────────────────────────
type = "npc/X"        → Template selection
direction = X         → Direction enum
npcId = S             → Npc(S)
displayName = S       → Name(S)
waypoints = "x,y;..."  → MovementRoute(points, loop, wait)
waypointWaitTime = F  → MovementRoute(..., waitTime)
```

### ⚠️ Information Lost

| **Lost Data** | **Impact** | **Severity** |
|--------------|-----------|--------------|
| Layer opacity | Rendering fidelity | Low |
| Layer visibility | Debug/editing | Low |
| Object width/height | Non-tile objects | Medium |
| Tiled-specific metadata | Editor features | Low |
| Custom map properties | Extensibility | Medium |
| Tile type (from Tiled) | Optional typing | Low |

### 🔄 Information Transformed

| **Original** | **Transformed To** | **Reason** |
|-------------|-------------------|-----------|
| Pixel coordinates (objects) | Tile coordinates | Grid-based movement system |
| Global tile ID (with flags) | Clean GID + flip booleans | Easier ECS queries |
| Base64 compressed data | 2D int arrays | Runtime efficiency |
| External tileset paths | Loaded and merged | Single-source tileset data |
| String direction values | Direction enum | Type safety |

---

## 4. Architectural Issues & Concerns

### 🔴 Critical: Tight Coupling to Tiled Format

**Issue**: The ECS layer has intimate knowledge of Tiled's data structures.

**Evidence**:
```csharp
// MapLoader.cs lines 267-324
private static string? DetermineTileTemplate(Dictionary<string, object> props)
{
    // Hardcoded Tiled property names
    if (props.TryGetValue("ledge_direction", out var ledgeValue))  // ← Tiled-specific
    if (props.TryGetValue("solid", out var solidValue))            // ← Tiled-specific
    if (props.TryGetValue("encounter_rate", out var encounterValue)) // ← Tiled-specific
}
```

**Impact**:
- Cannot swap map editors (e.g., to LDtk, Ogmo, Tiled2, etc.)
- Property name changes break the system
- Adding new tile types requires code changes

**Recommendation**: Introduce `ITilePropertyMapper` abstraction layer.

### 🔴 Critical: Missing Abstraction Layer

**Issue**: No intermediate domain model between Tiled and ECS.

**Current Flow**:
```
TiledJsonMap → TmxDocument → ECS Entities
```

**Ideal Flow**:
```
TiledJsonMap → TmxDocument → MapDefinition (domain) → ECS Entities
                              ↑
                         Editor-agnostic
```

**Benefits of MapDefinition**:
- Editor-agnostic intermediate format
- Clear separation of concerns
- Easier testing (mock MapDefinition)
- Multiple input sources (Tiled, LDtk, procedural, etc.)

### 🟡 Medium: Property Mapping is Hardcoded

**Issue**: Property → Component mappings are scattered across code.

**Locations**:
1. `DetermineTileTemplate()` (MapLoader.cs:267-324)
2. `CreateTileEntity()` (MapLoader.cs:326-480)
3. `SpawnMapObjects()` (MapLoader.cs:491-647)

**Problem**: Adding new properties requires editing multiple locations.

**Solution**: Centralized property mapping registry:
```csharp
public interface IPropertyMapper
{
    ComponentTemplate? MapToComponent(string propertyName, object value);
    string? DetermineTemplate(Dictionary<string, object> props);
}
```

### 🟡 Medium: Template Selection Logic is Opaque

**Issue**: Template determination uses priority-based switch statements.

**Current** (lines 267-324):
```csharp
// Priority 1: Ledge
if (props.TryGetValue("ledge_direction", ...)) return "tile/ledge/X";
// Priority 2: Wall
if (props.TryGetValue("solid", ...)) return "tile/wall";
// Priority 3: Grass
if (props.TryGetValue("encounter_rate", ...)) return "tile/grass";
// Default
return "tile/ground";
```

**Problems**:
- Priority order is implicit (code order)
- Cannot be configured externally
- Difficult to extend without modifying code

**Solution**: Rule-based template selector:
```csharp
public class TemplateSelector
{
    private List<(Predicate<Dictionary<string, object>> rule, string template)> _rules;

    public void AddRule(int priority, Predicate<...> rule, string template);
    public string? SelectTemplate(Dictionary<string, object> props);
}
```

### 🟢 Low: Dual-Path Creation Adds Complexity

**Issue**: Template path vs. manual fallback path.

**Code** (lines 352-456):
```csharp
if (_entityFactory != null && templateId != null && HasTemplate(templateId))
{
    // Template-based creation
}
else
{
    // Manual fallback (duplicate logic)
}
```

**Observation**: The fallback path duplicates component creation logic.

**Recommendation**:
- Remove fallback path once all templates are registered
- OR: Generate templates dynamically from properties
- Current: Keep for backward compatibility during transition

### 🟢 Low: Component Addition Uses Reflection

**Issue**: `EntityFactoryService` uses reflection to add components.

**Code** (EntityFactoryService.cs:72-83):
```csharp
foreach (var component in components)
{
    var componentType = component.GetType();
    var addMethod = GetCachedAddMethod(componentType);  // Reflection
    addMethod.Invoke(world, [entity, component]);
}
```

**Mitigation**: Method caching reduces performance impact.

**Alternatives**:
- Source generators (compile-time)
- Generic `AddComponentBatch<T1, T2, ...>()` methods
- Keep current approach (cached reflection is fast enough)

---

## 5. Extensibility Analysis

### ✅ Good Extensibility

| **Aspect** | **How It's Extensible** |
|-----------|------------------------|
| Templates | Add new templates to `TemplateRegistry` |
| Components | Define new component structs, no changes needed |
| Template inheritance | Multi-level inheritance supported |
| Component overrides | `EntityBuilder` fluent API |
| Object types | Add new object types → new templates |

### ❌ Limited Extensibility

| **Aspect** | **Why It's Limited** |
|-----------|---------------------|
| Property mappings | Hardcoded in `DetermineTileTemplate()` |
| Template selection | Switch statement (requires code changes) |
| Map formats | Tightly coupled to Tiled JSON |
| Component addition order | Fixed in template definition |

### 🔧 Extensibility Improvements

1. **Property Mapping Registry**:
```csharp
public class TilePropertyRegistry
{
    private Dictionary<string, Func<object, ComponentTemplate?>> _mappers;

    public void RegisterMapper(string propertyName, Func<object, ComponentTemplate?> mapper);
    public IEnumerable<ComponentTemplate> MapProperties(Dictionary<string, object> props);
}
```

2. **Template Selection DSL**:
```json
{
  "template_rules": [
    { "priority": 1, "if": "ledge_direction == 'down'", "template": "tile/ledge/down" },
    { "priority": 2, "if": "solid == true", "template": "tile/wall" },
    { "priority": 3, "if": "encounter_rate > 0", "template": "tile/grass" },
    { "priority": 99, "default": "tile/ground" }
  ]
}
```

3. **Plugin Architecture**:
```csharp
public interface IMapLoaderPlugin
{
    bool CanHandle(string propertyName);
    void EnrichEntity(Entity entity, string propertyName, object value, World world);
}
```

---

## 6. Recommendations

### Immediate Improvements (Low-Risk)

1. **Extract Property Constants**:
```csharp
public static class TiledPropertyNames
{
    public const string LedgeDirection = "ledge_direction";
    public const string Solid = "solid";
    public const string EncounterRate = "encounter_rate";
    // ... etc
}
```

2. **Document Property Contracts**:
   - Create `docs/tiled-property-reference.md`
   - List all recognized properties and their types
   - Explain component mappings

3. **Add Validation**:
```csharp
private void ValidateTileProperties(Dictionary<string, object> props)
{
    // Warn about unknown properties
    // Validate property types
    // Log mapping decisions
}
```

### Medium-Term Improvements (Moderate Risk)

1. **Introduce Property Mapper Interface**:
```csharp
public interface ITilePropertyMapper
{
    IEnumerable<ComponentTemplate> MapProperties(Dictionary<string, object> props);
    string? DetermineTemplate(Dictionary<string, object> props);
}
```

2. **Centralize Template Selection**:
```csharp
public class TemplateSelector
{
    private readonly List<TemplateRule> _rules;

    public string? SelectTemplate(Dictionary<string, object> props)
    {
        return _rules
            .OrderBy(r => r.Priority)
            .FirstOrDefault(r => r.Matches(props))
            ?.TemplateId;
    }
}
```

3. **Remove Manual Fallback Path**:
   - Ensure all tile types have templates
   - Generate templates dynamically if needed
   - Remove lines 377-456 from MapLoader.cs

### Long-Term Improvements (High Risk, High Reward)

1. **Introduce Domain Model Layer**:
```csharp
public class MapDefinition
{
    public int Width { get; set; }
    public int Height { get; set; }
    public List<TileDefinition> Tiles { get; set; }
    public List<EntityDefinition> Entities { get; set; }
}

public interface IMapLoader
{
    MapDefinition Load(string path);
}

public class TiledMapLoader : IMapLoader { }
public class LDtkMapLoader : IMapLoader { }  // Future
```

2. **Data-Driven Template Selection**:
   - Load template rules from JSON/YAML
   - Support custom rule engines
   - Allow mods to register custom mappings

3. **Decouple from Tiled Format**:
   - Abstract map loading behind `IMapLoader`
   - Support multiple map editors
   - Use domain model as single source of truth

---

## 7. Performance Considerations

### Current Performance Characteristics

| **Operation** | **Complexity** | **Notes** |
|--------------|----------------|-----------|
| JSON Deserialization | O(n) | Single pass |
| Layer Decompression | O(n) | gzip/zlib |
| Tile Entity Creation | O(layers × width × height) | Nested loops |
| Template Resolution | O(inheritance_depth) | Cached |
| Component Addition | O(components) | Reflection (cached) |
| Animated Tile Query | O(animated × tiles) | Post-processing |

### Optimizations in Place

1. **Method Caching** (EntityFactoryService.cs:20):
```csharp
private static readonly ConcurrentDictionary<Type, MethodInfo> _addMethodCache = new();
```

2. **Template Caching** (TemplateCache):
   - Templates loaded once at startup
   - Inheritance resolved once per template

3. **Flip Flag Extraction** (MapLoader.cs:85-90):
```csharp
// Single bitwise operation per tile
var rawGid = (uint)layerData[y, x];
var tileGid = (int)(rawGid & TILE_ID_MASK);
var flipH = (rawGid & FLIPPED_HORIZONTALLY_FLAG) != 0;
```

### Potential Bottlenecks

1. **Large Maps**: O(layers × width × height) entity creation
   - Mitigation: Streaming/chunking for maps > 100×100

2. **Reflection Overhead**: Component addition via reflection
   - Mitigation: Cached MethodInfo reduces impact

3. **Animated Tile Query**: Queries all tile entities
   - Mitigation: Use indexed query on TileGid

---

## 8. Testing Recommendations

### Unit Test Coverage

1. **TiledMapLoader**:
   - Deserialize various JSON formats
   - Handle external tilesets
   - Decompress gzip/zlib
   - Extract flip flags
   - Parse animations

2. **MapLoader**:
   - Template selection logic
   - Property type parsing
   - Coordinate conversion
   - Entity creation (both paths)

3. **EntityFactoryService**:
   - Template inheritance resolution
   - Component override application
   - Validation logic

### Integration Tests

1. **End-to-End Conversion**:
   - Load real Tiled maps
   - Verify entity counts
   - Verify component data
   - Check property preservation

2. **Edge Cases**:
   - Empty maps
   - Missing tilesets
   - Invalid properties
   - Circular template inheritance

---

## 9. Conclusion

The ECS conversion pipeline demonstrates **solid engineering** with template-based entity creation, data-driven property mapping, and performance optimizations. However, it suffers from **tight coupling to Tiled** and **limited extensibility** for custom tile types.

### Strengths
✅ Complete data preservation (~75-100% depending on data type)
✅ Template inheritance reduces duplication
✅ Performance optimizations (caching, bitwise operations)
✅ Dual-path creation ensures backward compatibility

### Weaknesses
❌ Tight coupling to Tiled property names
❌ No abstraction layer for map formats
❌ Hardcoded property mappings
❌ Template selection logic is opaque

### Priority Actions

1. **Short-term**: Extract property name constants, add validation
2. **Medium-term**: Introduce `ITilePropertyMapper` interface
3. **Long-term**: Implement domain model layer for format independence

---

## Appendix: Component Mapping Reference

### Tile Components

| **Component** | **Created By** | **Data Source** |
|--------------|----------------|----------------|
| `TilePosition` | Always | Tile coordinates (x, y, mapId) |
| `TileSprite` | Always | Tile GID, layer, flip flags |
| `Collision` | Property: `solid` or `ledge_direction` | Property value (bool) |
| `TileLedge` | Property: `ledge_direction` | Direction string → enum |
| `EncounterZone` | Property: `encounter_rate` | Rate (int) + table ID (string) |
| `TerrainType` | Property: `terrain_type` | Type string + footstep sound |
| `TileScript` | Property: `script` | Script path (string) |
| `AnimatedTile` | Tileset animation definition | Frame IDs + durations |

### Object Components

| **Component** | **Created By** | **Data Source** |
|--------------|----------------|----------------|
| `Position` | Always | Object coordinates (pixel → tile) |
| `Sprite` | Template | Template definition |
| `Direction` | Property: `direction` | Direction string → enum |
| `Npc` | Property: `npcId` or template | NPC identifier |
| `Name` | Property: `displayName` | Display name string |
| `MovementRoute` | Property: `waypoints` | Parsed point list |
| `GridMovement` | Template | Template definition (speed) |
| `Collision` | Template | Template definition (bool) |
| `Animation` | Template | Template definition (anim ID) |

---

**End of Analysis**
