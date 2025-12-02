// ice_tile.csx
// Event-driven ice tile behavior with continuous sliding
// Player slides in the direction they enter until hitting an obstacle

public class IceTile : TileBehaviorScriptBase {
    public override void RegisterEventHandlers(ScriptContext ctx) {
        // React to movement completion to keep player sliding
        OnMovementCompleted(evt => {
            // Only affect player
            if (!ctx.Player.IsPlayerEntity(evt.Entity)) {
                return;
            }

            // Check if still on ice tile
            if (IsOnIceTile(evt.NewPosition)) {
                // Keep sliding in same direction
                ContinueSliding(evt.Entity, evt.Direction);
            } else {
                // Left ice, restore normal speed
                RestoreNormalSpeed(evt.Entity);
            }
        });

        // Optional: Play sliding sound when entering ice
        OnTileSteppedOn(evt => {
            if (ctx.Player.IsPlayerEntity(evt.Entity)) {
                ctx.Effects.PlaySound("ice_slide");
            }
        });
    }

    private bool IsOnIceTile(Vector2 position) {
        var tile = ctx.Map.GetTileAt(position);
        return tile?.Type == TileType.Ice;
    }

    private void ContinueSliding(Entity entity, Direction direction) {
        var targetPos = GetNextPosition(entity, direction);

        // Check if can continue sliding
        if (ctx.Map.IsWalkable(targetPos) && !ctx.Map.HasCollision(targetPos)) {
            var movement = entity.Get<MovementComponent>();
            movement.Speed = 2.0f; // Slide faster than normal walk
            movement.StartMove(targetPos, direction);
        } else {
            // Hit obstacle, stop sliding
            RestoreNormalSpeed(entity);
            ctx.Effects.PlaySound("bump");
        }
    }

    private void RestoreNormalSpeed(Entity entity) {
        var movement = entity.Get<MovementComponent>();
        movement.Speed = 1.0f;
    }

    private Vector2 GetNextPosition(Entity entity, Direction direction) {
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
