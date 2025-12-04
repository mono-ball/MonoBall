using Arch.Core;
using PokeSharp.Game.Components.Tiles;

namespace PokeSharp.Game.Data.PropertyMapping;

/// <summary>
///     Maps Tiled properties to TerrainType components.
///     Handles "terrain_type" and "footstep_sound" properties for visual/audio terrain effects.
/// </summary>
public class TerrainTypeMapper : IEntityPropertyMapper<TerrainType>
{
    public bool CanMap(Dictionary<string, object> properties)
    {
        return properties.ContainsKey("terrain_type");
    }

    public TerrainType Map(Dictionary<string, object> properties)
    {
        if (!CanMap(properties))
        {
            throw new InvalidOperationException("Cannot map properties to TerrainType component");
        }

        if (!properties.TryGetValue("terrain_type", out object? terrainValue))
        {
            throw new InvalidOperationException("terrain_type property is required");
        }

        string? terrainType = terrainValue?.ToString();
        if (string.IsNullOrWhiteSpace(terrainType))
        {
            throw new InvalidOperationException("terrain_type property is empty or whitespace");
        }

        // Get footstep sound (optional, defaults to empty string)
        string footstepSound = properties.TryGetValue("footstep_sound", out object? soundValue)
            ? soundValue?.ToString() ?? ""
            : "";

        return new TerrainType(terrainType, footstepSound);
    }

    public void MapAndAdd(World world, Entity entity, Dictionary<string, object> properties)
    {
        if (CanMap(properties))
        {
            TerrainType terrainType = Map(properties);
            world.Add(entity, terrainType);
        }
    }
}
