using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PokeSharp.Engine.Debug;
using PokeSharp.Game.Infrastructure.Configuration;
using PokeSharp.Game.Infrastructure.ServiceRegistration;

namespace PokeSharp.Game;

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
        services.AddTemplateServices();
        services.AddScriptingServices();
        services.AddGameRuntimeServices();

        // Debug Console Services (pass configuration for console settings)
        services.AddDebugConsole(configuration);

        return services;
    }
}
