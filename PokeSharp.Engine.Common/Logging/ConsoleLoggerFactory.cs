using Microsoft.Extensions.Logging;

namespace PokeSharp.Engine.Common.Logging;

/// <summary>
///     Factory for creating console loggers without DI.
/// </summary>
public static class ConsoleLoggerFactory
{
    /// <summary>
    ///     Create a console logger for the specified type.
    /// </summary>
    /// <typeparam name="T">Type to create logger for</typeparam>
    /// <param name="minLevel">Minimum log level to output</param>
    /// <returns>Console logger instance</returns>
    public static ILogger<T> Create<T>(LogLevel minLevel = LogLevel.Information)
    {
        return new ConsoleLogger<T>(minLevel);
    }

    /// <summary>
    ///     Create a logger factory for creating loggers with dynamic category names.
    /// </summary>
    /// <param name="minLevel">Minimum log level to output</param>
    /// <returns>Logger factory instance</returns>
    public static ILoggerFactory Create(LogLevel minLevel = LogLevel.Information)
    {
        return new ConsoleLoggerFactoryImpl(minLevel);
    }

    /// <summary>
    ///     Create a composite logger that writes to both console and file.
    /// </summary>
    /// <typeparam name="T">Type to create logger for</typeparam>
    /// <param name="consoleLevel">Minimum log level for console output</param>
    /// <param name="fileLevel">Minimum log level for file output</param>
    /// <param name="logDirectory">Directory to store log files (default: "Logs")</param>
    /// <returns>Composite logger instance</returns>
    public static ILogger<T> CreateWithFile<T>(
        LogLevel consoleLevel = LogLevel.Information,
        LogLevel fileLevel = LogLevel.Debug,
        string logDirectory = "Logs"
    )
    {
        return new CompositeLogger<T>(consoleLevel, fileLevel, logDirectory);
    }
}
