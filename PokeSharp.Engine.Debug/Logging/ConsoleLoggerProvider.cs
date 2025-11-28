using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace PokeSharp.Engine.Debug.Logging;

/// <summary>
///     Logger provider that creates loggers writing to the debug console.
/// </summary>
[ProviderAlias("DebugConsole")]
public class ConsoleLoggerProvider : ILoggerProvider
{
    private const int MaxPendingLogs = 1000;
    private readonly ConcurrentDictionary<string, ConsoleLogger> _loggers = new();

    // Buffer for logs that arrive before handlers are set
    private readonly List<(LogLevel Level, string Message, string Category)> _pendingLogs = new();
    private readonly object _pendingLogsLock = new();
    private Action<LogLevel, string, string>? _addLogEntry; // level, message, category
    private bool _handlersConfigured;
    private Func<LogLevel, bool> _isEnabled = _ => true;

    public ILogger CreateLogger(string categoryName)
    {
        // Pass a wrapper that always calls the current _isEnabled, not a captured copy
        return _loggers.GetOrAdd(
            categoryName,
            name => new ConsoleLogger(name, AddLogEntry, IsLogLevelEnabled)
        );
    }

    public void Dispose()
    {
        _loggers.Clear();
    }

    /// <summary>
    ///     Sets the function to add structured log entries to the Logs panel.
    /// </summary>
    public void SetLogEntryHandler(Action<LogLevel, string, string> addLogEntry)
    {
        _addLogEntry = addLogEntry;

        // Flush any pending logs
        FlushPendingLogs();
    }

    /// <summary>
    ///     Sets the function to check if a log level is enabled.
    /// </summary>
    public void SetLogLevelFilter(Func<LogLevel, bool> isEnabled)
    {
        _isEnabled = isEnabled;
    }

    /// <summary>
    ///     Flushes any logs that were buffered before handlers were set.
    /// </summary>
    private void FlushPendingLogs()
    {
        List<(LogLevel, string, string)> logsToFlush;

        lock (_pendingLogsLock)
        {
            if (_pendingLogs.Count == 0 || _addLogEntry == null)
            {
                return;
            }

            logsToFlush = new List<(LogLevel, string, string)>(_pendingLogs);
            _pendingLogs.Clear();
            _handlersConfigured = true;
        }

        // Flush outside of lock
        foreach ((LogLevel level, string message, string category) in logsToFlush)
        {
            _addLogEntry(level, message, category);
        }
    }

    /// <summary>
    ///     Checks if a log level is enabled (called by loggers).
    ///     This ensures loggers always use the current filter, not a captured copy.
    /// </summary>
    private bool IsLogLevelEnabled(LogLevel level)
    {
        return _isEnabled(level);
    }

    private void AddLogEntry(LogLevel level, string message, string category)
    {
        if (_addLogEntry != null)
        {
            _addLogEntry(level, message, category);
        }
        else
        {
            // Buffer logs that arrive before handler is set
            lock (_pendingLogsLock)
            {
                if (!_handlersConfigured)
                {
                    _pendingLogs.Add((level, message, category));

                    // Debug: show pending log count periodically
                    if (_pendingLogs.Count % 50 == 0)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[ConsoleLoggerProvider] Pending logs: {_pendingLogs.Count}"
                        );
                    }

                    // Prevent unbounded growth
                    while (_pendingLogs.Count > MaxPendingLogs)
                    {
                        _pendingLogs.RemoveAt(0);
                    }
                }
            }
        }
    }
}
