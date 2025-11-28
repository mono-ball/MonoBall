using Arch.Core;
using PokeSharp.Game.Components.Tiles;

namespace PokeSharp.Game.Data.PropertyMapping;

/// <summary>
///     Maps Tiled properties to TileScript components.
///     Handles "script" and "on_step" properties.
/// </summary>
public class ScriptMapper : IEntityPropertyMapper<TileScript>
{
    public bool CanMap(Dictionary<string, object> properties)
    {
        // Can map if has script-related properties
        return properties.ContainsKey("script") || properties.ContainsKey("on_step");
    }

    public TileScript Map(Dictionary<string, object> properties)
    {
        if (!CanMap(properties))
        {
            throw new InvalidOperationException("Cannot map properties to TileScript component");
        }

        // Get script path
        string? scriptPath = null;

        if (properties.TryGetValue("script", out object? scriptValue))
        {
            scriptPath = scriptValue?.ToString();
        }
        else if (properties.TryGetValue("on_step", out object? stepValue))
        {
            scriptPath = stepValue?.ToString();
        }

        if (string.IsNullOrWhiteSpace(scriptPath))
        {
            throw new InvalidOperationException("Script property is empty or whitespace");
        }

        return new TileScript(scriptPath);
    }

    public void MapAndAdd(World world, Entity entity, Dictionary<string, object> properties)
    {
        if (CanMap(properties))
        {
            TileScript script = Map(properties);
            world.Add(entity, script);
        }
    }
}
