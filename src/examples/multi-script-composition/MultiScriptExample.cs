// MULTI-SCRIPT COMPOSITION EXAMPLE
// Demonstrates attaching multiple scripts to the same entity/tile using ScriptAttachment component

using Arch.Core;
using Arch.Core.Extensions;
using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Components.Scripting;
using PokeSharp.Game.Components.Tiles;
using PokeSharp.Game.Scripting.Runtime;

namespace PokeSharp.Examples.MultiScript;

/// <summary>
/// Example: Ice tile with encounter rate and warp functionality.
/// Shows how multiple scripts can compose behaviors on a single tile.
/// </summary>
public static class MultiScriptExample
{
    /// <summary>
    /// Create a tile with multiple script behaviors.
    /// </summary>
    public static Entity CreateIceEncounterWarpTile(World world, int x, int y)
    {
        var entity = world.Create(
            new TilePosition { X = x, Y = y },

            // Attach 3 different scripts with different priorities
            // Higher priority executes first
            new ScriptAttachment("tiles/ice_slide.csx", priority: 10),
            new ScriptAttachment("tiles/wild_encounter.csx", priority: 5),
            new ScriptAttachment("tiles/warp.csx", priority: 1)
        );

        return entity;
    }

    /// <summary>
    /// Query all scripts attached to an entity (in priority order).
    /// </summary>
    public static void ListScriptsOnTile(World world, Entity tileEntity)
    {
        var attachments = new List<ScriptAttachment>();

        // Collect all ScriptAttachment components
        world.Query(
            new QueryDescription().WithAll<ScriptAttachment>(),
            (Entity e, ref ScriptAttachment attachment) =>
            {
                if (e == tileEntity)
                {
                    attachments.Add(attachment);
                }
            }
        );

        // Sort by priority (highest first)
        attachments.Sort((a, b) => b.Priority.CompareTo(a.Priority));

        Console.WriteLine($"Scripts on tile {tileEntity.Id}:");
        foreach (var attachment in attachments)
        {
            Console.WriteLine($"  [{attachment.Priority}] {attachment.ScriptPath} (Active: {attachment.IsActive})");
        }
    }

    /// <summary>
    /// Dynamically add a script to an existing tile.
    /// </summary>
    public static void AddScriptToTile(World world, Entity tileEntity, string scriptPath, int priority)
    {
        // Add another ScriptAttachment component
        // Arch ECS allows multiple components of the same type
        tileEntity.Add(new ScriptAttachment(scriptPath, priority));

        Console.WriteLine($"Added script '{scriptPath}' with priority {priority} to tile {tileEntity.Id}");
    }

    /// <summary>
    /// Temporarily disable a specific script without removing it.
    /// </summary>
    public static void DisableScript(World world, Entity tileEntity, string scriptPath)
    {
        world.Query(
            new QueryDescription().WithAll<ScriptAttachment>(),
            (Entity e, ref ScriptAttachment attachment) =>
            {
                if (e == tileEntity && attachment.ScriptPath == scriptPath)
                {
                    attachment.IsActive = false;
                    Console.WriteLine($"Disabled script '{scriptPath}' on tile {tileEntity.Id}");
                }
            }
        );
    }

    /// <summary>
    /// Re-enable a disabled script.
    /// </summary>
    public static void EnableScript(World world, Entity tileEntity, string scriptPath)
    {
        world.Query(
            new QueryDescription().WithAll<ScriptAttachment>(),
            (Entity e, ref ScriptAttachment attachment) =>
            {
                if (e == tileEntity && attachment.ScriptPath == scriptPath)
                {
                    attachment.IsActive = true;
                    Console.WriteLine($"Enabled script '{scriptPath}' on tile {tileEntity.Id}");
                }
            }
        );
    }
}

/// <summary>
/// Example ice slide script that forces continued movement.
/// </summary>
public class IceSlideScript : TypeScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // Subscribe to movement events
        OnMovementCompleted(ctx, evt =>
        {
            // Force continued movement in same direction
            ctx.Logger?.LogInformation("Ice slide: Continuing movement...");
        });
    }
}

/// <summary>
/// Example wild encounter script that triggers battles.
/// </summary>
public class WildEncounterScript : TypeScriptBase
{
    private float _encounterRate = 0.1f;

    public override void OnInitialize(ScriptContext ctx)
    {
        // Load encounter rate from tile properties
        if (ctx.TryGetProperty("encounterRate", out float rate))
        {
            _encounterRate = rate;
        }
    }

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        OnTileSteppedOn(ctx, evt =>
        {
            // Roll for encounter
            if (Random.Shared.NextSingle() < _encounterRate)
            {
                ctx.Logger?.LogInformation("Wild encounter triggered!");
                // TODO: Trigger battle event
            }
        });
    }
}

/// <summary>
/// Example warp script that teleports the player.
/// </summary>
public class WarpScript : TypeScriptBase
{
    private int _destMapId;
    private int _destX;
    private int _destY;

    public override void OnInitialize(ScriptContext ctx)
    {
        // Load warp destination from tile properties
        _destMapId = ctx.GetProperty<int>("destMapId");
        _destX = ctx.GetProperty<int>("destX");
        _destY = ctx.GetProperty<int>("destY");
    }

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        OnTileSteppedOn(ctx, evt =>
        {
            ctx.Logger?.LogInformation($"Warping to map {_destMapId} at ({_destX}, {_destY})");
            // TODO: Execute warp
        });
    }
}

/// <summary>
/// Example: How scripts execute in priority order.
/// </summary>
public static class PriorityExecutionExample
{
    public static void DemonstratePriorityOrder(World world)
    {
        Console.WriteLine("=== Priority Execution Order ===");
        Console.WriteLine("Higher priority scripts execute first:\n");

        var tile = world.Create(
            new TilePosition { X = 5, Y = 5 },
            new ScriptAttachment("high_priority.csx", priority: 100),
            new ScriptAttachment("medium_priority.csx", priority: 50),
            new ScriptAttachment("low_priority.csx", priority: 10)
        );

        Console.WriteLine("Execution order when player steps on tile:");
        Console.WriteLine("1. high_priority.csx (priority: 100)");
        Console.WriteLine("2. medium_priority.csx (priority: 50)");
        Console.WriteLine("3. low_priority.csx (priority: 10)");

        Console.WriteLine("\nUse cases:");
        Console.WriteLine("- Priority 100: Critical blocking behaviors (warps, cutscenes)");
        Console.WriteLine("- Priority 50: Normal behaviors (ice, conveyors, encounters)");
        Console.WriteLine("- Priority 10: Cosmetic effects (particles, sounds)");
    }
}

/// <summary>
/// Example: Event-driven composition pattern.
/// All scripts can react to the same event independently.
/// </summary>
public static class EventDrivenCompositionExample
{
    public static void DemonstrateEventComposition(World world)
    {
        Console.WriteLine("=== Event-Driven Composition ===");
        Console.WriteLine("All scripts receive events independently:\n");

        var tile = world.Create(
            new TilePosition { X = 3, Y = 3 },
            new ScriptAttachment("tiles/play_sound.csx"),      // Plays footstep sound
            new ScriptAttachment("tiles/particle_effect.csx"), // Shows grass particles
            new ScriptAttachment("tiles/encounter_check.csx")  // Checks for wild encounter
        );

        Console.WriteLine("When player steps on grass tile:");
        Console.WriteLine("- play_sound.csx: Plays 'grass_step.wav'");
        Console.WriteLine("- particle_effect.csx: Spawns grass particle effects");
        Console.WriteLine("- encounter_check.csx: Rolls for wild Pokemon encounter");
        Console.WriteLine("\nAll scripts execute independently without interfering!");
    }
}

/// <summary>
/// Example: Dynamic script management at runtime.
/// </summary>
public static class DynamicScriptManagementExample
{
    public static void DemonstrateDynamicManagement(World world)
    {
        Console.WriteLine("=== Dynamic Script Management ===\n");

        var tile = world.Create(
            new TilePosition { X = 10, Y = 10 },
            new ScriptAttachment("tiles/base_behavior.csx")
        );

        Console.WriteLine("Initial: 1 script attached");

        // Add script dynamically based on game state
        if (PlayerHasSurfAbility())
        {
            tile.Add(new ScriptAttachment("tiles/surf_allowed.csx", priority: 20));
            Console.WriteLine("+ Added surf_allowed.csx (player learned Surf!)");
        }

        // Temporarily disable script during cutscene
        Console.WriteLine("+ Disabling encounters during cutscene...");
        MultiScriptExample.DisableScript(world, tile, "tiles/encounter_check.csx");

        // Re-enable after cutscene
        Console.WriteLine("+ Re-enabling encounters after cutscene");
        MultiScriptExample.EnableScript(world, tile, "tiles/encounter_check.csx");
    }

    private static bool PlayerHasSurfAbility() => true; // Placeholder
}
