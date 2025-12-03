using Arch.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Core.Events;
using PokeSharp.Game.Data.PropertyMapping;
using PokeSharp.Game.Infrastructure.Services;
using PokeSharp.Game.Scripting.Api;
using PokeSharp.Game.Scripting.Services;

namespace PokeSharp.Game.Infrastructure.ServiceRegistration;

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
        services.AddSingleton<GameStateApiService>();
        services.AddSingleton<DialogueApiService>();
        services.AddSingleton<EffectApiService>();
        // WorldApi removed - scripts now use domain APIs directly via ScriptContext

        // Scripting API Provider
        services.AddSingleton<IScriptingApiProvider, ScriptingApiProvider>();

        // Scripting Service - uses path resolver for correct path resolution
        services.AddSingleton(sp =>
        {
            IAssetPathResolver pathResolver = sp.GetRequiredService<IAssetPathResolver>();
            ILogger<ScriptService> logger = sp.GetRequiredService<ILogger<ScriptService>>();
            ILoggerFactory loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            IScriptingApiProvider apis = sp.GetRequiredService<IScriptingApiProvider>();
            IEventBus eventBus = sp.GetRequiredService<IEventBus>();
            World world = sp.GetRequiredService<World>();
            string scriptsPath = pathResolver.Resolve("Scripts");
            return new ScriptService(scriptsPath, logger, loggerFactory, apis, eventBus, world);
        });

        return services;
    }
}
