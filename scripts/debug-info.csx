// Debug Info Script
// Displays comprehensive system and player information
// Uses ScriptContext pattern (same as NPC behaviors)

Print("=== GAME DEBUG INFO ===");
Print("");

// === PLAYER INFO ===
Print("--- PLAYER INFO ---");
try
{
    // Direct API access (ScriptContext pattern)
    var playerName = Player.GetPlayerName();
    var playerPos = Player.GetPlayerPosition();
    var playerFacing = Player.GetPlayerFacing();
    var playerMoney = Player.GetMoney();
    var movementLocked = Player.IsPlayerMovementLocked();

    Print($"Name: {playerName}");
    Print($"Position: ({playerPos.X}, {playerPos.Y})");
    Print($"Facing: {playerFacing}");
    Print($"Money: ${playerMoney}");
    Print($"Movement: {(movementLocked ? "LOCKED" : "Free")}");
}
catch (Exception ex)
{
    Print($"Error getting player info: {ex.Message}");
}

Print("");

// === MAP INFO ===
Print("--- MAP INFO ---");
try
{
    var currentMapId = Map.GetCurrentMapId();
    var mapDims = Map.GetMapDimensions(currentMapId);

    Print($"Current Map ID: {currentMapId}");
    if (mapDims.HasValue)
    {
        Print($"Map Size: {mapDims.Value.width}x{mapDims.Value.height} tiles");
    }
}
catch (Exception ex)
{
    Print($"Error getting map info: {ex.Message}");
}

Print("");

// === WORLD INFO ===
Print("--- WORLD INFO ---");
try
{
    // Use helper method (handles QueryDescription correctly)
    var entityCount = CountEntities();
    Print($"Total Entities: {entityCount}");
}
catch (Exception ex)
{
    Print($"Error getting world info: {ex.Message}");
}

Print("");

// === SYSTEM INFO ===
Print("--- SYSTEM INFO ---");
Print($"Graphics Device: {Graphics.Adapter.Description}");
Print($"Viewport: {Graphics.Viewport.Width}x{Graphics.Viewport.Height}");

Print("");
Print("=== END DEBUG INFO ===");
