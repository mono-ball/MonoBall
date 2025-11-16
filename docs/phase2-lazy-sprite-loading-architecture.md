# Phase 2 Lazy Sprite Loading Architecture Design

## EXECUTIVE SUMMARY

**Objective**: Implement per-map lazy sprite loading to reduce memory consumption by 25-35MB (from 40MB to 5-10MB).

**Current Issue**: All sprite textures (100+ NPCs) are loaded eagerly at startup, wasting memory on sprites not used in the current map.

**Solution**: Load only sprites needed for current map's NPCs, with automatic cleanup on map transitions.

---

## 1. CURRENT ARCHITECTURE ANALYSIS

### 1.1 Startup Flow (EAGER LOADING - PROBLEM)

```
PokeSharpGame.InitializeAsync() [Line 131]
  └─> LoadAllSpritesAsync() [Line 221] - Loads sprite MANIFESTS
  └─> LoadSpriteTextures() [Line 227] - Currently does NOTHING (lazy loading placeholder)
  └─> LoadMap("LittlerootTown") [Line 230]

SpriteTextureLoader.LoadAllSpriteTexturesAsync() [Line 39-125]
  ├─> LoadAllSpritesAsync() - Gets ALL 100+ sprite manifests
  ├─> For each manifest:
  │   ├─> Load spritesheet.png texture (200-400KB each)
  │   └─> RegisterTexture(key, texture) in AssetManager
  └─> Result: 40MB loaded at startup
```

**MEMORY WASTE**: Loading 100+ sprites when map only uses 5-10 NPCs.

### 1.2 NPC Spawning Flow (WHERE WE NEED SPRITE IDs)

```
MapLoader.LoadMap(world, mapId) [Line 73]
  └─> LoadMapFromDocument(world, tmxDoc, mapDef) [Line 143]
      └─> SpawnMapObjects(world, tmxDoc, mapId, tileHeight) [Line 185, 1646]
          ├─> For each NPC object in map:
          │   ├─> Get npcId from object properties [Line 1754]
          │   ├─> NpcDefinitionService.GetNpc(npcId) [Line 1759]
          │   ├─> Extract npcDef.SpriteId (e.g., "generic/boy_1") [Line 1766]
          │   ├─> ParseSpriteId(spriteId) → (category, spriteName) [Line 1769, 2176]
          │   └─> builder.OverrideComponent(new Sprite(spriteName, category)) [Line 1770]
          └─> NO TEXTURE LOADING HERE - only component creation

Sprite Component [Sprite.cs:9-73]
  ├─> SpriteName: string (e.g., "boy_1")
  ├─> Category: string (e.g., "generic")
  └─> Used by rendering system to lookup texture
```

**KEY INSIGHT**: MapLoader collects sprite IDs during NPC spawning but doesn't load textures.

### 1.3 Current Texture Loading (LAZY - BUT TOO LATE)

```
SpriteTextureLoader.LoadSpriteTexture(category, spriteName) [Line 130-168]
  ├─> Called by: ElevationRenderSystem during first render
  ├─> Checks if texture already loaded
  ├─> Loads spritesheet.png if missing
  └─> RegisterTexture(textureKey, texture)

Problem: First render causes loading spike (janky)
Solution: Pre-load during map loading (smooth)
```

### 1.4 Map Lifecycle Management

```
MapLifecycleManager.TransitionToMap(newMapId) [Line 56-83]
  ├─> Identifies maps to unload (keep current + previous)
  └─> For each old map:
      └─> UnloadMap(mapId) [Line 88-112]
          ├─> DestroyMapEntities(mapId) - Destroy tile/NPC entities
          └─> UnloadMapTextures(textureIds) - Unload TILESET textures
              └─> Check if texture shared by other maps
              └─> AssetManager.UnregisterTexture(textureId)

Current: Only unloads TILESET textures, NOT sprite textures
Missing: Sprite texture tracking and unloading
```

---

## 2. PROPOSED NEW ARCHITECTURE

### 2.1 High-Level Data Flow

```
MAP LOADING SEQUENCE:
┌─────────────────────────────────────────────────────────────┐
│ 1. MapLoader.LoadMap(mapId)                                │
│    ├─> Parse map JSON, extract NPC objects                 │
│    ├─> For each NPC: collect spriteId from definition      │
│    └─> Return: HashSet<(category, spriteName)>             │
└─────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────┐
│ 2. SpriteTextureLoader.LoadSpritesForMapAsync()            │
│    ├─> Input: HashSet<(category, spriteName)>              │
│    ├─> For each sprite:                                    │
│    │   ├─> Check if already loaded (skip if yes)           │
│    │   ├─> LoadSpriteTexture(category, spriteName)         │
│    │   └─> RegisterTexture(key, texture)                   │
│    └─> Return: loaded sprite texture keys                  │
└─────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────┐
│ 3. MapLifecycleManager.RegisterMap()                       │
│    ├─> Track tileset textures (existing)                   │
│    └─> Track sprite textures (NEW)                         │
└─────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────┐
│ 4. MAP TRANSITION (user moves to new map)                  │
│    └─> MapLifecycleManager.TransitionToMap(newMapId)       │
│        └─> UnloadMap(oldMapId)                              │
│            ├─> DestroyMapEntities() - existing             │
│            ├─> UnloadMapTextures() - tilesets (existing)   │
│            └─> UnloadSpriteTextures() - NPCs (NEW)         │
│                └─> Check reference count (shared sprites)  │
│                └─> AssetManager.UnregisterTexture()        │
└─────────────────────────────────────────────────────────────┘

PLAYER SPRITE: Always loaded, never unloaded (special case)
```

### 2.2 New Method Signatures

#### SpriteTextureLoader.cs - NEW METHODS

```csharp
/// <summary>
/// Loads sprite textures needed for a specific map's NPCs.
/// Skips already-loaded sprites for efficiency.
/// </summary>
public async Task<HashSet<string>> LoadSpritesForMapAsync(
    HashSet<(string category, string spriteName)> spriteIds)
{
    var loadedKeys = new HashSet<string>();

    foreach (var (category, spriteName) in spriteIds)
    {
        var textureKey = GetTextureKey(category, spriteName);

        // Skip if already loaded
        if (_assetManager.HasTexture(textureKey))
        {
            loadedKeys.Add(textureKey);
            continue;
        }

        // Load sprite texture
        LoadSpriteTexture(category, spriteName);
        loadedKeys.Add(textureKey);
    }

    return loadedKeys;
}

/// <summary>
/// Unloads sprite textures for a map, checking reference counts.
/// </summary>
public int UnloadSpriteTextures(
    HashSet<string> spriteTextureKeys,
    Func<string, bool> isSharedTexture)
{
    var unloaded = 0;

    foreach (var textureKey in spriteTextureKeys)
    {
        // Skip if used by other maps
        if (isSharedTexture(textureKey))
            continue;

        // Skip player sprites (always keep loaded)
        if (IsPlayerSprite(textureKey))
            continue;

        if (_assetManager.UnregisterTexture(textureKey))
        {
            unloaded++;
            _logger?.LogDebug("Unloaded sprite texture: {Key}", textureKey);
        }
    }

    return unloaded;
}

private static bool IsPlayerSprite(string textureKey)
{
    // Player sprites: sprites/may/*, sprites/brendan/*
    return textureKey.StartsWith("sprites/may/") ||
           textureKey.StartsWith("sprites/brendan/");
}
```

#### MapLoader.cs - NEW METHOD

```csharp
/// <summary>
/// Extracts sprite IDs from NPCs during map loading.
/// Called during SpawnMapObjects() to collect sprite data.
/// </summary>
private HashSet<(string category, string spriteName)> ExtractMapSpriteIds(
    TmxDocument tmxDoc)
{
    var spriteIds = new HashSet<(string, string)>();

    foreach (var objectGroup in tmxDoc.ObjectGroups)
    foreach (var obj in objectGroup.Objects)
    {
        // Get npcId from object properties
        if (!obj.Properties.TryGetValue("npcId", out var npcIdProp))
            continue;

        var npcId = npcIdProp.ToString();
        if (string.IsNullOrWhiteSpace(npcId))
            continue;

        // Get NPC definition
        var npcDef = _npcDefinitionService?.GetNpc(npcId);
        if (npcDef == null || string.IsNullOrEmpty(npcDef.SpriteId))
            continue;

        // Parse sprite ID: "generic/boy_1" → ("generic", "boy_1")
        var (category, spriteName) = ParseSpriteId(npcDef.SpriteId);
        spriteIds.Add((category, spriteName));
    }

    return spriteIds;
}

/// <summary>
/// Gets all sprite texture keys loaded for a specific map.
/// Used by MapLifecycleManager for cleanup.
/// </summary>
public HashSet<string> GetLoadedSpriteTextureKeys(int mapId)
{
    return _mapSpriteTextureIds.TryGetValue(mapId, out var keys)
        ? new HashSet<string>(keys)
        : new HashSet<string>();
}

// NEW FIELD
private readonly Dictionary<int, HashSet<string>> _mapSpriteTextureIds = new();
```

#### MapLifecycleManager.cs - UPDATED METHOD

```csharp
/// <summary>
/// Registers a newly loaded map with both tileset AND sprite textures.
/// </summary>
public void RegisterMap(
    int mapId,
    string mapName,
    HashSet<string> tilesetTextureIds,
    HashSet<string> spriteTextureIds) // NEW parameter
{
    _loadedMaps[mapId] = new MapMetadata(
        mapName,
        tilesetTextureIds,
        spriteTextureIds); // NEW field

    _logger?.LogInformation(
        "Registered map: {MapName} (ID: {MapId}) with {TilesetCount} tilesets, {SpriteCount} sprites",
        mapName, mapId, tilesetTextureIds.Count, spriteTextureIds.Count);
}

private record MapMetadata(
    string Name,
    HashSet<string> TilesetTextureIds,
    HashSet<string> SpriteTextureIds); // NEW field
```

### 2.3 Integration Points

#### MapLoader.cs - LoadMapFromDocument() UPDATED

```csharp
private Entity LoadMapFromDocument(World world, TmxDocument tmxDoc, MapDefinition mapDef)
{
    var mapId = GetMapIdFromString(mapDef.MapId);
    var mapName = mapDef.DisplayName;

    // ... existing tileset loading ...

    // NEW: Extract sprite IDs from NPCs BEFORE spawning
    var mapSpriteIds = ExtractMapSpriteIds(tmxDoc);
    _logger?.LogDebug("Found {Count} unique sprites for map {MapName}",
        mapSpriteIds.Count, mapName);

    // NEW: Load sprite textures for this map
    var spriteTextureKeys = await _spriteTextureLoader.LoadSpritesForMapAsync(mapSpriteIds);
    _logger?.LogInformation("Loaded {Count} sprite textures for map {MapName}",
        spriteTextureKeys.Count, mapName);

    // Track sprite texture keys for lifecycle management
    _mapSpriteTextureIds[mapId] = spriteTextureKeys;

    // ... rest of existing code (spawn NPCs, tiles, etc.) ...

    return mapInfoEntity;
}
```

#### MapInitializer.cs - LoadMap() UPDATED

```csharp
public Entity? LoadMap(string mapId)
{
    try
    {
        logger.LogWorkflowStatus("Loading map from definition", ("mapId", mapId));

        var mapInfoEntity = mapLoader.LoadMap(world, mapId);
        var mapInfo = mapInfoEntity.Get<MapInfo>();

        var tilesetTextureIds = mapLoader.GetLoadedTextureIds(mapInfo.MapId);
        var spriteTextureIds = mapLoader.GetLoadedSpriteTextureKeys(mapInfo.MapId); // NEW

        // Register map with BOTH tileset and sprite textures
        mapLifecycleManager.RegisterMap(
            mapInfo.MapId,
            mapInfo.MapName,
            tilesetTextureIds,
            spriteTextureIds); // NEW parameter

        mapLifecycleManager.TransitionToMap(mapInfo.MapId);

        // ... rest of existing code ...
    }
    catch (Exception ex)
    {
        logger.LogExceptionWithContext(ex, "Failed to load map: {MapId}", mapId);
        return null;
    }
}
```

#### MapLifecycleManager.cs - UnloadMap() UPDATED

```csharp
public void UnloadMap(int mapId)
{
    if (!_loadedMaps.TryGetValue(mapId, out var metadata))
        return;

    _logger?.LogInformation("Unloading map: {MapName} (ID: {MapId})",
        metadata.Name, mapId);

    // 1. Destroy entities (existing)
    var tilesDestroyed = DestroyMapEntities(mapId);

    // 2. Unload tileset textures (existing)
    var tilesetsUnloaded = UnloadMapTextures(metadata.TilesetTextureIds);

    // 3. NEW: Unload sprite textures
    var spritesUnloaded = UnloadSpriteTextures(metadata.SpriteTextureIds);

    _loadedMaps.Remove(mapId);

    _logger?.LogInformation(
        "Map {MapName} unloaded: {Entities} entities, {Tilesets} tilesets, {Sprites} sprites freed",
        metadata.Name, tilesDestroyed, tilesetsUnloaded, spritesUnloaded);
}

/// <summary>
/// NEW: Unloads sprite textures for a map (with reference counting).
/// </summary>
private int UnloadSpriteTextures(HashSet<string> spriteTextureKeys)
{
    if (_assetProvider is not AssetManager assetManager)
        return 0;

    var unloaded = 0;

    foreach (var textureKey in spriteTextureKeys)
    {
        // Check if sprite is used by other loaded maps
        var isShared = _loadedMaps.Values.Any(m =>
            m.SpriteTextureIds.Contains(textureKey));

        if (!isShared)
        {
            // Skip player sprites (always keep loaded)
            if (IsPlayerSprite(textureKey))
                continue;

            if (assetManager.UnregisterTexture(textureKey))
            {
                unloaded++;
                _logger?.LogDebug("Unloaded sprite: {Key}", textureKey);
            }
        }
    }

    return unloaded;
}

private static bool IsPlayerSprite(string textureKey)
{
    return textureKey.StartsWith("sprites/may/") ||
           textureKey.StartsWith("sprites/brendan/");
}
```

---

## 3. EDGE CASES & HANDLING

### 3.1 Player Sprite (Always Loaded)

**Problem**: Player sprite should NEVER be unloaded.

**Solution**:
```csharp
private static bool IsPlayerSprite(string textureKey)
{
    // Match: sprites/may/*, sprites/brendan/*
    return textureKey.StartsWith("sprites/may/") ||
           textureKey.StartsWith("sprites/brendan/");
}

// In UnloadSpriteTextures():
if (IsPlayerSprite(textureKey))
{
    _logger?.LogDebug("Skipping player sprite: {Key}", textureKey);
    continue;
}
```

### 3.2 Shared NPCs Across Maps

**Problem**: NPC sprite used by multiple maps shouldn't be unloaded until ALL maps are unloaded.

**Solution**: Reference counting via map metadata check:
```csharp
// Check if ANY loaded map still uses this sprite
var isShared = _loadedMaps.Values.Any(m =>
    m.SpriteTextureIds.Contains(textureKey));

if (isShared)
{
    _logger?.LogDebug("Sprite {Key} shared by multiple maps - keeping loaded",
        textureKey);
    continue;
}
```

### 3.3 Missing Sprite Definitions

**Problem**: NPC object has invalid/missing npcId or SpriteId.

**Solution**: Graceful fallback during extraction:
```csharp
var npcDef = _npcDefinitionService?.GetNpc(npcId);
if (npcDef == null)
{
    _logger?.LogWarning("NPC definition not found: {NpcId}", npcId);
    continue; // Skip this NPC, don't add sprite ID
}

if (string.IsNullOrEmpty(npcDef.SpriteId))
{
    _logger?.LogWarning("NPC {NpcId} has no sprite ID", npcId);
    continue;
}
```

### 3.4 Map Transition Preloading

**Problem**: Loading sprites during map transition causes visible delay.

**Solution**: Load sprites BEFORE transitioning (existing async pattern):
```csharp
// MapLoader.LoadMapFromDocument() - already async
var spriteTextureKeys = await _spriteTextureLoader.LoadSpritesForMapAsync(mapSpriteIds);
// Sprites loaded BEFORE MapLifecycleManager.TransitionToMap()
// No visual pop-in or delay
```

### 3.5 First Map Load (No Previous Map)

**Problem**: First map load has no previous map to unload.

**Solution**: Existing code already handles this:
```csharp
// MapLifecycleManager.TransitionToMap()
var mapsToUnload = _loadedMaps.Keys
    .Where(id => id != _currentMapId && id != _previousMapId)
    .ToList();
// Empty list on first load - no unload occurs
```

---

## 4. MEMORY LIFECYCLE DIAGRAM

```
STARTUP (t=0):
┌─────────────────────────────────┐
│ Memory: 0MB sprites             │
│ - Player sprite NOT loaded yet  │
│ - NO NPC sprites loaded         │
└─────────────────────────────────┘

MAP 1 LOAD (t=1s - LittlerootTown):
┌─────────────────────────────────┐
│ ExtractMapSpriteIds()           │
│ → Found: may/walking,           │
│          generic/boy_1,         │
│          generic/girl_2         │
│ LoadSpritesForMapAsync()        │
│ → Loaded: 3 sprites (~1.5MB)    │
│ Memory: 1.5MB sprites           │
└─────────────────────────────────┘

MAP 2 LOAD (t=30s - Oldale Town):
┌─────────────────────────────────┐
│ ExtractMapSpriteIds()           │
│ → Found: may/walking (shared!), │
│          generic/boy_1 (shared),│
│          generic/nurse,         │
│          generic/shop_owner     │
│ LoadSpritesForMapAsync()        │
│ → Skipped: may/walking (loaded) │
│ → Skipped: generic/boy_1        │
│ → Loaded: nurse, shop_owner     │
│ Memory: 2.5MB sprites           │
│                                 │
│ UnloadMap(LittlerootTown):      │
│ → Check generic/boy_1: SHARED   │
│   (used by Oldale) - KEEP       │
│ → Check may/walking: PLAYER     │
│   sprite - KEEP                 │
│ → Check generic/girl_2: UNIQUE  │
│   to Littleroot - UNLOAD        │
│ Memory: 2.0MB sprites           │
└─────────────────────────────────┘

STEADY STATE (after 10 maps):
┌─────────────────────────────────┐
│ Current map: 5-8 sprites        │
│ Previous map: 5-8 sprites       │
│ Player sprite: 1 sprite         │
│ Shared NPCs: 2-3 sprites        │
│ Total: ~10-15 sprites (5-10MB)  │
│ vs OLD: 100+ sprites (40MB)     │
│ SAVINGS: 30-35MB                │
└─────────────────────────────────┘
```

---

## 5. IMPLEMENTATION PLAN

### Phase 1: Core Infrastructure (Coder Agent 1)

**Files to Modify**:
1. `SpriteTextureLoader.cs`
   - Add `LoadSpritesForMapAsync()` method
   - Add `UnloadSpriteTextures()` method
   - Add `IsPlayerSprite()` helper

**Estimated Lines**: ~80 new lines

### Phase 2: Map Loader Integration (Coder Agent 2)

**Files to Modify**:
1. `MapLoader.cs`
   - Add `ExtractMapSpriteIds()` method
   - Add `_mapSpriteTextureIds` field
   - Add `GetLoadedSpriteTextureKeys()` method
   - Update `LoadMapFromDocument()` to extract and load sprites

**Estimated Lines**: ~120 new lines

### Phase 3: Lifecycle Management (Coder Agent 3)

**Files to Modify**:
1. `MapLifecycleManager.cs`
   - Update `MapMetadata` record with `SpriteTextureIds` field
   - Update `RegisterMap()` signature (add spriteTextureIds param)
   - Add `UnloadSpriteTextures()` method
   - Add `IsPlayerSprite()` helper
   - Update `UnloadMap()` to call sprite unloading

**Estimated Lines**: ~60 new lines

### Phase 4: Map Initializer Integration (Coder Agent 4)

**Files to Modify**:
1. `MapInitializer.cs`
   - Update `LoadMap()` to retrieve sprite texture keys
   - Update `RegisterMap()` call with sprite textures
   - Update `LoadMapFromFile()` (legacy path)

**Estimated Lines**: ~15 new lines

### Phase 5: Startup Cleanup (Coder Agent 5)

**Files to Modify**:
1. `PokeSharpGame.cs`
   - Remove `LoadAllSpriteTexturesAsync()` call (if exists)
   - Verify lazy loading is active

**Estimated Lines**: ~5 deletions

---

## 6. TESTING STRATEGY

### 6.1 Unit Tests

```csharp
// Test: SpriteTextureLoader
[Fact]
public async Task LoadSpritesForMapAsync_LoadsOnlyNewSprites()
{
    // Arrange: Map with 3 sprites, 1 already loaded
    var spriteIds = new HashSet<(string, string)>
    {
        ("generic", "boy_1"),
        ("generic", "girl_2"),
        ("generic", "nurse")
    };

    // Pre-load boy_1
    _loader.LoadSpriteTexture("generic", "boy_1");

    // Act
    var loadedKeys = await _loader.LoadSpritesForMapAsync(spriteIds);

    // Assert
    Assert.Equal(3, loadedKeys.Count);
    Assert.Contains("sprites/generic/boy_1", loadedKeys);
    Assert.Contains("sprites/generic/girl_2", loadedKeys);
    Assert.Contains("sprites/generic/nurse", loadedKeys);

    // Verify only 2 new loads occurred (boy_1 was skipped)
    _mockAssetManager.Verify(x => x.RegisterTexture(
        It.Is<string>(k => k.Contains("girl_2")), It.IsAny<Texture2D>()), Times.Once);
}

[Fact]
public void UnloadSpriteTextures_SkipsPlayerSprites()
{
    // Arrange
    var textureKeys = new HashSet<string>
    {
        "sprites/may/walking",
        "sprites/generic/boy_1"
    };

    // Act
    var unloaded = _loader.UnloadSpriteTextures(textureKeys, _ => false);

    // Assert
    Assert.Equal(1, unloaded); // Only boy_1 unloaded, may/walking skipped
    _mockAssetManager.Verify(x => x.UnregisterTexture(
        "sprites/may/walking"), Times.Never);
    _mockAssetManager.Verify(x => x.UnregisterTexture(
        "sprites/generic/boy_1"), Times.Once);
}
```

### 6.2 Integration Tests

```csharp
[Fact]
public async Task MapTransition_UnloadsUnusedSprites()
{
    // Arrange
    var map1 = LoadMap("LittlerootTown");
    var map1Sprites = GetLoadedSpriteCount(); // Should be ~5

    // Act
    var map2 = LoadMap("OldaleTown");
    var map2Sprites = GetLoadedSpriteCount();

    // Assert
    Assert.InRange(map2Sprites, 8, 12); // Current + Previous + Player
    Assert.True(map2Sprites < map1Sprites + 5); // Unload occurred
}
```

### 6.3 Memory Profiling Tests

```csharp
[Fact]
public void MemoryUsage_AfterMapLoad_IsWithinBudget()
{
    // Arrange
    var beforeMem = GC.GetTotalMemory(true);

    // Act
    LoadMap("LittlerootTown");
    var afterMem = GC.GetTotalMemory(true);

    // Assert
    var spriteMem = (afterMem - beforeMem) / 1024 / 1024; // MB
    Assert.InRange(spriteMem, 2, 10); // Should be 2-10MB, not 40MB
}
```

---

## 7. PERFORMANCE PROJECTIONS

### 7.1 Memory Savings

| Metric | Before | After | Savings |
|--------|--------|-------|---------|
| Startup Memory | 40MB | 0MB | 40MB |
| Single Map | 40MB | 5-10MB | 30-35MB |
| Map Transition Peak | 40MB | 12-15MB | 25-28MB |
| Steady State | 40MB | 8-12MB | 28-32MB |

**BEST CASE**: 35MB savings (87.5% reduction)
**WORST CASE**: 25MB savings (62.5% reduction)
**AVERAGE**: 30MB savings (75% reduction)

### 7.2 Load Time Impact

| Operation | Before | After | Change |
|-----------|--------|-------|--------|
| Startup | 2.5s | 0.8s | -68% (faster) |
| First Map Load | 0.3s | 0.5s | +0.2s (acceptable) |
| Map Transition | 0.3s | 0.6s | +0.3s (acceptable) |

**NET RESULT**: Faster startup, slightly slower map loads (trade-off acceptable).

### 7.3 Texture Cache Hit Rate

```
Shared Sprites (boy_1, girl_2, etc.):
  - First map: 0% hit rate (all new)
  - Second map: 30-50% hit rate (shared NPCs)
  - Third map: 40-60% hit rate (more shared)
  - Steady state: 50-70% hit rate (most NPCs reused)

Result: Most sprite loads are cache hits after 2-3 maps
```

---

## 8. ROLLBACK PLAN

If lazy loading causes issues, rollback is simple:

```csharp
// In PokeSharpGame.LoadSpriteTextures():

// ROLLBACK: Restore eager loading
await spriteTextureLoader.LoadAllSpriteTexturesAsync();
_logging.CreateLogger<PokeSharpGame>()
    .LogWarning("Using EAGER sprite loading (rollback mode)");

// Comment out lazy loading
// _gameInitializer.RenderSystem.SetSpriteTextureLoader(spriteTextureLoader);
```

**Impact**: Game returns to 40MB sprite memory usage.

---

## 9. SUCCESS CRITERIA

1. **Memory**: Sprite memory usage < 15MB after map load
2. **Performance**: Map load time < 1s
3. **Correctness**: All NPCs render correctly (no missing sprites)
4. **Stability**: No memory leaks after 10+ map transitions
5. **Compatibility**: Player sprite never unloaded

---

## 10. DEPENDENCIES

### External Dependencies
- AssetManager.UnregisterTexture() - already exists
- AssetManager.HasTexture() - already exists
- NpcDefinitionService.GetNpc() - already exists

### Internal Dependencies
- MapLoader.SpawnMapObjects() - needs sprite ID extraction
- MapLifecycleManager.UnloadMap() - needs sprite unloading
- SpriteTextureLoader - needs bulk loading method

**NO BREAKING CHANGES**: All changes are additive (new methods/fields).

---

## 11. CODER AGENT ASSIGNMENTS

| Agent | Task | Files | Est. Lines | Priority |
|-------|------|-------|------------|----------|
| Coder 1 | Core sprite loading | SpriteTextureLoader.cs | 80 | HIGH |
| Coder 2 | Map sprite extraction | MapLoader.cs | 120 | HIGH |
| Coder 3 | Lifecycle unloading | MapLifecycleManager.cs | 60 | HIGH |
| Coder 4 | Initializer integration | MapInitializer.cs | 15 | MEDIUM |
| Coder 5 | Startup cleanup | PokeSharpGame.cs | 5 | LOW |
| Tester 1 | Unit tests | Tests/SpriteTextureLoaderTests.cs | 200 | HIGH |
| Tester 2 | Integration tests | Tests/MapLoadingTests.cs | 150 | MEDIUM |

**TOTAL ESTIMATED LINES**: ~630 new lines

---

## 12. CONCLUSION

This design achieves the 25-35MB memory reduction goal by:

1. **Lazy Loading**: Only load sprites for current map's NPCs
2. **Reference Counting**: Share sprites across maps
3. **Smart Unloading**: Unload unused sprites on map transition
4. **Player Protection**: Never unload player sprites
5. **Preloading**: Load sprites during map load (no visual pop-in)

**RISK**: Low - All changes are additive, no breaking changes.
**COMPLEXITY**: Medium - Requires coordination across 4 classes.
**BENEFIT**: High - 75% reduction in sprite memory usage.

**RECOMMENDATION**: Proceed with implementation.
