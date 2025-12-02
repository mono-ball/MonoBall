# Modding Platform Architecture: Unified Event-Driven Scripting

**Goal**: Enable extensibility, composition, custom events, and easy script authoring for modders.

---

## 🎯 Modding Requirements

Your goals require a **fundamentally different architecture**:

1. ✅ **Composition**: Multiple mods affecting same tile/entity
2. ✅ **Simplicity**: Single base class, easy to learn
3. ✅ **Extensibility**: Users create custom events
4. ✅ **Autoloading**: Scripts load dynamically
5. ✅ **Mod Interaction**: Mods can react to each other's events

**Current architecture limitations**:
- ❌ Only 1 behavior per tile (virtual method override = no composition)
- ❌ Fixed event types (can't add custom events without engine changes)
- ❌ Separate base classes (learning curve for modders)
- ❌ Query-based (mods can't easily intercept)

**Unified event-driven solves all of this!**

---

## 🏗️ Unified Architecture for Modding

### Single Base Class

```csharp
/// <summary>
/// Universal base class for ALL scripts (tiles, NPCs, items, custom entities).
/// Event-driven, composable, and extensible.
/// </summary>
public abstract class ScriptBase
{
    protected ScriptContext Context { get; private set; }
    private readonly List<IDisposable> subscriptions = new();

    // ==================== Lifecycle ====================

    /// <summary>
    /// Called once when script is loaded. Initialize state here.
    /// </summary>
    public virtual void Initialize(ScriptContext ctx)
    {
        Context = ctx;
    }

    /// <summary>
    /// Register event handlers. Called after Initialize.
    /// </summary>
    public virtual void RegisterEventHandlers(ScriptContext ctx)
    {
        // Override to subscribe to events
    }

    /// <summary>
    /// Called when script is unloaded (hot-reload or game shutdown).
    /// Automatically cleans up event subscriptions.
    /// </summary>
    public virtual void OnUnload()
    {
        foreach (var subscription in subscriptions)
            subscription.Dispose();
        subscriptions.Clear();
    }

    // ==================== Event Subscription ====================

    /// <summary>
    /// Subscribe to any event type. Supports custom user-defined events!
    /// </summary>
    protected void On<TEvent>(Action<TEvent> handler, int priority = 500)
        where TEvent : IGameEvent
    {
        var subscription = Context.Events.Subscribe(handler, priority);
        subscriptions.Add(subscription);
    }

    /// <summary>
    /// Subscribe to event only for specific entity (filtering).
    /// </summary>
    protected void OnEntity<TEvent>(Entity entity, Action<TEvent> handler, int priority = 500)
        where TEvent : IEntityEvent
    {
        On<TEvent>(evt => {
            if (evt.Entity == entity)
                handler(evt);
        }, priority);
    }

    /// <summary>
    /// Subscribe to event only for specific tile position (filtering).
    /// </summary>
    protected void OnTile<TEvent>(Vector2 tilePos, Action<TEvent> handler, int priority = 500)
        where TEvent : ITileEvent
    {
        On<TEvent>(evt => {
            if (evt.TilePosition == tilePos)
                handler(evt);
        }, priority);
    }

    // ==================== State Management ====================

    /// <summary>
    /// Get persistent state (survives hot-reload).
    /// </summary>
    protected T Get<T>(string key, T defaultValue = default)
    {
        return Context.State.Get(key, defaultValue);
    }

    /// <summary>
    /// Set persistent state (survives hot-reload).
    /// </summary>
    protected void Set<T>(string key, T value)
    {
        Context.State.Set(key, value);
    }

    // ==================== Custom Event Publishing ====================

    /// <summary>
    /// Publish custom event. Other scripts can react!
    /// </summary>
    protected void Publish<TEvent>(TEvent evt) where TEvent : IGameEvent
    {
        Context.Events.Publish(evt);
    }
}
```

**Key Features**:
- ✅ Works for tiles, NPCs, items, entities - everything!
- ✅ Event subscription with filtering (entity-specific, tile-specific)
- ✅ State management (hot-reload safe)
- ✅ Custom event support (users can define and publish)
- ✅ Automatic cleanup on unload

---

## 🎨 Composition Example: Multiple Mods on Same Tile

**Scenario**: Ice tile with tall grass - player slides AND encounters wild Pokémon!

### Mod 1: Ice Sliding (by core game)

```csharp
// ice_slide.csx
public class IceSlide : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // Continue sliding when movement completes
        On<MovementCompletedEvent>(evt => {
            if (IsOnIceTile(evt.NewPosition))
            {
                ctx.Player.ContinueMovement(evt.Direction);
                ctx.Effects.PlaySound("ice_slide");
            }
        });
    }

    private bool IsOnIceTile(Vector2 pos) =>
        ctx.Map.GetTileAt(pos)?.HasTag("ice") ?? false;
}
```

### Mod 2: Grass Encounters (by core game)

```csharp
// tall_grass.csx
public class TallGrass : ScriptBase
{
    private Random random = new Random();

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // Check for encounters on every step
        On<TileSteppedOnEvent>(evt => {
            if (ctx.Map.GetTileAt(evt.TilePosition)?.HasTag("grass") ?? false)
            {
                if (random.NextDouble() < 0.1)
                {
                    TriggerWildEncounter(evt.Entity);
                }
            }
        });
    }

    private void TriggerWildEncounter(Entity entity)
    {
        ctx.Effects.PlaySound("wild_encounter");
        ctx.GameState.StartWildBattle("Pidgey", level: 3);
    }
}
```

### Mod 3: Ice Crack Effect (by modder!)

```csharp
// ice_crack_effect.csx (community mod)
public class IceCrackEffect : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // Add visual effect when stepping on ice
        On<TileSteppedOnEvent>(evt => {
            if (ctx.Map.GetTileAt(evt.TilePosition)?.HasTag("ice") ?? false)
            {
                // Show crack animation
                ctx.Effects.PlayEffect("ice_crack", evt.TilePosition);

                // Publish custom event so other mods can react!
                Publish(new IceCrackedEvent {
                    TilePosition = evt.TilePosition,
                    Entity = evt.Entity
                });
            }
        });
    }
}

// Custom event defined by modder
public class IceCrackedEvent : IGameEvent, ITileEvent
{
    public Vector2 TilePosition { get; init; }
    public Entity Entity { get; init; }
}
```

### Mod 4: Ice Break Mechanic (by another modder!)

```csharp
// ice_break.csx (another community mod)
public class IceBreak : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // React to IceCrackedEvent from ice_crack_effect mod!
        On<IceCrackedEvent>(evt => {
            int cracks = Get<int>($"cracks_{evt.TilePosition}", 0);
            cracks++;
            Set($"cracks_{evt.TilePosition}", cracks);

            if (cracks >= 3)
            {
                // Ice breaks after 3 cracks!
                ctx.Map.SetTileType(evt.TilePosition, TileType.Water);
                ctx.Effects.PlayEffect("ice_shatter", evt.TilePosition);
                ctx.Effects.PlaySound("ice_break");
            }
        });
    }
}
```

**Result**: All 4 scripts work together on the same tile!
- Player steps on icy grass
- Ice slide kicks in (keeps player sliding)
- Grass encounter rolls (might trigger wild Pokémon)
- Ice crack shows visual effect
- Ice break counts cracks (breaks after 3 steps)

**This is impossible with virtual method overrides!**

---

## 🔧 Custom Event Creation by Users

### Step 1: Mod Defines Custom Event

```csharp
// my_custom_events.csx (modder creates this)
public interface IWeatherEvent : IGameEvent
{
    WeatherType Weather { get; }
    float Intensity { get; }
}

public class RainStartedEvent : IWeatherEvent
{
    public WeatherType Weather => WeatherType.Rain;
    public float Intensity { get; init; }
}

public class ThunderstrikeEvent : IWeatherEvent, ITileEvent
{
    public WeatherType Weather => WeatherType.Thunderstorm;
    public float Intensity => 1.0f;
    public Vector2 TilePosition { get; init; }
}
```

### Step 2: Mod Publishes Custom Event

```csharp
// weather_system.csx (modder's weather mod)
public class WeatherSystem : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // Change weather based on time
        On<GameTimeChangedEvent>(evt => {
            if (evt.Hour == 18 && IsRainySeason())
            {
                // Publish custom event!
                Publish(new RainStartedEvent { Intensity = 0.7f });
            }
        });

        // Random thunderstrikes during storms
        On<TickEvent>(evt => {
            if (IsThunderstorm() && Random.NextDouble() < 0.01)
            {
                var pos = GetRandomTilePosition();
                Publish(new ThunderstrikeEvent { TilePosition = pos });
            }
        });
    }
}
```

### Step 3: Other Mods React to Custom Event

```csharp
// plant_growth.csx (different modder)
public class PlantGrowth : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // Plants grow faster in rain!
        On<RainStartedEvent>(evt => {
            float growthBonus = evt.Intensity * 2.0f;
            ApplyGrowthBonus(growthBonus);
        });
    }
}

// lightning_rod.csx (yet another modder)
public class LightningRod : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // Redirect lightning to rods
        On<ThunderstrikeEvent>(evt => {
            if (IsNearLightningRod(evt.TilePosition))
            {
                // Cancel the original event
                if (evt is ICancellableEvent cancel)
                    cancel.Cancel("Redirected to lightning rod");

                // Create new event at rod location
                var rodPos = GetNearestRodPosition(evt.TilePosition);
                Publish(new ThunderstrikeEvent { TilePosition = rodPos });
            }
        });
    }
}
```

**Multiple mods interact through custom events - no engine changes needed!**

---

## 🚀 Jump Script Migration (Modding-Optimized)

### Before (TileBehaviorScriptBase - No Composition)

```csharp
// jump_south.csx - Only ONE script allowed per tile
public class JumpSouthBehavior : TileBehaviorScriptBase
{
    public override bool IsBlockedFrom(ScriptContext ctx, Direction from, Direction to)
    {
        if (from == Direction.North) return true;
        return false;
    }

    public override Direction GetJumpDirection(ScriptContext ctx, Direction from)
    {
        if (from == Direction.North) return Direction.South;
        return Direction.None;
    }
}
```

**Limitations**:
- ❌ Can't add "ledge_crumble" mod (only 1 script per tile)
- ❌ Can't add "jump_boost" mod (would need to modify this script)
- ❌ Can't react to jumps in other scripts (no event)

---

### After (ScriptBase - Full Composition)

```csharp
// jump_south.csx - Multiple scripts can coexist!
public class JumpSouthBehavior : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // Block northward movement
        On<CollisionCheckEvent>(evt => {
            if (evt.FromDirection == Direction.North || evt.ToDirection == Direction.North)
            {
                evt.IsBlocked = true;
                evt.BlockReason = "Can't climb ledge";
            }
        });

        // Allow jumping south
        On<JumpCheckEvent>(evt => {
            if (evt.FromDirection == Direction.North)
            {
                evt.JumpDirection = Direction.South;
                evt.PerformJump = true;

                // Publish custom event so other mods can react!
                Publish(new LedgeJumpedEvent {
                    Entity = evt.Entity,
                    TilePosition = evt.TilePosition,
                    Direction = Direction.South
                });
            }
        });
    }
}

// Custom event for jump
public class LedgeJumpedEvent : IGameEvent, ITileEvent, IEntityEvent
{
    public Entity Entity { get; init; }
    public Vector2 TilePosition { get; init; }
    public Direction Direction { get; init; }
}
```

---

### Now Modders Can Add:

**Mod 1: Ledge Crumble** (by modder)
```csharp
// ledge_crumble.csx - Adds to jump_south behavior!
public class LedgeCrumble : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<LedgeJumpedEvent>(evt => {
            // Ledge crumbles after use
            ctx.Effects.PlayEffect("ledge_crumble", evt.TilePosition);
            ctx.Map.SetTileType(evt.TilePosition, TileType.Pit);
        });
    }
}
```

**Mod 2: Jump Boost** (by another modder)
```csharp
// jump_boost.csx - Stacks with jump_south!
public class JumpBoost : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<LedgeJumpedEvent>(evt => {
            // Jump 2 tiles instead of 1!
            if (PlayerHasJumpBoots(evt.Entity))
            {
                var extraPos = evt.TilePosition + GetDirectionOffset(evt.Direction);
                ctx.Player.MoveInstantly(extraPos);
                ctx.Effects.PlayEffect("super_jump", extraPos);
            }
        });
    }
}
```

**Mod 3: Jump Achievement** (by yet another modder)
```csharp
// jump_achievement.csx - Tracks jumps!
public class JumpAchievement : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<LedgeJumpedEvent>(evt => {
            int jumps = Get<int>("total_jumps", 0);
            jumps++;
            Set("total_jumps", jumps);

            if (jumps == 100)
            {
                ctx.GameState.UnlockAchievement("jump_master");
                ctx.Dialogue.ShowMessage("Achievement unlocked: Jump Master!");
            }
        });
    }
}
```

**All 4 scripts work together! Core game + 3 mods = seamless integration.**

---

## 📦 Autoloading Example

### Core Game Scripts (always loaded)

```
/Assets/Scripts/Core/
    ├── movement_events.csx          (publishes MovementStartedEvent, etc.)
    ├── collision_handler.csx        (handles CollisionCheckEvent)
    └── tile_interactions.csx        (publishes TileSteppedOnEvent)
```

### User Mods (autoloaded from mods folder)

```
/Mods/WeatherMod/
    ├── weather_system.csx           (defines RainStartedEvent)
    ├── rain_effects.csx             (visual rain effects)
    └── weather_pokemon.csx          (rain-exclusive Pokémon)

/Mods/EnhancedLedges/
    ├── ledge_crumble.csx            (ledges break after use)
    ├── jump_boost_item.csx          (Jump Boots item)
    └── ledge_achievements.csx       (jump tracking)
```

**All mods load automatically** - no engine modifications needed!

---

## 🔄 Migration Strategy (Optimized for Modding)

### Phase 1: Create ScriptBase (Week 1)

**Goal**: Add unified base class alongside existing system.

**Tasks**:
1. Create `ScriptBase` class (as shown above)
2. Add event types: `CollisionCheckEvent`, `JumpCheckEvent`, etc.
3. Keep `TileBehaviorScriptBase` working (backwards compatibility)
4. Test ScriptBase with 1-2 example scripts

**Result**: Both systems coexist - no breaking changes yet.

---

### Phase 2: Enable Composition (Week 2)

**Goal**: Allow multiple scripts per tile/entity.

**Current System**:
```csharp
// Only 1 behavior per tile
var behavior = tileBehaviorSystem.GetBehavior(tilePos);
bool blocked = behavior.IsBlockedFrom(from, to);
```

**New System**:
```csharp
// Multiple scripts per tile via ScriptAttachment component
var scripts = world.Query<ScriptAttachment>(tileEntity);
foreach (var scriptAttachment in scripts)
{
    var script = scriptAttachment.Script;
    // All scripts get to react via events
}

// Or simpler: Just publish event
eventBus.Publish(new CollisionCheckEvent { ... });
// All subscribed scripts react automatically!
```

**Tasks**:
1. Add `ScriptAttachment` component (links scripts to entities/tiles)
2. Modify systems to publish events instead of direct method calls
3. Allow multiple `ScriptAttachment` components per entity
4. Test with 2-3 scripts on same tile

**Result**: Composition works! Multiple mods can affect same tile.

---

### Phase 3: Migrate Core Scripts (Week 3-4)

**Goal**: Convert core tile/NPC scripts to ScriptBase.

**Priority Order**:
1. High-traffic tiles (grass, ledges, ice) - most modded
2. NPC behaviors (patrol, dialogue)
3. Special tiles (warps, puzzles)
4. Low-traffic tiles

**Automated Migration Tool**:
```bash
# Script to auto-convert TileBehaviorScriptBase → ScriptBase
npx claude-flow migrate-scripts --input Assets/Scripts/TileBehaviors --output Assets/Scripts/Unified
```

**Result**: Core game runs on unified system.

---

### Phase 4: Modder Support (Week 5)

**Goal**: Documentation and tooling for modders.

**Deliverables**:
1. **Modding Guide**: How to create scripts, define events, publish mods
2. **Event Reference**: All built-in events with examples
3. **Template Scripts**: Starter templates for common mod types
4. **Mod Validator**: Tool to check script compatibility
5. **Event Inspector**: Debug tool to view all active events/subscriptions

**Example Template**:
```csharp
// template_tile_behavior.csx
using PokeSharp.Game.Scripting.Runtime;

/// <summary>
/// TODO: Describe your tile behavior
/// </summary>
public class MyTileBehavior : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // Example: React when player steps on tile
        On<TileSteppedOnEvent>(evt => {
            if (ctx.Player.IsPlayerEntity(evt.Entity))
            {
                // TODO: Your logic here
                ctx.Dialogue.ShowMessage("You stepped on my tile!");
            }
        });
    }
}

return new MyTileBehavior();
```

---

### Phase 5: Community Testing (Week 6+)

**Goal**: Validate with real modders.

**Tasks**:
1. Release modding beta
2. Create sample mods (weather, quests, new tiles)
3. Gather feedback on ScriptBase API
4. Iterate on pain points
5. Build mod showcase gallery

---

## 🎓 Comparison: Current vs Modding-Optimized

### Current Architecture

| Feature | Support | Notes |
|---------|---------|-------|
| Multiple scripts per tile | ❌ No | Only 1 TileBehaviorScriptBase per tile |
| Custom events | ❌ No | Fixed event types in engine |
| Mod composition | ❌ No | Virtual method = only 1 implementation |
| Single base class | ❌ No | TileBehaviorScriptBase, TypeScriptBase, etc. |
| Easy for modders | ⚠️ Medium | Need to learn different base classes |
| Hot-reload | ✅ Yes | Works great |

### Unified Event-Driven Architecture

| Feature | Support | Notes |
|---------|---------|-------|
| Multiple scripts per tile | ✅ Yes | Unlimited scripts via event subscriptions |
| Custom events | ✅ Yes | Users define `IGameEvent` implementations |
| Mod composition | ✅ Yes | Event-driven = all mods react independently |
| Single base class | ✅ Yes | ScriptBase for everything |
| Easy for modders | ✅ Yes | Learn once, use everywhere |
| Hot-reload | ✅ Yes | Even better (automatic cleanup) |

---

## 💡 Key Insights

### Why Unified is Better for Modding:

1. **Composition Over Replacement**:
   - Current: Mod replaces entire tile behavior (breaks other mods)
   - Unified: Mod adds behavior via events (stacks with other mods)

2. **Custom Events Enable Mod Interaction**:
   - Current: Mods can't communicate
   - Unified: Mods publish/subscribe custom events (ecosystem emerges)

3. **Single Base Class Reduces Learning Curve**:
   - Current: "Is this a tile or NPC? Which base class?"
   - Unified: "It's a script. Use ScriptBase."

4. **Event-Driven is Natural for Mods**:
   - Current: "Override this method to change behavior"
   - Unified: "React to this event to add behavior"

5. **Extensibility Without Engine Changes**:
   - Current: New behavior = new virtual method = engine update
   - Unified: New behavior = new event = no engine changes

---

## 🚀 Recommended Path Forward

### For Your Goals (Composition + Extensibility):

**✅ MIGRATE TO UNIFIED ARCHITECTURE**

**Timeline**: 6 weeks
**Effort**: Medium (phased approach)
**Breaking Changes**: Managed (coexist during transition)
**Payoff**: MASSIVE (enables entire modding ecosystem)

### Implementation Plan:

1. **Week 1**: Create ScriptBase + core events
2. **Week 2**: Enable multi-script composition
3. **Week 3-4**: Migrate core scripts
4. **Week 5**: Modder documentation + tooling
5. **Week 6+**: Community beta testing

### Success Criteria:

- [ ] Modders can create scripts with single base class
- [ ] Multiple mods can affect same tile without conflicts
- [ ] Mods can define and publish custom events
- [ ] Scripts autoload from mods folder
- [ ] Hot-reload works for all scripts
- [ ] Event inspector shows all subscriptions
- [ ] Modding guide published
- [ ] 5+ community mods created

---

## 📚 Next Steps

1. **Review**: `/docs/scripting/unified-script-architecture.md` (full API design)
2. **Examine**: `/src/examples/unified-scripts/` (working prototypes)
3. **Decide**: Approve unified architecture for modding platform
4. **Implement**: Phase 1 (ScriptBase creation)

**The unified event-driven architecture is perfect for your modding goals!**
