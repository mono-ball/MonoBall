using Arch.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Core.Events;
using PokeSharp.Game.Data.PropertyMapping;
using PokeSharp.Game.Scripting.Api;
using PokeSharp.Game.Scripting.Services;
using PokeSharp.Game.Services;
using PokeSharp.Game.Systems;
using PokeSharp.Game.Systems.Services;

namespace PokeSharp.Game.ServiceRegistration;

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
        // Event Bus
        services.AddSingleton<IEventBus>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<EventBus>>();
            return new EventBus(logger);
        });

        // Property Mappers (for extensible Tiled property → ECS component mapping)
        services.AddPropertyMappers();

        // Scripting API Services
        services.AddSingleton<PlayerApiService>();
        services.AddSingleton<NpcApiService>();
        services.AddSingleton<MapApiService>(sp =>
        {
            var world = sp.GetRequiredService<World>();
            var logger = sp.GetRequiredService<ILogger<MapApiService>>();
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

        // Scripting Service
        services.AddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<ScriptService>>();
            var apis = sp.GetRequiredService<IScriptingApiProvider>();
            return new ScriptService("Assets/Scripts", logger, apis);
        });

        return services;
    }
}

