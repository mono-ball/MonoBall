using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace PokeSharp.Game.Data.PropertyMapping;

/// <summary>
///     Extension methods for registering Property Mapper services in the DI container.
/// </summary>
public static class PropertyMapperServiceExtensions
{
    /// <summary>
    ///     Adds PropertyMapperRegistry and all property mappers to the service collection.
    /// </summary>
    public static IServiceCollection AddPropertyMappers(this IServiceCollection services)
    {
        services.AddSingleton(provider =>
        {
            ILogger<PropertyMapperRegistry>? logger = provider.GetService<
                ILogger<PropertyMapperRegistry>
            >();
            return CreatePropertyMapperRegistry(logger);
        });

        return services;
    }

    /// <summary>
    ///     Creates and configures a PropertyMapperRegistry with all mappers.
    ///     Exposed as public static for testing purposes.
    /// </summary>
    /// <param name="logger">Optional logger for the registry.</param>
    /// <returns>Configured PropertyMapperRegistry with all mappers registered.</returns>
    public static PropertyMapperRegistry CreatePropertyMapperRegistry(
        ILogger<PropertyMapperRegistry>? logger = null
    )
    {
        var registry = new PropertyMapperRegistry(logger);

        // Register tile property mappers
        registry.RegisterMapper(new CollisionMapper());
        registry.RegisterMapper(new TileBehaviorMapper());
        registry.RegisterMapper(new EncounterZoneMapper());
        registry.RegisterMapper(new TerrainTypeMapper());
        registry.RegisterMapper(new ScriptMapper());

        // Register entity property mappers (for objects)
        registry.RegisterMapper(new InteractionMapper());
        registry.RegisterMapper(new NpcMapper());

        // Add more mappers here as needed when new component types are created
        // Example: registry.RegisterMapper(new WarpPointMapper());

        logger?.LogInformation("Registered {MapperCount} property mappers", 7);

        return registry;
    }
}
