using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MonoBallFramework.Game.Engine.Audio.Configuration;
using MonoBallFramework.Game.Engine.Debug;
using MonoBallFramework.Game.Engine.Rendering.Configuration;
using MonoBallFramework.Game.Infrastructure.Configuration;
using MonoBallFramework.Game.Infrastructure.ServiceRegistration;

namespace MonoBallFramework.Game;

/// <summary>
///     Extension methods for configuring game services in the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Adds all game services to the service collection.
    ///     Orchestrates registration of all service groups.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">Optional configuration (loaded from appsettings.json)</param>
    /// <param name="environment">Environment name (Development, Production, etc.)</param>
    public static IServiceCollection AddGameServices(
        this IServiceCollection services,
        IConfiguration? configuration = null,
        string environment = "Production"
    )
    {
        // Add configuration to DI if provided
        if (configuration != null)
        {
            services.AddSingleton(configuration);

            // Configure game options from appsettings.json
            services.Configure<GameConfiguration>(
                configuration.GetSection(GameConfiguration.SectionName)
            );
        }
        else
        {
            // If no configuration provided, use default values
            services.Configure<GameConfiguration>(_ => { });
        }

        // Configure logging first (other services may need ILoggerFactory)
        services.AddGameLogging(configuration, environment);

        // Register infrastructure services first (path resolution depends on configuration)
        services.AddInfrastructureServices();

        // Register service groups
        services.AddCoreEcsServices();
        services.AddDataServices();
        services.AddModdingServices();
        services.AddGameRuntimeServices(); // Must be before scripting (provides IGameStateService)
        services.AddScriptingServices();

        // Debug Console Services (pass configuration for console settings)
        services.AddDebugConsole(configuration);

        // Audio Services (background music, sound effects, Pokemon cries)
        // Note: Production config is used for all environments (Development preset is deprecated)
        services.AddAudioServices(AudioConfiguration.Production);

        // Rendering Configuration (sprite batching, layers, performance tuning)
        RenderingConfiguration renderingConfig = environment == "Development"
            ? RenderingConfiguration.Default
            : RenderingConfiguration.Production;
        services.AddSingleton(renderingConfig);

        return services;
    }
}
