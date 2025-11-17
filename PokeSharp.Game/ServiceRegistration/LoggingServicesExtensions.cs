using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Common.Logging;
using Serilog;

namespace PokeSharp.Game.ServiceRegistration;

/// <summary>
///     Extension methods for configuring logging services.
/// </summary>
public static class LoggingServicesExtensions
{
    /// <summary>
    ///     Configures Serilog logging for the application.
    /// </summary>
    public static IServiceCollection AddGameLogging(
        this IServiceCollection services,
        IConfiguration? configuration = null,
        string environment = "Production"
    )
    {
        // Create Serilog logger
        var serilogConfig = SerilogConfiguration.CreateConfiguration(configuration, environment);
        var logger = serilogConfig.CreateLogger();
        Log.Logger = logger;

        // Add Serilog to DI
        services.AddLogging(loggingBuilder =>
        {
            loggingBuilder.ClearProviders();
            loggingBuilder.AddSerilog(logger, dispose: true);
        });

        return services;
    }
}

