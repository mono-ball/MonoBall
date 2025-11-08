using System.Text;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace PokeSharp.Core.Logging;

/// <summary>
///     Simple ILoggerFactory implementation for console logging.
/// </summary>
internal sealed class ConsoleLoggerFactoryImpl(LogLevel minLevel = LogLevel.Information)
    : ILoggerFactory
{
    private readonly LogLevel _minLevel = minLevel;

    public void AddProvider(ILoggerProvider provider)
    {
        // Not supported - this is a simple factory
    }

    public ILogger CreateLogger(string categoryName)
    {
        // Create ConsoleLogger<object> with categoryName override to show proper class names
        var loggerType = typeof(ConsoleLogger<>).MakeGenericType(typeof(object));
        var constructor = loggerType.GetConstructor(new[] { typeof(LogLevel), typeof(string) });
        if (constructor != null)
            return (ILogger)constructor.Invoke(new object[] { _minLevel, categoryName });

        // Fallback to a simple implementation
        return new SimpleLogger(categoryName, _minLevel);
    }

    public void Dispose()
    {
        // Nothing to dispose
    }

    /// <summary>
    ///     Simple logger implementation for dynamic category names.
    /// </summary>
    private sealed class SimpleLogger : ILogger
    {
        private static readonly AsyncLocal<Stack<string>> _scopeStack = new();
        private readonly string _categoryName;
        private readonly LogLevel _minLevel;

        public SimpleLogger(string categoryName, LogLevel minLevel)
        {
            _categoryName = categoryName;
            _minLevel = minLevel;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return new SimpleLogScope(state?.ToString() ?? "");
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
                return;

            var message = formatter(state, exception);
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var logLevelMarkup = GetLogLevelMarkup(logLevel);
            var categoryMarkup = GetCategoryMarkup(_categoryName);

            var logLine = new StringBuilder();
            logLine.Append($"[grey][[{timestamp}]][/] ");
            logLine.Append($"{logLevelMarkup} ");

            var scope = SimpleLogScope.CurrentScope;
            if (!string.IsNullOrEmpty(scope))
                logLine.Append($"[dim][[{scope}]][/] ");

            logLine.Append($"{categoryMarkup}: ");
            // Escape markup brackets
            var escapedMessage = message.Replace("[", "[[").Replace("]", "]]");
            logLine.Append(escapedMessage);

            AnsiConsole.MarkupLine(logLine.ToString());

            if (exception != null)
                AnsiConsole.WriteException(exception);
        }

        private static string GetLogLevelMarkup(LogLevel logLevel)
        {
            return logLevel switch
            {
                LogLevel.Trace => "[grey]TRACE[/]",
                LogLevel.Debug => "[cyan]DEBUG[/]",
                LogLevel.Information => "[green]INFO [/]",
                LogLevel.Warning => "[yellow]WARN [/]",
                LogLevel.Error => "[red]ERROR[/]",
                LogLevel.Critical => "[bold red]CRIT [/]",
                _ => "[grey]NONE [/]"
            };
        }

        private static string GetCategoryMarkup(string categoryName)
        {
            var color = GetCategoryColor(categoryName);
            return $"[{color}]{categoryName}[/]";
        }

        private static string GetCategoryColor(string categoryName)
        {
            // Simple hash-based color selection
            var hash = categoryName.GetHashCode();
            var colors = new[] { "cyan", "magenta", "blue", "yellow", "green", "white" };
            return colors[Math.Abs(hash) % colors.Length];
        }

        private sealed class SimpleLogScope : IDisposable
        {
            private readonly string _scopeName;

            public SimpleLogScope(string scopeName)
            {
                _scopeName = scopeName;
                _scopeStack.Value ??= new Stack<string>();
                _scopeStack.Value.Push(scopeName);
            }

            public static string CurrentScope
            {
                get
                {
                    if (_scopeStack.Value == null || _scopeStack.Value.Count == 0)
                        return string.Empty;

                    return string.Join(" > ", _scopeStack.Value.Reverse());
                }
            }

            public void Dispose()
            {
                if (_scopeStack.Value != null && _scopeStack.Value.Count > 0)
                    _scopeStack.Value.Pop();
            }
        }
    }
}