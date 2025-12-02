#load "UnifiedScriptBase.cs"

using PokeSharp.Scripting.Unified;
using System;

/// <summary>
/// Tall grass behavior - trigger random wild Pokemon encounters
/// Demonstrates: Event-driven tile behavior, random chance, persistent state
///
/// OLD SYSTEM: Required TileBehaviorScriptBase
/// NEW SYSTEM: Same UnifiedScriptBase as everything else!
/// </summary>
public class TallGrassScript : UnifiedScriptBase
{
    private const float ENCOUNTER_CHANCE = 0.1f; // 10% per step
    private const int STEPS_BEFORE_GUARANTEED_ENCOUNTER = 20;
    private static Random _random = new Random();

    public override void Initialize()
    {
        // Subscribe to player entering this tile
        SubscribeWhen<PlayerMoveEvent>(
            evt => evt.ToPosition == Target.Position,
            HandlePlayerEntered
        );

        // Subscribe to player leaving this tile
        SubscribeWhen<PlayerMoveEvent>(
            evt => evt.FromPosition == Target.Position,
            HandlePlayerExited
        );

        // Initialize encounter tracking
        Set("steps_since_encounter", 0);
        Set("total_encounters", 0);

        Log("Tall grass initialized at " + Target.Position);
    }

    private void HandlePlayerEntered(PlayerMoveEvent evt)
    {
        // Play rustling grass animation
        Publish(new PlayAnimationEvent
        {
            AnimationName = "grass_rustle",
            Position = Target.Position
        });

        // Play sound
        Publish(new PlaySoundEvent { SoundName = "grass_rustle" });

        // Check for wild encounter
        CheckForEncounter(evt.Player);
    }

    private void HandlePlayerExited(PlayerMoveEvent evt)
    {
        // Stop grass animation
        Publish(new StopAnimationEvent
        {
            AnimationName = "grass_rustle",
            Position = Target.Position
        });
    }

    private void CheckForEncounter(IPlayer player)
    {
        // Check if player has repel active
        if (PlayerHasActiveRepel())
        {
            Log("Repel is active, no encounter");
            return;
        }

        // Get steps since last encounter (persisted across game)
        int stepsSinceEncounter = Get("steps_since_encounter", 0);
        stepsSinceEncounter++;
        Set("steps_since_encounter", stepsSinceEncounter);

        // Guaranteed encounter after X steps
        bool guaranteedEncounter = stepsSinceEncounter >= STEPS_BEFORE_GUARANTEED_ENCOUNTER;

        // Calculate encounter chance
        float encounterRoll = (float)_random.NextDouble();
        bool shouldEncounter = guaranteedEncounter || encounterRoll < ENCOUNTER_CHANCE;

        if (shouldEncounter)
        {
            TriggerWildEncounter();
            Set("steps_since_encounter", 0);

            int totalEncounters = Get("total_encounters", 0) + 1;
            Set("total_encounters", totalEncounters);
        }
    }

    private void TriggerWildEncounter()
    {
        // Determine Pokemon based on location, time of day, etc.
        var encounterData = DetermineWildPokemon();

        Log($"Wild {encounterData.PokemonName} appeared!");

        // Publish encounter event
        Publish(new WildPokemonEncounterEvent
        {
            PokemonName = encounterData.PokemonName,
            Level = encounterData.Level,
            Location = Target.Position,
            EncounterType = "tall_grass"
        });
    }

    private EncounterData DetermineWildPokemon()
    {
        // In real implementation, this would use encounter tables
        // based on map, time of day, weather, etc.
        var pokemonOptions = new[]
        {
            new EncounterData { PokemonName = "Rattata", Level = 3, Weight = 40 },
            new EncounterData { PokemonName = "Pidgey", Level = 3, Weight = 40 },
            new EncounterData { PokemonName = "Pikachu", Level = 5, Weight = 20 }
        };

        // Weighted random selection
        int totalWeight = 0;
        foreach (var option in pokemonOptions)
            totalWeight += option.Weight;

        int roll = _random.Next(totalWeight);
        int currentWeight = 0;

        foreach (var option in pokemonOptions)
        {
            currentWeight += option.Weight;
            if (roll < currentWeight)
                return option;
        }

        return pokemonOptions[0]; // Fallback
    }

    private bool PlayerHasActiveRepel()
    {
        // Check player's active item effects
        // This would query the player's status effects
        return false; // Placeholder
    }

    private void Log(string message)
    {
        Publish(new LogEvent { Message = $"[TallGrass] {message}" });
    }
}

// Supporting types
public class EncounterData
{
    public string PokemonName { get; set; }
    public int Level { get; set; }
    public int Weight { get; set; }
}

public class PlayAnimationEvent : IGameEvent
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string AnimationName { get; set; }
    public Point Position { get; set; }
}

public class StopAnimationEvent : IGameEvent
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string AnimationName { get; set; }
    public Point Position { get; set; }
}

public class WildPokemonEncounterEvent : IGameEvent
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string PokemonName { get; set; }
    public int Level { get; set; }
    public Point Location { get; set; }
    public string EncounterType { get; set; }
}

return new TallGrassScript();
