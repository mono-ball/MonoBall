// ice_tile_unified.csx
// Unified ScriptBase implementation for ice tile behavior
// Player slides in the direction they enter until hitting an obstacle
// Uses ScriptBase for unified event handling

using PokeSharp.Game.Scripting.Runtime;
using PokeSharp.Game.Scripting.Events;
using PokeSharp.Core.Components;
using System.Numerics;

public class IceTileScript : ScriptBase
{
    // Configuration
    public float slideSpeed = 2.0f; // Faster than normal walk
    public float normalSpeed = 1.0f;

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // React to movement completion to keep player sliding
        On<MovementCompletedEvent>(evt => {
            // Only affect player
            if (!ctx.Player.IsPlayerEntity(evt.Entity)) {
                return;
            }

            ctx.Logger.Info($"Ice tile: Player completed movement to {evt.NewPosition}");

            // Check if still on ice tile
            if (IsOnIceTile(evt.NewPosition)) {
                // Keep sliding in same direction
                ContinueSliding(evt.Entity, evt.Direction);
            } else {
                // Left ice, restore normal speed
                RestoreNormalSpeed(evt.Entity);
                ctx.Logger.Info("Ice tile: Player left ice, normal speed restored");
            }
        });

        // Play sliding sound when entering ice
        On<TileSteppedOnEvent>(evt => {
            if (ctx.Player.IsPlayerEntity(evt.Entity)) {
                ctx.Effects.PlaySound("ice_slide");
                ctx.Logger.Info($"Ice tile activated at ({evt.TileX}, {evt.TileY})");
            }
        });
    }

    private bool IsOnIceTile(Vector2 position)
    {
        var tile = ctx.Map.GetTileAt(position);
        return tile?.Type == TileType.Ice;
    }

    private void ContinueSliding(Entity entity, Direction direction)
    {
        var targetPos = GetNextPosition(entity, direction);

        // Check if can continue sliding
        if (ctx.Map.IsWalkable(targetPos) && !ctx.Map.HasCollision(targetPos)) {
            var movement = entity.Get<MovementComponent>();
            movement.Speed = slideSpeed;
            movement.StartMove(targetPos, direction);
        } else {
            // Hit obstacle, stop sliding
            RestoreNormalSpeed(entity);
            ctx.Effects.PlaySound("bump");
            ctx.Logger.Info("Ice tile: Hit obstacle, stopped sliding");
        }
    }

    private void RestoreNormalSpeed(Entity entity)
    {
        var movement = entity.Get<MovementComponent>();
        movement.Speed = normalSpeed;
    }

    private Vector2 GetNextPosition(Entity entity, Direction direction)
    {
        var currentPos = entity.Get<Position>().Value;
        return direction switch {
            Direction.Up => currentPos + new Vector2(0, -1),
            Direction.Down => currentPos + new Vector2(0, 1),
            Direction.Left => currentPos + new Vector2(-1, 0),
            Direction.Right => currentPos + new Vector2(1, 0),
            _ => currentPos
        };
    }
}

// Return script instance
return new IceTileScript();
