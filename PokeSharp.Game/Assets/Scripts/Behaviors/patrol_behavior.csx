using Arch.Core;
using Microsoft.Xna.Framework;
using PokeSharp.Core.Components;
using PokeSharp.Scripting;

/// <summary>
/// Patrol behavior using ScriptContext pattern.
/// State stored in per-entity PatrolState component (not instance fields).
/// </summary>
public class PatrolBehavior : TypeScriptBase
{
    // NO INSTANCE FIELDS! All state in components.

    public override void OnActivated(ScriptContext ctx)
    {
        // Initialize per-entity state component
        if (!ctx.HasState<PatrolState>())
        {
            ref var path = ref ctx.World.Get<PathComponent>(ctx.Entity.Value);

            ctx.World.Add(
                ctx.Entity.Value,
                new PatrolState
                {
                    CurrentWaypoint = 0,
                    WaitTimer = 0f,
                    WaitDuration = path.WaypointWaitTime,
                    Speed = 4.0f,
                    IsWaiting = false,
                }
            );
        }

        ctx.Logger.LogDebug("Patrol behavior activated");
    }

    public override void OnTick(ScriptContext ctx, float deltaTime)
    {
        // Get per-entity state (each NPC has its own)
        ref var state = ref ctx.GetState<PatrolState>();
        ref var path = ref ctx.World.Get<PathComponent>(ctx.Entity.Value);
        ref var position = ref ctx.Position;

        // Check path validity
        if (path.Waypoints == null || path.Waypoints.Length == 0)
        {
            ctx.Logger.LogWarning("Path has no waypoints");
            return;
        }

        // Wait at waypoint
        if (state.WaitTimer > 0)
        {
            state.WaitTimer -= deltaTime;
            state.IsWaiting = true;
            return;
        }

        state.IsWaiting = false;

        var target = path.Waypoints[state.CurrentWaypoint];

        // Reached waypoint?
        if (position.X == target.X && position.Y == target.Y)
        {
            ctx.Logger.LogTrace("Reached waypoint {Waypoint}", state.CurrentWaypoint);

            state.CurrentWaypoint++;
            if (state.CurrentWaypoint >= path.Waypoints.Length)
            {
                state.CurrentWaypoint = path.Loop ? 0 : path.Waypoints.Length - 1;
            }

            state.WaitTimer = state.WaitDuration;
            return;
        }

        // Move toward waypoint
        var direction = TypeScriptBase.GetDirectionTo(new Point(position.X, position.Y), target);

        ctx.World.Add(ctx.Entity.Value, new MovementRequest(direction));
    }

    public override void OnDeactivated(ScriptContext ctx)
    {
        // Cleanup per-entity state
        if (ctx.HasState<PatrolState>())
        {
            ctx.RemoveState<PatrolState>();
        }

        ctx.Logger.LogDebug("Patrol behavior deactivated");
    }
}

return new PatrolBehavior();
