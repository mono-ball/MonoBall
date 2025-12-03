using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Scripting.Runtime;
using PokeSharp.Engine.Core.Events.Movement;
using PokeSharp.Engine.Core.Events.Tile;
using EnhancedLedges.Events;

/// <summary>
///     Crumbling ledge behavior - extends jump ledges with durability.
///     Ledges crumble after being jumped over 3 times, becoming impassable.
///     Demonstrates state persistence per tile and custom event publishing.
/// </summary>
/// <remarks>
///     Configuration via TileData properties:
///     - "max_jumps": Maximum jumps before crumbling (default: 3)
///     - "direction": Ledge direction 0=South, 1=West, 2=East, 3=North
///     - "show_cracks": Whether to show visual cracks (default: true)
/// </remarks>
public class CrumbleLedgeBehavior : ScriptBase
{
    private const string STATE_JUMP_COUNT = "jump_count";
    private const string STATE_CRUMBLED = "crumbled";
    private const int DEFAULT_MAX_JUMPS = 3;

    private int _maxJumps;
    private int _ledgeDirection;
    private bool _showCracks;

    public override void Initialize(ScriptContext ctx)
    {
        base.Initialize(ctx);

        // Read configuration from TileData properties
        _maxJumps = ctx.TileData.GetIntProperty("max_jumps", DEFAULT_MAX_JUMPS);
        _ledgeDirection = ctx.TileData.GetIntProperty("direction", 3); // Default: North
        _showCracks = ctx.TileData.GetBoolProperty("show_cracks", true);

        // Initialize state if not already set
        if (!ctx.State.HasKey(STATE_JUMP_COUNT))
        {
            ctx.State.SetInt(STATE_JUMP_COUNT, 0);
        }

        if (!ctx.State.HasKey(STATE_CRUMBLED))
        {
            ctx.State.SetBool(STATE_CRUMBLED, false);
        }

        Context.Logger.LogInformation("Crumble ledge initialized: max={MaxJumps} jumps, direction={Direction}", _maxJumps, _ledgeDirection);
    }

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // Check if already crumbled - if so, block all movement
        On<MovementStartedEvent>((evt) =>
        {
            var isCrumbled = Context.State.GetBool(STATE_CRUMBLED);
            if (isCrumbled)
            {
                Context.Logger.LogDebug("Crumbled ledge: Blocking all movement");
                evt.PreventDefault("This ledge has crumbled away");
                return;
            }

            // Block movement in the opposite direction (can't climb up ledge)
            var oppositeDirection = GetOppositeDirection(_ledgeDirection);
            if (evt.Direction == oppositeDirection)
            {
                Context.Logger.LogDebug($"Crumble ledge: Blocking opposite direction movement (dir={oppositeDirection})");
                evt.PreventDefault("Can't climb up the crumbled ledge");
            }
        });

        // Handle successful jump and update durability
        On<MovementCompletedEvent>((evt) =>
        {
            // Only process if moving in the ledge direction
            if (evt.Direction != _ledgeDirection)
            {
                return;
            }

            var isCrumbled = Context.State.GetBool(STATE_CRUMBLED);
            if (isCrumbled)
            {
                return;
            }

            // Increment jump count
            var jumpCount = Context.State.GetInt(STATE_JUMP_COUNT);
            jumpCount++;
            Context.State.SetInt(STATE_JUMP_COUNT, jumpCount);

            Context.Logger.LogInformation("Ledge jumped: {JumpCount}/{MaxJumps} times", jumpCount, _maxJumps);

            // Publish jump event
            Context.Events.Publish(new LedgeJumpedEvent
            {
                Entity = evt.Entity,
                Direction = _ledgeDirection,
                JumpHeight = 1.0f,
                TileX = Context.TileData.X,
                TileY = Context.TileData.Y,
                IsBoosted = false
            });

            // Show crack visual if enabled
            if (_showCracks && jumpCount < _maxJumps)
            {
                var crackLevel = (float)jumpCount / _maxJumps;
                Context.Logger.LogDebug($"Showing crack visual: level={crackLevel:F2}");
                // Visual system would handle crack rendering based on crack level
            }

            // Check if should crumble
            if (jumpCount >= _maxJumps)
            {
                CrumbleLedge(evt.Entity);
            }
        });

        // Handle entity stepping off - check for crumble underneath player
        On<TileSteppedOffEvent>((evt) =>
        {
            var isCrumbled = Context.State.GetBool(STATE_CRUMBLED);
            var jumpCount = Context.State.GetInt(STATE_JUMP_COUNT);

            // If at max jumps but not yet crumbled, crumble as player leaves
            if (!isCrumbled && jumpCount >= _maxJumps)
            {
                Context.Logger.LogInformation("Ledge crumbling as player steps off");
                CrumbleLedge(evt.Entity);
            }
        });
    }

    private void CrumbleLedge(Entity entity)
    {
        // Mark as crumbled
        Context.State.SetBool(STATE_CRUMBLED, true);

        var jumpCount = Context.State.GetInt(STATE_JUMP_COUNT);

        Context.Logger.LogWarning("Ledge crumbled after {JumpCount} jumps at ({X}, {Y})", jumpCount, Context.TileData.X, Context.TileData.Y);

        // Publish crumble event
        Context.Events.Publish(new LedgeCrumbledEvent
        {
            TileX = Context.TileData.X,
            TileY = Context.TileData.Y,
            WasPlayerOn = entity.IsAlive(), // Check if entity is still valid
            LedgeDirection = _ledgeDirection,
            TotalJumps = jumpCount
        });

        // Update tile appearance to show crumbled state
        // Visual system would handle rendering the crumbled ledge
        Context.Logger.LogDebug("Crumble animation triggered");
    }

    private int GetOppositeDirection(int direction)
    {
        // Direction values: 0=South, 1=West, 2=East, 3=North
        return direction switch
        {
            0 => 3, // South -> North
            1 => 2, // West -> East
            2 => 1, // East -> West
            3 => 0, // North -> South
            _ => direction
        };
    }
}

return new CrumbleLedgeBehavior();
