using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Core.Services;
using PokeSharp.Engine.Systems.Management;
using PokeSharp.Game.Data.Factories;
using PokeSharp.Game.Infrastructure.Diagnostics;
using PokeSharp.Game.Initialization;
using PokeSharp.Game.Initialization.Factories;
using PokeSharp.Game.Input;
using PokeSharp.Game.Systems;
using PokeSharp.Game.Systems.Services;

namespace PokeSharp.Game.Infrastructure.ServiceRegistration;

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
        // GraphicsDevice is available at runtime (in PokeSharpGame.Initialize)
        services.AddSingleton<IGraphicsServiceFactory, GraphicsServiceFactory>();

        // Game Time Service - register as singleton so both IGameTimeService and ITimeControl
        // resolve to the same instance
        services.AddSingleton<GameTimeService>();
        services.AddSingleton<IGameTimeService>(sp => sp.GetRequiredService<GameTimeService>());
        services.AddSingleton<ITimeControl>(sp => sp.GetRequiredService<GameTimeService>());

        // Collision Service - provides on-demand collision checking (not a system)
        services.AddSingleton<ICollisionService>(sp =>
        {
            var systemManager = sp.GetRequiredService<SystemManager>();
            // SpatialHashSystem is registered as a system and implements ISpatialQuery
            var spatialQuery = systemManager.GetSystem<SpatialHashSystem>();
            if (spatialQuery == null)
                throw new InvalidOperationException(
                    "SpatialHashSystem must be registered before CollisionService"
                );
            var logger = sp.GetService<ILogger<CollisionService>>();
            return new CollisionService(spatialQuery, logger);
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
