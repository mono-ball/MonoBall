# Debug Console - Scripting API Reference

Quick reference for available APIs in console scripts.

**NOTE:** Console scripts now use the **ScriptContext pattern** (same as NPC behaviors).
This means you access APIs directly: `Player.GetMoney()` instead of `Api.Player.GetMoney()`

## Available Globals

```csharp
World       // Arch.Core.World - ECS World instance
Systems     // SystemManager - Access to game systems
Graphics    // GraphicsDevice - MonoGame graphics device
Logger      // ILogger - Logger for debugging
Print()     // void Print(string) - Output to console

// Direct API access (ScriptContext pattern - same as NPC behaviors):
Player      // PlayerApiService - Player management
Npc         // NpcApiService - NPC control
Map         // MapApiService - Map & teleportation
GameState   // GameStateApiService - Flags & variables
Dialogue    // DialogueApiService - Messages
Effects     // EffectApiService - Visual effects
Entity      // EntityApiService - Spawn/destroy entities at runtime
Registry    // RegistryApiService - Query available sprites, behaviors, NPCs, etc.
```

## Player - Player Management

```csharp
// Get player information
string GetPlayerName()                      // Returns player's name (e.g., "PLAYER")
Point GetPlayerPosition()                   // Returns (X, Y) tile coordinates
Direction GetPlayerFacing()                 // Returns North/South/East/West
bool IsPlayerMovementLocked()               // True if movement is locked

// Money management
int GetMoney()                              // Returns current money balance
void GiveMoney(int amount)                  // Add money to player
bool TakeMoney(int amount)                  // Remove money (returns true if successful)
bool HasMoney(int amount)                   // Check if player has enough money

// Movement control
void SetPlayerFacing(Direction direction)   // Change facing without moving
void SetPlayerMovementLocked(bool locked)   // Lock/unlock player movement
```

## Map - Map & World Management

```csharp
// Map queries
int GetCurrentMapId()                       // Returns current map ID
(int width, int height)? GetMapDimensions(int mapId)  // Get map size in tiles
bool IsPositionWalkable(int mapId, int x, int y)     // Check if tile is walkable
Entity[] GetEntitiesAt(int mapId, int x, int y)      // Get entities at position

// Teleportation
void TransitionToMap(int mapId, int x, int y)  // Teleport player to map/position

// Pathfinding
Direction GetDirectionTo(int fromX, int fromY, int toX, int toY)  // Get direction
```

## GameState - Game Flags & Variables

```csharp
// Flags (boolean values)
void SetFlag(string key, bool value)        // Set a game flag
bool GetFlag(string key)                    // Get a flag (false if not set)
bool HasFlag(string key)                    // Check if flag exists

// Variables (integer values)
void SetVariable(string key, int value)     // Set a game variable
int GetVariable(string key)                 // Get a variable (0 if not set)
bool HasVariable(string key)                // Check if variable exists

// Clear data
void ClearFlag(string key)                  // Remove a flag
void ClearVariable(string key)              // Remove a variable
```

## Dialogue - Text Display

```csharp
// Display messages
void ShowMessage(string message)            // Show a dialogue box
void QueueMessage(string message)           // Add to dialogue queue
bool IsDialogueActive()                     // Check if dialogue is showing
```

## Entity - Runtime Entity Spawning

```csharp
// Fluent NPC builder
INpcSpawnBuilder CreateNpc(int x, int y)    // Start fluent NPC builder
    .WithSprite(GameSpriteId spriteId)      // Set NPC sprite
    .WithBehavior(GameBehaviorId behaviorId)// Set NPC behavior
    .WithDisplayName(string name)           // Set display name
    .Visible()                              // Make NPC visible
    .Spawn()                                // Create and return entity

// Direct spawning
Entity SpawnNpcAt(int x, int y, spriteId, behaviorId?, displayName?)

// Lifecycle
void DestroyEntity(Entity entity)           // Remove entity immediately
void DestroyEntityDelayed(Entity e, float s)// Remove after delay
bool IsAlive(Entity entity)                 // Check if entity exists

// Queries
Entity[] FindEntitiesAt(int x, int y)       // Get entities at position
Entity[] FindNpcsInRadius(int x, int y, r)  // Find NPCs in radius
Entity[] FindEntitiesByTag(string tag)      // Find by tag
```

## Registry - Query Game Definitions

```csharp
// Sprite registry
IEnumerable<GameSpriteId> GetAllSpriteIds()
IEnumerable<GameSpriteId> GetSpriteIdsByCategory(string category)
bool SpriteExists(GameSpriteId spriteId)

// Behavior registry
IEnumerable<GameBehaviorId> GetAllBehaviorIds()
IEnumerable<GameBehaviorId> GetBehaviorIdsByCategory(string category)
bool BehaviorExists(GameBehaviorId behaviorId)

// NPC registry
IEnumerable<GameNpcId> GetAllNpcIds()
IEnumerable<GameNpcId> GetNpcIdsByCategory(string category)
bool NpcExists(GameNpcId npcId)

// Trainer registry
IEnumerable<GameTrainerId> GetAllTrainerIds()
IEnumerable<GameTrainerId> GetTrainerIdsByCategory(string category)
bool TrainerExists(GameTrainerId trainerId)

// Map registry
IEnumerable<GameMapId> GetAllMapIds()
bool MapExists(GameMapId mapId)

// Flag registry
IEnumerable<GameFlagId> GetAllFlagIds()
IEnumerable<GameFlagId> GetFlagIdsByCategory(string category)
```

## World - ECS World (Advanced)

```csharp
// Entity queries
int CountEntities()                         // Total entity count
void Query(QueryDescription query, action)  // Query entities
Entity Create()                             // Create new entity
void Destroy(Entity entity)                 // Destroy entity

// Component access (requires entity)
bool Has<T>(Entity entity)                  // Check for component
ref T Get<T>(Entity entity)                 // Get component reference
void Add<T>(Entity entity, T component)     // Add component
void Remove<T>(Entity entity)               // Remove component
```

## Common Patterns

### Get Player Info

```csharp
var name = Player.GetPlayerName();
var pos = Player.GetPlayerPosition();
var money = Player.GetMoney();
Print($"{name} at ({pos.X}, {pos.Y}) with ${money}");
```

### Teleport Player

```csharp
var currentMapId = Map.GetCurrentMapId();
Map.TransitionToMap(currentMapId, 10, 10);
Print("Teleported to (10, 10)");
```

### Check Map Info

```csharp
var mapId = Map.GetCurrentMapId();
var dims = Map.GetMapDimensions(mapId);
if (dims.HasValue)
    Print($"Map size: {dims.Value.width}x{dims.Value.height}");
```

### Set Game Flags

```csharp
// Set a flag
GameState.SetFlag("defeated_gym_1", true);

// Check a flag
if (GameState.GetFlag("defeated_gym_1"))
    Print("Already defeated Gym 1");
```

### Lock Player Movement (Cutscene)

```csharp
// Lock movement
Player.SetPlayerMovementLocked(true);
Print("Player movement locked");

// Later: unlock
Player.SetPlayerMovementLocked(false);
```

### Give/Take Money

```csharp
// Give money
Player.GiveMoney(1000);
Print("Gave $1000");

// Take money
if (Player.TakeMoney(500))
    Print("Took $500");
else
    Print("Not enough money!");
```

### Query Entities (Advanced)

```csharp
// Use CountEntities() helper (handles QueryDescription correctly)
var entityCount = CountEntities();
Print($"Total entities: {entityCount}");

// Note: Direct World queries require knowledge of ECS architecture
// Use the high-level Player, Map, GameState, etc. methods when possible
```

### Spawn NPCs Dynamically

```csharp
// Using fluent builder
var npc = Entity.CreateNpc(10, 15)
    .WithSprite(GameSpriteId.CreateNpc("boy"))
    .WithBehavior(GameBehaviorId.CreateNpcBehavior("wander"))
    .WithDisplayName("Wandering NPC")
    .Visible()
    .Spawn();
Print($"Spawned NPC: Entity {npc.Id}");

// Query available sprites
var sprites = Registry.GetSpriteIdsByCategory("npcs").ToList();
Print($"Available NPC sprites: {sprites.Count}");
```

### Destroy Entity

```csharp
// Find and destroy an NPC at a location
var entities = Entity.FindEntitiesAt(10, 15);
foreach (var e in entities)
{
    Entity.DestroyEntity(e);
    Print($"Destroyed entity {e.Id}");
}
```

## Tips

1. **Use Try-Catch**: Wrap API calls in try-catch for better error messages
2. **Use Print()**: Liberally use Print() to see what's happening
3. **Check Flags**: Always check return values (HasMoney, TakeMoney, etc.)
4. **Save Often**: Use `save <name>` to save working scripts
5. **Test Incrementally**: Load and test scripts frequently as you build them

## Example Script Template

```csharp
// My Custom Script
// Description of what this script does
// Uses ScriptContext pattern (same as NPC behaviors)

Print("=== My Script ===" );
Print("");

try
{
    // Your code here
    var money = Player.GetMoney();
    Print($"Current money: ${money}");

    // Do something
    Player.GiveMoney(100);
    Print("Added $100");

    Print("");
    Print("✅ Script completed successfully!");
}
catch (Exception ex)
{
    Print($"❌ Error: {ex.Message}");
}
```

## See Also

- `load example` - Example script with common patterns
- `load debug-info` - Comprehensive system information
- `load spawn-npcs [count]` - Spawn random NPCs with wandering behavior
- `help` - Console commands
- `/docs/CONSOLE_SCRIPT_LOADING.md` - Full script documentation

