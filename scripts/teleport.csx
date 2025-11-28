// Parameterized Teleport Script
// Usage: load teleport.csx <x> <y> [mapId]
// Example: load teleport 10 20
// Example: load teleport 10 20 1
// Uses ScriptContext pattern (same as NPC behaviors)

// Check if we have required arguments
if (Args.Length < 2)
{
    Print("❌ Usage: load teleport.csx <x> <y> [mapId]");
    Print("  x      - Target X coordinate (required)");
    Print("  y      - Target Y coordinate (required)");
    Print("  mapId  - Target map ID (optional, defaults to current map)");
    Print("");
    Print("Examples:");
    Print("  load teleport 10 20        // Teleport to (10, 20) on current map");
    Print("  load teleport 15 25 2      // Teleport to (15, 25) on map 2");
    return;
}

// Parse arguments
if (!int.TryParse(Args[0], out int x))
{
    Print($"❌ Error: Invalid X coordinate '{Args[0]}'. Must be a number.");
    return;
}

if (!int.TryParse(Args[1], out int y))
{
    Print($"❌ Error: Invalid Y coordinate '{Args[1]}'. Must be a number.");
    return;
}

// Optional map ID (defaults to current map)
int mapId = Map.GetCurrentMapId();
if (Args.Length >= 3)
{
    if (!int.TryParse(Args[2], out mapId))
    {
        Print($"❌ Error: Invalid map ID '{Args[2]}'. Must be a number.");
        return;
    }
}

// Show current position
var currentPos = Player.GetPlayerPosition();
var currentMapId = Map.GetCurrentMapId();
Print($"📍 Current: Map {currentMapId}, Position ({currentPos.X}, {currentPos.Y})");

// Teleport
try
{
    Map.TransitionToMap(mapId, x, y);
    Print($"✨ Teleported to Map {mapId}, Position ({x}, {y})");
}
catch (Exception ex)
{
    Print($"❌ Teleport failed: {ex.Message}");
}
