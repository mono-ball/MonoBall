using System.Text;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace PokeSharp.Core.Logging;

/// <summary>
///     Simple console logger implementation for systems without dependency injection.
///     Provides basic console output for debugging and development.
/// </summary>
/// <typeparam name="T">Type being logged</typeparam>
public sealed class ConsoleLogger<T> : ILogger<T>
{
    private readonly ConsoleColor _categoryColor;
    private readonly string _categoryName;
    private readonly LogLevel _minLevel;

    public ConsoleLogger(
        LogLevel minLevel = LogLevel.Information,
        string? categoryNameOverride = null
    )
    {
        var fullName = categoryNameOverride ?? typeof(T).Name;
        // Extract just the class name without namespace (e.g., "SystemManager" from "PokeSharp.Core.Systems.SystemManager")
        _categoryName = fullName.Contains('.') ? fullName.Split('.')[^1] : fullName;
        _minLevel = minLevel;
        _categoryColor = GetCategoryColor(_categoryName);
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
            return;

        var message = formatter(state, exception);
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        var logLevelMarkup = GetLogLevelMarkup(logLevel);
        var categoryMarkup = GetCategoryMarkup(_categoryName);

        // Check if message already contains Spectre markup (templates use this)
        var isPreformatted = message.Contains("[/]");

        if (isPreformatted)
        {
            // Message already has markup from a template - just add prefix
            var logLine = new StringBuilder();

            // Timestamp in dim grey
            logLine.Append($"[grey][[{timestamp}]][/] ");

            // Log level with color
            logLine.Append($"{logLevelMarkup} ");

            // Scope (if any) in dim
            var scope = LogScope.CurrentScope;
            if (!string.IsNullOrEmpty(scope))
                logLine.Append($"[dim][[{scope}]][/] ");

            // Category with unique color
            logLine.Append($"{categoryMarkup}: ");

            // Message already contains markup - don't escape
            logLine.Append(message);

            // Write the formatted line using Spectre.Console
            AnsiConsole.MarkupLine(logLine.ToString());
        }
        else
        {
            // Regular message - apply full formatting
            var logLine = new StringBuilder();

            // Timestamp in dim grey
            logLine.Append($"[grey][[{timestamp}]][/] ");

            // Log level with color
            logLine.Append($"{logLevelMarkup} ");

            // Scope (if any) in dim
            var scope = LogScope.CurrentScope;
            if (!string.IsNullOrEmpty(scope))
                logLine.Append($"[dim][[{scope}]][/] ");

            // Category with unique color
            logLine.Append($"{categoryMarkup}");

            // Message with log level color (escape to prevent accidental markup)
            var logLevelColor = GetLogLevelColorName(logLevel);
            logLine.Append($": [{logLevelColor}]{EscapeMarkup(message)}[/]");

            // Write the formatted line using Spectre.Console
            AnsiConsole.MarkupLine(logLine.ToString());
        }

        if (exception != null)
        {
            // Exception in red with bold
            AnsiConsole.MarkupLine($"[red]  Exception: {EscapeMarkup(exception.Message)}[/]");
            if (logLevel >= LogLevel.Debug)
                AnsiConsole.MarkupLine(
                    $"[dim red]  StackTrace: {EscapeMarkup(exception.StackTrace ?? "N/A")}[/]"
                );
        }
    }

    /// <summary>
    ///     Escapes markup characters in user strings to prevent formatting issues.
    /// </summary>
    private static string EscapeMarkup(string text)
    {
        return text.Replace("[", "[[").Replace("]", "]]");
    }

    private static string GetLogLevelString(LogLevel logLevel)
    {
        return logLevel switch
        {
            LogLevel.Trace => "TRACE",
            LogLevel.Debug => "DEBUG",
            LogLevel.Information => "INFO ",
            LogLevel.Warning => "WARN ",
            LogLevel.Error => "ERROR",
            LogLevel.Critical => "CRIT ",
            _ => "NONE ",
        };
    }

    private static string GetLogLevelMarkup(LogLevel logLevel)
    {
        return logLevel switch
        {
            LogLevel.Trace => "[dim][[TRACE]][/]",
            LogLevel.Debug => "[grey][[DEBUG]][/]",
            LogLevel.Information => "[green][[INFO ]][/]",
            LogLevel.Warning => "[yellow][[WARN ]][/]",
            LogLevel.Error => "[red bold][[ERROR]][/]",
            LogLevel.Critical => "[magenta bold][[CRIT ]][/]",
            _ => "[white][[NONE ]][/]",
        };
    }

    private static string GetLogLevelColorName(LogLevel logLevel)
    {
        return logLevel switch
        {
            LogLevel.Trace => "dim",
            LogLevel.Debug => "grey",
            LogLevel.Information => "green",
            LogLevel.Warning => "yellow",
            LogLevel.Error => "red bold",
            LogLevel.Critical => "magenta bold",
            _ => "white",
        };
    }

    private static string GetCategoryMarkup(string categoryName)
    {
        // Spectre.Console color names for categories
        var categoryColors = new[]
        {
            "cyan1",
            "blue",
            "cyan3",
            "blue1",
            "magenta",
            "purple",
            "green",
            "lime",
            "yellow",
            "orange1",
            "red",
            "indianred",
            "silver",
            "grey",
            "aqua",
            "deepskyblue1",
            "mediumorchid",
            "springgreen1",
            "gold1",
            "hotpink",
        };

        // Generate a consistent hash from the category name
        var hash = 0;
        foreach (var c in categoryName)
            hash = (hash * 31 + c) & 0x7FFFFFFF;

        // Select color based on hash
        var color = categoryColors[hash % categoryColors.Length];
        return $"[{color} bold]{categoryName}[/]";
    }

    private static ConsoleColor GetCategoryColor(string categoryName)
    {
        // Legacy method kept for backward compatibility if needed
        // Now using Spectre.Console markup instead
        return ConsoleColor.Cyan;
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
