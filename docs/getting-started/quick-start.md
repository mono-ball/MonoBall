# Quick Start Guide

Get up and running with MonoBall Framework in 5 minutes!

---

## Prerequisites

- **.NET 9.0 SDK** or later ([Download](https://dotnet.microsoft.com/download))
- **Visual Studio 2022**, **Rider**, or **VS Code** (recommended: Rider)
- **Git** (for cloning the repository)

---

## Installation

### 1. Clone the Repository

```bash
git clone https://github.com/yourusername/MonoBallFramework.git
cd MonoBallFramework
```

### 2. Restore Dependencies

```bash
dotnet restore
```

### 3. Build the Project

```bash
dotnet build
```

### 4. Run the Game

```bash
dotnet run --project MonoBallFramework.Game
```

The game should launch! You'll see the test map with a player character.

---

## First Steps

### Open the Debug Console

1. Press **`~`** (tilde/backtick key, above Tab)
2. The console will slide in from the top
3. Type `help` to see available commands

### Try Some Commands

```bash
# Show help
help

# Teleport the player
player.teleport 15 10

# Load a different map
map.load test-map

# Show performance stats
tab stats
```

### Run C# Code

The console supports full C# scripting:

```csharp
// Get player position
var pos = Player.Position;
Log($"Player at: ({pos.X}, {pos.Y})");

// Loop and do something
for (int i = 0; i < 5; i++)
{
    Log($"Count: {i}");
}
```

---

## Exploring the Project

### Project Structure

```
MonoBallFramework/
├── MonoBallFramework.Game/     # Main game project
│   ├── Assets/                 # Maps, sprites, data
│   ├── Engine/                 # Core engine systems
│   ├── Components/             # ECS components
│   ├── GameSystems/            # Game-specific systems
│   └── Scripting/              # Scripting engine
│
├── Mods/                       # Example mods
│   ├── examples/              # Working examples
│   └── templates/             # Starting templates
│
├── examples/                   # Script examples
│   ├── basic-scripts/
│   └── event-driven/
│
└── tests/                      # Test projects
```

### Key Directories

**Assets/**
- `Assets/Data/Maps/` - Tiled map files (.json)
- `Assets/Sprites/` - Character sprites
- `Assets/Scripts/` - Behavior and tile behavior scripts

**Engine/**
- `Engine/Core/` - ECS, events, templates
- `Engine/Debug/` - Debug console implementation
- `Engine/Systems/` - Core systems (pooling, queries)
- `Engine/UI/` - Debug UI framework

---

## Create Your First Mod

### 1. Create Mod Folder

```bash
mkdir -p Mods/my-first-mod
cd Mods/my-first-mod
```

### 2. Create Mod Manifest

Create `mod.json`:

```json
{
  "id": "my-first-mod",
  "name": "My First Mod",
  "version": "1.0.0",
  "author": "Your Name",
  "description": "My first MonoBall Framework mod!",
  "scripts": [
    "hello.csx"
  ]
}
```

### 3. Create Script

Create `hello.csx`:

```csharp
// hello.csx - A simple greeting mod

Log("Hello from my first mod!");

// Subscribe to map enter events
void OnMapEntered(MapEnteredEvent evt)
{
    Player.ShowMessage($"Welcome to {evt.MapName}!");
    Log($"Player entered: {evt.MapName}");
}

Events.Subscribe<MapEnteredEvent>(OnMapEntered);
```

### 4. Run the Game

```bash
dotnet run --project ../../MonoBallFramework.Game
```

Your mod will load automatically! When you enter a map, you'll see your welcome message.

---

## Hot Reload

Edit your script while the game is running - changes apply immediately!

1. Keep the game running
2. Edit `hello.csx`
3. Save the file
4. Watch the console - you'll see "Script reloaded: hello.csx"
5. Your changes are live!

---

## Debug Tools

### Debug Console (`~`)

- Command execution
- C# scripting
- Auto-completion
- Command history

[Full Console Guide →](../features/debug-console.md)

### Event Inspector (F4)

- Real-time event monitoring
- Performance profiling per event
- Handler execution tracking

[Event Inspector Guide →](../features/event-inspector.md)

### Performance Profiler (F2)

- Frame-by-frame analysis
- System execution times
- Memory usage tracking

[Performance Guide →](../guides/performance-optimization.md)

### Stats Panel (F3)

- FPS counter
- Entity count
- Memory usage
- System performance

---

## Next Steps

### Learn the Basics

1. [Architecture Overview](../architecture/overview.md) - Understanding the engine
2. [ECS Systems](../architecture/ecs-systems.md) - Entity Component System explained
3. [Event System](../architecture/event-system.md) - Event-driven architecture

### Start Modding

1. [Modding Guide](../modding/getting-started.md) - Complete modding tutorial
2. [API Reference](../modding/api-reference.md) - Available APIs
3. [Event Reference](../modding/event-reference.md) - All events you can use
4. [Script Templates](../modding/script-templates.md) - Pre-built examples

### Explore Features

1. [Debug Console](../features/debug-console.md) - Console commands and scripting
2. [Hot Reload](../features/hot-reload.md) - Live code reloading
3. [Map Streaming](../features/map-streaming.md) - Seamless map transitions

---

## Common Issues

### Game won't start

**Problem**: `dotnet run` fails with build errors  
**Solution**: 
```bash
dotnet clean
dotnet restore
dotnet build
```

### Console won't open

**Problem**: Pressing `~` doesn't open console  
**Solution**: 
- Make sure game window has focus
- Try `Ctrl+~` instead
- Check key bindings in `Config/appsettings.json`

### Mod not loading

**Problem**: Created a mod but it doesn't appear  
**Solution**:
- Check `mod.json` syntax (must be valid JSON)
- Ensure script files exist and have `.csx` extension
- Check console output for error messages
- Verify mod folder is in `Mods/` directory

### Hot reload not working

**Problem**: Saving script doesn't trigger reload  
**Solution**:
- Check console - should see reload messages
- Ensure file has `.csx` extension
- Try saving twice
- Check file watcher is enabled in settings

---

## Getting Help

- **Documentation**: Browse [docs/](../)
- **Examples**: Check `examples/` and `Mods/examples/`
- **Issues**: Report bugs on GitHub
- **Console**: Type `help <command>` for command-specific help

---

## What's Next?

Now that you're set up, here are some fun things to try:

1. **Modify a behavior script**
   - Edit `Assets/Scripts/Behaviors/patrol_behavior.csx`
   - Change the patrol pattern
   - Save and watch NPCs update in real-time!

2. **Create a custom command**
   - See `examples/basic-scripts/`
   - Implement your own console command

3. **Build a complete mod**
   - Check `Mods/examples/weather-system/`
   - Learn how to add new game features

4. **Explore the engine**
   - Read the [Architecture Guide](../architecture/overview.md)
   - Understand how systems work together

---

**Welcome to MonoBall Framework!** 🎮✨

Happy modding!

