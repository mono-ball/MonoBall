using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace PokeSharp.Engine.Common.Logging;

/// <summary>
///     Shared formatting helpers for console and file loggers.
///     Centralizes palette, markup handling, and plain-text fallbacks.
/// </summary>
internal static class LogFormatting
{
    private const string PlainLevelTemplate = "[{0}]";
    private const int LevelLabelWidth = 5;
    private const int CategoryWidth = 18;

    private static readonly Lazy<bool> _supportsMarkup = new(() =>
    {
        if (Environment.GetEnvironmentVariable("POKESHARP_LOG_PLAIN") == "1")
        {
            return false;
        }

        try
        {
            if (Console.IsOutputRedirected)
            {
                return false;
            }
        }
        catch
        {
            // Ignore and assume no redirection
        }

        try
        {
            return AnsiConsole.Profile.Capabilities.Ansi;
        }
        catch
        {
            return false;
        }
    });

    private static readonly Regex _markupRegex = new(
        @"\[/?[^\]]+\]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant
    );

    private static readonly Dictionary<string, string> PlainGlyphMap = new()
    {
        { "⚡", "[perf]" },
        { "▶", ">" },
        { "→", "->" },
        { "│", "|" },
        { "╔", "====" },
        { "╚", "====" },
        { "╦", "====" },
        { "╩", "====" },
        { "╠", "|" },
        { "╣", "|" },
        { "║", "||" },
        { "✗", "x" },
        { "A  ", "[asset]" },
        { "R  ", "[render]" },
        { "P  ", "[time]" },
        { "MEM", "[mem]" },
        { "E  ", "[entity]" },
        { "I  ", "[input]" },
        { "WF ", "[task]" },
        { "SYS", "[sys]" },
    };

    private static readonly LogLevelStyle DefaultStyle = new(
        "NONE",
        "white",
        false,
        "white",
        false,
        "NONE"
    );

    private static readonly Dictionary<LogLevel, LogLevelStyle> LogLevelStyles = new()
    {
        { LogLevel.Trace, new LogLevelStyle("TRACE", "grey53", false, "grey35", true, "TRACE") },
        {
            LogLevel.Debug,
            new LogLevelStyle("DEBUG", "steelblue1", false, "lightsteelblue1", false, "DEBUG")
        },
        {
            LogLevel.Information,
            new LogLevelStyle("INFO", "skyblue3", false, "silver", true, "INFO ")
        },
        { LogLevel.Warning, new LogLevelStyle("WARN", "gold1", true, "gold1", false, "WARN ") },
        { LogLevel.Error, new LogLevelStyle("ERROR", "red3", true, "red3", false, "ERROR") },
        {
            LogLevel.Critical,
            new LogLevelStyle("CRIT", "magenta1", true, "magenta1", false, "CRIT ")
        },
    };

    private static readonly string[] CategoryPalette =
    {
        "cyan1",
        "deepskyblue1",
        "mediumorchid",
        "springgreen1",
        "gold1",
        "lightsteelblue",
        "dodgerblue3",
        "mediumvioletred",
        "turquoise2",
        "plum1",
    };

    public static bool SupportsMarkup => _supportsMarkup.Value;

    public static string FormatLogLine(
        LogLevel level,
        string category,
        string message,
        string? scope,
        DateTime timestamp,
        bool messageIsMarkup
    )
    {
        LogLevelStyle style = LogLevelStyles.TryGetValue(level, out LogLevelStyle? descriptor)
            ? descriptor
            : DefaultStyle;

        if (SupportsMarkup)
        {
            var builder = new StringBuilder();
            builder.Append($"[grey][[{timestamp:HH:mm:ss.fff}]][/] ");
            builder.Append($"{FormatLevelToken(style)} ");

            if (!string.IsNullOrWhiteSpace(scope))
            {
                builder.Append($"[dim][[{EscapeMarkup(scope!)}]][/] ");
            }

            builder.Append($"{FormatCategory(category)}: ");

            if (messageIsMarkup)
            {
                builder.Append(message);
            }
            else
            {
                builder.Append(FormatMessage(style, message));
            }

            return builder.ToString();
        }
        else
        {
            var builder = new StringBuilder();
            builder.Append($"[{timestamp:HH:mm:ss.fff}] ");
            builder.AppendFormat(PlainLevelTemplate, PadRight(style.PlainLabel, LevelLabelWidth));
            builder.Append(' ');

            if (!string.IsNullOrWhiteSpace(scope))
            {
                builder.Append($"[{scope}] ");
            }

            builder.Append(PadRight(category, CategoryWidth));
            builder.Append(": ");

            builder.Append(StripMarkup(message));
            return builder.ToString();
        }
    }

    public static IEnumerable<string> FormatExceptionLines(Exception ex, bool includeStackTrace)
    {
        string exceptionType = ex.GetType().Name;
        string exceptionMessage = ex.Message ?? string.Empty;
        if (SupportsMarkup)
        {
            yield return $"[red]  Exception: {EscapeMarkup(exceptionType)}: {EscapeMarkup(exceptionMessage)}[/]";
            if (includeStackTrace)
            {
                string trace = ex.StackTrace ?? "N/A";
                yield return $"[dim red]  StackTrace: {EscapeMarkup(trace)}[/]";
            }
        }
        else
        {
            yield return $"  Exception: {exceptionType}: {exceptionMessage}";
            if (includeStackTrace)
            {
                string trace = ex.StackTrace ?? "N/A";
                yield return $"  StackTrace: {trace}";
            }
        }
    }

    public static string FormatTemplate(string markup)
    {
        return SupportsMarkup ? markup : StripMarkup(markup);
    }

    public static bool ContainsMarkup(string? text)
    {
        if (!SupportsMarkup || string.IsNullOrEmpty(text))
        {
            return false;
        }

        return _markupRegex.IsMatch(text);
    }

    public static string EscapeMarkup(string text)
    {
        return Markup.Escape(text);
    }

    public static string StripMarkup(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        string withoutTags = _markupRegex
            .Replace(text, string.Empty)
            .Replace("[[", "[")
            .Replace("]]", "]");

        foreach ((string glyph, string replacement) in PlainGlyphMap)
        {
            withoutTags = withoutTags.Replace(glyph, replacement, StringComparison.Ordinal);
        }

        return withoutTags;
    }

    private static string FormatLevelToken(LogLevelStyle style)
    {
        string padded = PadRight(style.Label, LevelLabelWidth);
        string tokenStyle = style.TokenBold ? $"{style.TokenColor} bold" : style.TokenColor;
        return $"[{tokenStyle}][[{EscapeMarkup(padded)}]][/]";
    }

    private static string FormatMessage(LogLevelStyle style, string message)
    {
        string messageStyle = style.MessageDim ? $"{style.MessageColor} dim" : style.MessageColor;
        return $"[{messageStyle}]{EscapeMarkup(message)}[/]";
    }

    private static string FormatCategory(string category)
    {
        string sanitized = EscapeMarkup(category);
        string padded = PadRight(sanitized, CategoryWidth);
        string color = CategoryPalette[ComputeHash(category) % CategoryPalette.Length];
        return $"[{color} bold]{padded}[/]";
    }

    private static int ComputeHash(string value)
    {
        int hash = 0;
        foreach (char c in value)
        {
            hash = ((hash * 31) + c) & 0x7FFFFFFF;
        }

        return hash;
    }

    private static string PadRight(string value, int width)
    {
        if (string.IsNullOrEmpty(value))
        {
            return new string(' ', width);
        }

        if (value.Length >= width)
        {
            return value.Length == width ? value : value[..width];
        }

        return value + new string(' ', width - value.Length);
    }

    private sealed record LogLevelStyle(
        string Label,
        string TokenColor,
        bool TokenBold,
        string MessageColor,
        bool MessageDim,
        string PlainLabel
    );
}
