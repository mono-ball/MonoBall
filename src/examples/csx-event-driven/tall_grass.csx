// tall_grass.csx
// Event-driven tall grass behavior with wild Pokemon encounters
// Triggers random wild battles when player steps on grass

public class TallGrass : TileBehaviorScriptBase {
    private static readonly Random random = new Random();

    // Configuration
    public float encounterRate = 0.10f; // 10% chance per step
    public string[] wildPokemon = new[] { "Pidgey", "Rattata", "Caterpie" };
    public int minLevel = 2;
    public int maxLevel = 5;

    public override void RegisterEventHandlers(ScriptContext ctx) {
        OnTileSteppedOn(evt => {
            // Only trigger for player
            if (!ctx.Player.IsPlayerEntity(evt.Entity)) {
                return;
            }

            // Play grass rustle animation
            ctx.Effects.PlayEffect("grass_rustle", evt.TilePosition);

            // Check for wild encounter
            CheckWildEncounter(evt);
        });
    }

    private void CheckWildEncounter(TileSteppedOnEvent evt) {
        // Random encounter check
        if (random.NextDouble() < encounterRate) {
            TriggerWildBattle(evt.Entity, evt.TilePosition);
        }
    }

    private void TriggerWildBattle(Entity player, Vector2 position) {
        // Select random wild Pokemon
        var pokemonName = wildPokemon[random.Next(wildPokemon.Length)];
        var level = random.Next(minLevel, maxLevel + 1);

        // Play encounter music sting
        ctx.Effects.PlaySound("wild_encounter");

        // Flash screen
        ctx.Effects.FlashScreen(Color.White, duration: 0.3f);

        // Start wild battle
        ctx.GameState.StartWildBattle(pokemonName, level);

        // Log encounter (for debugging)
        Console.WriteLine($"Wild {pokemonName} (Lv.{level}) appeared at {position}!");
    }

    // Optional: Override default step sound
    public override void OnStepOn(Entity entity) {
        if (ctx.Player.IsPlayerEntity(entity)) {
            ctx.Effects.PlaySound("grass_step");
        }
    }
}
