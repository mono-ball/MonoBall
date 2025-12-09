using Arch.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Engine.Core.Events;
using MonoBallFramework.Game.Engine.Core.Modding;
using MonoBallFramework.Game.Engine.Systems.Management;
using MonoBallFramework.Game.Engine.Systems.Pooling;
using MonoBallFramework.Game.GameData;
using MonoBallFramework.Game.GameData.Loading;
using MonoBallFramework.Game.GameData.Services;
using MonoBallFramework.Game.GameData.Sprites;
using MonoBallFramework.Game.GameSystems.Services;
using MonoBallFramework.Game.Infrastructure.Configuration;
using MonoBallFramework.Game.Infrastructure.Services;
using static MonoBallFramework.Game.Engine.Systems.Pooling.PoolNames;

namespace MonoBallFramework.Game.Infrastructure.ServiceRegistration;

/// <summary>
///     Extension methods for registering core game services (ECS, data, modding).
/// </summary>
public static class CoreServicesExtensions
{
    /// <summary>
    ///     Registers infrastructure services (path resolution, etc.).
    /// </summary>
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        // Asset Path Resolver - centralizes all asset path resolution
        // Uses AppContext.BaseDirectory to work correctly regardless of working directory
        services.AddSingleton<IAssetPathResolver, AssetPathResolver>();

        return services;
    }

    /// <summary>
    ///     Registers core ECS services (World, SystemManager, EntityPoolManager, EventBus).
    /// </summary>
    public static IServiceCollection AddCoreEcsServices(this IServiceCollection services)
    {
        // Core ECS
        services.AddSingleton(sp =>
        {
            var world = World.Create();
            return world;
        });

        // System Manager - Sequential execution (optimal for <500 entities per system)
        // Parallel overhead (1-2ms) exceeds work time (0.09ms) for Pokemon-style games
        services.AddSingleton<SystemManager>(sp =>
        {
            ILogger<SystemManager>? logger = sp.GetService<ILogger<SystemManager>>();
            return new SystemManager(logger);
        });

        // Event Bus - Optimized implementation with cached handlers and reduced allocations
        // Features: cached handler arrays, fast-path for zero subscribers, aggressive inlining
        services.AddSingleton<IEventBus>(sp =>
        {
            ILogger<EventBus>? logger = sp.GetService<ILogger<EventBus>>();
            return new EventBus(logger);
        });

        // Entity Pool Manager - For entity recycling and pooling
        // Register pools immediately to ensure they're available before any service that needs them
        services.AddSingleton(sp =>
        {
            World world = sp.GetRequiredService<World>();
            var poolManager = new EntityPoolManager(world);

            // Register pools at creation time to eliminate temporal coupling
            // Previously, pools were registered in GameInitializer.Initialize() which
            // ran after services like LayerProcessor were created with the pool manager.
            // Now pools are available immediately after EntityPoolManager is created.
            var gameplayConfig = GameplayConfig.CreateDefault();
            PoolConfig playerPool = gameplayConfig.Pools.Player;
            PoolConfig npcPool = gameplayConfig.Pools.Npc;
            PoolConfig tilePool = gameplayConfig.Pools.Tile;

            poolManager.RegisterPool(
                Player,
                playerPool.InitialSize,
                playerPool.MaxSize,
                playerPool.Warmup,
                playerPool.AutoResize,
                playerPool.GrowthFactor,
                playerPool.AbsoluteMaxSize
            );
            poolManager.RegisterPool(
                Npc,
                npcPool.InitialSize,
                npcPool.MaxSize,
                npcPool.Warmup,
                npcPool.AutoResize,
                npcPool.GrowthFactor,
                npcPool.AbsoluteMaxSize
            );
            poolManager.RegisterPool(
                Tile,
                tilePool.InitialSize,
                tilePool.MaxSize,
                tilePool.Warmup,
                tilePool.AutoResize,
                tilePool.GrowthFactor,
                tilePool.AbsoluteMaxSize
            );

            return poolManager;
        });

        return services;
    }

    /// <summary>
    ///     Registers data services (database, data loaders, definition services).
    /// </summary>
    public static IServiceCollection AddDataServices(this IServiceCollection services)
    {
        // EF Core In-Memory Database for game data definitions
        // Register as Singleton since we're using In-Memory database for read-only data
        services.AddDbContext<GameDataContext>(
            options =>
            {
                options.UseInMemoryDatabase("GameData");

#if DEBUG
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
#endif
            },
            ServiceLifetime.Singleton // In-Memory DB can be singleton
        );

        // Data loading and services
        services.AddSingleton<GameDataLoader>();
        services.AddSingleton<NpcDefinitionService>();
        services.AddSingleton<MapDefinitionService>();
        services.AddSingleton<IMapPopupDataService, MapPopupDataService>();

        // Sprite Registry - for loading sprite definitions following the registry pattern
        services.AddSingleton<SpriteRegistry>();

        // Map Registry - tracks loaded maps and provides map ID management
        services.AddSingleton<MapRegistry>();

        return services;
    }

    /// <summary>
    ///     Registers modding services.
    /// </summary>
    public static IServiceCollection AddModdingServices(
        this IServiceCollection services,
        string modsDirectory = "Mods"
    )
    {
        ModdingExtensions.AddModdingServices(services, modsDirectory);
        return services;
    }
}
