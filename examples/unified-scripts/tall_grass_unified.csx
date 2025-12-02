// tall_grass_unified.csx
// Unified ScriptBase implementation for tall grass behavior
// Triggers random wild Pokemon encounters when player steps on grass
// Uses ScriptBase for unified event handling

using PokeSharp.Game.Scripting.Runtime;
using PokeSharp.Game.Scripting.Events;
using PokeSharp.Core.Components;
using System;
using System.Numerics;

public class TallGrassScript : ScriptBase
{
    private static readonly Random random = new Random();

    // Configuration
    public float encounterRate = 0.10f; // 10% chance per step
    public string[] wildPokemon = new[] { "Pidgey", "Rattata", "Caterpie" };
    public int minLevel = 2;
    public int maxLevel = 5;

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<TileSteppedOnEvent>(evt => {
            // Only trigger for player
            if (!ctx.Player.IsPlayerEntity(evt.Entity)) {
                return;
            }

            ctx.Logger.Info($"Tall grass: Player stepped on grass at ({evt.TileX}, {evt.TileY})");

            // Play grass rustle animation
            ctx.Effects.PlayEffect("grass_rustle", evt.TilePosition);

            // Check for wild encounter
            CheckWildEncounter(evt);
        });
    }

    private void CheckWildEncounter(TileSteppedOnEvent evt)
    {
        // Random encounter check
        if (random.NextDouble() < encounterRate) {
            ctx.Logger.Info("Tall grass: Wild encounter triggered!");
            TriggerWildBattle(evt.Entity, evt.TilePosition);
        }
    }

    private void TriggerWildBattle(Entity player, Vector2 position)
    {
        // Select random wild Pokemon
        var pokemonName = wildPokemon[random.Next(wildPokemon.Length)];
        var level = random.Next(minLevel, maxLevel + 1);

        ctx.Logger.Info($"Tall grass: Wild {pokemonName} (Lv.{level}) appeared at {position}!");

        // Play encounter music sting
        ctx.Effects.PlaySound("wild_encounter");

        // Flash screen
        ctx.Effects.FlashScreen(Color.White, duration: 0.3f);

        // Start wild battle
        ctx.GameState.StartWildBattle(pokemonName, level);
    }
}

// Return script instance
return new TallGrassScript();
