# NPC Declaration Flow Analysis

## Overview

This document analyzes the NPC declaration flow between:

1. **NPC Data Files** (`Assets/Data/NPCs/*.json`) - Reusable NPC definitions
2. **NPC Templates** (`Assets/Templates/NPCs/*.json`) - Entity structure templates
3. **Map Objects** (`Assets/Data/Maps/*.json`) - NPC instances placed on maps

## Flow Diagram

```
Map Object (test-map.json)
  ├─ type="npc/generic" → Template lookup
  ├─ npcId="wanderer" → NPC Definition lookup
  └─ Properties (direction, waypoints, etc.) → Overrides

Template (npc/generic.json)
  ├─ baseTemplateId="npc/base" → Inheritance chain
  └─ Components (GridMovement, etc.)

NPC Definition (wanderer.json)
  ├─ npcId="wanderer"
  ├─ behaviorScript="wander" → Behavior TypeId lookup
  ├─ spriteId="generic/boy_1" → Sprite lookup
  └─ movementSpeed=1.5 → GridMovement component
```

## Issues Found

### 🔴 CRITICAL: Documentation Mismatch in README.md

**Location**: `Assets/Data/NPCs/README.md`

**Problem**: The README shows incorrect examples:

```json
{
  "behaviorScript": "Behaviors/wander_behavior.csx"  // ❌ WRONG - This is a script path
}
```

**Reality**: NPC data files correctly use behavior typeIds:

```json
{
  "behaviorScript": "wander"  // ✅ CORRECT - This is a behavior typeId
}
```

**Impact**:

- Developers following the README will create broken NPCs
- The `behaviorScript` field should contain a behavior `typeId` (e.g., "wander", "patrol", "stationary")
- Behavior definitions are loaded from `Assets/Data/Behaviors/*.json` with `typeId` fields
- The Behavior component expects a `BehaviorTypeId` that matches a registered behavior definition

**Fix Required**: Update README.md examples to show behavior typeIds, not script paths.

---

### 🟡 WARNING: Missing npcId in Map Objects

**Location**: `Assets/Data/Maps/test-map.json`

**Problem**: Two NPC objects don't have `npcId` properties:

1. **Object 2 - "Stationary NPC"** (line 975-990):
    - `type="npc/stationary"` ✅
    - No `npcId` property ❌
    - No `displayName` property ❌
    - Will fall back to `ApplyManualNpcProperties()` which only sets basic Npc component

2. **Object 3 - "Trainer"** (line 991-1006):
    - `type="npc/trainer"` ✅
    - No `npcId` property ❌
    - No `displayName` property ❌
    - Will fall back to `ApplyManualNpcProperties()` which only sets basic Npc component

**Impact**:

- These NPCs won't get behavior scripts from definitions
- They won't get sprites from definitions
- They won't get dialogue scripts
- They'll only have basic components from the template

**Fix Options**:

1. Add `npcId` properties pointing to existing NPC definitions
2. Create new NPC definitions for these instances
3. Add manual properties (displayName, behaviorScript, etc.) if definitions aren't needed

---

### 🟡 WARNING: Template Type vs npcType Confusion

**Problem**: Two different "type" concepts that are easily confused:

1. **Template Type** (`type="npc/generic"` in map objects):
    - Defines entity structure (components, inheritance)
    - Examples: `npc/base`, `npc/generic`, `npc/patrol`, `npc/stationary`
    - Used by `EntityFactoryService` to spawn entities

2. **NPC Type** (`npcType="guard"` in NPC data files):
    - Just metadata for categorization
    - Examples: `guard`, `wanderer`, `professor`, `generic`
    - Used for filtering/querying NPCs, not for entity creation

**Current State**: These are correctly separated, but the naming is confusing.

**Recommendation**: Consider renaming `npcType` to `npcCategory` or `npcClass` to reduce confusion.

---

### 🟢 VERIFIED: BehaviorScript → BehaviorTypeId Mapping

**Status**: ✅ **CORRECT**

**Flow**:

1. NPC definition has `behaviorScript: "wander"` (behavior typeId)
2. `MapObjectSpawner.ApplyNpcDefinition()` creates `new Behavior(npcDef.BehaviorScript)`
3. Behavior component gets `BehaviorTypeId = "wander"`
4. `NPCBehaviorSystem` looks up behavior definition by `typeId: "wander"`
5. Behavior definition found in `Assets/Data/Behaviors/wander.json`

**Verification**:

- ✅ `wanderer.json` → `behaviorScript: "wander"` → matches `wander.json` → `typeId: "wander"`
- ✅ `guard_001.json` → `behaviorScript: "patrol"` → matches `patrol.json` → `typeId: "patrol"`
- ✅ `prof_birch.json` → `behaviorScript: "stationary"` → matches `stationary.json` → `typeId: "stationary"`

---

### 🟢 VERIFIED: SpriteId Format

**Status**: ✅ **CORRECT**

**Format**: `"category/spriteName"` or `"spriteName"` (defaults to category="generic")

**Examples from NPC files**:

- ✅ `"generic/youngster"` → Category: "generic", SpriteName: "youngster"
- ✅ `"generic/boy_1"` → Category: "generic", SpriteName: "boy_1"
- ✅ `"generic/prof_birch"` → Category: "generic", SpriteName: "prof_birch"

**Implementation**: `SpriteId.TryCreate()` correctly parses both formats.

---

### 🟡 WARNING: Sprite Override Behavior (Template vs NPC Definition)

**Problem**: Templates and NPC definitions both define sprites, with different formats and override behavior.

**Template Sprite Format** (`Assets/Templates/NPCs/base.json`):

```json
{
  "type": "Sprite",
  "data": {
    "spriteId": "boy_1",      // Separate fields
    "category": "generic"
  }
}
```

**NPC Definition Sprite Format** (`Assets/Data/NPCs/*.json`):

```json
{
  "spriteId": "generic/boy_1"  // Combined format
}
```

**Override Behavior**:

1. **Template provides default sprite**: `npc/base` template includes `Sprite("boy_1", "generic")` as a default
2. **NPC definition overrides**: If NPC definition has `spriteId`, it calls `builder.OverrideComponent(new Sprite(...))`
   which replaces the template sprite
3. **Fallback behavior**: If NPC definition has no `spriteId`, the template's default sprite is used

**Current Implementation** (from `MapObjectSpawner.ApplyNpcDefinition()`):

```csharp
if (npcDef.SpriteId.HasValue)
{
    // Override template sprite with NPC definition sprite
    builder.OverrideComponent(new Sprite(spriteId.SpriteName, spriteId.Category));
}
// If no spriteId, template's default sprite remains
```

**Issues**:

1. **Format inconsistency**: Templates use separate `spriteId` + `category` fields, NPC definitions use combined
   `"category/spriteName"` format
2. **Unclear fallback**: If an NPC definition doesn't specify a sprite, it silently falls back to template default (
   `boy_1/generic`)
3. **No validation**: No check to ensure NPCs have appropriate sprites - could lead to all NPCs looking the same if
   definitions are missing sprites

**Recommendations**:

1. **Document the fallback behavior** in README.md - make it clear that template sprites are defaults
2. **Consider making spriteId required** in NPC definitions to avoid silent fallbacks
3. **Consider standardizing format** - either use combined format everywhere or separate fields everywhere
4. **Add validation** - warn when NPC definition has no spriteId and will use template default

**Status**: ⚠️ **WORKS BUT NEEDS DOCUMENTATION**

All current NPC definitions have sprites, so this isn't a breaking issue, but it's a potential source of confusion.

---

### 🟢 VERIFIED: Template Inheritance Chain

**Status**: ✅ **CORRECT**

**Template Hierarchy**:

```
npc/base (base template)
  ├─ npc/generic (adds GridMovement)
  │   ├─ npc/trainer
  │   │   └─ npc/gym-leader
  │   ├─ npc/patrol
  │   └─ npc/fast (overrides GridMovement speed)
  └─ npc/stationary
      └─ npc/shop-keeper
```

**Map Usage**:

- ✅ `type="npc/generic"` → Uses `npc/generic` template
- ✅ `type="npc/stationary"` → Uses `npc/stationary` template
- ✅ `type="npc/trainer"` → Uses `npc/trainer` template
- ✅ `type="npc/patrol"` → Uses `npc/patrol` template

---

## Summary of Required Fixes

### Priority 1 (Critical) ✅ COMPLETED

1. **Fix README.md** - Update behaviorScript examples to show typeIds, not script paths ✅ **FIXED**
2. **Document sprite override behavior** - Added explanation of template vs NPC definition sprite interaction ✅ **FIXED
   **

### Priority 2 (Important) ✅ PARTIALLY COMPLETED

2. **Add npcId to map objects** - Either:
    - Add `npcId` properties to Object 2 and Object 3 in test-map.json
    - ✅ **Object 2 fixed** - Added `npcId: "generic_villager"`
    - ⚠️ **Object 3 left as-is** - Trainer object without trainerId (intentional test case)

### Priority 3 (Nice to Have)

3. **Consider renaming** - `npcType` → `npcCategory` or `npcClass` to reduce confusion with template types
4. **Standardize sprite format** - Consider using same format (combined vs separate fields) in templates and NPC
   definitions
5. **Add validation** - Warn when NPC definition has no spriteId and will use template default

---

## Data Flow Validation

### ✅ Working Correctly

- Template loading and inheritance
- NPC definition loading from JSON
- SpriteId parsing
- Behavior typeId → Behavior definition lookup
- Map object → Template → NPC Definition flow (when npcId is present)

### ⚠️ Needs Attention

- Documentation accuracy (README.md) ✅ **FIXED**
- Missing npcId in some map objects ✅ **PARTIALLY FIXED** (Object 2 fixed, Object 3 left as test case)
- Potential naming confusion (npcType vs template type)
- **Sprite override behavior** - Template defaults vs NPC definition sprites (documented, but format inconsistency
  remains)

---

## Test Cases

### Test Case 1: NPC with Definition

**Map Object**: `type="npc/generic"`, `npcId="wanderer"`
**Expected**:

- Template components applied ✅
- NPC definition data applied (sprite, behavior, displayName) ✅
- Map overrides applied (direction, waypoints) ✅

### Test Case 2: NPC without Definition

**Map Object**: `type="npc/stationary"` (no npcId)
**Expected**:

- Template components applied ✅
- Manual properties applied (if present) ✅
- No behavior script ❌ (unless manually specified)
- No sprite from definition ❌ (unless manually specified)

### Test Case 3: Behavior Script Lookup

**NPC Definition**: `behaviorScript: "wander"`
**Expected**:

- Behavior component created with `BehaviorTypeId="wander"` ✅
- Behavior definition found in registry ✅
- Script compiled and registered ✅

