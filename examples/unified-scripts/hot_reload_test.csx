// hot_reload_test.csx
// Example script demonstrating hot-reload capabilities
// Edit this file while game is running to see changes immediately

using PokeSharp.Game.Scripting.Runtime;
using PokeSharp.Game.Scripting.Events;
using PokeSharp.Core.Components;
using System;
using System.Numerics;

public class HotReloadTestScript : ScriptBase
{
    // Try changing these values while the game is running!
    public string welcomeMessage = "Hot reload is working! 🔥";
    public float effectDuration = 1.0f;
    public string soundEffect = "chime";
    public int version = 1; // Increment this to track reloads

    public override void OnInitialize(ScriptContext ctx)
    {
        base.OnInitialize(ctx);

        ctx.Logger.Info($"=== HOT RELOAD TEST v{version} ===");
        ctx.Logger.Info(welcomeMessage);
        ctx.Logger.Info("Try editing this file while the game is running!");
    }

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<TileSteppedOnEvent>(evt => {
            if (!ctx.Player.IsPlayerEntity(evt.Entity)) return;

            // Display the welcome message
            ctx.Logger.Info($"[Hot Reload v{version}] {welcomeMessage}");
            ctx.Logger.Info($"Position: ({evt.TileX}, {evt.TileY})");

            // Play the configured sound effect
            ctx.Effects.PlaySound(soundEffect);

            // Play an effect with configured duration
            ctx.Effects.PlayEffect("sparkle", evt.TilePosition, effectDuration);

            // Try adding new behavior here!
            // Example: Uncomment to add screen flash
            // ctx.Effects.FlashScreen(Color.Cyan, duration: 0.3f);
        });

        // Try adding a new event handler!
        // Example: Uncomment to react to movement
        /*
        On<MovementCompletedEvent>(evt => {
            if (ctx.Player.IsPlayerEntity(evt.Entity)) {
                ctx.Logger.Info($"[Hot Reload v{version}] Player moved to {evt.NewPosition}");
            }
        });
        */
    }

    // Try adding new methods!
    /*
    private void CustomBehavior(Entity entity)
    {
        ctx.Logger.Info("Custom behavior added via hot reload!");
        ctx.Effects.PlaySound("custom_sound");
    }
    */
}

// Return script instance
return new HotReloadTestScript();

// ========================================
// HOT RELOAD TESTING INSTRUCTIONS
// ========================================
//
// 1. Start the game with this script attached to a tile
// 2. Walk onto the tile - observe initial behavior
// 3. While game is running, try these edits:
//
//    a) Change welcomeMessage to something new
//    b) Change soundEffect to different sound
//    c) Increment version number
//    d) Uncomment the movement handler
//    e) Add a new event handler
//    f) Change effectDuration
//
// 4. Save the file
// 5. Walk onto the tile again - see immediate changes!
//
// ========================================
// WHAT SHOULD HAPPEN:
// ========================================
//
// - Changes appear immediately without restart
// - Logger shows new version number
// - New event handlers start working
// - Modified configuration takes effect
// - Old script instance is cleanly disposed
// - New script instance is created
//
// ========================================
// HOT RELOAD LIMITATIONS:
// ========================================
//
// - Cannot change class name (script identity)
// - Cannot change base type (must stay ScriptBase)
// - Static state may not reset
// - Active coroutines/tasks are terminated
//
// ========================================
// DEBUGGING HOT RELOAD:
// ========================================
//
// If hot reload doesn't work:
// 1. Check console for compilation errors
// 2. Verify file watcher is enabled
// 3. Check script attachment is active
// 4. Look for syntax errors in new code
// 5. Ensure script path hasn't changed
//
