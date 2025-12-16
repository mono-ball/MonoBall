# JSON & Entity Standardization Plan

## Hive Mind Analysis Summary

Analysis performed by 5 specialized agents examining 1,296+ JSON files and 10 entity classes.

---

## Critical Inconsistencies Found

### 1. JSON Field Casing Split

| Category | Casing Used | Example Fields |
|----------|-------------|----------------|
| Behaviors, TileBehaviors, Audio, Fonts, Mods | **camelCase** | `id`, `displayName`, `description` |
| Popups, Sprites, Maps | **PascalCase** | `Id`, `DisplayName`, `Description` |

**Impact:** Confusing for mod authors, requires case-insensitive parsing as workaround.

### 2. Name vs DisplayName Inconsistency

| Entity | Property Used | Should Be |
|--------|---------------|-----------|
| AudioEntity | `DisplayName` (inherited) | `DisplayName` ✓ |
| BehaviorEntity | `DisplayName` (inherited) | `DisplayName` ✓ |
| FontEntity | `DisplayName` (inherited) | `DisplayName` ✓ |
| MapEntity | `DisplayName` (own) | `DisplayName` ✓ |
| MapSection | `Name` (own) | `DisplayName` |
| PopupBackgroundEntity | `DisplayName` (inherited) | `DisplayName` ✓ |
| PopupOutlineEntity | `DisplayName` (inherited) | `DisplayName` ✓ |
| PopupTheme | `Name` (own) | `DisplayName` |
| SpriteEntity | `DisplayName` (inherited) | `DisplayName` ✓ |
| TileBehaviorEntity | `DisplayName` (inherited) | `DisplayName` ✓ |

**Impact:** MapSection and PopupTheme use `Name` instead of inheriting `DisplayName`.

### 3. Description Not in Base Class

**Current state:** 5 of 10 entities have `Description`, each defined individually.
- BehaviorEntity ✓
- FontEntity ✓
- PopupBackgroundEntity ✓
- PopupTheme ✓
- TileBehaviorEntity ✓

**Missing from:**
- AudioEntity
- MapEntity
- MapSection
- PopupOutlineEntity
- SpriteEntity

**Impact:** Inconsistent mod support, duplicate code.

### 4. JSON ID Field Naming

| Category | JSON Field | Entity Property |
|----------|------------|-----------------|
| Behaviors | `id` | `BehaviorId` |
| TileBehaviors | `id` | `TileBehaviorId` |
| Audio | `id` | `AudioId` |
| Fonts | `id` | `FontId` |
| Sprites | `Id` | `SpriteId` |
| Popups | `Id` | `BackgroundId`/`OutlineId` |
| Maps | `Id` | `MapId` |
| MapSections | `id` | `MapSectionId` |
| Themes | `Id` | `ThemeId` |

**Impact:** Mixed casing in JSON files.

---

## Standardization Recommendations

### Phase 1: Base Class Enhancement

**Add to ExtensibleEntity:**
```csharp
/// <summary>
///     Optional description for documentation and UI display.
/// </summary>
[MaxLength(1000)]
public string? Description { get; set; }
```

**Result:** All ExtensibleEntity descendants automatically get Description.

### Phase 2: JSON Standardization (camelCase)

Standardize ALL JSON definition files to use **camelCase**:

```json
{
  "id": "base:sprite:npcs/elite_four/drake",
  "displayName": "Drake",
  "description": "Elite Four Dragon master",
  "type": "Sprite",
  "texturePath": "Graphics/Sprites/npcs/elite_four/drake.png",
  "frameWidth": 16,
  "frameHeight": 32,
  "version": "1.0.0"
}
```

**Files requiring update:**
- `Definitions/Sprites/**/*.json` (~200+ files)
- `Definitions/Maps/Popups/Backgrounds/*.json` (7 files)
- `Definitions/Maps/Popups/Outlines/*.json` (7 files)
- `Definitions/Maps/Popups/Themes/*.json` (3 files)
- `Definitions/Maps/Regions/**/*.json` (~100+ files)

### Phase 3: Entity Inheritance Cleanup

**Option A: Migrate to ExtensibleEntity (Recommended)**
```csharp
// MapSection changes from:
public class MapSection : BaseEntity
{
    public string Name { get; set; }  // Remove
}

// To:
public class MapSection : ExtensibleEntity
{
    // DisplayName inherited
}
```

**Option B: Keep BaseEntity, add DisplayName**

If entities need different behavior, add `DisplayName` to `BaseEntity`:
```csharp
public abstract class BaseEntity
{
    [MaxLength(100)]
    public string? SourceMod { get; set; }

    [MaxLength(20)]
    public string Version { get; set; } = "1.0.0";

    [Required]
    [MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;  // NEW

    [NotMapped]
    public bool IsFromMod => !string.IsNullOrEmpty(SourceMod);
}
```

Then ExtensibleEntity only adds `ExtensionData`:
```csharp
public abstract class ExtensibleEntity : BaseEntity
{
    [MaxLength(1000)]
    public string? Description { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? ExtensionData { get; set; }

    // ... helper methods
}
```

### Phase 4: DTO Alignment

Ensure all DTOs match the standardized JSON structure:

```csharp
internal sealed record BehaviorDefinitionDto(
    string Id,                              // maps from "id"
    string? DisplayName,                    // maps from "displayName"
    string? Description,                    // maps from "description"
    string? BehaviorScript,
    float DefaultSpeed = 4.0f,
    float PauseAtWaypoint = 1.0f,
    bool AllowInteractionWhileMoving = false,
    string? SourceMod = null,
    string Version = "1.0.0"
);
```

---

## Proposed Standard Schema

### Universal Definition Fields

Every JSON definition file SHOULD include:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `id` | string | Yes | Unique namespaced identifier |
| `displayName` | string | Yes | Human-readable name |
| `description` | string | No | Documentation text |
| `version` | string | No | Semantic version (default "1.0.0") |
| `sourceMod` | string | No | Auto-populated from file path |

### ID Format Standard

All IDs follow the pattern: `{namespace}:{type}:{category}/{name}`

Examples:
- `base:behavior:movement/patrol`
- `base:audio:music/battle/encounter_aqua`
- `base:sprite:npcs/elite_four/drake`
- `base:map:hoenn/littleroot_town`
- `mymod:item:consumables/mega_potion`

---

## Implementation Priority

### High Priority (Do First)
1. Add `Description` to `ExtensibleEntity`
2. Migrate MapSection and PopupTheme to use `DisplayName`
3. Remove duplicate `Description` from individual entities

### Medium Priority
4. Update JSON files to use consistent camelCase
5. Add JsonPropertyName attributes where needed for backwards compatibility

### Low Priority (Future)
6. Create JSON schema files for validation
7. Add migration tool for mod authors

---

## Migration Impact

### Breaking Changes
- JSON files with PascalCase will need updating OR
- Add `[JsonPropertyName]` attributes for backwards compatibility

### Non-Breaking Approach
Keep `PropertyNameCaseInsensitive = true` (current behavior) and gradually migrate JSON files to camelCase without breaking existing mods.

---

## Files to Modify

### Entity Classes
- `Base/ExtensibleEntity.cs` - Add Description
- `MapSection.cs` - Change to ExtensibleEntity, remove Name
- `PopupTheme.cs` - Change to ExtensibleEntity, remove Name
- `BehaviorEntity.cs` - Remove Description (now inherited)
- `FontEntity.cs` - Remove Description (now inherited)
- `PopupBackgroundEntity.cs` - Remove Description (now inherited)
- `TileBehaviorEntity.cs` - Remove Description (now inherited)

### JSON Files (~320 files)
- All Sprite definitions
- All Popup definitions
- All Map region definitions
- All Theme definitions

---

## Verification Checklist

- [ ] All entities inherit DisplayName from base class
- [ ] All entities inherit Description from ExtensibleEntity
- [ ] All JSON files use camelCase field names
- [ ] Build compiles with 0 errors
- [ ] Existing mods continue to load (case-insensitive parsing)
- [ ] New mod template uses standardized field names
