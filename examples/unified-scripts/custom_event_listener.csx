// custom_event_listener.csx
// Example demonstrating custom event listening
// Listens for LedgeJumpedEvent and plays special effects/achievements

using PokeSharp.Game.Scripting.Runtime;
using PokeSharp.Game.Scripting.Events;
using PokeSharp.Core.Components;
using System;
using System.Numerics;

// This script listens for the custom LedgeJumpedEvent published by ledge_jump_unified.csx
public class LedgeJumpListenerScript : ScriptBase
{
    private int totalJumps = 0;
    private const int ACHIEVEMENT_THRESHOLD = 10;

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // Listen for custom LedgeJumpedEvent
        On<LedgeJumpedEvent>(evt => {
            totalJumps++;

            ctx.Logger.Info($"Ledge Jump Listener: Jump #{totalJumps} detected! " +
                           $"Direction: {evt.JumpDirection}, " +
                           $"From: {evt.StartPosition} To: {evt.LandingPosition}");

            // Play special effect for jump
            PlayJumpEffect(evt);

            // Check for achievement
            CheckJumpAchievement();

            // Example: Special behavior for consecutive jumps
            if (totalJumps % 5 == 0) {
                ctx.Effects.PlaySound("milestone");
                ctx.Logger.Info($"Ledge Jump Listener: {totalJumps} jumps milestone!");
            }
        });

        // Example: Listen for multiple event types
        On<MovementCompletedEvent>(evt => {
            if (ctx.Player.IsPlayerEntity(evt.Entity)) {
                // Could track total movement for statistics
                ctx.Logger.Debug("Ledge Jump Listener: Player movement detected");
            }
        });
    }

    private void PlayJumpEffect(LedgeJumpedEvent evt)
    {
        // Play special particle effect at landing position
        ctx.Effects.PlayEffect("jump_trail", evt.LandingPosition);

        // Different sounds based on jump direction
        var soundName = evt.JumpDirection switch {
            Direction.Down => "jump_down",
            Direction.Up => "jump_up",
            Direction.Left => "jump_left",
            Direction.Right => "jump_right",
            _ => "jump"
        };

        ctx.Effects.PlaySound(soundName);
    }

    private void CheckJumpAchievement()
    {
        if (totalJumps == ACHIEVEMENT_THRESHOLD) {
            ctx.Logger.Info($"Ledge Jump Listener: ACHIEVEMENT UNLOCKED - {ACHIEVEMENT_THRESHOLD} Jumps!");
            ctx.Effects.PlaySound("achievement");
            ctx.Effects.FlashScreen(Color.Gold, duration: 0.5f);

            // Could trigger achievement system here
            ctx.GameState.UnlockAchievement("parkour_master");
        }
    }

    public override void OnDestroy(ScriptContext ctx)
    {
        ctx.Logger.Info($"Ledge Jump Listener: Destroyed. Total jumps tracked: {totalJumps}");
        base.OnDestroy(ctx);
    }
}

// Return script instance
return new LedgeJumpListenerScript();

// CUSTOM EVENT PATTERN SUMMARY:
//
// 1. Event Publisher (ledge_jump_unified.csx):
//    - Defines custom event: LedgeJumpedEvent
//    - Publishes event: Publish(new LedgeJumpedEvent { ... })
//
// 2. Event Listener (this script):
//    - Subscribes to event: On<LedgeJumpedEvent>(evt => { ... })
//    - Receives event data: evt.JumpDirection, evt.StartPosition, etc.
//
// 3. Benefits:
//    - Decoupled scripts: Publisher doesn't know about listeners
//    - Multiple listeners: Any script can subscribe to same event
//    - Type-safe: Compile-time checking of event properties
//    - Extensible: Add new listeners without modifying publisher
//
// 4. Use Cases:
//    - Achievement tracking
//    - Statistics collection
//    - Quest progression
//    - Tutorial hints
//    - Special effects/rewards
