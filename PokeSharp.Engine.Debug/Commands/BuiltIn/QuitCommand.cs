namespace PokeSharp.Engine.Debug.Commands.BuiltIn;

/// <summary>
///     Closes the console (alias for exit).
/// </summary>
[ConsoleCommand("quit", "Close the console")]
public class QuitCommand : IConsoleCommand
{
    public string Name => "quit";
    public string Description => "Close the console";
    public string Usage => "quit";

    public Task ExecuteAsync(IConsoleContext context, string[] args)
    {
        context.Close();
        return Task.CompletedTask;
    }
}
