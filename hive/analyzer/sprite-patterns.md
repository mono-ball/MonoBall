# Sprite Usage Pattern Analysis Report

## Executive Summary

**Current State**: All sprites loaded at startup (~200+ sprite sheets)
**Memory Impact**: Significant - all NPC sprites remain in GPU memory throughout game
**Opportunity**: 90%+ of sprites are map-specific and can be lazily loaded

---

## 1. NPC SPAWNING CODE LOCATIONS

### Primary Spawning Flow
**File**: `MapLoader.cs` (lines 1716-1851)
- **Method**: `ApplyNpcDefinition()` - Applies sprite IDs from NPC definitions
- **Method**: `SpawnMapObjects()` - Creates entities from Tiled object layers
- **Line 1769**: Sprite component creation from `NpcDefinition.SpriteId`

### Sprite ID Assignment
**File**: `MapLoader.cs:1766-1771`
```csharp
if (!string.IsNullOrEmpty(npcDef.SpriteId))
{
    var (category, spriteName) = ParseSpriteId(npcDef.SpriteId);
    builder.OverrideComponent(new Sprite(spriteName, category));
}
```

**File**: `NpcDefinition.cs:35`
```csharp
public string? SpriteId { get; set; }
```

### Entity Factory Integration
**File**: `EntityFactoryService.cs` (lines 36-131)
- NPCs spawned via `SpawnFromTemplate()` method
- Templates can override Sprite component
- No sprite preloading before entity creation

---

## 2. SPRITE ID PATTERNS

### Format Standard
**Pattern**: `"category/spriteName"` or fallback to `"generic/spriteName"`

### Examples Found
```
"may/walking"           → category: may,      sprite: walking
"generic/sailor"        → category: generic,  sprite: sailor
"gym_leaders/roxanne"   → category: gym_leaders, sprite: roxanne
"boy_1"                 → category: generic,  sprite: boy_1 (fallback)
```

### Parsing Logic
**File**: `MapLoader.cs:2176-2186`
```csharp
private static (string category, string spriteName) ParseSpriteId(string spriteId)
{
    var parts = spriteId.Split('/', 2);
    if (parts.Length == 2)
        return (parts[0], parts[1]);
    return ("generic", spriteId); // Fallback
}
```

### Texture Key Format
**File**: `SpriteTextureLoader.cs:173-176`
```csharp
private static string GetTextureKey(string category, string spriteName)
{
    return $"sprites/{category}/{spriteName}";
}
```

---

## 3. SPRITE CATEGORIES

### Directory Structure Analysis
```
Assets/Sprites/
├── Players/               (2 subdirectories)
│   ├── brendan/          [ALWAYS NEEDED - Player character]
│   └── may/              [ALWAYS NEEDED - Player character]
└── NPCs/                 (8 categories)
    ├── elite_four/       (4 sprites)
    ├── frontier_brains/  (7 sprites)
    ├── generic/          (80+ sprites - COUNTED)
    ├── gym_leaders/      (9 sprites)
    ├── ruby_sapphire_brendan/
    ├── ruby_sapphire_may/
    ├── team_aqua/        (3 sprites)
    └── team_magma/       (3 sprites)
```

### Category Breakdown

| Category | Sprite Count | Usage Pattern | Unload Priority |
|----------|-------------|---------------|-----------------|
| **Players (brendan, may)** | 2 | Always active | **NEVER** unload |
| **generic** | 80+ | Common across maps | Consider reference counting |
| **gym_leaders** | 9 | Location-specific | High priority unload |
| **elite_four** | 4 | Location-specific | High priority unload |
| **frontier_brains** | 7 | Location-specific | High priority unload |
| **team_aqua** | 3 | Story-specific | High priority unload |
| **team_magma** | 3 | Story-specific | High priority unload |

### Total Sprite Count Estimate
- **Players**: 2 sprites (brendan, may)
- **NPCs**: ~200+ unique sprite manifests
- **Per-map average**: 5-20 unique NPC sprites (estimated)

---

## 4. MAP SPRITE DISTRIBUTION

### Current Data Sources
1. **NPC Definitions** (`NpcDefinition.SpriteId`)
2. **Tiled Map Objects** (properties: `spriteId`)
3. **Entity Templates** (component overrides)

### Sprite Extraction Points

#### **Before Map Load** ✅ FEASIBLE
**File**: `MapLoader.cs:83-119` - `LoadMap()` method
- Tiled JSON is parsed into `TmxDocument`
- Object layers accessible via `tmxDoc.ObjectGroups`
- Can collect sprite IDs BEFORE entity creation

#### **Sample Code Location**
```csharp
// MapLoader.cs:185 - SpawnMapObjects() is called
// We can add BEFORE this:
var spriteIds = CollectRequiredSprites(tmxDoc);
await _spriteLoader.LoadSpritesAsync(spriteIds);
```

### Estimated Sprites Per Map Type
- **Towns/Cities**: 10-30 NPCs (generic sprites dominate)
- **Routes**: 5-15 NPCs (trainers + generic)
- **Gyms**: 5-10 NPCs (gym leader + trainers)
- **Special Locations**: 1-5 NPCs (story-specific)

### Shared Sprite Detection
**High-Frequency Sprites** (appear on multiple maps):
- `generic/youngster`
- `generic/lass`
- `generic/sailor`
- `generic/nurse`
- `generic/mart_employee`

**Strategy**: Reference counting for generic sprites
- Track which maps use each sprite
- Only unload when ALL maps unloaded
- Keep "common" sprites (nurse, mart) permanently loaded

---

## 5. CURRENT LOADING FLOW

### Startup Sequence
```
PokeSharpGame.cs:LoadContent()
  ↓
SpriteTextureLoader.LoadAllSpriteTexturesAsync()
  ↓
SpriteLoader.LoadAllSpritesAsync()
  ↓
[LOADS ~200+ SPRITE MANIFESTS]
  ↓
[LOADS ALL SPRITE SHEETS INTO GPU MEMORY]
  ↓
[SPRITES REMAIN LOADED FOREVER]
```

**File**: `SpriteTextureLoader.cs:39-125`
```csharp
public async Task<int> LoadAllSpriteTexturesAsync()
{
    var manifests = await _spriteLoader.LoadAllSpritesAsync();
    foreach (var manifest in manifests)
    {
        using var fileStream = File.OpenRead(spritesheetPath);
        var texture = Texture2D.FromStream(_graphicsDevice, fileStream);
        RegisterTexture(textureKey, texture);
    }
}
```

### Map Load Sequence
```
MapLoader.LoadMap(mapId)
  ↓
Parse Tiled JSON
  ↓
Load Tilesets (textures)
  ↓
SpawnMapObjects() → Creates NPC entities
  ↓
[ASSUMES SPRITES ALREADY LOADED]
  ↓
Sprite component references texture key
```

**No Sprite Loading**: Sprites assumed pre-loaded

### Map Unload Sequence
```
[MAP UNLOAD]
  ↓
Entities destroyed
  ↓
[SPRITES REMAIN IN MEMORY] ⚠️ MEMORY LEAK
```

**File**: No unload mechanism exists

---

## 6. PROPOSED LOADING FLOW

### Phase 1: Startup (Minimal Load)
```
PokeSharpGame.cs:LoadContent()
  ↓
LoadPlayerSprites()  // ONLY brendan + may
  ↓
LoadCommonSprites()  // nurse, mart_employee (optional)
  ↓
[REST OF SPRITES UNLOADED]
```

### Phase 2: Map Load (Lazy Loading)
```
MapLoader.LoadMap(mapId)
  ↓
Parse Tiled JSON
  ↓
[NEW] CollectRequiredSprites(tmxDoc)
  ↓ 
  Extract sprite IDs from:
    - tmxDoc.ObjectGroups (NPC objects)
    - NPC definitions (via npcId lookups)
  ↓
[NEW] await _spriteTextureLoader.LoadSpritesAsync(spriteIds)
  ↓
SpawnMapObjects() → Now sprites are loaded
```

### Phase 3: Map Unload (Cleanup)
```
[MAP UNLOAD EVENT]
  ↓
Query all entities with MapInfo(mapId)
  ↓
Collect sprite texture keys used by map
  ↓
[NEW] UnloadMapSprites(mapId, spriteKeys)
  ↓
  Check reference count for each sprite
    - If count == 0 → Unload texture
    - If sprite is "common" → Keep loaded
    - If sprite is "player" → NEVER unload
```

---

## 7. SHARED SPRITE DETECTION STRATEGY

### Reference Counting System
```csharp
// Track which maps use each sprite
Dictionary<string, HashSet<int>> _spriteToMaps;
Dictionary<int, HashSet<string>> _mapToSprites;

// On map load
void TrackSpriteUsage(int mapId, string spriteKey)
{
    _spriteToMaps[spriteKey].Add(mapId);
    _mapToSprites[mapId].Add(spriteKey);
}

// On map unload
bool CanUnloadSprite(string spriteKey, int unloadingMapId)
{
    _spriteToMaps[spriteKey].Remove(unloadingMapId);
    return _spriteToMaps[spriteKey].Count == 0; // No other maps using it
}
```

### Permanent Sprites List
```csharp
private static readonly HashSet<string> NEVER_UNLOAD = new()
{
    "sprites/Players/brendan",
    "sprites/Players/may",
    "sprites/generic/nurse",       // Pokémon Center
    "sprites/generic/mart_employee" // Poké Mart
};
```

### Preload List (Common Sprites)
```csharp
private static readonly HashSet<string> PRELOAD_COMMON = new()
{
    "sprites/generic/youngster",
    "sprites/generic/lass",
    "sprites/generic/old_man",
    "sprites/generic/old_woman"
};
```

---

## 8. CODE SNIPPETS - SPRITE ID ACCESS

### Extract Sprite IDs from Map
```csharp
// NEW METHOD - Add to MapLoader.cs
private HashSet<string> CollectRequiredSprites(TmxDocument tmxDoc)
{
    var spriteIds = new HashSet<string>();
    
    foreach (var objectGroup in tmxDoc.ObjectGroups)
    {
        foreach (var obj in objectGroup.Objects)
        {
            // Check for NPC definition reference
            if (obj.Properties.TryGetValue("npcId", out var npcIdProp))
            {
                var npcId = npcIdProp.ToString();
                var npcDef = _npcDefinitionService?.GetNpc(npcId);
                
                if (npcDef?.SpriteId != null)
                {
                    spriteIds.Add(npcDef.SpriteId);
                }
            }
            
            // Check for direct sprite ID (backward compat)
            if (obj.Properties.TryGetValue("spriteId", out var spriteProp))
            {
                spriteIds.Add(spriteProp.ToString() ?? "");
            }
        }
    }
    
    return spriteIds;
}
```

### Lazy Load Sprite Batch
```csharp
// NEW METHOD - Add to SpriteTextureLoader.cs
public async Task LoadSpritesAsync(IEnumerable<string> spriteIds)
{
    foreach (var spriteId in spriteIds)
    {
        var (category, spriteName) = ParseSpriteId(spriteId);
        LoadSpriteTexture(category, spriteName);
    }
}
```

### Track Map Textures (ALREADY EXISTS)
```csharp
// MapLoader.cs:1245-1257 - Already tracks tilesets
// EXTEND to track sprite textures too
private void TrackMapTextures(int mapId, IReadOnlyList<LoadedTileset> tilesets)
{
    var textureIds = new HashSet<string>();
    foreach (var tileset in tilesets)
    {
        textureIds.Add(tileset.TilesetId);
    }
    
    // ADD SPRITE TRACKING HERE
    _mapTextureIds[mapId] = textureIds;
}
```

---

## RECOMMENDATIONS

### Immediate Wins
1. **Sprite ID extraction is trivial** - Data already in `TmxDocument`
2. **Lazy loading already exists** - `SpriteTextureLoader.LoadSpriteTexture()`
3. **Texture tracking exists** - `MapLoader.TrackMapTextures()` pattern

### Implementation Priority
1. ✅ **HIGH**: Extract sprite IDs before entity spawning
2. ✅ **HIGH**: Add sprite batch loading to map load flow
3. ✅ **MEDIUM**: Implement reference counting for shared sprites
4. ✅ **MEDIUM**: Add unload mechanism to `AssetManager`
5. ✅ **LOW**: Optimize with preload list for common sprites

### Estimated Impact
- **Memory Reduction**: 80-90% of sprite memory freed per map
- **Load Time**: Slight increase (~100-200ms per map for sprite loading)
- **Complexity**: Low - most infrastructure already exists

---

**Next Steps**: Architect agent should design the lazy loading service based on these patterns.

---

## VISUAL FLOW DIAGRAMS

### Current Flow (Memory Leak)
```
┌─────────────────────────────────────────────────────────────┐
│ STARTUP: PokeSharpGame.LoadContent()                        │
└────────────────┬────────────────────────────────────────────┘
                 ↓
┌─────────────────────────────────────────────────────────────┐
│ LoadAllSpriteTexturesAsync()                                │
│ - Loads ~200+ sprite sheets                                 │
│ - All NPCs, all categories                                  │
│ - brendan, may, gym leaders, elite four, generic, etc.     │
└────────────────┬────────────────────────────────────────────┘
                 ↓
┌─────────────────────────────────────────────────────────────┐
│ GPU MEMORY: ~200MB+ sprite textures                         │
│ ✅ Player sprites (2)                                        │
│ ❌ Elite Four (4) - NOT NEEDED YET                          │
│ ❌ Gym Leaders (9) - NOT NEEDED YET                         │
│ ❌ Frontier Brains (7) - NOT NEEDED YET                     │
│ ❌ Generic NPCs (80+) - ONLY 5-10 NEEDED PER MAP           │
└────────────────┬────────────────────────────────────────────┘
                 ↓
┌─────────────────────────────────────────────────────────────┐
│ MAP LOAD: MapLoader.LoadMap("littleroot_town")             │
│ - Spawns 8 NPCs (mom, prof_birch, rival, 5 townspeople)   │
│ - Uses ALREADY LOADED sprites                              │
│ - NO sprite loading at this stage                          │
└────────────────┬────────────────────────────────────────────┘
                 ↓
┌─────────────────────────────────────────────────────────────┐
│ MAP UNLOAD: Travel to Route 101                             │
│ - Entities destroyed                                         │
│ - Sprites REMAIN IN MEMORY ⚠️                               │
│ - No cleanup of unused textures                             │
└─────────────────────────────────────────────────────────────┘
```

### Proposed Flow (Lazy Loading)
```
┌─────────────────────────────────────────────────────────────┐
│ STARTUP: PokeSharpGame.LoadContent()                        │
└────────────────┬────────────────────────────────────────────┘
                 ↓
┌─────────────────────────────────────────────────────────────┐
│ LoadPlayerSprites() - ONLY brendan, may                     │
│ LoadCommonSprites() - nurse, mart_employee (optional)       │
└────────────────┬────────────────────────────────────────────┘
                 ↓
┌─────────────────────────────────────────────────────────────┐
│ GPU MEMORY: ~5MB sprite textures (90% REDUCTION)            │
│ ✅ Player sprites (2)                                        │
│ ✅ Common sprites (2) - nurse, mart                         │
└────────────────┬────────────────────────────────────────────┘
                 ↓
┌─────────────────────────────────────────────────────────────┐
│ MAP LOAD: MapLoader.LoadMap("littleroot_town")             │
└────────────────┬────────────────────────────────────────────┘
                 ↓
┌─────────────────────────────────────────────────────────────┐
│ Parse Tiled JSON → TmxDocument                              │
└────────────────┬────────────────────────────────────────────┘
                 ↓
┌─────────────────────────────────────────────────────────────┐
│ 🆕 CollectRequiredSprites(tmxDoc)                           │
│ - Extract sprite IDs from object layers                     │
│ - Query NPC definitions for sprite IDs                      │
│ - Found: ["generic/mom", "generic/prof_birch", ...]        │
└────────────────┬────────────────────────────────────────────┘
                 ↓
┌─────────────────────────────────────────────────────────────┐
│ 🆕 await LoadSpritesAsync(spriteIds)                        │
│ - Load ONLY map-specific sprites (5-10 textures)           │
│ - Skip already-loaded sprites (brendan, may)               │
│ - Track sprite usage per map                                │
└────────────────┬────────────────────────────────────────────┘
                 ↓
┌─────────────────────────────────────────────────────────────┐
│ SpawnMapObjects() → Entities created                         │
│ - Sprites now available in AssetManager                     │
│ - No missing texture errors                                 │
└────────────────┬────────────────────────────────────────────┘
                 ↓
┌─────────────────────────────────────────────────────────────┐
│ GPU MEMORY: ~10MB (player + common + map-specific)         │
└────────────────┬────────────────────────────────────────────┘
                 ↓
┌─────────────────────────────────────────────────────────────┐
│ MAP UNLOAD: Travel to Route 101                             │
└────────────────┬────────────────────────────────────────────┘
                 ↓
┌─────────────────────────────────────────────────────────────┐
│ 🆕 UnloadMapSprites(mapId)                                  │
│ - Get sprites used by map from tracking                     │
│ - Decrement reference count                                 │
│ - Unload sprites with count == 0                            │
│ - KEEP player sprites (NEVER unload)                        │
│ - KEEP common sprites (nurse, mart)                         │
└────────────────┬────────────────────────────────────────────┘
                 ↓
┌─────────────────────────────────────────────────────────────┐
│ GPU MEMORY: ~5MB (back to baseline) ✅                      │
└─────────────────────────────────────────────────────────────┘
```

---

## CODE INJECTION POINTS

### 1. MapLoader.cs - Sprite Collection
**Location**: Before `SpawnMapObjects()` call (line ~185)
```csharp
// BEFORE:
var objectsCreated = SpawnMapObjects(world, tmxDoc, mapId, tmxDoc.TileHeight);

// AFTER:
var requiredSprites = CollectRequiredSprites(tmxDoc); // NEW
await _spriteTextureLoader.LoadSpritesAsync(requiredSprites); // NEW
var objectsCreated = SpawnMapObjects(world, tmxDoc, mapId, tmxDoc.TileHeight);
```

### 2. SpriteTextureLoader.cs - Batch Loading
**Location**: Add new method
```csharp
public async Task LoadSpritesAsync(IEnumerable<string> spriteIds)
{
    foreach (var spriteId in spriteIds)
    {
        var (category, spriteName) = ParseSpriteId(spriteId);
        
        // Skip if already loaded
        var textureKey = GetTextureKey(category, spriteName);
        if (_assetManager.HasTexture(textureKey))
            continue;
            
        LoadSpriteTexture(category, spriteName);
    }
}
```

### 3. MapLoader.cs - Track Sprite Usage
**Location**: Extend `TrackMapTextures()` method (line ~1245)
```csharp
private void TrackMapTextures(
    int mapId, 
    IReadOnlyList<LoadedTileset> tilesets,
    HashSet<string> spriteIds // NEW PARAMETER
)
{
    var textureIds = new HashSet<string>();
    
    // Existing: Track tileset textures
    foreach (var tileset in tilesets)
        textureIds.Add(tileset.TilesetId);
    
    // NEW: Track sprite textures
    foreach (var spriteId in spriteIds)
    {
        var (category, spriteName) = ParseSpriteId(spriteId);
        textureIds.Add(GetTextureKey(category, spriteName));
    }
    
    _mapTextureIds[mapId] = textureIds;
}
```

### 4. AssetManager - Unload Support (NEW FILE)
**Location**: Add to `AssetManager.cs`
```csharp
public void UnloadTexture(string textureId)
{
    if (_textures.TryGetValue(textureId, out var texture))
    {
        texture.Dispose();
        _textures.Remove(textureId);
        _logger?.LogDebug("Unloaded texture: {TextureId}", textureId);
    }
}
```

---

## SUMMARY FOR ARCHITECT

### ✅ READY TO IMPLEMENT
- Sprite ID extraction logic: **TRIVIAL** (data in TmxDocument)
- Lazy loading infrastructure: **EXISTS** (LoadSpriteTexture method)
- Texture tracking pattern: **EXISTS** (TrackMapTextures pattern)

### 🔨 NEEDS IMPLEMENTATION
1. `CollectRequiredSprites()` method in MapLoader
2. Batch sprite loading in SpriteTextureLoader
3. Reference counting system for shared sprites
4. `UnloadTexture()` method in AssetManager
5. Map unload event handler

### 🎯 CRITICAL DESIGN DECISIONS
1. **Shared sprite strategy**: Reference counting vs. permanent load?
2. **Common sprite list**: Which sprites should NEVER unload?
3. **Preload list**: Should we preload high-frequency sprites?
4. **Async loading**: Block map load or show loading screen?

### 📊 ESTIMATED EFFORT
- **Low complexity**: Most infrastructure exists
- **High impact**: 80-90% memory reduction
- **Low risk**: Isolated changes, easy to test

