using Arch.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PokeSharp.Core.Events;
using PokeSharp.Core.Factories;
using PokeSharp.Core.Scripting.Services;
using PokeSharp.Core.ScriptingApi;
using PokeSharp.Core.Systems;
using PokeSharp.Core.Templates;
using PokeSharp.Core.Types;
using PokeSharp.Game.Diagnostics;
using PokeSharp.Game.Initialization;
using PokeSharp.Game.Input;
using PokeSharp.Game.Templates;
using PokeSharp.Rendering.Factories;
using PokeSharp.Scripting.Services;

namespace PokeSharp.Game;

/// <summary>
///     Extension methods for configuring game services in the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Adds all game services to the service collection.
    /// </summary>
    public static IServiceCollection AddGameServices(this IServiceCollection services)
    {
        // Core ECS
        services.AddSingleton(sp =>
        {
            var world = World.Create();
            return world;
        });

        // System Manager
        services.AddSingleton<SystemManager>();

        // Entity Factory & Templates
        services.AddSingleton(sp =>
        {
            var cache = new TemplateCache();
            TemplateRegistry.RegisterAllTemplates(cache);
            return cache;
        });
        services.AddSingleton<IEntityFactoryService, EntityFactoryService>();

        // Type Registry for Behaviors
        services.AddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<TypeRegistry<BehaviorDefinition>>>();
            return new TypeRegistry<BehaviorDefinition>("Assets/Types/Behaviors", logger);
        });

        // Event Bus
        services.AddSingleton<IEventBus>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<EventBus>>();
            return new EventBus(logger);
        });

        // API Test Event Subscriber (for Phase 1 validation)
        services.AddSingleton<ApiTestEventSubscriber>();
        services.AddSingleton<ApiTestInitializer>();

        // Abstract Factory Pattern: Graphics services that depend on GraphicsDevice
        // The factory allows deferred creation of AssetManager and MapLoader until
        // GraphicsDevice is available at runtime (in PokeSharpGame.Initialize)
        services.AddSingleton<IGraphicsServiceFactory, GraphicsServiceFactory>();

        // Scripting API Services
        services.AddSingleton<PlayerApiService>();
        services.AddSingleton<NpcApiService>();
        services.AddSingleton<MapApiService>(sp =>
        {
            var world = sp.GetRequiredService<World>();
            var logger = sp.GetRequiredService<ILogger<MapApiService>>();
            // SpatialHashSystem is initialized later in GameInitializer
            // It will be set via SetSpatialHashSystem method after initialization
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

        // Game Initializers and Helpers
        services.AddSingleton<PerformanceMonitor>();
        services.AddSingleton<InputManager>();
        services.AddSingleton<PlayerFactory>();

        // Note: GameInitializer, MapInitializer, NPCBehaviorInitializer, and SpatialHashSystem
        // are created after GraphicsDevice is available in PokeSharpGame.Initialize()
        // AssetManager and MapLoader are now created via IGraphicsServiceFactory

        return services;
    }
}
