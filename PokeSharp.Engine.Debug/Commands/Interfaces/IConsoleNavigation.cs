namespace PokeSharp.Engine.Debug.Commands;

/// <summary>
///     Provides console navigation and display operations.
/// </summary>
public interface IConsoleNavigation
{
    /// <summary>
    ///     Switches to a specific tab by index.
    /// </summary>
    void SwitchToTab(int tabIndex);

    /// <summary>
    ///     Gets the current active tab index.
    /// </summary>
    int GetActiveTab();

    /// <summary>
    ///     Sets the console height as a percentage of screen height.
    /// </summary>
    /// <param name="heightPercent">Height percentage (0.25 to 1.0)</param>
    void SetConsoleHeight(float heightPercent);
}
