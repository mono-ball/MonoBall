namespace PokeSharp.Engine.Debug.Commands.BuiltIn;

/// <summary>
///     Closes the console.
/// </summary>
[ConsoleCommand("exit", "Close the console")]
public class ExitCommand : IConsoleCommand
{
    public string Name => "exit";
    public string Description => "Close the console";
    public string Usage => "exit";

    public Task ExecuteAsync(IConsoleContext context, string[] args)
    {
        context.Close();
        return Task.CompletedTask;
    }
}
