# MonoBall Framework

A modern, extensible game engine built on **MonoGame** and **Arch ECS**, designed for creating 2D games with powerful modding support and developer-friendly tooling.

---

## ✨ Features

### 🎮 Core Engine
- **Arch ECS** - High-performance entity component system
- **Event-Driven Architecture** - Decoupled, extensible game systems
- **Hot Reload** - Edit scripts without restarting the game
- **Component Pooling** - Optimized memory management
- **Spatial Hashing** - Efficient collision detection

### 🔧 Developer Tools
- **Debug Console** - Powerful command-line interface with autocompletion
- **Event Inspector** - Real-time event monitoring and performance profiling
- **Performance Profiler** - Frame-by-frame performance analysis
- **Live Debugging** - Inspect and modify game state at runtime

### 🎨 Modding Support
- **C# Scripting** - Full C# scripting API using CSX files
- **Mod Loading** - Hot-loadable mods with dependency management
- **Comprehensive API** - Access to player, NPCs, maps, effects, and more
- **Script Templates** - Pre-built templates for common behaviors

### 🗺️ Map & World
- **Tiled Integration** - Import maps from Tiled Map Editor
- **Map Streaming** - Seamless transitions between maps
- **Dynamic Loading** - Load/unload map resources on demand
- **Connection System** - Link maps with automatic coordinate translation

---

## 🚀 Quick Start

### Prerequisites

- **.NET 9.0 SDK** or later
- **MonoGame** (included via NuGet)
- **Visual Studio 2022** or **Rider** (recommended)

### Building

```bash
# Clone the repository
git clone https://github.com/yourusername/MonoBallFramework.git
cd MonoBallFramework

# Restore dependencies
dotnet restore

# Build the project
dotnet build

# Run the game
dotnet run --project MonoBallFramework.Game
```

### Running Tests

```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test tests/MonoBallFramework.Game.Tests
```

---

## 📚 Documentation

- **[Getting Started](docs/getting-started/)** - Installation and setup
- **[Unified Scripting Guide](docs/scripting/unified-scripting-guide.md)** - ScriptBase system ⭐ **NEW**
- **[Architecture](docs/architecture/)** - System design and patterns
- **[Modding Guide](docs/modding/)** - Create your own mods
- **[API Reference](docs/api/)** - Complete API documentation
- **[Features](docs/features/)** - Feature-specific guides
- **[Guides](docs/guides/)** - How-to guides and tutorials

### Key Documents

- [Unified Scripting Guide](docs/scripting/unified-scripting-guide.md) - Complete scripting reference ⭐ **NEW**
- [Debug Console](docs/features/debug-console.md) - Console commands and usage
- [Event Inspector](docs/features/event-inspector.md) - Event debugging tool
- [Modding Quick Start](docs/modding/getting-started.md) - Your first mod

---

## 🎯 Project Structure

```
MonoBallFramework/
├── MonoBallFramework.Game/     # Main game project
│   ├── Assets/                 # Game assets (maps, sprites, etc.)
│   ├── Engine/                 # Core engine systems
│   ├── Components/             # ECS components
│   ├── GameSystems/            # Game-specific systems
│   ├── Scripting/              # Scripting engine
│   └── Infrastructure/         # DI, configuration, diagnostics
│
├── tests/                      # Test projects
│   ├── MonoBallFramework.Game.Tests/
│   ├── MonoBallFramework.Engine.Tests/
│   └── Integration/
│
├── Mods/                       # Example mods and templates
│   ├── examples/              # Example mod implementations
│   └── templates/             # Mod templates
│
├── examples/                   # Script examples
│   ├── basic-scripts/
│   ├── event-driven/
│   └── advanced/
│
└── docs/                       # Documentation
```

---

## 🎮 Example: Creating a Simple Mod

Create a file `Mods/my-first-mod/hello.csx`:

```csharp
// hello.csx - A simple greeting mod

// Access the player API
var player = Player;

// Log a message
Log("Hello from my first mod!");

// Define an event handler
void OnMapEntered(MapEnteredEvent evt)
{
    Log($"Player entered map: {evt.MapName}");
    Player.ShowMessage("Welcome to " + evt.MapName + "!");
}

// Subscribe to map enter events
Events.Subscribe<MapEnteredEvent>(OnMapEntered);
```

Load the mod:
1. Place `hello.csx` in `Mods/my-first-mod/`
2. Create `mod.json` with metadata
3. Run the game - mod loads automatically!

See the [Modding Guide](docs/modding/getting-started.md) for more details.

---

## 🔨 Development

### Console Commands

Press **`~`** in-game to open the debug console:

```
help                    # List all commands
player.teleport 10 5    # Teleport player
map.load test-map       # Load a different map
stats                   # Show performance stats
tab eventinspector      # Open event inspector
```

### Hot Reload

Edit any `.csx` script file while the game is running - changes apply immediately!

```bash
# Modify a behavior script
nano MonoBallFramework.Game/Assets/Scripts/Behaviors/patrol_behavior.csx

# Changes are automatically reloaded in-game
```

---

## 🧪 Testing

We use **xUnit** for unit and integration tests.

```bash
# Run all tests with coverage
dotnet test /p:CollectCoverage=true

# Run specific test category
dotnet test --filter Category=Integration

# Run memory validation tests
dotnet test tests/MemoryValidation
```

---

## 📈 Performance

MonoBall Framework is designed for high performance:

- **1000+ entities** at 60 FPS
- **< 5ms** event dispatch overhead
- **< 1ms** spatial queries
- **Minimal GC** pressure with object pooling

See [Performance Guide](docs/guides/performance-optimization.md) for optimization tips.

---

## 🤝 Contributing

We welcome contributions! Please see:

- [Contributing Guide](docs/contributing/pull-requests.md)
- [Code Style](docs/contributing/code-style.md)
- [Testing Guide](docs/contributing/testing.md)

### Quick Contribution Steps

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

---

## 📜 License

This project is licensed under the **MIT License** - see the [LICENSE](LICENSE) file for details.

---

## 🙏 Acknowledgments

- **MonoGame** - Cross-platform game framework
- **Arch ECS** - High-performance entity component system
- **Serilog** - Logging framework
- **Spectre.Console** - Beautiful console output
- **FontStashSharp** - Font rendering
- **Tiled** - Map editor

---

## 📞 Support

- **Documentation**: [docs/](docs/)
- **Issues**: [GitHub Issues](https://github.com/yourusername/MonoBallFramework/issues)
- **Discussions**: [GitHub Discussions](https://github.com/yourusername/MonoBallFramework/discussions)

---

## 🗺️ Roadmap

- [x] Core ECS architecture
- [x] Event system with inspector
- [x] Hot-reload scripting
- [x] Debug console
- [x] Component pooling
- [x] Spatial hashing
- [ ] Audio system
- [ ] Particle effects
- [ ] UI framework
- [ ] Networking (multiplayer)
- [ ] Asset pipeline
- [ ] Visual script editor

---

**Made with ❤️ for game developers and modders**

Star ⭐ this project if you find it useful!

