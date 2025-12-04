# NPC Definitions

This directory contains reusable NPC definitions.

## Structure

- Each NPC should have its own JSON file
- File name should match the `npcId` (e.g., `prof_birch.json` for `prof_birch`)

## Example: Generic NPC

```json
{
  "npcId": "generic_villager",
  "displayName": "VILLAGER",
  "npcType": "generic",
  "spriteId": "generic/girl_1",
  "behaviorScript": "stationary",
  "dialogueScript": "Dialogue/generic_greeting.csx",
  "movementSpeed": 1.5,
  "version": "1.0.0"
}
```

## Example: Important NPC

```json
{
  "npcId": "prof_birch",
  "displayName": "PROF. BIRCH",
  "npcType": "professor",
  "spriteId": "generic/prof_birch",
  "behaviorScript": "stationary",
  "dialogueScript": "Dialogue/prof_birch_intro.csx",
  "movementSpeed": 2.0,
  "version": "1.0.0",
  "customProperties": {
    "canGiveStarter": true,
    "hasPokedex": true
  }
}
```

## Example: Wandering NPC

```json
{
  "npcId": "wanderer",
  "displayName": "WANDERER",
  "npcType": "wanderer",
  "spriteId": "generic/boy_1",
  "behaviorScript": "wander",
  "dialogueScript": "Dialogue/generic_greeting.csx",
  "movementSpeed": 1.5,
  "version": "1.0.0"
}
```

## Example: Patrol NPC

```json
{
  "npcId": "guard_001",
  "displayName": "GUARD",
  "npcType": "guard",
  "spriteId": "generic/youngster",
  "behaviorScript": "patrol",
  "dialogueScript": "Dialogue/guard_greeting.csx",
  "movementSpeed": 2.0,
  "version": "1.0.0"
}
```

## Fields

- **npcId** (required): Unique identifier (e.g., "prof_birch", "wanderer", "guard_001")
- **displayName** (required): Name shown in-game
- **npcType**: Category for filtering/querying (generic, guard, professor, wanderer, etc.)
- **spriteId**: Sprite identifier in format "category/spriteName" or "spriteName" (defaults to category="generic")
    - **IMPORTANT**: This overrides the template's default sprite. If omitted, the NPC will use the template's default
      sprite (`boy_1/generic` from `npc/base` template)
    - **Recommended**: Always specify a spriteId to ensure NPCs have unique appearances
- **behaviorScript** (required): Behavior typeId that matches a behavior definition in `Assets/Data/Behaviors/` (e.g., "
  wander", "patrol", "stationary")
    - **IMPORTANT**: This must be a behavior typeId, NOT a script path
    - Behavior definitions are loaded from `Assets/Data/Behaviors/*.json` files
- **dialogueScript**: Path to dialogue script file (e.g., "Dialogue/generic_greeting.csx")
- **movementSpeed**: Tiles per second (0.0 = stationary)
- **version**: Version string for compatibility tracking (e.g., "1.0.0")
- **customProperties**: Extensible properties for modding (optional JSON object)

## Sprite Override Behavior

NPC templates (in `Assets/Templates/NPCs/`) include default sprites. When an NPC definition specifies a `spriteId`, it *
*overrides** the template's default sprite.

**Flow**:

1. Template provides default sprite: `npc/base` includes `Sprite("boy_1", "generic")`
2. NPC definition overrides: If `spriteId` is specified, it replaces the template sprite
3. Fallback: If `spriteId` is omitted, the template's default sprite is used

**Example**:

- Template: `npc/base` → Default sprite: `boy_1/generic`
- NPC Definition: `spriteId: "generic/girl_1"` → Overrides to `girl_1/generic`
- NPC Definition: No `spriteId` → Falls back to `boy_1/generic` (template default)

**Recommendation**: Always specify a `spriteId` in NPC definitions to ensure unique appearances and avoid silent
fallbacks to template defaults.
