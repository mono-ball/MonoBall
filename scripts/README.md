# Debug Console Scripts

This directory contains C# script files (`.csx`) for the debug console.

## What are these scripts?

These are reusable C# scripts that can be loaded and executed in the debug console during gameplay. They provide quick
debugging tools and common operations.

## How to use them

In the debug console (press `` ` `` or `~` in-game):

```bash
# List all available scripts
scripts

# Load and execute a script
load example
load debug-info
load teleport-player

# Save your current console input as a script
save my-script.csx
```

## Included Scripts

- **`example.csx`** - Auto-generated basic example (created at runtime)
- **`debug-info.csx`** - Comprehensive system and player information
- **`teleport-player.csx`** - Quick teleport template for testing
- **`quick-test.csx`** - Template for rapid iteration on test code

## Creating Your Own Scripts

Scripts have access to the following globals:

```csharp
World       // Arch ECS World
Systems     // SystemManager
Api         // Scripting API (Player, GameState, Dialogue, etc.)
Graphics    // GraphicsDevice
Print()     // Output to console
```

Example script:

```csharp
// Get player info
var player = Api.Player.GetEntity();
var transform = player.Get<TransformComponent>();
Print($"Player at ({transform.X}, {transform.Y})");

// Give player money
Api.Player.GiveMoney(1000);
Print("Gave player $1000");
```

## Build Process

These scripts are automatically copied to `PokeSharp.Game/bin/Debug/net9.0/Scripts/` during build. Scripts created using
the `save` command in the console are saved to the runtime location.

To version control new scripts:

1. Create/save the script in the console
2. Copy it from `bin/Debug/net9.0/Scripts/` to this directory
3. Commit to git

## Documentation

See `/docs/CONSOLE_SCRIPT_LOADING.md` for full documentation.

