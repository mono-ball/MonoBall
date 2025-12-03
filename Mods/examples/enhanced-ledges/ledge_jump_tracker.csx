using PokeSharp.Game.Scripting.Runtime;
using EnhancedLedges.Events;

/// <summary>
///     Ledge Jump Tracker - tracks player's total ledge jumps and awards achievements.
///     Demonstrates event subscription patterns and progression tracking.
/// </summary>
/// <remarks>
///     Achievements:
///     - "First Jump": Complete your first ledge jump
///     - "Ledge Enthusiast": Jump over 10 ledges
///     - "Jump Master": Jump over 50 ledges
///     - "Ledge Legend": Jump over 100 ledges
///     - "Boosted": Perform a boosted jump
///     - "Survivor": Be on a ledge when it crumbles
/// </remarks>
public class LedgeJumpTrackerBehavior : ScriptBase
{
    private const string STATE_TOTAL_JUMPS = "player_total_jumps";
    private const string STATE_BOOSTED_JUMPS = "player_boosted_jumps";
    private const string STATE_SURVIVED_CRUMBLES = "player_survived_crumbles";
    private const string STATE_ACHIEVEMENTS = "player_achievements";

    private readonly Dictionary<string, int> _achievementThresholds = new()
    {
        { "first_jump", 1 },
        { "ledge_enthusiast", 10 },
        { "jump_master", 50 },
        { "ledge_legend", 100 }
    };

    public override void Initialize(ScriptContext ctx)
    {
        base.Initialize(ctx);

        // Initialize tracking state
        if (!ctx.State.HasKey(STATE_TOTAL_JUMPS))
        {
            ctx.State.SetInt(STATE_TOTAL_JUMPS, 0);
        }

        if (!ctx.State.HasKey(STATE_BOOSTED_JUMPS))
        {
            ctx.State.SetInt(STATE_BOOSTED_JUMPS, 0);
        }

        if (!ctx.State.HasKey(STATE_SURVIVED_CRUMBLES))
        {
            ctx.State.SetInt(STATE_SURVIVED_CRUMBLES, 0);
        }

        if (!ctx.State.HasKey(STATE_ACHIEVEMENTS))
        {
            ctx.State.SetString(STATE_ACHIEVEMENTS, "");
        }

        var totalJumps = ctx.State.GetInt(STATE_TOTAL_JUMPS);
        Context.Logger.LogInformation("Jump tracker initialized: {TotalJumps} total jumps", totalJumps);
    }

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // Track ledge jumps
        On<LedgeJumpedEvent>((evt) =>
        {
            // Increment total jumps
            var totalJumps = Context.State.GetInt(STATE_TOTAL_JUMPS);
            totalJumps++;
            Context.State.SetInt(STATE_TOTAL_JUMPS, totalJumps);

            Context.Logger.LogInformation("Ledge jumped! Total: {TotalJumps}", totalJumps);

            // Track boosted jumps
            if (evt.IsBoosted)
            {
                var boostedJumps = Context.State.GetInt(STATE_BOOSTED_JUMPS);
                boostedJumps++;
                Context.State.SetInt(STATE_BOOSTED_JUMPS, boostedJumps);

                Context.Logger.LogInformation("Boosted jump! Total boosted: {BoostedJumps}", boostedJumps);
                CheckAchievement("boosted", 1, boostedJumps);
            }

            // Check jump count achievements
            CheckJumpAchievements(totalJumps);

            // Display statistics
            DisplayJumpStats();
        });

        // Track ledge crumbles (especially if player was on it)
        On<LedgeCrumbledEvent>((evt) =>
        {
            Context.Logger.LogWarning("Ledge crumbled at ({X}, {Y}) after {TotalJumps} jumps", evt.TileX, evt.TileY, evt.TotalJumps);

            if (evt.WasPlayerOn)
            {
                var survivedCrumbles = Context.State.GetInt(STATE_SURVIVED_CRUMBLES);
                survivedCrumbles++;
                Context.State.SetInt(STATE_SURVIVED_CRUMBLES, survivedCrumbles);

                Context.Logger.LogInformation("Survived crumble! Total survived: {SurvivedCrumbles}", survivedCrumbles);
                CheckAchievement("survivor", 1, survivedCrumbles);
            }
        });

        // Track jump boosts
        On<JumpBoostActivatedEvent>((evt) =>
        {
            Context.Logger.LogInformation("Jump boost activated: {Multiplier}x for {Duration}s", evt.BoostMultiplier, evt.DurationSeconds);
        });
    }

    private void CheckJumpAchievements(int totalJumps)
    {
        foreach (var achievement in _achievementThresholds)
        {
            if (totalJumps == achievement.Value)
            {
                AwardAchievement(achievement.Key, GetAchievementName(achievement.Key));
            }
        }
    }

    private void CheckAchievement(string achievementKey, int threshold, int currentValue)
    {
        if (currentValue == threshold)
        {
            AwardAchievement(achievementKey, GetAchievementName(achievementKey));
        }
    }

    private void AwardAchievement(string key, string name)
    {
        var achievements = Context.State.GetString(STATE_ACHIEVEMENTS);
        var achievementList = achievements.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();

        if (achievementList.Contains(key))
        {
            return; // Already awarded
        }

        achievementList.Add(key);
        Context.State.SetString(STATE_ACHIEVEMENTS, string.Join(",", achievementList));

        Context.Logger.LogInformation("🏆 ACHIEVEMENT UNLOCKED: {Name}", name);

        // Would trigger achievement UI notification here
    }

    private string GetAchievementName(string key)
    {
        return key switch
        {
            "first_jump" => "First Jump - Complete your first ledge jump",
            "ledge_enthusiast" => "Ledge Enthusiast - Jump over 10 ledges",
            "jump_master" => "Jump Master - Jump over 50 ledges",
            "ledge_legend" => "Ledge Legend - Jump over 100 ledges",
            "boosted" => "Boosted - Perform a boosted jump",
            "survivor" => "Survivor - Be on a ledge when it crumbles",
            _ => key
        };
    }

    private void DisplayJumpStats()
    {
        var totalJumps = Context.State.GetInt(STATE_TOTAL_JUMPS);
        var boostedJumps = Context.State.GetInt(STATE_BOOSTED_JUMPS);
        var survivedCrumbles = Context.State.GetInt(STATE_SURVIVED_CRUMBLES);
        var achievements = Context.State.GetString(STATE_ACHIEVEMENTS);
        var achievementCount = achievements.Split(',', StringSplitOptions.RemoveEmptyEntries).Length;

        Context.Logger.LogDebug(
            $"Jump Statistics:\n" +
            $"  Total Jumps: {totalJumps}\n" +
            $"  Boosted Jumps: {boostedJumps}\n" +
            $"  Survived Crumbles: {survivedCrumbles}\n" +
            $"  Achievements: {achievementCount}/{_achievementThresholds.Count + 2}"
        );
    }
}

return new LedgeJumpTrackerBehavior();
