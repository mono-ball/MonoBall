using Microsoft.Xna.Framework;

namespace PokeSharp.Engine.UI.Debug.Core;

/// <summary>
///     Standard interface for components that display text output.
///     Ensures consistent API across different display implementations (TextBuffer, OutputPanel, etc.)
/// </summary>
public interface ITextDisplay
{
    /// <summary>
    ///     Gets the total number of lines in the display.
    /// </summary>
    int TotalLines { get; }

    /// <summary>
    ///     Appends a line of text with default color.
    /// </summary>
    void AppendLine(string text);

    /// <summary>
    ///     Appends a line of text with specified color.
    /// </summary>
    void AppendLine(string text, Color color);

    /// <summary>
    ///     Appends a line of text with color and category.
    /// </summary>
    void AppendLine(string text, Color color, string category);

    /// <summary>
    ///     Clears all displayed text.
    /// </summary>
    void Clear();

    /// <summary>
    ///     Scrolls the display to show the bottom (most recent content).
    /// </summary>
    void ScrollToBottom();

    /// <summary>
    ///     Scrolls the display to show the top (oldest content).
    /// </summary>
    void ScrollToTop();
}
