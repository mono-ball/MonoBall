# MonoBall Framework Mod Templates

This folder contains production-ready templates for creating mods in MonoBall Framework. Each template is fully documented with TODO comments and examples to help you get started quickly.

## 📋 Available Templates

### 1. **template_tile_behavior.csx**
Template for creating custom tile behaviors (tall grass, warp tiles, special effects).

**Use Cases:**
- Trigger random encounters
- Apply status effects (poison tiles, healing tiles)
- Create warp points and map transitions
- Play animations or sounds when stepped on
- Grant items or experience
- Unlock areas based on conditions

**Key Features:**
- Event subscriptions (TileSteppedOn, TileSteppedOff)
- Position-filtered event handling (OnTile)
- Cancellable events for blocking tile entry
- State management examples
- Cooldown timer patterns

**Getting Started:**
1. Copy `template_tile_behavior.csx` to your mod's `Scripts/` folder
2. Rename the class (e.g., `LavaTileBehavior`)
3. Update TODO sections with your custom logic
4. Set tile type identifiers in your event handlers
5. Test with different entity types

---

### 2. **template_npc_behavior.csx**
Template for creating NPC behaviors (patrol, wander, dialogue, battles).

**Use Cases:**
- NPCs that patrol between waypoints
- Wandering NPCs with random movement
- Stationary NPCs (shopkeepers, guards)
- Trainer battles with line-of-sight detection
- Quest givers with dialogue progression
- NPCs with custom schedules

**Key Features:**
- Three movement patterns (patrol, wander, stationary)
- Movement state management
- Interaction handling
- Line-of-sight detection for trainer battles
- Dialogue progression system
- Per-entity state components

**Getting Started:**
1. Copy `template_npc_behavior.csx` to your mod's `Scripts/` folder
2. Rename the class (e.g., `VendorBehavior`)
3. Choose a movement pattern (or create your own)
4. Add interaction logic (dialogue, battles, trading)
5. Configure waypoints or behavior parameters

---

### 3. **template_item_script.csx**
Template for creating custom items (healing items, stat boosters, key items).

**Use Cases:**
- Healing items (potions, medicines)
- Status cure items (antidote, awakening)
- Stat boosters (rare candy, protein)
- Key items (HMs, event-specific items)
- Battle items (pokéballs, X Attack)
- Evolution items (stones, held items)

**Key Features:**
- Item usage event handling
- Consumable vs permanent item logic
- Context-based usage restrictions
- Effect application patterns
- Inventory integration examples
- Category-based organization (Medicine, Battle, etc.)

**Getting Started:**
1. Copy `template_item_script.csx` to your mod's `Scripts/` folder
2. Rename the class (e.g., `CustomPotionScript`)
3. Set item category and consumable flag
4. Implement usage validation logic
5. Define item effects (healing, stat boost, etc.)

---

### 4. **template_custom_event.csx**
Template for defining custom events and inter-mod communication.

**Use Cases:**
- Quest systems (QuestCompleted, QuestProgress)
- Custom battle systems
- Time/weather systems
- Economy and shop systems
- Achievement systems
- Mod-to-mod communication

**Key Features:**
- Basic event definitions (IGameEvent)
- Cancellable event definitions (ICancellableEvent)
- Entity-filtered events (IEntityEvent)
- Tile-filtered events (ITileEvent)
- Event publisher patterns
- Event subscriber patterns
- Inter-mod API examples

**Getting Started:**
1. Copy `template_custom_event.csx` to your mod's `Scripts/` folder
2. Define your custom event types (at the top of the file)
3. Create publisher scripts to raise events
4. Create subscriber scripts to listen to events
5. Use events for cross-mod communication

---

### 5. **template_mod_manifest.json**
Template for the mod manifest file that defines mod metadata and configuration.

**Key Sections:**
- Basic metadata (id, name, version, author)
- Dependencies and incompatibilities
- Script loading configuration
- Asset paths (sprites, audio, maps)
- Data files (Pokemon, items, moves)
- Permission requirements
- User configuration options

**Getting Started:**
1. Copy `template_mod_manifest.json` to your mod's root folder
2. Rename to `manifest.json`
3. Remove all `_comment` fields (they're documentation only)
4. Fill in your mod's information
5. List all scripts, assets, and data files

---

## 🚀 Quick Start Guide

### Creating Your First Mod

1. **Create Mod Structure:**
   ```
   MyMod/
   ├── manifest.json          (Copy from template_mod_manifest.json)
   ├── Scripts/
   │   ├── my_tile_behavior.csx
   │   └── my_npc_behavior.csx
   ├── Assets/
   │   ├── Sprites/
   │   ├── Audio/
   │   └── Maps/
   └── Data/
       ├── Pokemon/
       ├── Items/
       └── Moves/
   ```

2. **Copy Templates:**
   ```bash
   # Copy the templates you need
   cp template_tile_behavior.csx MyMod/Scripts/my_tile_behavior.csx
   cp template_npc_behavior.csx MyMod/Scripts/my_npc_behavior.csx
   cp template_mod_manifest.json MyMod/manifest.json
   ```

3. **Customize Templates:**
   - Open each template and search for `TODO` comments
   - Replace class names to match your mod
   - Implement custom logic in the TODO sections
   - Remove unused code sections

4. **Update Manifest:**
   - Edit `manifest.json` with your mod details
   - List all scripts in the `scripts` array
   - Remove `_comment` fields
   - Validate JSON syntax

5. **Test Your Mod:**
   - Place your mod folder in `MonoBall Framework/Mods/`
   - Launch the game and check the console for errors
   - Test all features thoroughly
   - Check for hot-reload support during development

---

## 📖 Template Customization Guide

### Understanding the ScriptBase Lifecycle

All script templates extend `ScriptBase`, which follows this lifecycle:

1. **Initialize(ctx)** - Called once when script loads
   - Set up default state
   - Cache references
   - Initialize configuration

2. **RegisterEventHandlers(ctx)** - Called after Initialize
   - Subscribe to game events using `On<TEvent>()`
   - Set up filtered subscriptions (`OnEntity`, `OnTile`)
   - Configure event priorities

3. **(Script runs, handlers execute)**
   - Event handlers run when events are published
   - State is managed via `Get<T>()` and `Set<T>()`
   - Scripts can publish custom events via `Publish<T>()`

4. **OnUnload()** - Called when script is unloaded
   - Clean up resources
   - Remove state components
   - Event subscriptions are auto-cleaned

### Event Subscription Patterns

```csharp
// Basic event subscription
On<TileSteppedOnEvent>(evt => {
    // Handle all tile step events
});

// High-priority subscription (for validation)
On<TileSteppedOnEvent>(evt => {
    if (shouldBlock) {
        evt.PreventDefault("Blocked!");
    }
}, priority: 1000);

// Entity-filtered subscription
OnEntity<MovementCompletedEvent>(playerEntity, evt => {
    // Only fires for the player entity
});

// Tile-filtered subscription
OnTile<TileSteppedOnEvent>(new Vector2(10, 15), evt => {
    // Only fires at coordinates (10, 15)
});
```

### State Management Best Practices

```csharp
// Use component types for state (preferred)
public struct MyCustomState
{
    public float Timer;
    public int Counter;
    public bool HasTriggered;
}

// Initialize state on first use
if (!Context.HasState<MyCustomState>())
{
    Context.World.Add(Context.Entity.Value, new MyCustomState
    {
        Timer = 0f,
        Counter = 0,
        HasTriggered = false
    });
}

// Access state (by reference for performance)
ref var state = ref Context.GetState<MyCustomState>();
state.Counter++;

// Clean up on unload
if (Context.HasState<MyCustomState>())
{
    Context.RemoveState<MyCustomState>();
}
```

### Publishing Custom Events

```csharp
// Define custom event type
public sealed record MyCustomEvent : IGameEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public required Entity SourceEntity { get; init; }
    public string Message { get; init; }
}

// Publish the event
Publish(new MyCustomEvent
{
    SourceEntity = Context.Entity.Value,
    Message = "Something happened!"
});
```

---

## 🎯 Common Patterns and Examples

### Pattern: Cooldown Timer
```csharp
// In RegisterEventHandlers
On<TickEvent>(evt =>
{
    var cooldown = Get<float>("cooldown_timer", 0f);
    if (cooldown > 0)
    {
        Set("cooldown_timer", cooldown - evt.DeltaTime);
    }
});

// When triggering an action
if (Get<float>("cooldown_timer", 0f) <= 0)
{
    TriggerAction();
    Set("cooldown_timer", 5.0f); // 5 second cooldown
}
```

### Pattern: Random Probability
```csharp
// Use GameState.Random() for consistent RNG
var encounterRate = 0.1f; // 10% chance
if (Context.GameState.Random() < encounterRate)
{
    TriggerEncounter();
}
```

### Pattern: Distance Check
```csharp
ref var playerPos = ref Context.World.Get<Position>(playerEntity);
ref var npcPos = ref Context.Position;

var distance = Math.Abs(playerPos.X - npcPos.X) + Math.Abs(playerPos.Y - npcPos.Y);
if (distance <= 2)
{
    // Player is within 2 tiles
}
```

### Pattern: Direction Calculation
```csharp
var direction = Context.Map.GetDirectionTo(
    fromX, fromY,
    toX, toY
);
```

---

## 🛠️ Where to Place Files

### Script Files (.csx)
- Location: `YourMod/Scripts/`
- Reference in manifest: `"scripts": [{"path": "Scripts/your_script.csx"}]`
- Automatically compiled on load
- Support hot-reload during development

### Manifest File (manifest.json)
- Location: `YourMod/manifest.json` (root of mod folder)
- Required for all mods
- Must be valid JSON (no trailing commas!)
- Remove all `_comment` fields before release

### Asset Files
- Sprites: `YourMod/Assets/Sprites/`
- Audio: `YourMod/Assets/Audio/`
- Maps: `YourMod/Assets/Maps/`
- Reference in manifest `assets` section

### Data Files
- Pokemon: `YourMod/Data/Pokemon/`
- Items: `YourMod/Data/Items/`
- Moves: `YourMod/Data/Moves/`
- Reference in manifest `data` section

---

## ⚠️ Important Notes

### Template Compilation
- All templates must compile without errors
- Test each template before distribution
- Use `Context.Logger` for debugging output
- Watch console for error messages

### Performance Tips
- Don't publish events every frame unless necessary
- Use filtered subscriptions (`OnEntity`, `OnTile`) when possible
- Keep event handlers fast and simple
- Use state components instead of dictionaries for performance

### Compatibility
- Check game version compatibility (`game_version` in manifest)
- List dependencies and incompatibilities explicitly
- Test with hot-reload enabled
- Verify script compilation on game start

### Best Practices
- Use meaningful variable and class names
- Document your custom events for other modders
- Keep scripts focused on a single responsibility
- Use TODO comments for customization points
- Test edge cases and error conditions

---

## 📚 Additional Resources

### Documentation
- [Full Modding Guide](../docs/MODDING-GUIDE.md) (if available)
- [ScriptBase API Reference](../docs/API-REFERENCE.md) (if available)
- [Event System Architecture](../docs/IMPLEMENTATION-ROADMAP.md)

### Example Mods
- Check `MonoBall Framework.Game/Assets/Scripts/Behaviors/` for built-in examples:
  - `wander_behavior.csx` - Example NPC wandering
  - `guard_behavior.csx` - Example stationary NPC
  - `patrol_behavior.csx` - Example patrol pattern

### Getting Help
- Check the game console for error messages
- Review the implementation roadmap for planned features
- Look at existing behavior scripts for patterns
- Test incrementally as you build

---

## 🎉 Happy Modding!

These templates are designed to get you started quickly. Don't hesitate to experiment and create unique mechanics. The modding system is event-driven and flexible, allowing for complex interactions between mods.

Remember to:
- Test thoroughly before release
- Document your mod's features
- Share your creations with the community
- Report bugs and suggest improvements

Good luck with your mod development!
