// EVENT-DRIVEN TILE BEHAVIOR SCRIPT BASE
// Modern scripting interface using events instead of virtual methods.

using Arch.Core;
using PokeSharp.Engine.Events;
using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Scripting.Runtime;

namespace PokeSharp.Game.Scripting.EventDriven;

/// <summary>
/// Modern event-driven base class for tile behavior scripts.
/// Uses event subscriptions instead of virtual methods for better composability.
/// </summary>
/// <remarks>
/// <para>
/// NEW APPROACH: Subscribe to events in Initialize() instead of overriding virtual methods.
/// This allows:
/// - Multiple behaviors per tile (via composition)
/// - Priority-based execution
/// - Mod injection without inheritance
/// - Cleaner separation of concerns
/// </para>
/// <para>
/// MIGRATION: Existing TileBehaviorScriptBase scripts can be converted by:
/// 1. Change base class to EventDrivenScriptBase
/// 2. Move virtual method code to event handlers
/// 3. Subscribe to events in Initialize()
/// </para>
/// </remarks>
public abstract class EventDrivenScriptBase : TypeScriptBase
{
    protected EventBus Events { get; private set; } = null!;
    protected Entity TileEntity { get; private set; }

    private readonly List<EventSubscription> _subscriptions = new();

    /// <summary>
    /// Called when script is initialized for a tile entity.
    /// Subscribe to events here.
    /// </summary>
    public virtual void Initialize(EventBus events, Entity tileEntity, ScriptContext context)
    {
        Events = events;
        TileEntity = tileEntity;

        // Override in derived classes to subscribe to events
        RegisterEventHandlers(context);
    }

    /// <summary>
    /// Override to register event handlers for this behavior.
    /// </summary>
    protected abstract void RegisterEventHandlers(ScriptContext context);

    /// <summary>
    /// Helper method to subscribe to collision checks.
    /// </summary>
    protected void OnCollisionCheck(EventHandler<CollisionCheckEvent> handler, int priority = 0)
    {
        var sub = Events.Subscribe<CollisionCheckEvent>(handler, priority);
        _subscriptions.Add(sub);
    }

    /// <summary>
    /// Helper method to subscribe to tile steps.
    /// </summary>
    protected void OnTileStep(EventHandler<TileSteppedEvent> handler, int priority = 0)
    {
        var sub = Events.Subscribe<TileSteppedEvent>(handler, priority);
        _subscriptions.Add(sub);
    }

    /// <summary>
    /// Helper method to subscribe to forced movement checks.
    /// </summary>
    protected void OnForcedMovementCheck(EventHandler<ForcedMovementCheckEvent> handler, int priority = 0)
    {
        var sub = Events.Subscribe<ForcedMovementCheckEvent>(handler, priority);
        _subscriptions.Add(sub);
    }

    /// <summary>
    /// Helper method to subscribe to jump checks.
    /// </summary>
    protected void OnJumpCheck(EventHandler<JumpCheckEvent> handler, int priority = 0)
    {
        var sub = Events.Subscribe<JumpCheckEvent>(handler, priority);
        _subscriptions.Add(sub);
    }

    /// <summary>
    /// Cleanup subscriptions when script is disposed.
    /// </summary>
    public virtual void Dispose()
    {
        foreach (var sub in _subscriptions)
        {
            Events.Unsubscribe(sub);
        }
        _subscriptions.Clear();
    }
}

/// <summary>
/// Example: Ice tile behavior using event-driven approach.
/// </summary>
public class IceTileBehavior : EventDrivenScriptBase
{
    protected override void RegisterEventHandlers(ScriptContext context)
    {
        // Force continued movement in same direction
        OnForcedMovementCheck((ref ForcedMovementCheckEvent evt) =>
        {
            // Only apply to entities on THIS tile
            if (evt.TileEntity != TileEntity) return;

            // Continue moving in current direction
            if (evt.CurrentDirection != Direction.None)
            {
                evt.ForcedDirection = evt.CurrentDirection;
            }
        });

        // Optional: Play slide sound when stepping on ice
        OnTileStep((ref TileSteppedEvent evt) =>
        {
            if (evt.TileEntity != TileEntity) return;

            // Play ice slide sound effect
            // SoundEffects.Play("ice_slide");
        });
    }
}

/// <summary>
/// Example: Ledge/Jump tile behavior.
/// </summary>
public class LedgeTileBehavior : EventDrivenScriptBase
{
    private Direction _jumpDirection = Direction.South;

    protected override void RegisterEventHandlers(ScriptContext context)
    {
        // Get jump direction from tile properties
        if (context.TryGetProperty("jumpDirection", out string? dirStr))
        {
            _jumpDirection = Enum.Parse<Direction>(dirStr);
        }

        // Allow jumping from specific direction
        OnJumpCheck((ref JumpCheckEvent evt) =>
        {
            if (evt.TileEntity != TileEntity) return;

            // Only allow jumping when approaching from opposite direction
            if (evt.FromDirection == _jumpDirection.Opposite())
            {
                evt.JumpDirection = _jumpDirection;
                evt.JumpDistance = 2; // Standard jump distance
            }
        });

        // Block movement in other directions
        OnCollisionCheck((ref CollisionCheckEvent evt) =>
        {
            if (evt.TileEntity != TileEntity) return;

            // Block if not jumping
            if (evt.FromDirection != _jumpDirection.Opposite())
            {
                evt.IsWalkable = false;
                evt.CancellationReason = "Can only jump from this direction";
            }
        });
    }
}

/// <summary>
/// Example: Spinning arrow tile (conveyor belt).
/// </summary>
public class SpinningArrowBehavior : EventDrivenScriptBase
{
    private Direction _arrowDirection = Direction.North;

    protected override void RegisterEventHandlers(ScriptContext context)
    {
        // Get arrow direction from tile properties
        if (context.TryGetProperty("arrowDirection", out string? dirStr))
        {
            _arrowDirection = Enum.Parse<Direction>(dirStr);
        }

        // Force movement in arrow direction
        OnForcedMovementCheck((ref ForcedMovementCheckEvent evt) =>
        {
            if (evt.TileEntity != TileEntity) return;

            // Always move in arrow direction
            evt.ForcedDirection = _arrowDirection;
        });

        // Play conveyor sound
        OnTileStep((ref TileSteppedEvent evt) =>
        {
            if (evt.TileEntity != TileEntity) return;

            // SoundEffects.Play("conveyor");
        });
    }
}

/// <summary>
/// Example: Water tile requiring Surf ability.
/// </summary>
public class WaterTileBehavior : EventDrivenScriptBase
{
    protected override void RegisterEventHandlers(ScriptContext context)
    {
        // Block movement unless player has Surf
        OnCollisionCheck((ref CollisionCheckEvent evt) =>
        {
            if (evt.TileEntity != TileEntity) return;

            // Check if entity has Surf ability
            if (HasSurfAbility(evt.Entity))
            {
                evt.IsWalkable = true;
            }
            else
            {
                evt.IsWalkable = false;
                evt.CancellationReason = "Need Surf to cross water";
            }
        });

        // Trigger Surf animation
        OnTileStep((ref TileSteppedEvent evt) =>
        {
            if (evt.TileEntity != TileEntity) return;

            // Start Surf animation if not already surfing
            // AnimationController.StartSurf(evt.Entity);
        });
    }

    private bool HasSurfAbility(Entity entity)
    {
        // Check if entity has Surf HM/ability
        // return entity.Has<Abilities>() && entity.Get<Abilities>().Has("Surf");
        return false; // Placeholder
    }
}

/// <summary>
/// Example: Tall grass with encounter rate.
/// </summary>
public class TallGrassBehavior : EventDrivenScriptBase
{
    private float _encounterRate = 0.1f;

    protected override void RegisterEventHandlers(ScriptContext context)
    {
        // Get encounter rate from properties
        if (context.TryGetProperty("encounterRate", out float rate))
        {
            _encounterRate = rate;
        }

        // Trigger wild encounters
        OnTileStep((ref TileSteppedEvent evt) =>
        {
            if (evt.TileEntity != TileEntity) return;

            // Only trigger for player
            if (!evt.Entity.Has<PlayerTag>()) return;

            // Roll for encounter
            if (Random.Shared.NextSingle() < _encounterRate)
            {
                TriggerWildEncounter(evt.Entity, context);
            }
        });
    }

    private void TriggerWildEncounter(Entity player, ScriptContext context)
    {
        // Trigger wild encounter event
        // Events.Publish(new WildEncounterEvent { ... });
    }
}

/// <summary>
/// Example: Warp tile (doors, stairs).
/// </summary>
public class WarpTileBehavior : EventDrivenScriptBase
{
    private int _destMapId;
    private int _destX;
    private int _destY;

    protected override void RegisterEventHandlers(ScriptContext context)
    {
        // Get warp destination from properties
        _destMapId = context.GetProperty<int>("destMapId");
        _destX = context.GetProperty<int>("destX");
        _destY = context.GetProperty<int>("destY");

        // Trigger warp on step
        OnTileStep((ref TileSteppedEvent evt) =>
        {
            if (evt.TileEntity != TileEntity) return;

            // Warp entity to destination
            WarpEntity(evt.Entity);
        });
    }

    private void WarpEntity(Entity entity)
    {
        // Update entity position
        if (entity.Has<Position>())
        {
            var pos = entity.Get<Position>();
            pos.MapId = _destMapId;
            pos.X = _destX;
            pos.Y = _destY;
            entity.Set(pos);

            // Fire warp event
            // Events.Publish(new EntityWarpedEvent { ... });
        }
    }
}

/// <summary>
/// Helper extensions for Direction enum.
/// </summary>
public static class DirectionExtensions
{
    public static Direction Opposite(this Direction direction)
    {
        return direction switch
        {
            Direction.North => Direction.South,
            Direction.South => Direction.North,
            Direction.East => Direction.West,
            Direction.West => Direction.East,
            _ => Direction.None
        };
    }

    public static string ToIdleAnimation(this Direction direction)
    {
        return direction switch
        {
            Direction.North => "idle_north",
            Direction.South => "idle_south",
            Direction.East => "idle_east",
            Direction.West => "idle_west",
            _ => "idle_south"
        };
    }

    public static string ToWalkAnimation(this Direction direction)
    {
        return direction switch
        {
            Direction.North => "walk_north",
            Direction.South => "walk_south",
            Direction.East => "walk_east",
            Direction.West => "walk_west",
            _ => "walk_south"
        };
    }

    public static string ToTurnAnimation(this Direction direction)
    {
        return direction switch
        {
            Direction.North => "turn_north",
            Direction.South => "turn_south",
            Direction.East => "turn_east",
            Direction.West => "turn_west",
            _ => "turn_south"
        };
    }
}

/// <summary>
/// Placeholder for PlayerTag component.
/// </summary>
public struct PlayerTag { }

/// <summary>
/// Placeholder for Position component.
/// </summary>
public struct Position
{
    public int MapId;
    public int X;
    public int Y;
    public float PixelX;
    public float PixelY;
}
