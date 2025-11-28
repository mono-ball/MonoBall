using Arch.Core;
using PokeSharp.Game.Components.Movement;

namespace PokeSharp.Game.Data.PropertyMapping;

/// <summary>
///     Maps Tiled properties to Collision components.
///     Handles "solid" and "collidable" properties.
/// </summary>
public class CollisionMapper : IEntityPropertyMapper<Collision>
{
    public bool CanMap(Dictionary<string, object> properties)
    {
        // Can map if has "solid" or "collidable" property
        return properties.ContainsKey("solid") || properties.ContainsKey("collidable");
    }

    public Collision Map(Dictionary<string, object> properties)
    {
        if (!CanMap(properties))
        {
            throw new InvalidOperationException("Cannot map properties to Collision component");
        }

        // Check for solid property
        if (properties.TryGetValue("solid", out object? solidValue))
        {
            bool isSolid = solidValue switch
            {
                bool b => b,
                string s => bool.TryParse(s, out bool result) && result,
                _ => false,
            };
            return new Collision(isSolid);
        }

        // Check for collidable property (alternative name)
        if (properties.TryGetValue("collidable", out object? collidableValue))
        {
            bool isCollidable = collidableValue switch
            {
                bool b => b,
                string s => bool.TryParse(s, out bool result) && result,
                _ => false,
            };
            return new Collision(isCollidable);
        }

        return new Collision(false);
    }

    public void MapAndAdd(World world, Entity entity, Dictionary<string, object> properties)
    {
        if (CanMap(properties))
        {
            Collision collision = Map(properties);
            world.Add(entity, collision);
        }
    }
}
