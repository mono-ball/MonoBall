using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PokeSharp.Game.ServiceRegistration;

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
    /// <param name="configuration">Optional configuration for Serilog (loaded from appsettings.json)</param>
    /// <param name="environment">Environment name (Development, Production, etc.)</param>
    public static IServiceCollection AddGameServices(
        this IServiceCollection services,
        IConfiguration? configuration = null,
        string environment = "Production"
    )
    {
        // Configure logging first (other services may need ILoggerFactory)
        services.AddGameLogging(configuration, environment);

        // Register service groups
        services.AddCoreEcsServices();
        services.AddDataServices();
        services.AddModdingServices("Mods");
        services.AddTemplateServices();
        services.AddScriptingServices();
        services.AddGameRuntimeServices();

        return services;
    }
}
