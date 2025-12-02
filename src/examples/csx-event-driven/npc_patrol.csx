// npc_patrol.csx
// Event-driven NPC patrol behavior
// NPC patrols between waypoints with line-of-sight detection

public class NPCPatrol : TypeScriptBase {
    // Configuration
    public List<Vector2> patrolPoints = new List<Vector2> {
        new Vector2(5, 5),
        new Vector2(10, 5),
        new Vector2(10, 10),
        new Vector2(5, 10)
    };

    public float waitTimeAtPoint = 2.0f; // Seconds to wait at each point
    public bool detectPlayer = true; // Trigger battle on line of sight
    public int detectionRange = 5; // Tiles

    private int currentIndex = 0;
    private bool isWaiting = false;
    private Entity npcEntity;

    public override void OnInitialize(ScriptContext ctx) {
        base.OnInitialize(ctx);

        // Get NPC entity this script is attached to
        npcEntity = GetAttachedEntity();

        // Start patrol
        MoveToNextPoint();
    }

    public override void RegisterEventHandlers(ScriptContext ctx) {
        // React to NPC movement completion
        OnMovementCompleted(evt => {
            if (evt.Entity != npcEntity) return;

            // Arrived at patrol point, wait before moving to next
            WaitAtPoint();
        });

        // Handle blocked movement
        OnMovementBlocked(evt => {
            if (evt.Entity != npcEntity) return;

            // Skip to next point if blocked
            currentIndex = (currentIndex + 1) % patrolPoints.Count;
            MoveToNextPoint();
        });

        // Detect player movement for line-of-sight
        if (detectPlayer) {
            OnMovementCompleted(evt => {
                if (ctx.Player.IsPlayerEntity(evt.Entity)) {
                    CheckPlayerDetection(evt.Entity);
                }
            });
        }
    }

    public override void OnTick(ScriptContext ctx, float deltaTime) {
        // Use polling for wait timer
        if (isWaiting) {
            waitTimer -= deltaTime;

            if (waitTimer <= 0) {
                isWaiting = false;
                MoveToNextPoint();
            }
        }
    }

    private float waitTimer = 0;

    private void WaitAtPoint() {
        isWaiting = true;
        waitTimer = waitTimeAtPoint;

        // Face random direction while waiting
        var randomDir = (Direction)new Random().Next(0, 4);
        FaceDirection(npcEntity, randomDir);
    }

    private void MoveToNextPoint() {
        currentIndex = (currentIndex + 1) % patrolPoints.Count;
        var target = patrolPoints[currentIndex];

        var movement = npcEntity.Get<MovementComponent>();
        movement.StartMove(target);
    }

    private void CheckPlayerDetection(Entity player) {
        if (!detectPlayer || isWaiting) return;

        var npcPos = npcEntity.Get<Position>().Value;
        var playerPos = player.Get<Position>().Value;
        var npcFacing = npcEntity.Get<Facing>().Direction;

        // Check if player is in line of sight
        if (IsInLineOfSight(npcPos, playerPos, npcFacing)) {
            TriggerBattle(player);
        }
    }

    private bool IsInLineOfSight(Vector2 npcPos, Vector2 playerPos, Direction facing) {
        var diff = playerPos - npcPos;

        // Check if player is in front of NPC
        bool inFront = facing switch {
            Direction.Up => diff.Y < 0 && Math.Abs(diff.X) == 0,
            Direction.Down => diff.Y > 0 && Math.Abs(diff.X) == 0,
            Direction.Left => diff.X < 0 && Math.Abs(diff.Y) == 0,
            Direction.Right => diff.X > 0 && Math.Abs(diff.Y) == 0,
            _ => false
        };

        if (!inFront) return false;

        // Check if within detection range
        var distance = Math.Abs(diff.X + diff.Y);
        return distance <= detectionRange;
    }

    private void TriggerBattle(Entity player) {
        // Stop patrol
        isWaiting = true;

        // Face player
        FaceEntity(npcEntity, player);

        // Play exclamation effect
        ctx.Effects.PlayEffect("exclamation", npcEntity.Get<Position>().Value);
        ctx.Effects.PlaySound("trainer_spotted");

        // Walk towards player
        WalkTowardsPlayer(player);

        // Start battle after walking
        Task.Delay(1000).ContinueWith(_ => {
            ctx.GameState.StartTrainerBattle(npcEntity);
        });
    }

    private void WalkTowardsPlayer(Entity player) {
        var npcPos = npcEntity.Get<Position>().Value;
        var playerPos = player.Get<Position>().Value;

        var direction = GetDirectionTowards(npcPos, playerPos);
        var movement = npcEntity.Get<MovementComponent>();
        movement.StartMove(playerPos - GetDirectionOffset(direction), direction);
    }

    private Entity GetAttachedEntity() {
        // Implementation depends on your script attachment system
        return ctx.Npc.GetCurrentNPC();
    }

    private void FaceDirection(Entity entity, Direction direction) {
        var facing = entity.Get<Facing>();
        facing.Direction = direction;
    }

    private void FaceEntity(Entity entity, Entity target) {
        var entityPos = entity.Get<Position>().Value;
        var targetPos = target.Get<Position>().Value;
        var direction = GetDirectionTowards(entityPos, targetPos);
        FaceDirection(entity, direction);
    }

    private Direction GetDirectionTowards(Vector2 from, Vector2 to) {
        var diff = to - from;

        if (Math.Abs(diff.X) > Math.Abs(diff.Y)) {
            return diff.X > 0 ? Direction.Right : Direction.Left;
        } else {
            return diff.Y > 0 ? Direction.Down : Direction.Up;
        }
    }

    private Vector2 GetDirectionOffset(Direction direction) {
        return direction switch {
            Direction.Up => new Vector2(0, -1),
            Direction.Down => new Vector2(0, 1),
            Direction.Left => new Vector2(-1, 0),
            Direction.Right => new Vector2(1, 0),
            _ => Vector2.Zero
        };
    }
}
