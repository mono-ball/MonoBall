using Arch.Core;
using Microsoft.Xna.Framework;
using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Scripting.Runtime;

/// <summary>
/// Wander behavior - NPC moves one tile in a random direction, waits, then repeats.
/// Based on proven patrol pattern for reliability.
/// State stored in per-entity WanderState component (not instance fields).
/// </summary>
public class WanderBehavior : TypeScriptBase
{
    // NO INSTANCE FIELDS! All state in components.

    public override void OnActivated(ScriptContext ctx)
    {
        // Initialize per-entity state component
        if (!ctx.HasState<WanderState>())
        {
            ref var position = ref ctx.Position;

            ctx.World.Add(
                ctx.Entity.Value,
                new WanderState
                {
                    WaitTimer = ctx.GameState.Random() * 3.0f, // Initial wait 0-3s
                    MinWaitTime = 1.0f,
                    MaxWaitTime = 4.0f,
                    CurrentDirection = Direction.None,
                    IsMoving = false,
                    MovementCount = 0,
                    StartPosition = new Point(position.X, position.Y),
                }
            );

            ctx.Logger.LogInformation(
                "Wander initialized at ({X}, {Y}) | wait: {MinWait}-{MaxWait}s",
                position.X,
                position.Y,
                1.0f,
                4.0f
            );
        }

        ctx.Logger.LogDebug("Wander behavior activated");
    }

    public override void OnTick(ScriptContext ctx, float deltaTime)
    {
        // Get per-entity state (each NPC has its own)
        ref var state = ref ctx.GetState<WanderState>();
        ref var position = ref ctx.Position;

        // Wait before next movement
        if (state.WaitTimer > 0)
        {
            state.WaitTimer -= deltaTime;

            // Deactivate any existing MovementRequest while waiting
            if (ctx.World.Has<MovementRequest>(ctx.Entity.Value))
            {
                ref var request = ref ctx.World.Get<MovementRequest>(ctx.Entity.Value);
                request.Active = false;
            }

            return;
        }

        // If we don't have a direction yet, pick one
        if (state.CurrentDirection == Direction.None)
        {
            var directions = new[]
            {
                Direction.North,
                Direction.South,
                Direction.West,
                Direction.East,
            };
            state.CurrentDirection = directions[ctx.GameState.RandomRange(0, directions.Length)];
            state.StartPosition = new Point(position.X, position.Y);
            state.IsMoving = true;
            state.MovingTimer = 0f; // Reset timer when picking new direction
            state.MovementCount++;

            ctx.Logger.LogInformation(
                "Starting wander {Direction} from ({X}, {Y}) - Move #{Count}",
                state.CurrentDirection,
                position.X,
                position.Y,
                state.MovementCount
            );
        }

        // Track how long we've been trying to move
        if (state.IsMoving)
        {
            state.MovingTimer += deltaTime;
        }

        // Check if movement completed (reached 1 tile away OR movement system stopped us)
        var gridMovement = ctx.World.Get<GridMovement>(ctx.Entity.Value);
        var movedOneTitle =
            position.X != state.StartPosition.X ||
            position.Y != state.StartPosition.Y;

        if (state.IsMoving && !gridMovement.IsMoving && movedOneTitle)
        {
            // Successfully moved one tile - start waiting for next move
            ctx.Logger.LogInformation(
                "Wander completed to ({X}, {Y}) | waiting {MinWait}-{MaxWait}s",
                position.X,
                position.Y,
                state.MinWaitTime,
                state.MaxWaitTime
            );

            // Reset for next move
            state.CurrentDirection = Direction.None;
            state.IsMoving = false;
            state.BlockedAttempts = 0; // Reset blocked counter on successful move
            state.WaitTimer = ctx.GameState.Random() * (state.MaxWaitTime - state.MinWaitTime) + state.MinWaitTime;

            // Deactivate movement request
            if (ctx.World.Has<MovementRequest>(ctx.Entity.Value))
            {
                ref var request = ref ctx.World.Get<MovementRequest>(ctx.Entity.Value);
                request.Active = false;
            }

            return;
        }

        // If blocked (not moving but didn't reach target), try a new random direction
        // BUT: Only check after giving MovementSystem time to process (0.1 seconds minimum)
        if (state.IsMoving && !gridMovement.IsMoving && !movedOneTitle && state.MovingTimer > 0.1f)
        {
            state.BlockedAttempts++;

            // After 4 blocked attempts, give up and wait (probably surrounded by obstacles)
            if (state.BlockedAttempts >= 4)
            {
                ctx.Logger.LogInformation(
                    "Wander stuck at ({X}, {Y}) after {Attempts} attempts - waiting",
                    position.X,
                    position.Y,
                    state.BlockedAttempts
                );

                // Reset and wait before trying again
                state.CurrentDirection = Direction.None;
                state.IsMoving = false;
                state.BlockedAttempts = 0;
                state.WaitTimer = ctx.GameState.Random() * (state.MaxWaitTime - state.MinWaitTime) + state.MinWaitTime;

                // Deactivate movement request
                if (ctx.World.Has<MovementRequest>(ctx.Entity.Value))
                {
                    ref var request = ref ctx.World.Get<MovementRequest>(ctx.Entity.Value);
                    request.Active = false;
                }

                return;
            }

            // Pick a NEW random direction immediately (don't wait)
            var directions = new[]
            {
                Direction.North,
                Direction.South,
                Direction.West,
                Direction.East,
            };
            state.CurrentDirection = directions[ctx.GameState.RandomRange(0, directions.Length)];
            state.StartPosition = new Point(position.X, position.Y);
            state.MovingTimer = 0f; // Reset timer for new direction
            // Stay in IsMoving state, no wait timer

            ctx.Logger.LogInformation(
                "Blocked - trying direction {Direction} (attempt {Attempt}/4)",
                state.CurrentDirection,
                state.BlockedAttempts
            );

            // Deactivate old movement request (will create new one on next pass)
            if (ctx.World.Has<MovementRequest>(ctx.Entity.Value))
            {
                ref var request = ref ctx.World.Get<MovementRequest>(ctx.Entity.Value);
                request.Active = false;
            }

            return;
        }

        // Keep issuing movement request (same pattern as patrol)
        if (state.IsMoving && state.CurrentDirection != Direction.None)
        {
            if (ctx.World.Has<MovementRequest>(ctx.Entity.Value))
            {
                ref var request = ref ctx.World.Get<MovementRequest>(ctx.Entity.Value);
                request.Direction = state.CurrentDirection;
                request.Active = true;
            }
            else
            {
                ctx.World.Add(ctx.Entity.Value, new MovementRequest(state.CurrentDirection));
            }
        }
    }

    public override void OnDeactivated(ScriptContext ctx)
    {
        // Cleanup per-entity state
        if (ctx.HasState<WanderState>())
        {
            ref var state = ref ctx.GetState<WanderState>();
            ctx.Logger.LogInformation(
                "Wander behavior deactivated after {Count} movements",
                state.MovementCount
            );
            ctx.RemoveState<WanderState>();
        }

        ctx.Logger.LogDebug("Wander behavior deactivated");
    }
}

// Component to store wander-specific state
public struct WanderState
{
    public float WaitTimer;
    public float MinWaitTime;
    public float MaxWaitTime;
    public Direction CurrentDirection;
    public bool IsMoving;
    public int MovementCount;
    public Point StartPosition;
    public int BlockedAttempts; // Track consecutive blocked attempts
    public float MovingTimer; // Time spent trying to move in current direction
}

return new WanderBehavior();
