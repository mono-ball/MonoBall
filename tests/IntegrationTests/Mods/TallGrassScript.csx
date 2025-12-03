// Tall Grass Script - Example Test Mod
// Triggers random wild Pokemon encounters in tall grass

using PokeSharp.Game.Scripting.Runtime;
using PokeSharp.Engine.Core.Events.Tile;
using PokeSharp.Engine.Core.Events;

public class TallGrassScript : ScriptBase
{
    // Custom event for wild encounters
    public sealed record WildEncounterTriggeredEvent : IGameEvent
    {
        public Guid EventId { get; init; } = Guid.NewGuid();
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
        public required Entity PlayerEntity { get; init; }
        public int TileX { get; init; }
        public int TileY { get; init; }
        public float EncounterRate { get; init; }
    }

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<TileSteppedOnEvent>(evt =>
        {
            if (evt.TileType == "tall_grass")
            {
                Context.Logger?.LogInformation(
                    "[TallGrassScript] Player in tall grass at ({X}, {Y})",
                    evt.TileX,
                    evt.TileY
                );

                // Roll for encounter
                var encounterRate = 0.10f; // 10% chance
                var roll = Random.Shared.NextDouble();

                if (roll < encounterRate)
                {
                    Context.Logger?.LogInformation(
                        "[TallGrassScript] Wild Pokemon encounter! (rolled {Roll:F3})",
                        roll
                    );

                    // Publish custom event for other systems to handle
                    Publish(new WildEncounterTriggeredEvent
                    {
                        PlayerEntity = evt.Entity,
                        TileX = evt.TileX,
                        TileY = evt.TileY,
                        EncounterRate = encounterRate
                    });
                }
            }
        }, priority: 500);
    }
}
