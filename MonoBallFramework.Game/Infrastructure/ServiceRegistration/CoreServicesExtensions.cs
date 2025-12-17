using Arch.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MonoBallFramework.Game.Engine.Content;
using MonoBallFramework.Game.Engine.Core.Events;
using MonoBallFramework.Game.Engine.Core.Modding;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.Engine.Rendering.Popups;
using MonoBallFramework.Game.Engine.Systems.Management;

using MonoBallFramework.Game.Engine.UI.Core;
using MonoBallFramework.Game.Engine.UI.Utilities;
using MonoBallFramework.Game.GameData;
using MonoBallFramework.Game.GameData.Loading;
using MonoBallFramework.Game.GameData.Services;
using MonoBallFramework.Game.GameData.Sprites;
using MonoBallFramework.Game.GameSystems.Services;
using MonoBallFramework.Game.Infrastructure.Configuration;
using MonoBallFramework.Game.Infrastructure.Services;


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
    ///     Registers core ECS services (World, SystemManager, EventBus).
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

        // Event Metrics - Always track publish counts from startup
        // Timing is only recorded when IsEnabled is true (when Event Inspector is active)
        services.AddSingleton<EventMetrics>();

        // Event Bus - Optimized implementation with cached handlers and reduced allocations
        // Features: cached handler arrays, fast-path for zero subscribers, aggressive inlining
        services.AddSingleton<IEventBus>(sp =>
        {
            ILogger<EventBus>? logger = sp.GetService<ILogger<EventBus>>();
            EventMetrics metrics = sp.GetRequiredService<EventMetrics>();
            var eventBus = new EventBus(logger);
            eventBus.Metrics = metrics;
            return eventBus;
        });

        // Behavior Type Registries - For NPC and Tile behavior definitions
        // These provide O(1) lookup for moddable behaviors loaded from JSON
        // Uses IContentProvider (resolved lazily) for mod-aware loading with fallback to base path
        services.AddSingleton(sp =>
        {
            IAssetPathResolver pathResolver = sp.GetRequiredService<IAssetPathResolver>();
            ILogger<TypeRegistry<BehaviorDefinition>> logger =
                sp.GetRequiredService<ILogger<TypeRegistry<BehaviorDefinition>>>();
            string behaviorPath = pathResolver.Resolve("Definitions/Behaviors");
            // Pass content type and service provider for mod-aware loading
            // IContentProvider is resolved lazily to avoid circular dependency
            return new TypeRegistry<BehaviorDefinition>(
                behaviorPath,
                "BehaviorDefinitions",  // Content type from ContentProviderOptions
                logger,
                sp);  // Service provider for lazy IContentProvider resolution
        });

        services.AddSingleton(sp =>
        {
            IAssetPathResolver pathResolver = sp.GetRequiredService<IAssetPathResolver>();
            ILogger<TypeRegistry<TileBehaviorDefinition>> logger =
                sp.GetRequiredService<ILogger<TypeRegistry<TileBehaviorDefinition>>>();
            string tileBehaviorPath = pathResolver.Resolve("Definitions/TileBehaviors");
            // Pass content type and service provider for mod-aware loading
            // IContentProvider is resolved lazily to avoid circular dependency
            return new TypeRegistry<TileBehaviorDefinition>(
                tileBehaviorPath,
                "TileBehaviorDefinitions",  // Content type from ContentProviderOptions
                logger,
                sp);  // Service provider for lazy IContentProvider resolution
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

        // Register DbContextFactory for services that need to create contexts on-demand
        // This is used by AudioRegistry (singleton) to avoid holding a scoped context
        services.AddSingleton<IDbContextFactory<GameDataContext>>(sp =>
        {
            var options = sp.GetRequiredService<DbContextOptions<GameDataContext>>();
            return new GameDataContextFactory(options);
        });

        // Data loading and services
        services.AddSingleton<GameDataLoader>();
        services.AddSingleton<MapEntityService>();
        services.AddSingleton<IMapPopupDataService, MapPopupDataService>();

        // Sprite Registry - for loading sprite definitions following the registry pattern
        // Uses EF Core as the source of truth with in-memory caching
        services.AddSingleton<SpriteRegistry>(sp =>
        {
            var contextFactory = sp.GetRequiredService<IDbContextFactory<GameDataContext>>();
            var logger = sp.GetRequiredService<ILogger<SpriteRegistry>>();
            return new SpriteRegistry(contextFactory, logger);
        });

        // Configure PopupRegistryOptions with default values
        services.Configure<PopupRegistryOptions>(config =>
        {
            // Use default values from PopupRegistryOptions class
            // These can be overridden via appsettings.json if needed
        });

        // Popup Registry - for popup background and outline definitions
        // Uses EF Core as the source of truth with in-memory caching
        // Inject the singleton GameDataContext to ensure data is read from the same context
        // that GameDataLoader writes to (required for EF Core In-Memory provider to share data)
        services.AddSingleton<PopupRegistry>(sp =>
        {
            var contextFactory = sp.GetRequiredService<IDbContextFactory<GameDataContext>>();
            var sharedContext = sp.GetRequiredService<GameDataContext>();
            var logger = sp.GetRequiredService<ILogger<PopupRegistry>>();
            var options = sp.GetRequiredService<IOptions<PopupRegistryOptions>>();
            return new PopupRegistry(contextFactory, logger, options, sharedContext);
        });

        // Map Registry - tracks loaded maps and provides map ID management
        services.AddSingleton<MapRegistry>();

        return services;
    }

    /// <summary>
    ///     Registers modding services and content provider.
    ///     IMPORTANT: IContentProvider must be registered BEFORE services that depend on it.
    /// </summary>
    public static IServiceCollection AddModdingServices(
        this IServiceCollection services,
        string? gameBasePath = null
    )
    {
        // Step 1: Register ModLoader first (it's required by IContentProvider)
        string basePath = gameBasePath ?? AppContext.BaseDirectory;
        ModdingExtensions.AddModdingServices(services, basePath);

        // Step 2: Register ContentProvider BEFORE services that depend on it
        // This ensures IContentProvider is available when FontLoader, AssetManager, etc. are created
        services.AddContentProvider(options =>
        {
            options.BaseGameRoot = "Assets";
            options.MaxCacheSize = 10000;
            options.ThrowOnPathTraversal = true;
        });

        // Step 3: Register FontLoader - depends on IContentProvider and IDbContextFactory (non-optional)
        services.AddSingleton<FontLoader>(sp =>
        {
            IContentProvider contentProvider = sp.GetRequiredService<IContentProvider>();
            IDbContextFactory<GameDataContext> contextFactory = sp.GetRequiredService<IDbContextFactory<GameDataContext>>();
            ILogger<FontLoader> logger = sp.GetRequiredService<ILogger<FontLoader>>();
            return new FontLoader(contentProvider, contextFactory, logger);
        });

        return services;
    }
}
