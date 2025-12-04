using Arch.Core;
using MonoBallFramework.Engine.Core.Events.System;
using MonoBallFramework.Game.Scripting.Runtime;

/// <summary>
/// Template for creating item behavior scripts.
///
/// INSTRUCTIONS:
/// 1. Copy this file to your mod's Scripts folder
/// 2. Rename the class to match your item (e.g., PotionScript, RareCandyScript)
/// 3. Update the TODO sections with your custom logic
/// 4. Define whether the item is consumable or permanent
/// 5. Implement use effects and restrictions
///
/// COMMON USE CASES:
/// - Healing items (Potion, Full Restore)
/// - Status cure items (Antidote, Awakening)
/// - Stat boosters (Rare Candy, Protein)
/// - Key items (HMs, event items)
/// - Battle items (Pokéball, X Attack)
/// - Evolution items (Fire Stone, Link Cable)
/// </summary>
public class TemplateItemScript : ScriptBase
{
    // ============================================================================
    // ITEM CONFIGURATION
    // ============================================================================
    // TODO: Define your item's properties

    // Item type (consumable vs permanent)
    private const bool IS_CONSUMABLE = true;

    // Item category (determines when it can be used)
    private enum ItemCategory
    {
        Medicine, // HP/status restoration
        StatBooster, // Permanent stat increases
        Evolution, // Causes Pokemon evolution
        Battle, // Usable in battle only
        KeyItem, // Cannot be consumed, special purpose
        Ball, // Pokemon capture
    }

    private const ItemCategory ITEM_TYPE = ItemCategory.Medicine;

    /// <summary>
    /// Initialize the item script.
    /// </summary>
    public override void Initialize(ScriptContext ctx)
    {
        base.Initialize(ctx); // REQUIRED: Initializes Context property

        // TODO: Set up item configuration
        // Examples:
        // Set("heal_amount", 20); // How much HP to restore
        // Set("cure_status", "poison"); // Which status to cure
        // Set("stat_boost", "attack"); // Which stat to boost
        // Set("use_cooldown", 0f); // Prevent spam usage

        ctx.Logger.LogInformation("TemplateItemScript initialized");
    }

    /// <summary>
    /// Register event handlers for item usage.
    /// </summary>
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // ========================================================================
        // EVENT: Item Used (PRIMARY)
        // ========================================================================
        // This event fires when the player attempts to use the item

        // TODO: Uncomment and implement item use logic
        /*
        On<ItemUsedEvent>(evt =>
        {
            // Check if this is our item
            if (evt.ItemId != "your_item_id") return;

            var playerEntity = evt.UserEntity;
            var targetEntity = evt.TargetEntity; // Pokemon being targeted, if any

            Context.Logger.LogInformation("Player used item: {ItemId}", evt.ItemId);

            // Check if item can be used in current context
            if (!CanUseItem(playerEntity, targetEntity))
            {
                evt.PreventDefault("Cannot use item right now");
                return;
            }

            // Apply item effect
            ApplyItemEffect(playerEntity, targetEntity);

            // Consume item if consumable
            if (IS_CONSUMABLE)
            {
                ConsumeItem(playerEntity, evt.ItemId);
            }

            Context.Logger.LogInformation("Item effect applied successfully");
        });
        */

        // ========================================================================
        // EVENT: Item Obtained
        // ========================================================================
        // Fires when the player obtains this item (pickup, purchase, gift)

        // TODO: Uncomment for item acquisition logic
        /*
        On<ItemObtainedEvent>(evt =>
        {
            if (evt.ItemId != "your_item_id") return;

            Context.Logger.LogInformation("Player obtained {ItemId} x{Quantity}", evt.ItemId, evt.Quantity);

            // TODO: Add acquisition logic
            // Examples:
            // - Show special message for key items
            // - Trigger quest progress
            // - Unlock new areas or abilities
            // - Play sound effect

            if (ITEM_TYPE == ItemCategory.KeyItem)
            {
                // Context.Dialogue.ShowMessage($"Obtained {evt.ItemId}! This seems important...");
                // UnlockRelatedContent(evt.ItemId);
            }
        });
        */

        // ========================================================================
        // EVENT: Inventory Check
        // ========================================================================
        // Check if the item can be used based on context

        // TODO: Uncomment for usage validation
        /*
        On<ItemCanUseEvent>(evt =>
        {
            if (evt.ItemId != "your_item_id") return;

            var playerEntity = evt.UserEntity;

            // Check context-specific restrictions
            bool canUse = true;
            string reason = "";

            // Example checks:
            // 1. Battle-only items can't be used in overworld
            if (ITEM_TYPE == ItemCategory.Battle && !Context.IsInBattle())
            {
                canUse = false;
                reason = "Can only be used in battle";
            }

            // 2. Healing items can't be used at full HP
            if (ITEM_TYPE == ItemCategory.Medicine)
            {
                var currentHp = GetPokemonHp(evt.TargetEntity);
                var maxHp = GetPokemonMaxHp(evt.TargetEntity);

                if (currentHp >= maxHp)
                {
                    canUse = false;
                    reason = "HP is already full";
                }
            }

            // 3. Evolution items need correct Pokemon
            if (ITEM_TYPE == ItemCategory.Evolution)
            {
                if (!CanEvolve(evt.TargetEntity))
                {
                    canUse = false;
                    reason = "Cannot be used on this Pokemon";
                }
            }

            if (!canUse)
            {
                evt.PreventDefault(reason);
            }
        });
        */

        // ========================================================================
        // EVENT: Tick (for item effects over time)
        // ========================================================================
        // Use for items that have ongoing effects

        // TODO: Uncomment for duration-based effects
        /*
        On<TickEvent>(evt =>
        {
            // Example: Handle item effect cooldowns
            var cooldown = Get<float>("use_cooldown", 0f);
            if (cooldown > 0)
            {
                Set("use_cooldown", cooldown - evt.DeltaTime);
            }
        });
        */
    }

    /// <summary>
    /// Called when the script is unloaded.
    /// </summary>
    public override void OnUnload()
    {
        Context.Logger.LogInformation("TemplateItemScript unloaded");
        base.OnUnload();
    }

    // ============================================================================
    // HELPER METHODS - Implement based on your item type
    // ============================================================================

    /// <summary>
    /// Check if the item can be used in the current context.
    /// </summary>
    private bool CanUseItem(Entity userEntity, Entity targetEntity)
    {
        // TODO: Implement usage validation
        // Examples:

        // 1. Check if in battle for battle-only items
        // if (ITEM_TYPE == ItemCategory.Battle && !Context.IsInBattle())
        // {
        //     return false;
        // }

        // 2. Check if target Pokemon is valid
        // if (targetEntity == Entity.Null)
        // {
        //     return false;
        // }

        // 3. Check cooldowns
        // if (Get<float>("use_cooldown", 0f) > 0)
        // {
        //     return false;
        // }

        return true; // Placeholder
    }

    /// <summary>
    /// Apply the item's effect to the target.
    /// </summary>
    private void ApplyItemEffect(Entity userEntity, Entity targetEntity)
    {
        // TODO: Implement your item's effect
        // Examples based on item category:

        // MEDICINE: Restore HP and/or cure status
        /*
        if (ITEM_TYPE == ItemCategory.Medicine)
        {
            var healAmount = Get<int>("heal_amount", 20);
            RestoreHp(targetEntity, healAmount);

            var cureStatus = Get<string>("cure_status", "");
            if (!string.IsNullOrEmpty(cureStatus))
            {
                CureStatus(targetEntity, cureStatus);
            }

            Context.Logger.LogInformation("Healed Pokemon for {Amount} HP", healAmount);
        }
        */

        // STAT BOOSTER: Permanently increase stats
        /*
        if (ITEM_TYPE == ItemCategory.StatBooster)
        {
            var statToBoost = Get<string>("stat_boost", "attack");
            BoostStat(targetEntity, statToBoost, 1);
            Context.Logger.LogInformation("Boosted {Stat} for Pokemon", statToBoost);
        }
        */

        // EVOLUTION: Trigger Pokemon evolution
        /*
        if (ITEM_TYPE == ItemCategory.Evolution)
        {
            if (CanEvolve(targetEntity))
            {
                TriggerEvolution(targetEntity);
                Context.Logger.LogInformation("Pokemon evolution triggered!");
            }
        }
        */

        // BATTLE: Apply battle effects
        /*
        if (ITEM_TYPE == ItemCategory.Battle)
        {
            ApplyBattleEffect(targetEntity);
        }
        */

        // BALL: Attempt to catch Pokemon
        /*
        if (ITEM_TYPE == ItemCategory.Ball)
        {
            var catchRate = Get<float>("catch_rate", 1.0f);
            AttemptCapture(userEntity, targetEntity, catchRate);
        }
        */

        Context.Logger.LogInformation("Item effect applied");
    }

    /// <summary>
    /// Remove one instance of the item from inventory.
    /// </summary>
    private void ConsumeItem(Entity userEntity, string itemId)
    {
        // TODO: Implement inventory consumption
        // Context.Inventory.RemoveItem(itemId, 1);
        Context.Logger.LogInformation("Consumed item: {ItemId}", itemId);
    }

    // ========================================================================
    // MEDICINE HELPERS
    // ========================================================================

    private void RestoreHp(Entity pokemonEntity, int amount)
    {
        // TODO: Implement HP restoration
        // var currentHp = Context.World.Get<HpComponent>(pokemonEntity);
        // currentHp.Current = Math.Min(currentHp.Current + amount, currentHp.Max);
        // Context.World.Set(pokemonEntity, currentHp);

        Context.Logger.LogInformation("Restored {Amount} HP to Pokemon", amount);
    }

    private void CureStatus(Entity pokemonEntity, string statusType)
    {
        // TODO: Implement status cure
        // if (Context.World.Has<StatusEffect>(pokemonEntity))
        // {
        //     ref var status = ref Context.World.Get<StatusEffect>(pokemonEntity);
        //     if (status.Type == statusType)
        //     {
        //         Context.World.Remove<StatusEffect>(pokemonEntity);
        //     }
        // }

        Context.Logger.LogInformation("Cured {Status} from Pokemon", statusType);
    }

    // ========================================================================
    // STAT BOOSTER HELPERS
    // ========================================================================

    private void BoostStat(Entity pokemonEntity, string statName, int amount)
    {
        // TODO: Implement stat boost
        // var stats = Context.World.Get<PokemonStats>(pokemonEntity);
        // switch (statName.ToLower())
        // {
        //     case "attack": stats.Attack += amount; break;
        //     case "defense": stats.Defense += amount; break;
        //     case "speed": stats.Speed += amount; break;
        //     // etc.
        // }
        // Context.World.Set(pokemonEntity, stats);

        Context.Logger.LogInformation("Boosted {Stat} by {Amount}", statName, amount);
    }

    // ========================================================================
    // EVOLUTION HELPERS
    // ========================================================================

    private bool CanEvolve(Entity pokemonEntity)
    {
        // TODO: Check evolution conditions
        // var pokemonData = Context.World.Get<PokemonData>(pokemonEntity);
        // return pokemonData.CanEvolve && pokemonData.EvolutionMethod == "item";

        return false; // Placeholder
    }

    private void TriggerEvolution(Entity pokemonEntity)
    {
        // TODO: Start evolution sequence
        // Context.Evolution.BeginEvolution(pokemonEntity);

        Context.Logger.LogInformation("Evolution sequence started");
    }

    // ========================================================================
    // BATTLE HELPERS
    // ========================================================================

    private void ApplyBattleEffect(Entity targetEntity)
    {
        // TODO: Implement battle effects
        // Examples:
        // - X Attack: Boost attack for this battle
        // - Guard Spec: Prevent stat reduction
        // - Dire Hit: Increase critical hit ratio

        Context.Logger.LogInformation("Battle effect applied");
    }

    // ========================================================================
    // CAPTURE HELPERS
    // ========================================================================

    private void AttemptCapture(Entity playerEntity, Entity wildPokemon, float catchRate)
    {
        // TODO: Implement capture mechanics
        // var captureChance = CalculateCaptureChance(wildPokemon, catchRate);
        // if (Context.GameState.Random() < captureChance)
        // {
        //     Context.Battle.CapturePokemon(playerEntity, wildPokemon);
        // }

        Context.Logger.LogInformation("Capture attempt with rate {Rate}", catchRate);
    }

    private int GetPokemonHp(Entity pokemonEntity)
    {
        // TODO: Get current HP
        return 0; // Placeholder
    }

    private int GetPokemonMaxHp(Entity pokemonEntity)
    {
        // TODO: Get max HP
        return 100; // Placeholder
    }
}

// ============================================================================
// CUSTOM EVENT TYPES (if needed)
// ============================================================================

// TODO: Define custom events if your item needs them
/*
public sealed record ItemUsedEvent : IGameEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public required string ItemId { get; init; }
    public required Entity UserEntity { get; init; }
    public Entity TargetEntity { get; init; }
}

public sealed record ItemObtainedEvent : IGameEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public required string ItemId { get; init; }
    public required int Quantity { get; init; }
}

public sealed record ItemCanUseEvent : ICancellableEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public required string ItemId { get; init; }
    public required Entity UserEntity { get; init; }
    public Entity TargetEntity { get; init; }
    public bool IsCancelled { get; private set; }
    public string? CancellationReason { get; private set; }

    public void PreventDefault(string? reason = null)
    {
        IsCancelled = true;
        CancellationReason = reason;
    }
}
*/

// IMPORTANT: Return an instance of your item script class
return new TemplateItemScript();
