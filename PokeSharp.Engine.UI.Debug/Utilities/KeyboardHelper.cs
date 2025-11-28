using Microsoft.Xna.Framework.Input;

namespace PokeSharp.Engine.UI.Debug.Utilities;

/// <summary>
///     Utility class for keyboard input processing.
///     Provides shared keyboard-to-character conversion logic.
/// </summary>
public static class KeyboardHelper
{
    /// <summary>
    ///     Converts a keyboard key to its character representation.
    /// </summary>
    /// <param name="key">The key pressed</param>
    /// <param name="shift">Whether shift is held</param>
    /// <returns>The character, or null if the key doesn't map to a character</returns>
    public static char? KeyToChar(Keys key, bool shift)
    {
        // Letters
        if (key >= Keys.A && key <= Keys.Z)
        {
            char c = (char)('a' + (key - Keys.A));
            return shift ? char.ToUpper(c) : c;
        }

        // Numbers and symbols
        if (key >= Keys.D0 && key <= Keys.D9)
        {
            if (shift)
            {
                return key switch
                {
                    Keys.D0 => ')',
                    Keys.D1 => '!',
                    Keys.D2 => '@',
                    Keys.D3 => '#',
                    Keys.D4 => '$',
                    Keys.D5 => '%',
                    Keys.D6 => '^',
                    Keys.D7 => '&',
                    Keys.D8 => '*',
                    Keys.D9 => '(',
                    _ => null,
                };
            }

            return (char)('0' + (key - Keys.D0));
        }

        // Numpad
        if (key >= Keys.NumPad0 && key <= Keys.NumPad9)
        {
            return (char)('0' + (key - Keys.NumPad0));
        }

        // Special keys
        return key switch
        {
            Keys.Space => ' ',
            Keys.OemPeriod => shift ? '>' : '.',
            Keys.OemComma => shift ? '<' : ',',
            Keys.OemSemicolon => shift ? ':' : ';',
            Keys.OemQuotes => shift ? '"' : '\'',
            Keys.OemQuestion => shift ? '?' : '/',
            Keys.OemPlus => shift ? '+' : '=',
            Keys.OemMinus => shift ? '_' : '-',
            Keys.OemOpenBrackets => shift ? '{' : '[',
            Keys.OemCloseBrackets => shift ? '}' : ']',
            Keys.OemPipe => shift ? '|' : '\\',
            Keys.OemTilde => shift ? '~' : '`',
            _ => null,
        };
    }
}
