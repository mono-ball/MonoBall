using TextCopy;

namespace PokeSharp.Engine.UI.Debug.Utilities;

/// <summary>
///     Cross-platform clipboard manager for copying and pasting text.
///     Uses the TextCopy library for cross-platform clipboard access.
/// </summary>
public static class ClipboardManager
{
    /// <summary>
    ///     Copies text to the system clipboard.
    /// </summary>
    /// <param name="text">The text to copy.</param>
    /// <returns>True if successful, false otherwise.</returns>
    public static bool SetText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        try
        {
            ClipboardService.SetText(text);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    ///     Gets text from the system clipboard.
    /// </summary>
    /// <returns>The clipboard text, or empty string if failed.</returns>
    public static string GetText()
    {
        try
        {
            string? text = ClipboardService.GetText();
            return text ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    ///     Checks if the clipboard has text available.
    /// </summary>
    /// <returns>True if clipboard has text, false otherwise.</returns>
    public static bool HasText()
    {
        try
        {
            string? text = ClipboardService.GetText();
            return !string.IsNullOrEmpty(text);
        }
        catch
        {
            return false;
        }
    }
}
