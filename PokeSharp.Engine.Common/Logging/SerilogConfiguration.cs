using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.Spectre;

namespace PokeSharp.Engine.Common.Logging;

/// <summary>
///     Provides centralized Serilog configuration for the PokeSharp engine.
/// </summary>
public static class SerilogConfiguration
{
    /// <summary>
    ///     Configures Serilog from appsettings.json or programmatic defaults.
    /// </summary>
    /// <param name="configuration">Optional configuration to read from appsettings.json</param>
    /// <param name="environment">Environment name (Development, Production, etc.)</param>
    /// <returns>Configured LoggerConfiguration</returns>
    public static LoggerConfiguration CreateConfiguration(
        IConfiguration? configuration = null,
        string environment = "Production"
    )
    {
        var loggerConfig = new LoggerConfiguration();

        if (configuration != null)
        {
            // Read from appsettings.json
            loggerConfig.ReadFrom.Configuration(configuration);
        }
        else
        {
            // Programmatic configuration as fallback
            bool isDevelopment = environment.Equals(
                "Development",
                StringComparison.OrdinalIgnoreCase
            );

            loggerConfig
                .MinimumLevel.Is(isDevelopment ? LogEventLevel.Debug : LogEventLevel.Information)
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                .MinimumLevel.Override("System", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .Enrich.WithThreadId()
                .Enrich.WithMachineName()
                .Enrich.WithProperty("Application", "PokeSharp");

            // Console sink with Spectre.Console support for markup rendering
            loggerConfig.WriteTo.Spectre(
                "[{Timestamp:HH:mm:ss.fff}] [{Level:u5}] {SourceContext}: {Message:lj}{NewLine}{Exception}"
            );

            // File sink with rotation
            loggerConfig.WriteTo.Async(a =>
                a.File(
                    $"logs/pokesharp-{environment.ToLowerInvariant()}-.log",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: isDevelopment ? 3 : 7,
                    fileSizeLimitBytes: isDevelopment ? 50 * 1024 * 1024 : 10 * 1024 * 1024, // 50MB dev, 10MB prod
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{ThreadId}] {SourceContext}: {Message:lj}{NewLine}{Exception}"
                )
            );
        }

        return loggerConfig;
    }

    /// <summary>
    ///     Creates an ILoggerFactory configured with Serilog.
    /// </summary>
    /// <param name="configuration">Optional configuration to read from appsettings.json</param>
    /// <param name="environment">Environment name (Development, Production, etc.)</param>
    /// <returns>Configured ILoggerFactory</returns>
    public static ILoggerFactory CreateLoggerFactory(
        IConfiguration? configuration = null,
        string environment = "Production"
    )
    {
        Logger serilogLogger = CreateConfiguration(configuration, environment).CreateLogger();
        Log.Logger = serilogLogger;

        ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddSerilog(serilogLogger, true);
        });

        return loggerFactory;
    }

    /// <summary>
    ///     Loads configuration from appsettings.json files.
    /// </summary>
    /// <param name="basePath">Base path where config files are located</param>
    /// <param name="environment">Environment name for environment-specific config</param>
    /// <returns>IConfiguration instance</returns>
    public static IConfiguration LoadConfiguration(
        string basePath,
        string environment = "Production"
    )
    {
        IConfigurationBuilder configBuilder = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("Config/appsettings.json", true, true)
            .AddJsonFile($"Config/appsettings.{environment}.json", true, true)
            .AddEnvironmentVariables("POKESHARP_");

        return configBuilder.Build();
    }
}
