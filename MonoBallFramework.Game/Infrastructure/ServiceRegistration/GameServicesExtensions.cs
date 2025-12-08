using Arch.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Engine.Core.Events;
using MonoBallFramework.Game.Engine.Core.Services;
using MonoBallFramework.Game.Engine.Rendering.Services;
using MonoBallFramework.Game.Engine.Scenes.Factories;
using MonoBallFramework.Game.Engine.Systems.Management;
using MonoBallFramework.Game.Engine.Systems.Pooling;
using MonoBallFramework.Game.GameData.Factories;
using MonoBallFramework.Game.GameData.PropertyMapping;
using MonoBallFramework.Game.GameData.Services;
using MonoBallFramework.Game.GameSystems.Movement;
using MonoBallFramework.Game.GameSystems.Services;
using MonoBallFramework.Game.GameSystems.Spatial;
using MonoBallFramework.Game.Infrastructure.Diagnostics;
using MonoBallFramework.Game.Initialization.Factories;
using MonoBallFramework.Game.Input;
using MonoBallFramework.Game.Scripting.Api;
using MonoBallFramework.Game.Systems;

namespace MonoBallFramework.Game.Infrastructure.ServiceRegistration;

/// <summary>
///     Extension methods for registering game-specific services (collision, game time, initializers).
/// </summary>
public static class GameServicesExtensions
{
    /// <summary>
    ///     Registers game-specific services (collision, game time, graphics factory, initializers).
    /// </summary>
    public static IServiceCollection AddGameRuntimeServices(this IServiceCollection services)
    {
        // Abstract Factory Pattern: Graphics services that depend on GraphicsDevice
        // The factory allows deferred creation of AssetManager and MapLoader until
        // GraphicsDevice is available at runtime (in MonoBallFrameworkGame.Initialize)
        services.AddSingleton<IGraphicsServiceFactory>(sp =>
        {
            ILoggerFactory loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            SystemManager systemManager = sp.GetRequiredService<SystemManager>();
            EntityPoolManager poolManager = sp.GetRequiredService<EntityPoolManager>();

            // PropertyMapperRegistry is created lazily when needed
            ILogger<PropertyMapperRegistry> mapperLogger = sp.GetRequiredService<
                ILogger<PropertyMapperRegistry>
            >();
            PropertyMapperRegistry propertyMapperRegistry =
                PropertyMapperServiceExtensions.CreatePropertyMapperRegistry(mapperLogger);

            NpcDefinitionService? npcDefinitionService = sp.GetService<NpcDefinitionService>();
            MapDefinitionService? mapDefinitionService = sp.GetService<MapDefinitionService>();
            IGameStateApi? gameStateApi = sp.GetService<IGameStateApi>();

            return new GraphicsServiceFactory(
                loggerFactory,
                systemManager,
                poolManager,
                propertyMapperRegistry,
                npcDefinitionService,
                mapDefinitionService,
                gameStateApi
            );
        });

        // Game Time Service - register as singleton so both IGameTimeService and ITimeControl
        // resolve to the same instance
        services.AddSingleton<GameTimeService>();
        services.AddSingleton<IGameTimeService>(sp => sp.GetRequiredService<GameTimeService>());
        services.AddSingleton<ITimeControl>(sp => sp.GetRequiredService<GameTimeService>());

        // Camera Service - provides centralized camera operations and queries
        services.AddSingleton<ICameraService>(sp =>
        {
            World world = sp.GetRequiredService<World>();
            return new CameraService(world);
        });

        // Camera Provider - provides ECS camera access with caching for scenes
        // Used by scenes to get camera without directly querying ECS
        services.AddSingleton<ICameraProvider>(sp =>
        {
            World world = sp.GetRequiredService<World>();
            return new EcsCameraProvider(world);
        });

        // Note: IRenderingService requires GraphicsDevice which is not available during DI setup.
        // It is registered after GraphicsDevice is available in MonoBallFrameworkGame.Initialize()
        // or in the initialization pipeline.

        // Collision Service - provides on-demand collision checking (not a system)
        services.AddSingleton<ICollisionService>(sp =>
        {
            SystemManager systemManager = sp.GetRequiredService<SystemManager>();
            // SpatialHashSystem is registered as a system and implements ISpatialQuery
            SpatialHashSystem? spatialQuery = systemManager.GetSystem<SpatialHashSystem>();
            if (spatialQuery == null)
            {
                throw new InvalidOperationException(
                    "SpatialHashSystem must be registered before CollisionService"
                );
            }

            IEventBus eventBus = sp.GetRequiredService<IEventBus>();
            ILogger<CollisionService>? logger = sp.GetService<ILogger<CollisionService>>();
            return new CollisionService(spatialQuery, eventBus, logger);
        });

        // Game Initializers and Helpers
        services.AddSingleton<PerformanceMonitor>();
        services.AddSingleton<InputManager>();
        services.AddSingleton<PlayerFactory>();

        // Note: GameInitializer, MapInitializer, NPCBehaviorInitializer, and SpatialHashSystem
        // are created after GraphicsDevice is available in MonoBallFrameworkGame.Initialize()
        // AssetManager and MapLoader are now created via IGraphicsServiceFactory

        return services;
    }
}
