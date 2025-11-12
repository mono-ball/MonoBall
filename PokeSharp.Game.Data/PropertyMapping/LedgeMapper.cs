using Arch.Core;
using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Components.Tiles;

namespace PokeSharp.Game.Data.PropertyMapping;

/// <summary>
///     Maps Tiled properties to TileLedge components.
///     Handles "ledge_direction" property for one-way ledge jumping.
/// </summary>
public class LedgeMapper : IEntityPropertyMapper<TileLedge>
{
    public bool CanMap(Dictionary<string, object> properties)
    {
        return properties.ContainsKey("ledge_direction");
    }

    public TileLedge Map(Dictionary<string, object> properties)
    {
        if (!CanMap(properties))
            throw new InvalidOperationException("Cannot map properties to TileLedge component");

        if (!properties.TryGetValue("ledge_direction", out var ledgeValue))
            throw new InvalidOperationException("ledge_direction property is required");

        var ledgeDir = ledgeValue switch
        {
            string s => s,
            _ => ledgeValue?.ToString(),
        };

        if (string.IsNullOrWhiteSpace(ledgeDir))
            throw new InvalidOperationException("ledge_direction property is empty or whitespace");

        var jumpDirection = ledgeDir.ToLower() switch
        {
            "south" or "down" => Direction.South,
            "north" or "up" => Direction.North,
            "west" or "left" => Direction.West,
            "east" or "right" => Direction.East,
            _ => throw new InvalidOperationException(
                $"Invalid ledge_direction value: '{ledgeDir}'. Valid values: south/down, north/up, west/left, east/right")
        };

        return new TileLedge(jumpDirection);
    }

    public void MapAndAdd(World world, Entity entity, Dictionary<string, object> properties)
    {
        if (CanMap(properties))
        {
            var ledge = Map(properties);
            world.Add(entity, ledge);
        }
    }
}
