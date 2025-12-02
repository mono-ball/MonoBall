// warp_tile.csx
// Event-driven warp tile behavior with animations and auto-walk
// Teleports player to target map with smooth transition

public class WarpTile : TileBehaviorScriptBase {
    // Configuration (set via map editor or script parameters)
    public string targetMap = "indoor_house";
    public Vector2 targetPosition = new Vector2(5, 5);
    public Direction? exitDirection = Direction.Down; // Auto-walk after warp
    public bool playAnimation = true;
    public string warpSound = "warp";

    public override void RegisterEventHandlers(ScriptContext ctx) {
        OnTileSteppedOn(evt => {
            // Only warp player
            if (ctx.Player.IsPlayerEntity(evt.Entity)) {
                StartWarpSequence(evt.Entity);
            }
        });
    }

    private async void StartWarpSequence(Entity entity) {
        if (playAnimation) {
            // Play warp out animation
            var currentPos = entity.Get<Position>().Value;
            ctx.Effects.PlayEffect("warp_out", currentPos);
            ctx.Effects.PlaySound(warpSound);

            // Fade out screen
            ctx.Effects.FadeScreen(Color.Black, duration: 0.3f);

            await Task.Delay(300);
        }

        // Perform the actual warp
        ctx.Map.WarpTo(targetMap, targetPosition);

        if (playAnimation) {
            // Fade in new map
            await Task.Delay(100);
            ctx.Effects.FadeScreen(Color.Transparent, duration: 0.3f);

            // Play warp in animation
            ctx.Effects.PlayEffect("warp_in", targetPosition);
        }

        // Auto-walk if exit direction specified
        if (exitDirection.HasValue) {
            await Task.Delay(200);
            AutoWalk(entity, exitDirection.Value);
        }
    }

    private void AutoWalk(Entity entity, Direction direction) {
        var movement = entity.Get<MovementComponent>();
        var currentPos = entity.Get<Position>().Value;

        var targetPos = direction switch {
            Direction.Up => currentPos + new Vector2(0, -1),
            Direction.Down => currentPos + new Vector2(0, 1),
            Direction.Left => currentPos + new Vector2(-1, 0),
            Direction.Right => currentPos + new Vector2(1, 0),
            _ => currentPos
        };

        movement.StartMove(targetPos, direction);
    }
}
