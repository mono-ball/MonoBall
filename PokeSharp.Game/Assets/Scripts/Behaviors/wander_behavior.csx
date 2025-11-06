using Arch.Core;
using Microsoft.Xna.Framework;
using PokeSharp.Core.Components;
using PokeSharp.Scripting;

/// <summary>
/// Wander behavior - NPC moves randomly.
/// Uses WanderState component for timing.
/// </summary>
public class WanderBehavior : TypeScriptBase
{
    public override void OnActivated(ScriptContext ctx)
    {
        ctx.World.Add(
            ctx.Entity.Value,
            new WanderState
            {
                MoveTimer = TypeScriptBase.Random() * 3.0f,
                MinWaitTime = 1.0f,
                MaxWaitTime = 4.0f,
                Speed = 2.0f,
                MovementCount = 0,
            }
        );

        ref var position = ref ctx.Position;
        ctx.Logger.LogInformation(
            "Wander behavior activated for entity at ({X}, {Y})",
            position.X,
            position.Y
        );
    }

    public override void OnTick(ScriptContext ctx, float deltaTime)
    {
        ref var state = ref ctx.GetState<WanderState>();
        ref var position = ref ctx.Position;

        state.MoveTimer -= deltaTime;

        if (state.MoveTimer <= 0)
        {
            // Pick random direction
            var directions = new[]
            {
                Direction.North,
                Direction.South,
                Direction.East,
                Direction.West,
            };
            var randomDir = directions[TypeScriptBase.RandomRange(0, directions.Length)];

            ctx.World.Add(ctx.Entity.Value, new MovementRequest(randomDir));

            // Next movement in 1-4 seconds
            state.MoveTimer =
                TypeScriptBase.Random() * (state.MaxWaitTime - state.MinWaitTime)
                + state.MinWaitTime;
            state.MovementCount++;

            ctx.Logger.LogTrace(
                "Wandering {Direction} from ({X}, {Y}) - Movement #{Count}",
                randomDir,
                position.X,
                position.Y,
                state.MovementCount
            );
        }
    }

    public override void OnDeactivated(ScriptContext ctx)
    {
        ref var state = ref ctx.GetState<WanderState>();
        ctx.Logger.LogInformation(
            "Wander behavior deactivated after {Count} movements",
            state.MovementCount
        );

        // Clean up wander state component
        if (ctx.World.Has<WanderState>(ctx.Entity.Value))
        {
            ctx.World.Remove<WanderState>(ctx.Entity.Value);
        }
    }
}

// Component to store wander-specific state
public struct WanderState
{
    public float MoveTimer;
    public float MinWaitTime;
    public float MaxWaitTime;
    public float Speed;
    public int MovementCount;
}

return new WanderBehavior();
