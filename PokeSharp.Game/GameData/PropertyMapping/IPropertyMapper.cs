using Arch.Core;

namespace PokeSharp.Game.Data.PropertyMapping;

/// <summary>
///     Interface for mapping Tiled property dictionaries to ECS components.
///     Implements Strategy Pattern for flexible property-to-component conversion.
/// </summary>
/// <typeparam name="TComponent">The component type this mapper produces.</typeparam>
/// <remarks>
///     Design Principles:
///     - Single Responsibility: Each mapper handles one component type
///     - Open/Closed: New mappers can be added without modifying existing code
///     - Dependency Inversion: MapLoader depends on abstractions, not concrete mappers
/// </remarks>
public interface IPropertyMapper<TComponent>
{
    /// <summary>
    ///     Determines if this mapper can handle the given properties.
    /// </summary>
    /// <param name="properties">Properties from Tiled tile/object.</param>
    /// <returns>True if this mapper can create a component from these properties.</returns>
    bool CanMap(Dictionary<string, object> properties);

    /// <summary>
    ///     Maps properties to a component instance.
    /// </summary>
    /// <param name="properties">Properties from Tiled tile/object.</param>
    /// <returns>The mapped component instance.</returns>
    /// <exception cref="InvalidOperationException">If CanMap returns false for these properties.</exception>
    TComponent Map(Dictionary<string, object> properties);
}

/// <summary>
///     Interface for mappers that can add components to an entity.
///     Extends IPropertyMapper to support direct entity modification.
/// </summary>
/// <typeparam name="TComponent">The component type this mapper produces.</typeparam>
public interface IEntityPropertyMapper<TComponent> : IPropertyMapper<TComponent>
{
    /// <summary>
    ///     Maps properties and adds the component to the entity.
    /// </summary>
    /// <param name="world">The ECS world.</param>
    /// <param name="entity">The entity to add the component to.</param>
    /// <param name="properties">Properties from Tiled tile/object.</param>
    void MapAndAdd(World world, Entity entity, Dictionary<string, object> properties);
}
