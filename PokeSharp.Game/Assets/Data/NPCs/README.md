# NPC Definitions

This directory contains reusable NPC definitions.

## Structure

- Each NPC should have its own JSON file
- File name should match the `npcId` (e.g., `prof_birch.json` for `npc/prof_birch`)

## Example: Generic NPC

```json
{
  "npcId": "npc/generic_villager",
  "displayName": "VILLAGER",
  "npcType": "generic",
  "spriteId": "npc-generic",
  "behaviorScript": "Behaviors/wander_behavior.csx",
  "dialogueScript": "Dialogue/generic_greeting.csx",
  "movementSpeed": 2.0
}
```

## Example: Important NPC

```json
{
  "npcId": "npc/prof_birch",
  "displayName": "PROF. BIRCH",
  "npcType": "important",
  "spriteId": "npc-prof-birch",
  "behaviorScript": "Behaviors/stationary_behavior.csx",
  "dialogueScript": "Dialogue/prof_birch_intro.csx",
  "movementSpeed": 0.0,
  "customProperties": {
    "canGiveStarter": true,
    "hasPokedex": true
  }
}
```

## Fields

- **npcId** (required): Unique identifier (e.g., "npc/prof_birch")
- **displayName** (required): Name shown in-game
- **npcType**: Category (generic, important, shopkeeper, etc.)
- **spriteId**: Sprite to use (references AssetManager)
- **behaviorScript**: Default behavior (e.g., wander, stationary)
- **dialogueScript**: Dialogue to show when interacted with
- **movementSpeed**: Tiles per second (0.0 = stationary)
- **customProperties**: Extensible properties for modding
