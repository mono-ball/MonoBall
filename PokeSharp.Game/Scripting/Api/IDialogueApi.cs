namespace PokeSharp.Game.Scripting.Api;

/// <summary>
///     Dialogue and message display API for scripts.
///     Provides access to displaying messages, dialogue boxes, and UI notifications.
/// </summary>
public interface IDialogueApi
{
    /// <summary>
    ///     Check if a dialogue is currently being displayed.
    /// </summary>
    /// <returns>True if dialogue is active, false otherwise.</returns>
    bool IsDialogueActive { get; }

    /// <summary>
    ///     Display a message to the player.
    /// </summary>
    /// <param name="message">The message text to display.</param>
    /// <param name="speakerName">Optional speaker name for attribution.</param>
    /// <param name="priority">Display priority (higher values show first).</param>
    void ShowMessage(string message, string? speakerName = null, int priority = 0);

    /// <summary>
    ///     Clear all pending dialogue messages.
    /// </summary>
    void ClearMessages();
}
