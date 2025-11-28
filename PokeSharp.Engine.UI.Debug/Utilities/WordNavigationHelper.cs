namespace PokeSharp.Engine.UI.Debug.Utilities;

/// <summary>
///     Helper methods for word-based navigation in text.
///     Supports CamelCase and various word boundary detections.
/// </summary>
public static class WordNavigationHelper
{
    /// <summary>
    ///     Finds the start position of the previous word from the given position.
    /// </summary>
    public static int FindPreviousWordStart(string text, int position)
    {
        if (string.IsNullOrEmpty(text) || position <= 0)
        {
            return 0;
        }

        position = Math.Clamp(position, 0, text.Length);

        // Skip any whitespace to the left
        while (position > 0 && char.IsWhiteSpace(text[position - 1]))
        {
            position--;
        }

        if (position == 0)
        {
            return 0;
        }

        // Now find the start of the word
        char startChar = text[position - 1];

        if (char.IsLetterOrDigit(startChar) || startChar == '_')
        {
            // Alphanumeric word - stop at non-alphanumeric
            while (position > 0)
            {
                char ch = text[position - 1];
                if (!char.IsLetterOrDigit(ch) && ch != '_')
                {
                    break;
                }

                // CamelCase support: stop before capital letter
                if (position > 1 && char.IsUpper(ch) && char.IsLower(text[position - 2]))
                {
                    break;
                }

                position--;
            }
        }
        else
        {
            // Punctuation/symbol word - stop at different type
            while (position > 0)
            {
                char ch = text[position - 1];
                if (char.IsWhiteSpace(ch) || char.IsLetterOrDigit(ch) || ch == '_')
                {
                    break;
                }

                position--;
            }
        }

        return position;
    }

    /// <summary>
    ///     Finds the end position of the next word from the given position.
    /// </summary>
    public static int FindNextWordEnd(string text, int position)
    {
        if (string.IsNullOrEmpty(text) || position >= text.Length)
        {
            return text?.Length ?? 0;
        }

        position = Math.Clamp(position, 0, text.Length);

        // Skip any whitespace to the right
        while (position < text.Length && char.IsWhiteSpace(text[position]))
        {
            position++;
        }

        if (position >= text.Length)
        {
            return text.Length;
        }

        // Now find the end of the word
        char startChar = text[position];

        if (char.IsLetterOrDigit(startChar) || startChar == '_')
        {
            // Alphanumeric word - stop at non-alphanumeric
            while (position < text.Length)
            {
                char ch = text[position];
                if (!char.IsLetterOrDigit(ch) && ch != '_')
                {
                    break;
                }

                // CamelCase support: stop at capital letter
                if (position > 0 && char.IsUpper(ch) && char.IsLower(text[position - 1]))
                {
                    break;
                }

                position++;
            }
        }
        else
        {
            // Punctuation/symbol word - stop at different type
            while (position < text.Length)
            {
                char ch = text[position];
                if (char.IsWhiteSpace(ch) || char.IsLetterOrDigit(ch) || ch == '_')
                {
                    break;
                }

                position++;
            }
        }

        return position;
    }

    /// <summary>
    ///     Checks if a character is a word boundary.
    /// </summary>
    public static bool IsWordBoundary(char ch)
    {
        return char.IsWhiteSpace(ch)
            || ch == '('
            || ch == ')'
            || ch == '['
            || ch == ']'
            || ch == '{'
            || ch == '}'
            || ch == ','
            || ch == ';'
            || ch == '.'
            || ch == '='
            || ch == '+'
            || ch == '-'
            || ch == '*'
            || ch == '/'
            || ch == '<'
            || ch == '>'
            || ch == '!'
            || ch == '?'
            || ch == ':'
            || ch == '"'
            || ch == '\''
            || ch == '`';
    }
}
