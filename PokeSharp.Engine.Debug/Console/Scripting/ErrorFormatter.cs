using System.Text;
using Microsoft.CodeAnalysis;

namespace PokeSharp.Engine.Debug.Console.Scripting;

/// <summary>
///     Formats compilation errors with context, line numbers, and helpful messages.
/// </summary>
public static class ErrorFormatter
{
    /// <summary>
    ///     Formats a compilation error with context lines and caret position.
    /// </summary>
    /// <param name="diagnostic">The diagnostic error from Roslyn.</param>
    /// <param name="sourceCode">The source code that was being compiled.</param>
    /// <returns>A formatted error message with context.</returns>
    public static FormattedError FormatError(Diagnostic diagnostic, string sourceCode)
    {
        FileLinePositionSpan lineSpan = diagnostic.Location.GetLineSpan();
        int startLine = lineSpan.StartLinePosition.Line;
        int startColumn = lineSpan.StartLinePosition.Character;
        int endLine = lineSpan.EndLinePosition.Line;
        int endColumn = lineSpan.EndLinePosition.Character;

        string[] lines = sourceCode.Split('\n');
        string errorMessage = diagnostic.GetMessage();
        string errorCode = diagnostic.Id;

        // Build context with lines around the error
        var context = new List<ContextLine>();

        // Show 2 lines before error (if available)
        for (int i = Math.Max(0, startLine - 2); i < startLine; i++)
        {
            if (i < lines.Length)
            {
                context.Add(new ContextLine(i + 1, lines[i], false));
            }
        }

        // Show the error line(s)
        for (int i = startLine; i <= Math.Min(endLine, lines.Length - 1); i++)
        {
            if (i < lines.Length)
            {
                context.Add(new ContextLine(i + 1, lines[i], true));
            }
        }

        // Show 1 line after error (if available)
        if (endLine + 1 < lines.Length)
        {
            context.Add(new ContextLine(endLine + 2, lines[endLine + 1], false));
        }

        return new FormattedError
        {
            ErrorCode = errorCode,
            Message = errorMessage,
            Line = startLine + 1, // Convert to 1-based
            Column = startColumn + 1, // Convert to 1-based
            EndLine = endLine + 1,
            EndColumn = endColumn + 1,
            Context = context,
            Severity = diagnostic.Severity,
        };
    }

    /// <summary>
    ///     Formats multiple errors.
    /// </summary>
    public static List<FormattedError> FormatErrors(
        IEnumerable<Diagnostic> diagnostics,
        string sourceCode
    )
    {
        return diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => FormatError(d, sourceCode))
            .ToList();
    }

    /// <summary>
    ///     Generates a caret line pointing to the error position.
    /// </summary>
    public static string GenerateCaretLine(int column, int length = 1)
    {
        var sb = new StringBuilder();
        sb.Append(' ', column - 1); // Offset to the column (1-based)
        sb.Append('^');
        if (length > 1)
        {
            sb.Append(new string('~', length - 1));
        }

        return sb.ToString();
    }
}

/// <summary>
///     Represents a formatted compilation error with context.
/// </summary>
public class FormattedError
{
    public string ErrorCode { get; set; } = "";
    public string Message { get; set; } = "";
    public int Line { get; set; }
    public int Column { get; set; }
    public int EndLine { get; set; }
    public int EndColumn { get; set; }
    public List<ContextLine> Context { get; set; } = new();
    public DiagnosticSeverity Severity { get; set; }
}

/// <summary>
///     Represents a line of context around an error.
/// </summary>
public class ContextLine
{
    public ContextLine(int lineNumber, string text, bool isErrorLine)
    {
        LineNumber = lineNumber;
        Text = text;
        IsErrorLine = isErrorLine;
    }

    public int LineNumber { get; set; }
    public string Text { get; set; }
    public bool IsErrorLine { get; set; }
}
