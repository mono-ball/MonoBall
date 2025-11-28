using Microsoft.Xna.Framework.Input;

namespace PokeSharp.Engine.UI.Debug.Components.Debug;

/// <summary>
///     Defines console keyboard shortcuts and generates hint text.
///     Centralizes shortcut definitions to prevent hints from becoming stale.
/// </summary>
public static class ConsoleShortcuts
{
    // ═══════════════════════════════════════════════════════════════════════
    // Normal Mode Shortcuts
    // ═══════════════════════════════════════════════════════════════════════

    public static readonly Shortcut Search = new(Keys.F, true, false, "Search");
    public static readonly Shortcut HistorySearch = new(Keys.R, true, false, "History");
    public static readonly Shortcut Complete = new(Keys.Tab, false, false, "Complete");
    public static readonly Shortcut HistoryUp = new(Keys.Up, false, false, "History");
    public static readonly Shortcut HistoryDown = new(Keys.Down, false, false, "History");
    public static readonly Shortcut Submit = new(Keys.Enter, false, false, "Submit");

    // ═══════════════════════════════════════════════════════════════════════
    // Multi-line Mode Shortcuts
    // ═══════════════════════════════════════════════════════════════════════

    public static readonly Shortcut MultiLineSubmit = new(Keys.Enter, true, false, "Submit");
    public static readonly Shortcut NewLine = new(Keys.Enter, false, true, "New line");

    // ═══════════════════════════════════════════════════════════════════════
    // Command History Search Shortcuts
    // ═══════════════════════════════════════════════════════════════════════

    public static readonly Shortcut ToggleHistorySearch = new(Keys.R, true, false, "Toggle search");
    public static readonly Shortcut NavigateUp = new(Keys.Up, false, false, "Navigate");
    public static readonly Shortcut NavigateDown = new(Keys.Down, false, false, "Navigate");
    public static readonly Shortcut SelectItem = new(Keys.Enter, false, false, "Select");
    public static readonly Shortcut Cancel = new(Keys.Escape, false, false, "Cancel");

    // ═══════════════════════════════════════════════════════════════════════
    // Suggestions Mode Shortcuts
    // ═══════════════════════════════════════════════════════════════════════

    public static readonly Shortcut Documentation = new(Keys.F1, false, false, "Docs");

    // ═══════════════════════════════════════════════════════════════════════
    // Hint Text Generation
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    ///     Gets hint text for normal console mode.
    /// </summary>
    public static string GetNormalModeHints()
    {
        return string.Join(
            " | ",
            Search.ToHintString(),
            HistorySearch.ToHintString(),
            Complete.ToHintString(),
            "[Up/Down] History",
            Submit.ToHintString()
        );
    }

    /// <summary>
    ///     Gets hint text for multi-line editing mode.
    /// </summary>
    public static string GetMultiLineModeHints(int lineCount)
    {
        return $"({lineCount} lines) {MultiLineSubmit.ToHintString()} | {NewLine.ToHintString()}";
    }

    /// <summary>
    ///     Gets hint text for command history search mode.
    /// </summary>
    public static string GetHistorySearchModeHints()
    {
        return string.Join(
            " | ",
            ToggleHistorySearch.ToHintString(),
            "[Type] Filter",
            "[Up/Down] Navigate",
            SelectItem.ToHintString(),
            Cancel.ToHintString()
        );
    }

    /// <summary>
    ///     Gets hint text for suggestions mode.
    /// </summary>
    public static string GetSuggestionsModeHints()
    {
        return string.Join(
            " | ",
            "[Up/Down] Navigate",
            "[Tab/Enter] Select",
            Documentation.ToHintString(),
            Cancel.ToHintString()
        );
    }

    /// <summary>
    ///     Represents a keyboard shortcut with its display text and action description.
    /// </summary>
    public readonly record struct Shortcut(Keys Key, bool Ctrl, bool Shift, string Action)
    {
        public string ToHintString()
        {
            string modifiers = "";
            if (Ctrl)
            {
                modifiers += "Ctrl+";
            }

            if (Shift)
            {
                modifiers += "Shift+";
            }

            string keyName = Key switch
            {
                Keys.F => "F",
                Keys.R => "R",
                Keys.Tab => "Tab",
                Keys.Up => "Up",
                Keys.Down => "Down",
                Keys.Enter => "Enter",
                Keys.Escape => "Esc",
                Keys.F1 => "F1",
                _ => Key.ToString(),
            };

            return $"[{modifiers}{keyName}] {Action}";
        }
    }
}
