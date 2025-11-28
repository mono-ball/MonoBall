namespace PokeSharp.Engine.Debug.Commands;

/// <summary>
///     Provides access to console command registry.
/// </summary>
public interface IConsoleCommands
{
    /// <summary>
    ///     Gets all registered console commands.
    /// </summary>
    IEnumerable<IConsoleCommand> GetAllCommands();

    /// <summary>
    ///     Gets a specific command by name.
    /// </summary>
    IConsoleCommand? GetCommand(string name);
}
