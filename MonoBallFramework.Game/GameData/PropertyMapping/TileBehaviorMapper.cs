using Arch.Core;
using MonoBallFramework.Game.Ecs.Components.Tiles;
using MonoBallFramework.Game.Engine.Core.Types;

namespace MonoBallFramework.Game.GameData.PropertyMapping;

/// <summary>
///     Maps Tiled properties to TileBehavior components.
///     Handles "behavior_type" or "tile_behavior" property.
/// </summary>
public class TileBehaviorMapper : IEntityPropertyMapper<TileBehavior>
{
    public bool CanMap(Dictionary<string, object> properties)
    {
        return properties.ContainsKey("behavior_type") || properties.ContainsKey("tile_behavior");
    }

    public TileBehavior Map(Dictionary<string, object> properties)
    {
        if (!CanMap(properties))
        {
            throw new InvalidOperationException("Cannot map properties to TileBehavior component");
        }

        string? behaviorTypeId = null;

        // Try "behavior_type" first, then "tile_behavior"
        if (properties.TryGetValue("behavior_type", out object? behaviorValue))
        {
            behaviorTypeId = behaviorValue?.ToString();
        }
        else if (properties.TryGetValue("tile_behavior", out object? tileBehaviorValue))
        {
            behaviorTypeId = tileBehaviorValue?.ToString();
        }

        if (string.IsNullOrWhiteSpace(behaviorTypeId))
        {
            throw new InvalidOperationException(
                "behavior_type or tile_behavior property is empty or whitespace"
            );
        }

        // Parse the behavior type ID - try full format first, then fallback to simple name
        GameTileBehaviorId behaviorId = GameTileBehaviorId.TryCreate(behaviorTypeId)
                                        ?? GameTileBehaviorId.CreateMovement(behaviorTypeId);

        return new TileBehavior { BehaviorId = behaviorId, IsActive = true, IsInitialized = false };
    }

    public void MapAndAdd(World world, Entity entity, Dictionary<string, object> properties)
    {
        if (CanMap(properties))
        {
            TileBehavior behavior = Map(properties);
            world.Add(entity, behavior);
        }
    }
}
