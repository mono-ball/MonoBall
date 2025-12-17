using Arch.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Engine.Core.Events;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.GameData.PropertyMapping;
using MonoBallFramework.Game.GameSystems.Services;
using MonoBallFramework.Game.Infrastructure.Services;
using MonoBallFramework.Game.Scripting.Api;
using MonoBallFramework.Game.Scripting.Services;

namespace MonoBallFramework.Game.Infrastructure.ServiceRegistration;

/// <summary>
///     Extension methods for registering scripting and API services.
/// </summary>
public static class ScriptingServicesExtensions
{
    /// <summary>
    ///     Registers scripting API services and scripting service.
    /// </summary>
    public static IServiceCollection AddScriptingServices(this IServiceCollection services)
    {
        // Event Bus - REMOVED DUPLICATE: Using the EventBus registered in CoreServicesExtensions.cs
        // Having two separate EventBus instances caused scripts to subscribe to a different instance
        // than the one used by NPCBehaviorSystem for publishing events.

        // Property Mappers (for extensible Tiled property → ECS component mapping)
        services.AddPropertyMappers();

        // Scripting API Services
        services.AddSingleton<PlayerApiService>();
        services.AddSingleton<NpcApiService>();
        services.AddSingleton<MapApiService>(sp =>
        {
            World world = sp.GetRequiredService<World>();
            ILogger<MapApiService> logger = sp.GetRequiredService<ILogger<MapApiService>>();
            // SpatialHashSystem is initialized later in GameInitializer
            // It will be set via SetSpatialQuery method after initialization
            return new MapApiService(world, logger);
        });
        services.AddSingleton<IMapApi>(sp => sp.GetRequiredService<MapApiService>());
        services.AddSingleton<GameStateApiService>();
        services.AddSingleton<IGameStateApi>(sp => sp.GetRequiredService<GameStateApiService>());
        services.AddSingleton<DialogueApiService>();
        // WorldApi removed - scripts now use domain APIs directly via ScriptContext

        // Entity API Service (for runtime entity spawning)
        services.AddSingleton<EntityApiService>();

        // Behavior Registry Adapter (wraps TypeRegistry<BehaviorDefinition> for IBehaviorRegistry interface)
        services.AddSingleton<IBehaviorRegistry>(sp =>
        {
            TypeRegistry<BehaviorDefinition> behaviorRegistry =
                sp.GetRequiredService<TypeRegistry<BehaviorDefinition>>();
            return new BehaviorRegistryAdapter(behaviorRegistry);
        });

        // Registry API Service (for querying game definitions)
        services.AddSingleton<RegistryApiService>();

        // Scripting API Provider
        services.AddSingleton<IScriptingApiProvider, ScriptingApiProvider>();

        // Scripting Service - uses lazy IContentProvider resolution to avoid circular dependency:
        // ModLoader -> ScriptService -> IContentProvider -> IModLoader -> ModLoader
        // IContentProvider is resolved lazily at first script load, not at construction time.
        services.AddSingleton(sp =>
        {
            IAssetPathResolver pathResolver = sp.GetRequiredService<IAssetPathResolver>();
            ILogger<ScriptService> logger = sp.GetRequiredService<ILogger<ScriptService>>();
            ILoggerFactory loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            IScriptingApiProvider apis = sp.GetRequiredService<IScriptingApiProvider>();
            IEventBus eventBus = sp.GetRequiredService<IEventBus>();
            World world = sp.GetRequiredService<World>();
            string scriptsPath = pathResolver.Resolve("Scripts");
            return new ScriptService(scriptsPath, logger, loggerFactory, apis, eventBus, world, sp);
        });

        return services;
    }
}
