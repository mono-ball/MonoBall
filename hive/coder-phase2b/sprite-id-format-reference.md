# Sprite ID Format Reference

## Overview
This document describes the sprite ID format used in MapLoader's sprite collection system.

## Format Specification

### Standard Format
```
"category/spriteName"
```

### Examples from Codebase
```csharp
"players/brendan"     // Player character - Brendan
"players/may"         // Player character - May
"generic/boy_1"       // Generic NPC - Boy variant 1
"generic/girl_2"      // Generic NPC - Girl variant 2
"generic/sailor"      // Generic NPC - Sailor
"gym_leaders/roxanne" // Gym Leader - Roxanne
"may/walking"         // May's walking animation
```

### Fallback Behavior
If no slash is present, the sprite ID is assumed to be in the "generic" category:
```csharp
"boy_1" → ParseSpriteId() → ("generic", "boy_1")
```

## Category Breakdown

### Players (Always Loaded)
- `players/brendan`
- `players/may`

### Generic NPCs (Most Common)
- `generic/boy_1` through `generic/boy_N`
- `generic/girl_1` through `generic/girl_N`
- `generic/youngster`
- `generic/lass`
- `generic/sailor`
- `generic/mom`
- `generic/nurse`
- `generic/mart_employee`
- `generic/professor_oak`
- etc. (80+ sprites)

### Gym Leaders (Location-Specific)
- `gym_leaders/brock`
- `gym_leaders/misty`
- `gym_leaders/roxanne`
- `gym_leaders/wallace`
- etc. (9 total)

### Elite Four (Location-Specific)
- `elite_four/sidney`
- `elite_four/phoebe`
- `elite_four/glacia`
- `elite_four/drake`

### Frontier Brains (Location-Specific)
- `frontier_brains/tucker`
- `frontier_brains/lucy`
- `frontier_brains/spencer`
- etc. (7 total)

### Team Aqua (Story-Specific)
- `team_aqua/grunt_m`
- `team_aqua/grunt_f`
- `team_aqua/archie`

### Team Magma (Story-Specific)
- `team_magma/grunt_m`
- `team_magma/grunt_f`
- `team_magma/maxie`

## Parsing Logic

### ParseSpriteId Method (Line 2185-2194)
```csharp
private static (string category, string spriteName) ParseSpriteId(string spriteId)
{
    var parts = spriteId.Split('/', 2);
    if (parts.Length == 2)
    {
        return (parts[0], parts[1]);
    }

    // No slash - assume generic category
    return ("generic", spriteId);
}
```

### Texture Key Mapping
The sprite ID is converted to a texture key for AssetManager lookup:
```csharp
// From SpriteTextureLoader.cs:173-176
private static string GetTextureKey(string category, string spriteName)
{
    return $"sprites/{category}/{spriteName}";
}
```

**Examples:**
- `"players/brendan"` → Texture key: `"sprites/players/brendan"`
- `"generic/boy_1"` → Texture key: `"sprites/generic/boy_1"`
- `"gym_leaders/roxanne"` → Texture key: `"sprites/gym_leaders/roxanne"`

## Collection Process

### 1. Map Load Start
```csharp
_requiredSpriteIds.Clear();
_requiredSpriteIds.Add("players/brendan");
_requiredSpriteIds.Add("players/may");
```

### 2. NPC Spawning
```csharp
// For each NPC object in map:
if (!string.IsNullOrEmpty(npcDef.SpriteId))
{
    _requiredSpriteIds.Add(npcDef.SpriteId); // e.g., "generic/boy_1"
}
```

### 3. Retrieval
```csharp
var spriteIds = mapLoader.GetRequiredSpriteIds();
// Returns IReadOnlySet<string> with all collected sprite IDs
```

## Example Collections by Map Type

### Littleroot Town (Starter Town)
```json
[
  "players/brendan",
  "players/may",
  "generic/mom",
  "generic/prof_birch",
  "generic/rival",
  "generic/boy_1",
  "generic/girl_2"
]
```
**Count**: 7 sprite IDs

### Route 1 (Early Route)
```json
[
  "players/brendan",
  "players/may",
  "generic/youngster",
  "generic/youngster", // Duplicate ignored by HashSet
  "generic/lass",
  "generic/bug_catcher",
  "generic/boy_1"
]
```
**Unique Count**: 6 sprite IDs

### Rustboro City Gym (Gym Location)
```json
[
  "players/brendan",
  "players/may",
  "gym_leaders/roxanne",
  "generic/trainer_1",
  "generic/trainer_2",
  "generic/trainer_3"
]
```
**Count**: 6 sprite IDs

### Victory Road (Late Game)
```json
[
  "players/brendan",
  "players/may",
  "generic/ace_trainer_m",
  "generic/ace_trainer_f",
  "generic/veteran",
  "elite_four/sidney",  // Preview/cameo
  "generic/cooltrainer_m"
]
```
**Count**: 7 sprite IDs

## Testing Recommendations

### Verify Sprite ID Collection
```csharp
// After loading a map:
var spriteIds = mapLoader.GetRequiredSpriteIds();

// Check player sprites are always present:
Assert.Contains("players/brendan", spriteIds);
Assert.Contains("players/may", spriteIds);

// Check NPC sprites are collected:
Assert.Contains("generic/boy_1", spriteIds); // If map has this NPC

// Check count is reasonable:
Assert.InRange(spriteIds.Count, 2, 50); // Typically 5-20 for most maps
```

### Verify Sprite ID Format
```csharp
foreach (var spriteId in spriteIds)
{
    // Should contain "/" or be parseable as generic
    var (category, spriteName) = ParseSpriteId(spriteId);

    Assert.False(string.IsNullOrEmpty(category));
    Assert.False(string.IsNullOrEmpty(spriteName));
}
```

## Common Issues

### Issue 1: Sprite ID is null or empty
**Cause**: NPC definition has no sprite ID set
**Solution**: Ensure all NPC definitions have a valid sprite ID

### Issue 2: Sprite ID doesn't match texture file
**Cause**: Sprite ID format mismatch with asset naming
**Solution**: Verify sprite ID matches asset path:
- Asset: `Assets/Sprites/NPCs/generic/boy_1/spritesheet.png`
- Sprite ID: `"generic/boy_1"` ✅

### Issue 3: Duplicate sprite IDs
**Cause**: Multiple NPCs use same sprite
**Result**: This is NORMAL and EXPECTED - HashSet automatically handles duplicates

## Integration with Phase 2C

Phase 2C (SpriteTextureLoader) will consume these sprite IDs:
```csharp
var spriteIds = mapLoader.GetRequiredSpriteIds();

foreach (var spriteId in spriteIds)
{
    var (category, spriteName) = ParseSpriteId(spriteId);
    var textureKey = GetTextureKey(category, spriteName);

    if (!assetManager.HasTexture(textureKey))
    {
        LoadSpriteTexture(category, spriteName); // Lazy load
    }
}
```

## Summary

- **Format**: `"category/spriteName"`
- **Fallback**: `"spriteName"` → `"generic/spriteName"`
- **Collection**: During NPC spawning in `ApplyNpcDefinition()`
- **Storage**: `HashSet<string>` (automatic deduplication)
- **Retrieval**: `IReadOnlySet<string> GetRequiredSpriteIds()`
- **Always Included**: `"players/brendan"`, `"players/may"`
