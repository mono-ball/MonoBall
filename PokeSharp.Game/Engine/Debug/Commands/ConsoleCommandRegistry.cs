using System.Reflection;
using Microsoft.Extensions.Logging;

namespace PokeSharp.Game.Engine.Debug.Commands;

/// <summary>
///     Registry that discovers and manages console commands.
/// </summary>
public class ConsoleCommandRegistry
{
    private readonly Dictionary<string, IConsoleCommand> _commands = new(
        StringComparer.OrdinalIgnoreCase
    );

    private readonly ILogger _logger;

    public ConsoleCommandRegistry(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        DiscoverCommands();
    }

    /// <summary>
    ///     Gets all registered commands.
    /// </summary>
    public IReadOnlyDictionary<string, IConsoleCommand> Commands => _commands;

    /// <summary>
    ///     Registers a command instance.
    /// </summary>
    public void RegisterCommand(IConsoleCommand command)
    {
        if (_commands.ContainsKey(command.Name))
        {
            _logger.LogWarning("Command '{Name}' is already registered, overwriting", command.Name);
        }

        _commands[command.Name] = command;
        _logger.LogDebug("Registered command: {Name}", command.Name);
    }

    /// <summary>
    ///     Tries to get a command by name.
    /// </summary>
    public bool TryGetCommand(string name, out IConsoleCommand? command)
    {
        return _commands.TryGetValue(name, out command);
    }

    /// <summary>
    ///     Gets a command by name, or null if not found.
    /// </summary>
    public IConsoleCommand? GetCommand(string name)
    {
        _commands.TryGetValue(name, out IConsoleCommand? command);
        return command;
    }

    /// <summary>
    ///     Gets all registered commands.
    /// </summary>
    public IEnumerable<IConsoleCommand> GetAllCommands()
    {
        return _commands.Values;
    }

    /// <summary>
    ///     Executes a command by name with given arguments.
    /// </summary>
    public async Task<bool> ExecuteAsync(string commandName, string[] args, IConsoleContext context)
    {
        if (TryGetCommand(commandName, out IConsoleCommand? command))
        {
            try
            {
                await command!.ExecuteAsync(context, args);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing command: {CommandName}", commandName);
                context.WriteLine($"Error executing command: {ex.Message}", context.Theme.Error);
                return false;
            }
        }

        return false; // Command not found
    }

    /// <summary>
    ///     Automatically discovers and registers commands with [ConsoleCommand] attribute.
    /// </summary>
    private void DiscoverCommands()
    {
        IEnumerable<Type> commandTypes = Assembly
            .GetExecutingAssembly()
            .GetTypes()
            .Where(t => t.GetCustomAttribute<ConsoleCommandAttribute>() != null)
            .Where(t => typeof(IConsoleCommand).IsAssignableFrom(t))
            .Where(t => !t.IsAbstract && !t.IsInterface);

        foreach (Type type in commandTypes)
        {
            try
            {
                var command = (IConsoleCommand)Activator.CreateInstance(type)!;
                RegisterCommand(command);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register command type: {TypeName}", type.Name);
            }
        }

        _logger.LogInformation(
            "Discovered and registered {Count} console commands",
            _commands.Count
        );
    }
}
