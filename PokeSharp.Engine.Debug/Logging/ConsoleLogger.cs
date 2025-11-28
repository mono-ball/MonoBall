using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace PokeSharp.Engine.Debug.Logging;

/// <summary>
///     Custom logger that writes log messages to the debug console.
/// </summary>
public class ConsoleLogger : ILogger
{
    private readonly Action<LogLevel, string, string> _addLogEntry;
    private readonly string _categoryName;
    private readonly Func<LogLevel, bool> _isEnabled;

    public ConsoleLogger(
        string categoryName,
        Action<LogLevel, string, string> addLogEntry,
        Func<LogLevel, bool> isEnabled
    )
    {
        _categoryName = categoryName;
        _addLogEntry = addLogEntry;
        _isEnabled = isEnabled;
    }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        return null; // Scopes not supported for now
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return _isEnabled(logLevel);
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
        if (string.IsNullOrEmpty(message) && exception == null)
        {
            return;
        }

        // Strip Serilog/Spectre.Console markup tags (e.g., [cyan], [red], etc.)
        message = StripMarkupTags(message);

        // Get short category name for display
        string category = GetShortCategoryName(_categoryName);

        // Add to Logs panel only (not console output)
        string logMessage = exception != null ? $"{message}\n{exception}" : message;
        _addLogEntry(logLevel, logMessage, category);
    }

    private static string GetShortCategoryName(string category)
    {
        // Shorten category name if it's too long
        int lastDot = category.LastIndexOf('.');
        if (lastDot >= 0 && lastDot < category.Length - 1)
        {
            return category.Substring(lastDot + 1);
        }

        return category;
    }

    /// <summary>
    ///     Strips Serilog/Spectre.Console markup tags from log messages.
    ///     Examples: [cyan], [red], [/], [skyblue1], [cyan bold], etc.
    /// </summary>
    private static string StripMarkupTags(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return message;
        }

        // Remove markup tags like [cyan], [red], [/], [skyblue1], [cyan bold], etc.
        // Pattern matches:
        // - [/] - closing tag
        // - [color] - simple color
        // - [color modifier] - color with modifier (e.g., "cyan bold")
        // - [color1] - numbered colors
        // This is more permissive to catch all Spectre.Console markup
        return Regex.Replace(message, @"\[/?[a-z0-9_ ]*\]", "", RegexOptions.IgnoreCase);
    }
}
