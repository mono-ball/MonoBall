using Arch.Core;
using PokeSharp.Game.Components.NPCs;

namespace PokeSharp.Game.Data.PropertyMapping;

/// <summary>
///     Maps Tiled properties to NPC components.
///     Handles "trainer", "pokemon_id", "view_range", and "npcId" properties.
/// </summary>
public class NpcMapper : IEntityPropertyMapper<Npc>
{
    public bool CanMap(Dictionary<string, object> properties)
    {
        // Can map if has NPC-related properties
        return properties.ContainsKey("trainer")
            || properties.ContainsKey("pokemon_id")
            || properties.ContainsKey("view_range")
            || properties.ContainsKey("npcId");
    }

    public Npc Map(Dictionary<string, object> properties)
    {
        if (!CanMap(properties))
        {
            throw new InvalidOperationException("Cannot map properties to Npc component");
        }

        // Get NPC ID (required)
        string npcId = properties.TryGetValue("npcId", out object? idValue)
            ? idValue?.ToString() ?? "unknown"
            : "unknown";

        var npc = new Npc(npcId);

        // Check if this NPC is a trainer
        if (properties.TryGetValue("trainer", out object? trainerValue))
        {
            npc.IsTrainer = trainerValue switch
            {
                bool b => b,
                string s => bool.TryParse(s, out bool result) && result,
                _ => false,
            };
        }

        // Get view range
        if (properties.TryGetValue("view_range", out object? rangeValue))
        {
            npc.ViewRange = rangeValue switch
            {
                int i => i,
                string s when int.TryParse(s, out int result) => result,
                _ => 0,
            };
        }

        return npc;
    }

    public void MapAndAdd(World world, Entity entity, Dictionary<string, object> properties)
    {
        if (CanMap(properties))
        {
            Npc npc = Map(properties);
            world.Add(entity, npc);
        }
    }
}
