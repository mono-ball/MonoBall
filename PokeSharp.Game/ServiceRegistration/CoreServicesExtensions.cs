using Arch.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Core.Modding;
using PokeSharp.Engine.Systems.Management;
using PokeSharp.Engine.Systems.Pooling;
using PokeSharp.Game.Data.Loading;
using PokeSharp.Game.Data.Services;
using PokeSharp.Game.Services;

namespace PokeSharp.Game.ServiceRegistration;

/// <summary>
///     Extension methods for registering core game services (ECS, data, modding).
/// </summary>
public static class CoreServicesExtensions
{
    /// <summary>
    ///     Registers core ECS services (World, SystemManager, EntityPoolManager).
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
            var logger = sp.GetService<ILogger<SystemManager>>();
            return new SystemManager(logger);
        });

        // Entity Pool Manager - For entity recycling and pooling
        services.AddSingleton(sp =>
        {
            var world = sp.GetRequiredService<World>();
            return new EntityPoolManager(world);
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
        services.AddDbContext<PokeSharp.Game.Data.GameDataContext>(
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
        services.AddSingleton<PokeSharp.Game.Data.Loading.GameDataLoader>();
        services.AddSingleton<PokeSharp.Game.Data.Services.NpcDefinitionService>();
        services.AddSingleton<PokeSharp.Game.Data.Services.MapDefinitionService>();

        // NPC Sprite Loader - for loading sprites extracted from Pokemon Emerald
        services.AddSingleton<SpriteLoader>();

        return services;
    }

    /// <summary>
    ///     Registers modding services.
    /// </summary>
    public static IServiceCollection AddModdingServices(this IServiceCollection services, string modsDirectory = "Mods")
    {
        PokeSharp.Engine.Core.Modding.ModdingExtensions.AddModdingServices(services, modsDirectory);
        return services;
    }
}

