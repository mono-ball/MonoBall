// Teleport Player Script
// Quick teleport to common locations
// Uses ScriptContext pattern (same as NPC behaviors)
//
// NOTE: You need to know the map ID and coordinates for your locations.
// Use 'load debug-info' to see your current map ID and position.

Print("=== Teleport Player ===");
Print("");
Print("Current location:");
var currentPos = Player.GetPlayerPosition();
var currentMapId = Map.GetCurrentMapId();
Print($"  Map ID: {currentMapId}");
Print($"  Position: ({currentPos.X}, {currentPos.Y})");
Print("");

// Example teleport locations (edit these to match your game)
// Format: Map.TransitionToMap(mapId, x, y)

Print("Available locations (uncomment one to use):");
Print("  1. Same map, different position");
Print("  2. Different map (if you have multiple maps)");
Print("");

// UNCOMMENT ONE OF THESE LINES TO TELEPORT:

// Example 1: Move to position (10, 10) on current map
// Map.TransitionToMap(currentMapId, 10, 10);
// Print($"✅ Teleported to ({10}, {10}) on map {currentMapId}");

// Example 2: Move to position (20, 15) on current map
// Map.TransitionToMap(currentMapId, 20, 15);
// Print($"✅ Teleported to ({20}, {15}) on map {currentMapId}");

// Example 3: Change to a different map (map ID 2, position 5, 5)
// Map.TransitionToMap(2, 5, 5);
// Print($"✅ Teleported to map 2 at ({5}, {5})");

Print("⚠️  No teleport executed. Uncomment a line above to teleport.");
Print("   Edit this script with your desired coordinates, then reload it.");
