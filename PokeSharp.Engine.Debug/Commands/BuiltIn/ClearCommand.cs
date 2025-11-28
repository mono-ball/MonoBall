namespace PokeSharp.Engine.Debug.Commands.BuiltIn;

/// <summary>
///     Clears all console output.
/// </summary>
[ConsoleCommand("clear", "Clear all console output")]
public class ClearCommand : IConsoleCommand
{
    public string Name => "clear";
    public string Description => "Clear all console output";
    public string Usage => "clear";

    public Task ExecuteAsync(IConsoleContext context, string[] args)
    {
        context.Clear();
        return Task.CompletedTask;
    }
}
