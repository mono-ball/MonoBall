# Type Definition and Entity Analysis

## Executive Summary

This analysis examines the relationship between type definition interfaces (`ITypeDefinition`, `IScriptedType`) and entity base classes (`BaseEntity`, `ExtensibleEntity`) to identify opportunities for alignment and consistency.

**Key Finding**: There is significant overlap in naming conventions and extensibility patterns between the type definition system and entity system, presenting opportunities for consolidation and improved consistency.

---

## 1. ITypeDefinition Interface

**Location**: `/MonoBallFramework.Game/Engine/Core/Types/ITypeDefinition.cs`

### Required Properties

```csharp
public interface ITypeDefinition
{
    string Id { get; }              // Unique identifier (e.g., "rain", "lava", "patrol")
    string DisplayName { get; }     // Human-readable name for players/editors
    string? Description { get; }    // Optional documentation/tooltip text
}
```

### Purpose
- Base interface for all moddable type definitions
- Pure data containers (no behavior methods)
- Used as key in `TypeRegistry` lookups
- Supports game data types: behaviors, weather, terrain, items, moves, abilities

### Design Characteristics
- Minimal interface (3 properties)
- Focused on identification and display
- Supports null `Description` for optional documentation
- String-based `Id` for flexible typing

---

## 2. IScriptedType Interface

**Location**: `/MonoBallFramework.Game/Engine/Core/Types/IScriptedType.cs`

### Extended Properties

```csharp
public interface IScriptedType : ITypeDefinition
{
    string? BehaviorScript { get; }  // Path to .csx script file (optional)
}
```

### Purpose
- Extends `ITypeDefinition` with scripting support
- Enables runtime compilation of Roslyn C# scripts
- Allows modders to create custom behaviors without recompiling engine

### Implementation Examples
- `BehaviorDefinition` - NPC behaviors
- `TileBehaviorDefinition` - Tile behaviors

---

## 3. Type Definition Implementations

### BehaviorDefinition (NPC Behaviors)

**Location**: `/MonoBallFramework.Game/Engine/Core/Types/BehaviorDefinition.cs`

```csharp
public record BehaviorDefinition : IScriptedType
{
    // ITypeDefinition properties
    [JsonPropertyName("id")]
    [JsonRequired]
    public required string Id { get; init; }

    [JsonPropertyName("displayName")]
    public required string DisplayName { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    // IScriptedType property
    [JsonPropertyName("behaviorScript")]
    public string? BehaviorScript { get; init; }

    // Behavior-specific properties
    [JsonPropertyName("defaultSpeed")]
    public float DefaultSpeed { get; init; } = 4.0f;

    [JsonPropertyName("pauseAtWaypoint")]
    public float PauseAtWaypoint { get; init; } = 1.0f;

    [JsonPropertyName("allowInteractionWhileMoving")]
    public bool AllowInteractionWhileMoving { get; init; } = false;

    // Extensibility
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}
```

**Naming Convention**:
- Uses `Id` (matches interface)
- Uses `DisplayName` (matches interface)
- Uses `Description` (matches interface)
- Includes `ExtensionData` for mod support

### TileBehaviorDefinition (Tile Behaviors)

**Location**: `/MonoBallFramework.Game/Engine/Core/Types/TileBehaviorDefinition.cs`

```csharp
public record TileBehaviorDefinition : IScriptedType
{
    // ITypeDefinition properties (identical to BehaviorDefinition)
    [JsonPropertyName("id")]
    [JsonRequired]
    public required string Id { get; init; }

    [JsonPropertyName("displayName")]
    public required string DisplayName { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    // IScriptedType property
    [JsonPropertyName("behaviorScript")]
    public string? BehaviorScript { get; init; }

    // Tile-specific properties
    [JsonPropertyName("flags")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TileBehaviorFlags Flags { get; init; } = TileBehaviorFlags.None;

    // Extensibility
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}
```

**Naming Convention**: Identical to `BehaviorDefinition` for core properties

---

## 4. Entity Base Classes

### BaseEntity

**Location**: `/MonoBallFramework.Game/GameData/Entities/Base/BaseEntity.cs`

```csharp
public abstract class BaseEntity
{
    [MaxLength(100)]
    public string? SourceMod { get; set; }     // Mod tracking

    [MaxLength(20)]
    public string Version { get; set; } = "1.0.0";  // Versioning

    [NotMapped]
    public bool IsFromMod => !string.IsNullOrEmpty(SourceMod);  // Computed
}
```

**Purpose**:
- Provides mod tracking infrastructure
- Enables version management
- Base for all game data entities

**Key Characteristics**:
- Focused on mod/version metadata
- No naming properties
- No extensibility support at this level

### ExtensibleEntity

**Location**: `/MonoBallFramework.Game/GameData/Entities/Base/ExtensibleEntity.cs`

```csharp
public abstract class ExtensibleEntity : BaseEntity
{
    [Required]
    [MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;  // Human-readable name

    [Column(TypeName = "nvarchar(max)")]
    public string? ExtensionData { get; set; }  // JSON-serialized custom data

    [NotMapped]
    public Dictionary<string, JsonElement>? ParsedExtensionData { get; }  // Parsed dictionary

    // Helper methods
    public T? GetExtensionProperty<T>(string propertyName) { ... }
    public void SetExtensionProperty<T>(string propertyName, T value) { ... }
}
```

**Purpose**:
- Adds display naming
- Provides mod extensibility via JSON
- Offers typed property access to extension data

**Key Characteristics**:
- Has `DisplayName` (matches `ITypeDefinition`)
- Has `ExtensionData` (similar to definition `ExtensionData`)
- **Missing**: No `Id` property
- **Missing**: No `Description` property
- Stores extension data as serialized JSON string (database-friendly)

---

## 5. Entity Implementations

### TileBehaviorEntity

**Location**: `/MonoBallFramework.Game/GameData/Entities/TileBehaviorEntity.cs`

```csharp
[Table("TileBehaviors")]
public class TileBehaviorEntity : ExtensibleEntity
{
    [Key]
    [Column(TypeName = "nvarchar(100)")]
    public GameTileBehaviorId TileBehaviorId { get; set; }  // Primary key

    // DisplayName inherited from ExtensibleEntity

    [MaxLength(1000)]
    public string? Description { get; set; }  // Matches ITypeDefinition

    public int Flags { get; set; } = 0;  // TileBehaviorFlags as int

    [MaxLength(500)]
    public string? BehaviorScript { get; set; }  // Matches IScriptedType

    [NotMapped]
    public TileBehaviorFlags BehaviorFlags { get; set; }  // Computed enum

    // SourceMod, Version, IsFromMod inherited from BaseEntity
    // ExtensionData inherited from ExtensibleEntity
}
```

**Naming Analysis**:
- Uses `TileBehaviorId` instead of `Id`
- Uses `DisplayName` (inherited from `ExtensibleEntity`)
- Uses `Description` (matches `ITypeDefinition`)
- Uses `BehaviorScript` (matches `IScriptedType`)
- **Inconsistency**: Property name is `TileBehaviorId`, not `Id`

### BehaviorEntity

**Location**: `/MonoBallFramework.Game/GameData/Entities/BehaviorEntity.cs`

```csharp
[Table("Behaviors")]
public class BehaviorEntity : ExtensibleEntity
{
    [Key]
    [Column(TypeName = "nvarchar(100)")]
    public GameBehaviorId BehaviorId { get; set; }  // Primary key

    // DisplayName inherited from ExtensibleEntity

    [MaxLength(1000)]
    public string? Description { get; set; }  // Matches ITypeDefinition

    public float DefaultSpeed { get; set; } = 4.0f;
    public float PauseAtWaypoint { get; set; } = 1.0f;
    public bool AllowInteractionWhileMoving { get; set; } = false;

    [MaxLength(500)]
    public string? BehaviorScript { get; set; }  // Matches IScriptedType

    // SourceMod, Version, IsFromMod inherited from BaseEntity
    // ExtensionData inherited from ExtensibleEntity
}
```

**Naming Analysis**:
- Uses `BehaviorId` instead of `Id`
- Uses `DisplayName` (inherited)
- Uses `Description` (matches `ITypeDefinition`)
- Uses `BehaviorScript` (matches `IScriptedType`)
- **Inconsistency**: Property name is `BehaviorId`, not `Id`

### MapEntity

**Location**: `/MonoBallFramework.Game/GameData/Entities/MapEntity.cs`

```csharp
[Table("Maps")]
public class MapEntity : BaseEntity  // Note: NOT ExtensibleEntity
{
    [Key]
    [MaxLength(100)]
    [Column(TypeName = "nvarchar(100)")]
    public GameMapId MapId { get; set; }  // Primary key

    [Required]
    [MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;  // Duplicates ExtensibleEntity

    // Many map-specific properties...

    public string? CustomPropertiesJson { get; set; }  // Similar to ExtensionData

    // SourceMod, Version, IsFromMod inherited from BaseEntity
}
```

**Naming Analysis**:
- Uses `MapId` instead of `Id`
- Uses `DisplayName` (duplicates `ExtensibleEntity`)
- Uses `CustomPropertiesJson` instead of `ExtensionData`
- **Inconsistency**: Doesn't extend `ExtensibleEntity` but reimplements similar concepts

### Other Entity Classes

All extend `ExtensibleEntity` with specific ID properties:

- **FontEntity**: No specific ID property shown
- **SpriteEntity**: No specific ID property shown
- **AudioEntity**: No specific ID property shown
- **PopupOutlineEntity**: No specific ID property shown
- **PopupBackgroundEntity**: No specific ID property shown

---

## 6. Naming Convention Comparison

| Concept | ITypeDefinition | Type Definitions | ExtensibleEntity | Entity Implementations |
|---------|----------------|------------------|------------------|----------------------|
| **Unique ID** | `Id` (string) | `Id` (string) | ❌ Missing | `[Type]Id` (GameId) |
| **Display Name** | `DisplayName` (string) | `DisplayName` (string) | `DisplayName` (string) | Inherited |
| **Description** | `Description` (string?) | `Description` (string?) | ❌ Missing | `Description` (string?) |
| **Script Path** | N/A | `BehaviorScript` (string?) | ❌ Missing | `BehaviorScript` (string?) |
| **Extensibility** | N/A | `ExtensionData` (Dictionary) | `ExtensionData` (JSON string) | Inherited |
| **Mod Tracking** | N/A | N/A | `SourceMod` (via BaseEntity) | Inherited |
| **Versioning** | N/A | N/A | `Version` (via BaseEntity) | Inherited |

### Key Observations

1. **`Id` vs `[Type]Id`**:
   - Definitions use simple `Id`
   - Entities use typed IDs like `TileBehaviorId`, `BehaviorId`, `MapId`
   - This is a **semantic difference**: Entities use strongly-typed ID objects

2. **`DisplayName` Consistency**:
   - Present in both systems with identical naming
   - Good alignment

3. **`Description` Gap**:
   - Present in `ITypeDefinition` and concrete definitions
   - **Missing from `ExtensibleEntity`**
   - Entities re-add `Description` individually

4. **`BehaviorScript` Gap**:
   - Present in `IScriptedType` and concrete definitions
   - **Missing from `ExtensibleEntity`**
   - Entities re-add `BehaviorScript` individually

5. **`ExtensionData` Implementation Difference**:
   - Definitions: `Dictionary<string, JsonElement>?` (in-memory)
   - Entities: `string?` (serialized JSON) + helper methods
   - Different but equivalent for their contexts

---

## 7. Opportunities for Alignment

### 7.1 Add ITypeDefinition-like Properties to ExtensibleEntity

**Current Gap**: `ExtensibleEntity` lacks `Description` property that's common across all type definitions and entity implementations.

**Proposed Change**:
```csharp
public abstract class ExtensibleEntity : BaseEntity
{
    [Required]
    [MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    // NEW: Add Description to base class
    [MaxLength(1000)]
    public string? Description { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? ExtensionData { get; set; }

    // ... rest of class
}
```

**Benefits**:
- Removes need to re-declare `Description` in `TileBehaviorEntity`, `BehaviorEntity`, etc.
- Creates consistency with `ITypeDefinition`
- All extensible entities automatically support documentation

**Impact**:
- ✅ Low risk: Adding optional nullable property
- ✅ Backward compatible: Existing entities inherit it automatically
- ⚠️ Requires database migration

---

### 7.2 Create IScriptedEntity Interface

**Current Gap**: Entities that need scripting support (`TileBehaviorEntity`, `BehaviorEntity`) each declare `BehaviorScript` independently.

**Proposed Change**:
```csharp
/// <summary>
/// Interface for entities that support custom Roslyn C# script behaviors.
/// Mirrors IScriptedType for the entity layer.
/// </summary>
public interface IScriptedEntity
{
    /// <summary>
    /// Path to Roslyn .csx script file (optional).
    /// Relative to the game's Scripts directory.
    /// </summary>
    string? BehaviorScript { get; set; }
}
```

**Updated Entities**:
```csharp
public class TileBehaviorEntity : ExtensibleEntity, IScriptedEntity
{
    // Remove BehaviorScript property declaration
    // It's now part of IScriptedEntity implementation
}

public class BehaviorEntity : ExtensibleEntity, IScriptedEntity
{
    // Remove BehaviorScript property declaration
    // It's now part of IScriptedEntity implementation
}
```

**Benefits**:
- Creates symmetry between definition layer (`IScriptedType`) and entity layer (`IScriptedEntity`)
- Type-safe identification of scriptable entities
- Enables generic script loading: `if (entity is IScriptedEntity scriptable) { ... }`

**Impact**:
- ✅ No breaking changes: Interface adds no new requirements
- ✅ Cleaner abstraction
- ✅ Better discoverability

---

### 7.3 Consider Optional Id Property on ExtensibleEntity

**Current Situation**:
- Definitions have `Id` property (string)
- Entities have typed ID properties (`GameTileBehaviorId`, `GameBehaviorId`, etc.)
- No common `Id` abstraction at entity level

**Option A: Don't Change (Recommended)**
- Keep typed IDs in entities for strong typing
- Keep string IDs in definitions for flexibility
- These serve different purposes and contexts

**Rationale**:
- Entity IDs are primary keys with type safety
- Definition IDs are lookup keys for registry
- The semantic difference justifies different approaches

**Option B: Add Virtual Id Property (Not Recommended)**
```csharp
public abstract class ExtensibleEntity : BaseEntity
{
    [NotMapped]
    public virtual string? Id => null;  // Override in derived classes

    // ... rest of class
}
```

**Why Not Recommended**:
- Adds complexity without clear benefit
- Entity classes already have strongly-typed ID properties
- Would create confusion between typed ID and string ID
- No practical use case identified

---

### 7.4 Standardize ExtensionData Handling

**Current Situation**:
- Definitions: `Dictionary<string, JsonElement>?` with `[JsonExtensionData]`
- Entities: `string?` with helper methods (`GetExtensionProperty`, `SetExtensionProperty`)

**Analysis**:
- Different implementations are **appropriate for context**:
  - Definitions are in-memory, JSON-deserialized objects
  - Entities are database-backed with serialized storage
- Both provide extensibility, just with different storage strategies

**Recommendation**: No change needed. The current approach is correct for each context.

---

### 7.5 Align MapEntity with ExtensibleEntity

**Current Issue**: `MapEntity` extends `BaseEntity` directly and reimplements `DisplayName` and custom properties.

**Proposed Change**:
```csharp
[Table("Maps")]
public class MapEntity : ExtensibleEntity  // Change from BaseEntity
{
    [Key]
    [MaxLength(100)]
    [Column(TypeName = "nvarchar(100)")]
    public GameMapId MapId { get; set; }

    // Remove DisplayName - now inherited from ExtensibleEntity

    // Remove CustomPropertiesJson - now use ExtensionData

    // ... rest of map-specific properties
}
```

**Benefits**:
- Consistent with other entity classes
- Gains automatic extension data support
- Removes code duplication
- Maps become moddable via standard extension mechanism

**Impact**:
- ⚠️ Requires migration: Rename `CustomPropertiesJson` → `ExtensionData`
- ✅ More consistent architecture
- ✅ Better mod support for maps

---

## 8. Summary of Recommendations

### High Priority (Recommended)

1. **Add `Description` to `ExtensibleEntity`**
   - Low risk, high consistency gain
   - Removes duplication across entities
   - Aligns with `ITypeDefinition`

2. **Create `IScriptedEntity` interface**
   - No breaking changes
   - Creates symmetry with `IScriptedType`
   - Better type safety and discoverability

3. **Migrate `MapEntity` to extend `ExtensibleEntity`**
   - Removes code duplication
   - Standardizes extension mechanism
   - Improves mod support

### Medium Priority (Optional)

4. **Document ID naming conventions**
   - Create architecture decision record (ADR)
   - Explain why entities use typed IDs vs definitions use strings
   - Provide guidance for new entity types

### Low Priority (No Action Needed)

5. **ExtensionData storage differences** - Keep as-is (context-appropriate)
6. **Id property abstraction** - Keep typed IDs in entities (strong typing benefit)

---

## 9. Implementation Roadmap

### Phase 1: Non-Breaking Changes
1. Create `IScriptedEntity` interface
2. Update `TileBehaviorEntity` and `BehaviorEntity` to implement interface
3. Document ID naming conventions (ADR)

### Phase 2: Database Schema Changes
1. Add `Description` column to entity tables via migration
2. Update `ExtensibleEntity` base class
3. Remove redundant `Description` declarations from derived classes

### Phase 3: MapEntity Refactoring
1. Create migration to rename `CustomPropertiesJson` → `ExtensionData`
2. Change `MapEntity` to extend `ExtensibleEntity`
3. Test map loading and custom properties
4. Update documentation

---

## 10. Conclusion

The type definition system (`ITypeDefinition`, `IScriptedType`) and entity system (`BaseEntity`, `ExtensibleEntity`) show strong conceptual alignment with minor inconsistencies:

**Strengths**:
- Consistent use of `DisplayName`
- Similar extensibility patterns
- Clear separation of concerns

**Opportunities**:
- Add `Description` to `ExtensibleEntity` for completeness
- Create `IScriptedEntity` for symmetry with `IScriptedType`
- Standardize `MapEntity` to use `ExtensibleEntity`

**Philosophy**:
- Keep typed IDs in entities (strong typing, database PKs)
- Keep string IDs in definitions (flexibility, registry lookups)
- Maintain context-appropriate implementations (in-memory vs database storage)

These changes would create a more unified architecture while preserving the strengths of each system's design.
