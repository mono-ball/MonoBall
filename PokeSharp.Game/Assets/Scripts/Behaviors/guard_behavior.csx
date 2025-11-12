using Arch.Core;
using Microsoft.Xna.Framework;
using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Scripting.Runtime;

/// <summary>
/// Guard behavior - NPC stays at position, scans for threats.
/// Uses GuardState component for per-entity state.
/// </summary>
public class GuardBehavior : TypeScriptBase
{
    public override void OnActivated(ScriptContext ctx)
    {
        ref var position = ref ctx.Position;

        ctx.World.Add(
            ctx.Entity.Value,
            new GuardState
            {
                GuardPosition = new Point(position.X, position.Y),
                FacingDirection = Direction.South,
                ScanTimer = 0f,
                ScanInterval = 2.0f,
            }
        );

        ctx.Logger.LogInformation("Guard activated at position ({X}, {Y})", position.X, position.Y);
    }

    public override void OnTick(ScriptContext ctx, float deltaTime)
    {
        ref var state = ref ctx.GetState<GuardState>();
        ref var position = ref ctx.Position;

        // Return to guard position if moved
        if (position.X != state.GuardPosition.X || position.Y != state.GuardPosition.Y)
        {
            var dir = ctx.Map.GetDirectionTo(
                position.X,
                position.Y,
                state.GuardPosition.X,
                state.GuardPosition.Y
            );

            // Use component pooling: reuse existing component or add new one
            if (ctx.World.Has<MovementRequest>(ctx.Entity.Value))
            {
                ref var request = ref ctx.World.Get<MovementRequest>(ctx.Entity.Value);
                request.Direction = dir;
                request.Active = true;
            }
            else
            {
                ctx.World.Add(ctx.Entity.Value, new MovementRequest(dir));
            }

            ctx.Logger.LogDebug(
                "Guard returning to post from ({X}, {Y}) to ({GuardX}, {GuardY})",
                position.X,
                position.Y,
                state.GuardPosition.X,
                state.GuardPosition.Y
            );
            return;
        }

        // Rotate scan direction periodically
        state.ScanTimer -= deltaTime;
        if (state.ScanTimer <= 0)
        {
            state.FacingDirection = RotateDirection(state.FacingDirection);
            state.ScanTimer = state.ScanInterval;

            // Update NPC facing direction (if we had that component)
            ctx.Logger.LogTrace("Guard facing {Direction}", state.FacingDirection);
        }
    }

    public override void OnDeactivated(ScriptContext ctx)
    {
        ctx.Logger.LogInformation("Guard deactivated");
        // Remove guard state component when script is deactivated
        if (ctx.World.Has<GuardState>(ctx.Entity.Value))
        {
            ctx.World.Remove<GuardState>(ctx.Entity.Value);
        }
    }

    private static Direction RotateDirection(Direction current)
    {
        return current switch
        {
            Direction.North => Direction.East,
            Direction.East => Direction.South,
            Direction.South => Direction.West,
            Direction.West => Direction.North,
            _ => Direction.North,
        };
    }
}

// Component to store guard-specific state
public struct GuardState
{
    public Point GuardPosition;
    public Direction FacingDirection;
    public float ScanTimer;
    public float ScanInterval;
}

return new GuardBehavior();
