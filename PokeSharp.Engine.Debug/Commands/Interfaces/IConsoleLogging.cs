using Microsoft.Extensions.Logging;

namespace PokeSharp.Engine.Debug.Commands;

/// <summary>
///     Provides console logging control operations.
/// </summary>
public interface IConsoleLogging
{
    /// <summary>
    ///     Gets whether console logging is currently enabled.
    /// </summary>
    bool IsLoggingEnabled { get; }

    /// <summary>
    ///     Gets the current minimum log level.
    /// </summary>
    LogLevel MinimumLogLevel { get; }

    /// <summary>
    ///     Sets whether console logging is enabled.
    /// </summary>
    void SetLoggingEnabled(bool enabled);

    /// <summary>
    ///     Sets the minimum log level for console output.
    /// </summary>
    void SetMinimumLogLevel(LogLevel level);
}
