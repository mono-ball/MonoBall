// ledge_jump_unified.csx
// Unified ScriptBase implementation with custom event
// Player can jump down ledges but not climb up
// Publishes custom LedgeJumpedEvent for other scripts to react

using PokeSharp.Game.Scripting.Runtime;
using PokeSharp.Game.Scripting.Events;
using PokeSharp.Core.Components;
using System;
using System.Numerics;

// Custom event for ledge jumps
public class LedgeJumpedEvent : IGameEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public Entity Entity { get; init; }
    public Direction JumpDirection { get; init; }
    public Vector2 StartPosition { get; init; }
    public Vector2 LandingPosition { get; init; }
}

public class LedgeJumpScript : ScriptBase
{
    // Configuration
    public Direction ledgeDirection = Direction.Down; // Can only jump in this direction
    public bool allowJump = true;
    public string jumpAnimation = "jump";
    public string jumpSound = "jump";

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // Prevent moving onto ledge from wrong direction
        On<MovementStartedEvent>(evt => {
            var tile = ctx.Map.GetTileAt(evt.TargetPosition);

            if (tile?.Type == TileType.Ledge) {
                // Check if trying to move in allowed direction
                if (evt.Direction != ledgeDirection) {
                    evt.Cancel("Can't climb up the ledge!");

                    // Play bump sound
                    ctx.Effects.PlaySound("bump");
                    ctx.Logger.Info($"Ledge: Blocked movement in {evt.Direction} direction");
                }
            }
        });

        // Play jump animation when jumping down ledge
        On<MovementCompletedEvent>(evt => {
            if (!ctx.Player.IsPlayerEntity(evt.Entity)) {
                return;
            }

            var tile = ctx.Map.GetTileAt(evt.OldPosition);

            if (tile?.Type == TileType.Ledge && evt.Direction == ledgeDirection) {
                ctx.Logger.Info($"Ledge: Player jumped from {evt.OldPosition} to {evt.NewPosition}");
                PerformJump(evt.Entity, evt.OldPosition, evt.NewPosition);
            }
        });
    }

    private void PerformJump(Entity entity, Vector2 startPos, Vector2 landingPos)
    {
        // Play jump animation on entity
        ctx.Effects.PlayAnimation(entity, jumpAnimation);

        // Play jump sound
        ctx.Effects.PlaySound(jumpSound);

        // Spawn dust cloud on landing
        ctx.Effects.PlayEffect("land_dust", landingPos);

        // Publish custom event for other scripts to react
        Publish(new LedgeJumpedEvent {
            Entity = entity,
            JumpDirection = ledgeDirection,
            StartPosition = startPos,
            LandingPosition = landingPos
        });

        ctx.Logger.Info("Ledge: Published LedgeJumpedEvent");

        // Optional: Continue movement one more tile (like in Pokemon games)
        if (allowJump) {
            ContinueJumpMovement(entity);
        }
    }

    private void ContinueJumpMovement(Entity entity)
    {
        var movement = entity.Get<MovementComponent>();
        var currentPos = entity.Get<Position>().Value;

        var targetPos = ledgeDirection switch {
            Direction.Up => currentPos + new Vector2(0, -1),
            Direction.Down => currentPos + new Vector2(0, 1),
            Direction.Left => currentPos + new Vector2(-1, 0),
            Direction.Right => currentPos + new Vector2(1, 0),
            _ => currentPos
        };

        if (ctx.Map.IsWalkable(targetPos)) {
            movement.StartMove(targetPos, ledgeDirection);
        }
    }
}

// Return script instance
return new LedgeJumpScript();
