using Microsoft.Xna.Framework;
using PokeSharp.Engine.UI.Debug.Core;

namespace PokeSharp.Engine.Debug.Commands;

/// <summary>
///     Provides basic console output operations.
/// </summary>
public interface IConsoleOutput
{
    /// <summary>
    ///     Gets the current UI theme.
    /// </summary>
    UITheme Theme { get; }

    /// <summary>
    ///     Writes a line of text to the console output with default color.
    /// </summary>
    void WriteLine(string text);

    /// <summary>
    ///     Writes a line of text to the console output with specified color.
    /// </summary>
    void WriteLine(string text, Color color);

    /// <summary>
    ///     Clears all console output.
    /// </summary>
    void Clear();

    /// <summary>
    ///     Requests the console to close.
    /// </summary>
    void Close();
}
