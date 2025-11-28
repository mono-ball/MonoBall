using Microsoft.Xna.Framework;
using PokeSharp.Engine.UI.Debug.Core;

namespace PokeSharp.Engine.UI.Debug.Utilities;

/// <summary>
///     Segment of text with a specific color for syntax highlighting.
/// </summary>
public readonly record struct ColoredSegment(string Text, Color Color, int StartIndex, int Length);

/// <summary>
///     Reusable syntax highlighter for C# code.
///     Provides comprehensive highlighting with support for methods, types, strings, comments, and more.
///     Uses ThemeManager for theme-aware colors.
/// </summary>
public static class SyntaxHighlighter
{
    // C# Keywords
    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "abstract",
        "as",
        "base",
        "break",
        "case",
        "catch",
        "checked",
        "class",
        "const",
        "continue",
        "default",
        "delegate",
        "do",
        "else",
        "enum",
        "event",
        "explicit",
        "extern",
        "finally",
        "fixed",
        "for",
        "foreach",
        "goto",
        "if",
        "implicit",
        "in",
        "interface",
        "internal",
        "is",
        "lock",
        "namespace",
        "new",
        "operator",
        "out",
        "override",
        "params",
        "private",
        "protected",
        "public",
        "readonly",
        "ref",
        "return",
        "sealed",
        "sizeof",
        "stackalloc",
        "static",
        "struct",
        "switch",
        "this",
        "throw",
        "try",
        "typeof",
        "unchecked",
        "unsafe",
        "using",
        "virtual",
        "volatile",
        "while",
        "async",
        "await",
        "yield",
        "when",
        "where",
        "get",
        "set",
        "add",
        "remove",
        "value",
        "nameof",
        "with",
        "init",
        "record",
        "global",
        "partial",
        "dynamic",
        "required",
        "scoped",
        "file",
    };

    // Control flow keywords (special color)
    private static readonly HashSet<string> ControlKeywords = new(StringComparer.Ordinal)
    {
        "if",
        "else",
        "switch",
        "case",
        "default",
        "for",
        "foreach",
        "while",
        "do",
        "break",
        "continue",
        "return",
        "throw",
        "try",
        "catch",
        "finally",
        "goto",
        "yield",
    };

    // Type keywords (built-in types)
    private static readonly HashSet<string> TypeKeywords = new(StringComparer.Ordinal)
    {
        "bool",
        "byte",
        "char",
        "decimal",
        "double",
        "float",
        "int",
        "long",
        "object",
        "sbyte",
        "short",
        "string",
        "uint",
        "ulong",
        "ushort",
        "void",
        "nint",
        "nuint",
    };

    // Literal keywords
    private static readonly HashSet<string> LiteralKeywords = new(StringComparer.Ordinal)
    {
        "true",
        "false",
        "null",
    };

    // Common type names (for better highlighting of common .NET types)
    private static readonly HashSet<string> CommonTypes = new(StringComparer.Ordinal)
    {
        "Console",
        "String",
        "Int32",
        "Int64",
        "Double",
        "Boolean",
        "Object",
        "Array",
        "List",
        "Dictionary",
        "HashSet",
        "Queue",
        "Stack",
        "LinkedList",
        "Task",
        "Action",
        "Func",
        "Predicate",
        "EventHandler",
        "Exception",
        "ArgumentException",
        "InvalidOperationException",
        "NullReferenceException",
        "DateTime",
        "TimeSpan",
        "Guid",
        "Uri",
        "Regex",
        "StringBuilder",
        "File",
        "Directory",
        "Path",
        "Stream",
        "StreamReader",
        "StreamWriter",
        "Math",
        "Convert",
        "Enum",
        "Tuple",
        "ValueTuple",
        "Nullable",
        "IEnumerable",
        "IList",
        "IDictionary",
        "ICollection",
        "IDisposable",
        "IComparable",
        "Type",
        "Attribute",
        "Delegate",
        "EventArgs",
        // Game-specific
        "Player",
        "Game",
        "World",
        "Entity",
        "Component",
        "Scene",
        "Vector2",
        "Vector3",
        "Color",
        "Rectangle",
        "Point",
        "GameTime",
        "SpriteBatch",
        "Texture2D",
    };

    // Theme-aware color accessors
    private static UITheme Theme => ThemeManager.Current;

    // Theme-aware color scheme - delegates to current theme's syntax colors
    public static Color DefaultColor => Theme.SyntaxDefault;
    public static Color KeywordColor => Theme.SyntaxKeyword;
    public static Color ControlColor => Theme.SyntaxKeyword; // Use keyword color for control flow
    public static Color TypeColor => Theme.SyntaxType;
    public static Color StringColor => Theme.SyntaxString;
    public static Color CommentColor => Theme.SyntaxComment;
    public static Color NumberColor => Theme.SyntaxNumber;
    public static Color MethodColor => Theme.SyntaxMethod;
    public static Color PropertyColor => Theme.SyntaxDefault; // Properties use default color
    public static Color ParameterColor => Theme.SyntaxDefault; // Parameters use default color
    public static Color LiteralColor => Theme.SyntaxKeyword; // Literals use keyword color
    public static Color OperatorColor => Theme.SyntaxOperator;
    public static Color AttributeColor => Theme.SyntaxType; // Attributes use type color
    public static Color InterpolationColor => Theme.SyntaxStringInterpolation;

    /// <summary>
    ///     Highlights C# code and returns colored segments.
    /// </summary>
    public static List<ColoredSegment> Highlight(string code)
    {
        if (string.IsNullOrEmpty(code))
        {
            return new List<ColoredSegment>();
        }

        var segments = new List<ColoredSegment>();
        int position = 0;
        char? prevNonWhitespace = null; // Track context

        while (position < code.Length)
        {
            // Skip whitespace
            if (char.IsWhiteSpace(code[position]))
            {
                int start = position;
                while (position < code.Length && char.IsWhiteSpace(code[position]))
                {
                    position++;
                }

                segments.Add(
                    new ColoredSegment(
                        code.Substring(start, position - start),
                        DefaultColor,
                        start,
                        position - start
                    )
                );
                continue;
            }

            // Preprocessor directives
            if (code[position] == '#' && (position == 0 || code[position - 1] == '\n'))
            {
                int start = position;
                while (position < code.Length && code[position] != '\n')
                {
                    position++;
                }

                segments.Add(
                    new ColoredSegment(
                        code.Substring(start, position - start),
                        CommentColor,
                        start,
                        position - start
                    )
                );
                continue;
            }

            // Single-line comment
            if (position < code.Length - 1 && code[position] == '/' && code[position + 1] == '/')
            {
                int start = position;
                while (position < code.Length && code[position] != '\n')
                {
                    position++;
                }

                segments.Add(
                    new ColoredSegment(
                        code.Substring(start, position - start),
                        CommentColor,
                        start,
                        position - start
                    )
                );
                continue;
            }

            // Multi-line comment
            if (position < code.Length - 1 && code[position] == '/' && code[position + 1] == '*')
            {
                int start = position;
                position += 2;
                while (position < code.Length - 1)
                {
                    if (code[position] == '*' && code[position + 1] == '/')
                    {
                        position += 2;
                        break;
                    }

                    position++;
                }

                if (position >= code.Length - 1 && position < code.Length)
                {
                    position = code.Length;
                }

                segments.Add(
                    new ColoredSegment(
                        code.Substring(start, position - start),
                        CommentColor,
                        start,
                        position - start
                    )
                );
                continue;
            }

            // Interpolated string $"..."
            if (code[position] == '$' && position < code.Length - 1 && code[position + 1] == '"')
            {
                HighlightInterpolatedString(code, ref position, segments);
                prevNonWhitespace = '"';
                continue;
            }

            // Verbatim string @"..."
            if (code[position] == '@' && position < code.Length - 1 && code[position + 1] == '"')
            {
                int start = position;
                position += 2; // Skip @"

                while (position < code.Length)
                {
                    if (code[position] == '"')
                    {
                        if (position < code.Length - 1 && code[position + 1] == '"')
                        {
                            position += 2; // Escaped quote
                            continue;
                        }

                        position++; // Closing quote
                        break;
                    }

                    position++;
                }

                segments.Add(
                    new ColoredSegment(
                        code.Substring(start, position - start),
                        StringColor,
                        start,
                        position - start
                    )
                );
                prevNonWhitespace = '"';
                continue;
            }

            // Regular string literals
            if (code[position] == '"')
            {
                int start = position;
                position++; // Skip opening quote

                while (position < code.Length)
                {
                    if (code[position] == '\\' && position < code.Length - 1)
                    {
                        position += 2; // Skip escape sequence
                        continue;
                    }

                    if (code[position] == '"')
                    {
                        position++; // Include closing quote
                        break;
                    }

                    position++;
                }

                segments.Add(
                    new ColoredSegment(
                        code.Substring(start, position - start),
                        StringColor,
                        start,
                        position - start
                    )
                );
                prevNonWhitespace = '"';
                continue;
            }

            // Character literals
            if (code[position] == '\'')
            {
                int start = position;
                position++; // Skip opening quote

                while (position < code.Length)
                {
                    if (code[position] == '\\' && position < code.Length - 1)
                    {
                        position += 2; // Skip escape sequence
                        continue;
                    }

                    if (code[position] == '\'')
                    {
                        position++; // Include closing quote
                        break;
                    }

                    position++;
                }

                segments.Add(
                    new ColoredSegment(
                        code.Substring(start, position - start),
                        StringColor,
                        start,
                        position - start
                    )
                );
                prevNonWhitespace = '\'';
                continue;
            }

            // Attributes [...]
            if (code[position] == '[')
            {
                // Check if this looks like an attribute (not array access)
                if (
                    prevNonWhitespace == null
                    || prevNonWhitespace == '\n'
                    || prevNonWhitespace == '{'
                    || prevNonWhitespace == ';'
                    || prevNonWhitespace == ']'
                )
                {
                    int start = position;
                    int depth = 1;
                    position++;

                    while (position < code.Length && depth > 0)
                    {
                        if (code[position] == '[')
                        {
                            depth++;
                        }
                        else if (code[position] == ']')
                        {
                            depth--;
                        }

                        position++;
                    }

                    segments.Add(
                        new ColoredSegment(
                            code.Substring(start, position - start),
                            AttributeColor,
                            start,
                            position - start
                        )
                    );
                    prevNonWhitespace = ']';
                    continue;
                }
            }

            // Numbers (including hex, binary, with underscores)
            if (
                char.IsDigit(code[position])
                || (
                    code[position] == '.'
                    && position < code.Length - 1
                    && char.IsDigit(code[position + 1])
                )
            )
            {
                int start = position;

                // Check for hex (0x) or binary (0b)
                if (code[position] == '0' && position < code.Length - 1)
                {
                    if (code[position + 1] == 'x' || code[position + 1] == 'X')
                    {
                        position += 2;
                        while (
                            position < code.Length
                            && (IsHexDigit(code[position]) || code[position] == '_')
                        )
                        {
                            position++;
                        }
                    }
                    else if (code[position + 1] == 'b' || code[position + 1] == 'B')
                    {
                        position += 2;
                        while (
                            position < code.Length
                            && (
                                code[position] == '0'
                                || code[position] == '1'
                                || code[position] == '_'
                            )
                        )
                        {
                            position++;
                        }
                    }
                    else
                    {
                        ParseDecimalNumber(code, ref position);
                    }
                }
                else
                {
                    ParseDecimalNumber(code, ref position);
                }

                // Suffix (f, d, m, l, u, ul, etc.)
                while (position < code.Length && "fFdDmMlLuU".Contains(code[position]))
                {
                    position++;
                }

                segments.Add(
                    new ColoredSegment(
                        code.Substring(start, position - start),
                        NumberColor,
                        start,
                        position - start
                    )
                );
                prevNonWhitespace = '0';
                continue;
            }

            // Identifiers and keywords
            if (char.IsLetter(code[position]) || code[position] == '_' || code[position] == '@')
            {
                int start = position;
                if (code[position] == '@')
                {
                    position++; // Skip @ for verbatim identifiers
                }

                while (
                    position < code.Length
                    && (char.IsLetterOrDigit(code[position]) || code[position] == '_')
                )
                {
                    position++;
                }

                string word = code.Substring(start, position - start);
                string bareWord = word.StartsWith("@") ? word.Substring(1) : word;

                Color color = GetIdentifierColor(bareWord, code, position, prevNonWhitespace);

                segments.Add(new ColoredSegment(word, color, start, position - start));
                prevNonWhitespace = word.Length > 0 ? word[^1] : null;
                continue;
            }

            // Operators and punctuation
            int opStart = position;
            char op = code[position];
            position++;

            // Handle multi-character operators
            if (position < code.Length)
            {
                char next = code[position];
                if (
                    (op == '=' && next == '=')
                    || (op == '!' && next == '=')
                    || (op == '<' && next == '=')
                    || (op == '>' && next == '=')
                    || (op == '&' && next == '&')
                    || (op == '|' && next == '|')
                    || (op == '+' && next == '+')
                    || (op == '-' && next == '-')
                    || (op == '+' && next == '=')
                    || (op == '-' && next == '=')
                    || (op == '*' && next == '=')
                    || (op == '/' && next == '=')
                    || (op == '?' && next == '?')
                    || (op == '?' && next == '.')
                    || (op == '=' && next == '>')
                    || (op == '-' && next == '>')
                    || (op == '<' && next == '<')
                    || (op == '>' && next == '>')
                )
                {
                    position++;
                }
            }

            segments.Add(
                new ColoredSegment(
                    code.Substring(opStart, position - opStart),
                    OperatorColor,
                    opStart,
                    position - opStart
                )
            );
            prevNonWhitespace = op;
        }

        return segments;
    }

    /// <summary>
    ///     Determines the color for an identifier based on context.
    /// </summary>
    private static Color GetIdentifierColor(
        string word,
        string code,
        int position,
        char? prevNonWhitespace
    )
    {
        // Literal keywords
        if (LiteralKeywords.Contains(word))
        {
            return LiteralColor;
        }

        // Built-in type keywords
        if (TypeKeywords.Contains(word))
        {
            return TypeColor;
        }

        // Control flow keywords
        if (ControlKeywords.Contains(word))
        {
            return ControlColor;
        }

        // Other keywords
        if (Keywords.Contains(word))
        {
            return KeywordColor;
        }

        // Check what comes after this identifier
        int lookAhead = position;
        while (lookAhead < code.Length && char.IsWhiteSpace(code[lookAhead]))
        {
            lookAhead++;
        }

        bool followedByParen = lookAhead < code.Length && code[lookAhead] == '(';
        bool followedByGeneric = lookAhead < code.Length && code[lookAhead] == '<';

        // Method call: identifier followed by (
        if (followedByParen)
        {
            // Check if it's after a dot (member method call)
            if (prevNonWhitespace == '.')
            {
                return MethodColor;
            }

            // Check if it's a known type (constructor call)
            if (CommonTypes.Contains(word) || char.IsUpper(word[0]))
            {
                // Could be constructor or static method
                return MethodColor;
            }

            return MethodColor;
        }

        // Generic type: identifier followed by <
        if (followedByGeneric)
        {
            return TypeColor;
        }

        // After 'new' keyword - it's a type
        if (prevNonWhitespace == 'w') // Last char of 'new'
        {
            // Need to look back further to confirm it's 'new'
            return TypeColor;
        }

        // After a dot - it's a member (property/field)
        if (prevNonWhitespace == '.')
        {
            return PropertyColor;
        }

        // Known types
        if (CommonTypes.Contains(word))
        {
            return TypeColor;
        }

        // PascalCase identifier starting with uppercase - likely a type
        if (word.Length > 1 && char.IsUpper(word[0]) && !word.All(char.IsUpper))
        {
            // Check context to distinguish type from property/method
            if (prevNonWhitespace == ':' || prevNonWhitespace == '<' || prevNonWhitespace == '(')
            {
                return TypeColor;
            }

            // If followed by identifier (space then letter), likely a type declaration
            if (
                lookAhead < code.Length
                && (char.IsLetter(code[lookAhead]) || code[lookAhead] == '_')
            )
            {
                return TypeColor;
            }

            return TypeColor; // Default to type for PascalCase
        }

        return DefaultColor;
    }

    /// <summary>
    ///     Parses a decimal number (with possible decimal point and exponent).
    /// </summary>
    private static void ParseDecimalNumber(string code, ref int position)
    {
        // Integer part
        while (position < code.Length && (char.IsDigit(code[position]) || code[position] == '_'))
        {
            position++;
        }

        // Decimal part
        if (position < code.Length && code[position] == '.')
        {
            if (position < code.Length - 1 && char.IsDigit(code[position + 1]))
            {
                position++; // Skip .
                while (
                    position < code.Length
                    && (char.IsDigit(code[position]) || code[position] == '_')
                )
                {
                    position++;
                }
            }
        }

        // Exponent part
        if (position < code.Length && (code[position] == 'e' || code[position] == 'E'))
        {
            position++;
            if (position < code.Length && (code[position] == '+' || code[position] == '-'))
            {
                position++;
            }

            while (
                position < code.Length && (char.IsDigit(code[position]) || code[position] == '_')
            )
            {
                position++;
            }
        }
    }

    /// <summary>
    ///     Highlights an interpolated string with embedded expressions.
    /// </summary>
    private static void HighlightInterpolatedString(
        string code,
        ref int position,
        List<ColoredSegment> segments
    )
    {
        int start = position;
        position += 2; // Skip $"

        int stringStart = start;

        while (position < code.Length)
        {
            if (code[position] == '\\' && position < code.Length - 1)
            {
                position += 2; // Skip escape sequence
                continue;
            }

            if (code[position] == '{' && position < code.Length - 1 && code[position + 1] != '{')
            {
                // End current string segment
                if (position > stringStart)
                {
                    segments.Add(
                        new ColoredSegment(
                            code.Substring(stringStart, position - stringStart),
                            StringColor,
                            stringStart,
                            position - stringStart
                        )
                    );
                }

                // Find matching }
                int braceStart = position;
                int depth = 1;
                position++;

                while (position < code.Length && depth > 0)
                {
                    if (code[position] == '{')
                    {
                        depth++;
                    }
                    else if (code[position] == '}')
                    {
                        depth--;
                    }

                    if (depth > 0)
                    {
                        position++;
                    }
                }

                if (depth == 0)
                {
                    // Highlight the interpolation expression
                    segments.Add(
                        new ColoredSegment(
                            code.Substring(braceStart, position - braceStart + 1),
                            InterpolationColor,
                            braceStart,
                            position - braceStart + 1
                        )
                    );
                    position++;
                }

                stringStart = position;
                continue;
            }

            if (code[position] == '"')
            {
                position++; // Include closing quote
                break;
            }

            position++;
        }

        // Add remaining string content
        if (position > stringStart)
        {
            segments.Add(
                new ColoredSegment(
                    code.Substring(stringStart, position - stringStart),
                    StringColor,
                    stringStart,
                    position - stringStart
                )
            );
        }
    }

    private static bool IsHexDigit(char c)
    {
        return char.IsDigit(c) || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
    }
}
