# DRY Violations Analysis - Data Loading Layer

**Analysis Date**: 2025-11-26
**Scope**: PokeSharp Game Data Layer - Tiled Map Loading
**Status**: Research Only - No Modifications Made

---

## Executive Summary

Found **4 major DRY violations** across 5 files in the data loading layer:

1. **CalculateTilesPerRow duplication** (3 implementations)
2. **CalculateSourceRect/CalculateTileSourceRect duplication** (3 implementations)
3. **JsonSerializerOptions creation** (3 implementations)
4. **Integer property extraction from JsonElement** (2+ implementations)

**Good News**: Partial consolidation exists in `TilesetUtilities.cs`, but it's not being used consistently across all files.

---

## DUPLICATION #1: CalculateTilesPerRow Pattern

### Overview
The same tile-per-row calculation logic is implemented identically in **3 different locations**.

### Locations and Implementation

#### 1. LayerProcessor.cs (Lines 306-349)
```csharp
private static int CalculateTilesPerRow(TmxTileset tileset)
{
    if (tileset.TileWidth <= 0)
        throw new InvalidOperationException(
            $"Tileset '{tileset.Name ?? "unnamed"}' has invalid tile width {tileset.TileWidth}."
        );

    if (tileset.Image == null || tileset.Image.Width <= 0)
        throw new InvalidOperationException(
            $"Tileset '{tileset.Name ?? "unnamed"}' is missing a valid image width."
        );

    var spacing = tileset.Spacing;
    var margin = tileset.Margin;

    if (spacing < 0)
        throw new InvalidOperationException(
            $"Tileset '{tileset.Name ?? "unnamed"}' has negative spacing value {spacing}."
        );
    if (margin < 0)
        throw new InvalidOperationException(
            $"Tileset '{tileset.Name ?? "unnamed"}' has negative margin value {margin}."
        );

    var usableWidth = tileset.Image.Width - margin * 2;
    if (usableWidth <= 0)
        throw new InvalidOperationException(
            $"Tileset '{tileset.Name ?? "unnamed"}' has unusable image width after margins."
        );

    var step = tileset.TileWidth + spacing;
    if (step <= 0)
        throw new InvalidOperationException(
            $"Tileset '{tileset.Name ?? "unnamed"}' has invalid step size {step}."
        );

    var tilesPerRow = (usableWidth + spacing) / step;
    if (tilesPerRow <= 0)
        throw new InvalidOperationException(
            $"Tileset '{tileset.Name ?? "unnamed"}' produced non-positive tiles-per-row."
        );

    return tilesPerRow;
}
```

#### 2. AnimatedTileProcessor.cs (Lines 171-214)
**Exact duplicate** of LayerProcessor implementation.

#### 3. TilesetUtilities.cs (Lines 18-61) - CANONICAL VERSION
```csharp
public static int CalculateTilesPerRow(TmxTileset tileset)
{
    // ... identical validation and calculation ...
}
```

#### 4. BorderProcessor.cs (Lines 224-256) - PARTIAL/SIMPLIFIED
```csharp
private Rectangle CalculateSourceRect(int tileGid, TmxTileset tileset)
{
    // ... inline calculation without separate CalculateTilesPerRow call ...
    var spacing = Math.Max(0, tileset.Spacing);
    var margin = Math.Max(0, tileset.Margin);
    var usableWidth = tileset.Image.Width - margin * 2;
    var step = tileWidth + spacing;
    var tilesPerRow = step > 0 ? (usableWidth + spacing) / step : 1;
    // ... rest of calculation ...
}
```

### Current State
- **TilesetUtilities.cs** has the canonical, fully-validated version
- **LayerProcessor.cs** uses its own copy (private static)
- **AnimatedTileProcessor.cs** uses its own copy (private static)
- **BorderProcessor.cs** inlines a simplified version without extraction into separate method

### Impact
- **Maintenance burden**: 3 copies to update if validation rules change
- **Inconsistency**: BorderProcessor uses simplified version without full validation
- **Code bloat**: ~45 lines duplicated across processors

### Consolidation Status
❌ **NOT CONSOLIDATED** - TilesetUtilities version exists but is unused

---

## DUPLICATION #2: CalculateSourceRect Pattern

### Overview
Source rectangle calculation has **3 different implementations** with varying signatures and approaches.

### Locations

#### 1. LayerProcessor.cs (Lines 263-301)
```csharp
private Rectangle CalculateSourceRect(int tileGid, TmxTileset tileset)
{
    var localTileId = tileGid - tileset.FirstGid;
    var tileWidth = tileset.TileWidth;
    var tileHeight = tileset.TileHeight;

    if (tileWidth <= 0 || tileHeight <= 0)
        throw new InvalidOperationException(...);

    if (tileset.Image == null || tileset.Image.Width <= 0 || tileset.Image.Height <= 0)
        throw new InvalidOperationException(...);

    var tilesPerRow = CalculateTilesPerRow(tileset);
    var spacing = Math.Max(0, tileset.Spacing);
    var margin = Math.Max(0, tileset.Margin);

    var sourceX = margin + tileX * (tileWidth + spacing);
    var sourceY = margin + tileY * (tileHeight + spacing);

    return new Rectangle(sourceX, sourceY, tileWidth, tileHeight);
}
```

#### 2. AnimatedTileProcessor.cs (Lines 219-245)
```csharp
private static Rectangle CalculateTileSourceRect(
    int tileGid,
    int firstGid,
    int tileWidth,
    int tileHeight,
    int tilesPerRow,
    int spacing,
    int margin
)
{
    var localId = tileGid - firstGid;
    if (localId < 0)
        throw new InvalidOperationException(...);

    spacing = Math.Max(0, spacing);
    margin = Math.Max(0, margin);

    var tileX = localId % tilesPerRow;
    var tileY = localId / tilesPerRow;

    var sourceX = margin + tileX * (tileWidth + spacing);
    var sourceY = margin + tileY * (tileHeight + spacing);

    return new Rectangle(sourceX, sourceY, tileWidth, tileHeight);
}
```

**Key difference**: Takes pre-calculated values instead of extracting from tileset object.

#### 3. BorderProcessor.cs (Lines 224-256)
```csharp
private Rectangle CalculateSourceRect(int tileGid, TmxTileset tileset)
{
    var localTileId = tileGid - tileset.FirstGid;
    if (localTileId < 0)
        return Rectangle.Empty;

    var tileWidth = tileset.TileWidth;
    var tileHeight = tileset.TileHeight;

    if (tileWidth <= 0 || tileHeight <= 0 || tileset.Image == null)
        return Rectangle.Empty;

    // Inline tilesPerRow calculation
    var spacing = Math.Max(0, tileset.Spacing);
    var margin = Math.Max(0, tileset.Margin);
    var usableWidth = tileset.Image.Width - margin * 2;
    var step = tileWidth + spacing;
    var tilesPerRow = step > 0 ? (usableWidth + spacing) / step : 1;

    if (tilesPerRow <= 0)
        return Rectangle.Empty;

    var tileX = localTileId % tilesPerRow;
    var tileY = localTileId / tilesPerRow;

    var sourceX = margin + tileX * (tileWidth + spacing);
    var sourceY = margin + tileY * (tileHeight + spacing);

    return new Rectangle(sourceX, sourceY, tileWidth, tileHeight);
}
```

**Key difference**: Returns `Rectangle.Empty` on errors instead of throwing; inlines tilesPerRow.

#### 4. TilesetUtilities.cs (Lines 70-125) - CANONICAL VERSION
```csharp
public static Rectangle CalculateSourceRect(int tileGid, TmxTileset tileset)
{
    var localTileId = tileGid - tileset.FirstGid;
    var tileWidth = tileset.TileWidth;
    var tileHeight = tileset.TileHeight;

    if (tileWidth <= 0 || tileHeight <= 0)
        throw new InvalidOperationException(...);

    if (tileset.Image == null || tileset.Image.Width <= 0 || tileset.Image.Height <= 0)
        throw new InvalidOperationException(...);

    var spacing = tileset.Spacing;
    var margin = tileset.Margin;
    // ... validation ...

    var tilesPerRow = CalculateTilesPerRow(tileset);

    var tileX = localTileId % tilesPerRow;
    var tileY = localTileId / tilesPerRow;

    var sourceX = margin + tileX * (tileWidth + spacing);
    var sourceY = margin + tileY * (tileHeight + spacing);

    return new Rectangle(sourceX, sourceY, tileWidth, tileHeight);
}
```

### Comparison Table

| Aspect | LayerProcessor | AnimatedTileProcessor | BorderProcessor | TilesetUtilities |
|--------|---|---|---|---|
| **Parameter Type** | TmxTileset object | Extracted values | TmxTileset object | TmxTileset object |
| **Error Handling** | Throws exceptions | Throws exceptions | Returns Rectangle.Empty | Throws exceptions |
| **Calls CalculateTilesPerRow** | Yes | No (uses param) | No (inlines calc) | Yes |
| **Validation** | Full | Minimal | Minimal | Full |
| **Location** | Private instance method | Private static method | Private instance method | **Public static** |
| **Usage** | LayerProcessor only | AnimatedTileProcessor | BorderProcessor | Unused |

### Impact
- **3 different error-handling strategies**: Inconsistent behavior across processors
- **Signature inconsistency**: Different parameter sets make it hard to use one implementation
- **AnimatedTileProcessor optimization** creates a third variant for performance (pre-calculated values)
- **Code duplication**: Core rectangle calculation logic repeated 3 times

### Consolidation Status
❌ **PARTIAL** - TilesetUtilities has canonical version but:
  - BorderProcessor uses simplified version
  - AnimatedTileProcessor uses special optimized version with different signature
  - LayerProcessor ignores the utility

---

## DUPLICATION #3: JsonSerializerOptions Configuration

### Overview
The same JSON serializer configuration is created in **3 different locations** with identical options.

### Locations

#### 1. GameDataLoader.cs (Lines 25-31)
```csharp
public GameDataLoader(GameDataContext context, ILogger<GameDataLoader> logger)
{
    _context = context ?? throw new ArgumentNullException(nameof(context));
    _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    _jsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
    };
}
```

#### 2. TilesetLoader.cs (Lines 54-59)
```csharp
public void LoadExternalTilesets(TmxDocument tmxDoc, string mapBasePath)
{
    var jsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };
    // ... uses jsonOptions ...
}
```

#### 3. MapLoader.cs (Lines 146-151 and 214-219)
**TWO occurrences** in same file:
```csharp
var jsonOptions = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
};
```

Repeated in both `LoadMap()` (line 146) and `LoadMapAtOffset()` (line 214).

### Configuration Details

| Setting | GameDataLoader | TilesetLoader | MapLoader |
|---------|---|---|---|
| PropertyNameCaseInsensitive | ✓ | ✓ | ✓ |
| ReadCommentHandling.Skip | ✓ | ✓ | ✓ |
| AllowTrailingCommas | ✓ | ✓ | ✓ |
| **WriteIndented** | ✓ (true) | ✗ | ✗ |

### Impact
- **4 separate instantiations** of identical JsonSerializerOptions
- **Inconsistency**: GameDataLoader adds `WriteIndented = true`, others don't
- **Maintenance**: If JSON parsing rules change, must update 4 locations
- **Performance**: Unnecessary allocations of identical objects

### Consolidation Status
❌ **NOT CONSOLIDATED** - No shared configuration factory exists

---

## DUPLICATION #4: JsonElement Integer Property Extraction

### Overview
Code to extract integer values from JsonElement properties is duplicated with varying approaches.

### Locations

#### 1. BorderProcessor.cs (Lines 195-203, 205-219)
Two helper methods:
```csharp
private static int GetIntProperty(JsonElement element, string propertyName)
{
    if (element.TryGetProperty(propertyName, out var prop))
    {
        if (prop.ValueKind == JsonValueKind.Number)
            return prop.GetInt32();
    }
    return 0;
}

private static int GetIntFromDict(Dictionary<string, object> dict, string key)
{
    if (dict.TryGetValue(key, out var value))
    {
        if (value is int intValue)
            return intValue;
        if (value is long longValue)
            return (int)longValue;
        if (value is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Number)
            return jsonElement.GetInt32();
        if (int.TryParse(value?.ToString(), out var parsed))
            return parsed;
    }
    return 0;
}
```

#### 2. TilesetLoader.cs (Lines 78-95)
**Similar pattern repeated inline** without extraction:
```csharp
tileset.Name = root.TryGetProperty("name", out var name)
    ? name.GetString() ?? ""
    : "";
tileset.TileWidth = root.TryGetProperty("tilewidth", out var tw)
    ? tw.GetInt32()
    : 0;
tileset.TileHeight = root.TryGetProperty("tileheight", out var th)
    ? th.GetInt32()
    : 0;
tileset.TileCount = root.TryGetProperty("tilecount", out var tc)
    ? tc.GetInt32()
    : 0;
// ... repeats for margin, spacing, imagewidth, imageheight ...
```

#### 3. GameDataLoader.cs (Lines 441-505)
**Sophisticated version** in `ExtractConnectionData()` method:
```csharp
if (connectionValue is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Object)
{
    string? mapId = null;
    int offset = 0;

    if (jsonElement.TryGetProperty("map", out var mapProp))
        mapId = mapProp.GetString();

    if (jsonElement.TryGetProperty("offset", out var offsetProp))
    {
        if (offsetProp.ValueKind == JsonValueKind.Number)
            offset = offsetProp.GetInt32();
    }

    return (mapId, offset);
}
else if (connectionValue is Dictionary<string, object> dict)
{
    string? mapId = null;
    int offset = 0;

    if (dict.TryGetValue("map", out var mapValue))
        mapId = mapValue?.ToString();

    if (dict.TryGetValue("offset", out var offsetValue))
    {
        if (offsetValue is int intOffset)
            offset = intOffset;
        else if (offsetValue is JsonElement je && je.ValueKind == JsonValueKind.Number)
            offset = je.GetInt32();
        else if (int.TryParse(offsetValue?.ToString(), out var parsedOffset))
            offset = parsedOffset;
    }

    return (mapId, offset);
}
```

#### 4. TiledJsonParser.cs (Lines 157-172)
```csharp
object? value = prop.Value switch
{
    JsonElement jsonElement
        when jsonElement.ValueKind == JsonValueKind.String =>
        jsonElement.GetString(),
    JsonElement jsonElement
        when jsonElement.ValueKind == JsonValueKind.Number =>
        jsonElement.GetInt32(),
    JsonElement jsonElement
        when jsonElement.ValueKind == JsonValueKind.True => true,
    JsonElement jsonElement
        when jsonElement.ValueKind == JsonValueKind.False => false,
    JsonElement jsonElement
        when jsonElement.ValueKind == JsonValueKind.Null => null,
    _ => prop.Value?.ToString(),
};
```

#### 5. TilesetLoader.cs ParseTilesetAnimations (Lines 200-209)
```csharp
object value = propValue.ValueKind switch
{
    JsonValueKind.String => propValue.GetString() ?? "",
    JsonValueKind.Number => propValue.TryGetInt32(out var i)
        ? i
        : propValue.GetDouble(),
    JsonValueKind.True => true,
    JsonValueKind.False => false,
    _ => propValue.ToString(),
};
```

### Pattern Comparison

| Implementation | Style | Handles JsonElement | Handles Dictionary | Error Strategy |
|---|---|---|---|---|
| BorderProcessor | Separate methods | ✓ | ✓ | Return default |
| TilesetLoader inline | Ternary operators | ✓ | ✗ | Return default |
| GameDataLoader | If-else block | ✓ | ✓ | Return default |
| TiledJsonParser | Switch expression | ✓ | ✗ | Cast/convert |
| TilesetLoader Animations | Switch expression | ✓ | ✗ | Cast/convert |

### Impact
- **5 different approaches** to the same problem
- **Inconsistent type conversion**: Some try to parse, others accept only native types
- **Varying error handling**: No consistent strategy for invalid data
- **Code duplication**: JsonElement extraction logic appears in every loader

### Consolidation Status
❌ **NOT CONSOLIDATED** - No shared utilities for JSON property extraction

---

## DUPLICATION #5: Magic Numbers and Constants

### Overview
Tile flip flags are **duplicated across processors** instead of being centralized.

### Locations

#### 1. LayerProcessor.cs (Lines 22-26)
```csharp
private const uint FLIPPED_HORIZONTALLY_FLAG = 0x80000000;
private const uint FLIPPED_VERTICALLY_FLAG = 0x40000000;
private const uint FLIPPED_DIAGONALLY_FLAG = 0x20000000;
private const uint TILE_ID_MASK = 0x1FFFFFFF;
```

#### 2. MapLoader.cs (Lines 41-45)
**Exact duplicate** of LayerProcessor constants.

### Impact
- **2 copies** of identical bit-flag constants
- **Maintenance burden**: If Tiled changes GID format, must update both
- **Inconsistency risk**: Could accidentally diverge

### Consolidation Status
❌ **NOT CONSOLIDATED** - Could be moved to shared constants class

---

## Summary Table

| Violation | Files Affected | Count | Consolidated? | Utility Available? |
|---|---|---|---|---|
| CalculateTilesPerRow | 3 (LP, ATP, BP) | 3 copies | ❌ No | ✓ Yes (TilesetUtilities) |
| CalculateSourceRect | 3 (LP, ATP, BP) | 3 implementations | ❌ Partial | ✓ Yes (TilesetUtilities) |
| JsonSerializerOptions | 3 (GDL, TL, ML) | 4 creations | ❌ No | ✗ No |
| JsonElement property extraction | 5 files | 5 implementations | ❌ No | ✗ No |
| Tile flip flag constants | 2 (LP, ML) | 2 copies | ❌ No | ✗ No |

---

## File-by-File Analysis

### 1. LayerProcessor.cs
**Violations**: 3 (CalculateTilesPerRow, CalculateSourceRect, flip flags)
```
Private methods:
- CalculateTilesPerRow (306-349) ← DUPLICATE
- CalculateSourceRect (263-301) ← DUPLICATE
- Flip flag constants (22-26) ← DUPLICATE
```

### 2. AnimatedTileProcessor.cs
**Violations**: 2 (CalculateTilesPerRow, CalculateTileSourceRect)
```
Private methods:
- CalculateTilesPerRow (171-214) ← DUPLICATE
- CalculateTileSourceRect (219-245) ← DIFFERENT SIGNATURE
```

### 3. BorderProcessor.cs
**Violations**: 3 (inlined CalculateTilesPerRow, simplified CalculateSourceRect, JsonElement extraction)
```
Private methods:
- CalculateSourceRect (224-256) ← SIMPLIFIED, INLINED tilesPerRow
- GetIntProperty (195-203) ← UTILITY but only for JsonElement
- GetIntFromDict (205-219) ← UTILITY but only for Dictionary
```

### 4. GameDataLoader.cs
**Violations**: 2 (JsonSerializerOptions, JsonElement extraction)
```
Properties:
- _jsonOptions created in constructor (25-31) ← DUPLICATE
Methods:
- ExtractConnectionData (441-505) ← SOPHISTICATED extraction
```

### 5. TilesetLoader.cs
**Violations**: 2 (JsonSerializerOptions, JsonElement extraction pattern)
```
Methods:
- LoadExternalTilesets creates jsonOptions (54-59) ← DUPLICATE
- ParseTilesetAnimations inline JsonElement handling (200-209)
```

### 6. MapLoader.cs (BONUS - Not in original scope)
**Violations**: 3 (flip flags duplicate, JsonSerializerOptions x2, uses TilesetUtilities correctly)
```
Constants:
- Flip flags (41-45) ← DUPLICATE of LayerProcessor
Methods:
- LoadMap creates jsonOptions (146-151) ← DUPLICATE
- LoadMapAtOffset creates jsonOptions (214-219) ← DUPLICATE

Good News:
✓ Uses TilesetUtilities for tile calculations
✓ Delegates to specialized processors
```

---

## Recommendations (Priority Order)

### HIGH PRIORITY

**1. Enforce TilesetUtilities Usage**
- Update LayerProcessor to use `TilesetUtilities.CalculateTilesPerRow()`
- Update LayerProcessor to use `TilesetUtilities.CalculateSourceRect()`
- Update AnimatedTileProcessor to use `TilesetUtilities.CalculateSourceRect()`
- Remove private `CalculateTilesPerRow` from LayerProcessor and AnimatedTileProcessor

**2. Create JsonSerializerOptions Factory**
- Create static class `JsonSerializerFactory` with method `GetTiledJsonOptions()`
- Use in GameDataLoader, TilesetLoader, MapLoader, TiledJsonParser
- Reduces 4 separate instantiations to 1 shared instance

**3. Create JsonPropertyExtractor Utility**
- Consolidate JsonElement/Dictionary property extraction patterns
- Methods: `GetIntValue()`, `GetStringValue()`, `GetBoolValue()`, etc.
- Support both JsonElement and Dictionary<string, object>
- Consistent error handling strategy

### MEDIUM PRIORITY

**4. Consolidate Tile Flip Flag Constants**
- Move FLIPPED_*_FLAG constants to shared class (e.g., `TileConstants.cs`)
- Reference from both LayerProcessor and MapLoader
- Consider making them public for other processors

**5. Add Overload to BorderProcessor**
- `CalculateSourceRect` should throw on errors (consistency)
- Or: Create `TryCalculateSourceRect` that returns bool
- Remove Rectangle.Empty pattern inconsistency

### LOW PRIORITY

**6. Consider AnimatedTileProcessor Optimization**
- Keep `CalculateTileSourceRect(int, int, int, int, int, int, int)` signature
- But have it internally call `TilesetUtilities.CalculateSourceRect()` with caching
- Document why different signature exists (performance optimization)

---

## Code Quality Impact

| Metric | Current | After Consolidation |
|---|---|---|
| **Lines of Duplicate Code** | ~150 | ~50 |
| **Separate JsonSerializerOptions instances** | 4 | 1 |
| **Different CalculateTilesPerRow implementations** | 3 | 1 |
| **Different CalculateSourceRect implementations** | 3 | 1-2 |
| **Different property extraction approaches** | 5 | 1 |
| **Maintenance Points for tile constants** | 2 | 1 |

---

## Files to Monitor for Future DRY Violations

- `PokeSharp.Game.Data/MapLoading/Tiled/Core/TiledMapLoader.cs` - May have duplicated parsing logic
- `PokeSharp.Game.Data/MapLoading/Tiled/Utilities/` - Growing utility library, watch for redundancy
- Any new processor classes - Will likely repeat patterns from existing processors

---

## Appendix: Detailed Code Comparisons

### CalculateTilesPerRow Comparison

All three implementations follow identical logic:
1. Validate TileWidth > 0
2. Validate Image exists and has Width > 0
3. Get spacing and margin
4. Validate spacing and margin >= 0
5. Calculate usableWidth = imageWidth - (margin * 2)
6. Validate usableWidth > 0
7. Calculate step = tileWidth + spacing
8. Validate step > 0
9. Return (usableWidth + spacing) / step
10. Validate result > 0

**Recommendation**: Use TilesetUtilities.CalculateTilesPerRow() everywhere.

### CalculateSourceRect Comparison

Core calculation is identical in all versions:
```csharp
var tileX = localTileId % tilesPerRow;
var tileY = localTileId / tilesPerRow;
var sourceX = margin + tileX * (tileWidth + spacing);
var sourceY = margin + tileY * (tileHeight + spacing);
return new Rectangle(sourceX, sourceY, tileWidth, tileHeight);
```

Differences:
- **LayerProcessor**: Throws on errors, calls CalculateTilesPerRow separately
- **AnimatedTileProcessor**: Takes pre-calculated values as parameters (optimization)
- **BorderProcessor**: Returns Rectangle.Empty on errors, inlines tilesPerRow calculation
- **TilesetUtilities**: Throws on errors, calls CalculateTilesPerRow separately

**Recommendation**: Keep TilesetUtilities version as canonical. Consider separate overload for AnimatedTileProcessor optimization if needed.

---

**End of Analysis**
