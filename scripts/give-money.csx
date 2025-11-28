// Parameterized Give Money Script
// Usage: load give-money.csx <amount>
// Example: load give-money 1000
// Uses ScriptContext pattern (same as NPC behaviors)

// Check if we have required arguments
if (Args.Length < 1)
{
    Print("❌ Usage: load give-money.csx <amount>");
    Print("  amount - Amount of money to give (required)");
    Print("");
    Print("Examples:");
    Print("  load give-money 1000       // Give $1000");
    Print("  load give-money 500        // Give $500");
    return;
}

// Parse argument
if (!int.TryParse(Args[0], out int amount))
{
    Print($"❌ Error: Invalid amount '{Args[0]}'. Must be a number.");
    return;
}

// Validate amount
if (amount <= 0)
{
    Print($"❌ Error: Amount must be positive. Got: {amount}");
    return;
}

// Show current money
var currentMoney = Player.GetMoney();
Print($"💰 Current money: ${currentMoney}");

// Give money
try
{
    Player.GiveMoney(amount);
    var newMoney = Player.GetMoney();
    Print($"✅ Gave ${amount}. New balance: ${newMoney}");
}
catch (Exception ex)
{
    Print($"❌ Failed to give money: {ex.Message}");
}
