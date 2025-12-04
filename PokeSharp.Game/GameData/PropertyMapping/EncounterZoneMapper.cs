using Arch.Core;
using PokeSharp.Game.Components.Maps;

namespace PokeSharp.Game.Data.PropertyMapping;

/// <summary>
///     Maps Tiled properties to EncounterZone components.
///     Handles "encounter_rate" and "encounter_table" properties for wild Pokemon encounters.
/// </summary>
public class EncounterZoneMapper : IEntityPropertyMapper<EncounterZone>
{
    public bool CanMap(Dictionary<string, object> properties)
    {
        return properties.ContainsKey("encounter_rate");
    }

    public EncounterZone Map(Dictionary<string, object> properties)
    {
        if (!CanMap(properties))
        {
            throw new InvalidOperationException("Cannot map properties to EncounterZone component");
        }

        // Get encounter rate (required)
        if (!properties.TryGetValue("encounter_rate", out object? encounterRateValue))
        {
            throw new InvalidOperationException("encounter_rate property is required");
        }

        int encounterRate = encounterRateValue switch
        {
            int i => i,
            string s when int.TryParse(s, out int result) => result,
            _ => throw new InvalidOperationException(
                $"Invalid encounter_rate value: '{encounterRateValue}'. Must be an integer."
            ),
        };

        if (encounterRate < 0 || encounterRate > 255)
        {
            throw new InvalidOperationException(
                $"encounter_rate must be between 0 and 255. Got: {encounterRate}"
            );
        }

        // Get encounter table ID (optional, defaults to empty string)
        string encounterTableId = properties.TryGetValue("encounter_table", out object? tableValue)
            ? tableValue?.ToString() ?? ""
            : "";

        return new EncounterZone(encounterTableId, encounterRate);
    }

    public void MapAndAdd(World world, Entity entity, Dictionary<string, object> properties)
    {
        if (CanMap(properties))
        {
            EncounterZone encounterZone = Map(properties);
            // Only add if encounter rate is greater than 0
            if (encounterZone.EncounterRate > 0)
            {
                world.Add(entity, encounterZone);
            }
        }
    }
}
