namespace PokeSharp.Engine.Debug.Commands;

/// <summary>
///     Attribute to mark classes as console commands for automatic discovery.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class ConsoleCommandAttribute : Attribute
{
    /// <summary>
    ///     Initializes a new instance of the ConsoleCommandAttribute.
    /// </summary>
    public ConsoleCommandAttribute(string name, string description = "")
    {
        Name = name;
        Description = description;
    }

    /// <summary>
    ///     Gets the command name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    ///     Gets the command description.
    /// </summary>
    public string Description { get; }
}
