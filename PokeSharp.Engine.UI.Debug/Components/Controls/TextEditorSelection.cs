using System.Text;

namespace PokeSharp.Engine.UI.Debug.Components.Controls;

/// <summary>
///     Manages text selection state for a text editor.
///     Handles selection ranges, manipulation, and querying.
/// </summary>
public class TextEditorSelection
{
    /// <summary>
    ///     Gets whether there is an active selection.
    /// </summary>
    public bool HasSelection { get; private set; }

    /// <summary>
    ///     Gets the selection start line.
    /// </summary>
    public int StartLine { get; private set; }

    /// <summary>
    ///     Gets the selection start column.
    /// </summary>
    public int StartColumn { get; private set; }

    /// <summary>
    ///     Gets the selection end line.
    /// </summary>
    public int EndLine { get; private set; }

    /// <summary>
    ///     Gets the selection end column.
    /// </summary>
    public int EndColumn { get; private set; }

    /// <summary>
    ///     Starts a new selection at the given position.
    /// </summary>
    public void Start(int line, int column)
    {
        HasSelection = true;
        StartLine = line;
        StartColumn = column;
        EndLine = line;
        EndColumn = column;
    }

    /// <summary>
    ///     Extends the selection to the given position.
    /// </summary>
    public void ExtendTo(int line, int column)
    {
        if (!HasSelection)
        {
            Start(line, column);
            return;
        }

        EndLine = line;
        EndColumn = column;
    }

    /// <summary>
    ///     Clears the selection.
    /// </summary>
    public void Clear()
    {
        HasSelection = false;
    }

    /// <summary>
    ///     Selects all text.
    /// </summary>
    public void SelectAll(int totalLines, Func<int, int> getLineLength)
    {
        HasSelection = true;
        StartLine = 0;
        StartColumn = 0;
        EndLine = totalLines - 1;
        EndColumn = getLineLength(EndLine);
    }

    /// <summary>
    ///     Selects the word at the given position.
    /// </summary>
    public void SelectWord(int line, int column, string lineText)
    {
        if (string.IsNullOrEmpty(lineText) || column > lineText.Length)
        {
            Clear();
            return;
        }

        // Find word boundaries
        int wordStart = column;
        int wordEnd = column;

        // Move start back to word boundary
        while (wordStart > 0 && IsWordChar(lineText[wordStart - 1]))
        {
            wordStart--;
        }

        // Move end forward to word boundary
        while (wordEnd < lineText.Length && IsWordChar(lineText[wordEnd]))
        {
            wordEnd++;
        }

        HasSelection = true;
        StartLine = line;
        StartColumn = wordStart;
        EndLine = line;
        EndColumn = wordEnd;
    }

    /// <summary>
    ///     Selects the entire line at the given position.
    /// </summary>
    public void SelectLine(int line, int lineLength)
    {
        HasSelection = true;
        StartLine = line;
        StartColumn = 0;
        EndLine = line;
        EndColumn = lineLength;
    }

    /// <summary>
    ///     Gets the normalized selection range (start before end).
    /// </summary>
    public (int startLine, int startColumn, int endLine, int endColumn) GetNormalizedRange()
    {
        if (!HasSelection)
        {
            return (0, 0, 0, 0);
        }

        // Normalize so start is always before end
        if (StartLine > EndLine || (StartLine == EndLine && StartColumn > EndColumn))
        {
            return (EndLine, EndColumn, StartLine, StartColumn);
        }

        return (StartLine, StartColumn, EndLine, EndColumn);
    }

    /// <summary>
    ///     Checks if a given position is within the selection.
    /// </summary>
    public bool ContainsPosition(int line, int column)
    {
        if (!HasSelection)
        {
            return false;
        }

        (int startLine, int startColumn, int endLine, int endColumn) = GetNormalizedRange();

        if (line < startLine || line > endLine)
        {
            return false;
        }

        if (line == startLine && line == endLine)
        {
            return column >= startColumn && column < endColumn;
        }

        if (line == startLine)
        {
            return column >= startColumn;
        }

        if (line == endLine)
        {
            return column < endColumn;
        }

        return true; // Middle line
    }

    /// <summary>
    ///     Gets the selected text from the given lines.
    /// </summary>
    public string GetSelectedText(List<string> lines)
    {
        if (!HasSelection || lines.Count == 0)
        {
            return string.Empty;
        }

        (int startLine, int startColumn, int endLine, int endColumn) = GetNormalizedRange();

        // Ensure indices are valid
        startLine = Math.Clamp(startLine, 0, lines.Count - 1);
        endLine = Math.Clamp(endLine, 0, lines.Count - 1);

        if (startLine == endLine)
        {
            // Single line selection
            string line = lines[startLine];
            startColumn = Math.Clamp(startColumn, 0, line.Length);
            endColumn = Math.Clamp(endColumn, 0, line.Length);
            return line.Substring(startColumn, endColumn - startColumn);
        }

        // Multi-line selection
        var result = new StringBuilder();

        for (int i = startLine; i <= endLine; i++)
        {
            string line = lines[i];

            if (i == startLine)
            {
                // First line: from start column to end
                startColumn = Math.Clamp(startColumn, 0, line.Length);
                result.Append(line.Substring(startColumn));
            }
            else if (i == endLine)
            {
                // Last line: from beginning to end column
                endColumn = Math.Clamp(endColumn, 0, line.Length);
                result.Append(line.Substring(0, endColumn));
            }
            else
            {
                // Middle lines: entire line
                result.Append(line);
            }

            if (i < endLine)
            {
                result.Append('\n');
            }
        }

        return result.ToString();
    }

    /// <summary>
    ///     Checks if a character is part of a word (for word selection).
    /// </summary>
    private static bool IsWordChar(char c)
    {
        return char.IsLetterOrDigit(c) || c == '_';
    }
}
