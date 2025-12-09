// Collision Service Control Script
// Toggles collision service on/off globally via GameState
//
// Usage: load collision [on|off]
// Example: load collision off  - Disables collision (walk through everything)
//          load collision on   - Re-enables collision
//          load collision      - Shows current status

Print("=== Collision Service Control ===");
Print("");

// Parse argument (default: show status)
string mode = Args.Length > 0 ? Args[0].ToLowerInvariant() : "status";

if (mode == "status")
{
    bool enabled = GameState.CollisionServiceEnabled;
    Print($"Collision Service: {(enabled ? "ENABLED" : "DISABLED")}");
    Print("");
    Print("Usage:");
    Print("  load collision off  - Disable collision");
    Print("  load collision on   - Enable collision");
}
else if (mode == "off" || mode == "disable" || mode == "0")
{
    GameState.CollisionServiceEnabled = false;
    Print("Collision Service: DISABLED");
    Print("Walk through walls and NPCs!");
    Print("");
    Print("Use 'load collision on' to restore.");
}
else if (mode == "on" || mode == "enable" || mode == "1")
{
    GameState.CollisionServiceEnabled = true;
    Print("Collision Service: ENABLED");
    Print("Collision restored to normal.");
}
else
{
    Print($"Unknown mode: '{mode}'");
    Print("");
    Print("Valid options:");
    Print("  off, disable, 0  - Disable collision service");
    Print("  on, enable, 1    - Enable collision service");
}
