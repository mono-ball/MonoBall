using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Core.Templates;
using PokeSharp.Engine.Core.Templates.Loading;
using PokeSharp.Engine.Core.Types;
using PokeSharp.Engine.Systems.Factories;
using PokeSharp.Game.Infrastructure.Services;
using PokeSharp.Game.Initialization.Initializers;
using PokeSharp.Game.Templates;

namespace PokeSharp.Game.Infrastructure.ServiceRegistration;

/// <summary>
///     Extension methods for registering template and entity factory services.
/// </summary>
public static class TemplateServicesExtensions
{
    /// <summary>
    ///     Registers template system services (deserializers, loaders, cache, factory).
    /// </summary>
    public static IServiceCollection AddTemplateServices(this IServiceCollection services)
    {
        // Component Deserializer Registry - for JSON template loading
        services.AddSingleton(sp =>
        {
            ILogger<ComponentDeserializerRegistry> logger = sp.GetRequiredService<
                ILogger<ComponentDeserializerRegistry>
            >();
            var registry = new ComponentDeserializerRegistry(logger);
            ComponentDeserializerSetup.RegisterAllDeserializers(registry, sp.GetService<ILogger>());
            return registry;
        });

        // JSON Template Loader - for loading templates from JSON files
        services.AddSingleton<JsonTemplateLoader>();

        // Template Cache - starts empty, will be initialized asynchronously
        // Note: Template loading is now done via TemplateCacheInitializer during async initialization
        services.AddSingleton<TemplateCache>();

        // Template Cache Initializer - handles async template loading
        services.AddSingleton<TemplateCacheInitializer>();

        // Entity Factory Service
        services.AddSingleton<IEntityFactoryService, EntityFactoryService>();

        // Behavior Registry - uses path resolver for correct path resolution
        services.AddSingleton(sp =>
        {
            IAssetPathResolver pathResolver = sp.GetRequiredService<IAssetPathResolver>();
            ILogger<TypeRegistry<BehaviorDefinition>> logger = sp.GetRequiredService<
                ILogger<TypeRegistry<BehaviorDefinition>>
            >();
            string behaviorsPath = pathResolver.ResolveData("Behaviors");
            return new TypeRegistry<BehaviorDefinition>(behaviorsPath, logger);
        });

        // Tile Behavior Registry - uses path resolver for correct path resolution
        services.AddSingleton(sp =>
        {
            IAssetPathResolver pathResolver = sp.GetRequiredService<IAssetPathResolver>();
            ILogger<TypeRegistry<TileBehaviorDefinition>> logger = sp.GetRequiredService<
                ILogger<TypeRegistry<TileBehaviorDefinition>>
            >();
            string tileBehaviorsPath = pathResolver.ResolveData("TileBehaviors");
            return new TypeRegistry<TileBehaviorDefinition>(tileBehaviorsPath, logger);
        });

        return services;
    }
}
