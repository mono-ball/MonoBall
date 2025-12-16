# Entity Property Naming Convention Analysis

## Summary

This document analyzes the property naming conventions across all entity classes in the `MonoBallFramework.Game/GameData/Entities/` directory, identifying inconsistencies in primary key naming, display name properties, and description properties.

## Entity Inheritance Structure

```
BaseEntity (abstract)
├── SourceMod: string?
├── Version: string
└── IsFromMod: bool (computed)

ExtensibleEntity : BaseEntity (abstract)
├── DisplayName: string (Required)
├── ExtensionData: string? (JSON)
└── ParsedExtensionData: Dictionary<string, JsonElement>? (computed)
```

## Detailed Entity Property Analysis

### Table: Property Naming Conventions

| Entity | Base Class | Primary Key Property | Display Name Property | Description Property | JsonPropertyName Attributes |
|--------|-----------|---------------------|----------------------|---------------------|---------------------------|
| **AudioEntity** | ExtensibleEntity | `AudioId` (GameAudioId) | `DisplayName` (inherited) | None | None |
| **BehaviorEntity** | ExtensibleEntity | `BehaviorId` (GameBehaviorId) | `DisplayName` (inherited) | `Description` (string?) | None |
| **FontEntity** | ExtensibleEntity | `FontId` (GameFontId) | `DisplayName` (inherited) | `Description` (string?) | None |
| **MapEntity** | BaseEntity | `MapId` (GameMapId) | `DisplayName` (string) | None | None |
| **MapSection** | BaseEntity | `MapSectionId` (GameMapSectionId) | `Name` (string) | None | None |
| **PopupBackgroundEntity** | ExtensibleEntity | `BackgroundId` (GamePopupBackgroundId) | `DisplayName` (inherited) | `Description` (string?) | None |
| **PopupOutlineEntity** | ExtensibleEntity | `OutlineId` (GamePopupOutlineId) | `DisplayName` (inherited) | None | `[JsonPropertyName]` on owned types (OutlineTile, OutlineTileUsage) |
| **PopupTheme** | BaseEntity | `ThemeId` (GameThemeId) | `Name` (string) | `Description` (string?) | None |
| **SpriteEntity** | ExtensibleEntity | `SpriteId` (GameSpriteId) | `DisplayName` (inherited) | None | `[JsonPropertyName]` on owned types (SpriteFrame, SpriteAnimation) |
| **TileBehaviorEntity** | ExtensibleEntity | `TileBehaviorId` (GameTileBehaviorId) | `DisplayName` (inherited) | `Description` (string?) | None |

## Identified Inconsistencies

### 1. Display Name Property Inconsistency

**Problem**: Entities that inherit from `BaseEntity` directly define their own display name property with different naming:

- **BaseEntity descendants use `Name`**:
  - `MapEntity.DisplayName` (string)
  - `MapSection.Name` (string) ⚠️
  - `PopupTheme.Name` (string) ⚠️

- **ExtensibleEntity descendants inherit `DisplayName`**:
  - `AudioEntity` → uses inherited `DisplayName`
  - `BehaviorEntity` → uses inherited `DisplayName`
  - `FontEntity` → uses inherited `DisplayName`
  - `PopupBackgroundEntity` → uses inherited `DisplayName`
  - `PopupOutlineEntity` → uses inherited `DisplayName`
  - `SpriteEntity` → uses inherited `DisplayName`
  - `TileBehaviorEntity` → uses inherited `DisplayName`

**Inconsistency**: `MapEntity` inherits from `BaseEntity` but defines its own `DisplayName` property, while `MapSection` and `PopupTheme` (also from `BaseEntity`) use `Name`.

### 2. Description Property Inconsistency

**Entities WITH Description property**:
- `BehaviorEntity.Description` (string?)
- `FontEntity.Description` (string?)
- `PopupBackgroundEntity.Description` (string?)
- `PopupTheme.Description` (string?)
- `TileBehaviorEntity.Description` (string?)

**Entities WITHOUT Description property**:
- `AudioEntity` ❌
- `MapEntity` ❌
- `MapSection` ❌
- `PopupOutlineEntity` ❌
- `SpriteEntity` ❌

**Note**: All Description properties are nullable (`string?`) and have `[MaxLength(500)]` or `[MaxLength(1000)]` attributes.

### 3. Primary Key Naming Pattern

**Consistent Pattern**: All entities follow the pattern `{EntityName}Id` for their primary key:
- `AudioId`, `BehaviorId`, `FontId`, `MapId`, `MapSectionId`, `BackgroundId`, `OutlineId`, `ThemeId`, `SpriteId`, `TileBehaviorId` ✅

**Strong Typing**: All primary keys use custom type wrappers (e.g., `GameAudioId`, `GameBehaviorId`) with appropriate column type configuration.

### 4. JsonPropertyName Attribute Usage

**Entities using JsonPropertyName**:
- `PopupOutlineEntity` - on owned types only:
  - `OutlineTile` properties (index, x, y, width, height)
  - `OutlineTileUsage` properties (topEdge, leftEdge, rightEdge, bottomEdge)
- `SpriteEntity` - on owned types only:
  - `SpriteFrame` properties (index, x, y, width, height)
  - `SpriteAnimation` properties (name, loop, frameIndices, frameDurations, flipHorizontal)

**Purpose**: These attributes are used for JSON serialization of owned entities stored in JSON columns, using camelCase naming convention for JSON compatibility.

## Recommendations for Standardization

### Priority 1: Fix Display Name Inconsistency

**Option A** (Recommended): Add `DisplayName` to `BaseEntity`
```csharp
public abstract class BaseEntity
{
    [Required]
    [MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    // ... existing properties
}
```
Then remove `DisplayName` from `ExtensibleEntity` and rename:
- `MapSection.Name` → migrate to inherited `DisplayName`
- `PopupTheme.Name` → migrate to inherited `DisplayName`

**Option B**: Keep current structure but standardize on `Name` property
- Rename `ExtensibleEntity.DisplayName` → `Name`
- Rename `MapEntity.DisplayName` → `Name`
- This would require more extensive changes throughout the codebase.

### Priority 2: Standardize Description Property

**Recommendation**: Add optional `Description` property to all entities that don't have one:

```csharp
// Add to AudioEntity, MapEntity, MapSection, PopupOutlineEntity, SpriteEntity
[MaxLength(500)]
public string? Description { get; set; }
```

This improves mod documentation and metadata consistency.

### Priority 3: Verify Type Column Configuration

All primary key properties should have consistent column configuration:
```csharp
[Key]
[MaxLength(100-150)]  // Consistent length based on ID type
[Column(TypeName = "nvarchar(100-150)")]
public GameXxxId XxxId { get; set; } = null!;
```

**Current variations**:
- Most use `nvarchar(100)`
- `AudioEntity`, `FontEntity`, `SpriteEntity` use `nvarchar(150)`
- `PopupOutlineEntity`, `PopupBackgroundEntity` use `nvarchar(150)`

**Recommendation**: Standardize based on expected ID length patterns, likely `nvarchar(150)` for all to allow flexibility.

## Implementation Impact

### Low Risk Changes
- Adding `Description` properties (backward compatible, nullable)
- Standardizing column lengths (database migration required)

### Medium Risk Changes
- Moving `DisplayName` to `BaseEntity` (requires careful migration)
- Renaming `Name` → `DisplayName` (requires code updates)

### Breaking Changes
- Any change to primary key types or names
- Changes to required properties
- Removing properties without migration path

## Current Strengths

1. **Consistent Primary Key Naming**: All entities follow the `{Entity}Id` pattern ✅
2. **Strong Typing**: All IDs use custom type wrappers ✅
3. **Extensibility Support**: ExtensibleEntity provides mod support ✅
4. **JSON Column Support**: Proper use of owned types for complex data ✅
5. **Mod Tracking**: SourceMod and Version on all entities ✅

## Conclusion

The codebase has strong foundations with consistent primary key naming and proper type safety. The main inconsistencies are:

1. **Display name property naming** varies between `Name` and `DisplayName` depending on base class
2. **Description properties** are inconsistently present across entities
3. **Column length specifications** vary slightly without clear rationale

Addressing these inconsistencies would improve maintainability and make the entity model more predictable for developers.
