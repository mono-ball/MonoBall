using Arch.Core;
using PokeSharp.Game.Components.NPCs;

namespace PokeSharp.Game.Data.PropertyMapping;

/// <summary>
///     Maps Tiled properties to Interaction components.
///     Handles "interaction_type", "interaction_range", "dialogue", and "script" properties.
/// </summary>
public class InteractionMapper : IEntityPropertyMapper<Interaction>
{
    public bool CanMap(Dictionary<string, object> properties)
    {
        // Can map if has interaction-related properties
        return properties.ContainsKey("interaction_type")
            || properties.ContainsKey("interaction_range")
            || properties.ContainsKey("dialogue")
            || properties.ContainsKey("on_interact");
    }

    public Interaction Map(Dictionary<string, object> properties)
    {
        if (!CanMap(properties))
        {
            throw new InvalidOperationException("Cannot map properties to Interaction component");
        }

        var interaction = new Interaction();

        // Get interaction range
        if (properties.TryGetValue("interaction_range", out object? rangeValue))
        {
            interaction.InteractionRange = rangeValue switch
            {
                int i => i,
                string s when int.TryParse(s, out int result) => result,
                _ => 1,
            };
        }

        // Get dialogue script
        if (properties.TryGetValue("dialogue", out object? dialogueValue))
        {
            interaction.DialogueScript = dialogueValue?.ToString();
        }

        // Get interaction event
        if (properties.TryGetValue("on_interact", out object? eventValue))
        {
            interaction.InteractionEvent = eventValue?.ToString();
        }

        // Check if facing is required (default true)
        if (properties.TryGetValue("requires_facing", out object? facingValue))
        {
            interaction.RequiresFacing = facingValue switch
            {
                bool b => b,
                string s => !bool.TryParse(s, out bool result) || result, // Default true
                _ => true,
            };
        }

        return interaction;
    }

    public void MapAndAdd(World world, Entity entity, Dictionary<string, object> properties)
    {
        if (CanMap(properties))
        {
            Interaction interaction = Map(properties);
            world.Add(entity, interaction);
        }
    }
}
