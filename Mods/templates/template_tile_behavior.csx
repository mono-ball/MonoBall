using Arch.Core;
using Microsoft.Xna.Framework;
using MonoBallFramework.Engine.Core.Events.System;
using MonoBallFramework.Engine.Core.Events.Tile;
using MonoBallFramework.Game.Scripting.Runtime;

/// <summary>
/// Template for creating tile behavior scripts.
///
/// INSTRUCTIONS:
/// 1. Copy this file to your mod's Scripts folder
/// 2. Rename the class to match your tile behavior (e.g., LavaTileBehavior)
/// 3. Update the TODO sections with your custom logic
/// 4. Test with different entity types and edge cases
///
/// COMMON USE CASES:
/// - Trigger encounters (tall grass)
/// - Apply status effects (poison swamp)
/// - Transport entities (warp tiles, ice slides)
/// - Play animations/sounds on step
/// - Grant items or experience
/// - Unlock areas or events
/// </summary>
public class TemplateTileBehavior : ScriptBase
{
    // ============================================================================
    // CONFIGURATION SECTION
    // ============================================================================
    // TODO: Define your tile behavior properties here
    // Examples:
    // - Encounter rates
    // - Status effects to apply
    // - Cooldown timers
    // - Activation conditions

    /// <summary>
    /// Initialize the script and set up default state.
    /// This is called once when the script is loaded.
    /// </summary>
    public override void Initialize(ScriptContext ctx)
    {
        base.Initialize(ctx); // REQUIRED: Initializes Context property

        // TODO: Initialize any default state values here
        // Example:
        // Set("encounter_rate", 0.1f);
        // Set("cooldown_timer", 0f);

        ctx.Logger.LogInformation("TemplateTileBehavior initialized");
    }

    /// <summary>
    /// Register event handlers for tile interactions.
    /// This is called after Initialize.
    /// </summary>
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // ========================================================================
        // EVENT: Tile Stepped On (MOST COMMON)
        // ========================================================================
        // This event fires when an entity steps onto a tile.
        // Use this for:
        // - Triggering encounters
        // - Applying effects
        // - Playing sounds/animations

        On<TileSteppedOnEvent>(evt =>
        {
            // TODO: Check if this is the correct tile type
            // if (evt.TileType != "your_tile_type") return;

            // TODO: Check if the entity is the player
            // var playerEntity = Context.Player.GetPlayerEntity();
            // if (evt.Entity != playerEntity) return;

            // TODO: Add your tile step logic here
            // Examples:

            // 1. Trigger random encounters
            // var encounterRate = Get<float>("encounter_rate", 0.1f);
            // if (Context.GameState.Random() < encounterRate)
            // {
            //     Context.Logger.LogInformation("Wild Pokemon appeared at ({X}, {Y})!", evt.TileX, evt.TileY);
            //     TriggerEncounter(evt.Entity);
            // }

            // 2. Apply status effects
            // Context.Logger.LogInformation("Entity stepped on special tile at ({X}, {Y})", evt.TileX, evt.TileY);
            // ApplyStatusEffect(evt.Entity, "poison");

            // 3. Warp to another location
            // Context.Logger.LogInformation("Warping entity to new location");
            // Context.Map.TransitionToMap(2, 10, 15);

            Context.Logger.LogInformation(
                "Entity {Entity} stepped on tile at ({X}, {Y}) - Type: {Type}",
                evt.Entity.Id,
                evt.TileX,
                evt.TileY,
                evt.TileType
            );
        });

        // ========================================================================
        // EVENT: Tile-Specific Stepped On (FILTERED BY POSITION)
        // ========================================================================
        // Use OnTile<> to only trigger at specific coordinates
        // Useful for unique tiles like warp points or special triggers

        // TODO: Uncomment and customize for position-specific behavior
        /*
        var specialTilePosition = new Vector2(10, 15);
        OnTile<TileSteppedOnEvent>(specialTilePosition, evt =>
        {
            Context.Logger.LogInformation("Stepped on special tile at ({X}, {Y})!", evt.TileX, evt.TileY);

            // TODO: Add position-specific logic
            // Example: Warp to secret area
            // Context.Map.TransitionToMap(99, 5, 5);
        });
        */

        // ========================================================================
        // EVENT: Tile Stepped Off
        // ========================================================================
        // Use this to clean up effects or restore state when leaving a tile

        // TODO: Uncomment if you need stepped-off logic
        /*
        On<TileSteppedOffEvent>(evt =>
        {
            if (evt.TileType != "your_tile_type") return;

            Context.Logger.LogInformation("Entity left tile at ({X}, {Y})", evt.TileX, evt.TileY);

            // TODO: Add cleanup logic
            // Example: Remove visual effects, stop sounds, etc.
        });
        */

        // ========================================================================
        // EVENT: Collision Check (PREVENT ENTRY)
        // ========================================================================
        // Use this to prevent entities from entering tiles
        // This event is CANCELLABLE

        // TODO: Uncomment if you need to block tile entry
        /*
        On<TileSteppedOnEvent>(evt =>
        {
            if (evt.TileType != "your_tile_type") return;

            // Example: Block entry without required item
            var hasRequiredItem = CheckPlayerHasItem("Surf_HM");
            if (!hasRequiredItem)
            {
                evt.PreventDefault("You need the Surf HM to cross water!");
                Context.Logger.LogInformation("Blocked entity from entering tile at ({X}, {Y})", evt.TileX, evt.TileY);
                return;
            }
        }, priority: 1000); // High priority to block before other handlers
        */

        // ========================================================================
        // EVENT: Tick (CONTINUOUS UPDATES)
        // ========================================================================
        // Use this for effects that need to update every frame
        // while the entity is on the tile

        // TODO: Uncomment if you need continuous updates
        /*
        On<TickEvent>(evt =>
        {
            // Check if entity is on this tile type
            ref var position = ref Context.Position;

            // TODO: Add per-frame logic
            // Example: Slow movement on ice, damage over time in lava

            var cooldown = Get<float>("cooldown_timer", 0f);
            if (cooldown > 0)
            {
                Set("cooldown_timer", cooldown - evt.DeltaTime);
            }
        });
        */
    }

    /// <summary>
    /// Called when the script is unloaded (hot reload or shutdown).
    /// Use this to clean up resources and state.
    /// </summary>
    public override void OnUnload()
    {
        // TODO: Add any cleanup logic here
        // Example: Remove visual effects, stop sounds, clear state

        Context.Logger.LogInformation("TemplateTileBehavior unloaded");

        base.OnUnload(); // REQUIRED: Cleans up event subscriptions
    }

    // ============================================================================
    // HELPER METHODS
    // ============================================================================
    // TODO: Add your helper methods here
    // Keep them organized and well-documented

    /// <summary>
    /// Example helper method: Trigger a random encounter.
    /// </summary>
    /// <param name="entity">The entity to trigger the encounter for</param>
    private void TriggerEncounter(Entity entity)
    {
        // TODO: Implement your encounter logic
        // Example:
        // var encounterData = new WildEncounterEvent
        // {
        //     Entity = entity,
        //     PokemonSpecies = "Pikachu",
        //     Level = 5
        // };
        // Publish(encounterData);

        Context.Logger.LogInformation("Encounter triggered for entity {Entity}", entity.Id);
    }

    /// <summary>
    /// Example helper method: Check if player has a specific item.
    /// </summary>
    /// <param name="itemId">The item ID to check</param>
    /// <returns>True if the player has the item</returns>
    private bool CheckPlayerHasItem(string itemId)
    {
        // TODO: Implement item check logic
        // Example:
        // return Context.Inventory.HasItem(itemId);

        Context.Logger.LogDebug("Checking for item: {ItemId}", itemId);
        return false; // Placeholder
    }

    /// <summary>
    /// Example helper method: Apply a status effect to an entity.
    /// </summary>
    /// <param name="entity">The entity to apply the effect to</param>
    /// <param name="effectType">The type of effect (poison, burn, etc.)</param>
    private void ApplyStatusEffect(Entity entity, string effectType)
    {
        // TODO: Implement status effect logic
        // Example:
        // Context.World.Add(entity, new StatusEffect { Type = effectType });

        Context.Logger.LogInformation("Applied {Effect} to entity {Entity}", effectType, entity.Id);
    }
}

// ============================================================================
// STATE COMPONENTS (OPTIONAL)
// ============================================================================
// TODO: Define custom state components if you need to store tile-specific data
// Components are stored per-entity and are more efficient than dictionaries

// Example state component:
/*
public struct YourTileState
{
    public float CooldownTimer;
    public int StepCount;
    public bool HasTriggered;
}
*/

// IMPORTANT: Return an instance of your behavior class
return new TemplateTileBehavior();
