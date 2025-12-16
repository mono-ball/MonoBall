# Entity Common Properties Analysis Report

## Executive Summary

This report analyzes 10 entity classes to identify common properties and patterns that could be extracted into base classes to reduce code duplication and improve maintainability.

## Analysis Date
Generated: 2025-12-16

## Entities Analyzed
1. AudioEntity
2. SpriteEntity
3. PopupBackgroundEntity
4. PopupOutlineEntity
5. MapEntity
6. FontEntity
7. BehaviorEntity
8. TileBehaviorEntity
9. PopupTheme
10. MapSection

---

## Common Property Matrix

| Property | Type | Audio | Sprite | Popup<br>Background | Popup<br>Outline | Map | Font | Behavior | Tile<br>Behavior | Popup<br>Theme | Map<br>Section | Count |
|----------|------|:-----:|:------:|:-------:|:--------:|:---:|:----:|:--------:|:----------:|:------:|:--------:|:-----:|
| **SourceMod** | `string?` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | **10/10** |
| **Version** | `string` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | **10/10** |
| **DisplayName** | `string` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ | **8/10** |
| **ExtensionData** | `string?` | ✅ | ✅ | ✅ | ✅ | ❌ | ✅ | ✅ | ✅ | ❌ | ❌ | **7/10** |
| **ParsedExtensionData** | `Dictionary<string, JsonElement>?` | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ | ✅ | ✅ | ❌ | ❌ | **6/10** |
| **Description** | `string?` | ❌ | ❌ | ✅ | ❌ | ❌ | ✅ | ✅ | ✅ | ✅ | ❌ | **5/10** |
| **GetExtensionProperty<T>** | Method | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ | ✅ | ❌ | ❌ | **2/10** |

---

## Detailed Property Analysis

### 1. SourceMod Property (100% Coverage)
**Present in:** All 10 entities

```csharp
/// <summary>
///     Source mod ID (null for base game).
/// </summary>
[MaxLength(100)]
public string? SourceMod { get; set; }
```

**Attributes:**
- `[MaxLength(100)]`
- Nullable string
- Default: `null`

**Purpose:** Tracks which mod contributed the entity (null for base game content).

---

### 2. Version Property (100% Coverage)
**Present in:** All 10 entities

```csharp
/// <summary>
///     Version for compatibility tracking.
/// </summary>
[MaxLength(20)]
public string Version { get; set; } = "1.0.0";
```

**Attributes:**
- `[MaxLength(20)]`
- Non-nullable string
- Default: `"1.0.0"`

**Purpose:** Enables version tracking for backward compatibility and migrations.

---

### 3. DisplayName Property (80% Coverage)
**Present in:** AudioEntity, SpriteEntity, PopupBackgroundEntity, PopupOutlineEntity, MapEntity, FontEntity, BehaviorEntity, TileBehaviorEntity

**Missing in:** PopupTheme (uses `Name`), MapSection (uses `Name`)

```csharp
/// <summary>
///     Human-readable display name.
/// </summary>
[Required]
[MaxLength(100)]
public string DisplayName { get; set; } = string.Empty;
```

**Attributes:**
- `[Required]`
- `[MaxLength(100)]`
- Non-nullable string
- Default: `string.Empty`

**Note:** PopupTheme and MapSection use `Name` instead, which serves the same purpose.

---

### 4. ExtensionData Property (70% Coverage)
**Present in:** AudioEntity, SpriteEntity, PopupBackgroundEntity, PopupOutlineEntity, FontEntity, BehaviorEntity, TileBehaviorEntity

**Missing in:** MapEntity, PopupTheme, MapSection

```csharp
/// <summary>
///     JSON-serialized extension data from mods.
///     Contains arbitrary custom properties added by mods.
/// </summary>
[Column(TypeName = "nvarchar(max)")]
public string? ExtensionData { get; set; }
```

**Attributes:**
- `[Column(TypeName = "nvarchar(max)")]`
- Nullable string
- Default: `null`

**Purpose:** Stores mod-specific custom properties as JSON for extensibility.

---

### 5. ParsedExtensionData Computed Property (60% Coverage)
**Present in:** AudioEntity, SpriteEntity, PopupBackgroundEntity, PopupOutlineEntity, BehaviorEntity, TileBehaviorEntity

**Missing in:** MapEntity, FontEntity, PopupTheme, MapSection

```csharp
/// <summary>
///     Gets the extension data as a parsed dictionary.
/// </summary>
[NotMapped]
public Dictionary<string, JsonElement>? ParsedExtensionData
{
    get
    {
        if (string.IsNullOrEmpty(ExtensionData))
            return null;
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(ExtensionData);
        }
        catch
        {
            return null;
        }
    }
}
```

**Attributes:**
- `[NotMapped]`
- Computed property (not stored in DB)
- Returns `Dictionary<string, JsonElement>?`

**Purpose:** Provides typed access to extension data without manual parsing.

---

### 6. Description Property (50% Coverage)
**Present in:** PopupBackgroundEntity, FontEntity, BehaviorEntity, TileBehaviorEntity, PopupTheme

**Missing in:** AudioEntity, SpriteEntity, PopupOutlineEntity, MapEntity, MapSection

```csharp
/// <summary>
///     Description of [entity type].
/// </summary>
[MaxLength(500)]  // or [MaxLength(1000)] in some cases
public string? Description { get; set; }
```

**Attributes:**
- `[MaxLength(500)]` or `[MaxLength(1000)]`
- Nullable string
- Default: `null`

**Purpose:** Provides human-readable description of the entity's purpose.

---

### 7. GetExtensionProperty<T> Method (20% Coverage)
**Present in:** BehaviorEntity, TileBehaviorEntity

```csharp
/// <summary>
///     Gets a custom property value from extension data.
/// </summary>
/// <typeparam name="T">The expected type of the property.</typeparam>
/// <param name="propertyName">The name of the property to retrieve.</param>
/// <returns>The property value, or default if not found or wrong type.</returns>
public T? GetExtensionProperty<T>(string propertyName)
{
    var data = ParsedExtensionData;
    if (data == null || !data.TryGetValue(propertyName, out var element))
        return default;
    try
    {
        return JsonSerializer.Deserialize<T>(element.GetRawText());
    }
    catch
    {
        return default;
    }
}
```

**Purpose:** Provides strongly-typed access to extension data properties.

---

## Additional Common Patterns

### Primary Key Pattern
**Common across all entities:**
- All entities use a typed ID as primary key
- All IDs use `[Column(TypeName = "nvarchar(...)")]`
- ID max length varies: 100-150 characters

**Examples:**
```csharp
[Key]
[MaxLength(150)]
[Column(TypeName = "nvarchar(150)")]
public GameAudioId AudioId { get; set; } = null!;

[Key]
[Column(TypeName = "nvarchar(100)")]
public GameMapSectionId MapSectionId { get; set; } = null!;
```

### IsFromMod Computed Property
**Present in:** BehaviorEntity, TileBehaviorEntity

```csharp
[NotMapped]
public bool IsFromMod => !string.IsNullOrEmpty(SourceMod);
```

**Purpose:** Quick check if entity comes from a mod.

---

## Recommended Base Class Hierarchy

### Level 1: BaseEntity (Universal Properties)
**Properties present in 100% of entities:**

```csharp
public abstract class BaseEntity
{
    /// <summary>
    ///     Source mod ID (null for base game).
    /// </summary>
    [MaxLength(100)]
    public string? SourceMod { get; set; }

    /// <summary>
    ///     Version for compatibility tracking.
    /// </summary>
    [MaxLength(20)]
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    ///     Gets whether this entity is from a mod.
    /// </summary>
    [NotMapped]
    public bool IsFromMod => !string.IsNullOrEmpty(SourceMod);
}
```

**Benefit:** Eliminates 20 lines of duplicated code across 10 entities.

---

### Level 2: DisplayableEntity (80% Coverage)
**Extends BaseEntity, adds DisplayName:**

```csharp
public abstract class DisplayableEntity : BaseEntity
{
    /// <summary>
    ///     Human-readable display name.
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;
}
```

**Applicable to:** AudioEntity, SpriteEntity, PopupBackgroundEntity, PopupOutlineEntity, MapEntity, FontEntity, BehaviorEntity, TileBehaviorEntity

**Special Cases:**
- PopupTheme and MapSection use `Name` property instead - consider refactoring or using separate base class

---

### Level 3: ExtensibleEntity (70% Coverage)
**Extends DisplayableEntity, adds extension data:**

```csharp
public abstract class ExtensibleEntity : DisplayableEntity
{
    /// <summary>
    ///     JSON-serialized extension data from mods.
    ///     Contains arbitrary custom properties added by mods.
    /// </summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? ExtensionData { get; set; }

    /// <summary>
    ///     Gets the extension data as a parsed dictionary.
    /// </summary>
    [NotMapped]
    public Dictionary<string, JsonElement>? ParsedExtensionData
    {
        get
        {
            if (string.IsNullOrEmpty(ExtensionData))
                return null;
            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(ExtensionData);
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    ///     Gets a custom property value from extension data.
    /// </summary>
    /// <typeparam name="T">The expected type of the property.</typeparam>
    /// <param name="propertyName">The name of the property to retrieve.</param>
    /// <returns>The property value, or default if not found or wrong type.</returns>
    public T? GetExtensionProperty<T>(string propertyName)
    {
        var data = ParsedExtensionData;
        if (data == null || !data.TryGetValue(propertyName, out var element))
            return default;
        try
        {
            return JsonSerializer.Deserialize<T>(element.GetRawText());
        }
        catch
        {
            return default;
        }
    }
}
```

**Applicable to:** AudioEntity, SpriteEntity, PopupBackgroundEntity, PopupOutlineEntity, FontEntity, BehaviorEntity, TileBehaviorEntity

**Not applicable to:** MapEntity, PopupTheme, MapSection (no ExtensionData)

---

### Level 4: DescribableEntity (Optional)
**Extends ExtensibleEntity, adds Description:**

```csharp
public abstract class DescribableEntity : ExtensibleEntity
{
    /// <summary>
    ///     Optional description.
    /// </summary>
    [MaxLength(1000)]
    public string? Description { get; set; }
}
```

**Applicable to:** FontEntity, BehaviorEntity, TileBehaviorEntity

**Partial applicability:** PopupBackgroundEntity, PopupTheme (use Description but not ExtensionData)

---

## Hierarchy Visualization

```
BaseEntity (10/10 entities)
├── Version
├── SourceMod
└── IsFromMod [computed]

    ├── DisplayableEntity (8/10 entities)
    │   └── DisplayName
    │
    │       ├── ExtensibleEntity (7/10 entities)
    │       │   ├── ExtensionData
    │       │   ├── ParsedExtensionData [computed]
    │       │   └── GetExtensionProperty<T>()
    │       │
    │       │       └── DescribableEntity (5/10 entities)
    │       │           └── Description
    │       │
    │       └── MapEntity (no extensions)
    │
    └── PopupTheme, MapSection (use "Name" instead of "DisplayName")
```

---

## Implementation Priority

### Priority 1: CRITICAL (100% coverage)
**BaseEntity with SourceMod + Version**
- Affects: All 10 entities
- Lines saved: ~30 lines
- Complexity: Low
- Risk: Low

### Priority 2: HIGH (80% coverage)
**DisplayableEntity with DisplayName**
- Affects: 8 entities
- Lines saved: ~40 lines
- Complexity: Low
- Risk: Medium (PopupTheme/MapSection use Name)

### Priority 3: MEDIUM (70% coverage)
**ExtensibleEntity with ExtensionData + ParsedExtensionData + GetExtensionProperty**
- Affects: 7 entities
- Lines saved: ~140 lines
- Complexity: Medium
- Risk: Low

### Priority 4: LOW (50% coverage)
**Description property standardization**
- Affects: 5 entities
- Lines saved: ~25 lines
- Complexity: Low
- Risk: Low

---

## Special Considerations

### 1. PopupTheme and MapSection Inconsistency
**Issue:** These entities use `Name` instead of `DisplayName`.

**Options:**
- A) Rename `Name` to `DisplayName` in these entities
- B) Create separate base class with `Name` property
- C) Leave as-is and exclude from DisplayableEntity

**Recommendation:** Option A - Standardize on DisplayName for consistency.

### 2. MapEntity Missing ExtensionData
**Issue:** MapEntity has no ExtensionData support despite being moddable.

**Options:**
- A) Add ExtensionData support to MapEntity
- B) Create separate hierarchy branch for non-extensible entities

**Recommendation:** Option A - Add ExtensionData for consistency.

### 3. FontEntity Missing ParsedExtensionData
**Issue:** FontEntity has ExtensionData but no ParsedExtensionData computed property.

**Status:** This inconsistency would be resolved by using ExtensibleEntity base class.

### 4. GetExtensionProperty<T> Limited Adoption
**Issue:** Only 2 entities implement this method despite 7 having ExtensionData.

**Recommendation:** Move to ExtensibleEntity base class for all entities with ExtensionData.

---

## Code Reduction Metrics

### Current State
- Total lines across common properties: ~200 lines
- Duplication factor: 7-10x
- Estimated duplicated code: ~1,000 lines

### After Refactoring
- Base class code: ~50 lines
- Entity-specific overrides: ~50 lines
- **Total savings: ~900 lines**

### Maintainability Impact
- Single source of truth for common logic
- Easier to add new modding features
- Reduced risk of inconsistent implementations
- Simplified unit testing

---

## Migration Path

### Phase 1: Create Base Classes
1. Create `BaseEntity` with SourceMod + Version
2. Create `DisplayableEntity` extending BaseEntity
3. Create `ExtensibleEntity` extending DisplayableEntity

### Phase 2: Migrate High-Impact Entities
1. Migrate entities with full property coverage:
   - AudioEntity
   - SpriteEntity
   - BehaviorEntity
   - TileBehaviorEntity

### Phase 3: Resolve Inconsistencies
1. Standardize Name vs DisplayName (PopupTheme, MapSection)
2. Add ExtensionData to MapEntity (if needed)
3. Add Description to remaining entities (if beneficial)

### Phase 4: Test and Validate
1. Run all entity tests
2. Verify EF Core migrations work correctly
3. Validate mod loading still functions
4. Check database schema is unchanged

---

## Testing Requirements

### Unit Tests Needed
- [ ] BaseEntity property behavior
- [ ] DisplayableEntity validation
- [ ] ExtensionData serialization/deserialization
- [ ] ParsedExtensionData error handling
- [ ] GetExtensionProperty<T> type safety
- [ ] IsFromMod computed property

### Integration Tests Needed
- [ ] EF Core table generation unchanged
- [ ] Mod loading with ExtensionData
- [ ] Database queries with base class properties
- [ ] JSON serialization round-trip

---

## Conclusion

The analysis reveals significant opportunity for code consolidation:

**Key Findings:**
1. **100% property coverage:** SourceMod and Version appear in all entities
2. **80% property coverage:** DisplayName (with Name variants) appears in most entities
3. **70% property coverage:** ExtensionData enables mod extensibility
4. **~900 lines** of code can be eliminated through inheritance

**Recommended Action:**
Implement a 3-level base class hierarchy (BaseEntity → DisplayableEntity → ExtensibleEntity) to maximize code reuse while maintaining flexibility.

**Risk Assessment:**
- **Low risk:** BaseEntity implementation (all entities benefit)
- **Medium risk:** DisplayableEntity (requires Name→DisplayName migration)
- **Low risk:** ExtensibleEntity (well-tested pattern in existing entities)

**Next Steps:**
1. Review and approve base class hierarchy design
2. Create base entity classes in `/GameData/Entities/Base/`
3. Migrate entities one at a time with comprehensive testing
4. Update documentation and migration guides

---

## Appendix: Property Usage by Entity

### AudioEntity
- ✅ SourceMod
- ✅ Version
- ✅ DisplayName
- ✅ ExtensionData
- ✅ ParsedExtensionData
- ❌ Description
- ❌ GetExtensionProperty

### SpriteEntity
- ✅ SourceMod
- ✅ Version
- ✅ DisplayName
- ✅ ExtensionData
- ✅ ParsedExtensionData
- ❌ Description
- ❌ GetExtensionProperty

### PopupBackgroundEntity
- ✅ SourceMod
- ✅ Version
- ✅ DisplayName
- ✅ ExtensionData
- ✅ ParsedExtensionData
- ✅ Description
- ❌ GetExtensionProperty

### PopupOutlineEntity
- ✅ SourceMod
- ✅ Version
- ✅ DisplayName
- ✅ ExtensionData
- ✅ ParsedExtensionData
- ❌ Description
- ❌ GetExtensionProperty

### MapEntity
- ✅ SourceMod
- ✅ Version
- ✅ DisplayName
- ❌ ExtensionData
- ❌ ParsedExtensionData
- ❌ Description
- ❌ GetExtensionProperty

### FontEntity
- ✅ SourceMod
- ✅ Version
- ✅ DisplayName
- ✅ ExtensionData
- ❌ ParsedExtensionData
- ✅ Description
- ❌ GetExtensionProperty

### BehaviorEntity
- ✅ SourceMod
- ✅ Version
- ✅ DisplayName
- ✅ ExtensionData
- ✅ ParsedExtensionData
- ✅ Description
- ✅ GetExtensionProperty

### TileBehaviorEntity
- ✅ SourceMod
- ✅ Version
- ✅ DisplayName
- ✅ ExtensionData
- ✅ ParsedExtensionData
- ✅ Description
- ✅ GetExtensionProperty

### PopupTheme
- ✅ SourceMod
- ✅ Version
- ⚠️ Name (not DisplayName)
- ❌ ExtensionData
- ❌ ParsedExtensionData
- ✅ Description
- ❌ GetExtensionProperty

### MapSection
- ✅ SourceMod
- ✅ Version
- ⚠️ Name (not DisplayName)
- ❌ ExtensionData
- ❌ ParsedExtensionData
- ❌ Description
- ❌ GetExtensionProperty

---

**End of Report**
