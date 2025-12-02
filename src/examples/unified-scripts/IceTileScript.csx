#load "UnifiedScriptBase.cs"

using PokeSharp.Scripting.Unified;
using Microsoft.Xna.Framework;

/// <summary>
/// Ice tile behavior - player slides when stepping on it
/// Demonstrates: Event-driven tile behavior, state management, delayed actions
///
/// OLD SYSTEM: Required TileBehaviorScriptBase
/// NEW SYSTEM: Just use UnifiedScriptBase!
/// </summary>
public class IceTileScript : UnifiedScriptBase
{
    private const int SLIDE_DELAY_TICKS = 4;
    private const float SLIDE_SPEED_MULTIPLIER = 2.0f;

    public override void Initialize()
    {
        // Subscribe to player movement - when player steps on this tile
        SubscribeWhen<PlayerMoveEvent>(
            evt => evt.ToPosition == Target.Position,
            HandlePlayerSteppedOn
        );

        Log("Ice tile initialized at " + Target.Position);
    }

    private void HandlePlayerSteppedOn(PlayerMoveEvent evt)
    {
        // Check if player is wearing ice skates (stored in player data)
        var player = evt.Player;

        // Get the direction the player was moving
        var direction = new Point(
            evt.ToPosition.X - evt.FromPosition.X,
            evt.ToPosition.Y - evt.FromPosition.Y
        );

        // Play ice slide sound
        Publish(new PlaySoundEvent { SoundName = "ice_slide" });

        // Slide the player in the same direction after a short delay
        DelayedAction(SLIDE_DELAY_TICKS, () =>
        {
            var nextPosition = new Point(
                evt.ToPosition.X + direction.X,
                evt.ToPosition.Y + direction.Y
            );

            // Check if next position is walkable
            if (World.IsTileWalkable(nextPosition))
            {
                Publish(new ForcePlayerMoveEvent
                {
                    TargetPosition = nextPosition,
                    IsSliding = true,
                    SpeedMultiplier = SLIDE_SPEED_MULTIPLIER
                });
            }
        });

        // Track ice tile steps (for achievements)
        IncrementPlayerStat("ice_tiles_stepped", 1);
    }

    // Helper method
    private void IncrementPlayerStat(string stat, int amount)
    {
        Publish(new ModifyPlayerStatEvent { StatName = stat, Amount = amount });
    }

    private void Log(string message)
    {
        Publish(new LogEvent { Message = $"[IceTile] {message}" });
    }
}

// Additional event types used by this script
public class PlaySoundEvent : IGameEvent
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string SoundName { get; set; }
}

public class ForcePlayerMoveEvent : IGameEvent
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public Point TargetPosition { get; set; }
    public bool IsSliding { get; set; }
    public float SpeedMultiplier { get; set; }
}

public class ModifyPlayerStatEvent : IGameEvent
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string StatName { get; set; }
    public int Amount { get; set; }
}

public class LogEvent : IGameEvent
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string Message { get; set; }
}

// Extension methods for IGameWorld
public static class WorldExtensions
{
    public static bool IsTileWalkable(this IGameWorld world, Point position)
    {
        // Implementation would check collision, boundaries, etc.
        return true; // Placeholder
    }
}

return new IceTileScript();
