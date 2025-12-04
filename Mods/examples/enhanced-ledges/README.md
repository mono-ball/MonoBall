# Enhanced Ledges Mod

Enhanced ledge mechanics demonstrating tile behavior extension, custom events, and mod composition patterns.

## Features

### 🪨 Crumbling Ledges
- Ledges crumble after being jumped over 3 times (configurable)
- Visual crack progression shows durability
- Crumbled ledges become completely impassable
- Triggers `LedgeCrumbledEvent` for other mods to react

### 🚀 Jump Boost Item
- Consumable item that increases jump height by 2x
- Default duration: 30 seconds
- Allows jumping over multiple tiles or special obstacles
- Publishes `JumpBoostActivatedEvent` when consumed

### 📊 Achievement System
- **First Jump**: Complete your first ledge jump
- **Ledge Enthusiast**: Jump over 10 ledges
- **Jump Master**: Jump over 50 ledges
- **Ledge Legend**: Jump over 100 ledges
- **Boosted**: Perform a jump with boost active
- **Survivor**: Be standing on a ledge when it crumbles

### 🎨 Visual Effects
- Parabolic jump arc animations
- Dust particle effects on landing
- Progressive crack visuals on ledges
- Dramatic crumble animation with falling debris
- Boost glow and aura effects
- Camera shake on crumble

## Custom Events

This mod introduces three new events that other mods can subscribe to:

### `LedgeJumpedEvent`
```csharp
public sealed record LedgeJumpedEvent : IGameEvent
{
    Entity Entity;          // Entity that jumped
    int Direction;          // Jump direction (0-3)
    float JumpHeight;       // Jump multiplier (1.0 = normal)
    int TileX, TileY;       // Location of jump
    bool IsBoosted;         // Whether boost was active
}
```

### `LedgeCrumbledEvent`
```csharp
public sealed record LedgeCrumbledEvent : IGameEvent
{
    int TileX, TileY;       // Location of crumbled ledge
    bool WasPlayerOn;       // Player was on tile
    int LedgeDirection;     // Direction ledge faced
    int TotalJumps;         // Jumps before crumble
}
```

### `JumpBoostActivatedEvent`
```csharp
public sealed record JumpBoostActivatedEvent : IGameEvent
{
    Entity Entity;          // Entity receiving boost
    float BoostMultiplier;  // Jump height multiplier
    float DurationSeconds;  // Effect duration
    string BoostSource;     // Source of boost
    DateTime ExpiresAt;     // When effect expires
}
```

## Configuration

### Crumbling Ledges
Configure via tile properties in map editor:
```json
{
  "max_jumps": 3,           // Jumps before crumbling (default: 3)
  "direction": 3,           // 0=South, 1=West, 2=East, 3=North
  "show_cracks": true       // Show visual crack progression
}
```

### Jump Boost Item
Configure via item properties:
```json
{
  "boost_multiplier": 2.0,  // Jump height multiplier (default: 2.0)
  "duration_seconds": 30.0, // Effect duration (default: 30)
  "consumable": true        // Item consumed on use (default: true)
}
```

## Installation

1. Copy the `enhanced-ledges` folder to `/Mods/examples/`
2. The mod will be auto-loaded on game start
3. Scripts are hot-reloadable during development

## File Structure

```
enhanced-ledges/
├── mod.json                    # Mod manifest
├── events/
│   └── LedgeEvents.csx        # Custom event definitions
├── ledge_crumble.csx          # Crumbling ledge behavior
├── jump_boost_item.csx        # Jump boost item logic
├── ledge_jump_tracker.csx     # Achievement tracking
├── visual_effects.csx         # Visual/audio effects
└── README.md                   # This file
```

## How Crumbling Works

1. **Normal State**: Ledge functions like standard jump ledge
2. **First Jump**: Jump count incremented, small cracks appear
3. **Second Jump**: Jump count incremented, larger cracks appear
4. **Third Jump**: Jump count reaches maximum, ledge marked for crumbling
5. **Crumble Trigger**: When player steps off, ledge crumbles completely
6. **Crumbled State**: Ledge blocks all movement, shows crumbled visual

The crumble uses **per-tile state persistence**, so each ledge tracks its own durability independently across game sessions.

## Achievement Tracking

The tracker subscribes to all ledge events and maintains statistics:

```csharp
// Subscribe to jump events
On<LedgeJumpedEvent>((evt) => {
    IncrementJumpCount();
    CheckAchievements();
});

// Subscribe to crumble events
On<LedgeCrumbledEvent>((evt) => {
    if (evt.WasPlayerOn) {
        AwardSurvivorAchievement();
    }
});
```

## Integration with Other Mods

Other mods can react to ledge events:

```csharp
// Example: Quest mod tracking ledge jumps
On<LedgeJumpedEvent>((evt) => {
    if (currentQuest == "Jump Master Challenge") {
        UpdateQuestProgress();
    }
});

// Example: Environmental mod preventing crumbling in rain
On<LedgeCrumbledEvent>((evt) => {
    if (IsRaining()) {
        evt.PreventDefault("Wet ledges don't crumble!");
    }
});
```

## Development Notes

### Demonstrates
- ✅ Extending core tile behaviors (ledges)
- ✅ Custom event publication and subscription
- ✅ Per-tile state persistence
- ✅ Item interaction patterns
- ✅ Achievement/progression systems
- ✅ Visual/audio effect coordination
- ✅ Mod composition via events

### Best Practices Shown
- Event-driven communication between scripts
- State management for tile durability
- Configuration via tile/item properties
- Logging at appropriate levels
- Clean separation of concerns
- Documentation of all events

## Troubleshooting

**Ledges crumble too quickly**: Increase `max_jumps` in tile properties
**Boost not working**: Check item configuration and event subscriptions
**Achievements not unlocking**: Verify state persistence is enabled
**Visual effects missing**: Check logger output for effect system errors

## Performance

- Events are lightweight records (no heavy objects)
- State queries are cached per tile
- Visual effects are pooled and reused
- Achievement checks only run on threshold values

## License

Part of MonoBall Framework examples - free to use and modify for learning purposes.
