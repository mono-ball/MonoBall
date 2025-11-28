using System.Reflection;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace PokeSharp.Engine.Common.Logging;

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
        Type loggerType = typeof(ConsoleLogger<>).MakeGenericType(typeof(object));
        ConstructorInfo? constructor = loggerType.GetConstructor(
            new[] { typeof(LogLevel), typeof(string) }
        );
        if (constructor != null)
        {
            return (ILogger)constructor.Invoke(new object[] { _minLevel, categoryName });
        }

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
            {
                return;
            }

            string message = formatter(state, exception);
            string scope = SimpleLogScope.CurrentScope;
            string formattedLine = LogFormatting.FormatLogLine(
                logLevel,
                _categoryName,
                message,
                scope,
                DateTime.Now,
                LogFormatting.ContainsMarkup(message)
            );

            if (LogFormatting.SupportsMarkup)
            {
                AnsiConsole.MarkupLine(formattedLine);
            }
            else
            {
                AnsiConsole.WriteLine(formattedLine);
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
}
