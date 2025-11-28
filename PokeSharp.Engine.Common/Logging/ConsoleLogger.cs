using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace PokeSharp.Engine.Common.Logging;

/// <summary>
///     Simple console logger implementation for systems without dependency injection.
///     Provides basic console output for debugging and development.
/// </summary>
/// <typeparam name="T">Type being logged</typeparam>
public sealed class ConsoleLogger<T> : ILogger<T>
{
    private readonly string _categoryName;
    private readonly LogLevel _minLevel;

    public ConsoleLogger(
        LogLevel minLevel = LogLevel.Information,
        string? categoryNameOverride = null
    )
    {
        string fullName = categoryNameOverride ?? typeof(T).Name;
        // Extract just the class name without namespace (e.g., "SystemManager" from "PokeSharp.Core.Systems.SystemManager")
        _categoryName = fullName.Contains('.') ? fullName.Split('.')[^1] : fullName;
        _minLevel = minLevel;
    }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        return new LogScope(state?.ToString() ?? "");
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel >= _minLevel;
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter
    )
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        string message = formatter(state, exception);
        string scope = LogScope.CurrentScope;
        DateTime timestamp = DateTime.Now;
        bool isMarkup = LogFormatting.ContainsMarkup(message);
        string logLine = LogFormatting.FormatLogLine(
            logLevel,
            _categoryName,
            message,
            scope,
            timestamp,
            isMarkup
        );

        if (LogFormatting.SupportsMarkup)
        {
            AnsiConsole.MarkupLine(logLine);
        }
        else
        {
            AnsiConsole.WriteLine(logLine);
        }

        if (exception == null)
        {
            return;
        }

        IEnumerable<string> exceptionLines = LogFormatting.FormatExceptionLines(
            exception,
            logLevel >= LogLevel.Debug
        );

        foreach (string line in exceptionLines)
        {
            if (LogFormatting.SupportsMarkup)
            {
                AnsiConsole.MarkupLine(line);
            }
            else
            {
                AnsiConsole.WriteLine(line);
            }
        }
    }

    /// <summary>
    ///     Represents a logging scope for grouping related log messages.
    /// </summary>
    private sealed class LogScope : IDisposable
    {
        private static readonly AsyncLocal<Stack<string>> _scopeStack = new();
        private readonly string _scopeName;

        public LogScope(string scopeName)
        {
            _scopeName = scopeName;
            _scopeStack.Value ??= new Stack<string>();
            _scopeStack.Value.Push(scopeName);
        }

        /// <summary>
        ///     Gets the current scope path (e.g., "Scope1 > Scope2").
        /// </summary>
        public static string CurrentScope
        {
            get
            {
                if (_scopeStack.Value == null || _scopeStack.Value.Count == 0)
                {
                    return string.Empty;
                }

                return string.Join(" > ", _scopeStack.Value.Reverse());
            }
        }

        public void Dispose()
        {
            if (_scopeStack.Value != null && _scopeStack.Value.Count > 0)
            {
                _scopeStack.Value.Pop();
            }
        }
    }
}
