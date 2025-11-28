using FontStashSharp;
using Microsoft.Xna.Framework;

namespace PokeSharp.Engine.UI.Debug.Utilities;

/// <summary>
/// Utility class for measuring text dimensions.
/// </summary>
public static class TextMeasurement
{
    /// <summary>
    /// Measures the size of a string when rendered with a specific font.
    /// </summary>
    public static Vector2 MeasureString(SpriteFontBase font, string text)
    {
        if (string.IsNullOrEmpty(text))
            return Vector2.Zero;

        return font.MeasureString(text);
    }

    /// <summary>
    /// Calculates the height needed for multi-line text with word wrapping.
    /// </summary>
    public static float MeasureWrappedHeight(SpriteFontBase font, string text, float maxWidth)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        var lines = WrapText(font, text, maxWidth);
        return lines.Count * font.LineHeight;
    }

    /// <summary>
    /// Wraps text to fit within a maximum width, returning individual lines.
    /// </summary>
    public static List<string> WrapText(SpriteFontBase font, string text, float maxWidth)
    {
        var lines = new List<string>();

        if (string.IsNullOrEmpty(text))
            return lines;

        var words = text.Split(' ');
        var currentLine = "";

        foreach (var word in words)
        {
            var testLine = string.IsNullOrEmpty(currentLine) ? word : $"{currentLine} {word}";
            var size = font.MeasureString(testLine);

            if (size.X > maxWidth && !string.IsNullOrEmpty(currentLine))
            {
                lines.Add(currentLine);
                currentLine = word;
            }
            else
            {
                currentLine = testLine;
            }
        }

        if (!string.IsNullOrEmpty(currentLine))
            lines.Add(currentLine);

        return lines;
    }
}




