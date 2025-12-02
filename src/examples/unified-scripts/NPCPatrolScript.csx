#load "UnifiedScriptBase.cs"

using PokeSharp.Scripting.Unified;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// NPC patrol behavior - follows a path and reacts to player
/// Demonstrates: Event-driven NPC behavior, pathfinding, state machines
///
/// OLD SYSTEM: Required TypeScriptBase or NPCBehaviorBase
/// NEW SYSTEM: Same UnifiedScriptBase as tiles! See the pattern?
/// </summary>
public class NPCPatrolScript : UnifiedScriptBase
{
    // Configuration (could be loaded from script parameters)
    private Point[] _patrolPath = new[]
    {
        new Point(10, 5),
        new Point(15, 5),
        new Point(15, 10),
        new Point(10, 10)
    };

    private int _currentWaypoint = 0;
    private NPCState _state = NPCState.Idle;
    private int _waitTicks = 0;

    private const int WAIT_AT_WAYPOINT = 60; // ~1 second at 60fps
    private const int PLAYER_DETECTION_RANGE = 5;

    public override void Initialize()
    {
        // Subscribe to tick events for movement (we need polling for smooth movement)
        Subscribe<TickEvent>(HandleTick);

        // Subscribe to player proximity events
        Subscribe<PlayerMoveEvent>(HandlePlayerMove);

        // Subscribe to interaction events (player talks to NPC)
        SubscribeWhen<PlayerInteractEvent>(
            evt => evt.Target == Target,
            HandlePlayerInteraction
        );

        // Load saved state
        _currentWaypoint = Get("current_waypoint", 0);
        _state = Get("state", NPCState.Idle);

        Log($"NPC patrol initialized with {_patrolPath.Length} waypoints");
    }

    private void HandleTick(TickEvent evt)
    {
        switch (_state)
        {
            case NPCState.Idle:
                // Check if we should start patrolling
                if (_waitTicks <= 0)
                {
                    _state = NPCState.Patrolling;
                }
                else
                {
                    _waitTicks--;
                }
                break;

            case NPCState.Patrolling:
                UpdatePatrol();
                break;

            case NPCState.Alerted:
                // Face the player
                FacePlayer();
                break;

            case NPCState.Interacting:
                // Do nothing, waiting for dialogue to finish
                break;
        }

        // Check for player proximity (spotting mechanic)
        if (_state == NPCState.Patrolling && IsPlayerNearby(PLAYER_DETECTION_RANGE))
        {
            var player = World.Player;
            if (CanSeePlayer(player))
            {
                _state = NPCState.Alerted;
                OnPlayerSpotted(player);
            }
        }
    }

    private void UpdatePatrol()
    {
        var targetWaypoint = _patrolPath[_currentWaypoint];
        var npc = Target as INPC;

        // Move towards current waypoint
        if (npc.Position != targetWaypoint)
        {
            MoveTowards(targetWaypoint);
        }
        else
        {
            // Reached waypoint, wait and move to next
            _waitTicks = WAIT_AT_WAYPOINT;
            _currentWaypoint = (_currentWaypoint + 1) % _patrolPath.Length;
            Set("current_waypoint", _currentWaypoint);
            _state = NPCState.Idle;
            Set("state", _state);
        }
    }

    private void MoveTowards(Point target)
    {
        var npc = Target as INPC;
        var current = npc.Position;

        // Simple pathfinding - move one tile at a time
        int dx = Math.Sign(target.X - current.X);
        int dy = Math.Sign(target.Y - current.Y);

        Point nextPos;
        if (dx != 0 && dy != 0)
        {
            // Diagonal - choose X or Y based on distance
            if (Math.Abs(target.X - current.X) > Math.Abs(target.Y - current.Y))
                nextPos = new Point(current.X + dx, current.Y);
            else
                nextPos = new Point(current.X, current.Y + dy);
        }
        else
        {
            nextPos = new Point(current.X + dx, current.Y + dy);
        }

        // Request movement through event system
        Publish(new RequestEntityMoveEvent
        {
            Entity = npc,
            TargetPosition = nextPos
        });
    }

    private void HandlePlayerMove(PlayerMoveEvent evt)
    {
        // Player moved out of range while we were alerted
        if (_state == NPCState.Alerted && !IsPlayerNearby(PLAYER_DETECTION_RANGE))
        {
            _state = NPCState.Patrolling;
            Log("Lost sight of player, resuming patrol");
        }
    }

    private void HandlePlayerInteraction(PlayerInteractEvent evt)
    {
        _state = NPCState.Interacting;
        FacePlayer();

        // Start dialogue
        var npc = Target as INPC;
        var dialogue = GetDialogue();

        Publish(new StartDialogueEvent
        {
            NPC = npc,
            DialogueText = dialogue,
            OnComplete = () =>
            {
                _state = NPCState.Patrolling;
                Log("Dialogue ended, resuming patrol");
            }
        });
    }

    private void OnPlayerSpotted(IPlayer player)
    {
        Log("Player spotted!");

        // Play exclamation animation
        Publish(new PlayAnimationEvent
        {
            AnimationName = "exclamation",
            Position = Target.Position
        });

        // Play sound
        Publish(new PlaySoundEvent { SoundName = "trainer_spotted" });

        // Check if this is a trainer battle
        bool isTrainer = Get("is_trainer", false);
        if (isTrainer && !Get("already_battled", false))
        {
            InitiateTrainerBattle(player);
        }
    }

    private void InitiateTrainerBattle(IPlayer player)
    {
        Set("already_battled", true);

        Publish(new TrainerBattleEvent
        {
            Trainer = Target as INPC,
            Player = player
        });
    }

    private bool CanSeePlayer(IPlayer player)
    {
        var npc = Target as INPC;

        // Check if player is in front of NPC based on facing direction
        var directionToPlayer = new Point(
            player.Position.X - npc.Position.X,
            player.Position.Y - npc.Position.Y
        );

        // Simple line of sight check
        return true; // Placeholder - would check facing direction and obstacles
    }

    private void FacePlayer()
    {
        var npc = Target as INPC;
        var player = World.Player;

        var dx = player.Position.X - npc.Position.X;
        var dy = player.Position.Y - npc.Position.Y;

        int direction;
        if (Math.Abs(dx) > Math.Abs(dy))
            direction = dx > 0 ? 2 : 3; // Right : Left
        else
            direction = dy > 0 ? 0 : 1; // Down : Up

        Publish(new SetEntityDirectionEvent
        {
            Entity = npc,
            Direction = direction
        });
    }

    private string GetDialogue()
    {
        var dialogueOptions = new[]
        {
            "I'm on patrol duty today!",
            "Have you seen any suspicious activity?",
            "Beautiful day for a walk, isn't it?",
            "I've been walking this route for years."
        };

        int dialogueIndex = Get("dialogue_count", 0);
        string dialogue = dialogueOptions[dialogueIndex % dialogueOptions.Length];
        Set("dialogue_count", dialogueIndex + 1);

        return dialogue;
    }

    public override void Cleanup()
    {
        // Save current state
        Set("state", _state);
        Log("NPC patrol cleanup complete");
    }

    private void Log(string message)
    {
        Publish(new LogEvent { Message = $"[NPCPatrol] {message}" });
    }
}

// Supporting types
public enum NPCState
{
    Idle,
    Patrolling,
    Alerted,
    Interacting
}

public interface INPC : IScriptable
{
    int FacingDirection { get; }
}

public class RequestEntityMoveEvent : IGameEvent
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public IEntity Entity { get; set; }
    public Point TargetPosition { get; set; }
}

public class StartDialogueEvent : IGameEvent
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public INPC NPC { get; set; }
    public string DialogueText { get; set; }
    public Action OnComplete { get; set; }
}

public class TrainerBattleEvent : IGameEvent
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public INPC Trainer { get; set; }
    public IPlayer Player { get; set; }
}

public class SetEntityDirectionEvent : IGameEvent
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public IEntity Entity { get; set; }
    public int Direction { get; set; }
}

return new NPCPatrolScript();
