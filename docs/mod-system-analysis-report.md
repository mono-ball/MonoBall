# PokeSharp Mod System Analysis Report

**Analysis Date:** 2025-12-16
**Analyzer:** Tester Agent (Hive Mind)
**Scope:** Mod content organization, structure, and override mechanisms

---

## Executive Summary

PokeSharp implements a sophisticated mod system that allows content overrides, custom type definitions, and script-based extensions. The system uses a priority-based content resolution strategy where mods with higher priority override base game content. This report documents the current mod structure, identifies patterns, and recommends improvements for consistency.

---

## 1. Mod Discovery & Loading Architecture

### 1.1 Mod Loader Workflow

The mod system operates in two phases:

**Phase 1: Discovery** (`DiscoverModsAsync`)
- Scans `/Mods/` directory for subdirectories containing `mod.json`
- Parses manifests and validates structure
- Resolves dependencies and determines load order
- Registers content folders (makes them available to ContentProvider)
- Registers custom type categories

**Phase 2: Script Loading** (`LoadModScriptsAsync`)
- Loads patches (JSON Patch RFC 6902 format)
- Loads script files (.csx)
- Initializes script instances with API providers

### 1.2 Content Resolution Strategy

The `ContentProvider` class implements a **priority-based override system**:

1. Check mods by priority (highest to lowest)
2. If not found in mods, check base game
3. Cache the result (LRU cache)

This means:
- Higher priority mods override lower priority mods
- Mods override base game content
- Duplicate filenames trigger overrides based on priority

---

## 2. Base Game Structure

### 2.1 Base Game Manifest

**Location:** `/MonoBallFramework.Game/Assets/mod.json`

```json
{
  "id": "base:pokesharp-core",
  "name": "PokeSharp Core Content",
  "priority": 1000,
  "contentFolders": {
    "Root": "",
    "Definitions": "Definitions",
    "Graphics": "Graphics",
    "Audio": "Audio",
    "Scripts": "Scripts",
    "Fonts": "Fonts",
    "Tiled": "Tiled",
    "Tilesets": "Tilesets",
    "TileBehaviors": "Definitions/TileBehaviors",
    "Behaviors": "Definitions/Behaviors",
    "Sprites": "Definitions/Sprites",
    "MapDefinitions": "Definitions/Maps/Regions",
    "AudioDefinitions": "Definitions/Audio",
    "PopupBackgrounds": "Definitions/Maps/Popups/Backgrounds",
    "PopupOutlines": "Definitions/Maps/Popups/Outlines",
    "PopupThemes": "Definitions/Maps/Popups/Themes",
    "MapSections": "Definitions/Maps/Sections"
  }
}
```

### 2.2 Base Game Directory Structure

```
MonoBallFramework.Game/Assets/
├── mod.json                           # Base game manifest
├── Definitions/
│   ├── Audio/                         # Audio definitions
│   │   └── Music/
│   │       └── Battle/
│   │           └── mus_encounter_cool.json
│   ├── Behaviors/                     # Behavior definitions
│   ├── Fonts/                         # Font definitions
│   ├── Maps/                          # Map definitions
│   │   ├── Regions/
│   │   ├── Sections/
│   │   └── Popups/
│   │       ├── Backgrounds/
│   │       ├── Outlines/
│   │       └── Themes/
│   ├── Sprites/                       # Sprite definitions
│   ├── TextWindow/                    # UI definitions
│   ├── TileBehaviors/                 # Tile behavior definitions
│   │   ├── ice.json
│   │   ├── impassable.json
│   │   └── jump_north.json
│   └── Worlds/                        # World definitions
├── Graphics/                          # Graphics assets
├── Audio/                             # Audio files
├── Scripts/                           # Base game scripts
│   └── TileBehaviors/                 # Behavior scripts
├── Fonts/                             # Font files
├── Tiled/                             # Tiled map files
└── Tilesets/                          # Tileset files
```

### 2.3 Built-in Content Types

The ModLoader recognizes these content types:

**Broad Categories:**
- `Root`, `Definitions`, `Graphics`, `Audio`, `Scripts`, `Fonts`, `Tiled`, `Tilesets`

**Fine-Grained Definition Types:**
- `TileBehaviors`, `Behaviors`, `Sprites`
- `MapDefinitions`, `AudioDefinitions`
- `PopupBackgrounds`, `PopupOutlines`, `PopupThemes`
- `MapSections`

---

## 3. Mod Structure Analysis

### 3.1 Current Mods

Three mods are currently present in `/Mods/`:

1. **achievement-system** - Script-based mod for tracking achievements
2. **test-override** - Content override test mod
3. **trainer-classes** - Custom type definition mod

### 3.2 Mod 1: Achievement System

**Purpose:** Adds achievement tracking system
**Type:** Script-based mod
**Priority:** 50

**Manifest Analysis:**
```json
{
  "id": "pokesharp.achievements",
  "name": "Achievement System",
  "priority": 50,
  "scripts": ["scripts/achievement_tracker.csx"],
  "permissions": [
    "events:subscribe",
    "gamestate:read",
    "gamestate:write",
    "dialogue:show"
  ],
  "dependencies": []
}
```

**Directory Structure:**
```
achievement-system/
├── mod.json
└── scripts/
    └── achievement_tracker.csx
```

**Characteristics:**
- Pure script-based mod
- No content overrides
- Declares required permissions
- Uses event subscription system

### 3.3 Mod 2: Test Override

**Purpose:** Tests content override functionality
**Type:** Content override mod
**Priority:** 100 (higher than base game's 1000 - **ISSUE**)

**Manifest Analysis:**
```json
{
  "id": "test.override",
  "name": "Test Override Mod",
  "priority": 100,
  "contentFolders": {
    "TileBehaviors": "content/Definitions/TileBehaviors"
  }
}
```

**Directory Structure:**
```
test-override/
├── mod.json
└── content/
    └── Definitions/
        └── TileBehaviors/
            └── ice.json        # Overrides base:tile_behavior:movement/ice
```

**Override Example:**

**Base game** (`Definitions/TileBehaviors/ice.json`):
```json
{
  "id": "base:tile_behavior:movement/ice",
  "name": "Ice Tile",
  "description": "Forces sliding movement",
  "behaviorScript": "TileBehaviors/ice.csx",
  "flags": "ForcesMovement, DisablesRunning"
}
```

**Mod override** (`content/Definitions/TileBehaviors/ice.json`):
```json
{
  "id": "base:tile_behavior:movement/ice",
  "name": "Modded Ice Tile",
  "description": "Forces sliding movement (MODDED VERSION)",
  "behaviorScript": "TileBehaviors/ice.csx",
  "flags": "ForcesMovement, DisablesRunning",
  "modded": true,
  "testProperty": "This property validates the override system is working"
}
```

**Observations:**
- Same ID as base game definition
- Adds additional properties (`modded`, `testProperty`)
- Keeps same behavior script reference
- Uses same relative path as base game (`ice.json` in TileBehaviors folder)

### 3.4 Mod 3: Trainer Classes

**Purpose:** Defines custom trainer class types
**Type:** Custom type definition mod
**Priority:** 50

**Manifest Analysis:**
```json
{
  "id": "pokesharp.trainer-classes",
  "name": "Trainer Classes",
  "priority": 50,
  "customTypes": {
    "TrainerClasses": {
      "key": "TrainerClasses",
      "folder": "content/TrainerClasses",
      "pattern": "*.json",
      "schema": "content/TrainerClasses/schema.json"
    }
  },
  "contentFolders": {
    "TrainerClasses": "content/TrainerClasses"
  }
}
```

**Directory Structure:**
```
trainer-classes/
├── mod.json
└── content/
    └── TrainerClasses/
        ├── schema.json           # JSON Schema for validation
        ├── ace_trainer.json
        ├── bug_catcher.json
        ├── lass.json
        └── youngster.json
```

**Custom Type Schema Features:**
- Defines JSON Schema for validation
- Enforces required properties: `id`, `name`, `basePrizeMoney`
- Validates data types and constraints
- Schema file is excluded from loading (won't be treated as content)

**Example Definition** (`youngster.json`):
```json
{
  "id": "youngster",
  "name": "Youngster",
  "basePrizeMoney": 16,
  "battleIntroDialogue": ["Hey! Let's battle!"],
  "preferredTypes": ["normal", "bug"],
  "levelRange": { "min": 2, "max": 15 },
  "teamSize": { "min": 1, "max": 3 }
}
```

**Registered ID Format:** `pokesharp.trainer-classes:TrainerClasses:youngster`

---

## 4. Content Organization Patterns

### 4.1 Pattern: Direct Override

**Used by:** test-override mod

**Strategy:**
1. Match base game folder structure exactly
2. Use same relative paths as base game
3. Higher priority mod wins

**Example:**
- Base: `Assets/Definitions/TileBehaviors/ice.json`
- Mod: `test-override/content/Definitions/TileBehaviors/ice.json`
- Content type: `TileBehaviors`
- Relative path: `ice.json`

**Pros:**
- Simple and intuitive
- Clear 1:1 mapping to base game
- Easy to understand what's being overridden

**Cons:**
- Requires knowing exact base game structure
- Deep folder nesting for consistency

### 4.2 Pattern: Custom Type Declaration

**Used by:** trainer-classes mod

**Strategy:**
1. Declare custom content type in manifest
2. Define JSON Schema for validation
3. Create content in mod-specific folder structure
4. Other mods can provide content for this type

**Example:**
- Custom type: `TrainerClasses`
- Folder: `content/TrainerClasses/`
- Schema: `content/TrainerClasses/schema.json`

**Pros:**
- Extensible (other mods can add more trainer classes)
- Schema validation ensures data quality
- Clean separation from base game content
- Supports cross-mod content

**Cons:**
- Requires CustomTypesApiService
- More complex setup
- Need to handle schema validation

### 4.3 Pattern: Script Extension

**Used by:** achievement-system mod

**Strategy:**
1. Provide only scripts, no content
2. Use permissions system for API access
3. React to game events

**Example:**
- Scripts: `scripts/achievement_tracker.csx`
- Permissions: `events:subscribe`, `gamestate:read/write`

**Pros:**
- No content conflicts
- Pure behavior extension
- Clean permission model

**Cons:**
- Limited to runtime behavior
- Cannot add/override static content
- Requires API access

---

## 5. Issues & Inconsistencies

### 5.1 CRITICAL: Priority Confusion

**Issue:** `test-override` mod has priority 100, but base game has priority 1000

**Problem:**
- Higher priority loads FIRST
- Base game (priority 1000) should load before mods
- test-override (priority 100) loads AFTER base game
- **This means test-override DOES NOT override base game!**

**Evidence from ContentProvider.cs:**
```csharp
// Line 111-113
var modsOrderedByPriority = _modLoader.LoadedMods.Values
    .OrderByDescending(m => m.Priority)  // Higher priority FIRST
    .ToList();
```

**Resolution Required:**
Either:
1. Mods should have priority HIGHER than 1000 to override base game
2. Base game should have priority 0 and mods have higher values
3. Documentation should clarify priority semantics

**Recommended Fix:**
- Base game: priority 0
- Normal mods: priority 50-100
- Override mods: priority 100+

### 5.2 Folder Structure Inconsistency

**Issue:** Mods use different folder organization strategies

**Observations:**
- `test-override`: Mirrors full base game structure (`content/Definitions/TileBehaviors/`)
- `trainer-classes`: Flat structure (`content/TrainerClasses/`)
- `achievement-system`: Script-only (`scripts/`)

**Problem:**
- No clear standard for how mods should organize content
- test-override has unnecessary nesting
- Confusion about when to mirror base game structure

**Recommendation:**
Establish clear guidelines:

**For Content Overrides:**
- Use flat structure if contentFolders maps correctly
- Example: `TileBehaviors: "content/TileBehaviors"` instead of `"content/Definitions/TileBehaviors"`

**For Custom Types:**
- Use flat structure under content folder
- Example: `content/TrainerClasses/`

**For Scripts:**
- Use `scripts/` folder
- Optionally organize by feature: `scripts/achievements/`, `scripts/behaviors/`

### 5.3 Content Folder Key Naming

**Issue:** Inconsistent use of content type keys

**Base game declares both broad and specific types:**
```json
"Definitions": "Definitions",           // Broad
"TileBehaviors": "Definitions/TileBehaviors"  // Specific
```

**Question:** Should mods use `Definitions` or `TileBehaviors`?

**Analysis:**
- ModLoader validates against known content types
- Both are valid
- Using specific types is better for clarity

**Recommendation:**
- Prefer specific content types (`TileBehaviors`) over broad types (`Definitions`)
- Update documentation to show preferred keys

### 5.4 Custom Type Registration Timing

**Issue:** Custom types must be registered before ContentProvider can resolve them

**Current Flow:**
1. DiscoverModsAsync() - registers custom type categories
2. LoadCustomTypeDefinitions() - loads actual definitions
3. LoadModScriptsAsync() - loads scripts that may use custom types

**Potential Issue:**
- If mod A declares custom type and mod B uses it, loading order matters
- Priority system handles this but isn't documented

**Recommendation:**
- Document cross-mod dependency requirements
- Validate that custom type providers load before consumers

---

## 6. Security Analysis

### 6.1 Path Traversal Protection

**Implementation:** Excellent security measures in place

**ModLoader Security:**
```csharp
// Line 270-299: IsPathSafe validation
- Rejects null/empty paths
- Rejects absolute paths
- Rejects ".." sequences
- Validates resolved path stays within mod directory
```

**ContentProvider Security:**
```csharp
// Line 496-522: IsPathSafe validation
- Blocks path traversal attempts
- Blocks rooted paths
- Blocks null characters
- Throws SecurityException if configured
```

**Assessment:** Strong protection against malicious mods attempting path traversal attacks.

### 6.2 Permission System

**Achievement-system mod declares permissions:**
```json
"permissions": [
  "events:subscribe",
  "gamestate:read",
  "gamestate:write",
  "dialogue:show"
]
```

**Question:** Are these permissions enforced?

**Investigation needed:**
- Find permission enforcement code
- Verify permissions are checked before API access
- Document permission model

---

## 7. Recommendations

### 7.1 Priority System Clarification

**Action Required:**
1. Update base game manifest to use priority 0
2. Update documentation to clarify: "Higher priority = loads first = can be overridden by lower priority"
3. OR change semantics to "Higher priority = overrides lower priority" and reorder loading

**Suggested Priority Ranges:**
```
0-999:     Reserved for base game and core systems
1000-4999: Normal mods
5000-8999: Override/compatibility mods
9000+:     Development/debug mods
```

### 7.2 Standardized Folder Structure

**Proposed Standard:**

**Content Override Mods:**
```
my-override-mod/
├── mod.json
└── content/
    ├── TileBehaviors/      # Uses specific content type key
    │   └── ice.json
    └── Sprites/
        └── player.json
```

**Custom Type Mods:**
```
my-custom-type-mod/
├── mod.json
└── content/
    └── MyCustomType/
        ├── schema.json
        ├── definition1.json
        └── definition2.json
```

**Script Mods:**
```
my-script-mod/
├── mod.json
└── scripts/
    ├── feature1/
    │   ├── behavior.csx
    │   └── events.csx
    └── feature2/
        └── logic.csx
```

### 7.3 Documentation Improvements

**Create Mod Development Guide:**
1. Explain priority system with examples
2. Document content folder key options
3. Show file organization patterns
4. Explain custom type system
5. Document permission model
6. Provide sample mods for each pattern

**Create Mod Manifest Reference:**
1. Document all manifest properties
2. Explain dependency system
3. Show validation rules
4. Provide JSON Schema for mod.json

### 7.4 Validation Enhancements

**Suggested Improvements:**
1. Validate that override mods have correct priority
2. Warn if mod mirrors base game structure unnecessarily
3. Validate custom type schemas are well-formed
4. Check for common mistakes (wrong content type keys, etc.)

### 7.5 Content Type Registration

**Clarify Registration Process:**
1. Document which content types are built-in
2. Show how to register custom types
3. Explain cross-mod custom type usage
4. Provide examples of extending another mod's custom types

---

## 8. Best Practices for Mod Development

### 8.1 Content Overrides

**DO:**
- Use specific content type keys (`TileBehaviors` not `Definitions`)
- Keep same relative paths as base game
- Use priority > 1000 to override base game (assuming base game uses 1000)
- Document what you're overriding

**DON'T:**
- Mirror entire base game folder structure unless necessary
- Use priority values that conflict with base game
- Override content without understanding the impact

### 8.2 Custom Types

**DO:**
- Provide JSON Schema for validation
- Use semantic versioning
- Document custom type properties
- Allow other mods to extend your types

**DON'T:**
- Hardcode paths to custom type content
- Skip schema validation
- Use generic type names that might conflict

### 8.3 Script Mods

**DO:**
- Declare required permissions
- Handle initialization/cleanup properly
- Use event system for loose coupling
- Log important operations

**DON'T:**
- Access APIs without permission
- Assume specific load order
- Modify state during discovery phase

---

## 9. Testing Recommendations

### 9.1 Test Scenarios

**Priority Override Testing:**
1. Create mod with priority 5000
2. Override base game TileBehavior
3. Verify mod version loads instead of base game

**Custom Type Testing:**
1. Create mod declaring custom type
2. Create second mod adding content for that type
3. Verify both mods load correctly
4. Verify cross-mod content is accessible

**Dependency Testing:**
1. Create mod A declaring custom type
2. Create mod B depending on mod A
3. Verify load order respects dependencies
4. Test what happens if mod A is missing

**Security Testing:**
1. Create mod with path traversal attempts
2. Verify ModLoader rejects malicious paths
3. Test with absolute paths, "..", null bytes

### 9.2 Integration Test Suite

**Recommended Tests:**
```
tests/
├── ModSystemTests/
│   ├── PriorityResolutionTests.cs
│   ├── CustomTypeRegistrationTests.cs
│   ├── ContentOverrideTests.cs
│   ├── DependencyResolutionTests.cs
│   ├── SecurityValidationTests.cs
│   └── PermissionEnforcementTests.cs
└── TestMods/
    ├── test-priority-high/
    ├── test-priority-low/
    ├── test-custom-type-provider/
    ├── test-custom-type-consumer/
    └── test-malicious-paths/
```

---

## 10. Conclusion

### 10.1 Summary

The PokeSharp mod system is well-architected with:
- Strong security measures
- Flexible content override system
- Custom type extensibility
- Script-based behavior extension

### 10.2 Critical Issues

1. **Priority confusion** - Base game priority 1000 vs mod priority 100
2. **Lack of documentation** - No clear guidelines for mod developers
3. **Inconsistent folder structures** - Different organizational patterns

### 10.3 Next Steps

**Immediate:**
1. Fix priority values (base game to 0, mods to 1000+)
2. Test that overrides actually work
3. Document priority semantics

**Short-term:**
4. Create mod development guide
5. Standardize folder structure recommendations
6. Add validation warnings for common mistakes

**Long-term:**
7. Build comprehensive test suite
8. Add permission enforcement
9. Create example mods for each pattern
10. Build mod compatibility checking tools

---

## Appendix A: Mod Manifest Schema

**Current Properties:**
- `id` (required): Unique identifier
- `name` (required): Display name
- `author`: Mod author
- `version`: Semantic version
- `description`: Mod description
- `priority`: Load order priority
- `dependencies`: Dependency list with version constraints
- `loadBefore`: Mods to load before this one
- `loadAfter`: Mods to load after this one
- `scripts`: Script file paths
- `permissions`: Required permissions
- `patches`: JSON Patch file paths
- `contentFolders`: Content type to folder mapping
- `customTypes`: Custom type declarations

**Built-in Content Types:**
- Root, Definitions, Graphics, Audio, Scripts, Fonts, Tiled, Tilesets
- TileBehaviors, Behaviors, Sprites
- MapDefinitions, AudioDefinitions
- PopupBackgrounds, PopupOutlines, PopupThemes, MapSections

---

## Appendix B: File Paths Reference

**Base Game:**
- Root: `/MonoBallFramework.Game/Assets/`
- Mod manifest: `/MonoBallFramework.Game/Assets/mod.json`
- Definitions: `/MonoBallFramework.Game/Assets/Definitions/`

**Mods:**
- Mods root: `/Mods/`
- Achievement system: `/Mods/achievement-system/`
- Test override: `/Mods/test-override/`
- Trainer classes: `/Mods/trainer-classes/`

**Engine:**
- ModLoader: `/MonoBallFramework.Game/Engine/Core/Modding/ModLoader.cs`
- ContentProvider: `/MonoBallFramework.Game/Engine/Content/ContentProvider.cs`

---

**Report Generated:** 2025-12-16
**Analysis Complete**
