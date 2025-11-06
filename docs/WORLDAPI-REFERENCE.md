# WorldAPI Reference

**Complete API Reference for Scripts**

## Overview

The WorldAPI provides scripts with controlled access to game systems. It's organized into domain-specific interfaces for clarity and maintainability.

## API Organization

```
IWorldApi (composed interface)
├── IPlayerApi     - Player management
├── IMapApi        - Map queries
├── INpcApi        - NPC control
└── IGameStateApi  - Flags and variables
```

Scripts access the API through the global `WorldApi` variable.

---

## IPlayerApi - Player Management

### GetPlayerName()
```csharp
string GetPlayerName()
```
Returns the player's chosen name.

**Example:**
```csharp
string name = WorldApi.GetPlayerName();
Console.WriteLine($"Player: {name}");
```

---

### GetMoney() / GiveMoney() / TakeMoney()
```csharp
int GetMoney()
void GiveMoney(int amount)
bool TakeMoney(int amount)
bool HasMoney(int amount)
```

Manage player's money balance.

**Examples:**
```csharp
// Check money
if (WorldApi.HasMoney(1000))
{
    WorldApi.TakeMoney(1000);
    WorldApi.ShowMessage("Purchased item!");
}

// Give reward
WorldApi.GiveMoney(500);
```

---

### GetPlayerPosition()
```csharp
Point GetPlayerPosition()
```
Gets player's current grid position (in tiles, not pixels).

**Example:**
```csharp
var playerPos = WorldApi.GetPlayerPosition();
var distance = Math.Abs(playerPos.X - npcPos.X) + Math.Abs(playerPos.Y - npcPos.Y);
if (distance <= 5)
{
    // Player is within 5 tiles
}
```

---

### GetPlayerFacing() / SetPlayerFacing()
```csharp
Direction GetPlayerFacing()
void SetPlayerFacing(Direction direction)
```

Get or set player's facing direction.

**Example:**
```csharp
// Make player face NPC during dialogue
WorldApi.SetPlayerFacing(Direction.North);
```

---

### SetPlayerMovementLocked()
```csharp
void SetPlayerMovementLocked(bool locked)
bool IsPlayerMovementLocked()
```

Lock/unlock player movement (for cutscenes, dialogue).

**Example:**
```csharp
// Lock during cutscene
WorldApi.SetPlayerMovementLocked(true);
// ... cutscene logic ...
WorldApi.SetPlayerMovementLocked(false);
```

---

## IMapApi - Map Queries

### IsPositionWalkable()
```csharp
bool IsPositionWalkable(int mapId, int x, int y)
```

Check if a tile position is walkable (no solid collision).

**Example:**
```csharp
bool canWalk = WorldApi.IsPositionWalkable(0, 10, 5);
if (canWalk)
{
    // Safe to move NPC there
}
```

---

### GetEntitiesAt()
```csharp
Entity[] GetEntitiesAt(int mapId, int x, int y)
```

Get all entities at a specific tile position.

**Example:**
```csharp
var entities = WorldApi.GetEntitiesAt(0, 10, 5);
foreach (var entity in entities)
{
    if (World.Has<NpcComponent>(entity))
    {
        // Found an NPC at this position
    }
}
```

---

### GetCurrentMapId()
```csharp
int GetCurrentMapId()
```

Get the currently active map ID.

**Example:**
```csharp
int currentMap = WorldApi.GetCurrentMapId();
```

---

### TransitionToMap()
```csharp
void TransitionToMap(int mapId, int x, int y)
```

Transition player to a different map.

**Example:**
```csharp
// Warp player to map 1 at position (5, 5)
WorldApi.TransitionToMap(1, 5, 5);
```

---

### GetMapDimensions()
```csharp
(int width, int height)? GetMapDimensions(int mapId)
```

Get map dimensions in tiles.

**Example:**
```csharp
var dims = WorldApi.GetMapDimensions(0);
if (dims.HasValue)
{
    Console.WriteLine($"Map size: {dims.Value.width}x{dims.Value.height}");
}
```

---

## INpcApi - NPC Control

### MoveNpc()
```csharp
void MoveNpc(Entity npc, Direction direction)
```

Request NPC to move in a direction (subject to collision).

**Example:**
```csharp
WorldApi.MoveNpc(Entity.Value, Direction.North);
```

---

### FaceDirection()
```csharp
void FaceDirection(Entity npc, Direction direction)
```

Set NPC's facing direction without moving.

**Example:**
```csharp
WorldApi.FaceDirection(Entity.Value, Direction.South);
```

---

### FaceEntity()
```csharp
void FaceEntity(Entity npc, Entity target)
```

Make NPC face toward another entity.

**Example:**
```csharp
// Face the player
var playerEntity = GetPlayerEntity();
WorldApi.FaceEntity(Entity.Value, playerEntity);
```

---

### GetNpcPosition()
```csharp
Point GetNpcPosition(Entity npc)
```

Get NPC's current grid position.

**Example:**
```csharp
var pos = WorldApi.GetNpcPosition(Entity.Value);
Console.WriteLine($"NPC at: ({pos.X}, {pos.Y})");
```

---

### SetNpcPath()
```csharp
void SetNpcPath(Entity npc, Point[] waypoints, bool loop)
```

Set waypoint path for NPC.

**Example:**
```csharp
var waypoints = new[]
{
    new Point(10, 10),
    new Point(15, 10),
    new Point(15, 15),
    new Point(10, 15)
};
WorldApi.SetNpcPath(Entity.Value, waypoints, loop: true);
```

---

### IsNpcMoving()
```csharp
bool IsNpcMoving(Entity npc)
```

Check if NPC is currently moving.

**Example:**
```csharp
if (!WorldApi.IsNpcMoving(Entity.Value))
{
    // NPC has stopped, start next action
}
```

---

### StopNpc()
```csharp
void StopNpc(Entity npc)
```

Stop NPC's current movement immediately.

**Example:**
```csharp
WorldApi.StopNpc(Entity.Value);
```

---

## IGameStateApi - Flags and Variables

### Flags (Boolean State)

```csharp
bool GetFlag(string flagId)
void SetFlag(string flagId, bool value)
bool FlagExists(string flagId)
IEnumerable<string> GetActiveFlags()
```

Manage boolean game state flags.

**Examples:**
```csharp
// Check if event completed
if (WorldApi.GetFlag("defeated_rival"))
{
    // Show post-battle dialogue
}

// Set flag when quest complete
WorldApi.SetFlag("delivered_parcel", true);

// List all active flags
foreach (var flag in WorldApi.GetActiveFlags())
{
    Console.WriteLine($"Active flag: {flag}");
}
```

**Common Flag Naming Conventions:**
- `defeated_{trainer_name}` - Battle outcomes
- `obtained_{item_name}` - Item acquisition
- `visited_{location}` - Location discovery
- `quest_{name}_completed` - Quest tracking

---

### Variables (String State)

```csharp
string? GetVariable(string key)
void SetVariable(string key, string value)
bool VariableExists(string key)
void DeleteVariable(string key)
IEnumerable<string> GetVariableKeys()
```

Manage string-valued game state.

**Examples:**
```csharp
// Store player choice
WorldApi.SetVariable("starter_pokemon", "charmander");

// Retrieve later
var starter = WorldApi.GetVariable("starter_pokemon");
if (starter == "charmander")
{
    // Fire-type specific dialogue
}

// Conditional variable
if (WorldApi.VariableExists("rival_name"))
{
    var name = WorldApi.GetVariable("rival_name");
    WorldApi.ShowMessage($"{name} wants to battle!");
}

// Cleanup
WorldApi.DeleteVariable("temp_state");
```

**Common Variable Uses:**
- Player choices (starter, rival name)
- NPC relationships (friendship levels)
- Quest progress markers
- Dynamic content keys

---

## Complete Usage Example

```csharp
public class TrainerBehavior : TypeScriptBase
{
    private bool _hasSpottedPlayer = false;
    
    public override void OnTick(float deltaTime)
    {
        // Check if already battled
        if (WorldApi.GetFlag("defeated_trainer_bob"))
        {
            // Face player if nearby
            var playerPos = WorldApi.GetPlayerPosition();
            var npcPos = WorldApi.GetNpcPosition(Entity.Value);
            var distance = Math.Abs(playerPos.X - npcPos.X) + 
                          Math.Abs(playerPos.Y - npcPos.Y);
            
            if (distance <= 2)
            {
                var playerEntity = GetPlayerEntity();
                WorldApi.FaceEntity(Entity.Value, playerEntity);
                ShowMessage("Good battle!");
            }
            return;
        }
        
        // Spot player in view range
        if (!_hasSpottedPlayer)
        {
            var playerPos = WorldApi.GetPlayerPosition();
            var npcPos = WorldApi.GetNpcPosition(Entity.Value);
            var facing = World.Get<GridMovement>(Entity.Value).CurrentDirection;
            
            // Check if player is in line of sight
            if (IsPlayerInLineOfSight(playerPos, npcPos, facing, viewRange: 5))
            {
                _hasSpottedPlayer = true;
                WorldApi.SetPlayerMovementLocked(true);
                ShowMessage("I challenge you!");
                // Trigger battle...
            }
        }
    }
}
```

---

## Error Handling

All WorldAPI methods handle invalid inputs gracefully:

```csharp
// Returns null for invalid entity
Point pos = WorldApi.GetNpcPosition(Entity.Invalid); // Returns Point.Zero

// Returns empty array for out-of-bounds
Entity[] entities = WorldApi.GetEntitiesAt(999, -1, -1); // Returns []

// Returns null for missing variables
string? value = WorldApi.GetVariable("nonexistent"); // Returns null
```

Always check return values:
```csharp
var mapDims = WorldApi.GetMapDimensions(mapId);
if (mapDims.HasValue)
{
    // Safe to use mapDims.Value
}
```

---

## Performance Notes

- **Flag/Variable operations:** O(1) dictionary lookup
- **Entity queries:** Use spatial hash when possible
- **Map queries:** Cached, very fast
- **Player queries:** Direct component access

Avoid calling API methods repeatedly in tight loops:
```csharp
// BAD: Calls API 1000 times
for (int i = 0; i < 1000; i++)
{
    var pos = WorldApi.GetPlayerPosition();
    // ...
}

// GOOD: Cache the result
var playerPos = WorldApi.GetPlayerPosition();
for (int i = 0; i < 1000; i++)
{
    // Use cached playerPos
}
```

---

## See Also

- [SCRIPTING-GUIDE.md](SCRIPTING-GUIDE.md) - How to write behavior scripts
- [NPC-BEHAVIOR-SYSTEM.md](NPC-BEHAVIOR-SYSTEM.md) - System architecture
- [TYPE-SYSTEM.md](TYPE-SYSTEM.md) - Type registry guide


