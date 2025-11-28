namespace PokeSharp.Engine.UI.Debug.Core;

/// <summary>
///     Standard interface for components that accept text input.
///     Ensures consistent API across different input implementations (CommandInput, TextEditor, etc.)
/// </summary>
public interface ITextInput
{
    /// <summary>
    ///     Gets the current text content.
    /// </summary>
    string Text { get; }

    /// <summary>
    ///     Gets the current cursor position (character index).
    /// </summary>
    int CursorPosition { get; }

    /// <summary>
    ///     Gets whether there is currently selected text.
    /// </summary>
    bool HasSelection { get; }

    /// <summary>
    ///     Gets the currently selected text, or empty string if no selection.
    /// </summary>
    string SelectedText { get; }

    /// <summary>
    ///     Sets the text content programmatically.
    /// </summary>
    void SetText(string text);

    /// <summary>
    ///     Clears all text content.
    /// </summary>
    void Clear();

    /// <summary>
    ///     Requests focus for this input component.
    /// </summary>
    void Focus();

    /// <summary>
    ///     Event fired when text content changes.
    /// </summary>
    event Action<string>? OnTextChanged;

    /// <summary>
    ///     Event fired when user submits the input (e.g., presses Enter).
    /// </summary>
    event Action<string>? OnSubmit;
}
