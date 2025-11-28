// Quick Test Script
// Template for quick one-off debugging tasks
// Edit this file and reload it for fast iteration
// Uses ScriptContext pattern (same as NPC behaviors)

Print("=== Quick Test Script ===");
Print("");

// Your test code here
// Example: Test player money system
Print("Testing player money...");
var currentMoney = Player.GetMoney();
Print($"Current money: ${currentMoney}");

// Give 100 money
Player.GiveMoney(100);
var newMoney = Player.GetMoney();
Print($"After adding $100: ${newMoney}");

// Check the difference
Print($"Difference: ${newMoney - currentMoney}");
Print("");
Print("✅ Test complete!");
