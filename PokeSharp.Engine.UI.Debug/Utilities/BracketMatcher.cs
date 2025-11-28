namespace PokeSharp.Engine.UI.Debug.Utilities;

/// <summary>
///     Utility for finding matching brackets in text.
///     Supports (), [], and {} bracket pairs.
/// </summary>
public static class BracketMatcher
{
    private static readonly Dictionary<char, char> _openToClose = new()
    {
        { '(', ')' },
        { '[', ']' },
        { '{', '}' },
    };

    private static readonly Dictionary<char, char> _closeToOpen = new()
    {
        { ')', '(' },
        { ']', '[' },
        { '}', '{' },
    };

    /// <summary>
    ///     Checks if a character is an opening bracket.
    /// </summary>
    public static bool IsOpeningBracket(char ch)
    {
        return _openToClose.ContainsKey(ch);
    }

    /// <summary>
    ///     Checks if a character is a closing bracket.
    /// </summary>
    public static bool IsClosingBracket(char ch)
    {
        return _closeToOpen.ContainsKey(ch);
    }

    /// <summary>
    ///     Checks if a character is any kind of bracket.
    /// </summary>
    public static bool IsBracket(char ch)
    {
        return IsOpeningBracket(ch) || IsClosingBracket(ch);
    }

    /// <summary>
    ///     Finds the matching bracket for the bracket at the given position.
    ///     Returns the position of the matching bracket, or null if not found.
    /// </summary>
    /// <param name="lines">The text lines.</param>
    /// <param name="line">The line index of the bracket.</param>
    /// <param name="column">The column index of the bracket.</param>
    /// <returns>The (line, column) of the matching bracket, or null if not found or not a bracket.</returns>
    public static (int line, int column)? FindMatchingBracket(
        List<string> lines,
        int line,
        int column
    )
    {
        if (line < 0 || line >= lines.Count)
        {
            return null;
        }

        string currentLine = lines[line];
        if (column < 0 || column >= currentLine.Length)
        {
            return null;
        }

        char bracketChar = currentLine[column];

        if (IsOpeningBracket(bracketChar))
        {
            // Search forward for closing bracket
            return FindClosingBracket(lines, line, column, bracketChar, _openToClose[bracketChar]);
        }

        if (IsClosingBracket(bracketChar))
        {
            // Search backward for opening bracket
            return FindOpeningBracket(lines, line, column, _closeToOpen[bracketChar], bracketChar);
        }

        return null; // Not a bracket
    }

    private static (int line, int column)? FindClosingBracket(
        List<string> lines,
        int startLine,
        int startCol,
        char openChar,
        char closeChar
    )
    {
        int depth = 1; // We start at an opening bracket

        // Start searching from the character after the opening bracket
        int col = startCol + 1;

        for (int lineIdx = startLine; lineIdx < lines.Count; lineIdx++)
        {
            string line = lines[lineIdx];

            for (int i = col; i < line.Length; i++)
            {
                char ch = line[i];

                if (ch == openChar)
                {
                    depth++;
                }
                else if (ch == closeChar)
                {
                    depth--;
                    if (depth == 0)
                    {
                        return (lineIdx, i);
                    }
                }
            }

            // Continue from start of next line
            col = 0;
        }

        return null; // No matching bracket found
    }

    private static (int line, int column)? FindOpeningBracket(
        List<string> lines,
        int startLine,
        int startCol,
        char openChar,
        char closeChar
    )
    {
        int depth = 1; // We start at a closing bracket

        // Start searching from the character before the closing bracket
        int col = startCol - 1;

        for (int lineIdx = startLine; lineIdx >= 0; lineIdx--)
        {
            string line = lines[lineIdx];

            for (int i = col; i >= 0; i--)
            {
                char ch = line[i];

                if (ch == closeChar)
                {
                    depth++;
                }
                else if (ch == openChar)
                {
                    depth--;
                    if (depth == 0)
                    {
                        return (lineIdx, i);
                    }
                }
            }

            // Continue from end of previous line
            if (lineIdx > 0)
            {
                col = lines[lineIdx - 1].Length - 1;
            }
        }

        return null; // No matching bracket found
    }

    /// <summary>
    ///     Finds bracket positions near the cursor (before or at cursor).
    ///     Returns positions for both the cursor bracket and its match.
    /// </summary>
    public static (
        (int line, int col) cursor,
        (int line, int col) match
    )? FindBracketPairNearCursor(List<string> lines, int cursorLine, int cursorColumn)
    {
        if (cursorLine < 0 || cursorLine >= lines.Count)
        {
            return null;
        }

        string line = lines[cursorLine];

        // Check character at cursor
        if (cursorColumn < line.Length)
        {
            (int line, int column)? match = FindMatchingBracket(lines, cursorLine, cursorColumn);
            if (match.HasValue)
            {
                return ((cursorLine, cursorColumn), match.Value);
            }
        }

        // Check character before cursor
        if (cursorColumn > 0)
        {
            (int line, int column)? match = FindMatchingBracket(
                lines,
                cursorLine,
                cursorColumn - 1
            );
            if (match.HasValue)
            {
                return ((cursorLine, cursorColumn - 1), match.Value);
            }
        }

        return null;
    }
}
