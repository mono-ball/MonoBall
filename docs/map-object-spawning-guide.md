# Map Object Spawning Guide

**Date**: November 5, 2025  
**Version**: 1.0  
**Status**: Production Ready

## Overview

NPCs and other entities are now spawned directly from the Tiled map's object layers, rather than being hardcoded in the game. This allows level designers to place entities in the map editor and configure them with properties.

## How It Works

1. **Map Objects**: Create objects in Tiled's object layer
2. **Type Property**: Set the object's "Type" to a valid template ID (e.g., `npc/generic`)
3. **Automatic Spawning**: MapLoader reads objects and spawns entities using the template system
4. **Position Conversion**: Object pixel coordinates are converted to tile coordinates

## Adding NPCs to a Map

### In Tiled Editor

1. **Create Object Layer**:
   - Layer → New → Object Layer
   - Name it "Objects" or "NPCs"

2. **Add Object**:
   - Select "Insert Rectangle" tool
   - Click on the map where you want the NPC
   - Size: 16x16 pixels (1 tile)

3. **Set Object Type**:
   - Select the object
   - In Properties panel, set "Type" to template ID
   - Available types:
     - `npc/generic` - Standard movable NPC
     - `npc/stationary` - Non-moving NPC
     - `npc/trainer` - Trainer for battles
     - `npc/gym-leader` - Gym leader NPC
     - `npc/shop-keeper` - Shop/merchant NPC
     - `npc/fast` - Fast-moving NPC

4. **Optional: Set Direction**:
   - Add custom property "direction" (string)
   - Values: "up", "down", "left", "right"
   - Default: "down"

5. **Optional: Name the Object**:
   - Set the "Name" field for identification
   - Example: "Professor Oak", "Gym Leader Brock"

### Example Map JSON

```json
{
  "layers": [
    // ... tile layers ...
    {
      "id": 5,
      "name": "Objects",
      "type": "objectgroup",
      "objects": [
        {
          "id": 1,
          "name": "Rival",
          "type": "npc/trainer",
          "x": 192,
          "y": 160,
          "width": 16,
          "height": 16,
          "properties": [
            {
              "name": "direction",
              "type": "string",
              "value": "down"
            }
          ]
        }
      ]
    }
  ]
}
```

## Coordinate System

### Tiled Pixel Coordinates → Tile Coordinates

Tiled uses pixel coordinates with origin at top-left:
- X: 0 = left edge, increases rightward
- Y: 0 = top edge, increases downward

Conversion to tile coordinates:
```
tileX = floor(objectX / tileHeight)
tileY = floor((objectY + objectHeight) / tileHeight) - 1
```

### Example Conversions (16x16 tiles)

| Pixel (X, Y) | Tile (X, Y) | Notes |
|--------------|-------------|-------|
| (0, 16) | (0, 0) | Top-left tile |
| (16, 32) | (1, 1) | One tile right, one down |
| (240, 128) | (15, 7) | Middle of map |
| (304, 224) | (19, 13) | Near bottom-right |

**Pro Tip**: In Tiled, align objects to the grid (View → Snap to Grid) for precise placement.

## Available NPC Templates

### Base Templates

#### `npc/base`
**Do not use directly** - abstract base for all NPCs
- Components: Position, Sprite, Direction, Animation, Collision

### Movable NPCs

#### `npc/generic`
Standard movable NPC
- Inherits: `npc/base`
- Movement: 2.0 tiles/second
- Use for: Background characters, wandering NPCs

#### `npc/fast`
Fast-moving NPC
- Inherits: `npc/generic`
- Movement: 4.0 tiles/second (same as player)
- Use for: Rivals, fleeing characters

#### `npc/trainer`
Trainer NPC (for battles)
- Inherits: `npc/generic`
- Sprite: trainer-spritesheet
- Use for: Pokemon trainers
- Future: Will trigger battles

#### `npc/gym-leader`
Gym leader NPC
- Inherits: `npc/trainer` → `npc/generic` → `npc/base` (3-level!)
- Sprite: gym-leader-spritesheet
- Use for: Gym leaders, special boss NPCs
- Future: Will have badge component

### Stationary NPCs

#### `npc/stationary`
Non-moving NPC
- Inherits: `npc/base`
- No GridMovement component
- Use for: Sign readers, guards at posts

#### `npc/shop-keeper`
Shop/merchant NPC
- Inherits: `npc/stationary`
- Sprite: shop-keeper-spritesheet
- Use for: Poké Mart clerks, item sellers
- Future: Will have shop inventory

## Custom Properties

Objects can have custom properties that override template defaults:

### `direction` (string)
Sets the NPC's facing direction
- Values: "up", "down", "left", "right"
- Default: "down"
- Example: Trainer facing toward player spawn point

```json
{
  "name": "direction",
  "type": "string",
  "value": "left"
}
```

### Future Properties

Planned support for:
- `patrol_route` - Waypoints for patrolling NPCs
- `dialog_id` - Reference to dialog tree
- `trainer_team` - Pokemon team for trainers
- `shop_inventory` - Items for sale
- `script` - Lua script for custom behavior

## Workflow Example

### Scenario: Adding a Gym Leader

1. **Open map in Tiled**:
   ```bash
   tiled PokeSharp.Game/Assets/Maps/test-map.json
   ```

2. **Create/Select Object Layer**:
   - If no object layer exists, create one: Layer → New → Object Layer → "Objects"
   - Select the Objects layer

3. **Add Gym Leader Object**:
   - Click "Insert Rectangle" tool
   - Click at desired position (e.g., center of gym)
   - Resize to 16x16 if needed

4. **Configure Object**:
   - Name: "Gym Leader Brock"
   - Type: `npc/gym-leader`
   - Add property:
     - Name: `direction`
     - Type: `string`
     - Value: `down`

5. **Save and Test**:
   ```bash
   cd PokeSharp.Game/bin/Debug/net9.0
   ./PokeSharp.Game.exe
   ```

6. **Verify in Console**:
   ```
   ✅ Loaded map: test-map (20x15 tiles)
      Created 3 entities from map objects
      Spawned 'Gym Leader Brock' (npc/gym-leader) at (10, 10)
   ```

## Troubleshooting

### Object Not Spawning

**Problem**: "Object 'MyNPC' has no type/template, skipping"
```
Solution: Set the object's "Type" field to a valid template ID in Tiled
```

**Problem**: "Template 'npc/custom' not found for object 'MyNPC', skipping"
```
Solution: Either:
1. Use an existing template (see Available NPC Templates above)
2. Register a new template in TemplateRegistry.cs
```

### Wrong Position

**Problem**: NPC appears one tile off from where I placed it
```
Solution: Tiled uses pixel coordinates from the top-left of the object.
         Make sure the object is 16x16 and aligned to the grid.
         The NPC spawns at the bottom-center of the object.
```

**Problem**: NPC spawns outside the playable area
```
Solution: Check object layer visibility in Tiled. Ensure coordinates are:
         - X: 16-304 (tiles 1-18)
         - Y: 32-224 (tiles 1-13)
         Adjust in Tiled and save.
```

### Template Issues

**Problem**: NPC has wrong sprite/behavior
```
Solution: Check the template hierarchy:
         - npc/generic uses npc-spritesheet
         - npc/trainer uses trainer-spritesheet
         - npc/gym-leader uses gym-leader-spritesheet
         Make sure the template matches your intent.
```

## Advanced: Adding New Object Types

### Example: Adding Item Pickups

**Step 1**: Create template (TemplateRegistry.cs)
```csharp
var itemTemplate = new EntityTemplate {
    TemplateId = "item/pokeball",
    Name = "Poké Ball Pickup",
    Tag = "item",
};
itemTemplate.WithComponent(new Position(0, 0));
itemTemplate.WithComponent(new Sprite("items-spritesheet"));
itemTemplate.WithComponent(new Collision(false)); // Can walk through
// TODO: Add Item component when implemented
cache.Register(itemTemplate);
```

**Step 2**: Add to map (Tiled)
- Create object with Type: `item/pokeball`
- Position where you want the item
- Add properties as needed (item_id, quantity, etc.)

**Step 3**: Test
```bash
cd PokeSharp.Game/bin/Debug/net9.0
./PokeSharp.Game.exe
```

Console will show:
```
Spawned 'Potion' (item/pokeball) at (12, 8)
```

## Best Practices

### 1. Organize with Object Layers

Create separate layers for different entity types:
- "NPCs" - All NPC objects
- "Items" - Item pickups
- "Triggers" - Warp zones, cutscene triggers
- "Collision" - Custom collision shapes (future)

### 2. Name Your Objects

Give meaningful names for debugging:
```
✅ "Professor Oak"
✅ "Guard #1"
✅ "Potion (hidden)"

❌ "Object 1"
❌ "Rectangle"
```

### 3. Group Related NPCs

Use Tiled's grouping feature:
- Group trainer + their patrol route
- Group shop keeper + shop items
- Group gym leader + gym trainers

### 4. Document in Map Properties

Add custom properties to the map itself:
- `region` - Which region/zone
- `music` - Background music track
- `encounter_table` - Wild Pokemon table
- `description` - Notes for other developers

## Template Reference Table

| Template ID | Movement | Collision | Sprite | Use Case |
|-------------|----------|-----------|--------|----------|
| `npc/generic` | 2.0 t/s | Yes | npc-spritesheet | Standard NPCs |
| `npc/stationary` | None | Yes | npc-stationary-spritesheet | Guards, clerks |
| `npc/trainer` | 2.0 t/s | Yes | trainer-spritesheet | Battle trainers |
| `npc/gym-leader` | 2.0 t/s | Yes | gym-leader-spritesheet | Gym leaders |
| `npc/shop-keeper` | None | Yes | shop-keeper-spritesheet | Shop NPCs |
| `npc/fast` | 4.0 t/s | Yes | npc-spritesheet | Fast NPCs, rivals |

## Related Documentation

- [Template Inheritance Guide](template-inheritance-guide.md)
- [Template System Implementation](template-system-implementation.md)
- [Tile Template Implementation](tile-template-implementation-summary.md)

## Changelog

### Version 1.0 (November 5, 2025)
- Initial implementation of map object spawning
- Support for NPC templates from object layers
- Automatic position conversion (pixels → tiles)
- Custom property support (direction)
- Comprehensive error handling and logging
- Documentation completed



