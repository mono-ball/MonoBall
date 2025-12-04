using Arch.Core;
using Microsoft.Xna.Framework;
using MonoBallFramework.Engine.Core.Events.Movement;
using MonoBallFramework.Engine.Core.Events.System;
using MonoBallFramework.Game.Components.Movement;
using MonoBallFramework.Game.Scripting.Runtime;

/// <summary>
/// Template for creating NPC behavior scripts.
///
/// INSTRUCTIONS:
/// 1. Copy this file to your mod's Scripts folder
/// 2. Rename the class to match your NPC behavior (e.g., VendorBehavior, TrainerBehavior)
/// 3. Update the TODO sections with your custom logic
/// 4. Choose a movement pattern (patrol, wander, stationary, or custom)
/// 5. Add dialogue and interaction logic
///
/// COMMON USE CASES:
/// - NPCs that walk around (patrol, wander)
/// - Stationary NPCs (shopkeepers, guards)
/// - Trainer battles on line-of-sight
/// - Quest givers with dialogue trees
/// - Roaming NPCs with schedules
/// </summary>
public class TemplateNpcBehavior : ScriptBase
{
    // ============================================================================
    // CONFIGURATION SECTION
    // ============================================================================
    // TODO: Configure your NPC's behavior here

    /// <summary>
    /// Initialize the NPC script and set up initial state.
    /// This is called once when the script is loaded.
    /// </summary>
    public override void Initialize(ScriptContext ctx)
    {
        base.Initialize(ctx); // REQUIRED: Initializes Context property

        // TODO: Set up initial NPC state
        // Examples:
        // Set("movement_mode", "patrol"); // patrol, wander, stationary, custom
        // Set("dialogue_state", 0); // Track dialogue progression
        // Set("has_battled", false); // Track if player has battled this trainer
        // Set("interaction_cooldown", 0f); // Prevent spam interactions

        ctx.Logger.LogInformation("TemplateNpcBehavior initialized");
    }

    /// <summary>
    /// Register event handlers for NPC behavior.
    /// This is called after Initialize.
    /// </summary>
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // ========================================================================
        // MOVEMENT SYSTEM - Choose ONE pattern or create your own
        // ========================================================================

        // OPTION 1: PATROL MOVEMENT
        // NPC walks back and forth between waypoints
        // TODO: Uncomment and customize for patrol behavior
        /*
        On<TickEvent>(evt =>
        {
            if (!Context.HasState<NpcPatrolState>())
            {
                // Initialize patrol state on first tick
                ref var initPos = ref Context.Position;
                Context.World.Add(Context.Entity.Value, new NpcPatrolState
                {
                    WaypointIndex = 0,
                    Waypoints = new[]
                    {
                        new Point(initPos.X, initPos.Y),
                        new Point(initPos.X + 5, initPos.Y),
                        new Point(initPos.X + 5, initPos.Y + 3),
                        new Point(initPos.X, initPos.Y + 3),
                    },
                    PatrolSpeed = 1.0f,
                    IsForward = true
                });
                return;
            }

            ref var patrolState = ref Context.GetState<NpcPatrolState>();
            ref var position = ref Context.Position;

            var currentWaypoint = patrolState.Waypoints[patrolState.WaypointIndex];

            // Check if reached current waypoint
            if (position.X == currentWaypoint.X && position.Y == currentWaypoint.Y)
            {
                // Move to next waypoint
                if (patrolState.IsForward)
                {
                    patrolState.WaypointIndex++;
                    if (patrolState.WaypointIndex >= patrolState.Waypoints.Length)
                    {
                        patrolState.WaypointIndex = patrolState.Waypoints.Length - 2;
                        patrolState.IsForward = false;
                    }
                }
                else
                {
                    patrolState.WaypointIndex--;
                    if (patrolState.WaypointIndex < 0)
                    {
                        patrolState.WaypointIndex = 1;
                        patrolState.IsForward = true;
                    }
                }
            }
            else
            {
                // Move toward current waypoint
                var targetWaypoint = patrolState.Waypoints[patrolState.WaypointIndex];
                var direction = Context.Map.GetDirectionTo(position.X, position.Y, targetWaypoint.X, targetWaypoint.Y);

                if (Context.World.Has<MovementRequest>(Context.Entity.Value))
                {
                    ref var request = ref Context.World.Get<MovementRequest>(Context.Entity.Value);
                    request.Direction = direction;
                    request.Active = true;
                }
                else
                {
                    Context.World.Add(Context.Entity.Value, new MovementRequest(direction));
                }
            }
        });
        */

        // OPTION 2: WANDER MOVEMENT
        // NPC walks randomly, pausing between moves
        // TODO: Uncomment and customize for wander behavior
        /*
        On<TickEvent>(evt =>
        {
            if (!Context.HasState<NpcWanderState>())
            {
                // Initialize wander state
                Context.World.Add(Context.Entity.Value, new NpcWanderState
                {
                    WaitTimer = Context.GameState.Random() * 3.0f,
                    MinWaitTime = 2.0f,
                    MaxWaitTime = 5.0f
                });
                return;
            }

            ref var wanderState = ref Context.GetState<NpcWanderState>();

            // Wait before next move
            if (wanderState.WaitTimer > 0)
            {
                wanderState.WaitTimer -= evt.DeltaTime;
                return;
            }

            // Pick random direction and move
            var directions = new[] { Direction.North, Direction.South, Direction.West, Direction.East };
            var randomDirection = directions[Context.GameState.RandomRange(0, directions.Length)];

            if (Context.World.Has<MovementRequest>(Context.Entity.Value))
            {
                ref var request = ref Context.World.Get<MovementRequest>(Context.Entity.Value);
                request.Direction = randomDirection;
                request.Active = true;
            }
            else
            {
                Context.World.Add(Context.Entity.Value, new MovementRequest(randomDirection));
            }

            // Reset wait timer after move attempt
            wanderState.WaitTimer = Context.GameState.Random() *
                (wanderState.MaxWaitTime - wanderState.MinWaitTime) + wanderState.MinWaitTime;
        });
        */

        // OPTION 3: STATIONARY NPC
        // NPC stays in place, only changes facing direction
        // TODO: Uncomment for stationary behavior
        /*
        On<TickEvent>(evt =>
        {
            // Keep NPC at initial position
            // Optionally rotate to face player when nearby

            var playerEntity = Context.Player.GetPlayerEntity();
            if (Context.World.Has<Position>(playerEntity))
            {
                ref var playerPos = ref Context.World.Get<Position>(playerEntity);
                ref var npcPos = ref Context.Position;

                var distance = Math.Abs(playerPos.X - npcPos.X) + Math.Abs(playerPos.Y - npcPos.Y);
                if (distance <= 2)
                {
                    // Face the player
                    var faceDirection = Context.Map.GetDirectionTo(npcPos.X, npcPos.Y, playerPos.X, playerPos.Y);
                    Context.Logger.LogDebug("NPC facing player: {Direction}", faceDirection);
                    // TODO: Update sprite direction component
                }
            }
        });
        */

        // ========================================================================
        // INTERACTION SYSTEM
        // ========================================================================
        // Handle player interactions (talking, battling, trading, etc.)

        // TODO: Uncomment and implement interaction logic
        /*
        On<InteractionEvent>(evt =>
        {
            if (evt.TargetEntity != Context.Entity.Value) return;

            var playerEntity = evt.SourceEntity;

            // Check cooldown to prevent spam
            var cooldown = Get<float>("interaction_cooldown", 0f);
            if (cooldown > 0)
            {
                Context.Logger.LogDebug("Interaction on cooldown");
                return;
            }

            Context.Logger.LogInformation("Player interacted with NPC");

            // TODO: Implement your interaction logic
            // Examples:

            // 1. Show dialogue
            // ShowDialogue(playerEntity);

            // 2. Start battle
            // if (!Get<bool>("has_battled", false))
            // {
            //     StartTrainerBattle(playerEntity);
            //     Set("has_battled", true);
            // }

            // 3. Open shop
            // OpenShop(playerEntity);

            // 4. Give quest
            // GiveQuest(playerEntity);

            // Set cooldown
            Set("interaction_cooldown", 1.0f);
        });
        */

        // ========================================================================
        // LINE-OF-SIGHT DETECTION
        // ========================================================================
        // Detect when player enters NPC's line of sight (for trainers)

        // TODO: Uncomment for line-of-sight detection
        /*
        On<TickEvent>(evt =>
        {
            // Only check if we haven't battled yet
            if (Get<bool>("has_battled", false)) return;

            var playerEntity = Context.Player.GetPlayerEntity();
            if (!Context.World.Has<Position>(playerEntity)) return;

            ref var playerPos = ref Context.World.Get<Position>(playerEntity);
            ref var npcPos = ref Context.Position;

            // Check if player is in line of sight (e.g., 5 tiles in front)
            var facingDirection = Get<Direction>("facing_direction", Direction.South);
            var inLineOfSight = CheckLineOfSight(npcPos, playerPos, facingDirection, 5);

            if (inLineOfSight)
            {
                Context.Logger.LogInformation("Player spotted! Initiating battle!");
                StartTrainerBattle(playerEntity);
                Set("has_battled", true);
            }
        });
        */

        // ========================================================================
        // MOVEMENT COMPLETED
        // ========================================================================
        // React when NPC completes a movement

        On<MovementCompletedEvent>(evt =>
        {
            if (evt.Entity != Context.Entity.Value)
                return;

            Context.Logger.LogDebug(
                "NPC completed movement from ({PrevX}, {PrevY}) to ({CurrX}, {CurrY})",
                evt.PreviousX,
                evt.PreviousY,
                evt.CurrentX,
                evt.CurrentY
            );

            // TODO: Add post-movement logic
            // Examples:
            // - Play sound effect
            // - Check for items at new location
            // - Update quest progress
        });

        // ========================================================================
        // COOLDOWN UPDATES
        // ========================================================================
        // Handle cooldown timers

        On<TickEvent>(evt =>
        {
            var cooldown = Get<float>("interaction_cooldown", 0f);
            if (cooldown > 0)
            {
                Set("interaction_cooldown", cooldown - evt.DeltaTime);
            }
        });
    }

    /// <summary>
    /// Called when the script is unloaded.
    /// </summary>
    public override void OnUnload()
    {
        // TODO: Clean up NPC state
        Context.Logger.LogInformation("TemplateNpcBehavior unloaded");

        base.OnUnload(); // REQUIRED: Cleans up event subscriptions
    }

    // ============================================================================
    // HELPER METHODS
    // ============================================================================

    /// <summary>
    /// Example: Show dialogue to the player.
    /// </summary>
    private void ShowDialogue(Entity playerEntity)
    {
        // TODO: Implement dialogue system integration
        var dialogueState = Get<int>("dialogue_state", 0);

        Context.Logger.LogInformation("Showing dialogue state: {State}", dialogueState);

        // Example dialogue progression
        switch (dialogueState)
        {
            case 0:
                // First conversation
                // Context.Dialogue.ShowMessage("Hello, traveler!");
                Set("dialogue_state", 1);
                break;
            case 1:
                // Second conversation
                // Context.Dialogue.ShowMessage("Nice to see you again!");
                Set("dialogue_state", 2);
                break;
            default:
                // Repeat dialogue
                // Context.Dialogue.ShowMessage("Have a great day!");
                break;
        }
    }

    /// <summary>
    /// Example: Start a trainer battle.
    /// </summary>
    private void StartTrainerBattle(Entity playerEntity)
    {
        // TODO: Implement battle system integration
        Context.Logger.LogInformation("Starting trainer battle!");

        // Example:
        // Context.Battle.InitiateTrainerBattle(Context.Entity.Value, playerEntity);
    }

    /// <summary>
    /// Example: Check if player is in NPC's line of sight.
    /// </summary>
    private bool CheckLineOfSight(Position npcPos, Position playerPos, Direction facing, int range)
    {
        // TODO: Implement line-of-sight logic
        switch (facing)
        {
            case Direction.North:
                return playerPos.X == npcPos.X
                    && playerPos.Y < npcPos.Y
                    && (npcPos.Y - playerPos.Y) <= range;
            case Direction.South:
                return playerPos.X == npcPos.X
                    && playerPos.Y > npcPos.Y
                    && (playerPos.Y - npcPos.Y) <= range;
            case Direction.West:
                return playerPos.Y == npcPos.Y
                    && playerPos.X < npcPos.X
                    && (npcPos.X - playerPos.X) <= range;
            case Direction.East:
                return playerPos.Y == npcPos.Y
                    && playerPos.X > npcPos.X
                    && (playerPos.X - npcPos.X) <= range;
            default:
                return false;
        }
    }
}

// ============================================================================
// STATE COMPONENTS
// ============================================================================

/// <summary>
/// State for patrol movement pattern.
/// </summary>
public struct NpcPatrolState
{
    public int WaypointIndex;
    public Point[] Waypoints;
    public float PatrolSpeed;
    public bool IsForward;
}

/// <summary>
/// State for wander movement pattern.
/// </summary>
public struct NpcWanderState
{
    public float WaitTimer;
    public float MinWaitTime;
    public float MaxWaitTime;
}

// IMPORTANT: Return an instance of your behavior class
return new TemplateNpcBehavior();
