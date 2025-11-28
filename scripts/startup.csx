// Startup Script
// This script runs automatically when the console initializes
// Use it to set up common variables, helper functions, or debugging shortcuts
// Uses ScriptContext pattern (same as NPC behaviors)

// To disable auto-load: Set console.Config.AutoLoadStartupScript = false

// === EXAMPLE: Define helper variables ===
// Uncomment these to have them available in every console session:

// var currentMapId = Map.GetCurrentMapId();
// var playerPos = Player.GetPlayerPosition();
// Print($"🎮 Startup: Map {currentMapId}, Player at ({playerPos.X}, {playerPos.Y})");

// === EXAMPLE: Define helper functions ===
// Create shortcuts for common debugging tasks:

// Quick teleport helper
// void TP(int x, int y) {
//     Map.TransitionToMap(Map.GetCurrentMapId(), x, y);
//     Print($"✨ Teleported to ({x}, {y})");
// }

// Quick money helper
// void GM(int amount) {
//     Player.GiveMoney(amount);
//     Print($"💰 Gave {amount} money. New balance: ${Player.GetMoney()}");
// }

// Quick flag helper
// void SetF(string flag, bool value) {
//     GameState.SetFlag(flag, value);
//     Print($"🚩 Flag '{flag}' set to {value}");
// }

// === EXAMPLE: Common debug setup ===
// Uncomment to auto-enable logging:

// Print("📋 Enabling console logging...");
// Config.LoggingEnabled = true;
// Config.MinimumLogLevel = LogLevel.Debug;

// === YOUR CUSTOM STARTUP CODE HERE ===
// Add your own initialization code below:

// Silence is golden - this script runs silently unless you Print() something
// Uncomment the line below to confirm startup script is running:
// Print("✅ Startup script loaded!");
