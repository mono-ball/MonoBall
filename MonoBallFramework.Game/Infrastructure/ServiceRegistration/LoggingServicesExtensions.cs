using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Engine.Common.Logging;
using MonoBallFramework.Game.Engine.Debug.Logging;
using Serilog;
using Serilog.Core;

namespace MonoBallFramework.Game.Infrastructure.ServiceRegistration;

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
        LoggerConfiguration serilogConfig = SerilogConfiguration.CreateConfiguration(
            configuration,
            environment
        );
        Logger logger = serilogConfig.CreateLogger();
        Log.Logger = logger;

        // Add Serilog to DI
        services.AddLogging(loggingBuilder =>
        {
            loggingBuilder.ClearProviders();
            loggingBuilder.AddSerilog(logger, true);

            // Add console logger provider for in-game debug console
            // This provider will be configured later by ConsoleSystem
            loggingBuilder.Services.AddSingleton<ILoggerProvider>(sp =>
                sp.GetRequiredService<ConsoleLoggerProvider>()
            );
        });

        return services;
    }
}
