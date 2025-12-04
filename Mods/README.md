# MonoBall Framework Mods Directory

This directory contains user-created mods that extend MonoBall Framework functionality.

## 📁 Directory Structure

```
Mods/
├── README.md                    # This file
├── example-mod/                 # Example mod demonstrating structure
│   ├── mod.json                # Mod manifest
│   ├── ledge_crumble.csx       # Script 1
│   └── jump_boost_item.csx     # Script 2
└── your-mod-name/
    ├── mod.json
    └── your_script.csx
```

## 📋 Mod Manifest Format

Each mod **must** have a `mod.json` file in its root directory:

```json
{
  "id": "your-mod-id",
  "name": "Your Mod Name",
  "version": "1.0.0",
  "author": "Your Name",
  "description": "What your mod does",
  "scripts": [
    "script1.csx",
    "script2.csx"
  ],
  "dependencies": [
    "MonoBall Framework-core >= 1.0.0",
    "another-mod == 2.1.0"
  ],
  "permissions": [
    "events:subscribe",
    "world:modify",
    "effects:play"
  ],
  "priority": 0
}
```

### Manifest Fields

| Field | Required | Description |
|-------|----------|-------------|
| `id` | ✅ Yes | Unique identifier (kebab-case recommended) |
| `name` | ✅ Yes | Human-readable mod name |
| `version` | ✅ Yes | Semantic version (e.g., "1.0.0") |
| `author` | ❌ No | Creator name or organization |
| `description` | ❌ No | What the mod does |
| `scripts` | ✅ Yes | Array of .csx files to load (relative paths) |
| `dependencies` | ❌ No | Array of mod dependencies with version constraints |
| `permissions` | ❌ No | Array of required permissions (future use) |
| `priority` | ❌ No | Load order priority (higher = loads first, default: 0) |

## 🔗 Dependencies

Dependencies use semantic versioning constraints:

- `mod-id >= 1.0.0` - Greater than or equal to version
- `mod-id == 2.1.0` - Exact version match
- `mod-id > 1.0.0` - Greater than
- `mod-id <= 3.0.0` - Less than or equal to
- `mod-id < 4.0.0` - Less than

### Dependency Resolution

Mods are loaded in **dependency order**:
1. Dependencies are resolved using topological sort
2. If mod A depends on mod B, B loads first
3. Circular dependencies are detected and cause an error
4. Mods with higher `priority` values load first when no dependencies exist

## 📝 Writing Mod Scripts

All mod scripts must:
1. Inherit from `ScriptBase`
2. Override `RegisterEventHandlers(ScriptContext context)`
3. Return an instance at the end of the script

### Example Script Template

```csharp
using MonoBall Framework.Game.Scripting.Runtime;
using MonoBall Framework.Engine.Core.Events;

public class MyModScript : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext context)
    {
        context.EventBus.Subscribe<SomeEvent>(OnEvent);
        context.Logger.LogInformation("My mod loaded");
    }

    private void OnEvent(SomeEvent evt)
    {
        // Handle event
        Context.Logger.LogInformation("Event fired!");
    }

    public override void OnUnload()
    {
        Context?.Logger.LogInformation("My mod unloaded");
    }
}

return new MyModScript();
```

## 🔧 Available APIs

Scripts have access to the following APIs through `Context.Api`:

- **Audio**: `Context.Api.Audio.PlaySound(string soundName)`
- **World**: `Context.Api.World` (access to world state)
- **Events**: `Context.EventBus.Subscribe<TEvent>(handler)`
- **Logger**: `Context.Logger.LogInformation(string message)`

## 🚀 Loading Mods

Mods are automatically loaded when the game starts:

1. Game scans `/Mods/` directory for subdirectories
2. Each subdirectory is checked for `mod.json`
3. Manifests are parsed and validated
4. Dependencies are resolved
5. Scripts are loaded in dependency order
6. Each script is initialized with a `ScriptContext`

## ⚠️ Common Errors

### "Mod manifest missing required 'id' field"
- Ensure your `mod.json` has all required fields: `id`, `name`, `version`, `scripts`

### "Circular dependency detected"
- Mod A depends on Mod B, which depends on Mod A
- Remove circular references in `dependencies`

### "Mod depends on 'xyz' which is not installed"
- Install the required dependency mod
- Or remove it from your `dependencies` array

### "Script file not found"
- Check that script filenames in `scripts` array match actual files
- Paths are relative to the mod directory

## 🛠️ Testing Your Mod

1. Create a directory in `/Mods/` with your mod name
2. Add a `mod.json` manifest
3. Add your `.csx` script files
4. Start the game
5. Check logs for mod loading messages

## 📚 Example Mods

See `/Mods/example-mod/` for a complete working example.

## 🤝 Contributing

Share your mods with the community! Consider:
- Documenting your mod's features
- Providing example usage
- Following semantic versioning
- Testing with different dependency configurations

---

**Need help?** Check the main documentation at `/docs/scripting/`
