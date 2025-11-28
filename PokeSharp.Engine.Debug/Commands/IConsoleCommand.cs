namespace PokeSharp.Engine.Debug.Commands;

/// <summary>
///     Interface for console commands that can be executed by the debug console.
/// </summary>
public interface IConsoleCommand
{
    /// <summary>
    ///     Gets the name of the command (e.g., "help", "clear").
    /// </summary>
    string Name { get; }

    /// <summary>
    ///     Gets a brief description of what this command does.
    /// </summary>
    string Description { get; }

    /// <summary>
    ///     Gets the usage/syntax information for this command.
    /// </summary>
    string Usage { get; }

    /// <summary>
    ///     Executes the command with the given arguments.
    /// </summary>
    /// <param name="context">The console context providing access to console services.</param>
    /// <param name="args">Command arguments (excluding the command name itself).</param>
    Task ExecuteAsync(IConsoleContext context, string[] args);
}
