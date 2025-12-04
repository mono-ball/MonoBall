using System.Reflection;
using Arch.Core;
using Microsoft.Extensions.Logging;

namespace PokeSharp.Game.Data.PropertyMapping;

/// <summary>
///     Registry for property mappers. Provides centralized access to all mappers.
///     Implements Composite Pattern to manage multiple mappers.
/// </summary>
public class PropertyMapperRegistry
{
    private readonly ILogger<PropertyMapperRegistry>? _logger;
    private readonly List<object> _mappers = new();

    public PropertyMapperRegistry(ILogger<PropertyMapperRegistry>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    ///     Registers a property mapper.
    /// </summary>
    public void RegisterMapper<TComponent>(IPropertyMapper<TComponent> mapper)
    {
        _mappers.Add(mapper);
        _logger?.LogDebug("Registered mapper for {ComponentType}", typeof(TComponent).Name);
    }

    /// <summary>
    ///     Gets all mappers of a specific type.
    /// </summary>
    public IEnumerable<IPropertyMapper<TComponent>> GetMappers<TComponent>()
    {
        return _mappers.OfType<IPropertyMapper<TComponent>>();
    }

    /// <summary>
    ///     Tries to map properties using all registered mappers.
    ///     Applies all mappers that can handle the properties.
    /// </summary>
    /// <param name="world">The ECS world.</param>
    /// <param name="entity">The entity to add components to.</param>
    /// <param name="properties">Properties from Tiled tile/object.</param>
    /// <returns>Number of components successfully mapped and added.</returns>
    public int MapAndAddAll(World world, Entity entity, Dictionary<string, object> properties)
    {
        int count = 0;

        foreach (object mapper in _mappers)
        {
            // Use reflection to call MapAndAdd on IEntityPropertyMapper instances
            Type mapperType = mapper.GetType();
            Type[] interfaces = mapperType.GetInterfaces();

            // Find IEntityPropertyMapper<T> interface
            Type? entityMapperInterface = interfaces.FirstOrDefault(i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEntityPropertyMapper<>)
            );

            if (entityMapperInterface != null)
            {
                // Get the MapAndAdd method
                MethodInfo? mapAndAddMethod = entityMapperInterface.GetMethod("MapAndAdd");
                if (mapAndAddMethod != null)
                {
                    try
                    {
                        mapAndAddMethod.Invoke(mapper, new object[] { world, entity, properties });
                        count++;
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(
                            ex,
                            "Mapper {MapperType} failed to map properties",
                            mapperType.Name
                        );
                    }
                }
            }
        }

        return count;
    }
}
