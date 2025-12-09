// Spawn NPCs Debug Script
// Creates N NPCs with random sprites and wandering behavior at non-colliding locations
// Uses ScriptContext pattern (same as NPC behaviors)
//
// Usage: load spawn-npcs [count]
// Example: load spawn-npcs 5
// Default: 3 NPCs

using System.Linq;
using MonoBallFramework.Game.Engine.Core.Types;

Print("=== Spawn Random NPCs ===");
Print("");

// Parse argument for NPC count (default: 1)
int npcCount = 1;
if (Args.Length > 0 && int.TryParse(Args[0], out int parsedCount))
{
    npcCount = Math.Clamp(parsedCount, 1, 1000); // Limit to 1-50 NPCs
}

Print($"Spawning {npcCount} NPC(s)...");
Print("");

// Get current map info
var currentMapId = Map.GetCurrentMapId();
var mapDims = Map.GetMapDimensions(currentMapId);

if (!mapDims.HasValue)
{
    Print("Error: Could not get map dimensions!");
    return;
}

var (mapWidth, mapHeight) = mapDims.Value;
Print($"Map: {currentMapId}");
Print($"Size: {mapWidth}x{mapHeight}");
Print("");

// Get available NPC sprites from registry - filter for sprites with proper NPC animations
var availableSprites = Registry.GetSpriteIdsByCategory("npcs")
    .Where(s => s.SpriteName.StartsWith("generic_"))
    .Where(s => Registry.HasNpcAnimations(s)) // Only sprites with go_* and face_* animations
    .ToList();

if (availableSprites.Count == 0)
{
    Print("Warning: No sprites with NPC animations found!");
    Print("Trying all generic sprites without animation filter...");

    // Fallback: try without animation filter
    availableSprites = Registry.GetSpriteIdsByCategory("npcs")
        .Where(s => s.SpriteName.StartsWith("generic_"))
        .ToList();

    if (availableSprites.Count == 0)
    {
        Print("Error: No generic NPC sprites in registry!");
        Print("Using hardcoded fallback sprite ID...");
        availableSprites.Add(GameSpriteId.CreateNpc("boy"));
    }
}

Print($"Available sprites with animations: {availableSprites.Count}");

// Wandering behavior ID - must match the typeId in wander.json exactly
// wander.json has typeId: "base:behavior:movement/wander" (category=movement)
var wanderBehavior = new GameBehaviorId("movement", "wander");
Print($"Behavior: {wanderBehavior}");
Print("");

// Track used positions to prevent collisions
var usedPositions = new HashSet<(int x, int y)>();

// Get player position to avoid spawning on player
var playerPos = Player.GetPlayerPosition();
usedPositions.Add((playerPos.X, playerPos.Y));

// Add adjacent tiles to player as blocked too
usedPositions.Add((playerPos.X + 1, playerPos.Y));
usedPositions.Add((playerPos.X - 1, playerPos.Y));
usedPositions.Add((playerPos.X, playerPos.Y + 1));
usedPositions.Add((playerPos.X, playerPos.Y - 1));

var random = new Random();
var spawnedNpcs = new List<Arch.Core.Entity>();
int attempts = 0;
int maxAttempts = npcCount * 100; // Prevent infinite loop

Print("Spawning NPCs:");

while (spawnedNpcs.Count < npcCount && attempts < maxAttempts)
{
    attempts++;

    // Generate random position within map bounds (with margin)
    int margin = 2;
    int x = random.Next(margin, mapWidth - margin);
    int y = random.Next(margin, mapHeight - margin);

    // Check if position is already used
    if (usedPositions.Contains((x, y)))
        continue;

    // Check if position is walkable
    if (!Map.IsPositionWalkable(currentMapId, x, y))
        continue;

    // Check if there are entities already at this position
    var entitiesAtPos = Entity.FindEntitiesAt(x, y);
    if (entitiesAtPos.Length > 0)
    {
        usedPositions.Add((x, y));
        continue;
    }

    // Pick a random sprite
    var spriteId = availableSprites[random.Next(availableSprites.Count)];

    // Generate a display name and NPC ID (restricted to "generic" category)
    string displayName = $"NPC #{spawnedNpcs.Count + 1}";
    var npcId = GameNpcId.Create($"spawned_{spawnedNpcs.Count + 1}", "generic");

    try
    {
        // Spawn the NPC using the fluent builder
        var npc = Entity.CreateNpc(x, y)
            .FromDefinition(npcId)
            .WithSprite(spriteId)
            .WithBehavior(wanderBehavior)
            .WithDisplayName(displayName)
            .OnMap(currentMapId) // Required for collision checks and spatial hash
            .Visible()
            .Spawn();

        spawnedNpcs.Add(npc);
        usedPositions.Add((x, y));

        Print($"  [{spawnedNpcs.Count}] {displayName} at ({x}, {y}) - {spriteId.Name}");
    }
    catch (Exception ex)
    {
        Print($"  Error spawning NPC: {ex.Message}");
    }
}

Print("");

if (spawnedNpcs.Count < npcCount)
{
    Print($"Warning: Only spawned {spawnedNpcs.Count}/{npcCount} NPCs");
    Print($"  (Could not find enough valid positions after {attempts} attempts)");
}
else
{
    Print($"Successfully spawned {spawnedNpcs.Count} NPC(s)!");
}

Print("");
Print("NPCs will wander randomly around the map.");
Print("Use 'entity list' command to see all entities.");
