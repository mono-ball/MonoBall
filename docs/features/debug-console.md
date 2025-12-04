# Debug Console

The Debug Console is a powerful in-game development tool that provides command execution, C# scripting, and real-time debugging capabilities.

---

## Quick Start

### Opening the Console

Press **`~`** (tilde/backtick) to toggle the console.

The console features:
- **Command execution** - Run built-in or custom commands
- **C# scripting** - Execute C# code in real-time
- **Auto-completion** - Tab to complete commands/variables
- **Command history** - ↑↓ to navigate previous commands
- **Syntax highlighting** - Color-coded C# syntax
- **Multi-line editor** - Shift+Enter for new lines

### Basic Usage

```
# List all commands
help

# Get help for specific command
help player

# Execute a command
player.teleport 10 5

# Run C# code
var pos = Player.Position;
Log($"Player at: {pos}");
```

---

## Key Bindings

### Essential Keys

| Key | Action |
|-----|--------|
| **`~`** | Toggle console open/close |
| **Enter** | Execute command/script |
| **Shift+Enter** | New line (multi-line mode) |
| **Esc** | Close console |
| **Tab** | Auto-complete / next suggestion |
| **Shift+Tab** | Previous suggestion |

### Navigation

| Key | Action |
|-----|--------|
| **↑** | Previous command in history |
| **↓** | Next command in history |
| **Ctrl+↑** | Scroll output up |
| **Ctrl+↓** | Scroll output down |
| **Home** | Move cursor to start of line |
| **End** | Move cursor to end of line |
| **Ctrl+Home** | Go to first command in history |
| **Ctrl+End** | Go to last command in history |

### Editing

| Key | Action |
|-----|--------|
| **Backspace** | Delete character before cursor |
| **Delete** | Delete character after cursor |
| **Ctrl+Backspace** | Delete word before cursor |
| **Ctrl+Delete** | Delete word after cursor |
| **Ctrl+C** | Copy selected text |
| **Ctrl+X** | Cut selected text |
| **Ctrl+V** | Paste from clipboard |
| **Ctrl+A** | Select all text |

### Selection

| Key | Action |
|-----|--------|
| **Shift+←/→** | Select character left/right |
| **Shift+Home** | Select to start of line |
| **Shift+End** | Select to end of line |
| **Mouse drag** | Select text |

---

## Built-in Commands

### Help & Information

```bash
help                    # List all available commands
help <command>          # Show help for specific command
stats                   # Show performance statistics
vars                    # List all variables
```

### Player Commands

```bash
player.teleport X Y     # Teleport player to coordinates
player.setspeed N       # Set player movement speed
player.position         # Show current position
player.facing           # Show facing direction
```

### Map Commands

```bash
map.load <name>         # Load a different map
map.info               # Show current map information
map.connections        # List map connections
map.reload             # Reload current map
```

### Debug Tabs

```bash
tab profiler           # Open performance profiler (or F2)
tab eventinspector     # Open event inspector (or F4)
tab stats              # Open stats panel (or F3)
```

### Script Management

```bash
reload <script>        # Hot-reload a script file
scripts.list           # List all loaded scripts
scripts.reload         # Reload all scripts
mods.list             # List all loaded mods
```

### System Commands

```bash
exit / quit            # Close the game
clear                  # Clear console output
history                # Show command history
alias <name> <cmd>     # Create command alias
```

---

## C# Scripting

The console supports full C# scripting with access to game APIs.

### Available APIs

```csharp
// Player API
Player.Position
Player.Teleport(x, y)
Player.ShowMessage("Hello!")

// Map API
Map.Load("mapname")
Map.CurrentMap
Map.GetTile(x, y)

// NPC API
NPC.Spawn("npcId", x, y)
NPC.Find("npcName")
NPC.GetAll()

// Effects API
Effect.Play("effectName", x, y)
Effect.Stop("effectName")

// Logging
Log("message")
LogInfo("info")
LogWarning("warning")
LogError("error")

// Events
Events.Publish(new MyEvent())
Events.Subscribe<MyEvent>(handler)
```

### Scripting Examples

**Simple math:**
```csharp
var result = 10 + 5 * 2;
Log($"Result: {result}");
```

**Loop through NPCs:**
```csharp
foreach (var npc in NPC.GetAll())
{
    Log($"NPC: {npc.Name} at {npc.Position}");
}
```

**Conditional logic:**
```csharp
if (Player.Position.X > 10)
{
    Player.ShowMessage("You're on the right side!");
}
else
{
    Player.ShowMessage("You're on the left side!");
}
```

**Define functions:**
```csharp
void TeleportRandom()
{
    var x = Random.Next(0, 20);
    var y = Random.Next(0, 15);
    Player.Teleport(x, y);
}

TeleportRandom();
```

---

## Advanced Features

### Multi-line Scripts

Use **Shift+Enter** to create multi-line scripts:

```csharp
// Press Shift+Enter after each line
var items = new List<string> { 
    "Potion", 
    "Super Potion", 
    "Hyper Potion" 
};

foreach (var item in items)
{
    Log($"Item: {item}");
}
// Press Enter to execute
```

### Auto-completion

The console provides intelligent auto-completion:

- **Commands** - Tab to complete command names
- **Variables** - Tab to complete variable names
- **Properties** - Dot notation shows available members
- **Methods** - Parentheses trigger parameter hints

Type `play` and press Tab → `player.`  
Type `Player.` and press Tab → shows all Player API methods

### Command Aliases

Create shortcuts for frequently used commands:

```bash
# Create alias
alias tp player.teleport

# Use alias
tp 10 5    # Same as: player.teleport 10 5
```

### Variable Persistence

Variables persist across console sessions:

```csharp
// Define in one session
var mySpawnPoint = new Vector2(10, 5);

// Use in another session
Player.Teleport(mySpawnPoint.X, mySpawnPoint.Y);
```

---

## Customization

### Console Settings

Configure via `Config/appsettings.json`:

```json
{
  "DebugConsole": {
    "Size": "Medium",           // Small, Medium, Large
    "Theme": "pokeball",        // Theme name
    "FontSize": 16,             // Font size in pixels
    "SyntaxHighlightingEnabled": true,
    "AutoCompleteEnabled": true,
    "PersistHistory": true,     // Save history between sessions
    "LoggingEnabled": true,     // Show console commands in log
    "MinimumLogLevel": "Debug", // Debug, Info, Warning, Error
    "AutoLoadStartupScript": true,
    "StartupScriptName": "startup.csx"
  }
}
```

### Startup Scripts

Create `Scripts/startup.csx` to run code automatically when the console opens:

```csharp
// startup.csx
Log("Console loaded! Custom commands available.");

// Define helper functions
void QuickSave()
{
    Log("Game saved!");
    // Your save logic
}

// Create aliases
// (Note: aliases must be set via console commands)
Log("Type 'qs' for quick save");
```

### Custom Commands

Create custom commands by implementing `IConsoleCommand`:

```csharp
public class MyCustomCommand : IConsoleCommand
{
    public string Name => "mycmd";
    public string Description => "My custom command";
    
    public void Execute(string[] args, IConsoleOutput output)
    {
        output.WriteLine("Hello from my command!");
    }
}
```

Register in your mod's initialization.

---

## Troubleshooting

### Console won't open

- **Check key binding**: Make sure `~` isn't bound to something else
- **Check focus**: Click on the game window first
- **Check logs**: Look for console initialization errors

### Auto-completion not working

- **Wait for initialization**: Auto-completion loads after game starts
- **Check settings**: `AutoCompleteEnabled` must be `true`
- **Try Tab twice**: Sometimes needs a double-tap

### Syntax highlighting broken

- **Check theme**: Some themes may have issues
- **Reset settings**: Delete console settings, restart game
- **Check logs**: Look for shader compilation errors

### Commands not executing

- **Check syntax**: Ensure proper command format
- **Check permissions**: Some commands may be disabled
- **Check logs**: Look for execution errors

### History not saving

- **Check setting**: `PersistHistory` must be `true`
- **Check file permissions**: Console needs write access
- **Check path**: History saved to `Data/console_history.txt`

---

## Performance Tips

1. **Close when not needed** - Console rendering has overhead
2. **Limit script complexity** - Complex scripts can cause frame drops
3. **Use profiler** - Check `tab profiler` to see console impact
4. **Clear output** - Use `clear` to remove old output
5. **Disable syntax highlighting** - For better performance on low-end systems

---

## See Also

- [Event Inspector](event-inspector.md) - Real-time event debugging
- [Performance Profiler](../guides/performance-optimization.md) - Performance analysis
- [Scripting API](../api/player-api.md) - Complete API reference
- [Modding Guide](../modding/getting-started.md) - Creating mods

---

**Last Updated**: December 4, 2024

