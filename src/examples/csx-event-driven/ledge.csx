// ledge.csx
// Event-driven ledge behavior with one-way jump
// Player can jump down but not climb up

public class Ledge : TileBehaviorScriptBase {
    // Configuration
    public Direction ledgeDirection = Direction.Down; // Can only jump in this direction
    public bool allowJump = true;
    public string jumpAnimation = "jump";
    public string jumpSound = "jump";

    public override void RegisterEventHandlers(ScriptContext ctx) {
        // Prevent moving onto ledge from wrong direction
        OnMovementStarted(evt => {
            var tile = ctx.Map.GetTileAt(evt.TargetPosition);

            if (tile?.Type == TileType.Ledge) {
                // Check if trying to move in allowed direction
                if (evt.Direction != ledgeDirection) {
                    evt.Cancel("Can't climb up the ledge!");

                    // Play bump sound
                    ctx.Effects.PlaySound("bump");
                }
            }
        });

        // Play jump animation when jumping down ledge
        OnMovementCompleted(evt => {
            if (!ctx.Player.IsPlayerEntity(evt.Entity)) {
                return;
            }

            var tile = ctx.Map.GetTileAt(evt.OldPosition);

            if (tile?.Type == TileType.Ledge && evt.Direction == ledgeDirection) {
                PerformJump(evt.Entity, evt.NewPosition);
            }
        });
    }

    private void PerformJump(Entity entity, Vector2 landingPosition) {
        // Play jump animation on entity
        ctx.Effects.PlayAnimation(entity, jumpAnimation);

        // Play jump sound
        ctx.Effects.PlaySound(jumpSound);

        // Spawn dust cloud on landing
        ctx.Effects.PlayEffect("land_dust", landingPosition);

        // Optional: Continue movement one more tile (like in Pokemon games)
        if (allowJump) {
            ContinueJumpMovement(entity);
        }
    }

    private void ContinueJumpMovement(Entity entity) {
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
